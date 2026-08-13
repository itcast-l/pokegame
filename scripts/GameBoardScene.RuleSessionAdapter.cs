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
				SeatCardSupplyLimits: seatCardSupplyLimits)
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
				SeatCardSupplyLimits: seatCardSupplyLimits);

		RuleCommandResult result = _authorityRuleSession.Submit(command);
		if (result.Accepted)
		{
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

	private bool TrySubmitPlayCardFromHandRuleCommand(
		int playerIndex,
		int handIndex,
		int turnSequenceGeneration,
		CardPlayRuleOptions options,
		int tableInsertIndexOverride,
		bool playedCardFacesUp,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot play an authoritative card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlayCardFromHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			handIndex,
			options.PrimaryMatchRank,
			options.AlternateMatchRanks,
			options.CollectEntireTable,
			options.CollectBySuit,
			options.SuitMatchIsFallbackOnly,
			options.InsertAtTableStart,
			tableInsertIndexOverride,
			playedCardFacesUp));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Card play rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitCaptureCardsRuleCommand(
		int playerIndex,
		int captureStartIndex,
		int turnSequenceGeneration,
		IReadOnlyCollection<int>? extraTableCardIndexes,
		bool preserveCardEffectsWhenEnteringDeck,
		IReadOnlyCollection<int>? preservedTableCardIndexes,
		int redirectedTableCardIndex,
		int redirectedPlayerIndex,
		ImmutableArray<RuleScoreMutation> scoreMutations,
		bool restartQuestionRefreshTracking,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot capture authoritative cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new CaptureCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			captureStartIndex,
			ToSortedRuleIndexes(extraTableCardIndexes),
			preserveCardEffectsWhenEnteringDeck,
			ToSortedRuleIndexes(preservedTableCardIndexes),
			redirectedTableCardIndex,
			redirectedTableCardIndex >= 0 ? redirectedPlayerIndex : -1,
			scoreMutations,
			restartQuestionRefreshTracking));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Capture rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitScoreMutationsRuleCommand(
		ImmutableArray<RuleScoreMutation> mutations,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot change authoritative scores.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ApplyScoreMutationsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			mutations));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Score rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitDrawCardsRuleCommand(
		int playerIndex,
		int drawCount,
		int maximumHandSize,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot draw authoritative cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new DrawCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			playerIndex,
			drawCount,
			maximumHandSize));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Draw rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitReturnCardsToHandRuleCommand(
		int playerIndex,
		ImmutableArray<long> cardRuntimeInstanceIds,
		int maximumHandSize,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot return authoritative cards to a hand.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ReturnCardsToHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			playerIndex,
			cardRuntimeInstanceIds,
			maximumHandSize));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Return-to-hand rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitPlayItemFromHandRuleCommand(
		int playerIndex,
		int itemHandIndex,
		string expectedItemId,
		int turnSequenceGeneration,
		RuleItemPlayDestination destination,
		int tableIndex,
		bool tableItemFacesUp,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot play an authoritative item card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlayItemFromHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			itemHandIndex,
			expectedItemId,
			destination,
			tableIndex,
			tableItemFacesUp));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item play rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitDiscardHandCardRuleCommand(
		int playerIndex,
		int displayIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot discard an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new DiscardHandCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			displayIndex,
			expectedCardRuntimeInstanceId,
			expectedItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Hand discard rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitGrantItemToHandRuleCommand(
		int playerIndex,
		string itemId,
		int maximumHandSize,
		int discardDisplayIndex,
		long expectedDiscardCardRuntimeInstanceId,
		string expectedDiscardItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot grant an authoritative item card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new GrantItemToHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			itemId,
			maximumHandSize,
			discardDisplayIndex,
			expectedDiscardCardRuntimeInstanceId,
			expectedDiscardItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item reward rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitReplaceItemInHandRuleCommand(
		int playerIndex,
		int itemHandIndex,
		string expectedItemId,
		string replacementItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot replace an authoritative item card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ReplaceItemInHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			itemHandIndex,
			expectedItemId,
			replacementItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item replacement rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitAttachItemToHandCardRuleCommand(
		int playerIndex,
		int handIndex,
		string itemId,
		ImmutableArray<int> randomCandidateHandIndexes,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot attach an authoritative item effect.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new AttachItemToHandCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			handIndex,
			itemId,
			randomCandidateHandIndexes));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item attachment rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitChooseRandomHandDisplayIndexRuleCommand(
		int actingPlayerIndex,
		int handPlayerIndex,
		ImmutableArray<int> excludedDisplayIndexes,
		int turnSequenceGeneration,
		out int displayIndex)
	{
		displayIndex = -1;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot choose an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomHandDisplayIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				handPlayerIndex,
				excludedDisplayIndexes));
		if (result.Accepted)
		{
			displayIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random hand selection rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomHandCardIndexRuleCommand(
		int playerIndex,
		ImmutableArray<int> candidateHandIndexes,
		int turnSequenceGeneration,
		out int handIndex)
	{
		handIndex = -1;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot choose an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomHandCardIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				playerIndex,
				candidateHandIndexes));
		if (result.Accepted)
		{
			handIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random attachment target rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomTargetSeatRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<int> candidatePlayerIndexes,
		int turnSequenceGeneration,
		out int targetPlayerIndex)
	{
		targetPlayerIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomTargetSeatCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				candidatePlayerIndexes));
		if (result.Accepted)
		{
			targetPlayerIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random target-seat rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseWeightedOptionRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleWeightedOption> options,
		int turnSequenceGeneration,
		out string selectedId)
	{
		selectedId = string.Empty;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseWeightedOptionCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				options));
		if (result.Accepted)
		{
			selectedId = result.Mutation.LogicalItemId;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Weighted-option rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomTableCardIndexRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> candidates,
		int turnSequenceGeneration,
		out int tableCardIndex)
	{
		tableCardIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomTableCardIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				candidates));
		if (result.Accepted)
		{
			tableCardIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random table-card rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomIndexRuleCommand(
		int actingPlayerIndex,
		int minimumInclusive,
		int maximumInclusive,
		int turnSequenceGeneration,
		out int selectedIndex)
	{
		selectedIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				minimumInclusive,
				maximumInclusive));
		if (result.Accepted)
		{
			selectedIndex = result.Mutation.SourceIndex;
			return true;
		}

		GD.PushWarning(
			$"Random-index rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitChooseReservoirHandDisplayIndexRuleCommand(
		int actingPlayerIndex,
		int handPlayerIndex,
		ImmutableArray<int> candidateDisplayIndexes,
		int turnSequenceGeneration,
		out int selectedIndex)
	{
		selectedIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseReservoirHandDisplayIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				handPlayerIndex,
				candidateDisplayIndexes));
		if (result.Accepted)
		{
			selectedIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Reservoir hand-index rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitTransferHandCardRuleCommand(
		int actingPlayerIndex,
		int sourcePlayerIndex,
		int sourceDisplayIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		int maximumHandSize,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot transfer an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new TransferHandCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourcePlayerIndex,
			sourceDisplayIndex,
			maximumHandSize,
			expectedCardRuntimeInstanceId,
			expectedItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Hand transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitTransferDeckCardsRuleCommand(
		int actingPlayerIndex,
		int sourcePlayerIndex,
		int maximumCount,
		bool preserveCardEffectsWhenEnteringDeck,
		ImmutableArray<long> expectedSourceTopCardRuntimeInstanceIds,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot transfer authoritative deck cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new TransferDeckCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourcePlayerIndex,
			maximumCount,
			preserveCardEffectsWhenEnteringDeck,
			expectedSourceTopCardRuntimeInstanceIds));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Deck transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitTransferTopDeckCardRuleCommand(
		int actingPlayerIndex,
		int sourcePlayerIndex,
		long expectedRuntimeInstanceId,
		bool preserveCardEffectsWhenEnteringDeck,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new TransferTopDeckCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourcePlayerIndex,
			expectedRuntimeInstanceId,
			preserveCardEffectsWhenEnteringDeck));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Top-deck transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitShuffleDeckRuleCommand(
		int actingPlayerIndex,
		int deckPlayerIndex,
		bool cutAfterShuffle,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new ShuffleDeckCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			deckPlayerIndex,
			cutAfterShuffle));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Deck-shuffle rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitPlaceTableItemRuleCommand(
		int actingPlayerIndex,
		string itemId,
		int tableIndex,
		bool isFaceUp,
		int itemOwnerPlayerIndex,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot place an authoritative table item.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlaceTableItemCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			itemId,
			tableIndex,
			isFaceUp,
			itemOwnerPlayerIndex));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table item placement rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitPlaceTableCardRuleCommand(
		int actingPlayerIndex,
		PlayingCard card,
		int tableIndex,
		bool isFaceUp,
		int cardOwnerPlayerIndex,
		bool removeOnCapture,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlaceTableCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			ToRuleCardState(card),
			tableIndex,
			isFaceUp,
			cardOwnerPlayerIndex,
			removeOnCapture));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card placement rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitChooseTableCardsRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> candidates,
		int maximumCount,
		int turnSequenceGeneration,
		out ImmutableArray<int> selectedIndexes)
	{
		selectedIndexes = ImmutableArray<int>.Empty;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot choose authoritative table cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new ChooseTableCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			candidates,
			maximumCount));
		if (result.Accepted)
		{
			var indexes = ImmutableArray.CreateBuilder<int>(result.PresentationIntents.Length);
			foreach (PresentationIntent intent in result.PresentationIntents)
			{
				if (intent.Kind == PresentationIntentKind.TableCardsSelected)
				{
					indexes.Add(intent.TargetIndex);
				}
			}
			selectedIndexes = indexes.ToImmutable();
			return selectedIndexes.Length == result.Mutation.CollectedCardCount;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Table-card selection rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitSetTableCardsFaceUpRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		bool expectedFaceUp,
		bool faceUp,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new SetTableCardsFaceUpCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			expectedFaceUp,
			faceUp));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card face-state rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitSetTableCardsHiddenRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		bool expectedHidden,
		bool hidden,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new SetTableCardsHiddenCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			expectedHidden,
			hidden));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card visibility rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitSetTableCardsScoreSuppressedRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		bool expectedScoreSuppressed,
		bool scoreSuppressed,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new SetTableCardsScoreSuppressedCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			expectedScoreSuppressed,
			scoreSuppressed));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card score-suppression rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitRemoveTableCardAttachmentRuleCommand(
		int actingPlayerIndex,
		int tableCardIndex,
		long expectedTableCardRuntimeInstanceId,
		string itemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new RemoveTableCardAttachmentCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCardIndex,
			expectedTableCardRuntimeInstanceId,
			itemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card attachment removal rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitRemoveTableCardRangeRuleCommand(
		int actingPlayerIndex,
		int startIndex,
		ImmutableArray<long> expectedRuntimeInstanceIds,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new RemoveTableCardRangeCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			startIndex,
			expectedRuntimeInstanceIds));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card removal rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitMoveTableCardRuleCommand(
		int actingPlayerIndex,
		int sourceIndex,
		int targetIndex,
		long expectedRuntimeInstanceId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new MoveTableCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourceIndex,
			targetIndex,
			expectedRuntimeInstanceId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card move rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitMoveTableCardToDeckRuleCommand(
		int actingPlayerIndex,
		int tableCardIndex,
		long expectedTableCardRuntimeInstanceId,
		int deckPlayerIndex,
		bool preserveCardEffectsWhenEnteringDeck,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new MoveTableCardToDeckCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCardIndex,
			expectedTableCardRuntimeInstanceId,
			deckPlayerIndex,
			preserveCardEffectsWhenEnteringDeck));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card deck transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitPlanTableShuffleRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		ImmutableArray<int> fixedTableIndexes,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlanTableShuffleCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			fixedTableIndexes));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Table-shuffle planning rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitBeginMandatorySelectionRuleCommand(
		int actingPlayerIndex,
		MandatorySelectionKind kind,
		int sourceHandIndex,
		string itemId,
		ImmutableArray<int> legalSelectionIndexes,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new BeginMandatorySelectionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			kind,
			sourceHandIndex,
			itemId,
			legalSelectionIndexes));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Mandatory-selection begin rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitResolveMandatorySelectionRuleCommand(
		int actingPlayerIndex,
		MandatorySelectionKind expectedKind,
		int selectionIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ResolveMandatorySelectionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			expectedKind,
			selectionIndex,
			expectedCardRuntimeInstanceId,
			expectedItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Mandatory-selection resolve rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitClearMandatorySelectionRuleCommand(
		int actingPlayerIndex,
		MandatorySelectionKind expectedKind,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ClearMandatorySelectionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			expectedKind));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Mandatory-selection clear rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private ImmutableArray<PendingAttachmentRewardState> GetPendingAttachmentRewards()
	{
		if (_authorityRuleSession != null)
		{
			return _authorityRuleSession.ReadModel.NormalizedPendingAttachmentRewards;
		}

		return _replicaRuleSession?.ReadModel.NormalizedPendingAttachmentRewards
			?? ImmutableArray<PendingAttachmentRewardState>.Empty;
	}

	private bool TrySubmitEnqueuePendingAttachmentRewardRuleCommand(
		int seat,
		string itemId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new EnqueuePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				seat,
				itemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Pending attachment enqueue was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TryTakePendingAttachmentReward(
		int turnSequenceGeneration,
		out PendingAttachmentRewardState reward)
	{
		reward = default;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		ImmutableArray<PendingAttachmentRewardState> rewards =
			current.NormalizedPendingAttachmentRewards;
		if (rewards.IsEmpty)
		{
			return false;
		}

		reward = rewards[0];
		RuleCommandResult result = _authorityRuleSession.Submit(
			new TakePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				reward.Seat,
				reward.ItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Pending attachment take was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		reward = default;
		return false;
	}

	private bool TrySubmitRemovePendingAttachmentRewardRuleCommand(
		int seat,
		string itemId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new RemovePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				seat,
				itemId));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Pending attachment removal was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitReplacePendingAttachmentRewardRuleCommand(
		int seat,
		string expectedItemId,
		string replacementItemId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ReplacePendingAttachmentRewardCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				seat,
				expectedItemId,
				replacementItemId));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Pending attachment replacement was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitSetPendingAttachmentRewardsRuleCommand(
		ImmutableArray<PendingAttachmentRewardState> rewards,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new SetPendingAttachmentRewardsCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				rewards));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Pending attachment queue restore was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitBeginRealTurnRuleCommand(
		int actingPlayerIndex,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new BeginRealTurnCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Real-turn begin rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitEndRealTurnRuleCommand(
		int actingPlayerIndex,
		int turnSequenceGeneration,
		out ExtraActionCommandOutcome outcome)
	{
		outcome = new ExtraActionCommandOutcome();
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new EndRealTurnCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex));
		if (result.Accepted)
		{
			outcome = result.ExtraActionOutcome ?? new ExtraActionCommandOutcome();
			return true;
		}

		GD.PushWarning(
			$"Real-turn end rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitRegisterRealTurnEndMarkerRuleCommand(
		int actingPlayerIndex,
		string markerId,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new RegisterRealTurnEndMarkerCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				markerId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Real-turn marker rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitGrantExtraActionRuleCommand(
		int actingPlayerIndex,
		string sourceId,
		bool resumeNormalTurnAfterChain,
		ExtraActionReentryPolicy reentryPolicy,
		bool oncePerRealTurn,
		int turnSequenceGeneration,
		out ExtraActionCommandOutcome outcome)
	{
		outcome = new ExtraActionCommandOutcome();
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new GrantExtraActionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourceId,
			resumeNormalTurnAfterChain,
			reentryPolicy,
			oncePerRealTurn));
		if (result.Accepted)
		{
			outcome = result.ExtraActionOutcome ?? new ExtraActionCommandOutcome();
			return true;
		}

		GD.PushWarning(
			$"Extra-action grant rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitAcceptExtraActionRuleCommand(
		int actingPlayerIndex,
		ExtraActionGrantState? expectedGrant,
		int turnSequenceGeneration,
		out ExtraActionCommandOutcome outcome)
	{
		outcome = new ExtraActionCommandOutcome();
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new AcceptExtraActionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			expectedGrant));
		if (result.Accepted)
		{
			outcome = result.ExtraActionOutcome ?? new ExtraActionCommandOutcome();
			return true;
		}

		GD.PushWarning(
			$"Extra-action accept rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitFinishExtraActionExecutionRuleCommand(
		int actingPlayerIndex,
		ExtraActionGrantState? acceptedGrant,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new FinishExtraActionExecutionCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				acceptedGrant));
		if (result.Accepted || result.Rejection.Code == RuleRejectionCode.SessionEnded)
		{
			return true;
		}

		GD.PushWarning(
			$"Extra-action finish rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitCompleteExtraActionRuleCommand(
		int actingPlayerIndex,
		ExtraActionGrantState consumedGrant,
		int turnSequenceGeneration,
		out ExtraActionCommandOutcome outcome)
	{
		outcome = new ExtraActionCommandOutcome();
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new CompleteExtraActionCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			consumedGrant));
		if (result.Accepted)
		{
			outcome = result.ExtraActionOutcome ?? new ExtraActionCommandOutcome();
			return true;
		}

		GD.PushWarning(
			$"Extra-action completion rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitAbortExtraActionChainRuleCommand(
		int actingPlayerIndex,
		int turnSequenceGeneration,
		out ExtraActionCommandOutcome outcome)
	{
		outcome = new ExtraActionCommandOutcome();
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new AbortExtraActionChainCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex));
		if (result.Accepted)
		{
			outcome = result.ExtraActionOutcome ?? new ExtraActionCommandOutcome();
			return true;
		}

		GD.PushWarning(
			$"Extra-action abort rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitAdvanceTurnRuleCommand(
		int actingPlayerIndex,
		bool captured,
		int candidateNextPlayerIndex,
		bool advanceQuestionRefresh,
		bool hasTableSpecialCard,
		bool hasTableQuestionCard,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot advance the authoritative turn.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new AdvanceTurnCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			actingPlayerIndex,
			captured,
			candidateNextPlayerIndex,
			advanceQuestionRefresh,
			hasTableSpecialCard,
			advanceQuestionRefresh ? QuestionCardRefreshCyclesAfterCapture : 0,
			hasTableQuestionCard,
			advanceQuestionRefresh && hasTableQuestionCard
				? QuestionCardUncollectedLifetimeTurns
				: 0));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Turn rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitAdvanceTurnGenerationRuleCommand(out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new AdvanceTurnGenerationCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration));
		return result.Accepted;
	}

	private bool TrySubmitEvaluateMatchOutcomeRuleCommand(
		int scoreOrLifeWinnerPlayerIndex,
		ImmutableArray<bool> hasPlayableItemActions,
		bool settleRegardlessOfAvailableActions,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot resolve the authoritative match outcome.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new EvaluateMatchOutcomeCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			scoreOrLifeWinnerPlayerIndex,
			hasPlayableItemActions,
			settleRegardlessOfAvailableActions));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.SessionEnded)
		{
			GD.PushWarning(
				$"Match outcome rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
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
