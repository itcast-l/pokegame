using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
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
		if (command.AdvanceQuestionRefresh
			&& command.HasQuestionCard
			&& command.QuestionCardLifetimeTurns <= 0)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.InvalidCommand,
					command.QuestionCardLifetimeTurns.ToString()),
				ReadModel);
		}

		int previousSeat = _state.CurrentTurnPlayerIndex;
		ClearPendingBasePlayCapture(_state);
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
		bool shouldExpireQuestionCard = false;
		if (command.AdvanceQuestionRefresh)
		{
			QuestionCardRefreshAdvanceResult refreshResult =
				_turnAdvancementService.AdvanceQuestionCardRefresh(
					_state,
					_profile.Capabilities.Contains(MatchCapability.ItemRules),
					command.HasTableSpecialCard,
					command.HasQuestionCard,
					nextSeat,
					command.QuestionRefreshCyclesAfterCapture,
					command.QuestionCardLifetimeTurns);
			shouldRefreshQuestionCard = refreshResult.ShouldRefreshQuestionCard;
			shouldExpireQuestionCard = refreshResult.ShouldExpireQuestionCard;
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
				ShouldExpireQuestionCard = shouldExpireQuestionCard,
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
		ClearPendingBasePlayCapture(_state);
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
			ClearPendingBasePlayCapture(_state);
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


	private RuleCommandResult ApplyEndSession(
		EndMatchSessionCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		_state.MandatorySelection = MandatorySelectionState.None;
		_state.PendingAttachmentRewards.Clear();
		ClearPendingBasePlayCapture(_state);
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

}
