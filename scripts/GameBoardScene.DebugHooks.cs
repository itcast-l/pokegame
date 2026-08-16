using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	// These small state hooks are shared by production paths so Release can omit
	// the large smoke-test entrypoint file without changing those paths.
	private bool _debugSpecialCardProbeRunning;
	private Tween? _debugSpecialCardProbeTween;
	private Control? _debugSpecialCardProbeEffectRoot;
	private double _debugSpecialCardProbeElapsedSeconds;
	private bool _debugForceNextPendingHandLimitDiscardHeuristicFailure;
	private bool _debugForceNextAdventureUpgradeOverlayDisplayFailure;
	private bool _debugForceNextAdventureUpgradeReplacementOverlayDisplayFailure;
	private double _debugNetworkVisualPlaybackDelaySeconds;
	private int _debugNetworkVisualPlaybackFailuresToInject;
	private int _debugQuestionRewardGrantCount;
	private string _debugLastQuestionRewardItemCardId = string.Empty;
	private readonly Queue<string> _debugForcedQuestionRewardItemCardIds = new();

	private bool ConsumeDebugForceNextPendingHandLimitDiscardHeuristicFailure()
	{
		if (!_debugForceNextPendingHandLimitDiscardHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingHandLimitDiscardHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure()
	{
		if (!_debugForceNextAdventureUpgradeOverlayDisplayFailure)
		{
			return false;
		}

		_debugForceNextAdventureUpgradeOverlayDisplayFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextAdventureUpgradeReplacementOverlayDisplayFailure()
	{
		if (!_debugForceNextAdventureUpgradeReplacementOverlayDisplayFailure)
		{
			return false;
		}

		_debugForceNextAdventureUpgradeReplacementOverlayDisplayFailure = false;
		return true;
	}

	private double GetDebugNetworkVisualPlaybackDelaySeconds()
	{
		return _debugNetworkVisualPlaybackDelaySeconds;
	}

	private bool ConsumeDebugNetworkVisualPlaybackFailure()
	{
		if (_debugNetworkVisualPlaybackFailuresToInject <= 0)
		{
			return false;
		}

		_debugNetworkVisualPlaybackFailuresToInject--;
		return true;
	}

	private string ConsumeDebugForcedNextQuestionRewardItemCardId()
	{
		return _debugForcedQuestionRewardItemCardIds.Count > 0
			? _debugForcedQuestionRewardItemCardIds.Dequeue()
			: string.Empty;
	}

	private TaskCompletionSource<bool>? _debugCaptureBeforeApplyBarrier = null;
	private TaskCompletionSource<bool>? _debugCaptureAfterApplyBarrier = null;
	private bool _debugCaptureBeforeApplyBarrierEntered;
	private bool _debugCaptureAfterApplyBarrierEntered;
	private bool _debugCaptureResultObservationArmed;
	private bool _debugCaptureResultObserved;
	private bool _debugCaptureResultApplied;
	private bool _debugCaptureResultAborted;

	private Task WaitForDebugCaptureBeforeApplyBarrierAsync()
	{
		TaskCompletionSource<bool>? completionSource = _debugCaptureBeforeApplyBarrier;
		if (completionSource == null)
		{
			return Task.CompletedTask;
		}

		_debugCaptureBeforeApplyBarrierEntered = true;
		return completionSource.Task;
	}

	private Task WaitForDebugCaptureAfterApplyBarrierAsync()
	{
		TaskCompletionSource<bool>? completionSource = _debugCaptureAfterApplyBarrier;
		if (completionSource == null)
		{
			return Task.CompletedTask;
		}

		_debugCaptureAfterApplyBarrierEntered = true;
		return completionSource.Task;
	}

	private void RecordDebugCaptureOwnershipResult(CaptureTableCardsResult result)
	{
		if (!_debugCaptureResultObservationArmed)
		{
			return;
		}

		_debugCaptureResultObservationArmed = false;
		_debugCaptureResultObserved = true;
		_debugCaptureResultApplied = result.Applied;
		_debugCaptureResultAborted = result.Aborted;
	}
}
