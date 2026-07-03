using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)
	{
		NetworkManager? networkManager = GetNetworkManager();
		if (!NetworkReconnectRules.ShouldQueueRealtimeBoardEventForClient(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			networkManager?.State ?? NetworkSessionState.Idle))
		{
			return;
		}

		int snapshotId = ReadNetworkInt(payload, "snapshot_id", -1);
		if (snapshotId >= 0 && snapshotId <= _networkLastAppliedSnapshotId)
		{
			return;
		}

		_networkVisualEventQueue.Enqueue(payload);
		if (!_networkVisualQueueProcessing)
		{
			_ = ProcessNetworkVisualQueueAsync();
		}
	}

	internal void RejectNetworkBoardAction(string reason, int turnSequenceGeneration, int rejectedHandIndex)
	{
		_networkPendingSelectionRequestInFlight = false;

		if (rejectedHandIndex == _networkPendingHandIndex
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration)
		{
			RollbackPredictedNetworkHandCardPlay();
			_networkPendingHandIndex = -1;
			_networkPendingTurnSequenceGeneration = -1;
		}

		if (rejectedHandIndex == _networkPendingItemHandIndex
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration)
		{
			RollbackPredictedNetworkItemCardPlay();
			_networkPendingItemHandIndex = -1;
			_networkPendingTurnSequenceGeneration = -1;
		}

		if (rejectedHandIndex < 0
			&& turnSequenceGeneration == _networkPendingTurnSequenceGeneration
			&& _networkLocallyCommittedPendingSelectionMode != PendingItemSelectionMode.None)
		{
			RollbackPredictedNetworkItemCardPlay();
		}

		PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
		RenderPlayerHands();
	}

	private async Task ProcessNetworkVisualQueueAsync()
	{
		_networkVisualQueueProcessing = true;
		try
		{
			while (_networkVisualEventQueue.Count > 0)
			{
				Godot.Collections.Dictionary payload = _networkVisualEventQueue.Dequeue();
				int snapshotId = ReadNetworkInt(payload, "snapshot_id", -1);
				if (snapshotId >= 0 && snapshotId <= _networkLastAppliedSnapshotId)
				{
					continue;
				}

				if (_networkQueuedSnapshot != null
					&& ReadNetworkInt(_networkQueuedSnapshot, "snapshot_id", -1) > snapshotId)
				{
					continue;
				}

				await PlayNetworkCardPlayedEventAsync(payload);
			}
		}
		finally
		{
			_networkVisualQueueProcessing = false;
			CompleteReconnectCatchupIfReady();
			if (_networkQueuedSnapshot != null)
			{
				Godot.Collections.Dictionary snapshot = _networkQueuedSnapshot;
				_networkQueuedSnapshot = null;
				if (IsUsableNetworkSnapshot(snapshot, out int snapshotId))
				{
					ApplyNetworkBoardSnapshotNow(snapshot, snapshotId);
				}
			}
		}
	}

	private async Task PlayNetworkCardPlayedEventAsync(Godot.Collections.Dictionary payload)
	{
		string eventType = ReadNetworkString(payload, NetworkBoardVisualEventTypes.EventTypeKey, NetworkBoardVisualEventTypes.PlayHandCard);
		switch (eventType)
		{
			case NetworkBoardVisualEventTypes.PlayItemCard:
				await PlayNetworkItemCardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.CaptureReveal:
				await PlayNetworkCaptureRevealVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.CaptureToDeck:
				await PlayNetworkCaptureVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.DrawCard:
				await PlayNetworkDrawCardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.DeckShuffle:
				await PlayNetworkDeckShuffleVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.StealHandCard:
				await PlayNetworkStealHandCardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.StealDeckCards:
				await PlayNetworkStealDeckCardsVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.LensReveal:
				await PlayNetworkLensRevealVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.TableShuffle:
				await PlayNetworkTableShuffleVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.QuestionRefresh:
				await PlayNetworkQuestionRefreshVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.QuestionReward:
				await PlayNetworkQuestionRewardVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.BombPair:
				await PlayNetworkBombPairVisualEventAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.MantaTideShift:
				await PlayNetworkMantaTideShiftVisualEventAsync(payload);
				return;
			default:
				break;
		}

		int actorSeatIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int handIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1);
		string playedCardCode = ReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, string.Empty);
		bool playedCardIsFaceUp = ReadNetworkBool(payload, NetworkBoardVisualEventTypes.PlayedCardIsFaceUpKey, false);
		int tableCardIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TableCardIndexKey, _tableCards.Count);
		string borrowTideAdjustedRank = ReadNetworkString(payload, NetworkBoardVisualEventTypes.BorrowTideAdjustedRankKey, string.Empty);
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count)
		{
			return;
		}

		PlayingCard playedCard = CreateNetworkPlayingCard(playedCardCode);
		int clampedTableCardIndex = Mathf.Clamp(tableCardIndex, 0, _tableCards.Count);
		bool matchesLocalPrediction = actorSeatIndex == GetLocalHandPlayerIndex()
			&& handIndex == _networkPendingHandIndex
			&& _networkPredictedHandVisualActive
			&& clampedTableCardIndex == _networkPredictedHandTableCardIndex
			&& string.Equals(borrowTideAdjustedRank, _networkPredictedHandBorrowTideAdjustedRank, StringComparison.Ordinal);
		if (!matchesLocalPrediction)
		{
			_tableCards.Insert(clampedTableCardIndex, new TableCard(playedCard, playedCardIsFaceUp, cardOwnerPlayerIndex: actorSeatIndex));
			if (actorSeatIndex == GetLocalHandPlayerIndex()
				&& handIndex == _networkPendingHandIndex)
			{
				HideHandDisplaySlot(actorSeatIndex, handIndex);
			}
		}

		if (!matchesLocalPrediction)
		{
			Vector2 startGlobalPosition = TryGetHandCardStartGlobalPosition(actorSeatIndex, handIndex)
				?? GetVisibleCardPileTopGlobalPosition(actorSeatIndex);
			double debugPlaybackDelaySeconds = GetDebugNetworkVisualPlaybackDelaySeconds();
			if (debugPlaybackDelaySeconds > 0.0)
			{
				await ToSignal(GetTree().CreateTimer(debugPlaybackDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			}

			await PlayCardToTableAnimationAsync(actorSeatIndex, playedCard, startGlobalPosition, clampedTableCardIndex, playedCardIsFaceUp);
		}
		else
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedHandVisualState();
		}
		RenderTableCards();
	}

	private async Task PlayNetworkItemCardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int actorSeatIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int itemHandIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1);
		string itemCardId = ReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, string.Empty);
		string playPresentation = ReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayPresentationKey, CardPlayPresentationTypes.DirectTrigger);
		int targetTableCardIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexKey, -1);
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count || string.IsNullOrWhiteSpace(itemCardId))
		{
			return;
		}

		ItemCard itemCard = CreateNetworkVisualItemCard(itemCardId);
		bool matchesLocalPrediction = actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex
			&& _networkPredictedItemVisualActive
			&& playPresentation != CardPlayPresentationTypes.TargetedTrigger;
		if (!matchesLocalPrediction
			&& actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex)
		{
			HideHandDisplaySlot(actorSeatIndex, _playerHands[actorSeatIndex].Count + itemHandIndex);
			RenderPlayerHands();
		}

		if (matchesLocalPrediction)
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedItemVisualState();
			ResetNetworkLocallyCommittedPendingSelectionState();
			RenderTableCards();
			return;
		}

		if (actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex
			&& _networkPredictedItemVisualActive
			&& playPresentation == CardPlayPresentationTypes.TargetedTrigger)
		{
			DisposeActiveSelectionOverlay();
			ClearPendingItemSelectionState();
			ClearNetworkPendingActionState();
			ResetNetworkPredictedItemVisualState();
			RenderPlayerHands();
		}

		bool matchesLocalCommittedSelection = actorSeatIndex == GetLocalHandPlayerIndex()
			&& _networkLocallyCommittedPendingSelectionMode != PendingItemSelectionMode.None
			&& playPresentation == CardPlayPresentationTypes.TargetedTrigger;
		if (matchesLocalCommittedSelection)
		{
			ResetNetworkLocallyCommittedPendingSelectionState();
		}

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(actorSeatIndex, itemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(actorSeatIndex);
		if (playPresentation == CardPlayPresentationTypes.TableOverlay && targetTableCardIndex >= 0)
		{
			InsertNetworkTableOverlayIfMissing(actorSeatIndex, targetTableCardIndex, itemCard);
			await PlayItemCardToTableAnimationAsync(actorSeatIndex, itemCard, startGlobalPosition, targetTableCardIndex, tableCardIsFaceUp: false);
			RenderTableCards();
			return;
		}

		if (playPresentation == CardPlayPresentationTypes.TargetedTrigger)
		{
			await PlaySpecialCardTriggerEffectAsync(
				itemCard.Texture,
				startGlobalPosition,
				0,
				GetCardRotationDegreesForPlayer(actorSeatIndex));
			return;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		await PlayItemCardToTransformAreaAnimationAsync(
			itemCard.Texture,
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(actorSeatIndex));
		if (actorSeatIndex == GetLocalHandPlayerIndex())
		{
			await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, transformAreaGlobalPosition, 0);
		}
	}

	private async Task PlayNetworkDrawCardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int displayIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.DisplayIndexKey, -1);
		string drawnCardCode = ReadNetworkString(payload, NetworkBoardVisualEventTypes.DrawnCardCodeKey, string.Empty);
		if (playerIndex < 0 || playerIndex >= player_count || displayIndex < 0)
		{
			return;
		}

		EnsureNetworkHandCardPlaceholder(playerIndex, displayIndex, drawnCardCode);
		DrawnHandCard drawnCard = new(
			string.IsNullOrWhiteSpace(drawnCardCode)
				? CreatePlaceholderPlayingCard()
				: CreateNetworkPlayingCard(drawnCardCode),
			displayIndex);
		await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, _turnSequenceGeneration);
	}

	private async Task PlayNetworkDeckShuffleVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		if (playerIndex < 0 || playerIndex >= player_count)
		{
			return;
		}

		await PlayPhysicalDeckShuffleAsync(playerIndex);
	}

	private async Task PlayNetworkStealHandCardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int ownerPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int targetPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetSeatIndexKey, -1);
		int targetHandIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetHandIndexKey, -1);
		string itemCardId = ReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, string.Empty);
		string stolenCardCode = ReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, string.Empty);
		string[] attachedItemCardIds = ReadStringArray(payload, NetworkBoardVisualEventTypes.AttachedItemCardIdsKey);
		if (ownerPlayerIndex != GetLocalHandPlayerIndex()
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= player_count
			|| targetHandIndex < 0)
		{
			return;
		}

		bool stealsItemCard = !string.IsNullOrWhiteSpace(itemCardId);
		if (stealsItemCard)
		{
			int sourceItemHandIndex = targetHandIndex - _playerHands[targetPlayerIndex].Count;
			ItemCard stolenItemCard = sourceItemHandIndex >= 0 && sourceItemHandIndex < _playerItemHands[targetPlayerIndex].Count
				? _playerItemHands[targetPlayerIndex][sourceItemHandIndex]
				: CreateNetworkVisualItemCard(itemCardId);
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenItemCardTexture(targetPlayerIndex, stolenItemCard),
				null,
				GetVisibleStolenItemCardScale(targetPlayerIndex, stolenItemCard),
				GetStolenItemCardTargetDisplayIndex(ownerPlayerIndex));
			if (_turnSequenceGeneration != ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration)
				|| IsGameOver())
			{
				RenderPlayerHands();
				return;
			}

			if (sourceItemHandIndex >= 0 && sourceItemHandIndex < _playerItemHands[targetPlayerIndex].Count)
			{
				_playerItemHands[targetPlayerIndex].RemoveAt(sourceItemHandIndex);
				ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			}

			_playerItemHands[ownerPlayerIndex].Add(stolenItemCard);
		}
		else
		{
			PlayingCard stolenCard = targetHandIndex >= 0 && targetHandIndex < _playerHands[targetPlayerIndex].Count
				? _playerHands[targetPlayerIndex][targetHandIndex]
				: CreateNetworkPlayingCard(stolenCardCode, attachedItemCardIds);
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenPlayingCardTexture(targetPlayerIndex, stolenCard),
				GetVisibleStolenPlayingCardAttachmentOverlayTexture(targetPlayerIndex, stolenCard),
				1.0f,
				GetStolenPlayingCardTargetDisplayIndex(ownerPlayerIndex));
			if (_turnSequenceGeneration != ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration)
				|| IsGameOver())
			{
				RenderPlayerHands();
				return;
			}

			if (targetHandIndex >= 0 && targetHandIndex < _playerHands[targetPlayerIndex].Count)
			{
				_playerHands[targetPlayerIndex].RemoveAt(targetHandIndex);
				ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			}

			_playerHands[ownerPlayerIndex].Add(stolenCard);
		}

		PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerHands();
	}

	private async Task PlayNetworkStealDeckCardsVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int ownerPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int targetPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TargetSeatIndexKey, -1);
		int movedCardCount = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.MovedCardCountKey, 0);
		int turnSequenceGeneration = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration);
		if (ownerPlayerIndex != GetLocalHandPlayerIndex()
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= player_count
			|| movedCardCount <= 0)
		{
			return;
		}

		PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
		Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(targetPlayerIndex);
		Vector2 targetGlobalPosition = GetDrawCardStartGlobalPosition(ownerPlayerIndex);
		float startRotationDegrees = GetCardPileRotationDegreesForPlayer(targetPlayerIndex);
		float targetRotationDegrees = GetCardPileRotationDegreesForPlayer(ownerPlayerIndex);
		int appliedMoveCount = Mathf.Min(movedCardCount, _playerDecks[targetPlayerIndex].Count);
		for (int cardIndex = 0; cardIndex < appliedMoveCount; cardIndex++)
		{
			await PlayStolenDeckCardFlightAsync(
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				targetRotationDegrees);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				RenderPlayerCardPiles();
				return;
			}

			PlayingCard topCard = _playerDecks[targetPlayerIndex][0];
			_playerDecks[targetPlayerIndex].RemoveAt(0);
			AddCardToPlayerDeck(ownerPlayerIndex, topCard);
		}

		if (appliedMoveCount > 0)
		{
			Shuffle(_playerDecks[ownerPlayerIndex]);
			Shuffle(_playerDecks[targetPlayerIndex]);
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

		RenderPlayerCardPiles();
	}

	private async Task PlayNetworkLensRevealVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int ownerPlayerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		float splitCenterX = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.SplitCenterXKey, 0.0f);
		float splitCenterY = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.SplitCenterYKey, 0.0f);
		Godot.Collections.Array targetIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexesKey);
		int[] targetIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexesKey, targetIndexesArray.Count);
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= player_count || targetIndexes.Length == 0)
		{
			return;
		}

		await RevealLensTargetCardsAsync(targetIndexes, new Vector2(splitCenterX, splitCenterY), _turnSequenceGeneration);
	}

	private async Task PlayNetworkTableShuffleVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		Godot.Collections.Array targetIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey);
		int[] targetIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey, targetIndexesArray.Count);
		List<TableCard> shuffledTableCards = BuildNetworkShuffledTableCards(targetIndexes);
		if (shuffledTableCards.Count != _tableCards.Count)
		{
			return;
		}

		await PlayTableCardsShuffleAnimationAsync(shuffledTableCards, _turnSequenceGeneration);
	}

	private async Task PlayNetworkQuestionRefreshVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int tableCardIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TableCardIndexKey, -1);
		if (tableCardIndex < 0)
		{
			return;
		}

		EnsureNetworkQuestionTableCard(tableCardIndex);
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		await PlayQuestionCardRefreshAnimationAsync(tableCardIndex);
		RenderTableCards();
	}

	private async Task PlayNetworkQuestionRewardVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		string itemCardId = ReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, string.Empty);
		int itemHandIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1);
		float startGlobalX = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.StartGlobalXKey, 0.0f);
		float startGlobalY = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.StartGlobalYKey, 0.0f);
		bool requiresMagnetAttachment = ReadNetworkBool(payload, NetworkBoardVisualEventTypes.RequiresMagnetAttachmentKey, false);
		if (playerIndex != GetLocalHandPlayerIndex() || string.IsNullOrWhiteSpace(itemCardId))
		{
			return;
		}

		ItemCard rewardItem = CreateNetworkVisualItemCard(itemCardId);
		if (!requiresMagnetAttachment && playerIndex >= 0 && playerIndex < _playerItemHands.Count && itemHandIndex >= 0)
		{
			while (_playerItemHands[playerIndex].Count <= itemHandIndex)
			{
				_playerItemHands[playerIndex].Add(rewardItem);
			}

			HideHandDisplaySlot(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
			RenderPlayerHands();
		}

		await PlayQuestionCardRewardAnimationAsync(
			playerIndex,
			new QuestionCardRewardAnimation(
				rewardItem,
				itemHandIndex,
				new Vector2(startGlobalX, startGlobalY),
				requiresMagnetAttachment),
			_turnSequenceGeneration);
	}

	private async Task PlayNetworkBombPairVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		await ResolveBombPairAsync(_turnSequenceGeneration);
		RenderTableCards();
	}

	private async Task PlayNetworkMantaTideShiftVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		if (_tableCards.Count <= 1)
		{
			return;
		}

		await PlayMantaRayTideShiftTableMoveAnimationAsync(_turnSequenceGeneration);
		if (_tableCards.Count > 0)
		{
			TableCard tideShiftedCard = _tableCards[0];
			_tableCards.RemoveAt(0);
			_tableCards.Add(tideShiftedCard);
			RenderTableCards();
		}
	}

	private async Task PlayNetworkCaptureVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		int captureStartIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.CaptureStartIndexKey, -1);
		Godot.Collections.Array extraIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey);
		int[] extraIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey, extraIndexesArray.Count);
		float flightStaggerSeconds = ReadNetworkFloat(payload, NetworkBoardVisualEventTypes.FlightStaggerSecondsKey, TableCaptureFlightStaggerSeconds);
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		HashSet<int>? extraTableCardIndexes = extraIndexes.Length == 0 ? null : new HashSet<int>(extraIndexes);
		List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
			captureStartIndex,
			false,
			false,
			extraTableCardIndexes);
		if (capturedCardVisuals.Count == 0)
		{
			return;
		}

		await PlayCapturedCardsToDeckAnimationAsync(
			playerIndex,
			captureStartIndex,
			capturedCardVisuals,
			_turnSequenceGeneration,
			extraTableCardIndexes,
			flightStaggerSeconds);
	}

	private async Task PlayNetworkCaptureRevealVisualEventAsync(Godot.Collections.Dictionary payload)
	{
		int playerIndex = ReadNetworkInt(payload, NetworkBoardVisualEventTypes.ActorSeatIndexKey, -1);
		if (playerIndex != GetLocalHandPlayerIndex())
		{
			return;
		}

		Godot.Collections.Array revealIndexesArray = ReadArray(payload, NetworkBoardVisualEventTypes.RevealTableCardIndexesKey);
		int[] revealIndexes = ReadIntArray(payload, NetworkBoardVisualEventTypes.RevealTableCardIndexesKey, revealIndexesArray.Count);
		string[] revealCardCodes = ReadStringArray(payload, NetworkBoardVisualEventTypes.RevealCardCodesKey);
		string[] revealAttachedItemCardIdsCsv = ReadStringArray(payload, NetworkBoardVisualEventTypes.RevealAttachedItemCardIdsCsvKey);
		int[] revealPointValues = ReadIntArray(payload, NetworkBoardVisualEventTypes.RevealPointValuesKey, revealIndexes.Length);
		bool[] revealPointDoubleFlags = ReadBoolArray(payload, NetworkBoardVisualEventTypes.RevealPointDoubleFlagsKey, revealIndexes.Length);
		for (int revealIndex = 0; revealIndex < revealIndexes.Length; revealIndex++)
		{
			int tableCardIndex = revealIndexes[revealIndex];
			if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			string cardCode = revealIndex < revealCardCodes.Length ? revealCardCodes[revealIndex] : string.Empty;
			if (!string.IsNullOrWhiteSpace(cardCode))
			{
				string attachedItemCardIdsCsv = revealIndex < revealAttachedItemCardIdsCsv.Length ? revealAttachedItemCardIdsCsv[revealIndex] : string.Empty;
				PlayingCard revealedCard = CreateNetworkPlayingCard(cardCode);
				if (!string.IsNullOrWhiteSpace(attachedItemCardIdsCsv))
				{
					foreach (string attachedItemCardId in attachedItemCardIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
					{
						revealedCard = revealedCard.WithAttachedItemEffect(attachedItemCardId);
					}
				}

				tableCard = new TableCard(
					revealedCard,
					tableCard.IsFaceUp,
					tableCard.ItemOverlay,
					tableCard.ItemOwnerPlayerIndex,
					tableCard.CardOwnerPlayerIndex,
					tableCard.RemoveCardOnCapture)
				{
					IsBuriedInSand = tableCard.IsBuriedInSand,
					IsSlimeCoated = tableCard.IsSlimeCoated,
					IsHiddenDuringCapture = tableCard.IsHiddenDuringCapture,
				};
				_tableCards[tableCardIndex] = tableCard;
			}

			Texture2D? revealTexture = tableCard.Card.HasValue
				? tableCard.Card.Value.Texture
				: tableCard.ItemOverlay?.Texture;
			if (revealTexture == null)
			{
				continue;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
			if (_turnSequenceGeneration != ReadNetworkInt(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, _turnSequenceGeneration)
				|| IsGameOver())
			{
				return;
			}

			_tableCards[tableCardIndex].IsFaceUp = true;
			RenderTableCards();
			int points = revealIndex < revealPointValues.Length ? revealPointValues[revealIndex] : 0;
			if (points > 0 && _tableCards[tableCardIndex].Card.HasValue)
			{
				bool isDouble = revealIndex < revealPointDoubleFlags.Length && revealPointDoubleFlags[revealIndex];
				_ = PlayTableCardPointPopupAsync(tableCardIndex, points, isDouble);
			}

			await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
