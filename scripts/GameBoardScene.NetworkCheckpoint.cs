using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

public partial class GameBoardScene
{
	private long _networkAuthorityEpoch;
	private long _networkCheckpointId;
	private bool _networkMigrationInputLocked;

	internal long NetworkAuthorityEpoch => _networkAuthorityEpoch;
	internal long NetworkCheckpointId => _networkCheckpointId;
	internal bool IsNetworkMigrationInputLocked => _networkMigrationInputLocked;

	internal void BeginNetworkHostMigrationVisualState()
	{
		_networkReconnectCatchupPendingCompletion = true;
		_networkMigrationInputLocked = true;
		// Invalidate callbacks resumed from the pre-migration turn. The candidate
		// restores the checkpoint generation before accepting new input.
		AdvanceTurnSequenceGeneration();
		NetworkVisualPlayback.ClearEvents();
		NetworkVisualPlayback.ClearPendingSnapshot();
		ResetNetworkPredictedClientState();
		ClearNetworkPendingActionState();
		RefreshNetworkReconnectOverlayIfNeeded(NetworkSessionState.MigratingHost);
	}

	internal void CompleteNetworkHostMigrationVisualState()
	{
		_networkMigrationInputLocked = false;
		_networkReconnectCatchupPendingCompletion = false;
		RefreshNetworkReconnectOverlayIfNeeded(NetworkSessionState.InGame);
	}

	internal void AdvanceNetworkAuthorityEpoch(long authorityEpoch)
	{
		if (authorityEpoch <= _networkAuthorityEpoch)
		{
			return;
		}

		_networkAuthorityEpoch = authorityEpoch;
		_networkLastAppliedSnapshotId = -1;
		NetworkVisualPlayback.ClearEvents();
		NetworkVisualPlayback.ClearPendingSnapshot();
		ResetNetworkPredictedClientState();
	}

	internal bool TryBuildAuthorityCheckpointEnvelope(
		IReadOnlyDictionary<int, long> lastAcceptedClientActionIds,
		long candidateCheckpointId,
		out AuthorityCheckpointEnvelope envelope,
		out string error)
	{
		envelope = AuthorityCheckpointEnvelope.Empty;
		error = string.Empty;
		NetworkManager? networkManager = GetNetworkManager();
		if (!IsNetworkMultiplayerMode() || !IsNetworkHost() || networkManager == null)
		{
			error = "board_not_authority";
			return false;
		}
		if (!AuthorityActionStateRules.IsStableCheckpointState(_networkAuthorityActionState))
		{
			error = "authority_action_not_stable";
			return false;
		}
		if (candidateCheckpointId <= _networkCheckpointId)
		{
			error = "checkpoint_id_not_monotonic";
			return false;
		}

		EnsureNetworkCardCache();
		NetworkSessionConfig config = networkManager.CurrentConfig;
		var checkpoint = new NetworkAuthorityCheckpoint
		{
			SessionId = config.SessionId,
			MatchId = config.MatchId,
			RoomName = config.RoomName,
			RoomPasswordVerifierVersion = config.RoomPasswordVerifier.Version,
			RoomPasswordVerifierIterations = config.RoomPasswordVerifier.Iterations,
			RoomPasswordVerifierSalt = config.RoomPasswordVerifier.Salt,
			RoomPasswordVerifier = config.RoomPasswordVerifier.Verifier,
			MatchSeed = config.MatchSeed,
			PlayerCount = player_count,
			ItemMode = item_mode,
			WinningScore = winning_score,
			Transport = config.Transport,
			ManifestHash = _itemCardCatalog.GameplayManifestHash,
			Roster = networkManager.GetCheckpointRoster(),
			SeatControls = networkManager.GetCheckpointSeatControls(),
			LastAcceptedClientActionIds = new Dictionary<int, long>(lastAcceptedClientActionIds),
			Decks = ExportCardLists(_playerDecks),
			Hands = ExportCardLists(_playerHands),
			ItemHands = ExportItemHands(),
			TableCards = ExportTableCards(),
			Scores = _playerScores.Take(player_count).ToArray(),
			MatchOutcomeKind = _matchOutcomeKind,
			WinnerPlayerIndex = _winnerPlayerIndex,
			OpeningDealCompleted = true,
			CurrentTurnPlayerIndex = _currentTurnPlayerIndex,
			TurnSequenceGeneration = _turnSequenceGeneration,
			CompletedTurnsSinceQuestionRefresh = _completedTurnsSinceQuestionCardRefresh,
			QuestionRefreshAnchorPlayerIndex = _questionCardRefreshAnchorPlayerIndex,
			LastAuthorityActionId = _nextNetworkAuthorityActionId,
			AuthorityAction = _networkAuthorityActionState,
			RngSeed = _matchSeed,
			RngState = _rng.State.ToString(CultureInfo.InvariantCulture),
			SnapshotId = _networkSnapshotId,
			AuthorityEpoch = _networkAuthorityEpoch,
			CheckpointId = candidateCheckpointId,
			ReconnectClaims = networkManager.GetCheckpointReconnectClaims(),
		};

		if (AuthorityCheckpointEnvelope.TryCreate(checkpoint, out envelope, out string encodeError))
		{
			return true;
		}

		NetworkAuthorityCheckpointCodec.Validate(checkpoint, out string validationError);
		error = string.Join(
			" ",
			encodeError,
			validationError,
			$"action={checkpoint.AuthorityAction.Kind}/{checkpoint.AuthorityAction.Phase}",
			$"action_valid={AuthorityActionStateRules.IsValid(checkpoint.AuthorityAction, checkpoint.PlayerCount)}",
			$"outcome={checkpoint.MatchOutcomeKind}/{checkpoint.WinnerPlayerIndex}",
			$"turn={checkpoint.CurrentTurnPlayerIndex}/{checkpoint.TurnSequenceGeneration}",
			$"refresh={checkpoint.CompletedTurnsSinceQuestionRefresh}/{checkpoint.QuestionRefreshAnchorPlayerIndex}",
			$"counts={checkpoint.Roster.Count}/{checkpoint.SeatControls.Count}/{checkpoint.ReconnectClaims.Count}",
			$"hands={string.Join(',', checkpoint.Hands.Select(hand => hand.Count))}",
			$"items={string.Join(',', checkpoint.ItemHands.Select(hand => hand.Count))}",
			$"decks={string.Join(',', checkpoint.Decks.Select(deck => deck.Count))}",
			$"table={checkpoint.TableCards.Count}",
			$"controls={string.Join(',', checkpoint.SeatControls.Select(control => $"{control.SeatIndex}:{control.Mode}:{control.PeerId}:{control.CredentialGeneration}:{control.PendingCredentialGeneration}:{control.PendingCredentialPeerId}"))}",
			$"claims={string.Join(',', checkpoint.ReconnectClaims.Select(claim => $"{claim.SeatIndex}:{claim.CredentialGeneration}:{claim.ClaimState}:{claim.ClaimedPeerId}"))}");
		return false;
	}

	internal void CommitNetworkCheckpointEnvelope(AuthorityCheckpointEnvelope envelope)
	{
		if (envelope is null
			|| !envelope.HasPayload
			|| envelope.CheckpointId <= _networkCheckpointId)
		{
			return;
		}

		_networkCheckpointId = envelope.CheckpointId;
	}

	internal bool TryRestoreAuthorityCheckpoint(
		ReadOnlySpan<byte> payload,
		string expectedHash,
		out string error)
	{
		error = string.Empty;
		if (!NetworkAuthorityCheckpointCodec.TryDecode(payload, expectedHash, out NetworkAuthorityCheckpoint checkpoint, out error))
		{
			return false;
		}

		NetworkManager? networkManager = GetNetworkManager();
		NetworkSessionConfig activeConfig = networkManager?.CurrentConfig ?? default;
		if (checkpoint.PlayerCount != player_count
			|| (!string.IsNullOrWhiteSpace(_networkSessionId)
				&& !string.Equals(_networkSessionId, checkpoint.SessionId, StringComparison.Ordinal))
			|| (!string.IsNullOrWhiteSpace(_networkMatchId)
				&& !string.Equals(_networkMatchId, checkpoint.MatchId, StringComparison.Ordinal))
			|| (networkManager != null && !MatchesCheckpointConfig(activeConfig, checkpoint))
			|| !MatchesCheckpointManifest(checkpoint.ManifestHash)
			|| !MatchesCheckpointRoster(networkManager, checkpoint))
		{
			error = "checkpoint does not match the active session, config, manifest, or roster";
			return false;
		}

		EnsureNetworkStateLists();
		EnsureNetworkCardCache();
		if (!TurnGenerationRules.TryGetNextAfterRestore(
				_turnSequenceGeneration,
				checkpoint.TurnSequenceGeneration,
				out int restoredTurnGeneration))
		{
			error = "checkpoint turn generation cannot advance monotonically";
			return false;
		}
		if (!TryBuildRestoredState(
			checkpoint,
			out List<List<PlayingCard>> restoredDecks,
			out List<List<PlayingCard>> restoredHands,
			out List<List<ItemCard>> restoredItemHands,
			out List<TableCard> restoredTableCards,
			out error))
		{
			return false;
		}

		// A reconnecting client can still hold the previous host's pending-choice
		// presentation while the authority checkpoint is being restored. Clear
		// those transient fields before the first post-migration snapshot; the
		// serialized authority phase will recreate a waiter when one is required.
		DisposeActiveSelectionOverlay();
		ClearPendingItemSelectionState();
		ClearNetworkPendingActionState();
		ResetNetworkPredictedClientState();
		_isProcessingCardPlay = false;
		_isResolvingMagnetAttachmentSelection = false;
		_isResolvingTurnSequence = false;
		_isOpeningDealAnimationPlaying = false;

		// All validation and resource construction happens above. Only now mutate
		// the live board, so a rejected checkpoint cannot leave a half-restored game.
		_networkAuthorityEpoch = checkpoint.AuthorityEpoch;
		_networkCheckpointId = checkpoint.CheckpointId;
		_networkSnapshotId = checkpoint.SnapshotId;
		_networkLastAppliedSnapshotId = checkpoint.SnapshotId;
		if (checkpoint.RngSeed != 0)
		{
			_rng.Seed = unchecked((ulong)checkpoint.RngSeed);
			if (ulong.TryParse(checkpoint.RngState, NumberStyles.None, CultureInfo.InvariantCulture, out ulong rngState)
				&& rngState != 0)
			{
				_rng.State = rngState;
			}
		}
		_turnSequenceGeneration = restoredTurnGeneration;
		_turnStateMachine.SyncGeneration(_turnSequenceGeneration);
		_currentTurnPlayerIndex = checkpoint.CurrentTurnPlayerIndex;
		_completedTurnsSinceQuestionCardRefresh = checkpoint.CompletedTurnsSinceQuestionRefresh;
		_questionCardRefreshAnchorPlayerIndex = checkpoint.QuestionRefreshAnchorPlayerIndex;
		_matchOutcomeKind = checkpoint.MatchOutcomeKind;
		_winnerPlayerIndex = checkpoint.WinnerPlayerIndex;
		AuthorityActionState restoredAuthorityAction = checkpoint.AuthorityAction.IsActive
			? checkpoint.AuthorityAction with { TurnGeneration = _turnSequenceGeneration }
			: checkpoint.AuthorityAction;
		for (int seat = 0; seat < player_count; seat++)
		{
			_playerScores[seat] = checkpoint.Scores[seat];
			_playerDecks[seat].Clear();
			_playerDecks[seat].AddRange(restoredDecks[seat]);
			_playerHands[seat].Clear();
			_playerHands[seat].AddRange(restoredHands[seat]);
			_playerItemHands[seat].Clear();
			_playerItemHands[seat].AddRange(restoredItemHands[seat]);
		}

		_tableCards.Clear();
		_tableCards.AddRange(restoredTableCards);
		ShowAllOpeningHandCards();
		ShowAllOpeningPileCards();
		RestoreNetworkAuthorityActionState(
			restoredAuthorityAction,
			checkpoint.LastAuthorityActionId);
		networkManager?.RestoreLastAcceptedClientActionIds(checkpoint.LastAcceptedClientActionIds);
		InvalidateRenderCaches();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	private bool MatchesCheckpointConfig(NetworkSessionConfig activeConfig, NetworkAuthorityCheckpoint checkpoint)
	{
		return string.IsNullOrWhiteSpace(activeConfig.SessionId)
			|| (string.Equals(activeConfig.SessionId, checkpoint.SessionId, StringComparison.Ordinal)
				&& string.Equals(activeConfig.RoomName, checkpoint.RoomName, StringComparison.Ordinal)
				&& activeConfig.RoomPasswordVerifier == new RoomPasswordVerifier
				{
					Version = checkpoint.RoomPasswordVerifierVersion,
					Iterations = checkpoint.RoomPasswordVerifierIterations,
					Salt = checkpoint.RoomPasswordVerifierSalt,
					Verifier = checkpoint.RoomPasswordVerifier,
				}
				&& string.Equals(activeConfig.MatchId, checkpoint.MatchId, StringComparison.Ordinal)
				&& activeConfig.MatchSeed == checkpoint.MatchSeed
				&& activeConfig.Transport == checkpoint.Transport
				&& activeConfig.PlayerCount == checkpoint.PlayerCount
				&& activeConfig.ItemMode == checkpoint.ItemMode
				&& activeConfig.WinningScore == checkpoint.WinningScore);
	}

	private bool MatchesCheckpointManifest(string manifestHash)
	{
		return !string.IsNullOrWhiteSpace(_itemCardCatalog?.GameplayManifestHash)
			&& string.Equals(_itemCardCatalog.GameplayManifestHash, manifestHash, StringComparison.OrdinalIgnoreCase);
	}

	private static bool MatchesCheckpointRoster(NetworkManager? networkManager, NetworkAuthorityCheckpoint checkpoint)
	{
		if (networkManager == null)
		{
			return true;
		}

		List<NetworkMigrationRosterEntry> activeRoster = networkManager.GetCheckpointRoster();
		return activeRoster.Count == checkpoint.Roster.Count
			&& activeRoster.All(active => checkpoint.Roster.Any(expected =>
				active.SeatIndex == expected.SeatIndex
				&& string.Equals(active.PlayerSessionId, expected.PlayerSessionId, StringComparison.Ordinal)
				&& string.Equals(active.DisplayName, expected.DisplayName, StringComparison.Ordinal)
				&& string.Equals(active.AvatarId, expected.AvatarId, StringComparison.Ordinal)));
	}

	private bool TryBuildRestoredState(
		NetworkAuthorityCheckpoint checkpoint,
		out List<List<PlayingCard>> restoredDecks,
		out List<List<PlayingCard>> restoredHands,
		out List<List<ItemCard>> restoredItemHands,
		out List<TableCard> restoredTableCards,
		out string error)
	{
		restoredDecks = new List<List<PlayingCard>>(player_count);
		restoredHands = new List<List<PlayingCard>>(player_count);
		restoredItemHands = new List<List<ItemCard>>(player_count);
		restoredTableCards = new List<TableCard>(checkpoint.TableCards.Count);
		error = string.Empty;

		for (int seat = 0; seat < player_count; seat++)
		{
			if (!TryBuildRestoredCards(checkpoint.Decks[seat], out List<PlayingCard> deck, out error)
				|| !TryBuildRestoredCards(checkpoint.Hands[seat], out List<PlayingCard> hand, out error)
				|| !TryBuildRestoredItems(checkpoint.ItemHands[seat], out List<ItemCard> items, out error))
			{
				return false;
			}

			restoredDecks.Add(deck);
			restoredHands.Add(hand);
			restoredItemHands.Add(items);
		}

		foreach (NetworkCheckpointTableCard tableCard in checkpoint.TableCards)
		{
			PlayingCard? card = null;
			if (tableCard.Card != null)
			{
				if (!TryBuildRestoredCard(tableCard.Card, out PlayingCard restoredCard, out error))
				{
					return false;
				}

				card = restoredCard;
			}

			ItemCard? overlay = null;
			if (!string.IsNullOrEmpty(tableCard.ItemOverlayId))
			{
				if (!TryBuildRestoredItem(tableCard.ItemOverlayId, out ItemCard restoredItem, out error))
				{
					return false;
				}

				overlay = restoredItem;
			}

			restoredTableCards.Add(new TableCard(
				card,
				tableCard.IsFaceUp,
				overlay,
				tableCard.ItemOwnerPlayerIndex,
				tableCard.CardOwnerPlayerIndex,
				tableCard.RemoveCardOnCapture)
			{
				IsBuriedInSand = tableCard.IsBuriedInSand,
				IsSlimeCoated = tableCard.IsSlimeCoated,
			});
		}

		return true;
	}

	private bool TryBuildRestoredCards(
		IReadOnlyList<NetworkCheckpointCard> source,
		out List<PlayingCard> cards,
		out string error)
	{
		cards = new List<PlayingCard>(source.Count);
		error = string.Empty;
		foreach (NetworkCheckpointCard card in source)
		{
			if (!TryBuildRestoredCard(card, out PlayingCard restored, out error))
			{
				return false;
			}

			cards.Add(restored);
		}

		return true;
	}

	private bool TryBuildRestoredCard(
		NetworkCheckpointCard source,
		out PlayingCard card,
		out string error)
	{
		card = default;
		error = string.Empty;
		if (source == null
			|| !_networkCardsByCode.ContainsKey(source.Code)
			|| source.AttachedItemCardIds.Length > 1
			|| source.AttachedItemCardIds.Any(itemId => !IsValidNetworkAttachmentItemCardId(itemId)))
		{
			error = $"checkpoint card cannot be rebuilt: {source?.Code}";
			return false;
		}

		card = CreateNetworkPlayingCard(source.Code, source.AttachedItemCardIds);
		return true;
	}

	private bool TryBuildRestoredItems(
		IReadOnlyList<string> source,
		out List<ItemCard> items,
		out string error)
	{
		items = new List<ItemCard>(source.Count);
		error = string.Empty;
		foreach (string itemId in source)
		{
			if (!TryBuildRestoredItem(itemId, out ItemCard item, out error))
			{
				return false;
			}

			items.Add(item);
		}

		return true;
	}

	private bool TryBuildRestoredItem(string itemId, out ItemCard item, out string error)
	{
		item = default;
		error = string.Empty;
		if (!IsValidNetworkItemCardId(itemId, true))
		{
			error = $"checkpoint item cannot be rebuilt: {itemId}";
			return false;
		}

		item = CreateNetworkVisualItemCard(itemId);
		return true;
	}

	private List<List<NetworkCheckpointCard>> ExportCardLists(List<List<PlayingCard>> source)
	{
		var result = new List<List<NetworkCheckpointCard>>(player_count);
		for (int seat = 0; seat < player_count; seat++)
		{
			result.Add(source[seat].Select(ToCheckpointCard).ToList());
		}

		return result;
	}

	private List<List<string>> ExportItemHands()
	{
		var result = new List<List<string>>(player_count);
		for (int seat = 0; seat < player_count; seat++)
		{
			result.Add(_playerItemHands[seat].Select(item => item.Id).ToList());
		}

		return result;
	}

	private NetworkCheckpointCard ToCheckpointCard(PlayingCard card)
	{
		return new NetworkCheckpointCard
		{
			Code = ToNetworkCardCode(card),
			AttachedItemCardIds = card.GetAttachedItemCardIds().ToArray(),
		};
	}

	private List<NetworkCheckpointTableCard> ExportTableCards()
	{
		return _tableCards.Select(tableCard => new NetworkCheckpointTableCard
		{
			Card = tableCard.Card.HasValue ? ToCheckpointCard(tableCard.Card.Value) : null,
			IsFaceUp = tableCard.IsFaceUp,
			IsBuriedInSand = tableCard.IsBuriedInSand,
			IsSlimeCoated = tableCard.IsSlimeCoated,
			CardOwnerPlayerIndex = tableCard.CardOwnerPlayerIndex,
			RemoveCardOnCapture = tableCard.RemoveCardOnCapture,
			ItemOverlayId = tableCard.ItemOverlay?.Id ?? string.Empty,
			ItemOwnerPlayerIndex = tableCard.ItemOwnerPlayerIndex,
		}).ToList();
	}

}
