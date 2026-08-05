using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private const int CompatibilityPlayerCapacity = 4;

	private readonly IRuleRandomSource _randomSource;
	private readonly GameBoardState _state = new();
	private readonly HashSet<long> _appliedActionIds = new();
	private readonly CardPlayStateService _cardPlayStateService = new();
	private readonly CaptureStateService _captureStateService = new();
	private readonly TurnAdvancementService _turnAdvancementService = new();
	private MatchRuleProfile _profile;
	private MatchReadModel _readModel = EmptyReadModel;
	private long _nextPresentationBarrierId = 1;

	public AuthorityRuleSession(IRuleRandomSource randomSource, MatchRuleProfile profile)
	{
		_randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
		_profile = profile ?? throw new ArgumentNullException(nameof(profile));
	}

	public MatchReadModel ReadModel => _readModel;

	public MatchCapabilitySet Capabilities => _profile.Capabilities;

	public CaptureStatePlan CreateCapturePlan(
		int captureStartIndex,
		IReadOnlyCollection<int>? extraTableCardIndexes = null)
	{
		HashSet<int>? extraIndexes = extraTableCardIndexes == null
			? null
			: new HashSet<int>(extraTableCardIndexes);
		return _captureStateService.CreatePlan(_state, captureStartIndex, extraIndexes);
	}

	public RuleCommandResult Submit(MatchCommand command)
	{
		ArgumentNullException.ThrowIfNull(command);

		MatchReadModel before = ReadModel;
		RuleRejection? commonRejection = ValidateCommonCommand(command);
		if (commonRejection.HasValue)
		{
			return RuleCommandResult.Rejected(commonRejection.Value, before);
		}

		var intents = ImmutableArray.CreateBuilder<PresentationIntent>();
		foreach (IRuleModule module in _profile.Modules)
		{
			RuleModuleDecision decision = module.Evaluate(command, before);
			if (!decision.Allowed)
			{
				RuleRejection rejection = decision.Rejection.Code == RuleRejectionCode.None
					? new RuleRejection(RuleRejectionCode.ModuleRejected, module.Id)
					: decision.Rejection;
				return RuleCommandResult.Rejected(rejection, before);
			}

			if (!decision.PresentationIntents.IsDefaultOrEmpty)
			{
				intents.AddRange(decision.PresentationIntents);
			}
		}

		RuleCommandResult result = command switch
		{
			InitializeMatchCommand initialize => ApplyInitialize(initialize, intents),
			RestartMatchCommand restart => ApplyRestart(restart, intents),
			InitializeScriptedMatchCommand scripted => ApplyScriptedInitialize(scripted, intents),
			PlayCardFromHandCommand playCard => ApplyPlayCardFromHand(playCard, intents),
			CaptureCardsCommand capture => ApplyCaptureCards(capture, intents),
			ApplyScoreMutationsCommand score => ApplyScoreMutations(score, intents),
			DrawCardsCommand draw => ApplyDrawCards(draw, intents),
			ReturnCardsToHandCommand returnCards => ApplyReturnCardsToHand(returnCards, intents),
			PlayItemFromHandCommand playItem => ApplyPlayItemFromHand(playItem, intents),
			DiscardHandCardCommand discard => ApplyDiscardHandCard(discard, intents),
			GrantItemToHandCommand grantItem => ApplyGrantItemToHand(grantItem, intents),
			ReplaceItemInHandCommand replaceItem => ApplyReplaceItemInHand(replaceItem, intents),
			AttachItemToHandCardCommand attachItem => ApplyAttachItemToHandCard(attachItem, intents),
			ChooseRandomHandDisplayIndexCommand chooseHandCard => ApplyChooseRandomHandDisplayIndex(chooseHandCard, intents),
			ChooseRandomHandCardIndexCommand chooseBaseCard => ApplyChooseRandomHandCardIndex(chooseBaseCard, intents),
			ChooseRandomTargetSeatCommand chooseTargetSeat => ApplyChooseRandomTargetSeat(chooseTargetSeat, intents),
			ChooseWeightedOptionCommand chooseWeightedOption => ApplyChooseWeightedOption(chooseWeightedOption, intents),
			ChooseRandomTableCardIndexCommand chooseTableCard => ApplyChooseRandomTableCardIndex(chooseTableCard, intents),
			ChooseRandomIndexCommand chooseRandomIndex => ApplyChooseRandomIndex(chooseRandomIndex, intents),
			ChooseReservoirHandDisplayIndexCommand chooseReservoirHandIndex => ApplyChooseReservoirHandDisplayIndex(chooseReservoirHandIndex, intents),
			TransferHandCardCommand transferHandCard => ApplyTransferHandCard(transferHandCard, intents),
			TransferDeckCardsCommand transferDeckCards => ApplyTransferDeckCards(transferDeckCards, intents),
			TransferTopDeckCardCommand transferTopDeckCard => ApplyTransferTopDeckCard(transferTopDeckCard, intents),
			ShuffleDeckCommand shuffleDeck => ApplyShuffleDeck(shuffleDeck, intents),
			PlaceTableItemCommand placeTableItem => ApplyPlaceTableItem(placeTableItem, intents),
			PlaceTableCardCommand placeTableCard => ApplyPlaceTableCard(placeTableCard, intents),
			ChooseTableCardsCommand chooseTableCards => ApplyChooseTableCards(chooseTableCards, intents),
			SetTableCardsFaceUpCommand setFaceUp => ApplySetTableCardsFaceUp(setFaceUp, intents),
			SetTableCardsHiddenCommand setHidden => ApplySetTableCardsHidden(setHidden, intents),
			SetTableCardsScoreSuppressedCommand setScoreSuppressed => ApplySetTableCardsScoreSuppressed(setScoreSuppressed, intents),
			RemoveTableCardAttachmentCommand removeTableAttachment => ApplyRemoveTableCardAttachment(removeTableAttachment, intents),
			RemoveTableCardRangeCommand removeTableRange => ApplyRemoveTableCardRange(removeTableRange, intents),
			MoveTableCardCommand moveTableCard => ApplyMoveTableCard(moveTableCard, intents),
			MoveTableCardToDeckCommand moveTableCardToDeck => ApplyMoveTableCardToDeck(moveTableCardToDeck, intents),
			PlanTableShuffleCommand planTableShuffle => ApplyPlanTableShuffle(planTableShuffle, intents),
			BeginMandatorySelectionCommand beginSelection => ApplyBeginMandatorySelection(beginSelection, intents),
			ResolveMandatorySelectionCommand resolveSelection => ApplyResolveMandatorySelection(resolveSelection, intents),
			ClearMandatorySelectionCommand clearSelection => ApplyClearMandatorySelection(clearSelection, intents),
			EnqueuePendingAttachmentRewardCommand enqueueAttachment => ApplyEnqueuePendingAttachmentReward(enqueueAttachment, intents),
			TakePendingAttachmentRewardCommand takeAttachment => ApplyTakePendingAttachmentReward(takeAttachment, intents),
			RemovePendingAttachmentRewardCommand removeAttachment => ApplyRemovePendingAttachmentReward(removeAttachment, intents),
			ReplacePendingAttachmentRewardCommand replaceAttachment => ApplyReplacePendingAttachmentReward(replaceAttachment, intents),
			SetPendingAttachmentRewardsCommand setAttachments => ApplySetPendingAttachmentRewards(setAttachments, intents),
			ReplaceMatchFlowStateCommand replaceFlow => ApplyReplaceMatchFlowState(replaceFlow, intents),
			BeginRealTurnCommand beginRealTurn => ApplyBeginRealTurn(beginRealTurn, intents),
			EndRealTurnCommand endRealTurn => ApplyEndRealTurn(endRealTurn, intents),
			RegisterRealTurnEndMarkerCommand registerRealTurnEndMarker => ApplyRegisterRealTurnEndMarker(registerRealTurnEndMarker, intents),
			GrantExtraActionCommand grantExtraAction => ApplyGrantExtraAction(grantExtraAction, intents),
			AcceptExtraActionCommand acceptExtraAction => ApplyAcceptExtraAction(acceptExtraAction, intents),
			FinishExtraActionExecutionCommand finishExtraAction => ApplyFinishExtraActionExecution(finishExtraAction, intents),
			CompleteExtraActionCommand completeExtraAction => ApplyCompleteExtraAction(completeExtraAction, intents),
			AbortExtraActionChainCommand abortExtraAction => ApplyAbortExtraActionChain(abortExtraAction, intents),
				AdvanceTurnCommand advanceTurn => ApplyAdvanceTurn(advanceTurn, intents),
				AdvanceTurnGenerationCommand advanceGeneration => ApplyAdvanceTurnGeneration(advanceGeneration, intents),
			EvaluateMatchOutcomeCommand evaluateOutcome => ApplyEvaluateMatchOutcome(evaluateOutcome, intents),
			EndMatchSessionCommand endSession => ApplyEndSession(endSession, intents),
			CompletePresentationBarrierCommand completion => ApplyPresentationBarrierCompletion(completion, intents),
			_ => RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.GetType().Name),
				before),
		};

		if (result.Accepted)
		{
			_appliedActionIds.Add(command.ActionId);
		}

		return result;
	}

	public MatchStateSnapshot CaptureSnapshot()
	{
		if (!_state.RuleSessionInitialized)
		{
			throw new InvalidOperationException("The match has not been initialized.");
		}

		return CreateSnapshot(
			_state,
			_randomSource.CaptureState(),
			_appliedActionIds.Order().ToImmutableArray());
	}

	public static AuthorityRuleSession Restore(
		MatchStateSnapshot snapshot,
		IRuleRandomSource randomSource,
		MatchRuleProfile profile)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentNullException.ThrowIfNull(randomSource);
		ArgumentNullException.ThrowIfNull(profile);
		SnapshotValidation.ValidateOrThrow(snapshot, nameof(snapshot));
		if (snapshot.RandomState.State == 0)
		{
			throw new ArgumentException(
				"The authority snapshot contains an invalid random state.",
				nameof(snapshot));
		}

		var session = new AuthorityRuleSession(randomSource, profile)
		{
			_nextPresentationBarrierId = Math.Max(1, snapshot.PendingPresentationBarrierId + 1),
		};
		RestoreState(session._state, snapshot);
		session._readModel = CreateReadModel(session._state) with
		{
			Adventure = session._adventureReadModel,
		};
		if (!snapshot.AppliedActionIds.IsDefaultOrEmpty)
		{
			session._appliedActionIds.UnionWith(snapshot.AppliedActionIds);
		}

		randomSource.RestoreState(snapshot.RandomState);
		return session;
	}

	private RuleRejection? ValidateCommonCommand(MatchCommand command)
	{
		if (command.ActionId <= 0)
		{
			return new RuleRejection(RuleRejectionCode.InvalidCommand, "ActionId must be positive.");
		}

		if (_appliedActionIds.Contains(command.ActionId))
		{
			return new RuleRejection(RuleRejectionCode.DuplicateAction, command.ActionId.ToString());
		}

		if (command is InitializeScriptedMatchCommand scripted)
		{
			if (!_state.RuleSessionInitialized)
			{
				return scripted.ExpectedRevision == 0
					&& scripted.TurnGeneration == 0
					&& scripted.LifecycleGeneration > 0
					? null
					: new RuleRejection(RuleRejectionCode.InvalidConfiguration, string.Empty);
			}

			if (scripted.ExpectedRevision != _state.RuleRevision)
			{
				return new RuleRejection(RuleRejectionCode.StaleRevision, scripted.ExpectedRevision.ToString());
			}

			if (scripted.LifecycleGeneration <= _state.RuleLifecycleGeneration)
			{
				return new RuleRejection(
					RuleRejectionCode.StaleLifecycleGeneration,
					scripted.LifecycleGeneration.ToString());
			}

			if (scripted.TurnGeneration != _state.TurnSequenceGeneration)
			{
				return new RuleRejection(RuleRejectionCode.StaleTurnGeneration, scripted.TurnGeneration.ToString());
			}

			if (scripted.TurnGeneration == int.MaxValue)
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Turn generation is exhausted.");
			}

			return null;
		}

		if (command is InitializeMatchCommand)
		{
			return !_state.RuleSessionInitialized
				? null
				: new RuleRejection(RuleRejectionCode.SessionAlreadyInitialized, string.Empty);
		}

		if (command is RestartMatchCommand restart)
		{
			if (!_state.RuleSessionInitialized)
			{
				return new RuleRejection(RuleRejectionCode.SessionNotInitialized, string.Empty);
			}

			if (restart.ExpectedRevision != _state.RuleRevision)
			{
				return new RuleRejection(RuleRejectionCode.StaleRevision, restart.ExpectedRevision.ToString());
			}

			if (restart.LifecycleGeneration <= _state.RuleLifecycleGeneration)
			{
				return new RuleRejection(
					RuleRejectionCode.StaleLifecycleGeneration,
					restart.LifecycleGeneration.ToString());
			}

			if (restart.TurnGeneration != _state.TurnSequenceGeneration)
			{
				return new RuleRejection(RuleRejectionCode.StaleTurnGeneration, restart.TurnGeneration.ToString());
			}

			if (restart.TurnGeneration == int.MaxValue)
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Turn generation is exhausted.");
			}

			return null;
		}

		if (!_state.RuleSessionInitialized)
		{
			return new RuleRejection(RuleRejectionCode.SessionNotInitialized, string.Empty);
		}

		if (_state.RuleSessionEnded
			|| _state.MatchOutcomeKind != global::MatchOutcomeKind.InProgress
				&& command is not EndMatchSessionCommand
				&& command is not MatchFlowCommand)
		{
			return new RuleRejection(RuleRejectionCode.SessionEnded, string.Empty);
		}

		if (command.ExpectedRevision != _state.RuleRevision)
		{
			return new RuleRejection(RuleRejectionCode.StaleRevision, command.ExpectedRevision.ToString());
		}

		if (command.LifecycleGeneration != _state.RuleLifecycleGeneration)
		{
			return new RuleRejection(RuleRejectionCode.StaleLifecycleGeneration, command.LifecycleGeneration.ToString());
		}

		if (command.TurnGeneration != _state.TurnSequenceGeneration)
		{
			return new RuleRejection(RuleRejectionCode.StaleTurnGeneration, command.TurnGeneration.ToString());
		}

		if (_state.RuleRevision == long.MaxValue)
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Rule revision is exhausted.");
		}

		if (_state.RulePendingPresentationBarrierId > 0
			&& command is not CompletePresentationBarrierCommand
			&& command is not AdvanceTurnGenerationCommand
			&& command is not MatchFlowCommand)
		{
			return new RuleRejection(
				RuleRejectionCode.PresentationBarrierPending,
				_state.RulePendingPresentationBarrierId.ToString());
		}

		if (_state.MandatorySelection.Kind != MandatorySelectionKind.None
			&& command is not ResolveMandatorySelectionCommand
			&& command is not ClearMandatorySelectionCommand
			&& command is not ChooseRandomHandDisplayIndexCommand
			&& command is not ChooseRandomHandCardIndexCommand
			&& command is not FinishExtraActionExecutionCommand
			&& command is not AbortExtraActionChainCommand
			&& command is not AdvanceTurnGenerationCommand
			&& command is not EndMatchSessionCommand
			&& command is not MatchFlowCommand)
		{
			return new RuleRejection(
				RuleRejectionCode.MandatorySelectionPending,
				_state.MandatorySelection.Kind.ToString());
		}

		return null;
	}

	private RuleCommandResult ApplyInitialize(
		InitializeMatchCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateInitialize(command);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		return ApplyMatchInitialization(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.DealEqualRanks,
			command.Deck,
			command.LifecycleGeneration,
			turnGeneration: 1,
			command.CreateOpeningPresentationBarrier,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits,
			intents);
	}

	private RuleCommandResult ApplyRestart(
		RestartMatchCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateInitializeConfiguration(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.Deck,
			command.LifecycleGeneration,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		return ApplyMatchInitialization(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.DealEqualRanks,
			command.Deck,
			command.LifecycleGeneration,
			command.TurnGeneration + 1,
			command.CreateOpeningPresentationBarrier,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits,
			intents);
	}

	private RuleCommandResult ApplyScriptedInitialize(
		InitializeScriptedMatchCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateScriptedInitialize(command);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ResetForInitialization(
			_state,
			command.PlayerCount,
			command.WinningScore,
			command.LifecycleGeneration,
			command.TurnGeneration + 1);
		_state.CurrentTurnPlayerIndex = command.CurrentTurnSeat;
		for (int seat = 0; seat < command.PlayerCount; seat++)
		{
			PlayerStateSnapshot player = command.Players[seat];
			_state.PlayerDecks[seat].AddRange(player.Deck.Select(FromLogicalCard));
			_state.PlayerHands[seat].AddRange(player.Hand.Select(FromLogicalCard));
			_state.PlayerItemHands[seat].AddRange(
				player.ItemHand.Select(item => new ItemCard(item.Id)));
			_state.PlayerScores[seat] = player.Score;
		}

		foreach (TableCardState tableCard in command.TableCards)
		{
			TableCard restored = TableCard.Restore(
				tableCard.RuntimeInstanceId,
				tableCard.Card.HasValue ? FromLogicalCard(tableCard.Card.Value) : null,
				tableCard.IsFaceUp,
				tableCard.ItemOverlay.HasValue ? new ItemCard(tableCard.ItemOverlay.Value.Id) : null,
				tableCard.ItemOwnerSeat,
				tableCard.CardOwnerSeat,
				tableCard.RemoveOnCapture);
			restored.IsHiddenDuringCapture = tableCard.IsHiddenDuringCapture;
			restored.IsBuriedInSand = tableCard.IsBuried;
			restored.IsSlimeCoated = tableCard.IsScoreSuppressed;
			_state.TableCards.Add(restored);
		}

		_state.RuleRevision = 1;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MatchInitialized,
			"match.scripted_initialized"));
		return Accepted(intents);
	}

	private static RuleRejection? ValidateScriptedInitialize(InitializeScriptedMatchCommand command)
	{
		if (command.LifecycleGeneration <= 0
			|| command.PlayerCount is < 2 or > 4
			|| command.CurrentTurnSeat < 0
			|| command.CurrentTurnSeat >= command.PlayerCount
			|| command.WinningScore <= 0
			|| command.Players.IsDefault
			|| command.Players.Length != command.PlayerCount
			|| command.TableCards.IsDefault)
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, string.Empty);
		}

		var runtimeIds = new HashSet<long>();
		for (int seat = 0; seat < command.Players.Length; seat++)
		{
			PlayerStateSnapshot player = command.Players[seat];
			if (player.Seat != seat
				|| player.Score < 0
				|| player.Deck.IsDefault
				|| player.Hand.IsDefault
				|| player.ItemHand.IsDefault
				|| player.ItemHand.Any(item => string.IsNullOrWhiteSpace(item.Id)))
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Invalid scripted player state.");
			}

			foreach (LogicalCardState card in player.Deck.Concat(player.Hand))
			{
				if (!IsValidLogicalCard(card) || !runtimeIds.Add(card.RuntimeInstanceId))
				{
					return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Invalid scripted card state.");
				}
			}
		}

		var tableRuntimeIds = new HashSet<long>();
		foreach (TableCardState tableCard in command.TableCards)
		{
			if (tableCard.RuntimeInstanceId <= 0
				|| !tableRuntimeIds.Add(tableCard.RuntimeInstanceId)
				|| tableCard.Card.HasValue
					&& (!IsValidLogicalCard(tableCard.Card.Value)
						|| !runtimeIds.Add(tableCard.Card.Value.RuntimeInstanceId))
				|| tableCard.ItemOverlay.HasValue
					&& string.IsNullOrWhiteSpace(tableCard.ItemOverlay.Value.Id)
				|| !tableCard.Card.HasValue && !tableCard.ItemOverlay.HasValue)
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Invalid scripted table state.");
			}
		}

		return null;
	}

	private static bool IsValidLogicalCard(LogicalCardState card)
	{
		return card.RuntimeInstanceId > 0
			&& !string.IsNullOrWhiteSpace(card.Code)
			&& card.Suit != null
			&& (card.Suit.Length == 0 || !string.IsNullOrWhiteSpace(card.Suit))
			&& !string.IsNullOrWhiteSpace(card.Rank)
			&& !card.AttachedEffectIds.IsDefault;
	}

	private RuleCommandResult ApplyMatchInitialization(
		int playerCount,
		int winningScore,
		int startingHandSize,
		bool dealEqualRanks,
		ImmutableArray<LogicalCardState> logicalDeck,
		int lifecycleGeneration,
		int turnGeneration,
		bool createOpeningPresentationBarrier,
		ImmutableArray<int> startingScores,
		int maximumHandSize,
		ImmutableArray<int> additionalStartingDraws,
		ImmutableArray<int> seatCardSupplyLimits,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		ResetForInitialization(
			_state,
			playerCount,
			winningScore,
			lifecycleGeneration,
			turnGeneration);
		List<PlayingCard> deck = logicalDeck.Select(FromLogicalCard).ToList();
		if (dealEqualRanks)
		{
			DealEqualRanks(_state, deck, _randomSource);
		}
		else
		{
			Shuffle(deck, _randomSource);
			Cut(deck, _randomSource);
			for (int cardIndex = 0; cardIndex < deck.Count; cardIndex++)
			{
				_state.PlayerDecks[cardIndex % playerCount].Add(deck[cardIndex]);
			}
		}

		for (int seat = 0; seat < playerCount; seat++)
		{
			List<PlayingCard> playerDeck = _state.PlayerDecks[seat];
			List<PlayingCard> playerHand = _state.PlayerHands[seat];
			int drawCount = Math.Min(startingHandSize, playerDeck.Count);
			for (int index = 0; index < drawCount; index++)
			{
				playerHand.Add(playerDeck[0]);
				playerDeck.RemoveAt(0);
			}

			int additionalDrawCount = additionalStartingDraws.IsDefault
				? 0
				: additionalStartingDraws[seat];
			for (int index = 0;
				index < additionalDrawCount
					&& playerDeck.Count > 0
					&& playerHand.Count < maximumHandSize;
				index++)
			{
				playerHand.Add(playerDeck[0]);
				playerDeck.RemoveAt(0);
			}

			int cardSupplyLimit = seatCardSupplyLimits.IsDefault
				? -1
				: seatCardSupplyLimits[seat];
			if (cardSupplyLimit >= 0)
			{
				int retainedHandCount = Math.Min(cardSupplyLimit, playerHand.Count);
				if (playerHand.Count > retainedHandCount)
				{
					playerHand.RemoveRange(retainedHandCount, playerHand.Count - retainedHandCount);
				}

				int retainedDeckCount = Math.Min(
					Math.Max(0, cardSupplyLimit - retainedHandCount),
					playerDeck.Count);
				if (playerDeck.Count > retainedDeckCount)
				{
					playerDeck.RemoveRange(retainedDeckCount, playerDeck.Count - retainedDeckCount);
				}
			}

			_state.PlayerScores[seat] = startingScores.IsDefault ? 0 : startingScores[seat];
		}

		_state.RuleRevision = 1;
		if (createOpeningPresentationBarrier)
		{
			_state.RulePendingPresentationBarrierId = _nextPresentationBarrierId++;
		}

		intents.Add(new PresentationIntent(
			PresentationIntentKind.MatchInitialized,
			"match.initialized",
			BarrierId: _state.RulePendingPresentationBarrierId));
		return Accepted(intents);
	}

	private static RuleRejection? ValidateInitialize(InitializeMatchCommand command)
	{
		return ValidateInitializeConfiguration(
			command.PlayerCount,
			command.WinningScore,
			command.StartingHandSize,
			command.Deck,
			command.LifecycleGeneration,
			command.StartingScores,
			command.MaximumHandSize,
			command.AdditionalStartingDraws,
			command.SeatCardSupplyLimits);
	}

	private static RuleRejection? ValidateInitializeConfiguration(
		int playerCount,
		int winningScore,
		int startingHandSize,
		ImmutableArray<LogicalCardState> deck,
		int lifecycleGeneration,
		ImmutableArray<int> startingScores,
		int maximumHandSize,
		ImmutableArray<int> additionalStartingDraws,
		ImmutableArray<int> seatCardSupplyLimits)
	{
		if (lifecycleGeneration <= 0
			|| playerCount is < 2 or > 4
			|| winningScore <= 0
			|| startingHandSize < 0
			|| maximumHandSize < startingHandSize
			|| deck.IsDefault
			|| deck.Length < playerCount * startingHandSize
			|| !startingScores.IsDefault && startingScores.Length != playerCount
			|| !startingScores.IsDefault && startingScores.Any(score => score < 0)
			|| !additionalStartingDraws.IsDefault && additionalStartingDraws.Length != playerCount
			|| !additionalStartingDraws.IsDefault && additionalStartingDraws.Any(drawCount => drawCount < 0)
			|| !additionalStartingDraws.IsDefault
				&& additionalStartingDraws.Any(drawCount => drawCount > maximumHandSize - startingHandSize)
			|| !seatCardSupplyLimits.IsDefault && seatCardSupplyLimits.Length != playerCount
			|| !seatCardSupplyLimits.IsDefault && seatCardSupplyLimits.Any(limit => limit < -1))
		{
			return new RuleRejection(RuleRejectionCode.InvalidConfiguration, string.Empty);
		}

		var runtimeIds = new HashSet<long>();
		foreach (LogicalCardState card in deck)
		{
			if (card.RuntimeInstanceId <= 0
				|| string.IsNullOrWhiteSpace(card.Code)
				|| string.IsNullOrWhiteSpace(card.Suit)
				|| string.IsNullOrWhiteSpace(card.Rank)
				|| card.AttachedEffectIds.IsDefault
				|| !runtimeIds.Add(card.RuntimeInstanceId))
			{
				return new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Deck contains an invalid card.");
			}
		}

		return null;
	}

	private RuleCommandResult ApplyPlayCardFromHand(
		PlayCardFromHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePlayCardFromHand(command);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		PlayingCard playedCard = _state.PlayerHands[command.Seat][command.HandIndex];
		var tableCards = new CardPlayTableCard[_state.TableCards.Count];
		for (int index = 0; index < tableCards.Length; index++)
		{
			TableCard tableCard = _state.TableCards[index];
			tableCards[index] = new CardPlayTableCard(
				tableCard.Card?.Rank,
				tableCard.IsFaceUp,
				tableCard.IsBuriedInSand,
				tableCard.Card?.Suit);
		}

		IReadOnlyList<string>? alternateRanks = command.AlternateMatchRanks.IsDefaultOrEmpty
			? null
			: command.AlternateMatchRanks;
		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard(
				command.PrimaryMatchRank,
				command.CollectEntireTable,
				playedCard.Suit,
				command.CollectBySuit,
				command.SuitMatchIsFallbackOnly),
			alternateRanks,
			command.InsertAtTableStart,
			command.TableInsertIndexOverride,
			command.PlayedCardFacesUp);
		CardPlayStartResult start = _cardPlayStateService.TryStartPlayFromHand(
			_state,
			command.Seat,
			command.HandIndex,
			playedCard,
			plan);
		if (!start.Played)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.HandIndex.ToString()),
				ReadModel);
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.CardPlayed,
			"card.played",
			playedCard.InstanceId,
			command.Seat,
			command.HandIndex,
			start.PlayedTableCardIndex,
			start.CaptureStartIndex));
		if (start.MatchedTableCardFlipped)
		{
			intents.Add(new PresentationIntent(
				PresentationIntentKind.SemanticEffect,
				"card.match_reveal",
				Seat: command.Seat,
				TargetIndex: plan.MatchingTableCardIndex));
		}

		return Accepted(
			intents,
			new RuleMutationSummary(
				playedCard.InstanceId,
				command.Seat,
				command.HandIndex,
				start.PlayedTableCardIndex,
				start.CaptureStartIndex,
				0,
				0,
				ImmutableArray<RuleScoreChange>.Empty,
				start.CaptureStartIndex >= 0,
				false));
	}

	private RuleRejection? ValidatePlayCardFromHand(PlayCardFromHandCommand command)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.Seat);
		if (seatRejection.HasValue)
		{
			return seatRejection;
		}

		if (command.HandIndex < 0 || command.HandIndex >= _state.PlayerHands[command.Seat].Count)
		{
			return new RuleRejection(RuleRejectionCode.InvalidIndex, command.HandIndex.ToString());
		}

		if (string.IsNullOrWhiteSpace(command.PrimaryMatchRank)
			|| command.TableInsertIndexOverride < -1
			|| command.TableInsertIndexOverride > _state.TableCards.Count)
		{
			return new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty);
		}

		if (!command.AlternateMatchRanks.IsDefault)
		{
			var seenRanks = new HashSet<string>(StringComparer.Ordinal);
			foreach (string rank in command.AlternateMatchRanks)
			{
				if (string.IsNullOrWhiteSpace(rank) || !seenRanks.Add(rank))
				{
					return new RuleRejection(
						RuleRejectionCode.InvalidCommand,
						"Alternate match ranks must be non-empty and distinct.");
				}
			}
		}

		return null;
	}

	private RuleCommandResult ApplyCaptureCards(
		CaptureCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.Seat);
		if (seatRejection.HasValue)
		{
			return RuleCommandResult.Rejected(seatRejection.Value, ReadModel);
		}

		if (command.CaptureStartIndex < 0 || command.CaptureStartIndex >= _state.TableCards.Count)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.CaptureStartIndex.ToString()),
				ReadModel);
		}

		RuleRejection? extraIndexRejection = TryBuildCaptureIndexSet(
			command.ExtraTableCardIndexes,
			command.CaptureStartIndex,
			requireBeforeCaptureStart: true,
			out HashSet<int>? extraIndexes);
		if (extraIndexRejection.HasValue)
		{
			return RuleCommandResult.Rejected(extraIndexRejection.Value, ReadModel);
		}

		CaptureStatePlan plan = _captureStateService.CreatePlan(
			_state,
			command.CaptureStartIndex,
			extraIndexes);
		if (!plan.IsValid)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.CaptureStartIndex.ToString()),
				ReadModel);
		}

		RuleRejection? preservedIndexRejection = TryBuildPreservedCaptureIndexSet(
			command.PreservedTableCardIndexes,
			plan,
			out HashSet<int>? preservedIndexes);
		if (preservedIndexRejection.HasValue)
		{
			return RuleCommandResult.Rejected(preservedIndexRejection.Value, ReadModel);
		}

		if (command.RedirectedTableCardIndex < -1
			|| command.RedirectedTableCardIndex >= 0
				&& (!IsCapturedIndex(plan, command.RedirectedTableCardIndex)
					|| command.RedirectedSeat < 0
					|| command.RedirectedSeat >= _state.PlayerCount)
			|| command.RedirectedTableCardIndex < 0 && command.RedirectedSeat != -1)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.RedirectedTableCardIndex.ToString()),
				ReadModel);
		}

		RuleRejection? scoreRejection = TryBuildScoreChanges(
			command.ScoreMutations,
			allowEmpty: true,
			out ImmutableArray<RuleScoreChange> scoreChanges);
		if (scoreRejection.HasValue)
		{
			return RuleCommandResult.Rejected(scoreRejection.Value, ReadModel);
		}

		ApplyScoreChanges(scoreChanges);
		CaptureStateResult captureResult = _captureStateService.ApplyCapture(
			_state,
			plan,
			command.Seat,
			command.PreserveCardEffectsWhenEnteringDeck,
			preservedIndexes,
			command.RedirectedTableCardIndex,
			command.RedirectedSeat);
		if (command.RestartQuestionRefreshTracking)
		{
			QuestionCardRefreshState refreshState =
				_turnAdvancementService.StartQuestionCardRefreshTracking(command.Seat);
			_state.CompletedTurnsSinceQuestionCardRefresh =
				refreshState.CompletedTurnsSinceQuestionCardRefresh;
			_state.QuestionCardRefreshAnchorPlayerIndex =
				refreshState.QuestionCardRefreshAnchorPlayerIndex;
		}
		_state.RuleRevision++;

		intents.Add(new PresentationIntent(
			PresentationIntentKind.CardsCaptured,
			"cards.captured",
			Seat: command.Seat,
			SourceIndex: command.CaptureStartIndex,
			Value: captureResult.CollectedCardCount));
		AddScoreChangedIntents(intents, scoreChanges);
		return Accepted(
			intents,
			new RuleMutationSummary(
				0,
				command.Seat,
				command.CaptureStartIndex,
				-1,
				command.CaptureStartIndex,
				captureResult.CollectedCardCount,
				captureResult.RemovedTableCardCount,
				scoreChanges,
				true,
				false));
	}

	private RuleCommandResult ApplyScoreMutations(
		ApplyScoreMutationsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = TryBuildScoreChanges(
			command.Mutations,
			allowEmpty: false,
			out ImmutableArray<RuleScoreChange> scoreChanges);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ApplyScoreChanges(scoreChanges);
		_state.RuleRevision++;
		AddScoreChangedIntents(intents, scoreChanges);
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { ScoreChanges = scoreChanges });
	}

	private RuleCommandResult ApplyDrawCards(
		DrawCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (command.Seat < 0 || command.Seat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.Seat.ToString()),
				ReadModel);
		}

		if (command.Count <= 0 || command.MaximumHandSize < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.Count.ToString()),
				ReadModel);
		}

		List<PlayingCard> deck = _state.PlayerDecks[command.Seat];
		List<PlayingCard> hand = _state.PlayerHands[command.Seat];
		int totalHandCount = hand.Count + _state.PlayerItemHands[command.Seat].Count;
		int drawCount = Math.Min(
			command.Count,
			Math.Min(deck.Count, Math.Max(0, command.MaximumHandSize - totalHandCount)));
		if (drawCount <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No card can be drawn."),
				ReadModel);
		}

		long firstCardId = 0;
		int firstHandIndex = hand.Count;
		for (int index = 0; index < drawCount; index++)
		{
			PlayingCard card = deck[0];
			deck.RemoveAt(0);
			hand.Add(card);
			firstCardId = firstCardId == 0 ? card.InstanceId : firstCardId;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.CardDrawn,
				"card.drawn",
				card.InstanceId,
				command.Seat,
				SourceIndex: 0,
				TargetIndex: hand.Count - 1));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			new RuleMutationSummary(
				firstCardId,
				command.Seat,
				0,
				firstHandIndex,
				-1,
				drawCount,
				0,
				ImmutableArray<RuleScoreChange>.Empty,
				false,
				false));
	}

	private RuleCommandResult ApplyReturnCardsToHand(
		ReturnCardsToHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.Seat);
		if (seatRejection.HasValue)
		{
			return RuleCommandResult.Rejected(seatRejection.Value, ReadModel);
		}

		if (command.CardRuntimeInstanceIds.IsDefaultOrEmpty
			|| command.MaximumHandSize < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty),
				ReadModel);
		}

		List<PlayingCard> deck = _state.PlayerDecks[command.Seat];
		List<PlayingCard> hand = _state.PlayerHands[command.Seat];
		int totalHandCount = hand.Count + _state.PlayerItemHands[command.Seat].Count;
		if (command.CardRuntimeInstanceIds.Length > command.MaximumHandSize - totalHandCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Returned cards exceed hand capacity."),
				ReadModel);
		}

		var seenIds = new HashSet<long>();
		var cards = new PlayingCard[command.CardRuntimeInstanceIds.Length];
		for (int requestedIndex = 0;
			requestedIndex < command.CardRuntimeInstanceIds.Length;
			requestedIndex++)
		{
			long runtimeInstanceId = command.CardRuntimeInstanceIds[requestedIndex];
			if (runtimeInstanceId <= 0 || !seenIds.Add(runtimeInstanceId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, runtimeInstanceId.ToString()),
					ReadModel);
			}

			int deckIndex = deck.FindIndex(card => card.InstanceId == runtimeInstanceId);
			if (deckIndex < 0)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, runtimeInstanceId.ToString()),
					ReadModel);
			}

			cards[requestedIndex] = deck[deckIndex];
		}

		int firstHandIndex = hand.Count;
		for (int requestedIndex = 0; requestedIndex < cards.Length; requestedIndex++)
		{
			PlayingCard card = cards[requestedIndex];
			int deckIndex = deck.FindIndex(candidate => candidate.InstanceId == card.InstanceId);
			deck.RemoveAt(deckIndex);
			hand.Add(card);
			intents.Add(new PresentationIntent(
				PresentationIntentKind.CardReturnedToHand,
				"card.returned_to_hand",
				card.InstanceId,
				command.Seat,
				deckIndex,
				hand.Count - 1));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			new RuleMutationSummary(
				cards[0].InstanceId,
				command.Seat,
				-1,
				firstHandIndex,
				-1,
				cards.Length,
				0,
				ImmutableArray<RuleScoreChange>.Empty,
				false,
				false));
	}

	private RuleCommandResult ApplyPlayItemFromHand(
		PlayItemFromHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		List<ItemCard> itemHand = _state.PlayerItemHands[command.Seat];
		if (command.ItemHandIndex < 0
			|| command.ItemHandIndex >= itemHand.Count
			|| string.IsNullOrWhiteSpace(command.ExpectedItemId)
			|| !string.Equals(
				itemHand[command.ItemHandIndex].Id,
				command.ExpectedItemId,
				StringComparison.Ordinal)
			|| !Enum.IsDefined(command.Destination))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.ItemHandIndex.ToString()),
				ReadModel);
		}

		if (command.Destination == RuleItemPlayDestination.Consume
			&& command.TableIndex != -1
			|| command.Destination == RuleItemPlayDestination.Table
				&& (command.TableIndex < 0 || command.TableIndex > _state.TableCards.Count))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.TableIndex.ToString()),
				ReadModel);
		}

		ItemCard item = itemHand[command.ItemHandIndex];
		itemHand.RemoveAt(command.ItemHandIndex);
		int targetIndex = -1;
		if (command.Destination == RuleItemPlayDestination.Table)
		{
			var tableItem = new TableCard(
				null,
				command.TableItemFacesUp,
				item,
				command.Seat);
			_state.TableCards.Insert(command.TableIndex, tableItem);
			targetIndex = command.TableIndex;
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemPlayed,
			command.Destination == RuleItemPlayDestination.Table
				? "item.played_to_table"
				: "item.played",
			Seat: command.Seat,
			SourceIndex: command.ItemHandIndex,
			TargetIndex: targetIndex,
			LogicalItemId: item.Id));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = command.ItemHandIndex,
				TargetIndex = targetIndex,
				LogicalItemId = item.Id,
			});
	}

	private RuleCommandResult ApplyDiscardHandCard(
		DiscardHandCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? displayRejection = ValidateHandDisplayCard(
			command.Seat,
			command.DisplayIndex,
			command.ExpectedCardRuntimeInstanceId,
			command.ExpectedItemId,
			out bool isBaseCard,
			out int zoneIndex);
		if (displayRejection.HasValue)
		{
			return RuleCommandResult.Rejected(displayRejection.Value, ReadModel);
		}

		long cardId = 0;
		string itemId = string.Empty;
		if (isBaseCard)
		{
			cardId = _state.PlayerHands[command.Seat][zoneIndex].InstanceId;
			_state.PlayerHands[command.Seat].RemoveAt(zoneIndex);
		}
		else
		{
			itemId = _state.PlayerItemHands[command.Seat][zoneIndex].Id;
			_state.PlayerItemHands[command.Seat].RemoveAt(zoneIndex);
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.HandCardDiscarded,
			"hand.card_discarded",
			cardId,
			command.Seat,
			command.DisplayIndex,
			LogicalItemId: itemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = cardId,
				SourceSeat = command.Seat,
				SourceIndex = command.DisplayIndex,
				LogicalItemId = itemId,
			});
	}

	private RuleCommandResult ApplyGrantItemToHand(
		GrantItemToHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (string.IsNullOrWhiteSpace(command.ItemId) || command.MaximumHandSize <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.ItemId),
				ReadModel);
		}

		bool hasDiscard = command.DiscardDisplayIndex >= 0;
		bool discardIsBaseCard = false;
		int discardZoneIndex = -1;
		if (hasDiscard)
		{
			RuleRejection? discardRejection = ValidateHandDisplayCard(
				command.Seat,
				command.DiscardDisplayIndex,
				command.ExpectedDiscardCardRuntimeInstanceId,
				command.ExpectedDiscardItemId,
				out discardIsBaseCard,
				out discardZoneIndex);
			if (discardRejection.HasValue)
			{
				return RuleCommandResult.Rejected(discardRejection.Value, ReadModel);
			}
		}
		else if (command.ExpectedDiscardCardRuntimeInstanceId != 0
			|| !string.IsNullOrEmpty(command.ExpectedDiscardItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty),
				ReadModel);
		}

		int totalHandCount = _state.PlayerHands[command.Seat].Count
			+ _state.PlayerItemHands[command.Seat].Count;
		if (totalHandCount - (hasDiscard ? 1 : 0) >= command.MaximumHandSize)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Item reward exceeds hand capacity."),
				ReadModel);
		}

		if (hasDiscard)
		{
			long discardedCardId = 0;
			string discardedItemId = string.Empty;
			if (discardIsBaseCard)
			{
				discardedCardId = _state.PlayerHands[command.Seat][discardZoneIndex].InstanceId;
				_state.PlayerHands[command.Seat].RemoveAt(discardZoneIndex);
			}
			else
			{
				discardedItemId = _state.PlayerItemHands[command.Seat][discardZoneIndex].Id;
				_state.PlayerItemHands[command.Seat].RemoveAt(discardZoneIndex);
			}

			intents.Add(new PresentationIntent(
				PresentationIntentKind.HandCardDiscarded,
				"hand.card_discarded",
				discardedCardId,
				command.Seat,
				command.DiscardDisplayIndex,
				LogicalItemId: discardedItemId));
		}

		int itemHandIndex = _state.PlayerItemHands[command.Seat].Count;
		_state.PlayerItemHands[command.Seat].Add(new ItemCard(command.ItemId));
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemGranted,
			"item.granted",
			Seat: command.Seat,
			SourceIndex: command.DiscardDisplayIndex,
			TargetIndex: itemHandIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = command.DiscardDisplayIndex,
				TargetIndex = itemHandIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyReplaceItemInHand(
		ReplaceItemInHandCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		List<ItemCard> itemHand = _state.PlayerItemHands[command.Seat];
		if (command.ItemHandIndex < 0
			|| command.ItemHandIndex >= itemHand.Count
			|| string.IsNullOrWhiteSpace(command.ExpectedItemId)
			|| string.IsNullOrWhiteSpace(command.ReplacementItemId)
			|| !string.Equals(
				itemHand[command.ItemHandIndex].Id,
				command.ExpectedItemId,
				StringComparison.Ordinal))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.ItemHandIndex.ToString()),
				ReadModel);
		}

		itemHand[command.ItemHandIndex] = new ItemCard(command.ReplacementItemId);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemGranted,
			"item.replaced",
			Seat: command.Seat,
			SourceIndex: command.ItemHandIndex,
			TargetIndex: command.ItemHandIndex,
			LogicalItemId: command.ReplacementItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = command.ItemHandIndex,
				TargetIndex = command.ItemHandIndex,
				LogicalItemId = command.ReplacementItemId,
			});
	}

	private RuleCommandResult ApplyAttachItemToHandCard(
		AttachItemToHandCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (string.IsNullOrWhiteSpace(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, string.Empty),
				ReadModel);
		}

		List<PlayingCard> hand = _state.PlayerHands[command.Seat];
		int handIndex = command.HandIndex;
		if (handIndex < 0)
		{
			if (command.RandomCandidateHandIndexes.IsDefaultOrEmpty)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.NoLegalAction, "No attachment target was supplied."),
					ReadModel);
			}

			var seenIndexes = new HashSet<int>();
			foreach (int candidateIndex in command.RandomCandidateHandIndexes)
			{
				if (candidateIndex < 0
					|| candidateIndex >= hand.Count
					|| !seenIndexes.Add(candidateIndex)
					|| hand[candidateIndex].HasAttachedItemEffect(command.ItemId))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidIndex, candidateIndex.ToString()),
						ReadModel);
				}
			}

			int randomOrdinal = _randomSource.NextIntInclusive(0, int.MaxValue);
			handIndex = command.RandomCandidateHandIndexes[
				(randomOrdinal & int.MaxValue) % command.RandomCandidateHandIndexes.Length];
		}
		else if (!command.RandomCandidateHandIndexes.IsDefaultOrEmpty
			|| handIndex >= hand.Count
			|| hand[handIndex].HasAttachedItemEffect(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, handIndex.ToString()),
				ReadModel);
		}

		PlayingCard card = hand[handIndex];
		hand[handIndex] = card.WithAttachedItemEffect(command.ItemId);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.ItemAttached,
			"item.attached",
			card.InstanceId,
			command.Seat,
			TargetIndex: handIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = card.InstanceId,
				SourceSeat = command.Seat,
				TargetIndex = handIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyChooseRandomHandDisplayIndex(
		ChooseRandomHandDisplayIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.HandSeat < 0 || command.HandSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.HandSeat.ToString()),
				ReadModel);
		}

		int totalHandCount = _state.PlayerHands[command.HandSeat].Count
			+ _state.PlayerItemHands[command.HandSeat].Count;
		if (totalHandCount <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "The hand is empty."),
				ReadModel);
		}

		var excludedIndexes = new HashSet<int>();
		if (!command.ExcludedDisplayIndexes.IsDefaultOrEmpty)
		{
			foreach (int excludedIndex in command.ExcludedDisplayIndexes)
			{
				if (excludedIndex < 0
					|| excludedIndex >= totalHandCount
					|| !excludedIndexes.Add(excludedIndex))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidIndex, excludedIndex.ToString()),
						ReadModel);
				}
			}
		}

		if (excludedIndexes.Count == totalHandCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Every hand card is excluded."),
				ReadModel);
		}

		int selectedIndex = _randomSource.NextIntInclusive(0, totalHandCount - 1);
		if (excludedIndexes.Contains(selectedIndex))
		{
			for (int displayIndex = 0; displayIndex < totalHandCount; displayIndex++)
			{
				if (!excludedIndexes.Contains(displayIndex))
				{
					selectedIndex = displayIndex;
					break;
				}
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.HandSeat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseRandomHandCardIndex(
		ChooseRandomHandCardIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.Seat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.CandidateHandIndexes.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No hand-card candidate was supplied."),
				ReadModel);
		}

		var seenIndexes = new HashSet<int>();
		int handCount = _state.PlayerHands[command.Seat].Count;
		foreach (int candidateIndex in command.CandidateHandIndexes)
		{
			if (candidateIndex < 0
				|| candidateIndex >= handCount
				|| !seenIndexes.Add(candidateIndex))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, candidateIndex.ToString()),
					ReadModel);
			}
		}

		int randomOrdinal = _randomSource.NextIntInclusive(0, int.MaxValue);
		int selectedIndex = command.CandidateHandIndexes[
			(randomOrdinal & int.MaxValue) % command.CandidateHandIndexes.Length];
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseRandomTargetSeat(
		ChooseRandomTargetSeatCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.CandidateSeats.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No target seat was supplied."),
				ReadModel);
		}

		var seenSeats = new HashSet<int>();
		foreach (int candidateSeat in command.CandidateSeats)
		{
			if (candidateSeat < 0
				|| candidateSeat >= _state.PlayerCount
				|| candidateSeat == command.ActingSeat
				|| !seenSeats.Add(candidateSeat))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidSeat, candidateSeat.ToString()),
					ReadModel);
			}
		}

		int selectedSeat = command.CandidateSeats[
			_randomSource.NextIntInclusive(0, command.CandidateSeats.Length - 1)];
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = selectedSeat,
			});
	}

	private RuleCommandResult ApplyChooseWeightedOption(
		ChooseWeightedOptionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.Options.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No weighted option was supplied."),
				ReadModel);
		}

		var seenIds = new HashSet<string>(StringComparer.Ordinal);
		int maximumTotalWeight = 0;
		foreach (RuleWeightedOption option in command.Options)
		{
			int weight = Math.Max(0, option.Weight);
			if (string.IsNullOrWhiteSpace(option.Id) || !seenIds.Add(option.Id))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, option.Id ?? string.Empty),
					ReadModel);
			}
			if (weight > int.MaxValue - maximumTotalWeight)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Weighted option total exceeds Int32."),
					ReadModel);
			}
			maximumTotalWeight += weight;
		}
		if (maximumTotalWeight == 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Every weighted option has zero weight."),
				ReadModel);
		}

		string selectedId = string.Empty;
		int totalWeight = 0;
		foreach (RuleWeightedOption option in command.Options)
		{
			int weight = Math.Max(0, option.Weight);
			if (weight == 0)
			{
				continue;
			}
			totalWeight += weight;
			if (_randomSource.NextIntInclusive(1, totalWeight) <= weight)
			{
				selectedId = option.Id;
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				LogicalItemId = selectedId,
			});
	}

	private RuleCommandResult ApplyChooseRandomTableCardIndex(
		ChooseRandomTableCardIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.Candidates.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No table-card candidate was supplied."),
				ReadModel);
		}

		var seenIndexes = new HashSet<int>();
		foreach (RuleTableCardReference candidate in command.Candidates)
		{
			if (candidate.Index < 0
				|| candidate.Index >= _state.TableCards.Count
				|| !seenIndexes.Add(candidate.Index)
				|| _state.TableCards[candidate.Index].InstanceId != candidate.RuntimeInstanceId)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, candidate.Index.ToString()),
					ReadModel);
			}
		}

		int selectedIndex = -1;
		int candidateCount = 0;
		foreach (RuleTableCardReference candidate in command.Candidates)
		{
			candidateCount++;
			if (_randomSource.NextIntInclusive(1, candidateCount) == 1)
			{
				selectedIndex = candidate.Index;
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseRandomIndex(
		ChooseRandomIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.MinimumInclusive < 0
			|| command.MaximumInclusive < command.MinimumInclusive)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, "Invalid random range."),
				ReadModel);
		}

		int selectedIndex = _randomSource.NextIntInclusive(
			command.MinimumInclusive,
			command.MaximumInclusive);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = selectedIndex,
			});
	}

	private RuleCommandResult ApplyChooseReservoirHandDisplayIndex(
		ChooseReservoirHandDisplayIndexCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.HandSeat < 0 || command.HandSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.HandSeat.ToString()),
				ReadModel);
		}
		if (command.CandidateDisplayIndexes.IsDefaultOrEmpty)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "No hand-display candidate was supplied."),
				ReadModel);
		}

		int totalHandCount = _state.PlayerHands[command.HandSeat].Count
			+ _state.PlayerItemHands[command.HandSeat].Count;
		var seenIndexes = new HashSet<int>();
		foreach (int candidateIndex in command.CandidateDisplayIndexes)
		{
			if (candidateIndex < 0
				|| candidateIndex >= totalHandCount
				|| !seenIndexes.Add(candidateIndex))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, candidateIndex.ToString()),
					ReadModel);
			}
		}

		int selectedIndex = -1;
		int candidateCount = 0;
		foreach (int candidateIndex in command.CandidateDisplayIndexes)
		{
			candidateCount++;
			if (_randomSource.NextIntInclusive(1, candidateCount) == 1)
			{
				selectedIndex = candidateIndex;
			}
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.HandSeat,
				SourceIndex = selectedIndex,
				CollectedCardCount = candidateCount,
			});
	}

	private RuleCommandResult ApplyTransferHandCard(
		TransferHandCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		bool expectsBaseCard = command.ExpectedCardRuntimeInstanceId > 0;
		bool expectsItemCard = !string.IsNullOrEmpty(command.ExpectedItemId);
		if (command.SourceSeat < 0
			|| command.SourceSeat >= _state.PlayerCount
			|| command.SourceSeat == command.ActingSeat
			|| command.MaximumHandSize <= 0
			|| expectsBaseCard == expectsItemCard)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceSeat.ToString()),
				ReadModel);
		}

		RuleRejection? sourceRejection = ValidateHandDisplayCard(
			command.SourceSeat,
			command.SourceDisplayIndex,
			command.ExpectedCardRuntimeInstanceId,
			command.ExpectedItemId,
			out bool isBaseCard,
			out int sourceZoneIndex);
		if (sourceRejection.HasValue)
		{
			return RuleCommandResult.Rejected(sourceRejection.Value, ReadModel);
		}

		int destinationHandCount = _state.PlayerHands[command.ActingSeat].Count
			+ _state.PlayerItemHands[command.ActingSeat].Count;
		if (destinationHandCount >= command.MaximumHandSize)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Destination hand is full."),
				ReadModel);
		}

		long cardId = 0;
		string itemId = string.Empty;
		int targetDisplayIndex;
		if (isBaseCard)
		{
			PlayingCard card = _state.PlayerHands[command.SourceSeat][sourceZoneIndex];
			cardId = card.InstanceId;
			_state.PlayerHands[command.SourceSeat].RemoveAt(sourceZoneIndex);
			targetDisplayIndex = _state.PlayerHands[command.ActingSeat].Count;
			_state.PlayerHands[command.ActingSeat].Add(card);
		}
		else
		{
			ItemCard item = _state.PlayerItemHands[command.SourceSeat][sourceZoneIndex];
			itemId = item.Id;
			_state.PlayerItemHands[command.SourceSeat].RemoveAt(sourceZoneIndex);
			targetDisplayIndex = _state.PlayerHands[command.ActingSeat].Count
				+ _state.PlayerItemHands[command.ActingSeat].Count;
			_state.PlayerItemHands[command.ActingSeat].Add(item);
		}

		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.HandCardTransferred,
			"hand.card_transferred",
			cardId,
			command.SourceSeat,
			command.SourceDisplayIndex,
			targetDisplayIndex,
			command.ActingSeat,
			LogicalItemId: itemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = cardId,
				SourceSeat = command.SourceSeat,
				SourceIndex = command.SourceDisplayIndex,
				TargetIndex = targetDisplayIndex,
				LogicalItemId = itemId,
			});
	}

	private RuleCommandResult ApplyTransferDeckCards(
		TransferDeckCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.SourceSeat < 0
			|| command.SourceSeat >= _state.PlayerCount
			|| command.SourceSeat == command.ActingSeat
			|| command.MaximumCount < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceSeat.ToString()),
				ReadModel);
		}

		List<PlayingCard> sourceDeck = _state.PlayerDecks[command.SourceSeat];
		List<PlayingCard> destinationDeck = _state.PlayerDecks[command.ActingSeat];
		int cardsToMove = Math.Min(sourceDeck.Count, command.MaximumCount);
		ImmutableArray<long> expectedCardIds = command.ExpectedSourceTopCardRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: command.ExpectedSourceTopCardRuntimeInstanceIds;
		if (expectedCardIds.Length != cardsToMove)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, expectedCardIds.Length.ToString()),
				ReadModel);
		}

		for (int index = 0; index < cardsToMove; index++)
		{
			if (expectedCardIds[index] <= 0
				|| sourceDeck[index].InstanceId != expectedCardIds[index])
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, index.ToString()),
					ReadModel);
			}
		}

		for (int index = 0; index < cardsToMove; index++)
		{
			PlayingCard card = sourceDeck[0];
			sourceDeck.RemoveAt(0);
			destinationDeck.Add(
				command.PreserveCardEffectsWhenEnteringDeck
					? card
					: card.WithoutItemEffects());
		}

		Shuffle(destinationDeck, _randomSource);
		Shuffle(sourceDeck, _randomSource);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.DeckCardsTransferred,
			"deck.cards_transferred",
			Seat: command.SourceSeat,
			TargetIndex: command.ActingSeat,
			Value: cardsToMove));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.SourceSeat,
				TargetIndex = command.ActingSeat,
				CollectedCardCount = cardsToMove,
			});
	}

	private RuleCommandResult ApplyTransferTopDeckCard(
		TransferTopDeckCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.SourceSeat < 0
			|| command.SourceSeat >= _state.PlayerCount
			|| command.SourceSeat == command.ActingSeat
			|| command.ExpectedRuntimeInstanceId <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceSeat.ToString()),
				ReadModel);
		}

		List<PlayingCard> sourceDeck = _state.PlayerDecks[command.SourceSeat];
		if (sourceDeck.Count == 0
			|| sourceDeck[0].InstanceId != command.ExpectedRuntimeInstanceId)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.ExpectedRuntimeInstanceId.ToString()),
				ReadModel);
		}

		PlayingCard card = sourceDeck[0];
		sourceDeck.RemoveAt(0);
		_state.PlayerDecks[command.ActingSeat].Add(
			command.PreserveCardEffectsWhenEnteringDeck
				? card
				: card.WithoutItemEffects());
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.DeckCardsTransferred,
			"deck.top_card_transferred",
			card.InstanceId,
			command.SourceSeat,
			TargetIndex: command.ActingSeat,
			Value: 1));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = card.InstanceId,
				SourceSeat = command.SourceSeat,
				TargetIndex = command.ActingSeat,
				CollectedCardCount = 1,
			});
	}

	private RuleCommandResult ApplyShuffleDeck(
		ShuffleDeckCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.DeckSeat < 0 || command.DeckSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.DeckSeat.ToString()),
				ReadModel);
		}

		List<PlayingCard> deck = _state.PlayerDecks[command.DeckSeat];
		Shuffle(deck, _randomSource);
		if (command.CutAfterShuffle)
		{
			Cut(deck, _randomSource);
		}
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.SemanticEffect,
			command.CutAfterShuffle ? "deck.shuffled_and_cut" : "deck.shuffled",
			Seat: command.DeckSeat));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.DeckSeat,
			});
	}

	private RuleCommandResult ApplyPlaceTableItem(
		PlaceTableItemCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (string.IsNullOrWhiteSpace(command.ItemId)
			|| command.TableIndex < 0
			|| command.TableIndex > _state.TableCards.Count
			|| command.ItemOwnerSeat < -1
			|| command.ItemOwnerSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		var tableCard = new TableCard(
			null,
			command.IsFaceUp,
			new ItemCard(command.ItemId),
			command.ItemOwnerSeat);
		_state.TableCards.Insert(command.TableIndex, tableCard);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableItemPlaced,
			"table.item_placed",
			tableCard.InstanceId,
			command.ActingSeat,
			TargetIndex: command.TableIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				TargetIndex = command.TableIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyPlaceTableCard(
		PlaceTableCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (!IsValidLogicalCard(command.Card)
			|| ContainsCardRuntimeInstanceId(command.Card.RuntimeInstanceId)
			|| command.TableIndex < 0
			|| command.TableIndex > _state.TableCards.Count
			|| command.CardOwnerSeat < -1
			|| command.CardOwnerSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		var tableCard = new TableCard(
			FromLogicalCard(command.Card),
			command.IsFaceUp,
			cardOwnerPlayerIndex: command.CardOwnerSeat,
			removeCardOnCapture: command.RemoveOnCapture);
		_state.TableCards.Insert(command.TableIndex, tableCard);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.CardPlayed,
			"table.card_placed",
			tableCard.InstanceId,
			command.ActingSeat,
			TargetIndex: command.TableIndex));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				TargetIndex = command.TableIndex,
			});
	}

	private RuleCommandResult ApplyChooseTableCards(
		ChooseTableCardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? candidateRejection = ValidateTableCardReferences(command.Candidates);
		if (candidateRejection.HasValue)
		{
			return RuleCommandResult.Rejected(candidateRejection.Value, ReadModel);
		}

		if (command.MaximumCount <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.MaximumCount.ToString()),
				ReadModel);
		}

		int targetLimit = Math.Min(command.MaximumCount, command.Candidates.Length);
		var selected = new List<RuleTableCardReference>(targetLimit);
		int candidateCount = 0;
		foreach (RuleTableCardReference candidate in command.Candidates)
		{
			candidateCount++;
			if (selected.Count < targetLimit)
			{
				selected.Add(candidate);
				continue;
			}

			int replacementIndex = _randomSource.NextIntInclusive(0, candidateCount - 1);
			if (replacementIndex < targetLimit)
			{
				selected[replacementIndex] = candidate;
			}
		}

		Shuffle(selected, _randomSource);
		_state.RuleRevision++;
		foreach (RuleTableCardReference target in selected)
		{
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardsSelected,
				"table.card_selected",
				target.RuntimeInstanceId,
				command.ActingSeat,
				TargetIndex: target.Index));
		}

		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = selected.Count,
			});
	}

	private RuleCommandResult ApplySetTableCardsFaceUp(
		SetTableCardsFaceUpCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			if (_state.TableCards[reference.Index].IsFaceUp != command.ExpectedFaceUp)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, reference.Index.ToString()),
					ReadModel);
			}
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			_state.TableCards[reference.Index].IsFaceUp = command.FaceUp;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardStateChanged,
				command.FaceUp ? "table.card_revealed" : "table.card_concealed",
				reference.RuntimeInstanceId,
				command.ActingSeat,
				TargetIndex: reference.Index));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = command.TableCards.Length,
			});
	}

	private RuleCommandResult ApplySetTableCardsHidden(
		SetTableCardsHiddenCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			if (_state.TableCards[reference.Index].IsHiddenDuringCapture != command.ExpectedHidden)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, reference.Index.ToString()),
					ReadModel);
			}
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			_state.TableCards[reference.Index].IsHiddenDuringCapture = command.Hidden;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardStateChanged,
				command.Hidden ? "table.card_hidden" : "table.card_shown",
				reference.RuntimeInstanceId,
				command.ActingSeat,
				TargetIndex: reference.Index));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = command.TableCards.Length,
			});
	}

	private RuleCommandResult ApplySetTableCardsScoreSuppressed(
		SetTableCardsScoreSuppressedCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			if (_state.TableCards[reference.Index].IsSlimeCoated
				!= command.ExpectedScoreSuppressed)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, reference.Index.ToString()),
					ReadModel);
			}
		}

		foreach (RuleTableCardReference reference in command.TableCards)
		{
			_state.TableCards[reference.Index].IsSlimeCoated = command.ScoreSuppressed;
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableCardStateChanged,
				command.ScoreSuppressed
					? "table.score_suppressed"
					: "table.score_suppression_cleared",
				_state.TableCards[reference.Index].InstanceId,
				command.ActingSeat,
				TargetIndex: reference.Index,
				Value: command.ScoreSuppressed ? 1 : 0));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = command.TableCards.Length,
			});
	}

	private RuleCommandResult ApplyRemoveTableCardAttachment(
		RemoveTableCardAttachmentCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.TableIndex < 0
			|| command.TableIndex >= _state.TableCards.Count
			|| command.ExpectedTableCardRuntimeInstanceId <= 0
			|| string.IsNullOrWhiteSpace(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		TableCard tableCard = _state.TableCards[command.TableIndex];
		if (tableCard.InstanceId != command.ExpectedTableCardRuntimeInstanceId
			|| !tableCard.Card.HasValue
			|| !tableCard.Card.Value.HasAttachedItemEffect(command.ItemId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		tableCard.ClearAttachedItemEffect(command.ItemId);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardStateChanged,
			"table.card_attachment_removed",
			tableCard.InstanceId,
			command.ActingSeat,
			TargetIndex: command.TableIndex,
			LogicalItemId: command.ItemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				TargetIndex = command.TableIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyRemoveTableCardRange(
		RemoveTableCardRangeCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ImmutableArray<long> expectedIds = command.ExpectedRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: command.ExpectedRuntimeInstanceIds;
		if (command.StartIndex < 0
			|| expectedIds.IsEmpty
			|| command.StartIndex > _state.TableCards.Count - expectedIds.Length)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.StartIndex.ToString()),
				ReadModel);
		}

		for (int offset = 0; offset < expectedIds.Length; offset++)
		{
			if (expectedIds[offset] <= 0
				|| _state.TableCards[command.StartIndex + offset].InstanceId != expectedIds[offset])
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, offset.ToString()),
					ReadModel);
			}
		}

		_state.TableCards.RemoveRange(command.StartIndex, expectedIds.Length);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardsRemoved,
			"table.cards_removed",
			Seat: command.ActingSeat,
			SourceIndex: command.StartIndex,
			Value: expectedIds.Length));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = command.StartIndex,
				RemovedTableCardCount = expectedIds.Length,
			});
	}

	private RuleCommandResult ApplyMoveTableCard(
		MoveTableCardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (command.SourceIndex < 0
			|| command.SourceIndex >= _state.TableCards.Count
			|| command.TargetIndex < 0
			|| command.TargetIndex >= _state.TableCards.Count
			|| command.ExpectedRuntimeInstanceId <= 0
			|| _state.TableCards[command.SourceIndex].InstanceId != command.ExpectedRuntimeInstanceId)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.SourceIndex.ToString()),
				ReadModel);
		}

		TableCard tableCard = _state.TableCards[command.SourceIndex];
		_state.TableCards.RemoveAt(command.SourceIndex);
		_state.TableCards.Insert(command.TargetIndex, tableCard);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardMoved,
			"table.card_moved",
			tableCard.InstanceId,
			command.ActingSeat,
			command.SourceIndex,
			command.TargetIndex));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				SourceIndex = command.SourceIndex,
				TargetIndex = command.TargetIndex,
			});
	}

	private RuleCommandResult ApplyMoveTableCardToDeck(
		MoveTableCardToDeckCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateActingSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (command.TableIndex < 0
			|| command.TableIndex >= _state.TableCards.Count
			|| command.ExpectedTableCardRuntimeInstanceId <= 0
			|| command.DeckSeat < 0
			|| command.DeckSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableIndex.ToString()),
				ReadModel);
		}

		TableCard tableCard = _state.TableCards[command.TableIndex];
		if (tableCard.InstanceId != command.ExpectedTableCardRuntimeInstanceId
			|| !tableCard.Card.HasValue)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.ExpectedTableCardRuntimeInstanceId.ToString()),
				ReadModel);
		}

		PlayingCard card = tableCard.Card.Value;
		_state.TableCards.RemoveAt(command.TableIndex);
		_state.PlayerDecks[command.DeckSeat].Add(
			command.PreserveCardEffectsWhenEnteringDeck
				? card
				: card.WithoutItemEffects());
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TableCardsRemoved,
			"table.card_moved_to_deck",
			tableCard.InstanceId,
			command.ActingSeat,
			SourceIndex: command.TableIndex,
			TargetIndex: command.DeckSeat,
			Value: 1));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = tableCard.InstanceId,
				SourceSeat = command.ActingSeat,
				SourceIndex = command.TableIndex,
				TargetIndex = command.DeckSeat,
				RemovedTableCardCount = 1,
			});
	}

	private RuleCommandResult ApplyPlanTableShuffle(
		PlanTableShuffleCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		RuleRejection? tableRejection = ValidateTableCardReferences(command.TableCards);
		if (tableRejection.HasValue)
		{
			return RuleCommandResult.Rejected(tableRejection.Value, ReadModel);
		}

		if (command.TableCards.Length != _state.TableCards.Count)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.TableCards.Length.ToString()),
				ReadModel);
		}
		for (int index = 0; index < command.TableCards.Length; index++)
		{
			if (command.TableCards[index].Index != index)
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidIndex, index.ToString()),
					ReadModel);
			}
		}

		var fixedIndexes = new HashSet<int>();
		if (!command.FixedTableIndexes.IsDefaultOrEmpty)
		{
			foreach (int fixedIndex in command.FixedTableIndexes)
			{
				if (fixedIndex < 0
					|| fixedIndex >= command.TableCards.Length
					|| !fixedIndexes.Add(fixedIndex))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidIndex, fixedIndex.ToString()),
						ReadModel);
				}
			}
		}

		var movableIndexes = new List<int>();
		var movableReferences = new List<RuleTableCardReference>();
		for (int index = 0; index < command.TableCards.Length; index++)
		{
			if (!fixedIndexes.Contains(index))
			{
				movableIndexes.Add(index);
				movableReferences.Add(command.TableCards[index]);
			}
		}
		if (movableReferences.Count < 2)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, "Fewer than two table cards can move."),
				ReadModel);
		}

		Shuffle(movableReferences, _randomSource);
		bool unchanged = true;
		for (int index = 0; index < movableReferences.Count; index++)
		{
			if (movableReferences[index].RuntimeInstanceId
				!= command.TableCards[movableIndexes[index]].RuntimeInstanceId)
			{
				unchanged = false;
				break;
			}
		}
		if (unchanged)
		{
			(movableReferences[^1], movableReferences[^2]) =
				(movableReferences[^2], movableReferences[^1]);
		}

		var plannedOrder = command.TableCards
			.Select(reference => reference.RuntimeInstanceId)
			.ToArray();
		int movableOrdinal = 0;
		for (int index = 0; index < plannedOrder.Length; index++)
		{
			if (!fixedIndexes.Contains(index))
			{
				plannedOrder[index] = movableReferences[movableOrdinal++].RuntimeInstanceId;
			}
		}

		_state.RulePendingTableOrderRuntimeInstanceIds.Clear();
		_state.RulePendingTableOrderRuntimeInstanceIds.AddRange(plannedOrder);
		_state.RulePendingPresentationBarrierId = _nextPresentationBarrierId++;
		_state.RuleRevision++;
		for (int sourceIndex = 0; sourceIndex < command.TableCards.Length; sourceIndex++)
		{
			RuleTableCardReference source = command.TableCards[sourceIndex];
			int targetIndex = Array.IndexOf(plannedOrder, source.RuntimeInstanceId);
			intents.Add(new PresentationIntent(
				PresentationIntentKind.TableShufflePlanned,
				"table.shuffle_planned",
				source.RuntimeInstanceId,
				command.ActingSeat,
				sourceIndex,
				targetIndex,
				BarrierId: _state.RulePendingPresentationBarrierId));
		}

		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				CollectedCardCount = movableReferences.Count,
			});
	}

	private RuleCommandResult ApplyBeginMandatorySelection(
		BeginMandatorySelectionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateItemCommandSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (_state.MandatorySelection.Kind != MandatorySelectionKind.None)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.MandatorySelectionPending,
					_state.MandatorySelection.Kind.ToString()),
				ReadModel);
		}

		ImmutableArray<int> legalIndexes = command.LegalSelectionIndexes.IsDefault
			? ImmutableArray<int>.Empty
			: command.LegalSelectionIndexes;
		var seenIndexes = new HashSet<int>();
		int maximumIndex = command.Kind switch
		{
			MandatorySelectionKind.HandLimitDiscard =>
				_state.PlayerHands[command.ActingSeat].Count
				+ _state.PlayerItemHands[command.ActingSeat].Count,
			MandatorySelectionKind.AttachmentHandCard =>
				_state.PlayerHands[command.ActingSeat].Count,
			_ => -1,
		};
		if (maximumIndex < 0
			|| legalIndexes.IsEmpty
			|| command.Kind == MandatorySelectionKind.HandLimitDiscard
				&& (!string.IsNullOrEmpty(command.ItemId) || command.SourceHandIndex != -1)
			|| command.Kind == MandatorySelectionKind.AttachmentHandCard
				&& (string.IsNullOrWhiteSpace(command.ItemId)
					|| command.SourceHandIndex < -1
					|| command.SourceHandIndex >= _state.PlayerItemHands[command.ActingSeat].Count)
			|| legalIndexes.Any(index => index < 0 || index >= maximumIndex || !seenIndexes.Add(index)))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidCommand, command.Kind.ToString()),
				ReadModel);
		}

		if (command.Kind == MandatorySelectionKind.AttachmentHandCard)
		{
			if (command.SourceHandIndex >= 0
				&& !string.Equals(
					_state.PlayerItemHands[command.ActingSeat][command.SourceHandIndex].Id,
					command.ItemId,
					StringComparison.Ordinal))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SourceHandIndex.ToString()),
					ReadModel);
			}

			foreach (int handIndex in legalIndexes)
			{
				if (_state.PlayerHands[command.ActingSeat][handIndex]
					.HasAttachedItemEffect(command.ItemId))
				{
					return RuleCommandResult.Rejected(
						new RuleRejection(RuleRejectionCode.InvalidCommand, handIndex.ToString()),
						ReadModel);
				}
			}
		}

		_state.MandatorySelection = new MandatorySelectionState(
			command.Kind,
			command.ActingSeat,
			command.SourceHandIndex,
			command.TurnGeneration,
			command.ItemId,
			legalIndexes);
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MandatorySelectionBegan,
			"mandatory_selection.began",
			Seat: command.ActingSeat,
			SourceIndex: command.SourceHandIndex,
			Value: (int)command.Kind,
			LogicalItemId: command.ItemId));
		return Accepted(intents);
	}

	private RuleCommandResult ApplyResolveMandatorySelection(
		ResolveMandatorySelectionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		MandatorySelectionState pending = _state.MandatorySelection;
		RuleRejection? rejection = ValidatePendingMandatorySelection(
			command.ActingSeat,
			command.ExpectedKind,
			pending);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ImmutableArray<int> legalIndexes = pending.NormalizedLegalSelectionIndexes;
		if (!legalIndexes.Contains(command.SelectionIndex))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidIndex, command.SelectionIndex.ToString()),
				ReadModel);
		}

		long cardId = 0;
		string itemId = string.Empty;
		if (pending.Kind == MandatorySelectionKind.HandLimitDiscard)
		{
			RuleRejection? displayRejection = ValidateHandDisplayCard(
				pending.Seat,
				command.SelectionIndex,
				command.ExpectedCardRuntimeInstanceId,
				command.ExpectedItemId,
				out bool isBaseCard,
				out int zoneIndex);
			if (displayRejection.HasValue)
			{
				return RuleCommandResult.Rejected(displayRejection.Value, ReadModel);
			}
			if (isBaseCard
					? command.ExpectedCardRuntimeInstanceId <= 0
					: string.IsNullOrEmpty(command.ExpectedItemId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SelectionIndex.ToString()),
					ReadModel);
			}

			if (isBaseCard)
			{
				cardId = _state.PlayerHands[pending.Seat][zoneIndex].InstanceId;
				_state.PlayerHands[pending.Seat].RemoveAt(zoneIndex);
			}
			else
			{
				itemId = _state.PlayerItemHands[pending.Seat][zoneIndex].Id;
				_state.PlayerItemHands[pending.Seat].RemoveAt(zoneIndex);
			}
			intents.Add(new PresentationIntent(
				PresentationIntentKind.HandCardDiscarded,
				"hand.card_discarded",
				cardId,
				pending.Seat,
				command.SelectionIndex,
				LogicalItemId: itemId));
		}
		else
		{
			if (command.ExpectedCardRuntimeInstanceId <= 0
				|| !string.IsNullOrEmpty(command.ExpectedItemId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SelectionIndex.ToString()),
					ReadModel);
			}

			PlayingCard card = _state.PlayerHands[pending.Seat][command.SelectionIndex];
			if (card.InstanceId != command.ExpectedCardRuntimeInstanceId
				|| card.HasAttachedItemEffect(pending.ItemId))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidCommand, command.SelectionIndex.ToString()),
					ReadModel);
			}
			cardId = card.InstanceId;
			itemId = pending.ItemId;
			_state.PlayerHands[pending.Seat][command.SelectionIndex] =
				card.WithAttachedItemEffect(pending.ItemId);
			intents.Add(new PresentationIntent(
				PresentationIntentKind.ItemAttached,
				"item.attached",
				cardId,
				pending.Seat,
				TargetIndex: command.SelectionIndex,
				LogicalItemId: pending.ItemId));
		}

		_state.MandatorySelection = MandatorySelectionState.None;
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MandatorySelectionResolved,
			"mandatory_selection.resolved",
			cardId,
			pending.Seat,
			TargetIndex: command.SelectionIndex,
			Value: (int)pending.Kind,
			LogicalItemId: itemId));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				LogicalEntityId = cardId,
				SourceSeat = pending.Seat,
				SourceIndex = command.SelectionIndex,
				TargetIndex = command.SelectionIndex,
				LogicalItemId = itemId,
			});
	}

	private RuleCommandResult ApplyClearMandatorySelection(
		ClearMandatorySelectionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		MandatorySelectionState pending = _state.MandatorySelection;
		RuleRejection? rejection = ValidatePendingMandatorySelection(
			command.ActingSeat,
			command.ExpectedKind,
			pending);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		_state.MandatorySelection = MandatorySelectionState.None;
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.MandatorySelectionCleared,
			"mandatory_selection.cleared",
			Seat: pending.Seat,
			Value: (int)pending.Kind,
			LogicalItemId: pending.ItemId));
		return Accepted(intents);
	}

	private RuleCommandResult ApplyEnqueuePendingAttachmentReward(
		EnqueuePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.Seat,
			command.ItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		_state.PendingAttachmentRewards.Add(
			new PendingAttachmentRewardState(command.Seat, command.ItemId));
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				TargetIndex = _state.PendingAttachmentRewards.Count - 1,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyTakePendingAttachmentReward(
		TakePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.ExpectedSeat,
			command.ExpectedItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		if (_state.PendingAttachmentRewards.Count == 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ExpectedItemId),
				ReadModel);
		}

		PendingAttachmentRewardState first = _state.PendingAttachmentRewards[0];
		if (first.Seat != command.ExpectedSeat
			|| !string.Equals(first.ItemId, command.ExpectedItemId, StringComparison.Ordinal))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ExpectedItemId),
				ReadModel);
		}

		_state.PendingAttachmentRewards.RemoveAt(0);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = first.Seat,
				SourceIndex = 0,
				LogicalItemId = first.ItemId,
			});
	}

	private RuleCommandResult ApplyRemovePendingAttachmentReward(
		RemovePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.Seat,
			command.ItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		int rewardIndex = FindLastPendingAttachmentReward(command.Seat, command.ItemId);
		if (rewardIndex < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ItemId),
				ReadModel);
		}

		_state.PendingAttachmentRewards.RemoveAt(rewardIndex);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = rewardIndex,
				LogicalItemId = command.ItemId,
			});
	}

	private RuleCommandResult ApplyReplacePendingAttachmentReward(
		ReplacePendingAttachmentRewardCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidatePendingAttachmentReward(
			command.Seat,
			command.ExpectedItemId);
		rejection ??= ValidatePendingAttachmentReward(
			command.Seat,
			command.ReplacementItemId);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		int rewardIndex = FindLastPendingAttachmentReward(
			command.Seat,
			command.ExpectedItemId);
		if (rewardIndex < 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.NoLegalAction, command.ExpectedItemId),
				ReadModel);
		}

		_state.PendingAttachmentRewards[rewardIndex] =
			new PendingAttachmentRewardState(command.Seat, command.ReplacementItemId);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.Seat,
				SourceIndex = rewardIndex,
				TargetIndex = rewardIndex,
				LogicalItemId = command.ReplacementItemId,
			});
	}

	private RuleCommandResult ApplySetPendingAttachmentRewards(
		SetPendingAttachmentRewardsCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (!_profile.Capabilities.Contains(MatchCapability.ItemRules))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.ModuleRejected, "Item rules are disabled."),
				ReadModel);
		}

		ImmutableArray<PendingAttachmentRewardState> rewards = command.Rewards.IsDefault
			? ImmutableArray<PendingAttachmentRewardState>.Empty
			: command.Rewards;
		foreach (PendingAttachmentRewardState reward in rewards)
		{
			RuleRejection? rejection = ValidatePendingAttachmentReward(
				reward.Seat,
				reward.ItemId);
			if (rejection.HasValue)
			{
				return RuleCommandResult.Rejected(rejection.Value, ReadModel);
			}
		}

		_state.PendingAttachmentRewards.Clear();
		_state.PendingAttachmentRewards.AddRange(rewards);
		_state.RuleRevision++;
		return Accepted(intents);
	}

	private RuleCommandResult ApplyBeginRealTurn(
		BeginRealTurnCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		_state.ExtraActions.BeginRealTurn();
		_state.RuleRevision++;
		return Accepted(intents);
	}

	private RuleCommandResult ApplyEndRealTurn(
		EndRealTurnCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ImmutableArray<string> markerIds = _state.ExtraActions.EndRealTurn();
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty,
			new ExtraActionCommandOutcome(RealTurnEndMarkerIds: markerIds));
	}

	private RuleCommandResult ApplyRegisterRealTurnEndMarker(
		RegisterRealTurnEndMarkerCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}
		if (string.IsNullOrWhiteSpace(command.MarkerId))
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.InvalidCommand,
					"A real-turn marker ID is required."),
				ReadModel);
		}

		string markerId = command.MarkerId.Trim();
		_state.ExtraActions.RegisterRealTurnEndMarker(markerId);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { LogicalItemId = markerId });
	}

	private RuleCommandResult ApplyGrantExtraAction(
		GrantExtraActionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ExtraActionGrantResult result =
			_state.ExtraActions.TryGrant(
				new ExtraActionGrantRequest(
					command.SourceId,
					command.ResumeNormalTurnAfterChain,
					command.ReentryPolicy,
					command.OncePerRealTurn));
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				LogicalItemId = command.SourceId,
			},
			new ExtraActionCommandOutcome(
				GrantStatus: result.Status,
				Grant: result.Grant,
				RootChainId: result.RootChainId,
				RootEventCount: result.RootEventCount,
				ExtraActionsExecuted: result.ExtraActionsExecuted,
				QueuedGrantCount: result.QueuedGrantCount));
	}

	private RuleCommandResult ApplyAcceptExtraAction(
		AcceptExtraActionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ExtraActionAcceptResult result =
			_state.ExtraActions.TryAccept(command.ExpectedGrant);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { SourceSeat = command.ActingSeat },
			new ExtraActionCommandOutcome(
				AcceptStatus: result.Status,
				Grant: result.Grant,
				RootChainId: result.RootChainId,
				ExtraActionsExecuted: result.ExtraActionsExecuted));
	}

	private RuleCommandResult ApplyFinishExtraActionExecution(
		FinishExtraActionExecutionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		_state.ExtraActions.FinishActionExecution(command.AcceptedGrant);
		_state.RuleRevision++;
		return Accepted(intents);
	}

	private RuleCommandResult ApplyCompleteExtraAction(
		CompleteExtraActionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ExtraActionCompletionResult result =
			_state.ExtraActions.Complete(command.ConsumedGrant);
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { SourceSeat = command.ActingSeat },
			new ExtraActionCommandOutcome(
				CompletionKind: result.Kind,
				RootChainId: result.RootChainId,
				RootEventCount: result.RootEventCount,
				ExtraActionsExecuted: result.ExtraActionsExecuted));
	}

	private RuleCommandResult ApplyAbortExtraActionChain(
		AbortExtraActionChainCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? rejection = ValidateExtraActionSeat(command.ActingSeat);
		if (rejection.HasValue)
		{
			return RuleCommandResult.Rejected(rejection.Value, ReadModel);
		}

		ExtraActionAbortResult result = _state.ExtraActions.Abort();
		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { SourceSeat = command.ActingSeat },
			new ExtraActionCommandOutcome(
				HadActiveRootChain: result.HadActiveRootChain,
				ResumeNormalTurn: result.ResumeNormalTurn,
				RootChainId: result.RootChainId,
				RootEventCount: result.RootEventCount,
				ExtraActionsExecuted: result.ExtraActionsExecuted,
				ClearedGrantCount: result.ClearedGrantCount));
	}

	private int FindLastPendingAttachmentReward(int seat, string itemId)
	{
		for (int index = _state.PendingAttachmentRewards.Count - 1; index >= 0; index--)
		{
			PendingAttachmentRewardState reward = _state.PendingAttachmentRewards[index];
			if (reward.Seat == seat
				&& string.Equals(reward.ItemId, itemId, StringComparison.Ordinal))
			{
				return index;
			}
		}

		return -1;
	}

	private RuleCommandResult ApplyAdvanceTurn(
		AdvanceTurnCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		RuleRejection? seatRejection = ValidateActingSeat(command.ActingSeat);
		if (seatRejection.HasValue)
		{
			return RuleCommandResult.Rejected(seatRejection.Value, ReadModel);
		}

		if (command.CandidateNextSeat < 0 || command.CandidateNextSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidSeat, command.CandidateNextSeat.ToString()),
				ReadModel);
		}
		if (command.AdvanceQuestionRefresh
			&& command.QuestionRefreshCyclesAfterCapture <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.InvalidCommand,
					command.QuestionRefreshCyclesAfterCapture.ToString()),
				ReadModel);
		}

		int previousSeat = _state.CurrentTurnPlayerIndex;
		int nextSeat = _turnAdvancementService.ApplyPlayedResult(
			_state,
			command.ActingSeat,
			command.Captured,
			command.CandidateNextSeat);
		if (global::NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(
			previousSeat,
			nextSeat))
		{
			ClearBlockedAutomatedItem(_state);
		}
		bool shouldRefreshQuestionCard = false;
		if (command.AdvanceQuestionRefresh)
		{
			QuestionCardRefreshAdvanceResult refreshResult =
				_turnAdvancementService.AdvanceQuestionCardRefresh(
					_state,
					_profile.Capabilities.Contains(MatchCapability.ItemRules),
					command.HasTableSpecialCard,
					nextSeat,
					command.QuestionRefreshCyclesAfterCapture);
			shouldRefreshQuestionCard = refreshResult.ShouldRefreshQuestionCard;
		}
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(
			PresentationIntentKind.TurnChanged,
			"turn.changed",
			Seat: nextSeat,
			SourceIndex: previousSeat,
			TargetIndex: nextSeat));
		return Accepted(
			intents,
			RuleMutationSummary.Empty with
			{
				SourceSeat = command.ActingSeat,
				SourceIndex = previousSeat,
				TargetIndex = nextSeat,
				Captured = command.Captured,
				ShouldRefreshQuestionCard = shouldRefreshQuestionCard,
			});
	}

	private RuleCommandResult ApplyAdvanceTurnGeneration(
		AdvanceTurnGenerationCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (_state.TurnSequenceGeneration == int.MaxValue)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Turn generation is exhausted."),
				ReadModel);
		}

		_state.TurnSequenceGeneration++;
		_state.RulePendingPresentationBarrierId = 0;
		_state.RulePendingTableOrderRuntimeInstanceIds.Clear();
		ClearBlockedAutomatedItem(_state);
		_state.RuleRevision++;
		return Accepted(intents);
	}

	private RuleCommandResult ApplyEvaluateMatchOutcome(
		EvaluateMatchOutcomeCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (command.ScoreOrLifeWinnerSeat < -1
			|| command.ScoreOrLifeWinnerSeat >= _state.PlayerCount
			|| !command.HasPlayableItemActions.IsDefault
				&& command.HasPlayableItemActions.Length != _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidConfiguration, string.Empty),
				ReadModel);
		}

		int scoreOrLifeWinnerSeat = command.ScoreOrLifeWinnerSeat;
		if (scoreOrLifeWinnerSeat < 0 && !_profile.Capabilities.Contains(MatchCapability.LifeBattle))
		{
			for (int seat = 0; seat < _state.PlayerCount; seat++)
			{
				if (_state.PlayerScores[seat] >= _state.WinningScore)
				{
					scoreOrLifeWinnerSeat = seat;
					break;
				}
			}
		}

		global::MatchOutcome outcome;
		if (command.SettleRegardlessOfAvailableActions)
		{
			outcome = MatchOutcomeRules.ResolveStalemate(_state.PlayerScores, _state.PlayerCount);
		}
		else
		{
			var playerStates = new GameTurnPlayerCardState[_state.PlayerCount];
			for (int seat = 0; seat < _state.PlayerCount; seat++)
			{
				playerStates[seat] = new GameTurnPlayerCardState(
					_state.PlayerHands[seat].Count,
					_state.PlayerDecks[seat].Count,
					_state.PlayerItemHands[seat].Count,
					!command.HasPlayableItemActions.IsDefault
						&& command.HasPlayableItemActions[seat]);
			}

			outcome = MatchOutcomeRules.Evaluate(
				playerStates,
				_state.PlayerScores,
				_state.PlayerCount,
				scoreOrLifeWinnerSeat);
		}

		if (outcome.Kind != global::MatchOutcomeKind.InProgress
			&& _state.TurnSequenceGeneration == int.MaxValue)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.InvalidConfiguration, "Turn generation is exhausted."),
				ReadModel);
		}

		bool outcomeChanged = outcome.Kind != global::MatchOutcomeKind.InProgress;
		if (outcomeChanged)
		{
			_state.MatchOutcomeKind = outcome.Kind;
			_state.WinnerPlayerIndex = outcome.WinnerPlayerIndex;
			_state.TurnSequenceGeneration++;
			_state.ExtraActions.Reset();
			ClearBlockedAutomatedItem(_state);
			if (outcome.Kind == global::MatchOutcomeKind.Winner)
			{
				_state.CurrentTurnPlayerIndex = outcome.WinnerPlayerIndex;
			}

			intents.Add(new PresentationIntent(
				PresentationIntentKind.MatchEnded,
				outcome.Kind == global::MatchOutcomeKind.Winner
					? "match.winner"
					: "match.draw",
				Seat: outcome.WinnerPlayerIndex));
		}

		_state.RuleRevision++;
		return Accepted(
			intents,
			RuleMutationSummary.Empty with { MatchOutcomeChanged = outcomeChanged });
	}

	private static void ClearBlockedAutomatedItem(GameBoardState state)
	{
		state.MatchFlow = state.MatchFlow with
		{
			BlockedAutomatedItemSeat = -1,
			BlockedAutomatedItemId = string.Empty,
		};
	}

	private RuleRejection? ValidateActingSeat(int seat)
	{
		if (seat < 0 || seat >= _state.PlayerCount)
		{
			return new RuleRejection(RuleRejectionCode.InvalidSeat, seat.ToString());
		}

		return seat == _state.CurrentTurnPlayerIndex
			? null
			: new RuleRejection(RuleRejectionCode.WrongTurn, seat.ToString());
	}

	private RuleRejection? ValidateItemCommandSeat(int seat)
	{
		if (!_profile.Capabilities.Contains(MatchCapability.ItemRules))
		{
			return new RuleRejection(RuleRejectionCode.ModuleRejected, "Item rules are disabled.");
		}

		return ValidateActingSeat(seat);
	}

	private RuleRejection? ValidateExtraActionSeat(int seat)
	{
		if (!_profile.Capabilities.Contains(MatchCapability.UpgradeSelection))
		{
			return new RuleRejection(
				RuleRejectionCode.ModuleRejected,
				"Extra actions are disabled.");
		}

		return ValidateActingSeat(seat);
	}

	private RuleRejection? ValidatePendingAttachmentReward(int seat, string itemId)
	{
		if (!_profile.Capabilities.Contains(MatchCapability.ItemRules))
		{
			return new RuleRejection(RuleRejectionCode.ModuleRejected, "Item rules are disabled.");
		}

		if (seat < 0 || seat >= _state.PlayerCount)
		{
			return new RuleRejection(RuleRejectionCode.InvalidSeat, seat.ToString());
		}

		return string.IsNullOrWhiteSpace(itemId)
			? new RuleRejection(RuleRejectionCode.InvalidCommand, "Attachment item ID is required.")
			: null;
	}

	private RuleRejection? ValidatePendingMandatorySelection(
		int actingSeat,
		MandatorySelectionKind expectedKind,
		MandatorySelectionState pending)
	{
		if (!_profile.Capabilities.Contains(MatchCapability.ItemRules))
		{
			return new RuleRejection(RuleRejectionCode.ModuleRejected, "Item rules are disabled.");
		}

		if (pending.Kind == MandatorySelectionKind.None)
		{
			return new RuleRejection(RuleRejectionCode.MandatorySelectionMissing, string.Empty);
		}

		if (actingSeat != pending.Seat
			|| expectedKind != pending.Kind
			|| pending.TurnGeneration != _state.TurnSequenceGeneration)
		{
			return new RuleRejection(
				RuleRejectionCode.MandatorySelectionMismatch,
				expectedKind.ToString());
		}

		return ValidateActingSeat(actingSeat);
	}

	private RuleRejection? ValidateHandDisplayCard(
		int seat,
		int displayIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		out bool isBaseCard,
		out int zoneIndex)
	{
		isBaseCard = false;
		zoneIndex = -1;
		List<PlayingCard> hand = _state.PlayerHands[seat];
		List<ItemCard> itemHand = _state.PlayerItemHands[seat];
		if (displayIndex < 0 || displayIndex >= hand.Count + itemHand.Count)
		{
			return new RuleRejection(RuleRejectionCode.InvalidIndex, displayIndex.ToString());
		}

		if (displayIndex < hand.Count)
		{
			PlayingCard card = hand[displayIndex];
			if (expectedCardRuntimeInstanceId > 0
				&& card.InstanceId != expectedCardRuntimeInstanceId
				|| !string.IsNullOrEmpty(expectedItemId))
			{
				return new RuleRejection(RuleRejectionCode.InvalidCommand, displayIndex.ToString());
			}

			isBaseCard = true;
			zoneIndex = displayIndex;
			return null;
		}

		zoneIndex = displayIndex - hand.Count;
		ItemCard item = itemHand[zoneIndex];
		if (expectedCardRuntimeInstanceId != 0
			|| !string.IsNullOrEmpty(expectedItemId)
				&& !string.Equals(item.Id, expectedItemId, StringComparison.Ordinal))
		{
			return new RuleRejection(RuleRejectionCode.InvalidCommand, displayIndex.ToString());
		}

		return null;
	}

	private RuleRejection? ValidateTableCardReferences(
		ImmutableArray<RuleTableCardReference> references)
	{
		if (references.IsDefaultOrEmpty)
		{
			return new RuleRejection(RuleRejectionCode.NoLegalAction, "No table card was supplied.");
		}

		var seenIndexes = new HashSet<int>();
		foreach (RuleTableCardReference reference in references)
		{
			if (reference.Index < 0
				|| reference.Index >= _state.TableCards.Count
				|| reference.RuntimeInstanceId <= 0
				|| !seenIndexes.Add(reference.Index)
				|| _state.TableCards[reference.Index].InstanceId != reference.RuntimeInstanceId)
			{
				return new RuleRejection(RuleRejectionCode.InvalidIndex, reference.Index.ToString());
			}
		}

		return null;
	}

	private bool ContainsCardRuntimeInstanceId(long runtimeInstanceId)
	{
		for (int seat = 0; seat < _state.PlayerCount; seat++)
		{
			if (_state.PlayerDecks[seat].Any(card => card.InstanceId == runtimeInstanceId)
				|| _state.PlayerHands[seat].Any(card => card.InstanceId == runtimeInstanceId))
			{
				return true;
			}
		}

		return _state.TableCards.Any(tableCard =>
			tableCard.Card.HasValue
			&& tableCard.Card.Value.InstanceId == runtimeInstanceId);
	}

	private RuleRejection? TryBuildCaptureIndexSet(
		ImmutableArray<int> indexes,
		int captureStartIndex,
		bool requireBeforeCaptureStart,
		out HashSet<int>? result)
	{
		result = null;
		if (indexes.IsDefaultOrEmpty)
		{
			return null;
		}

		result = new HashSet<int>();
		foreach (int index in indexes)
		{
			if (index < 0
				|| index >= _state.TableCards.Count
				|| requireBeforeCaptureStart && index >= captureStartIndex
				|| !_state.TableCards[index].Card.HasValue
				|| _state.TableCards[index].IsBuriedInSand
				|| !result.Add(index))
			{
				return new RuleRejection(RuleRejectionCode.InvalidIndex, index.ToString());
			}
		}

		return null;
	}

	private static RuleRejection? TryBuildPreservedCaptureIndexSet(
		ImmutableArray<int> indexes,
		CaptureStatePlan plan,
		out HashSet<int>? result)
	{
		result = null;
		if (indexes.IsDefaultOrEmpty)
		{
			return null;
		}

		result = new HashSet<int>();
		foreach (int index in indexes)
		{
			if (!IsCapturedIndex(plan, index) || !result.Add(index))
			{
				return new RuleRejection(RuleRejectionCode.InvalidIndex, index.ToString());
			}
		}

		return null;
	}

	private static bool IsCapturedIndex(CaptureStatePlan plan, int index)
	{
		return plan.BaseTableCardIndexes.Contains(index)
			|| plan.ExtraTableCardIndexes.Contains(index);
	}

	private RuleRejection? TryBuildScoreChanges(
		ImmutableArray<RuleScoreMutation> mutations,
		bool allowEmpty,
		out ImmutableArray<RuleScoreChange> changes)
	{
		changes = ImmutableArray<RuleScoreChange>.Empty;
		if (mutations.IsDefaultOrEmpty)
		{
			return allowEmpty
				? null
				: new RuleRejection(RuleRejectionCode.InvalidCommand, "Score mutations cannot be empty.");
		}

		var workingScores = new int[_state.PlayerScores.Length];
		Array.Copy(_state.PlayerScores, workingScores, workingScores.Length);
		var builder = ImmutableArray.CreateBuilder<RuleScoreChange>(mutations.Length);
		foreach (RuleScoreMutation mutation in mutations)
		{
			if (mutation.Seat < 0
				|| mutation.Seat >= _state.PlayerCount
				|| mutation.Value < 0
				|| !Enum.IsDefined(mutation.Kind))
			{
				return new RuleRejection(RuleRejectionCode.InvalidCommand, mutation.Seat.ToString());
			}

			int previousValue = workingScores[mutation.Seat];
			int currentValue = mutation.Kind switch
			{
				RuleScoreMutationKind.Add => (int)Math.Min(
					int.MaxValue,
					(long)previousValue + mutation.Value),
				RuleScoreMutationKind.Subtract => Math.Max(0, previousValue - mutation.Value),
				RuleScoreMutationKind.Set => mutation.Value,
				_ => previousValue,
			};
			workingScores[mutation.Seat] = currentValue;
			builder.Add(new RuleScoreChange(mutation.Seat, previousValue, currentValue));
		}

		changes = builder.MoveToImmutable();
		return null;
	}

	private void ApplyScoreChanges(ImmutableArray<RuleScoreChange> changes)
	{
		foreach (RuleScoreChange change in changes)
		{
			_state.PlayerScores[change.Seat] = change.CurrentValue;
		}
	}

	private static void AddScoreChangedIntents(
		ImmutableArray<PresentationIntent>.Builder intents,
		ImmutableArray<RuleScoreChange> changes)
	{
		foreach (RuleScoreChange change in changes)
		{
			intents.Add(new PresentationIntent(
				PresentationIntentKind.ScoreChanged,
				"score.changed",
				Seat: change.Seat,
				Value: change.Delta));
		}
	}

	private RuleCommandResult ApplyEndSession(
		EndMatchSessionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		_state.MandatorySelection = MandatorySelectionState.None;
		_state.PendingAttachmentRewards.Clear();
		_state.ExtraActions.Reset();
		_state.RuleSessionEnded = true;
		_state.RuleRevision++;
		intents.Add(new PresentationIntent(PresentationIntentKind.MatchEnded, "match.session_ended"));
		return Accepted(intents);
	}

	private RuleCommandResult ApplyPresentationBarrierCompletion(
		CompletePresentationBarrierCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (_state.RulePendingPresentationBarrierId <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.PresentationBarrierMissing, string.Empty),
				ReadModel);
		}

		if (command.BarrierId != _state.RulePendingPresentationBarrierId)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(RuleRejectionCode.PresentationBarrierMismatch, command.BarrierId.ToString()),
				ReadModel);
		}

		if (_state.RulePendingTableOrderRuntimeInstanceIds.Count > 0)
		{
			var tableCardsById = _state.TableCards.ToDictionary(card => card.InstanceId);
			if (_state.RulePendingTableOrderRuntimeInstanceIds.Count != _state.TableCards.Count
				|| tableCardsById.Count != _state.TableCards.Count
				|| _state.RulePendingTableOrderRuntimeInstanceIds.Any(id => !tableCardsById.ContainsKey(id)))
			{
				return RuleCommandResult.Rejected(
					new RuleRejection(RuleRejectionCode.InvalidConfiguration, "The pending table order is invalid."),
					ReadModel);
			}

			_state.TableCards.Clear();
			foreach (long runtimeInstanceId in _state.RulePendingTableOrderRuntimeInstanceIds)
			{
				_state.TableCards.Add(tableCardsById[runtimeInstanceId]);
			}
			_state.RulePendingTableOrderRuntimeInstanceIds.Clear();
		}

		_state.RulePendingPresentationBarrierId = 0;
		_state.RuleRevision++;
		return Accepted(intents);
	}

	private RuleCommandResult Accepted(ImmutableArray<PresentationIntent>.Builder intents)
	{
		return Accepted(intents, RuleMutationSummary.Empty);
	}

	private RuleCommandResult Accepted(
		ImmutableArray<PresentationIntent>.Builder intents,
		RuleMutationSummary mutation)
	{
		return Accepted(intents, mutation, null);
	}

	private RuleCommandResult Accepted(
		ImmutableArray<PresentationIntent>.Builder intents,
		RuleMutationSummary mutation,
		ExtraActionCommandOutcome? extraActionOutcome)
	{
		MatchReadModel readModel = CreateReadModel(_state) with
		{
			Adventure = _adventureReadModel,
		};
		_readModel = readModel;
		return new RuleCommandResult(
			true,
			RuleRejection.None,
			readModel.Revision,
			readModel.TurnGeneration,
			intents.ToImmutable(),
			readModel,
			mutation,
			extraActionOutcome);
	}

	private static void ResetForInitialization(
		GameBoardState state,
		int playerCount,
		int winningScore,
		int lifecycleGeneration,
		int turnGeneration)
	{
		state.PlayerCount = playerCount;
		state.WinningScore = winningScore;
		state.CurrentTurnPlayerIndex = 0;
		state.TurnSequenceGeneration = turnGeneration;
		state.CompletedTurnsSinceQuestionCardRefresh = 0;
		state.QuestionCardRefreshAnchorPlayerIndex = -1;
		state.MatchOutcomeKind = global::MatchOutcomeKind.InProgress;
		state.WinnerPlayerIndex = -1;
		state.MandatorySelection = MandatorySelectionState.None;
		state.PendingAttachmentRewards.Clear();
		state.ExtraActions.Reset();
		state.MatchFlow = MatchFlowState.NewMatch;
		state.RuleSessionInitialized = true;
		state.RuleSessionEnded = false;
		state.RuleRevision = 0;
		state.RuleLifecycleGeneration = lifecycleGeneration;
		state.RulePendingPresentationBarrierId = 0;
		state.RulePendingTableOrderRuntimeInstanceIds.Clear();

		state.PlayerDecks.Clear();
		state.PlayerHands.Clear();
		state.PlayerItemHands.Clear();
		state.TableCards.Clear();
		Array.Clear(state.PlayerScores);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			state.PlayerDecks.Add(new List<PlayingCard>());
			state.PlayerHands.Add(new List<PlayingCard>());
			state.PlayerItemHands.Add(new List<ItemCard>());
		}
	}

	internal static void RestoreState(GameBoardState state, MatchStateSnapshot snapshot)
	{
		state.PlayerCount = snapshot.PlayerCount;
		state.WinningScore = snapshot.WinningScore;
		state.CurrentTurnPlayerIndex = snapshot.CurrentTurnSeat;
		state.TurnSequenceGeneration = snapshot.TurnGeneration;
		state.CompletedTurnsSinceQuestionCardRefresh = snapshot.CompletedTurnsSinceQuestionRefresh;
		state.QuestionCardRefreshAnchorPlayerIndex = snapshot.QuestionRefreshAnchorSeat;
		state.MatchOutcomeKind = snapshot.Outcome switch
		{
			MatchOutcome.Won => global::MatchOutcomeKind.Winner,
			MatchOutcome.Draw => global::MatchOutcomeKind.Draw,
			_ => global::MatchOutcomeKind.InProgress,
		};
		state.WinnerPlayerIndex = snapshot.WinnerSeat;
		state.RuleSessionInitialized = true;
		state.RuleSessionEnded = snapshot.Outcome == MatchOutcome.Ended;
		state.RuleRevision = snapshot.Revision;
		state.RuleLifecycleGeneration = snapshot.LifecycleGeneration;
		state.RulePendingPresentationBarrierId = snapshot.PendingPresentationBarrierId;
		state.RulePendingTableOrderRuntimeInstanceIds.Clear();
		state.RulePendingTableOrderRuntimeInstanceIds.AddRange(
			snapshot.PendingTableOrderRuntimeInstanceIds.IsDefault
				? ImmutableArray<long>.Empty
				: snapshot.PendingTableOrderRuntimeInstanceIds);
		MandatorySelectionState snapshotSelection = snapshot.MandatorySelection
			?? MandatorySelectionState.None;
		state.MandatorySelection = snapshotSelection with
		{
			LegalSelectionIndexes = snapshotSelection.NormalizedLegalSelectionIndexes,
		};
		state.PendingAttachmentRewards.Clear();
		state.PendingAttachmentRewards.AddRange(
			snapshot.NormalizedPendingAttachmentRewards);
		state.ExtraActions.RestoreSnapshot(
			snapshot.NormalizedExtraActions);
		state.MatchFlow = snapshot.NormalizedMatchFlow;

		state.PlayerDecks.Clear();
		state.PlayerHands.Clear();
		state.PlayerItemHands.Clear();
		state.TableCards.Clear();
		Array.Clear(state.PlayerScores);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			state.PlayerDecks.Add(new List<PlayingCard>());
			state.PlayerHands.Add(new List<PlayingCard>());
			state.PlayerItemHands.Add(new List<ItemCard>());
			if (seat >= snapshot.PlayerCount)
			{
				continue;
			}

			PlayerStateSnapshot player = snapshot.Players[seat];
			state.PlayerDecks[seat].AddRange(player.Deck.Select(FromLogicalCard));
			state.PlayerHands[seat].AddRange(player.Hand.Select(FromLogicalCard));
			state.PlayerItemHands[seat].AddRange(player.ItemHand.Select(item => new ItemCard(item.Id)));
			state.PlayerScores[seat] = player.Score;
		}

		foreach (TableCardState tableCard in snapshot.TableCards)
		{
			TableCard restored = TableCard.Restore(
				tableCard.RuntimeInstanceId,
				tableCard.Card.HasValue ? FromLogicalCard(tableCard.Card.Value) : null,
				tableCard.IsFaceUp,
				tableCard.ItemOverlay.HasValue ? new ItemCard(tableCard.ItemOverlay.Value.Id) : null,
				tableCard.ItemOwnerSeat,
				tableCard.CardOwnerSeat,
				tableCard.RemoveOnCapture);
			restored.IsHiddenDuringCapture = tableCard.IsHiddenDuringCapture;
			restored.IsBuriedInSand = tableCard.IsBuried;
			restored.IsSlimeCoated = tableCard.IsScoreSuppressed;
			state.TableCards.Add(restored);
		}
	}

	private static MatchReadModel CreateReadModel(GameBoardState state)
	{
		return new MatchReadModel(
			state.RuleRevision,
			state.RuleLifecycleGeneration,
			state.TurnSequenceGeneration,
			state.PlayerCount,
			state.WinningScore,
			state.CurrentTurnPlayerIndex,
			GetOutcome(state),
			state.WinnerPlayerIndex,
			CreatePlayerSnapshots(state),
			CreateTableCardSnapshots(state),
			state.RulePendingPresentationBarrierId,
			state.RulePendingTableOrderRuntimeInstanceIds.ToImmutableArray(),
			state.MandatorySelection,
			state.PendingAttachmentRewards.ToImmutableArray(),
			state.ExtraActions.CaptureSnapshot(),
			state.CompletedTurnsSinceQuestionCardRefresh,
			state.QuestionCardRefreshAnchorPlayerIndex,
			state.MatchFlow);
	}

	private static MatchStateSnapshot CreateSnapshot(
		GameBoardState state,
		RuleRandomState randomState,
		ImmutableArray<long> appliedActionIds)
	{
		return new MatchStateSnapshot(
			MatchStateSnapshot.CurrentSchemaVersion,
			state.RuleRevision,
			state.RuleLifecycleGeneration,
			state.TurnSequenceGeneration,
			state.PlayerCount,
			state.WinningScore,
			state.CurrentTurnPlayerIndex,
			GetOutcome(state),
			state.WinnerPlayerIndex,
			CreatePlayerSnapshots(state),
			CreateTableCardSnapshots(state),
			randomState,
			state.RulePendingPresentationBarrierId,
			appliedActionIds,
			state.RulePendingTableOrderRuntimeInstanceIds.ToImmutableArray(),
			state.MandatorySelection,
			state.PendingAttachmentRewards.ToImmutableArray(),
			state.ExtraActions.CaptureSnapshot(),
			state.CompletedTurnsSinceQuestionCardRefresh,
			state.QuestionCardRefreshAnchorPlayerIndex,
			state.MatchFlow);
	}

	private static ImmutableArray<PlayerStateSnapshot> CreatePlayerSnapshots(GameBoardState state)
	{
		var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(state.PlayerCount);
		for (int seat = 0; seat < state.PlayerCount; seat++)
		{
			players.Add(new PlayerStateSnapshot(
				seat,
				state.PlayerDecks[seat].Select(ToLogicalCard).ToImmutableArray(),
				state.PlayerHands[seat].Select(ToLogicalCard).ToImmutableArray(),
				state.PlayerItemHands[seat].Select(item => new LogicalItemState(item.Id)).ToImmutableArray(),
				state.PlayerScores[seat]));
		}

		return players.MoveToImmutable();
	}

	private static ImmutableArray<TableCardState> CreateTableCardSnapshots(GameBoardState state)
	{
		var tableCards = ImmutableArray.CreateBuilder<TableCardState>(state.TableCards.Count);
		foreach (TableCard tableCard in state.TableCards)
		{
			tableCards.Add(new TableCardState(
				tableCard.InstanceId,
				tableCard.Card.HasValue ? ToLogicalCard(tableCard.Card.Value) : null,
				tableCard.IsFaceUp,
				tableCard.IsHiddenDuringCapture,
				tableCard.IsBuriedInSand,
				tableCard.IsSlimeCoated,
				tableCard.ItemOverlay.HasValue ? new LogicalItemState(tableCard.ItemOverlay.Value.Id) : null,
				tableCard.ItemOwnerPlayerIndex,
				tableCard.CardOwnerPlayerIndex,
				tableCard.RemoveCardOnCapture));
		}

		return tableCards.MoveToImmutable();
	}

	private static MatchOutcome GetOutcome(GameBoardState state)
	{
		if (state.RuleSessionEnded)
		{
			return MatchOutcome.Ended;
		}

		return state.MatchOutcomeKind switch
		{
			global::MatchOutcomeKind.Winner => MatchOutcome.Won,
			global::MatchOutcomeKind.Draw => MatchOutcome.Draw,
			_ => MatchOutcome.InProgress,
		};
	}

	private static LogicalCardState ToLogicalCard(PlayingCard card)
	{
		return new LogicalCardState(
			card.InstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.GetAttachedItemCardIds().ToImmutableArray());
	}

	private static PlayingCard FromLogicalCard(LogicalCardState card)
	{
		return PlayingCard.Restore(
			card.RuntimeInstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.NormalizedAttachedEffectIds);
	}

	private static void DealEqualRanks(
		GameBoardState state,
		List<PlayingCard> deck,
		IRuleRandomSource randomSource)
	{
		var cardsByRank = new Dictionary<string, List<PlayingCard>>(StringComparer.Ordinal);
		foreach (PlayingCard card in deck)
		{
			if (!cardsByRank.TryGetValue(card.Rank, out List<PlayingCard>? rankCards))
			{
				rankCards = new List<PlayingCard>();
				cardsByRank.Add(card.Rank, rankCards);
			}

			rankCards.Add(card);
		}

		var remainingCards = new List<PlayingCard>();
		foreach (List<PlayingCard> rankCards in cardsByRank.Values)
		{
			Shuffle(rankCards, randomSource);
			int cardsPerPlayer = rankCards.Count / state.PlayerCount;
			for (int seat = 0; seat < state.PlayerCount; seat++)
			{
				for (int cardOffset = 0; cardOffset < cardsPerPlayer; cardOffset++)
				{
					state.PlayerDecks[seat].Add(rankCards[seat * cardsPerPlayer + cardOffset]);
				}
			}

			for (int index = cardsPerPlayer * state.PlayerCount; index < rankCards.Count; index++)
			{
				remainingCards.Add(rankCards[index]);
			}
		}

		Shuffle(remainingCards, randomSource);
		foreach (PlayingCard card in remainingCards)
		{
			int seat = randomSource.NextIntInclusive(0, state.PlayerCount - 1);
			state.PlayerDecks[seat].Add(card);
		}

		for (int seat = 0; seat < state.PlayerCount; seat++)
		{
			Shuffle(state.PlayerDecks[seat], randomSource);
			Cut(state.PlayerDecks[seat], randomSource);
		}
	}

	private static void Shuffle<T>(List<T> values, IRuleRandomSource randomSource)
	{
		for (int index = values.Count - 1; index > 0; index--)
		{
			int swapIndex = randomSource.NextIntInclusive(0, index);
			(values[index], values[swapIndex]) = (values[swapIndex], values[index]);
		}
	}

	private static void Cut<T>(List<T> values, IRuleRandomSource randomSource)
	{
		if (values.Count < 2)
		{
			return;
		}

		int cutIndex = randomSource.NextIntInclusive(1, values.Count - 1);
		values.Reverse(0, cutIndex);
		values.Reverse(cutIndex, values.Count - cutIndex);
		values.Reverse();
	}

	private static MatchReadModel EmptyReadModel { get; } = new(
		0,
		0,
		0,
		0,
		0,
		-1,
		MatchOutcome.InProgress,
		-1,
		ImmutableArray<PlayerStateSnapshot>.Empty,
		ImmutableArray<TableCardState>.Empty,
		0);
}

public sealed class ReplicaRuleSession
{
	private MatchReadModel _readModel = EmptyReadModel;
	private bool _hasSnapshot;

	public MatchReadModel ReadModel => _readModel;

	public SnapshotApplyResult ApplyAuthoritySnapshot(MatchStateSnapshot snapshot)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		try
		{
			SnapshotValidation.ValidateOrThrow(snapshot, nameof(snapshot));
		}
		catch (ArgumentException exception)
		{
			return Reject(RuleRejectionCode.InvalidConfiguration, exception.Message);
		}

		if (_hasSnapshot && snapshot.LifecycleGeneration < _readModel.LifecycleGeneration)
		{
			return Reject(RuleRejectionCode.StaleLifecycleGeneration, snapshot.LifecycleGeneration.ToString());
		}

		if (_hasSnapshot
			&& snapshot.LifecycleGeneration == _readModel.LifecycleGeneration
			&& snapshot.Revision <= _readModel.Revision)
		{
			return Reject(RuleRejectionCode.StaleRevision, snapshot.Revision.ToString());
		}

		_readModel = CreateReadModel(snapshot);
		_hasSnapshot = true;
		return new SnapshotApplyResult(true, RuleRejection.None, _readModel);
	}

	private SnapshotApplyResult Reject(RuleRejectionCode code, string detail)
	{
		return new SnapshotApplyResult(false, new RuleRejection(code, detail), _readModel);
	}

	private static MatchReadModel CreateReadModel(MatchStateSnapshot snapshot)
	{
		return new MatchReadModel(
			snapshot.Revision,
			snapshot.LifecycleGeneration,
			snapshot.TurnGeneration,
			snapshot.PlayerCount,
			snapshot.WinningScore,
			snapshot.CurrentTurnSeat,
			snapshot.Outcome,
			snapshot.WinnerSeat,
			snapshot.Players,
			snapshot.TableCards,
			snapshot.PendingPresentationBarrierId,
				snapshot.PendingTableOrderRuntimeInstanceIds,
				snapshot.MandatorySelection,
				snapshot.PendingAttachmentRewards,
				snapshot.ExtraActions,
				snapshot.CompletedTurnsSinceQuestionRefresh,
				snapshot.QuestionRefreshAnchorSeat,
				snapshot.MatchFlow);
	}

	private static MatchReadModel EmptyReadModel { get; } = new(
		0,
		0,
		0,
		0,
		0,
		-1,
		MatchOutcome.InProgress,
		-1,
		ImmutableArray<PlayerStateSnapshot>.Empty,
		ImmutableArray<TableCardState>.Empty,
		0);
}

internal static class SnapshotValidation
{
	internal static void ValidateOrThrow(MatchStateSnapshot snapshot, string parameterName)
	{
		if (snapshot.SchemaVersion != MatchStateSnapshot.CurrentSchemaVersion
			|| snapshot.Revision < 0
			|| snapshot.LifecycleGeneration <= 0
			|| snapshot.TurnGeneration <= 0
			|| snapshot.PlayerCount is < 2 or > 4
			|| snapshot.WinningScore <= 0
			|| snapshot.CurrentTurnSeat < 0
			|| snapshot.CurrentTurnSeat >= snapshot.PlayerCount
			|| snapshot.WinnerSeat < -1
			|| snapshot.WinnerSeat >= snapshot.PlayerCount
			|| snapshot.Players.IsDefault
			|| snapshot.Players.Length != snapshot.PlayerCount
			|| snapshot.TableCards.IsDefault
			|| snapshot.PendingPresentationBarrierId < 0
			|| snapshot.AppliedActionIds.IsDefault
			|| snapshot.CompletedTurnsSinceQuestionRefresh < 0
			|| snapshot.QuestionRefreshAnchorSeat < -1
			|| snapshot.QuestionRefreshAnchorSeat >= snapshot.PlayerCount)
		{
			throw new ArgumentException("The match snapshot is structurally invalid.", parameterName);
		}

		for (int seat = 0; seat < snapshot.Players.Length; seat++)
		{
			PlayerStateSnapshot player = snapshot.Players[seat];
			if (player.Seat != seat
				|| player.Score < 0
				|| player.Deck.IsDefault
				|| player.Hand.IsDefault
				|| player.ItemHand.IsDefault)
			{
				throw new ArgumentException("The match snapshot contains an invalid player.", parameterName);
			}
		}

		if (snapshot.AppliedActionIds.Any(actionId => actionId <= 0)
			|| snapshot.AppliedActionIds.Distinct().Count() != snapshot.AppliedActionIds.Length)
		{
			throw new ArgumentException("The match snapshot contains invalid action IDs.", parameterName);
		}

		ImmutableArray<long> pendingTableOrder = snapshot.PendingTableOrderRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: snapshot.PendingTableOrderRuntimeInstanceIds;
		if (pendingTableOrder.Length > 0
			&& (snapshot.PendingPresentationBarrierId <= 0
				|| pendingTableOrder.Length != snapshot.TableCards.Length
				|| pendingTableOrder.Any(id => id <= 0)
				|| pendingTableOrder.Distinct().Count() != pendingTableOrder.Length
				|| !pendingTableOrder.Order().SequenceEqual(
					snapshot.TableCards.Select(card => card.RuntimeInstanceId).Order())))
		{
			throw new ArgumentException("The match snapshot contains an invalid pending table order.", parameterName);
		}

		ValidateMandatorySelection(snapshot, parameterName);
		ValidatePendingAttachmentRewards(snapshot, parameterName);
		ValidateExtraActions(snapshot.NormalizedExtraActions, parameterName);
		if (!MatchFlowStateValidation.IsValid(snapshot.NormalizedMatchFlow)
			|| snapshot.NormalizedMatchFlow.PendingWinnerSeat >= snapshot.PlayerCount
			|| snapshot.NormalizedMatchFlow.BlockedAutomatedItemSeat >= snapshot.PlayerCount)
		{
			throw new ArgumentException(
				"The match snapshot contains invalid flow state.",
				parameterName);
		}
	}

	private static void ValidateExtraActions(
		ExtraActionQueueSnapshot extraActions,
		string parameterName)
	{
		if (extraActions.NextRootChainId <= 0
			|| extraActions.ActiveRootChainId < 0
			|| extraActions.RootEventCount < 0
			|| extraActions.RootMaxDepth < 0
			|| extraActions.ExtraActionsExecutedThisRealTurn < 0
			|| extraActions.QueuedGrants.IsDefault
			|| extraActions.SourcesInRootChain.IsDefault
			|| extraActions.PendingRealTurnEndMarkerIds.IsDefault
			|| extraActions.SourcesInRootChain.Any(string.IsNullOrWhiteSpace)
			|| extraActions.SourcesInRootChain.Distinct(StringComparer.Ordinal).Count()
				!= extraActions.SourcesInRootChain.Length
			|| extraActions.PendingRealTurnEndMarkerIds.Any(string.IsNullOrWhiteSpace)
			|| extraActions.PendingRealTurnEndMarkerIds.Distinct(StringComparer.Ordinal).Count()
				!= extraActions.PendingRealTurnEndMarkerIds.Length)
		{
			throw new ArgumentException(
				"The match snapshot contains invalid extra-action state.",
				parameterName);
		}

		IEnumerable<ExtraActionGrantState> grants = extraActions.QueuedGrants;
		if (extraActions.CurrentGrant.HasValue)
		{
			grants = grants.Prepend(extraActions.CurrentGrant.Value);
		}
		if (grants.Any(grant =>
			string.IsNullOrWhiteSpace(grant.SourceId)
			|| grant.RootChainId <= 0
			|| grant.ChainDepth < 0
			|| !Enum.IsDefined(grant.ReentryPolicy)
			|| grant.RootChainId != extraActions.ActiveRootChainId))
		{
			throw new ArgumentException(
				"The match snapshot contains an invalid extra-action grant.",
				parameterName);
		}

		if (extraActions.ActiveRootChainId == 0
			&& (extraActions.CurrentGrant.HasValue
				|| extraActions.QueuedGrants.Length > 0
				|| extraActions.SourcesInRootChain.Length > 0
				|| extraActions.RootResumesNormalTurn
				|| extraActions.RootDerivationStopped
				|| extraActions.RootEventCount != 0
				|| extraActions.RootMaxDepth != 0))
		{
			throw new ArgumentException(
				"The match snapshot contains an orphaned extra-action root.",
				parameterName);
		}
	}

	private static void ValidatePendingAttachmentRewards(
		MatchStateSnapshot snapshot,
		string parameterName)
	{
		foreach (PendingAttachmentRewardState reward in
			snapshot.NormalizedPendingAttachmentRewards)
		{
			if (reward.Seat < 0
				|| reward.Seat >= snapshot.PlayerCount
				|| string.IsNullOrWhiteSpace(reward.ItemId))
			{
				throw new ArgumentException(
					"The match snapshot contains an invalid pending attachment reward.",
					parameterName);
			}
		}
	}

	private static void ValidateMandatorySelection(
		MatchStateSnapshot snapshot,
		string parameterName)
	{
		MandatorySelectionState selection = snapshot.MandatorySelection
			?? MandatorySelectionState.None;
		ImmutableArray<int> legalIndexes = selection.NormalizedLegalSelectionIndexes;
		if (!Enum.IsDefined(selection.Kind))
		{
			throw new ArgumentException("The match snapshot contains an invalid mandatory selection kind.", parameterName);
		}

		if (selection.Kind == MandatorySelectionKind.None)
		{
			if (selection.Seat != -1
				|| selection.SourceHandIndex != -1
				|| selection.TurnGeneration != -1
				|| !string.IsNullOrEmpty(selection.ItemId)
				|| legalIndexes.Length != 0)
			{
				throw new ArgumentException("The match snapshot contains a non-empty mandatory selection sentinel.", parameterName);
			}

			return;
		}

		if (selection.Seat < 0 || selection.Seat >= snapshot.PlayerCount
			|| selection.TurnGeneration != snapshot.TurnGeneration
			|| selection.SourceHandIndex < -1
			|| legalIndexes.Length == 0
			|| legalIndexes.Distinct().Count() != legalIndexes.Length)
		{
			throw new ArgumentException("The match snapshot contains an invalid mandatory selection header.", parameterName);
		}

		PlayerStateSnapshot player = snapshot.Players[selection.Seat];
		int legalIndexLimit = selection.Kind == MandatorySelectionKind.HandLimitDiscard
			? player.Hand.Length + player.ItemHand.Length
			: player.Hand.Length;
		if (selection.Kind == MandatorySelectionKind.HandLimitDiscard
			&& (!string.IsNullOrEmpty(selection.ItemId) || selection.SourceHandIndex != -1))
		{
			throw new ArgumentException("The hand-limit mandatory selection contains an invalid source.", parameterName);
		}

		if (selection.Kind == MandatorySelectionKind.AttachmentHandCard
			&& (string.IsNullOrWhiteSpace(selection.ItemId)
				|| selection.SourceHandIndex >= player.ItemHand.Length))
		{
			throw new ArgumentException("The attachment mandatory selection contains an invalid item source.", parameterName);
		}

		if (legalIndexes.Any(index => index < 0 || index >= legalIndexLimit))
		{
			throw new ArgumentException("The match snapshot contains an invalid mandatory selection index.", parameterName);
		}

		if (selection.Kind == MandatorySelectionKind.AttachmentHandCard
			&& legalIndexes.Any(index => player.Hand[index].NormalizedAttachedEffectIds.Contains(
				selection.ItemId,
				StringComparer.Ordinal)))
		{
			throw new ArgumentException("The attachment mandatory selection contains an occupied target.", parameterName);
		}

		if (selection.SourceHandIndex >= 0
			&& !string.Equals(
				player.ItemHand[selection.SourceHandIndex].Id,
				selection.ItemId,
				StringComparison.Ordinal))
		{
			throw new ArgumentException("The mandatory selection source item does not match.", parameterName);
		}
	}
}
