using System.Collections.Immutable;

namespace Deckland.Rules;

internal readonly record struct ExtraActionGrantRequest(
	string SourceId,
	bool ResumeNormalTurnAfterChain,
	ExtraActionReentryPolicy ReentryPolicy = ExtraActionReentryPolicy.DisallowSameSource,
	bool OncePerRealTurn = false);

internal readonly record struct ExtraActionGrantResult(
	ExtraActionGrantStatus Status,
	ExtraActionGrantState? Grant,
	long RootChainId,
	int RootEventCount,
	int ExtraActionsExecuted,
	int QueuedGrantCount)
{
	internal bool Granted => Status == ExtraActionGrantStatus.Granted && Grant.HasValue;
	internal bool IsBudgetExhausted => Status is ExtraActionGrantStatus.ExtraActionBudgetExhausted
		or ExtraActionGrantStatus.RootEventBudgetExhausted;
}

internal readonly record struct ExtraActionAcceptResult(
	ExtraActionAcceptStatus Status,
	ExtraActionGrantState? Grant,
	long RootChainId,
	int ExtraActionsExecuted)
{
	internal bool Accepted => Status == ExtraActionAcceptStatus.Accepted && Grant.HasValue;
}

internal readonly record struct ExtraActionCompletionResult(
	ExtraActionCompletionKind Kind,
	long RootChainId,
	int RootEventCount,
	int ExtraActionsExecuted);

internal readonly record struct ExtraActionAbortResult(
	bool HadActiveRootChain,
	bool ResumeNormalTurn,
	long RootChainId,
	int RootEventCount,
	int ExtraActionsExecuted,
	int ClearedGrantCount);

internal sealed class ExtraActionQueueState
{
	internal const int DefaultMaxExtraActionsPerRealTurn = 8;
	internal const int DefaultMaxEventsPerRootChain = 32;

	private readonly int _maxExtraActionsPerRealTurn;
	private readonly int _maxEventsPerRootChain;
	private readonly Queue<ExtraActionGrantState> _grantQueue = new();
	private readonly HashSet<string> _sourcesInRootChain = new(StringComparer.Ordinal);
	private readonly HashSet<string> _pendingRealTurnEndMarkerIds = new(StringComparer.Ordinal);
	private ExtraActionGrantState? _currentGrant;
	private long _nextRootChainId = 1;
	private long _activeRootChainId;
	private bool _rootResumesNormalTurn;
	private bool _rootDerivationStopped;
	private int _rootEventCount;
	private int _rootMaxDepth;

	internal ExtraActionQueueState(
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
	internal ExtraActionGrantState? CurrentGrant => _currentGrant;

	internal void BeginRealTurn()
	{
		ExtraActionsExecutedThisRealTurn = 0;
		ContinueBranchUsedThisRealTurn = false;
	}

	internal ImmutableArray<string> EndRealTurn()
	{
		ImmutableArray<string> markerIds = _pendingRealTurnEndMarkerIds
			.Order(StringComparer.Ordinal)
			.ToImmutableArray();
		_pendingRealTurnEndMarkerIds.Clear();
		ExtraActionsExecutedThisRealTurn = 0;
		ContinueBranchUsedThisRealTurn = false;
		return markerIds;
	}

	internal bool HasPendingRealTurnEndMarker(string markerId)
	{
		return !string.IsNullOrWhiteSpace(markerId)
			&& _pendingRealTurnEndMarkerIds.Contains(markerId.Trim());
	}

	internal bool RegisterRealTurnEndMarker(string markerId)
	{
		return !string.IsNullOrWhiteSpace(markerId)
			&& _pendingRealTurnEndMarkerIds.Add(markerId.Trim());
	}

	internal void Reset()
	{
		ClearRootChain();
		_nextRootChainId = 1;
		_pendingRealTurnEndMarkerIds.Clear();
		BeginRealTurn();
	}

	internal ExtraActionQueueSnapshot CaptureSnapshot()
	{
		return new ExtraActionQueueSnapshot(
			_nextRootChainId,
			_activeRootChainId,
			_rootResumesNormalTurn,
			_rootDerivationStopped,
			_rootEventCount,
			_rootMaxDepth,
			ExtraActionsExecutedThisRealTurn,
			ContinueBranchUsedThisRealTurn,
			_currentGrant,
			_grantQueue.ToImmutableArray(),
			_sourcesInRootChain.Order(StringComparer.Ordinal).ToImmutableArray(),
			_pendingRealTurnEndMarkerIds.Order(StringComparer.Ordinal).ToImmutableArray());
	}

	internal void RestoreSnapshot(ExtraActionQueueSnapshot snapshot)
	{
		_grantQueue.Clear();
		_sourcesInRootChain.Clear();
		_pendingRealTurnEndMarkerIds.Clear();
		_nextRootChainId = snapshot.NextRootChainId;
		_activeRootChainId = snapshot.ActiveRootChainId;
		_rootResumesNormalTurn = snapshot.RootResumesNormalTurn;
		_rootDerivationStopped = snapshot.RootDerivationStopped;
		_rootEventCount = snapshot.RootEventCount;
		_rootMaxDepth = snapshot.RootMaxDepth;
		ExtraActionsExecutedThisRealTurn = snapshot.ExtraActionsExecutedThisRealTurn;
		ContinueBranchUsedThisRealTurn = snapshot.ContinueBranchUsedThisRealTurn;
		_currentGrant = snapshot.CurrentGrant;
		foreach (ExtraActionGrantState grant in snapshot.QueuedGrants)
		{
			_grantQueue.Enqueue(grant);
		}
		foreach (string sourceId in snapshot.SourcesInRootChain)
		{
			_sourcesInRootChain.Add(sourceId);
		}
		foreach (string markerId in snapshot.PendingRealTurnEndMarkerIds)
		{
			_pendingRealTurnEndMarkerIds.Add(markerId);
		}
	}

	internal ExtraActionGrantState? Peek()
	{
		return _grantQueue.Count > 0 ? _grantQueue.Peek() : null;
	}

	internal ExtraActionGrantResult TryGrant(ExtraActionGrantRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.SourceId))
		{
			return BuildGrantResult(ExtraActionGrantStatus.InvalidSource);
		}

		string sourceId = request.SourceId.Trim();
		if (request.OncePerRealTurn && ContinueBranchUsedThisRealTurn)
		{
			return BuildGrantResult(ExtraActionGrantStatus.OncePerRealTurnBlocked);
		}

		if (_rootDerivationStopped)
		{
			return BuildGrantResult(ExtraActionGrantStatus.DerivationStopped);
		}

		if (_activeRootChainId > 0
			&& request.ReentryPolicy == ExtraActionReentryPolicy.DisallowSameSource
			&& _sourcesInRootChain.Contains(sourceId))
		{
			return BuildGrantResult(ExtraActionGrantStatus.ReentryBlocked);
		}

		if (ExtraActionsExecutedThisRealTurn + _grantQueue.Count >= _maxExtraActionsPerRealTurn)
		{
			ExtraActionGrantResult result = BuildGrantResult(
				ExtraActionGrantStatus.ExtraActionBudgetExhausted);
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
			ExtraActionGrantResult result = BuildGrantResult(
				ExtraActionGrantStatus.RootEventBudgetExhausted);
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
		var grant = new ExtraActionGrantState(
			sourceId,
			_activeRootChainId,
			inheritedResumeNormalTurn,
			request.ReentryPolicy,
			chainDepth);
		_grantQueue.Enqueue(grant);
		_sourcesInRootChain.Add(sourceId);
		if (request.OncePerRealTurn)
		{
			ContinueBranchUsedThisRealTurn = true;
		}

		return BuildGrantResult(ExtraActionGrantStatus.Granted, grant);
	}

	internal ExtraActionAcceptResult TryAccept(ExtraActionGrantState? expectedGrant)
	{
		if (!expectedGrant.HasValue)
		{
			return BuildAcceptResult(ExtraActionAcceptStatus.NoGrant);
		}

		if (_currentGrant.HasValue)
		{
			return BuildAcceptResult(ExtraActionAcceptStatus.ActionInProgress);
		}

		if (_grantQueue.Count == 0 || _grantQueue.Peek() != expectedGrant.Value)
		{
			return BuildAcceptResult(ExtraActionAcceptStatus.QueueMismatch);
		}

		if (ExtraActionsExecutedThisRealTurn >= _maxExtraActionsPerRealTurn)
		{
			ExtraActionAcceptResult result = BuildAcceptResult(
				ExtraActionAcceptStatus.ExtraActionBudgetExhausted);
			StopDerivation();
			return result;
		}

		ExtraActionGrantState acceptedGrant = _grantQueue.Dequeue();
		_currentGrant = acceptedGrant;
		ExtraActionsExecutedThisRealTurn++;
		return BuildAcceptResult(ExtraActionAcceptStatus.Accepted, acceptedGrant);
	}

	internal void FinishActionExecution(ExtraActionGrantState? acceptedGrant)
	{
		if (acceptedGrant.HasValue && _currentGrant == acceptedGrant)
		{
			_currentGrant = null;
		}
	}

	internal ExtraActionCompletionResult Complete(ExtraActionGrantState consumedGrant)
	{
		long rootChainId = _activeRootChainId > 0
			? _activeRootChainId
			: consumedGrant.RootChainId;
		int eventCount = _rootEventCount;
		int executedCount = ExtraActionsExecutedThisRealTurn;
		if (_grantQueue.Count > 0)
		{
			return new ExtraActionCompletionResult(
				ExtraActionCompletionKind.ContinueChain,
				rootChainId,
				eventCount,
				executedCount);
		}

		bool resumeNormalTurn = _rootResumesNormalTurn
			|| consumedGrant.ResumeNormalTurnAfterChain;
		ClearRootChain();
		return new ExtraActionCompletionResult(
			resumeNormalTurn
				? ExtraActionCompletionKind.ResumeNormalTurn
				: ExtraActionCompletionKind.FollowActionResult,
			rootChainId,
			eventCount,
			executedCount);
	}

	internal ExtraActionAbortResult Abort()
	{
		if (_activeRootChainId <= 0)
		{
			return default;
		}

		var result = new ExtraActionAbortResult(
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

	private ExtraActionGrantResult BuildGrantResult(
		ExtraActionGrantStatus status,
		ExtraActionGrantState? grant = null)
	{
		return new ExtraActionGrantResult(
			status,
			grant,
			_activeRootChainId,
			_rootEventCount,
			ExtraActionsExecutedThisRealTurn,
			_grantQueue.Count);
	}

	private ExtraActionAcceptResult BuildAcceptResult(
		ExtraActionAcceptStatus status,
		ExtraActionGrantState? grant = null)
	{
		return new ExtraActionAcceptResult(
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
