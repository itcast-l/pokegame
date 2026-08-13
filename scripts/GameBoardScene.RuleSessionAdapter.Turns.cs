using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
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

}
