using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private NetworkVisualPlaybackController? _networkVisualPlaybackController;

	private NetworkVisualPlaybackController NetworkVisualPlayback
		=> _networkVisualPlaybackController ??= new NetworkVisualPlaybackController(
			PlayNetworkVisualEventFromPlaybackControllerAsync,
			ApplyNetworkBoardSnapshotFromPlaybackController,
			CompleteReconnectCatchupIfReady);

	private async Task PlayNetworkVisualEventFromPlaybackControllerAsync(NetworkVisualEvent visualEvent)
	{
		double debugPlaybackDelaySeconds = GetDebugNetworkVisualPlaybackDelaySeconds();
		if (debugPlaybackDelaySeconds > 0.0)
		{
			await WaitForBoardDelayAsync(debugPlaybackDelaySeconds);
		}

		await PlayNetworkCardPlayedEventAsync(visualEvent);
	}

	private bool ApplyNetworkBoardSnapshotFromPlaybackController(ValidatedNetworkBoardSnapshot snapshot)
	{
		try
		{
			ApplyNetworkBoardSnapshotNow(snapshot);
			return true;
		}
		catch (Exception error)
		{
			Godot.GD.PushWarning($"Failed to apply network visual playback snapshot {snapshot.SnapshotId}: {error.Message}");
			return false;
		}
	}

	private sealed class NetworkVisualPlaybackController
	{
		private readonly Queue<NetworkVisualEvent> _pendingEvents = new();
		private readonly Func<NetworkVisualEvent, Task> _playEventAsync;
		private readonly Func<ValidatedNetworkBoardSnapshot, bool> _applySnapshot;
		private readonly Action _onQueueStable;
		private ValidatedNetworkBoardSnapshot? _pendingSnapshot;
		private bool _shutdown;

		internal NetworkVisualPlaybackController(
			Func<NetworkVisualEvent, Task> playEventAsync,
			Func<ValidatedNetworkBoardSnapshot, bool> applySnapshot,
			Action onQueueStable)
		{
			_playEventAsync = playEventAsync;
			_applySnapshot = applySnapshot;
			_onQueueStable = onQueueStable;
		}

		internal int PendingEventCount => _pendingEvents.Count;
		internal bool IsProcessing { get; private set; }
		internal bool IsFastForwarding { get; private set; }
		internal int DroppedEventCount { get; private set; }
		internal int FastForwardCount { get; private set; }
		internal int ActivityCount { get; private set; }

		internal bool HasQueuedEventForSnapshot(long authorityEpoch, int snapshotId)
		{
			if (_shutdown)
			{
				return false;
			}
			foreach (NetworkVisualEvent visualEvent in _pendingEvents)
			{
				if (visualEvent.AuthorityEpoch == authorityEpoch && visualEvent.SnapshotId == snapshotId)
				{
					return true;
				}
			}

			return false;
		}

		internal void Enqueue(NetworkVisualEvent visualEvent)
		{
			if (_shutdown)
			{
				return;
			}

			ActivityCount = NetworkVisualQueuePolicy.SaturatingAdd(ActivityCount, 1);
			switch (NetworkVisualQueuePolicy.ClassifyAdmission(_pendingEvents.Count, IsFastForwarding))
			{
				case NetworkVisualQueueAdmission.DropWhileFastForwarding:
					DroppedEventCount = NetworkVisualQueuePolicy.SaturatingAdd(DroppedEventCount, 1);
					return;
				case NetworkVisualQueueAdmission.EnterFastForward:
					FastForwardCount = NetworkVisualQueuePolicy.SaturatingAdd(FastForwardCount, 1);
					DroppedEventCount = NetworkVisualQueuePolicy.SaturatingAdd(DroppedEventCount, _pendingEvents.Count + 1);
					_pendingEvents.Clear();
					IsFastForwarding = true;
					StartProcessingIfNeeded();
					return;
				default:
					_pendingEvents.Enqueue(visualEvent);
					StartProcessingIfNeeded();
					return;
			}
		}

		internal void SubmitSnapshot(ValidatedNetworkBoardSnapshot snapshot)
		{
			if (_shutdown)
			{
				return;
			}

			ActivityCount = NetworkVisualQueuePolicy.SaturatingAdd(ActivityCount, 1);
			StageLatestSnapshot(snapshot);
			if (IsFastForwarding)
			{
				ApplyPendingSnapshotIfIdle();
				return;
			}

			if (IsProcessing || HasQueuedEventForSnapshot(snapshot.AuthorityEpoch, snapshot.SnapshotId))
			{
				return;
			}

			ApplyPendingSnapshotIfIdle();
		}

		internal void StagePendingSnapshot(ValidatedNetworkBoardSnapshot snapshot)
		{
			if (_shutdown)
			{
				return;
			}
			StageLatestSnapshot(snapshot);
		}

		internal void ClearEvents()
		{
			_pendingEvents.Clear();
			IsFastForwarding = false;
		}

		internal void ClearPendingSnapshot()
		{
			_pendingSnapshot = null;
		}

		internal void Shutdown()
		{
			_shutdown = true;
			_pendingEvents.Clear();
			_pendingSnapshot = null;
			IsFastForwarding = false;
		}

		private void StageLatestSnapshot(ValidatedNetworkBoardSnapshot snapshot)
		{
			if (_pendingSnapshot == null || IsAtOrAfterFence(snapshot.AuthorityEpoch, snapshot.SnapshotId, _pendingSnapshot.AuthorityEpoch, _pendingSnapshot.SnapshotId))
			{
				_pendingSnapshot = snapshot;
			}
		}

		private void StartProcessingIfNeeded()
		{
			if (!_shutdown && !IsProcessing)
			{
				_ = ProcessAsync();
			}
		}

		private async Task ProcessAsync()
		{
			IsProcessing = true;
			try
			{
				while (!_shutdown && !IsFastForwarding && _pendingEvents.Count > 0)
				{
					NetworkVisualEvent visualEvent = _pendingEvents.Dequeue();
				if (_pendingSnapshot != null
					&& IsAtOrAfterFence(
						_pendingSnapshot.AuthorityEpoch,
						_pendingSnapshot.SnapshotId,
						visualEvent.AuthorityEpoch,
						visualEvent.SnapshotId))
					{
						continue;
					}

					await _playEventAsync(visualEvent);
				}
			}
			catch (OperationCanceledException) when (_shutdown)
			{
			}
			catch (Exception error)
			{
				if (!_shutdown)
				{
					GD.PushError($"Network visual playback failed: {error}");
				}
			}
			finally
			{
				if (!_shutdown)
				{
					ApplyPendingSnapshotIfIdle(allowWhileProcessing: true);
				}
				IsProcessing = false;
				if (!_shutdown)
				{
					_onQueueStable();
				}
			}
		}

		private void ApplyPendingSnapshotIfIdle(bool allowWhileProcessing = false)
		{
			if (_shutdown || (!allowWhileProcessing && IsProcessing) || _pendingSnapshot == null)
			{
				return;
			}

			ValidatedNetworkBoardSnapshot snapshot = _pendingSnapshot;
			if (!_applySnapshot(snapshot))
			{
				return;
			}

			_pendingSnapshot = null;
			_pendingEvents.Clear();
			IsFastForwarding = false;
		}

		private static bool IsAtOrAfterFence(
			long leftEpoch,
			int leftSnapshotId,
			long rightEpoch,
			int rightSnapshotId)
		{
			return leftEpoch > rightEpoch
				|| (leftEpoch == rightEpoch && leftSnapshotId >= rightSnapshotId);
		}

	}
}
