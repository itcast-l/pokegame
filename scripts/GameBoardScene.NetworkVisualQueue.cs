using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	// This is the trust-boundary representation. Godot Variants must not cross into playback.
	private sealed record NetworkVisualEvent(
		string Type, int SnapshotId, int Generation, int Actor, int HandIndex, int TargetSeat,
		int TargetHandIndex, int TableIndex, int DisplayIndex, int CaptureStartIndex, int MovedCount,
		string CardCode, string ItemId, string Presentation, bool FaceUp, bool RequiresMagnet,
		float FirstFloat, float SecondFloat, int[] Indexes, string[] Strings,
		NetworkCaptureRevealEntry[] CaptureRevealEntries);

	private readonly record struct NetworkCaptureRevealEntry(
		int TableCardIndex,
		string CardCode,
		string[] AttachedItemCardIds,
		int PointValue,
		bool PointDoubleFlag);

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

			if (!TryDecodeNetworkVisualEvent(payload, out NetworkVisualEvent visualEvent)
				|| visualEvent.SnapshotId <= _networkLastAppliedSnapshotId)
		{
			return;
		}

			NetworkVisualPlayback.Enqueue(visualEvent);
		}

		private bool TryDecodeNetworkVisualEvent(Godot.Collections.Dictionary payload, out NetworkVisualEvent visualEvent)
	{
		visualEvent = default!;
		EnsureNetworkCardCache();
		if (!TryReadNetworkString(payload, NetworkBoardVisualEventTypes.EventTypeKey, out string type)
			|| !IsKnownNetworkVisualEventType(type)
			|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.SnapshotIdKey, 0, int.MaxValue, out int snapshotId)
			|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TurnSequenceGenerationKey, 0, int.MaxValue, out int generation)
			|| generation != _turnSequenceGeneration) return false;

		int actor = -1, handIndex = -1, targetSeat = -1, targetHandIndex = -1, tableIndex = -1, displayIndex = -1, captureStartIndex = -1, movedCount = -1;
		string cardCode = string.Empty, itemId = string.Empty, presentation = string.Empty;
		bool faceUp = false, requiresMagnet = false;
		float firstFloat = 0, secondFloat = 0;
		int[] indexes = Array.Empty<int>();
		string[] strings = Array.Empty<string>();
		NetworkCaptureRevealEntry[] captureRevealEntries = Array.Empty<NetworkCaptureRevealEntry>();
		bool seat(string key, out int value) => TryReadNetworkInt32(payload, key, 0, player_count - 1, out value);

		switch (type)
		{
			case NetworkBoardVisualEventTypes.PlayHandCard:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor >= _playerHands.Count
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, 0, _playerHands[actor].Count - 1, out handIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, MaxNetworkCardCodeLength, out cardCode) || !_networkCardsByCode.ContainsKey(cardCode)
					|| !TryReadNetworkBool(payload, NetworkBoardVisualEventTypes.PlayedCardIsFaceUpKey, out faceUp)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TableCardIndexKey, 0, _tableCards.Count, out tableIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.BorrowTideAdjustedRankKey, MaxNetworkCardCodeLength, out string rank)) return false;
				if (_networkPredictedHandVisualActive && actor == GetLocalHandPlayerIndex()
					&& (handIndex != _networkPendingHandIndex || tableIndex != _networkPredictedHandTableCardIndex
						|| !string.Equals(rank, _networkPredictedHandBorrowTideAdjustedRank, StringComparison.Ordinal))) return false;
				strings = new[] { rank };
				break;
			case NetworkBoardVisualEventTypes.PlayItemCard:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor >= _playerItemHands.Count
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, 0, _playerItemHands[actor].Count - 1, out handIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, MaxNetworkItemCardIdLength, out itemId) || !IsValidNetworkItemCardId(itemId, true)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayPresentationKey, out presentation) || !IsKnownCardPlayPresentation(presentation)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexKey, -1, _tableCards.Count, out tableIndex)
					|| (_networkPredictedItemVisualActive && actor == GetLocalHandPlayerIndex() && !MatchesNetworkPredictedItemVisual(actor, handIndex, itemId, presentation, tableIndex))
					|| (presentation == CardPlayPresentationTypes.TableOverlay ? !CanApplyNetworkTableOverlay(actor, tableIndex, itemId) : actor != GetLocalHandPlayerIndex() || tableIndex != -1)) return false;
				break;
			case NetworkBoardVisualEventTypes.DrawCard:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.DisplayIndexKey, 0, MaxTotalHandCards - 1, out displayIndex)
					|| !CanExpandNetworkPlayingHandToIndex(actor, displayIndex)
					|| !TryReadOptionalNetworkString(payload, NetworkBoardVisualEventTypes.DrawnCardCodeKey, out cardCode)
					|| (cardCode.Length > MaxNetworkCardCodeLength || (cardCode.Length > 0 && !_networkCardsByCode.ContainsKey(cardCode)))) return false;
				break;
			case NetworkBoardVisualEventTypes.DeckShuffle:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor != GetLocalHandPlayerIndex()) return false;
				break;
			case NetworkBoardVisualEventTypes.QuestionRefresh:
				if (!TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TableCardIndexKey, 0, _tableCards.Count, out tableIndex)) return false;
				break;
			case NetworkBoardVisualEventTypes.TableShuffle:
				if (_tableCards.Count <= 1 || !TryReadBoundedNetworkIntArray(payload, NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey, _tableCards.Count, _tableCards.Count, 0, _tableCards.Count - 1, out indexes) || !HasUniqueValues(indexes)) return false;
				break;
			case NetworkBoardVisualEventTypes.LensReveal:
				int maxLens = Math.Min(LensRevealMaxCards, _tableCards.Count);
				if (maxLens <= 0 || !seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor != GetLocalHandPlayerIndex()
					|| !TryReadNetworkFiniteFloat(payload, NetworkBoardVisualEventTypes.SplitCenterXKey, out firstFloat)
					|| !TryReadNetworkFiniteFloat(payload, NetworkBoardVisualEventTypes.SplitCenterYKey, out secondFloat)
					|| !TryReadBoundedNetworkIntArray(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexesKey, 1, maxLens, 0, _tableCards.Count - 1, out indexes) || !HasUniqueValues(indexes)) return false;
				break;
			case NetworkBoardVisualEventTypes.CaptureToDeck:
				if (_tableCards.Count == 0 || !seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.CaptureStartIndexKey, 0, _tableCards.Count - 1, out captureStartIndex)
					|| !TryReadOptionalNetworkInt32(payload, NetworkBoardVisualEventTypes.RedirectedTableCardIndexKey, -1, -1, _tableCards.Count - 1, out tableIndex)
					|| (tableIndex >= 0 && (tableIndex <= captureStartIndex || tableIndex >= _tableCards.Count - 1))
					|| !TryReadBoundedNetworkIntArray(payload, NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey, 0, _tableCards.Count, 0, _tableCards.Count - 1, out indexes) || !HasUniqueValues(indexes)
					|| !TryReadNetworkFiniteFloat(payload, NetworkBoardVisualEventTypes.FlightStaggerSecondsKey, out firstFloat) || firstFloat < 0 || firstFloat > TableCaptureFlightStaggerSeconds) return false;
				break;
			case NetworkBoardVisualEventTypes.QuestionReward:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor != GetLocalHandPlayerIndex()
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, MaxNetworkItemCardIdLength, out itemId) || !IsValidNetworkItemCardId(itemId, false)
					|| !_itemCardCatalog.TryGetDefinition(itemId, out ItemCardDefinition reward) || !reward.CanBeQuestionReward
					|| !TryReadNetworkBool(payload, NetworkBoardVisualEventTypes.RequiresMagnetAttachmentKey, out requiresMagnet) || requiresMagnet != IsValidNetworkAttachmentItemCardId(itemId)
					|| (!requiresMagnet && !_itemCardCatalog.IsPlayable(itemId))
					|| !TryReadNetworkFiniteFloat(payload, NetworkBoardVisualEventTypes.StartGlobalXKey, out firstFloat)
					|| !TryReadNetworkFiniteFloat(payload, NetworkBoardVisualEventTypes.StartGlobalYKey, out secondFloat)
					|| !(requiresMagnet ? TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1, -1, out handIndex) : TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, 0, MaxTotalHandCards - 1, out handIndex) && CanExpandNetworkItemHandToIndex(actor, handIndex))) return false;
				break;
			case NetworkBoardVisualEventTypes.StealDeckCards:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor != GetLocalHandPlayerIndex() || !seat(NetworkBoardVisualEventTypes.TargetSeatIndexKey, out targetSeat) || targetSeat == actor || targetSeat >= _playerDecks.Count || !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.MovedCardCountKey, 1, _playerDecks[targetSeat].Count, out movedCount)) return false;
				break;
			case NetworkBoardVisualEventTypes.StealHandCard:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor != GetLocalHandPlayerIndex() || !seat(NetworkBoardVisualEventTypes.TargetSeatIndexKey, out targetSeat) || targetSeat == actor || targetSeat >= _playerHands.Count || targetSeat >= _playerItemHands.Count || GetTotalHandCardCount(actor) >= MaxTotalHandCards
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TargetHandIndexKey, 0, GetTotalHandCardCount(targetSeat) - 1, out targetHandIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, MaxNetworkItemCardIdLength, out itemId)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, MaxNetworkCardCodeLength, out cardCode)
					|| !TryReadBoundedNetworkStringArray(payload, NetworkBoardVisualEventTypes.AttachedItemCardIdsKey, 0, 1, MaxNetworkItemCardIdLength, out strings) || !HasValidNetworkAttachedItemCardIds(strings)) return false;
				if (itemId.Length == 0)
				{
					if (!_networkCardsByCode.ContainsKey(cardCode) || targetHandIndex >= _playerHands[targetSeat].Count || !string.Equals(ToNetworkCardCode(_playerHands[targetSeat][targetHandIndex]), cardCode, StringComparison.Ordinal) || !string.Equals(_playerHands[targetSeat][targetHandIndex].AttachedItemCardIdsCsv, strings.Length == 0 ? string.Empty : strings[0], StringComparison.Ordinal)) return false;
				}
				else if (!IsValidNetworkItemCardId(itemId, true) || cardCode.Length != 0 || strings.Length != 0 || targetHandIndex - _playerHands[targetSeat].Count < 0 || targetHandIndex - _playerHands[targetSeat].Count >= _playerItemHands[targetSeat].Count || !string.Equals(_playerItemHands[targetSeat][targetHandIndex - _playerHands[targetSeat].Count].Id, itemId, StringComparison.Ordinal)) return false;
				break;
			case NetworkBoardVisualEventTypes.CaptureReveal:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor != GetLocalHandPlayerIndex() || !TryDecodeNetworkCaptureRevealEntries(payload, out captureRevealEntries)) return false;
				indexes = new int[captureRevealEntries.Length];
				for (int index = 0; index < indexes.Length; index++) indexes[index] = captureRevealEntries[index].TableCardIndex;
				break;
			case NetworkBoardVisualEventTypes.BombPair:
				break;
			case NetworkBoardVisualEventTypes.MantaTideShift:
				if (_tableCards.Count <= 1) return false;
				break;
			default: return false;
		}

		visualEvent = new(type, snapshotId, generation, actor, handIndex, targetSeat, targetHandIndex, tableIndex, displayIndex, captureStartIndex, movedCount, cardCode, itemId, presentation, faceUp, requiresMagnet, firstFloat, secondFloat, indexes, strings, captureRevealEntries);
		return true;
	}

	private static bool IsKnownNetworkVisualEventType(string value) => value is NetworkBoardVisualEventTypes.PlayHandCard or NetworkBoardVisualEventTypes.PlayItemCard or NetworkBoardVisualEventTypes.CaptureReveal or NetworkBoardVisualEventTypes.CaptureToDeck or NetworkBoardVisualEventTypes.DrawCard or NetworkBoardVisualEventTypes.DeckShuffle or NetworkBoardVisualEventTypes.StealHandCard or NetworkBoardVisualEventTypes.StealDeckCards or NetworkBoardVisualEventTypes.LensReveal or NetworkBoardVisualEventTypes.TableShuffle or NetworkBoardVisualEventTypes.QuestionRefresh or NetworkBoardVisualEventTypes.QuestionReward or NetworkBoardVisualEventTypes.BombPair or NetworkBoardVisualEventTypes.MantaTideShift;
	private static bool IsKnownCardPlayPresentation(string value) => value is CardPlayPresentationTypes.DirectTrigger or CardPlayPresentationTypes.TableOverlay or CardPlayPresentationTypes.TransformAreaTrigger or CardPlayPresentationTypes.LensSplit or CardPlayPresentationTypes.TargetedTrigger;
	private bool HasCurrentNetworkTableIndexes(int[] indexes, int minimumCount, int maximumCount)
	{
		if (indexes.Length < minimumCount
			|| indexes.Length > maximumCount
			|| _tableCards.Count == 0
			|| !HasUniqueValues(indexes))
		{
			return false;
		}

		foreach (int index in indexes)
		{
			if (index < 0 || index >= _tableCards.Count)
			{
				return false;
			}
		}

		return true;
	}
	private bool TryDecodeNetworkCaptureRevealEntries(Godot.Collections.Dictionary payload, out NetworkCaptureRevealEntry[] entries)
	{
		entries = Array.Empty<NetworkCaptureRevealEntry>();
		if (_tableCards.Count == 0
			|| !TryReadBoundedNetworkIntArray(payload, NetworkBoardVisualEventTypes.RevealTableCardIndexesKey, 1, _tableCards.Count, 0, _tableCards.Count - 1, out int[] indexes)
			|| !HasUniqueValues(indexes)
			|| !TryReadBoundedNetworkStringArray(payload, NetworkBoardVisualEventTypes.RevealCardCodesKey, indexes.Length, indexes.Length, MaxNetworkCardCodeLength, out string[] codes)
			|| !TryReadBoundedNetworkStringArray(payload, NetworkBoardVisualEventTypes.RevealAttachedItemCardIdsCsvKey, indexes.Length, indexes.Length, MaxNetworkAttachmentCsvLength, out string[] csv)
			|| !TryReadBoundedNetworkIntArray(payload, NetworkBoardVisualEventTypes.RevealPointValuesKey, indexes.Length, indexes.Length, 0, int.MaxValue, out int[] points)
			|| !TryReadExactNetworkBoolArray(payload, NetworkBoardVisualEventTypes.RevealPointDoubleFlagsKey, indexes.Length, out bool[] doubled)) return false;
		var decoded = new NetworkCaptureRevealEntry[indexes.Length];
		for (int index = 0; index < indexes.Length; index++)
		{
			if (!TryValidateNetworkCaptureRevealEntry(indexes[index], codes[index], csv[index], points[index], doubled[index], out string[] attachedIds)) return false;
			decoded[index] = new NetworkCaptureRevealEntry(indexes[index], codes[index], attachedIds, points[index], doubled[index]);
		}
		entries = decoded;
		return true;
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
			await PlayCardToTableAnimationAsync(actorSeatIndex, playedCard, startGlobalPosition, clampedTableCardIndex, playedCardIsFaceUp);
		}
		else
		{
			ClearNetworkPendingActionState();
			ResetNetworkPredictedHandVisualState();
		}
		RenderTableCards();
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
			if (actorSeatIndex >= _playerItemHands.Count
				|| itemHandIndex < 0
				|| itemHandIndex >= _playerItemHands[actorSeatIndex].Count
				|| !string.Equals(_playerItemHands[actorSeatIndex][itemHandIndex].Id, itemCardId, StringComparison.Ordinal)
				|| !InsertNetworkTableOverlayIfMissing(actorSeatIndex, targetTableCardIndex, itemCard))
			{
				return;
			}
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

	private async Task PlayNetworkDrawCardVisualEventAsync(NetworkVisualEvent payload)
	{
		int playerIndex = payload.Actor;
		int displayIndex = payload.DisplayIndex;
		string drawnCardCode = payload.CardCode;
		if (playerIndex < 0 || playerIndex >= player_count || displayIndex < 0)
		{
			return;
		}

		if (!EnsureNetworkHandCardPlaceholder(playerIndex, displayIndex, drawnCardCode))
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
				|| !string.Equals(_playerItemHands[targetPlayerIndex][sourceItemHandIndex].Id, itemCardId, StringComparison.Ordinal)) return;
			ItemCard stolenItemCard = _playerItemHands[targetPlayerIndex][sourceItemHandIndex];
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
				|| !string.Equals(_playerItemHands[targetPlayerIndex][sourceItemHandIndex].Id, itemCardId, StringComparison.Ordinal)) return;
			_playerItemHands[targetPlayerIndex].RemoveAt(sourceItemHandIndex);
			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);

			_playerItemHands[ownerPlayerIndex].Add(stolenItemCard);
		}
		else
		{
			if (targetHandIndex < 0 || targetHandIndex >= _playerHands[targetPlayerIndex].Count
				|| !string.Equals(ToNetworkCardCode(_playerHands[targetPlayerIndex][targetHandIndex]), stolenCardCode, StringComparison.Ordinal)
				|| !string.Equals(_playerHands[targetPlayerIndex][targetHandIndex].AttachedItemCardIdsCsv, attachedItemCardIds.Length == 0 ? string.Empty : attachedItemCardIds[0], StringComparison.Ordinal)) return;
			PlayingCard stolenCard = _playerHands[targetPlayerIndex][targetHandIndex];
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
				|| !string.Equals(ToNetworkCardCode(_playerHands[targetPlayerIndex][targetHandIndex]), stolenCardCode, StringComparison.Ordinal)
				|| !string.Equals(_playerHands[targetPlayerIndex][targetHandIndex].AttachedItemCardIdsCsv, attachedItemCardIds.Length == 0 ? string.Empty : attachedItemCardIds[0], StringComparison.Ordinal)) return;
			_playerHands[targetPlayerIndex].RemoveAt(targetHandIndex);
			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);

			_playerHands[ownerPlayerIndex].Add(stolenCard);
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
		for (int cardIndex = 0; cardIndex < appliedMoveCount; cardIndex++)
		{
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

		if (!EnsureNetworkQuestionTableCard(tableCardIndex))
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
			if (!EnsureNetworkQuestionRewardItem(playerIndex, itemHandIndex, rewardItem)) return;
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
			TableCard tideShiftedCard = _tableCards[0];
			_tableCards.RemoveAt(0);
			_tableCards.Add(tideShiftedCard);
			RenderTableCards();
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
				_tableCards[tableCardIndex] = tableCard;
			}

			Texture2D? revealTexture = tableCard.Card.HasValue
				? tableCard.Card.Value.Texture
				: tableCard.ItemOverlay?.Texture;
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

			_tableCards[tableCardIndex].IsFaceUp = true;
			RenderTableCards();
			int points = entry.PointValue;
			if (points > 0 && _tableCards[tableCardIndex].Card.HasValue)
			{
				bool isDouble = entry.PointDoubleFlag;
				_ = PlayTableCardPointPopupAsync(tableCardIndex, points, isDouble);
			}

			await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
		}
	}
}
