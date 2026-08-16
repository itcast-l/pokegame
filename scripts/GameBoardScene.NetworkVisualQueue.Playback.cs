using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)
	{
		string eventType = payload.Type;
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
			case NetworkBoardVisualEventTypes.CaptureThresholdFeedback:
				ShowCaptureThresholdFeedback(
					payload.Actor,
					payload.Presentation == NetworkBoardVisualEventTypes.CaptureThresholdBigTide,
					payload.MovedCount);
				return;
			default:
				break;
		}

		int actorSeatIndex = payload.Actor;
		int handIndex = payload.HandIndex;
		string playedCardCode = payload.CardCode;
		bool playedCardIsFaceUp = payload.FaceUp;
		int tableCardIndex = payload.TableIndex;
		string borrowTideAdjustedRank = payload.Strings.Length == 1 ? payload.Strings[0] : string.Empty;
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count)
		{
			return;
		}

		PlayingCard playedCard = CreateNetworkPlayingCard(playedCardCode);
		int clampedTableCardIndex = tableCardIndex;
		bool matchesLocalPrediction = actorSeatIndex == GetLocalHandPlayerIndex()
			&& handIndex == _networkPendingHandIndex
			&& _networkPredictedHandVisualActive
			&& clampedTableCardIndex == _networkPredictedHandTableCardIndex
			&& string.Equals(borrowTideAdjustedRank, _networkPredictedHandBorrowTideAdjustedRank, StringComparison.Ordinal);
		if (!matchesLocalPrediction
			&& (actorSeatIndex >= _playerHands.Count
				|| handIndex < 0
				|| handIndex >= _playerHands[actorSeatIndex].Count
				|| !string.Equals(ToNetworkCardCode(_playerHands[actorSeatIndex][handIndex]), playedCardCode, StringComparison.Ordinal)
				|| tableCardIndex < 0
				|| tableCardIndex > _tableCards.Count))
		{
			return;
		}
		if (!matchesLocalPrediction)
		{
			if (!TryInsertNetworkPresentationTableCard(
				clampedTableCardIndex,
				new TableCard(
					playedCard,
					playedCardIsFaceUp,
					cardOwnerPlayerIndex: actorSeatIndex)))
			{
				return;
			}
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
			await PlayCardToTableAnimationAsync(actorSeatIndex, playedCard, startGlobalPosition, clampedTableCardIndex, playedCardIsFaceUp);
		}
		else
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedHandVisualState();
		}
		RenderTableCards();
	}

	private async Task PlayNetworkAuthorityHostVisualEventAsync(NetworkVisualEvent payload)
	{
		_networkAuthorityHostVisualPlaybackCount = NetworkVisualQueuePolicy.SaturatingAdd(
			_networkAuthorityHostVisualPlaybackCount,
			1);
		Vector2 startGlobalPosition = payload.AuthorityHostStartGlobalPosition;
		if (startGlobalPosition == Vector2.Zero && payload.Actor >= 0 && payload.Actor < player_count)
		{
			startGlobalPosition = GetVisibleCardPileTopGlobalPosition(payload.Actor);
		}

		switch (payload.Type)
		{
			case NetworkBoardVisualEventTypes.PlayHandCard:
				if (payload.Actor >= 0
					&& payload.Actor < player_count
					&& payload.TableIndex >= 0
					&& payload.TableIndex < _tableCards.Count)
				{
					await PlayCardToTableAnimationAsync(
						payload.Actor,
						CreateNetworkPlayingCard(payload.CardCode),
						startGlobalPosition,
						payload.TableIndex,
						payload.FaceUp);
				}
				return;
			case NetworkBoardVisualEventTypes.PlayItemCard:
				await PlayNetworkAuthorityHostItemCardVisualOnlyAsync(payload, startGlobalPosition);
				return;
			case NetworkBoardVisualEventTypes.DrawCard:
				if (payload.Actor >= 0
					&& payload.Actor < _playerHands.Count
					&& payload.DisplayIndex >= 0
					&& payload.DisplayIndex < _playerHands[payload.Actor].Count)
				{
					PlayingCard drawnCard = string.IsNullOrWhiteSpace(payload.CardCode)
						? _playerHands[payload.Actor][payload.DisplayIndex]
						: CreateNetworkPlayingCard(payload.CardCode);
					await PlayReplacementDrawAnimationAsync(
						payload.Actor,
						new DrawnHandCard(drawnCard, payload.DisplayIndex),
						_turnSequenceGeneration,
						broadcastNetworkVisualEvent: false);
				}
				return;
			case NetworkBoardVisualEventTypes.DeckShuffle:
				await PlayPhysicalDeckShuffleAsync(
					payload.Actor,
					broadcastNetworkVisualEvent: false);
				return;
			case NetworkBoardVisualEventTypes.StealDeckCards:
				await PlayStealDeckCardsAnimationAsync(
					payload.Actor,
					payload.TargetSeat,
					payload.MovedCount,
					_turnSequenceGeneration);
				return;
			case NetworkBoardVisualEventTypes.QuestionReward:
				await PlayNetworkAuthorityHostQuestionRewardVisualOnlyAsync(payload);
				return;
			case NetworkBoardVisualEventTypes.CaptureThresholdFeedback:
				ShowCaptureThresholdFeedback(
					payload.Actor,
					payload.Presentation == NetworkBoardVisualEventTypes.CaptureThresholdBigTide,
					payload.MovedCount);
				return;
			default:
				return;
		}
	}

	private async Task PlayNetworkAuthorityHostQuestionRewardVisualOnlyAsync(
		NetworkVisualEvent payload)
	{
		ItemCard rewardItem = CreateValidatedNetworkItemCard(payload.ItemId);
		if (!payload.RequiresMagnet)
		{
			BeginQuestionRewardHandDelivery(payload.Actor, payload.HandIndex);
			RenderPlayerHands();
		}

		try
		{
			await PlayQuestionCardRewardAnimationAsync(
				payload.Actor,
				new QuestionCardRewardAnimation(
					rewardItem,
					payload.HandIndex,
					new Vector2(payload.FirstFloat, payload.SecondFloat),
					payload.RequiresMagnet),
				_turnSequenceGeneration);
		}
		finally
		{
			if (!payload.RequiresMagnet)
			{
				EndQuestionRewardHandDelivery(payload.Actor, payload.HandIndex);
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayNetworkAuthorityHostItemCardVisualOnlyAsync(
		NetworkVisualEvent payload,
		Vector2 startGlobalPosition)
	{
		if (payload.Actor < 0 || payload.Actor >= player_count)
		{
			return;
		}

		ItemCard itemCard = CreateNetworkVisualItemCard(payload.ItemId);
		if (payload.Presentation == CardPlayPresentationTypes.TableOverlay)
		{
			if (payload.TableIndex >= 0 && payload.TableIndex < _tableCards.Count)
			{
				await PlayItemCardToTableAnimationAsync(
					payload.Actor,
					itemCard,
					startGlobalPosition,
					payload.TableIndex,
					tableCardIsFaceUp: false);
			}
			return;
		}

		if (payload.Presentation == CardPlayPresentationTypes.DirectTrigger)
		{
			await PlaySpecialCardTriggerEffectAsync(
				GetItemCardTexture(itemCard),
				startGlobalPosition,
				0,
				GetCardRotationDegreesForPlayer(payload.Actor));
			return;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		await PlayItemCardToTransformAreaAnimationAsync(
			GetItemCardTexture(itemCard),
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(payload.Actor));
		if (payload.Actor == GetLocalHandPlayerIndex())
		{
			await PlaySpecialCardTriggerEffectAsync(GetItemCardTexture(itemCard), transformAreaGlobalPosition, 0);
		}
	}

	private async Task PlayNetworkItemCardVisualEventAsync(NetworkVisualEvent payload)
	{
		int actorSeatIndex = payload.Actor;
		int itemHandIndex = payload.HandIndex;
		string itemCardId = payload.ItemId;
		string playPresentation = payload.Presentation;
		int targetTableCardIndex = payload.TableIndex;
		if (actorSeatIndex < 0 || actorSeatIndex >= player_count || string.IsNullOrWhiteSpace(itemCardId))
		{
			return;
		}

		ItemCard itemCard = CreateNetworkVisualItemCard(itemCardId);
		bool matchesLocalPrediction = actorSeatIndex == GetLocalHandPlayerIndex()
			&& itemHandIndex == _networkPendingItemHandIndex
			&& _networkPredictedItemVisualActive;
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

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(actorSeatIndex, itemHandIndex)
			?? GetVisibleCardPileTopGlobalPosition(actorSeatIndex);
		if (playPresentation == CardPlayPresentationTypes.TableOverlay && targetTableCardIndex >= 0)
		{
			if (actorSeatIndex >= _playerItemHands.Count
				|| itemHandIndex < 0
				|| itemHandIndex >= _playerItemHands[actorSeatIndex].Count
				|| !string.Equals(_playerItemHands[actorSeatIndex][itemHandIndex].Id, itemCardId, StringComparison.Ordinal)
				|| !TryAddNetworkTableOverlayPresentation(actorSeatIndex, targetTableCardIndex, itemCard))
			{
				return;
			}
			await PlayItemCardToTableAnimationAsync(actorSeatIndex, itemCard, startGlobalPosition, targetTableCardIndex, tableCardIsFaceUp: false);
			RenderTableCards();
			return;
		}

		if (playPresentation == CardPlayPresentationTypes.DirectTrigger)
		{
			await PlaySpecialCardTriggerEffectAsync(
				GetItemCardTexture(itemCard),
				startGlobalPosition,
				0,
				GetCardRotationDegreesForPlayer(actorSeatIndex));
			return;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		await PlayItemCardToTransformAreaAnimationAsync(
			GetItemCardTexture(itemCard),
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(actorSeatIndex));
		if (actorSeatIndex == GetLocalHandPlayerIndex())
		{
			await PlaySpecialCardTriggerEffectAsync(GetItemCardTexture(itemCard), transformAreaGlobalPosition, 0);
		}
	}

	private async Task PlayNetworkDrawCardVisualEventAsync(NetworkVisualEvent payload)
	{
		int playerIndex = payload.Actor;
		int displayIndex = payload.DisplayIndex;
		string drawnCardCode = payload.CardCode;
		if (playerIndex < 0 || playerIndex >= player_count || displayIndex < 0)
		{
			return;
		}

		if (!TryAddNetworkHandCardPresentationPlaceholder(playerIndex, displayIndex, drawnCardCode))
		{
			return;
		}
		DrawnHandCard drawnCard = new(
			string.IsNullOrWhiteSpace(drawnCardCode)
				? CreatePlaceholderPlayingCard()
				: CreateNetworkPlayingCard(drawnCardCode),
			displayIndex);
		await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, _turnSequenceGeneration);
	}

	private async Task PlayNetworkDeckShuffleVisualEventAsync(NetworkVisualEvent payload)
	{
		int playerIndex = payload.Actor;
		if (playerIndex < 0 || playerIndex >= player_count)
		{
			return;
		}

		await PlayPhysicalDeckShuffleAsync(playerIndex);
	}

	private async Task PlayNetworkStealHandCardVisualEventAsync(NetworkVisualEvent payload)
	{
		int ownerPlayerIndex = payload.Actor;
		int targetPlayerIndex = payload.TargetSeat;
		int targetHandIndex = payload.TargetHandIndex;
		string itemCardId = payload.ItemId;
		string stolenCardCode = payload.CardCode;
		string[] attachedItemCardIds = payload.Strings;
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
			if (sourceItemHandIndex < 0 || sourceItemHandIndex >= _playerItemHands[targetPlayerIndex].Count
				|| !MatchesVisibleNetworkItemCardIdentity(
					_playerItemHands[targetPlayerIndex][sourceItemHandIndex],
					itemCardId)) return;
			ItemCard stolenItemCard = CreateNetworkVisualItemCard(itemCardId);
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenItemCardTexture(targetPlayerIndex, stolenItemCard),
				null,
				GetVisibleStolenItemCardScale(targetPlayerIndex, stolenItemCard),
				GetStolenItemCardTargetDisplayIndex(ownerPlayerIndex));
			if (_turnSequenceGeneration != payload.Generation
				|| IsGameOver())
			{
				RenderPlayerHands();
				return;
			}

			if (GetTotalHandCardCount(ownerPlayerIndex) >= MaxTotalHandCards
				|| sourceItemHandIndex < 0
				|| sourceItemHandIndex >= _playerItemHands[targetPlayerIndex].Count
				|| !MatchesVisibleNetworkItemCardIdentity(
					_playerItemHands[targetPlayerIndex][sourceItemHandIndex],
					itemCardId)
				|| !TryTransferNetworkPresentationItemCard(
					targetPlayerIndex,
					sourceItemHandIndex,
					ownerPlayerIndex,
					stolenItemCard)) return;
			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
		}
		else
		{
			if (targetHandIndex < 0 || targetHandIndex >= _playerHands[targetPlayerIndex].Count
				|| !MatchesVisibleNetworkPlayingCardIdentity(
					_playerHands[targetPlayerIndex][targetHandIndex],
					stolenCardCode,
					attachedItemCardIds.Length == 0 ? string.Empty : attachedItemCardIds[0])) return;
			PlayingCard stolenCard = CreateNetworkPlayingCard(stolenCardCode, attachedItemCardIds);
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenPlayingCardTexture(targetPlayerIndex, stolenCard),
				GetVisibleStolenPlayingCardAttachmentOverlayTexture(targetPlayerIndex, stolenCard),
				1.0f,
				GetStolenPlayingCardTargetDisplayIndex(ownerPlayerIndex));
			if (_turnSequenceGeneration != payload.Generation
				|| IsGameOver())
			{
				RenderPlayerHands();
				return;
			}

			if (GetTotalHandCardCount(ownerPlayerIndex) >= MaxTotalHandCards
				|| targetHandIndex < 0
				|| targetHandIndex >= _playerHands[targetPlayerIndex].Count
				|| !MatchesVisibleNetworkPlayingCardIdentity(
					_playerHands[targetPlayerIndex][targetHandIndex],
					stolenCardCode,
					attachedItemCardIds.Length == 0 ? string.Empty : attachedItemCardIds[0])
				|| !TryTransferNetworkPresentationHandCard(
					targetPlayerIndex,
					targetHandIndex,
					ownerPlayerIndex,
					stolenCard)) return;
			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
		}

		PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerHands();
	}

	private async Task PlayNetworkStealDeckCardsVisualEventAsync(NetworkVisualEvent payload)
	{
		int ownerPlayerIndex = payload.Actor;
		int targetPlayerIndex = payload.TargetSeat;
		int movedCardCount = payload.MovedCount;
		int turnSequenceGeneration = payload.Generation;
		if (ownerPlayerIndex != GetLocalHandPlayerIndex()
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= player_count
			|| movedCardCount <= 0)
		{
			return;
		}
		if (targetPlayerIndex >= _playerDecks.Count
			|| ownerPlayerIndex >= _playerDecks.Count
			|| movedCardCount > _playerDecks[targetPlayerIndex].Count)
		{
			return;
		}

		PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
		Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(targetPlayerIndex);
		Vector2 targetGlobalPosition = GetDrawCardStartGlobalPosition(ownerPlayerIndex);
		float startRotationDegrees = GetCardPileRotationDegreesForPlayer(targetPlayerIndex);
		float targetRotationDegrees = GetCardPileRotationDegreesForPlayer(ownerPlayerIndex);
		int appliedMoveCount = movedCardCount;
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

		}
		if (turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver()
			|| movedCardCount > _playerDecks[targetPlayerIndex].Count)
		{
			RenderPlayerCardPiles();
			return;
		}
		if (TryTransferNetworkPresentationDeckCards(
			targetPlayerIndex,
			ownerPlayerIndex,
			appliedMoveCount))
		{
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

		RenderPlayerCardPiles();
	}

	private async Task PlayNetworkLensRevealVisualEventAsync(NetworkVisualEvent payload)
	{
		int ownerPlayerIndex = payload.Actor;
		float splitCenterX = payload.FirstFloat;
		float splitCenterY = payload.SecondFloat;
		int[] targetIndexes = payload.Indexes;
		if (ownerPlayerIndex != GetLocalHandPlayerIndex()
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| !HasCurrentNetworkTableIndexes(targetIndexes, 1, Math.Min(LensRevealMaxCards, _tableCards.Count)))
		{
			return;
		}

		await RevealLensTargetCardsAsync(targetIndexes, new Vector2(splitCenterX, splitCenterY), _turnSequenceGeneration);
	}

	private async Task PlayNetworkTableShuffleVisualEventAsync(NetworkVisualEvent payload)
	{
		int[] targetIndexes = payload.Indexes;
		if (targetIndexes.Length != _tableCards.Count || !HasUniqueValues(targetIndexes))
		{
			return;
		}
		List<TableCard> shuffledTableCards = BuildNetworkShuffledTableCards(targetIndexes);
		if (shuffledTableCards.Count != _tableCards.Count
			|| !await PlayTableCardsShuffleAnimationAsync(
				shuffledTableCards,
				payload.Generation))
		{
			return;
		}
	}

	private async Task PlayNetworkQuestionRefreshVisualEventAsync(NetworkVisualEvent payload)
	{
		int tableCardIndex = payload.TableIndex;
		if (tableCardIndex < 0 || tableCardIndex > _tableCards.Count)
		{
			return;
		}

		if (!TryAddNetworkQuestionTablePresentationCard(tableCardIndex))
		{
			return;
		}
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		await PlayQuestionCardRefreshAnimationAsync(tableCardIndex);
		RenderTableCards();
	}

	private async Task PlayNetworkQuestionRewardVisualEventAsync(NetworkVisualEvent payload)
	{
		int playerIndex = payload.Actor;
		string itemCardId = payload.ItemId;
		int itemHandIndex = payload.HandIndex;
		float startGlobalX = payload.FirstFloat;
		float startGlobalY = payload.SecondFloat;
		bool requiresMagnetAttachment = payload.RequiresMagnet;
		ItemCard rewardItem = CreateValidatedNetworkItemCard(itemCardId);
		if (!requiresMagnetAttachment)
		{
			if (!TryAddNetworkQuestionRewardPresentationItem(playerIndex, itemHandIndex, rewardItem)) return;
			BeginQuestionRewardHandDelivery(playerIndex, itemHandIndex);
			RenderPlayerHands();
		}

		try
		{
			await PlayQuestionCardRewardAnimationAsync(
				playerIndex,
				new QuestionCardRewardAnimation(
					rewardItem,
					itemHandIndex,
					new Vector2(startGlobalX, startGlobalY),
					requiresMagnetAttachment),
				_turnSequenceGeneration);
		}
		finally
		{
			if (!requiresMagnetAttachment)
			{
				EndQuestionRewardHandDelivery(playerIndex, itemHandIndex);
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayNetworkBombPairVisualEventAsync(NetworkVisualEvent payload)
	{
		await ResolveBombPairAsync(_turnSequenceGeneration);
		RenderTableCards();
	}

	private async Task PlayNetworkMantaTideShiftVisualEventAsync(NetworkVisualEvent payload)
	{
		if (_tableCards.Count <= 1)
		{
			return;
		}

		await PlayMantaRayTideShiftTableMoveAnimationAsync(_turnSequenceGeneration);
		if (_turnSequenceGeneration == payload.Generation && !IsGameOver() && _tableCards.Count > 1)
		{
			if (TryMoveFirstNetworkPresentationTableCardToEnd())
			{
				RenderTableCards();
			}
		}
	}

	private async Task PlayNetworkCaptureVisualEventAsync(NetworkVisualEvent payload)
	{
		int playerIndex = payload.Actor;
		int captureStartIndex = payload.CaptureStartIndex;
		int[] extraIndexes = payload.Indexes;
		float flightStaggerSeconds = payload.FirstFloat;
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count
			|| !HasCurrentNetworkTableIndexes(extraIndexes, 0, _tableCards.Count))
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
			flightStaggerSeconds,
			payload.TableIndex);
	}

	private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)
	{
		int playerIndex = payload.Actor;
		if (playerIndex != GetLocalHandPlayerIndex())
		{
			return;
		}

		NetworkCaptureRevealEntry[] entries = payload.CaptureRevealEntries;
		// Every target is checked against the current board before the first replacement.
		// A queued event can become stale while an earlier visual is playing.
		for (int revealIndex = 0; revealIndex < entries.Length; revealIndex++)
		{
			NetworkCaptureRevealEntry entry = entries[revealIndex];
			if (entry.TableCardIndex < 0 || entry.TableCardIndex >= _tableCards.Count
				|| !TryValidateNetworkCaptureRevealEntry(entry.TableCardIndex, entry.CardCode,
					entry.AttachedItemCardIds.Length == 0 ? string.Empty : entry.AttachedItemCardIds[0],
					entry.PointValue, entry.PointDoubleFlag, out string[] validatedAttachedIds)
				|| !validatedAttachedIds.AsSpan().SequenceEqual(entry.AttachedItemCardIds))
			{
				return;
			}
		}

		for (int revealIndex = 0; revealIndex < entries.Length; revealIndex++)
		{
			NetworkCaptureRevealEntry entry = entries[revealIndex];
			int tableCardIndex = entry.TableCardIndex;
			TableCard tableCard = _tableCards[tableCardIndex];
			if (entry.CardCode.Length > 0)
			{
				PlayingCard revealedCard = CreateNetworkPlayingCard(entry.CardCode, entry.AttachedItemCardIds);
				tableCard = new TableCard(revealedCard, tableCard.IsFaceUp, tableCard.ItemOverlay,
					tableCard.ItemOwnerPlayerIndex, tableCard.CardOwnerPlayerIndex, tableCard.RemoveCardOnCapture)
				{
					IsBuriedInSand = tableCard.IsBuriedInSand,
					IsSlimeCoated = tableCard.IsSlimeCoated,
					IsHiddenDuringCapture = tableCard.IsHiddenDuringCapture,
				};
				if (!TryReplaceNetworkPresentationTableCard(tableCardIndex, tableCard))
				{
					return;
				}
			}

			Texture2D? revealTexture = tableCard.Card.HasValue
				? GetPlayingCardTexture(tableCard.Card.Value)
				: tableCard.ItemOverlay.HasValue ? GetItemCardTexture(tableCard.ItemOverlay.Value) : null;
			if (revealTexture == null)
			{
				return;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
			if (_turnSequenceGeneration != payload.Generation
				|| IsGameOver())
			{
				return;
			}

			if (!TrySetNetworkPresentationTableCardFaceUp(tableCardIndex, true))
			{
				return;
			}
			RenderTableCards();
			int points = entry.PointValue;
			if (points > 0 && _tableCards[tableCardIndex].Card.HasValue)
			{
				bool isDouble = entry.PointDoubleFlag;
				TrackBoardTask(PlayTableCardPointPopupAsync(tableCardIndex, points, isDouble), nameof(PlayTableCardPointPopupAsync));
			}

			await WaitForBoardDelayAsync(TableCardRevealPauseSeconds);
		}
	}
}
