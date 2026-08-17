using System;
using Godot;

public partial class GameBoardScene
{
	// This is the trust-boundary representation. Godot Variants must not cross into playback.
	internal sealed record NetworkVisualEvent(
		string Type, long AuthorityEpoch, long CheckpointId, long ActionId, int SnapshotId, int Generation, int Actor, int HandIndex, int TargetSeat,
		int TargetHandIndex, int TableIndex, int DisplayIndex, int CaptureStartIndex, int MovedCount,
		string CardCode, string ItemId, string Presentation, bool FaceUp, bool RequiresMagnet,
		float FirstFloat, float SecondFloat, int[] Indexes, string[] Strings,
		NetworkCaptureRevealEntry[] CaptureRevealEntries,
		bool IsAuthorityHostVisualOnly = false,
		Vector2 AuthorityHostStartGlobalPosition = default);

	internal readonly record struct NetworkCaptureRevealEntry(
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
			|| visualEvent.AuthorityEpoch < _networkAuthorityEpoch
			|| (visualEvent.AuthorityEpoch == _networkAuthorityEpoch
				&& visualEvent.SnapshotId <= _networkLastAppliedSnapshotId))
		{
			return;
		}

		NetworkVisualPlayback.Enqueue(visualEvent);
	}

	private bool TryDecodeNetworkVisualEvent(
		Godot.Collections.Dictionary payload,
		out NetworkVisualEvent visualEvent,
		bool authorityHostVisualOnly = false)
	{
		visualEvent = default!;
		EnsureNetworkCardCache();
		if (!TryReadNetworkString(payload, NetworkBoardVisualEventTypes.EventTypeKey, out string type)
			|| !IsKnownNetworkVisualEventType(type)
			|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.SessionIdKey, NetworkProtocol.MaxSessionIdLength, out string sessionId)
			|| !string.Equals(sessionId, network_session_id, StringComparison.Ordinal)
			|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.MatchIdKey, MaxNetworkSessionIdLength, out string matchId)
			|| !string.Equals(matchId, network_match_id, StringComparison.Ordinal)
			|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.SnapshotIdKey, 0, int.MaxValue, out int snapshotId)
			|| !TryReadNetworkInt64(payload, NetworkBoardVisualEventTypes.CheckpointIdKey, 0, long.MaxValue, out long checkpointId)
			|| !TryReadNetworkInt64(payload, NetworkBoardVisualEventTypes.AuthorityEpochKey, 0, long.MaxValue, out long authorityEpoch)
			|| authorityEpoch != _networkAuthorityEpoch
			|| checkpointId < _networkCheckpointId
			|| !TryReadNetworkInt64(payload, NetworkBoardVisualEventTypes.ActionIdKey, 0, long.MaxValue, out long actionId)
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
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, 0, authorityHostVisualOnly ? MaxTotalHandCards - 1 : _playerHands[actor].Count - 1, out handIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayedCardCodeKey, MaxNetworkCardCodeLength, out cardCode) || !_networkCardsByCode.ContainsKey(cardCode)
					|| !TryReadNetworkBool(payload, NetworkBoardVisualEventTypes.PlayedCardIsFaceUpKey, out faceUp)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TableCardIndexKey, 0, _tableCards.Count, out tableIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.BorrowTideAdjustedRankKey, MaxNetworkCardCodeLength, out string rank)) return false;
				if (!authorityHostVisualOnly
					&& _networkPredictedHandVisualActive && actor == GetLocalHandPlayerIndex()
					&& (handIndex != _networkPendingHandIndex || tableIndex != _networkPredictedHandTableCardIndex
						|| !string.Equals(rank, _networkPredictedHandBorrowTideAdjustedRank, StringComparison.Ordinal))) return false;
				strings = new[] { rank };
				break;
			case NetworkBoardVisualEventTypes.PlayItemCard:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor) || actor >= _playerItemHands.Count
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, 0, authorityHostVisualOnly ? MaxTotalHandCards - 1 : _playerItemHands[actor].Count - 1, out handIndex)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.ItemCardIdKey, MaxNetworkItemCardIdLength, out itemId) || !IsValidNetworkItemCardId(itemId, true)
					|| !TryReadNetworkString(payload, NetworkBoardVisualEventTypes.PlayPresentationKey, out presentation) || !IsKnownCardPlayPresentation(presentation)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.TargetTableCardIndexKey, -1, _tableCards.Count, out tableIndex)
					|| (!authorityHostVisualOnly && _networkPredictedItemVisualActive && actor == GetLocalHandPlayerIndex() && !MatchesNetworkPredictedItemVisual(actor, handIndex, itemId, presentation, tableIndex))
					|| (!authorityHostVisualOnly && (presentation == CardPlayPresentationTypes.TableOverlay ? !CanApplyNetworkTableOverlay(actor, tableIndex, itemId) : actor != GetLocalHandPlayerIndex() || tableIndex != -1))) return false;
				break;
			case NetworkBoardVisualEventTypes.DrawCard:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor)
					|| !TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.DisplayIndexKey, 0, MaxTotalHandCards - 1, out displayIndex)
					|| (!authorityHostVisualOnly && !CanExpandNetworkPlayingHandToIndex(actor, displayIndex))
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
					|| !(requiresMagnet
						? TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, -1, -1, out handIndex)
						: TryReadNetworkInt32(payload, NetworkBoardVisualEventTypes.HandIndexKey, 0, MaxTotalHandCards - 1, out handIndex)
							&& (authorityHostVisualOnly || CanExpandNetworkItemHandToIndex(actor, handIndex)))) return false;
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
					if (!_networkCardsByCode.ContainsKey(cardCode)
						|| targetHandIndex >= _playerHands[targetSeat].Count
						|| !MatchesVisibleNetworkPlayingCardIdentity(
							_playerHands[targetSeat][targetHandIndex],
							cardCode,
							strings.Length == 0 ? string.Empty : strings[0])) return false;
				}
				else if (!IsValidNetworkItemCardId(itemId, true)
					|| cardCode.Length != 0
					|| strings.Length != 0
					|| targetHandIndex - _playerHands[targetSeat].Count < 0
					|| targetHandIndex - _playerHands[targetSeat].Count >= _playerItemHands[targetSeat].Count
					|| !MatchesVisibleNetworkItemCardIdentity(
						_playerItemHands[targetSeat][targetHandIndex - _playerHands[targetSeat].Count],
						itemId)) return false;
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
			case NetworkBoardVisualEventTypes.CaptureThresholdFeedback:
				if (!seat(NetworkBoardVisualEventTypes.ActorSeatIndexKey, out actor)
					|| !TryReadNetworkString(
						payload,
						NetworkBoardVisualEventTypes.CaptureThresholdFeedbackTypeKey,
						32,
						out presentation)
					|| presentation is not (NetworkBoardVisualEventTypes.CaptureThresholdBlocked
						or NetworkBoardVisualEventTypes.CaptureThresholdBigTide)
					|| !TryReadNetworkInt32(
						payload,
						NetworkBoardVisualEventTypes.CaptureThresholdCountKey,
						1,
						NetworkProtocol.MaxAuthorityActionQueueCount * 4,
						out movedCount)) return false;
				break;
			default: return false;
		}

		visualEvent = new(type, authorityEpoch, checkpointId, actionId, snapshotId, generation, actor, handIndex, targetSeat, targetHandIndex, tableIndex, displayIndex, captureStartIndex, movedCount, cardCode, itemId, presentation, faceUp, requiresMagnet, firstFloat, secondFloat, indexes, strings, captureRevealEntries);
		return true;
	}

	private static bool IsKnownNetworkVisualEventType(string value) => value is NetworkBoardVisualEventTypes.PlayHandCard or NetworkBoardVisualEventTypes.PlayItemCard or NetworkBoardVisualEventTypes.CaptureReveal or NetworkBoardVisualEventTypes.CaptureToDeck or NetworkBoardVisualEventTypes.DrawCard or NetworkBoardVisualEventTypes.DeckShuffle or NetworkBoardVisualEventTypes.StealHandCard or NetworkBoardVisualEventTypes.StealDeckCards or NetworkBoardVisualEventTypes.LensReveal or NetworkBoardVisualEventTypes.TableShuffle or NetworkBoardVisualEventTypes.QuestionRefresh or NetworkBoardVisualEventTypes.QuestionReward or NetworkBoardVisualEventTypes.BombPair or NetworkBoardVisualEventTypes.MantaTideShift or NetworkBoardVisualEventTypes.CaptureThresholdFeedback;
	private static bool IsKnownCardPlayPresentation(string value) => value is CardPlayPresentationTypes.DirectTrigger or CardPlayPresentationTypes.TableOverlay or CardPlayPresentationTypes.TransformAreaTrigger or CardPlayPresentationTypes.LensSplit;
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
}
