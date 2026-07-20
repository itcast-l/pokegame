using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#if !RULES_TESTS
using Godot;
#endif

internal readonly record struct AdventureExtraActionGrantRequest(
	string SourceUpgradeId,
	bool ResumeNormalTurnAfterChain,
	AdventureExtraActionReentryPolicy ReentryPolicy = AdventureExtraActionReentryPolicy.DisallowSameSource,
	bool OncePerRealTurn = false);

internal enum AdventureExtraActionGrantStatus
{
	Granted,
	InvalidSource,
	OncePerRealTurnBlocked,
	ReentryBlocked,
	DerivationStopped,
	ExtraActionBudgetExhausted,
	RootEventBudgetExhausted,
}

internal readonly record struct AdventureExtraActionGrantResult(
	AdventureExtraActionGrantStatus Status,
	AdventureExtraActionGrant? Grant,
	long RootChainId,
	int RootEventCount,
	int ExtraActionsExecuted,
	int QueuedGrantCount)
{
	internal bool Granted => Status == AdventureExtraActionGrantStatus.Granted && Grant.HasValue;
	internal bool IsBudgetExhausted => Status is AdventureExtraActionGrantStatus.ExtraActionBudgetExhausted
		or AdventureExtraActionGrantStatus.RootEventBudgetExhausted;
}

internal enum AdventureExtraActionAcceptStatus
{
	NoGrant,
	Accepted,
	ActionInProgress,
	QueueMismatch,
	ExtraActionBudgetExhausted,
}

internal readonly record struct AdventureExtraActionAcceptResult(
	AdventureExtraActionAcceptStatus Status,
	AdventureExtraActionGrant? Grant,
	long RootChainId,
	int ExtraActionsExecuted)
{
	internal bool Accepted => Status == AdventureExtraActionAcceptStatus.Accepted && Grant.HasValue;
}

internal enum AdventureExtraActionCompletionKind
{
	FollowActionResult,
	ContinueChain,
	ResumeNormalTurn,
}

internal readonly record struct AdventureExtraActionCompletionResult(
	AdventureExtraActionCompletionKind Kind,
	long RootChainId,
	int RootEventCount,
	int ExtraActionsExecuted);

internal readonly record struct AdventureExtraActionAbortResult(
	bool HadActiveRootChain,
	bool ResumeNormalTurn,
	long RootChainId,
	int RootEventCount,
	int ExtraActionsExecuted,
	int ClearedGrantCount);

internal sealed class AdventureExtraActionQueueState
{
	internal const int DefaultMaxExtraActionsPerRealTurn = 8;
	internal const int DefaultMaxEventsPerRootChain = 32;

	private readonly int _maxExtraActionsPerRealTurn;
	private readonly int _maxEventsPerRootChain;
	private readonly Queue<AdventureExtraActionGrant> _grantQueue = new();
	private readonly HashSet<string> _sourcesInRootChain = new(StringComparer.Ordinal);
	private AdventureExtraActionGrant? _currentGrant;
	private long _nextRootChainId = 1;
	private long _activeRootChainId;
	private bool _rootResumesNormalTurn;
	private bool _rootDerivationStopped;
	private int _rootEventCount;
	private int _rootMaxDepth;

	internal AdventureExtraActionQueueState(
		int maxExtraActionsPerRealTurn = DefaultMaxExtraActionsPerRealTurn,
		int maxEventsPerRootChain = DefaultMaxEventsPerRootChain)
	{
		_maxExtraActionsPerRealTurn = Math.Max(1, maxExtraActionsPerRealTurn);
		_maxEventsPerRootChain = Math.Max(1, maxEventsPerRootChain);
	}

	internal bool HasPendingGrant => _grantQueue.Count > 0;
	internal int QueuedGrantCount => _grantQueue.Count;
	internal int ExtraActionsExecutedThisRealTurn { get; private set; }
	internal bool ContinueBranchUsedThisRealTurn { get; private set; }
	internal long ActiveRootChainId => _activeRootChainId;
	internal int RootEventCount => _rootEventCount;
	internal int RootMaxDepth => _rootMaxDepth;
	internal bool RootResumesNormalTurn => _rootResumesNormalTurn;
	internal AdventureExtraActionGrant? CurrentGrant => _currentGrant;
	internal bool PendingMissBandageRegistration { get; private set; }

	internal void BeginRealTurn()
	{
		ExtraActionsExecutedThisRealTurn = 0;
		ContinueBranchUsedThisRealTurn = false;
	}

	internal bool EndRealTurn()
	{
		bool commitMissBandage = PendingMissBandageRegistration;
		PendingMissBandageRegistration = false;
		ExtraActionsExecutedThisRealTurn = 0;
		ContinueBranchUsedThisRealTurn = false;
		return commitMissBandage;
	}

	internal void RegisterMissBandageForRealTurnEnd()
	{
		PendingMissBandageRegistration = true;
	}

	internal void Reset()
	{
		ClearRootChain();
		_nextRootChainId = 1;
		PendingMissBandageRegistration = false;
		BeginRealTurn();
	}

	internal AdventureExtraActionGrant? Peek()
	{
		return _grantQueue.Count > 0 ? _grantQueue.Peek() : null;
	}

	internal AdventureExtraActionGrantResult TryGrant(AdventureExtraActionGrantRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.SourceUpgradeId))
		{
			return BuildGrantResult(AdventureExtraActionGrantStatus.InvalidSource);
		}

		string sourceUpgradeId = request.SourceUpgradeId.Trim();
		if (request.OncePerRealTurn && ContinueBranchUsedThisRealTurn)
		{
			return BuildGrantResult(AdventureExtraActionGrantStatus.OncePerRealTurnBlocked);
		}

		if (_rootDerivationStopped)
		{
			return BuildGrantResult(AdventureExtraActionGrantStatus.DerivationStopped);
		}

		if (_activeRootChainId > 0
			&& request.ReentryPolicy == AdventureExtraActionReentryPolicy.DisallowSameSource
			&& _sourcesInRootChain.Contains(sourceUpgradeId))
		{
			return BuildGrantResult(AdventureExtraActionGrantStatus.ReentryBlocked);
		}

		if (ExtraActionsExecutedThisRealTurn + _grantQueue.Count >= _maxExtraActionsPerRealTurn)
		{
			AdventureExtraActionGrantResult result = BuildGrantResult(
				AdventureExtraActionGrantStatus.ExtraActionBudgetExhausted);
			StopDerivation();
			return result;
		}

		if (_activeRootChainId <= 0)
		{
			BeginRootChain(request.ResumeNormalTurnAfterChain);
		}
		else if (request.ResumeNormalTurnAfterChain && !_rootResumesNormalTurn)
		{
			_rootResumesNormalTurn = true;
		}

		if (_rootEventCount >= _maxEventsPerRootChain)
		{
			AdventureExtraActionGrantResult result = BuildGrantResult(
				AdventureExtraActionGrantStatus.RootEventBudgetExhausted);
			StopDerivation();
			return result;
		}
		_rootEventCount++;

		int chainDepth = _currentGrant.HasValue
			? _currentGrant.Value.ChainDepth + 1
			: _rootMaxDepth;
		_rootMaxDepth = Math.Max(_rootMaxDepth, chainDepth + 1);
		bool inheritedResumeNormalTurn = _rootResumesNormalTurn
			|| request.ResumeNormalTurnAfterChain
			|| (_currentGrant?.ResumeNormalTurnAfterChain ?? false);
		var grant = new AdventureExtraActionGrant(
			sourceUpgradeId,
			_activeRootChainId,
			inheritedResumeNormalTurn,
			request.ReentryPolicy,
			chainDepth);
		_grantQueue.Enqueue(grant);
		_sourcesInRootChain.Add(sourceUpgradeId);
		if (request.OncePerRealTurn)
		{
			ContinueBranchUsedThisRealTurn = true;
		}

		return BuildGrantResult(AdventureExtraActionGrantStatus.Granted, grant);
	}

	internal AdventureExtraActionAcceptResult TryAccept(AdventureExtraActionGrant? expectedGrant)
	{
		if (!expectedGrant.HasValue)
		{
			return BuildAcceptResult(AdventureExtraActionAcceptStatus.NoGrant);
		}

		if (_currentGrant.HasValue)
		{
			return BuildAcceptResult(AdventureExtraActionAcceptStatus.ActionInProgress);
		}

		if (_grantQueue.Count == 0 || _grantQueue.Peek() != expectedGrant.Value)
		{
			return BuildAcceptResult(AdventureExtraActionAcceptStatus.QueueMismatch);
		}

		if (ExtraActionsExecutedThisRealTurn >= _maxExtraActionsPerRealTurn)
		{
			AdventureExtraActionAcceptResult result = BuildAcceptResult(
				AdventureExtraActionAcceptStatus.ExtraActionBudgetExhausted);
			StopDerivation();
			return result;
		}

		AdventureExtraActionGrant acceptedGrant = _grantQueue.Dequeue();
		_currentGrant = acceptedGrant;
		ExtraActionsExecutedThisRealTurn++;
		return BuildAcceptResult(
			AdventureExtraActionAcceptStatus.Accepted,
			acceptedGrant);
	}

	internal void FinishActionExecution(AdventureExtraActionGrant? acceptedGrant)
	{
		if (acceptedGrant.HasValue && _currentGrant == acceptedGrant)
		{
			_currentGrant = null;
		}
	}

	internal AdventureExtraActionCompletionResult Complete(AdventureExtraActionGrant consumedGrant)
	{
		long rootChainId = _activeRootChainId > 0
			? _activeRootChainId
			: consumedGrant.RootChainId;
		int eventCount = _rootEventCount;
		int executedCount = ExtraActionsExecutedThisRealTurn;
		if (_grantQueue.Count > 0)
		{
			return new AdventureExtraActionCompletionResult(
				AdventureExtraActionCompletionKind.ContinueChain,
				rootChainId,
				eventCount,
				executedCount);
		}

		bool resumeNormalTurn = _rootResumesNormalTurn
			|| consumedGrant.ResumeNormalTurnAfterChain;
		ClearRootChain();
		return new AdventureExtraActionCompletionResult(
			resumeNormalTurn
				? AdventureExtraActionCompletionKind.ResumeNormalTurn
				: AdventureExtraActionCompletionKind.FollowActionResult,
			rootChainId,
			eventCount,
			executedCount);
	}

	internal AdventureExtraActionAbortResult Abort()
	{
		if (_activeRootChainId <= 0)
		{
			return default;
		}

		var result = new AdventureExtraActionAbortResult(
			HadActiveRootChain: true,
			ResumeNormalTurn: _rootResumesNormalTurn
				|| (_currentGrant?.ResumeNormalTurnAfterChain ?? false),
			RootChainId: _activeRootChainId,
			RootEventCount: _rootEventCount,
			ExtraActionsExecuted: ExtraActionsExecutedThisRealTurn,
			ClearedGrantCount: _grantQueue.Count);
		ClearRootChain();
		return result;
	}

	private AdventureExtraActionGrantResult BuildGrantResult(
		AdventureExtraActionGrantStatus status,
		AdventureExtraActionGrant? grant = null)
	{
		return new AdventureExtraActionGrantResult(
			status,
			grant,
			_activeRootChainId,
			_rootEventCount,
			ExtraActionsExecutedThisRealTurn,
			_grantQueue.Count);
	}

	private AdventureExtraActionAcceptResult BuildAcceptResult(
		AdventureExtraActionAcceptStatus status,
		AdventureExtraActionGrant? grant = null)
	{
		return new AdventureExtraActionAcceptResult(
			status,
			grant,
			_activeRootChainId,
			ExtraActionsExecutedThisRealTurn);
	}

	private void BeginRootChain(bool resumeNormalTurnAfterChain)
	{
		long rootChainId = _nextRootChainId;
		_nextRootChainId = rootChainId == long.MaxValue ? 1 : rootChainId + 1;
		_activeRootChainId = rootChainId;
		_rootResumesNormalTurn = resumeNormalTurnAfterChain;
		_rootDerivationStopped = false;
		_rootEventCount = 0;
		_rootMaxDepth = 0;
		_sourcesInRootChain.Clear();
	}

	private void StopDerivation()
	{
		_rootDerivationStopped = true;
		_grantQueue.Clear();
		if (!_currentGrant.HasValue)
		{
			ClearRootChain();
		}
	}

	private void ClearRootChain()
	{
		_grantQueue.Clear();
		_sourcesInRootChain.Clear();
		_currentGrant = null;
		_activeRootChainId = 0;
		_rootResumesNormalTurn = false;
		_rootDerivationStopped = false;
		_rootEventCount = 0;
		_rootMaxDepth = 0;
	}
}

#if !RULES_TESTS
public partial class GameBoardScene
{
	private const string AdventureBaseCardActionSource = "base_card";
	private const string AdventureContinueBranchUpgradeId = "clubs_continue_branch";
	private const string AdventureInterceptBeatUpgradeId = "spades_intercept_beat";

	private bool HasPendingAdventureExtraBaseCardAction()
	{
		return _state.AdventureExtraActionQueue.HasPendingGrant;
	}

	private AdventureExtraActionGrant? PeekAdventureExtraActionGrantForAction(int playerIndex)
	{
		return playerIndex == MainPlayerIndex
			? _state.AdventureExtraActionQueue.Peek()
			: null;
	}

	private bool TryGrantAdventureMainPlayerExtraBaseCardAction(
		string sourceUpgradeId,
		bool resumeNormalTurnAfterChain = false,
		AdventureExtraActionReentryPolicy reentryPolicy = AdventureExtraActionReentryPolicy.DisallowSameSource)
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
		AdventureExtraActionGrantResult result = _state.AdventureExtraActionQueue.TryGrant(
			new AdventureExtraActionGrantRequest(
				sourceUpgradeId,
				resumeNormalTurnAfterChain,
				reentryPolicy,
				oncePerRealTurn));
		if (result.IsBudgetExhausted)
		{
			WarnAdventureExtraActionLimit(result, sourceUpgradeId);
		}

		return result.Granted;
	}

	private AdventureExtraActionGrant? AcceptAdventureExtraActionGrantForAction(
		int playerIndex,
		AdventureExtraActionGrant? expectedGrant)
	{
		if (playerIndex != MainPlayerIndex)
		{
			return null;
		}

		AdventureExtraActionAcceptResult result =
			_state.AdventureExtraActionQueue.TryAccept(expectedGrant);
		if (result.Status == AdventureExtraActionAcceptStatus.ExtraActionBudgetExhausted)
		{
			GD.PushWarning(
				$"adventure_extra_action_chain_limit reason=extra_action_accept_budget_exhausted "
				+ $"root_chain_id={result.RootChainId} "
				+ $"extra_actions={result.ExtraActionsExecuted}");
		}

		return result.Accepted ? result.Grant : null;
	}

	private BaseCardActionContext CreateBaseCardActionContext(
		PlayingCard playedCard,
		AdventureExtraActionGrant? acceptedGrant)
	{
		long actionId = _state.AdventureNextBaseCardActionId;
		_state.AdventureNextBaseCardActionId = actionId == long.MaxValue ? 1 : actionId + 1;
		return new BaseCardActionContext(
			actionId,
			acceptedGrant?.RootChainId ?? 0,
			acceptedGrant?.SourceUpgradeId ?? AdventureBaseCardActionSource,
			acceptedGrant.HasValue,
			playedCard.InstanceId,
			playedCard,
			Array.Empty<string>());
	}

	private void CompleteAdventureExtraActionGrantAction(AdventureExtraActionGrant? acceptedGrant)
	{
		_state.AdventureExtraActionQueue.FinishActionExecution(acceptedGrant);
	}

	private AdventureExtraActionCompletionResult CompleteConsumedAdventureExtraActionGrant(
		AdventureExtraActionGrant consumedGrant)
	{
		return _state.AdventureExtraActionQueue.Complete(consumedGrant);
	}

	private bool AbortAdventureExtraActionRootChain(string reason)
	{
		AdventureExtraActionAbortResult result = _state.AdventureExtraActionQueue.Abort();
		if (!result.HadActiveRootChain)
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
		_state.AdventureExtraActionQueue.Reset();
		_state.AdventureRealMainPlayerTurnActive = false;
		_state.AdventureRealMainPlayerTurnStartPending = false;
	}

	private void ResetAdventureInteractionStateForNewBoard()
	{
		_state.AdventureExtraActionQueue.Reset();
		_state.AdventureRealMainPlayerTurnActive = true;
		_state.AdventureRealMainPlayerTurnStartPending = false;
		_state.AdventureNextBaseCardActionId = 1;
	}

	private static void WarnAdventureExtraActionLimit(
		AdventureExtraActionGrantResult result,
		string sourceUpgradeId)
	{
		GD.PushWarning(
			$"adventure_extra_action_chain_limit reason={result.Status} "
			+ $"source_upgrade_id={sourceUpgradeId} "
			+ $"root_chain_id={result.RootChainId} "
			+ $"event_count={result.RootEventCount} "
			+ $"extra_actions={result.ExtraActionsExecuted} "
			+ $"queued_grants={result.QueuedGrantCount}");
	}
}
#endif
