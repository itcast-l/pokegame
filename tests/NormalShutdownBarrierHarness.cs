using System.Threading.Tasks;
using Godot;

public partial class NormalShutdownBarrierHarness : RefCounted
{
	private Task<Error>? _firstWriteTask;
	private Task<Error>? _secondWriteTask;
	private Task<Error>? _sealedWriteProbeTask;

	public Error ConfigureProfile(
		string slotId,
		string playerName,
		string selectedAvatarId)
	{
		return GameSaveStore.CreateProfile(slotId, playerName, selectedAvatarId);
	}

	public void StartProfileWrites()
	{
		_firstWriteTask = GameSaveStore.UpdateActiveProfileAsync(profile => profile with
		{
			PlayerName = "FirstWrite",
		});
		_secondWriteTask = GameSaveStore.UpdateActiveProfileAsync(profile => profile with
		{
			HighestCompletedMainAdventureStage = 2,
		});
	}

	public void StartSealedWriteProbe()
	{
		_sealedWriteProbeTask = GameSaveStore.UpdateActiveProfileAsync(profile => profile with
		{
			PlayerName = "Must Not Persist",
		});
	}

	public int GetAdmittedOperationCount()
	{
		return GameSaveProfileWriteCoordinator.AdmittedOperationCount;
	}

	public bool AreProfileWritesCompleted()
	{
		return _firstWriteTask?.IsCompleted == true
			&& _secondWriteTask?.IsCompleted == true;
	}

	public Error GetFirstWriteResult()
	{
		return GetCompletedResult(_firstWriteTask);
	}

	public Error GetSecondWriteResult()
	{
		return GetCompletedResult(_secondWriteTask);
	}

	public Error GetSealedWriteProbeResult()
	{
		return GetCompletedResult(_sealedWriteProbeTask);
	}

	private static Error GetCompletedResult(Task<Error>? task)
	{
		return task?.IsCompletedSuccessfully == true
			? task.Result
			: Error.Busy;
	}
}
