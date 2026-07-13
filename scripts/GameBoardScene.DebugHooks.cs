using System;
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
	private bool _debugForceNextPendingTargetPlayerHeuristicFailure;
	private bool _debugForceNextPendingTargetHandHeuristicFailure;
	private bool _debugForceNextPendingHandLimitDiscardHeuristicFailure;
	private bool _debugForceNextAdventureUpgradeOverlayDisplayFailure;
	private double _debugNetworkVisualPlaybackDelaySeconds;
	private int _debugQuestionRewardGrantCount;
	private string _debugLastQuestionRewardItemCardId = string.Empty;
	private string _debugForcedNextQuestionRewardItemCardId = string.Empty;

	private bool ConsumeDebugForceNextPendingTargetPlayerHeuristicFailure()
	{
		if (!_debugForceNextPendingTargetPlayerHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingTargetPlayerHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextPendingTargetHandHeuristicFailure()
	{
		if (!_debugForceNextPendingTargetHandHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingTargetHandHeuristicFailure = false;
		return true;
	}

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

	private double GetDebugNetworkVisualPlaybackDelaySeconds()
	{
		return _debugNetworkVisualPlaybackDelaySeconds;
	}

	private string ConsumeDebugForcedNextQuestionRewardItemCardId()
	{
		string itemCardId = _debugForcedNextQuestionRewardItemCardId;
		_debugForcedNextQuestionRewardItemCardId = string.Empty;
		return itemCardId;
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
