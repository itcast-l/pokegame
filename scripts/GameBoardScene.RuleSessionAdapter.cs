using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private long _nextRuleActionId;

	internal bool UsesReplicaRuleSession => _replicaRuleSession != null;
	private MatchReadModel? CurrentRuleReadModel =>
		_authorityRuleSession?.ReadModel ?? _replicaRuleSession?.ReadModel;
	private MatchFlowState CurrentMatchFlowState =>
		CurrentRuleReadModel?.NormalizedMatchFlow ?? MatchFlowState.Empty;
	private NetworkAiBlockedItemState CurrentNetworkAiBlockedItem => new(
		CurrentMatchFlowState.BlockedAutomatedItemSeat,
		CurrentMatchFlowState.BlockedAutomatedItemId);
	private GameBoardRuleReadProjection CurrentRuleProjection
	{
		get
		{
			MatchReadModel? readModel = CurrentRuleReadModel;
			if (readModel != null)
			{
				_ruleReadProjection.Synchronize(readModel);
			}
			return _ruleReadProjection;
		}
	}

	private void InitializeRuleSessionForCurrentMode()
	{
		_replicaTurnSequenceGenerationOverride = -1;
		if (IsNetworkMultiplayerMode() && !IsNetworkHost())
		{
			_replicaRuleSession = new ReplicaRuleSession();
			_authorityRuleSession = null;
		}
		else
		{
			_authorityRuleSession = new AuthorityRuleSession(
				_ruleRandomSource,
				GameModeRuleProfileMapper.Create(_gameModePolicy, _itemMode));
			_replicaRuleSession = null;
		}
	}

	private bool TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
	{
		if (_authorityRuleSession != null)
		{
			MatchReadModel current = _authorityRuleSession.ReadModel;
			if (current.IsInitialized)
			{
				readModel = current;
				return true;
			}
		}

		readModel = null!;
		return false;
	}

	private bool TryUpdateMatchFlowState(
		Func<MatchFlowState, MatchFlowState> update)
	{
		ArgumentNullException.ThrowIfNull(update);
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		if (!current.IsInitialized || current.Outcome == Deckland.Rules.MatchOutcome.Ended)
		{
			return false;
		}

		MatchFlowState next = update(current.NormalizedMatchFlow);
		if (Equals(next, current.NormalizedMatchFlow))
		{
			return true;
		}

		RuleCommandResult result = _authorityRuleSession.Submit(
			new ReplaceMatchFlowStateCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				current.TurnGeneration,
				next));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushError(
			$"Match flow update failed: {result.Rejection.Code} "
			+ result.Rejection.Detail);
		return false;
	}

	private static bool TryGetRulePlayer(
		MatchReadModel readModel,
		int playerIndex,
		out PlayerStateSnapshot player)
	{
		if (playerIndex >= 0
			&& playerIndex < readModel.Players.Length
			&& readModel.Players[playerIndex].Seat == playerIndex)
		{
			player = readModel.Players[playerIndex];
			return true;
		}

		player = null!;
		return false;
	}

	private static PlayingCard FromRuleCardState(LogicalCardState card)
	{
		return PlayingCard.Restore(
			card.RuntimeInstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.NormalizedAttachedEffectIds);
	}

	private CaptureStatePlan CreateAuthorityCapturePlan(
		int captureStartIndex,
		IReadOnlyCollection<int>? extraTableCardIndexes)
	{
		return _authorityRuleSession?.CreateCapturePlan(
			captureStartIndex,
			extraTableCardIndexes) ?? CaptureStatePlan.Invalid;
	}

	private bool TryCaptureAuthorityRuleSnapshot(
		out MatchStateSnapshot snapshot,
		out string error)
	{
		if (_authorityRuleSession == null)
		{
			snapshot = null!;
			error = "authority_rule_session_unavailable";
			return false;
		}

		try
		{
			snapshot = _authorityRuleSession.CaptureSnapshot();
			error = string.Empty;
			return true;
		}
		catch (InvalidOperationException exception)
		{
			snapshot = null!;
			error = exception.Message;
			return false;
		}
	}

	private bool TryRestoreAuthorityRuleSnapshot(
		MatchStateSnapshot snapshot,
		out string error)
	{
		RuleRandomState previousRandomState = _ruleRandomSource.CaptureState();
		try
		{
			AuthorityRuleSession restored = AuthorityRuleSession.Restore(
				snapshot,
				_ruleRandomSource,
				GameModeRuleProfileMapper.Create(_gameModePolicy, _itemMode));
			_authorityRuleSession = restored;
			_replicaRuleSession = null;
			_replicaTurnSequenceGenerationOverride = -1;
			AdventureRunSessionStore? adventureStore = GetAdventureRunSessionStore();
			if (_gameModePolicy.CanReadAdventureRun
				&& adventureStore != null
				&& !adventureStore.BindAuthorityRuleSession(restored))
			{
				throw new InvalidOperationException(
					"The restored match session could not take ownership of the adventure run.");
			}
			if (!snapshot.AppliedActionIds.IsDefaultOrEmpty)
			{
				foreach (long actionId in snapshot.AppliedActionIds)
				{
					_nextRuleActionId = Math.Max(_nextRuleActionId, actionId);
				}
			}

			SynchronizeSceneAfterRuleInitialization();
			error = string.Empty;
			return true;
		}
		catch (ArgumentException exception)
		{
			_ruleRandomSource.RestoreState(previousRandomState);
			error = exception.Message;
			return false;
		}
		catch (InvalidOperationException exception)
		{
			_ruleRandomSource.RestoreState(previousRandomState);
			error = exception.Message;
			return false;
		}
	}

	private bool TryInitializeRuleMatch(
		IReadOnlyList<PlayingCard> deck,
		bool createOpeningPresentationBarrier)
	{
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot initialize an authoritative match.");
			return false;
		}

		if (deck.Count == 0)
		{
			GD.PushError("Rule match initialization rejected an empty deck.");
			return false;
		}

		var logicalDeck = ImmutableArray.CreateBuilder<LogicalCardState>(deck.Count);
		foreach (PlayingCard card in deck)
		{
			logicalDeck.Add(new LogicalCardState(
				card.InstanceId,
				card.Code,
				card.Suit,
				card.Rank,
				card.GetAttachedItemCardIds().ToImmutableArray()));
		}
		ImmutableArray<LogicalCardState> initialDeck = logicalDeck.MoveToImmutable();
		var startingScoreBuilder = ImmutableArray.CreateBuilder<int>(player_count);
		for (int seat = 0; seat < player_count; seat++)
		{
			startingScoreBuilder.Add(GetStartingPlayerScore(seat));
		}
		ImmutableArray<int> startingScores = startingScoreBuilder.MoveToImmutable();
		var additionalDrawBuilder = ImmutableArray.CreateBuilder<int>(player_count);
		var cardSupplyLimitBuilder = ImmutableArray.CreateBuilder<int>(player_count);
		int mainPlayerAdditionalDraws = GetAdventureBattleStartDrawCount();
		for (int seat = 0; seat < player_count; seat++)
		{
			additionalDrawBuilder.Add(seat == MainPlayerIndex ? mainPlayerAdditionalDraws : 0);
			bool limitToOneCard = player_count == 2
				&& (seat == MainPlayerIndex
					? debug_main_player_has_one_card
					: debug_opponent_has_one_card);
			cardSupplyLimitBuilder.Add(limitToOneCard ? 1 : -1);
		}
		ImmutableArray<int> additionalStartingDraws = additionalDrawBuilder.MoveToImmutable();
		ImmutableArray<int> seatCardSupplyLimits = cardSupplyLimitBuilder.MoveToImmutable();
		if (!TryCreateOpeningDiceOutcome(
			out int openingDiceRollResult,
			out int startingTurnSeat))
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		long actionId = NextRuleActionId();
		int nextLifecycleGeneration = current.IsInitialized
			? checked(current.LifecycleGeneration + 1)
			: 1;
		MatchCommand command = current.IsInitialized
			? new RestartMatchCommand(
				actionId,
				current.Revision,
				nextLifecycleGeneration,
				current.TurnGeneration,
				player_count,
				winning_score,
				StartingHandCards,
				DealEqualRanks: true,
				Deck: initialDeck,
				CreateOpeningPresentationBarrier: createOpeningPresentationBarrier,
				StartingScores: startingScores,
				MaximumHandSize: MaxTotalHandCards,
				AdditionalStartingDraws: additionalStartingDraws,
				SeatCardSupplyLimits: seatCardSupplyLimits,
				StartingTurnSeat: startingTurnSeat)
			: new InitializeMatchCommand(
				actionId,
				nextLifecycleGeneration,
				player_count,
				winning_score,
				StartingHandCards,
				DealEqualRanks: true,
				Deck: initialDeck,
				CreateOpeningPresentationBarrier: createOpeningPresentationBarrier,
				StartingScores: startingScores,
				MaximumHandSize: MaxTotalHandCards,
				AdditionalStartingDraws: additionalStartingDraws,
				SeatCardSupplyLimits: seatCardSupplyLimits,
				StartingTurnSeat: startingTurnSeat);

		RuleCommandResult result = _authorityRuleSession.Submit(command);
		if (result.Accepted)
		{
			CommitOpeningDiceOutcome(openingDiceRollResult, startingTurnSeat);
			SynchronizeSceneAfterRuleInitialization();
			return true;
		}

		GD.PushError(
			$"Rule match initialization failed: {result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TryInitializeScriptedRuleMatch(
		ImmutableArray<PlayerStateSnapshot> players,
		ImmutableArray<TableCardState> tableCards,
		int currentTurnSeat = MainPlayerIndex)
	{
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot initialize a scripted match.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		var command = new InitializeScriptedMatchCommand(
			NextRuleActionId(),
			ExpectedRevision: current.IsInitialized ? current.Revision : 0,
			LifecycleGeneration: current.IsInitialized
				? checked(current.LifecycleGeneration + 1)
				: 1,
			TurnGeneration: current.IsInitialized ? current.TurnGeneration : 0,
			PlayerCount: player_count,
			WinningScore: winning_score,
			Players: players,
			TableCards: tableCards,
			CurrentTurnSeat: currentTurnSeat);
		RuleCommandResult result = _authorityRuleSession.Submit(command);
		if (result.Accepted)
		{
			SynchronizeSceneAfterRuleInitialization();
			return true;
		}

		GD.PushError(
			$"Scripted rule match initialization failed: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private void SynchronizeSceneAfterRuleInitialization()
	{
		_isResolvingMagnetAttachmentSelection = false;
		_isAnimatingMagnetAttachment = false;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
	}

	private static LogicalCardState ToRuleCardState(PlayingCard card)
	{
		return new LogicalCardState(
			card.InstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.GetAttachedItemCardIds().ToImmutableArray());
	}

	private static TableCardState ToRuleTableCardState(TableCard tableCard)
	{
		return new TableCardState(
			tableCard.InstanceId,
			tableCard.Card.HasValue ? ToRuleCardState(tableCard.Card.Value) : null,
			tableCard.IsFaceUp,
			tableCard.IsHiddenDuringCapture,
			tableCard.IsBuriedInSand,
			tableCard.IsSlimeCoated,
			tableCard.ItemOverlay.HasValue
				? new LogicalItemState(tableCard.ItemOverlay.Value.Id)
				: null,
			tableCard.ItemOwnerPlayerIndex,
			tableCard.CardOwnerPlayerIndex,
			tableCard.RemoveCardOnCapture);
	}

	private bool TryCompleteOpeningRulePresentationBarrier(int turnSequenceGeneration)
	{
		return TryCompleteRulePresentationBarrier(
			turnSequenceGeneration,
			"Opening presentation barrier completion");
	}

	private bool TryCompleteRulePresentationBarrier(
		int turnSequenceGeneration,
		string rejectionContext)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel readModel = _authorityRuleSession.ReadModel;
		if (!readModel.HasPendingPresentationBarrier)
		{
			return true;
		}

		RuleCommandResult result = _authorityRuleSession.Submit(
			new CompletePresentationBarrierCommand(
				NextRuleActionId(),
				readModel.Revision,
				readModel.LifecycleGeneration,
				turnSequenceGeneration,
				readModel.PendingPresentationBarrierId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"{rejectionContext} was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TryApplyReplicaRuleSnapshot(ValidatedNetworkBoardSnapshot snapshot)
	{
		if (_replicaRuleSession == null)
		{
			return true;
		}

		long nextRuntimeInstanceId = 1;
		var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(player_count);
		for (int seat = 0; seat < player_count; seat++)
		{
			var deck = ImmutableArray.CreateBuilder<LogicalCardState>(snapshot.DeckCounts[seat]);
			for (int index = 0; index < snapshot.DeckCounts[seat]; index++)
			{
				deck.Add(CreateRedactedReplicaCard(nextRuntimeInstanceId++, seat, "deck", index));
			}

			var hand = ImmutableArray.CreateBuilder<LogicalCardState>(snapshot.HandCounts[seat]);
			if (seat == GetLocalHandPlayerIndex())
			{
				foreach (NetworkPlayingCardSnapshot card in snapshot.OwnHandCards)
				{
					PlayingCard source = CreateNetworkPlayingCard(card.CardCode, card.AttachedItemCardIds);
					hand.Add(new LogicalCardState(
						nextRuntimeInstanceId++,
						source.Code,
						source.Suit,
						source.Rank,
						card.AttachedItemCardIds.ToImmutableArray()));
				}
			}
			else
			{
				for (int index = 0; index < snapshot.HandCounts[seat]; index++)
				{
					hand.Add(CreateRedactedReplicaCard(nextRuntimeInstanceId++, seat, "hand", index));
				}
			}

			var itemHand = ImmutableArray.CreateBuilder<LogicalItemState>(snapshot.ItemHandCounts[seat]);
			if (seat == GetLocalHandPlayerIndex())
			{
				foreach (string itemCardId in snapshot.OwnItemHandCardIds)
				{
					itemHand.Add(new LogicalItemState(itemCardId));
				}
			}
			else
			{
				for (int index = 0; index < snapshot.ItemHandCounts[seat]; index++)
				{
					itemHand.Add(new LogicalItemState($"redacted:{seat}:item:{index}"));
				}
			}

			players.Add(new PlayerStateSnapshot(
				seat,
				deck.MoveToImmutable(),
				hand.MoveToImmutable(),
				itemHand.MoveToImmutable(),
				snapshot.Scores[seat]));
		}

		var tableCards = ImmutableArray.CreateBuilder<TableCardState>(snapshot.TableCards.Length);
		foreach (NetworkTableCardSnapshot tableCard in snapshot.TableCards)
		{
			LogicalCardState? logicalCard = null;
			if (!string.IsNullOrEmpty(tableCard.CardCode))
			{
				PlayingCard source = CreateNetworkPlayingCard(
					tableCard.CardCode,
					tableCard.AttachedItemCardIds);
				logicalCard = new LogicalCardState(
					nextRuntimeInstanceId++,
					source.Code,
					source.Suit,
					source.Rank,
					tableCard.AttachedItemCardIds.ToImmutableArray());
			}

			tableCards.Add(new TableCardState(
				nextRuntimeInstanceId++,
				logicalCard,
				tableCard.IsFaceUp,
				IsHiddenDuringCapture: false,
				tableCard.IsBuriedInSand,
				tableCard.IsSlimeCoated,
				string.IsNullOrEmpty(tableCard.ItemOverlayId)
					? null
					: new LogicalItemState(tableCard.ItemOverlayId),
				tableCard.ItemOwnerPlayerIndex,
				tableCard.CardOwnerPlayerIndex,
				tableCard.RemoveCardOnCapture));
		}

		int lifecycleGeneration = snapshot.AuthorityEpoch >= int.MaxValue
			? int.MaxValue
			: checked((int)snapshot.AuthorityEpoch + 1);
		ImmutableArray<PlayerStateSnapshot> playerSnapshots = players.MoveToImmutable();
		MandatorySelectionState mandatorySelection = CreateReplicaMandatorySelection(
			snapshot,
			playerSnapshots);
		var ruleSnapshot = new MatchStateSnapshot(
			MatchStateSnapshot.CurrentSchemaVersion,
			snapshot.SnapshotId,
			lifecycleGeneration,
			snapshot.TurnSequenceGeneration,
			player_count,
			winning_score,
			snapshot.CurrentTurnPlayerIndex,
			snapshot.MatchOutcomeKind switch
			{
				MatchOutcomeKind.Winner => Deckland.Rules.MatchOutcome.Won,
				MatchOutcomeKind.Draw => Deckland.Rules.MatchOutcome.Draw,
				_ => Deckland.Rules.MatchOutcome.InProgress,
			},
			snapshot.WinnerPlayerIndex,
			playerSnapshots,
			tableCards.MoveToImmutable(),
			new RuleRandomState(0, 0),
			PendingPresentationBarrierId: 0,
			ImmutableArray<long>.Empty,
			MandatorySelection: mandatorySelection,
			PendingAttachmentRewards:
				BuildPendingAttachmentRewardsFromAuthorityAction(snapshot.AuthorityAction));

		SnapshotApplyResult result = _replicaRuleSession.ApplyAuthoritySnapshot(ruleSnapshot);
		if (result.Applied)
		{
			_replicaTurnSequenceGenerationOverride = -1;
			return true;
		}

		GD.PushWarning(
			$"Replica rule snapshot rejected: {result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private static LogicalCardState CreateRedactedReplicaCard(
		long runtimeInstanceId,
		int seat,
		string zone,
		int index)
	{
		return new LogicalCardState(
			runtimeInstanceId,
			$"redacted:{seat}:{zone}:{index}",
			"redacted",
			"redacted");
	}

	private static MandatorySelectionState CreateReplicaMandatorySelection(
		ValidatedNetworkBoardSnapshot snapshot,
		ImmutableArray<PlayerStateSnapshot> players)
	{
		MandatorySelectionKind kind = snapshot.PendingSelectionMode switch
		{
			PendingItemSelectionMode.HandLimitDiscard => MandatorySelectionKind.HandLimitDiscard,
			PendingItemSelectionMode.AttachmentRewardHandCard => MandatorySelectionKind.AttachmentHandCard,
			_ => MandatorySelectionKind.None,
		};
		if (kind == MandatorySelectionKind.None)
		{
			return MandatorySelectionState.None;
		}

		PlayerStateSnapshot player = players[snapshot.PendingSelectionOwner];
		var legalIndexes = ImmutableArray.CreateBuilder<int>();
		if (kind == MandatorySelectionKind.HandLimitDiscard)
		{
			for (int index = 0; index < player.Hand.Length + player.ItemHand.Length; index++)
			{
				legalIndexes.Add(index);
			}
		}
		else
		{
			for (int index = 0; index < player.Hand.Length; index++)
			{
				if (!player.Hand[index].NormalizedAttachedEffectIds.Contains(
					snapshot.PendingItemCardId,
					StringComparer.Ordinal))
				{
					legalIndexes.Add(index);
				}
			}
		}

		return new MandatorySelectionState(
			kind,
			snapshot.PendingSelectionOwner,
			SourceHandIndex: -1,
			snapshot.PendingTurnSequenceGeneration,
			kind == MandatorySelectionKind.HandLimitDiscard
				? string.Empty
				: snapshot.PendingItemCardId,
			legalIndexes.ToImmutable());
	}


	private static ImmutableArray<int> ToSortedRuleIndexes(IReadOnlyCollection<int>? indexes)
	{
		if (indexes == null || indexes.Count == 0)
		{
			return ImmutableArray<int>.Empty;
		}

		int[] sorted = new int[indexes.Count];
		int index = 0;
		foreach (int value in indexes)
		{
			sorted[index++] = value;
		}
		Array.Sort(sorted);
		return ImmutableArray.Create(sorted);
	}

	private long NextRuleActionId()
	{
		return checked(++_nextRuleActionId);
	}
}
