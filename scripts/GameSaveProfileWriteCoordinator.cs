using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;

internal static class GameSaveProfileWriteCoordinator
{
	private static readonly SemaphoreSlim Gate = new(1, 1);
	private static readonly object StateLock = new();
	private static GameSaveStore.GameProgressData? _cachedProfile;
	private static string _cachedSlotId = string.Empty;
	private static bool _acceptingOperations = true;
	private static int _admittedOperationCount;
	private static TaskCompletionSource? _drainedCompletion;

	internal static async Task<ProfileWriteLease?> AcquireAsync(
		Func<GameSaveStore.GameProgressData, GameSaveStore.GameProgressData> update,
		bool forcePersist = true)
	{
		ArgumentNullException.ThrowIfNull(update);
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager == null || !TryAdmitOperation())
		{
			return null;
		}

		bool gateAcquired = false;
		try
		{
			await Gate.WaitAsync();
			gateAcquired = true;
			GameSaveMigration.EnsureInitialized(saveManager);
			string slotId = GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager);
			GameSaveStore.GameProgressData? cachedProfile;
			string cachedSlotId;
			lock (StateLock)
			{
				cachedProfile = _cachedProfile;
				cachedSlotId = _cachedSlotId;
			}
			GameSaveStore.GameProgressData baseProfile = cachedProfile != null
				&& string.Equals(cachedSlotId, slotId, StringComparison.Ordinal)
					? cachedProfile
					: GameSaveProfileRepository.LoadProfileRaw(saveManager, slotId);
			GameSaveStore.GameProgressData updatedProfile = update(baseProfile);
			bool requiresPersist = forcePersist || !Equals(updatedProfile, baseProfile);
			GameSaveStore.GameProgressData candidate = requiresPersist
				? GameSaveProfileRepository.PrepareForSave(updatedProfile)
				: baseProfile;
			return new ProfileWriteLease(
				saveManager,
				slotId,
				baseProfile,
				candidate,
				requiresPersist);
		}
		catch (Exception error)
		{
			GD.PushWarning($"Failed to prepare serialized profile write: {error.Message}");
			if (gateAcquired)
			{
				Gate.Release();
			}
			ReleaseAdmission();
			return null;
		}
	}

	internal static ProfileExclusiveLease? TryAcquireExclusiveLease(
		out Error error)
	{
		lock (StateLock)
		{
			if (!_acceptingOperations)
			{
				error = Error.Unavailable;
				return null;
			}
			if (_admittedOperationCount != 0 || !Gate.Wait(0))
			{
				error = Error.Busy;
				return null;
			}

			_admittedOperationCount++;
			error = Error.Ok;
			return new ProfileExclusiveLease();
		}
	}

	internal static Task SealAndDrainAsync()
	{
		lock (StateLock)
		{
			_acceptingOperations = false;
			if (_admittedOperationCount == 0)
			{
				return Task.CompletedTask;
			}

			_drainedCompletion ??= new TaskCompletionSource(
				TaskCreationOptions.RunContinuationsAsynchronously);
			return _drainedCompletion.Task;
		}
	}

	internal static int AdmittedOperationCount
	{
		get
		{
			lock (StateLock)
			{
				return _admittedOperationCount;
			}
		}
	}

	internal static void ObserveCommitted(
		string slotId,
		GameSaveStore.GameProgressData profile)
	{
		lock (StateLock)
		{
			_cachedSlotId = slotId;
			_cachedProfile = profile;
		}
	}

	internal static void Invalidate()
	{
		lock (StateLock)
		{
			_cachedSlotId = string.Empty;
			_cachedProfile = null;
		}
	}

	internal static void ResetForTests()
	{
		lock (StateLock)
		{
			_cachedSlotId = string.Empty;
			_cachedProfile = null;
			_acceptingOperations = true;
			_admittedOperationCount = 0;
			_drainedCompletion = null;
		}
	}

	internal static void ReleaseLease()
	{
		Gate.Release();
		ReleaseAdmission();
	}

	private static bool TryAdmitOperation()
	{
		lock (StateLock)
		{
			if (!_acceptingOperations)
			{
				return false;
			}

			_admittedOperationCount++;
			return true;
		}
	}

	private static void ReleaseAdmission()
	{
		TaskCompletionSource? drained = null;
		lock (StateLock)
		{
			if (_admittedOperationCount <= 0)
			{
				throw new InvalidOperationException(
					"A profile coordinator admission was released more than once.");
			}

			_admittedOperationCount--;
			if (!_acceptingOperations && _admittedOperationCount == 0)
			{
				drained = _drainedCompletion;
				_drainedCompletion = null;
			}
		}
		drained?.TrySetResult();
	}
}

internal sealed class ProfileExclusiveLease : IDisposable
{
	private bool _released;

	public void Dispose()
	{
		if (_released)
		{
			return;
		}

		_released = true;
		GameSaveProfileWriteCoordinator.ReleaseLease();
	}
}

internal sealed class ProfileWriteLease
{
	private readonly Node _saveManager;
	private readonly Godot.Collections.Dictionary _candidateData;
	private readonly bool _requiresPersist;
	private bool _released;

	internal ProfileWriteLease(
		Node saveManager,
		string slotId,
		GameSaveStore.GameProgressData baseProfile,
		GameSaveStore.GameProgressData candidateProfile,
		bool requiresPersist)
	{
		_saveManager = saveManager;
		SlotId = slotId;
		BaseProfile = baseProfile;
		CandidateProfile = candidateProfile;
		_candidateData = candidateProfile.ToDictionary();
		_requiresPersist = requiresPersist;
	}

	internal string SlotId { get; }
	internal GameSaveStore.GameProgressData BaseProfile { get; }
	internal GameSaveStore.GameProgressData CandidateProfile { get; }

	internal async Task<Error> PersistAttemptAsync()
	{
		if (!_requiresPersist)
		{
			return Error.Ok;
		}

		try
		{
			return await GameSaveStore.QueueSlotSaveAsync(_saveManager, SlotId, _candidateData);
		}
		catch (Exception error)
		{
			GD.PushWarning($"Serialized profile write failed unexpectedly: {error.Message}");
			return Error.Failed;
		}
	}

	internal void CompleteSuccess()
	{
		if (_released)
		{
			return;
		}

		GameSaveProfileWriteCoordinator.ObserveCommitted(SlotId, CandidateProfile);
		Release();
	}

	internal void Abort()
	{
		Release();
	}

	private void Release()
	{
		if (_released)
		{
			return;
		}

		_released = true;
		GameSaveProfileWriteCoordinator.ReleaseLease();
	}
}
