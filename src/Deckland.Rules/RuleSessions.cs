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
		if (_preparedAdventureTransaction != null)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.AdventureTransactionPending,
					"An adventure transaction is awaiting persistence."),
				before);
		}
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
