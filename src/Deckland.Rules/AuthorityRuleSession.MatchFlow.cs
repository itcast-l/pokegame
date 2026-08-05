using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	private RuleCommandResult ApplyReplaceMatchFlowState(
		ReplaceMatchFlowStateCommand command,
		ImmutableArray<PresentationIntent>.Builder intents)
	{
		if (!MatchFlowStateValidation.IsValid(command.State)
			|| command.State.PendingWinnerSeat >= _state.PlayerCount
			|| command.State.BlockedAutomatedItemSeat >= _state.PlayerCount)
		{
			return RuleCommandResult.Rejected(
				new RuleRejection(
					RuleRejectionCode.InvalidConfiguration,
					"The match flow state is invalid."),
				ReadModel);
		}

		if (Equals(_state.MatchFlow, command.State))
		{
			return Accepted(intents);
		}

		_state.MatchFlow = command.State;
		_state.RuleRevision++;
		return Accepted(intents);
	}
}

internal static class MatchFlowStateValidation
{
	internal static bool IsValid(MatchFlowState? state)
	{
		return state != null
			&& Enum.IsDefined(state.PendingCardPlacement)
			&& state.SuccessfulCapturesThisTurn >= 0
			&& state.MainPlayerCapturesThisBattle >= 0
			&& !(state.MainPlayerRealTurnActive && state.MainPlayerRealTurnStartPending)
			&& state.PendingPlacementAnchorRuntimeId >= 0
			&& state.CaptureMarkRuntimeId >= 0
			&& state.TurnCaptureMarkRuntimeId >= 0
			&& state.SequentialRevealAnchorRuntimeId >= 0
			&& state.DisabledMainPlayerHandDisplayIndex >= -1
			&& state.CaptureThresholdProgress >= 0
			&& state.PeriodicItemProgress >= 0
			&& state.PeriodicItemActivationsThisMatch >= 0
			&& state.DoubleEffectProgress >= 0
			&& state.LimitedDoubleEffectActivationsThisMatch >= 0
			&& state.DestructiveEffectCooldownTurns >= 0
			&& state.MissThresholdProgress >= 0
			&& state.StealThresholdProgress >= 0
			&& state.LimitedStealThresholdProgress >= 0
			&& state.LimitedStealActivationsThisMatch >= 0
			&& state.ScriptedSequenceStep >= 0
			&& state.ScriptedTourIndex >= 0
			&& state.PendingWinnerSeat is >= -1 and < 4
			&& state.BlockedAutomatedItemSeat is >= -1 and < 4
			&& state.BlockedAutomatedItemId != null
			&& state.BlockedAutomatedItemId.Length <= 128
			&& (state.BlockedAutomatedItemSeat >= 0)
				== !string.IsNullOrWhiteSpace(state.BlockedAutomatedItemId)
			&& state.NormalizedPendingProgressionChoices.All(choice =>
				choice != PendingProgressionChoiceKind.None
				&& Enum.IsDefined(choice))
			&& state.NormalizedPendingProgressionChoices.Distinct().Count()
				== state.NormalizedPendingProgressionChoices.Length;
	}
}
