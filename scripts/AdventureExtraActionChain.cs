using System;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private const string AdventureBaseCardActionSource = "base_card";
	private const string AdventureContinueBranchUpgradeId = "clubs_continue_branch";
	private const string AdventureInterceptBeatUpgradeId = "spades_intercept_beat";
	private const string AdventureMissBandageRealTurnEndMarkerId =
		"adventure.miss_bandage";

	private bool HasPendingAdventureExtraBaseCardAction()
	{
		return GetExtraActionReadModel().HasPendingGrant;
	}

	private ExtraActionGrantState? PeekAdventureExtraActionGrantForAction(int playerIndex)
	{
		if (playerIndex != MainPlayerIndex)
		{
			return null;
		}

		ExtraActionQueueSnapshot extraActions = GetExtraActionReadModel();
		return extraActions.QueuedGrants.IsDefaultOrEmpty
			? null
			: extraActions.QueuedGrants[0];
	}

	private bool TryGrantAdventureMainPlayerExtraBaseCardAction(
		string sourceUpgradeId,
		bool resumeNormalTurnAfterChain = false,
		ExtraActionReentryPolicy reentryPolicy = ExtraActionReentryPolicy.DisallowSameSource)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| _playerHands.Count <= MainPlayerIndex
			|| _playerHands[MainPlayerIndex].Count == 0)
		{
			return false;
		}

		bool oncePerRealTurn = string.Equals(
			sourceUpgradeId,
			AdventureContinueBranchUpgradeId,
			StringComparison.Ordinal);
		if (!TrySubmitGrantExtraActionRuleCommand(
			MainPlayerIndex,
			sourceUpgradeId,
			resumeNormalTurnAfterChain,
			reentryPolicy,
			oncePerRealTurn,
			_turnSequenceGeneration,
			out ExtraActionCommandOutcome result))
		{
			return false;
		}
		if (result.GrantStatus is ExtraActionGrantStatus.ExtraActionBudgetExhausted
			or ExtraActionGrantStatus.RootEventBudgetExhausted)
		{
			WarnAdventureExtraActionLimit(result, sourceUpgradeId);
		}

		return result.GrantStatus == ExtraActionGrantStatus.Granted
			&& result.Grant.HasValue;
	}

	private ExtraActionGrantState? AcceptAdventureExtraActionGrantForAction(
		int playerIndex,
		ExtraActionGrantState? expectedGrant)
	{
		if (playerIndex != MainPlayerIndex || !expectedGrant.HasValue)
		{
			return null;
		}

		if (!TrySubmitAcceptExtraActionRuleCommand(
			playerIndex,
			expectedGrant,
			_turnSequenceGeneration,
			out ExtraActionCommandOutcome result))
		{
			return null;
		}
		if (result.AcceptStatus == ExtraActionAcceptStatus.ExtraActionBudgetExhausted)
		{
			GD.PushWarning(
				$"adventure_extra_action_chain_limit reason=extra_action_accept_budget_exhausted "
				+ $"root_chain_id={result.RootChainId} "
				+ $"extra_actions={result.ExtraActionsExecuted}");
		}

		return result.AcceptStatus == ExtraActionAcceptStatus.Accepted
			? result.Grant
			: null;
	}

	private BaseCardActionContext CreateBaseCardActionContext(
		PlayingCard playedCard,
		ExtraActionGrantState? acceptedGrant)
	{
		long actionId = _adventureNextBaseCardActionId;
		_adventureNextBaseCardActionId = actionId == long.MaxValue ? 1 : actionId + 1;
		return new BaseCardActionContext(
			actionId,
			acceptedGrant?.RootChainId ?? 0,
			acceptedGrant?.SourceId ?? AdventureBaseCardActionSource,
			acceptedGrant.HasValue,
			playedCard.InstanceId,
			playedCard,
			Array.Empty<string>());
	}

	private void CompleteAdventureExtraActionGrantAction(ExtraActionGrantState? acceptedGrant)
	{
		if (!acceptedGrant.HasValue)
		{
			return;
		}

		TrySubmitFinishExtraActionExecutionRuleCommand(
			MainPlayerIndex,
			acceptedGrant,
			_turnSequenceGeneration);
	}

	private ExtraActionCommandOutcome CompleteConsumedAdventureExtraActionGrant(
		ExtraActionGrantState consumedGrant)
	{
		return TrySubmitCompleteExtraActionRuleCommand(
			MainPlayerIndex,
			consumedGrant,
			_turnSequenceGeneration,
			out ExtraActionCommandOutcome outcome)
			? outcome
			: new ExtraActionCommandOutcome(
				CompletionKind: ExtraActionCompletionKind.FollowActionResult);
	}

	private bool AbortAdventureExtraActionRootChain(string reason)
	{
		if (!TrySubmitAbortExtraActionChainRuleCommand(
			MainPlayerIndex,
			_turnSequenceGeneration,
			out ExtraActionCommandOutcome result)
			|| !result.HadActiveRootChain)
		{
			return false;
		}

		GD.PushWarning(
			$"adventure_extra_action_chain_aborted reason={reason} "
			+ $"root_chain_id={result.RootChainId} "
			+ $"event_count={result.RootEventCount} "
			+ $"extra_actions={result.ExtraActionsExecuted} "
			+ $"cleared_grants={result.ClearedGrantCount} "
			+ $"resume_normal_turn={result.ResumeNormalTurn}");
		return result.ResumeNormalTurn;
	}

	private async Task SettleAdventureExtraActionChainAbortAsync(
		bool resumeNormalTurn,
		int turnSequenceGeneration)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		if (resumeNormalTurn)
		{
			if (_currentTurnPlayerIndex != MainPlayerIndex)
			{
					if (!await SetCurrentTurnPlayerIndexAsync(MainPlayerIndex))
					{
						return;
					}
			}

			await BeginRealMainPlayerTurnIfNeededAsync(turnSequenceGeneration);
			return;
		}

		if (_currentTurnPlayerIndex != MainPlayerIndex)
		{
			return;
		}

		await ResolveActionCompletionAsync(
			MainPlayerIndex,
			new PlayCardResult(false, false, CompletesTurn: true));
		if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
		{
			await ResolveOpponentTurnsAsync(turnSequenceGeneration);
		}
	}

	private void ClearAdventureInteractionStateForBattleEnd()
	{
		TryUpdateMatchFlowState(state => state with
		{
			MainPlayerRealTurnActive = false,
			MainPlayerRealTurnStartPending = false,
		});
	}

	private void ResetAdventureInteractionStateForNewBoard()
	{
		TryUpdateMatchFlowState(state => state with
		{
			MainPlayerRealTurnActive = true,
			MainPlayerRealTurnStartPending = false,
		});
		_adventureNextBaseCardActionId = 1;
	}

	private static void WarnAdventureExtraActionLimit(
		ExtraActionCommandOutcome result,
		string sourceUpgradeId)
	{
		GD.PushWarning(
			$"adventure_extra_action_chain_limit reason={result.GrantStatus} "
			+ $"source_upgrade_id={sourceUpgradeId} "
			+ $"root_chain_id={result.RootChainId} "
			+ $"event_count={result.RootEventCount} "
			+ $"extra_actions={result.ExtraActionsExecuted} "
			+ $"queued_grants={result.QueuedGrantCount}");
	}

	private ExtraActionQueueSnapshot GetExtraActionReadModel()
	{
		return _authorityRuleSession?.ReadModel.NormalizedExtraActions
			?? _replicaRuleSession?.ReadModel.NormalizedExtraActions
			?? ExtraActionQueueSnapshot.Empty;
	}
}
