using Godot;
using System;
using System.Text.Json;

public sealed partial class AdventureRunSessionStore : Node
{
	private readonly AdventureUpgradeRunState _runState = new();

	public bool HasActiveRun => _runState.IsActive;

	public override void _Ready()
	{
		ReloadFromActiveProfile();
	}

	internal AdventureUpgradeRunState GetRunState()
	{
		return _runState;
	}

	internal bool TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot)
	{
		if (!_runState.IsActive)
		{
			snapshot = default;
			return false;
		}

		snapshot = _runState.ToSnapshot();
		return true;
	}

	internal void BeginNewRun(int avatarStageNumber)
	{
		_runState.BeginNewRun(avatarStageNumber);
		Persist();
	}

	internal void AdvanceToStage(int avatarStageNumber)
	{
		_runState.AdvanceToStage(avatarStageNumber);
		Persist();
	}

	internal void MarkRunStartUpgradeGranted()
	{
		_runState.MarkRunStartUpgradeGranted();
		Persist();
	}

	internal void MarkVictoryUpgradeGranted()
	{
		_runState.MarkVictoryUpgradeGranted();
		Persist();
	}

	internal void AcquireUpgrade(string upgradeId)
	{
		_runState.AcquireUpgrade(upgradeId);
		Persist();
	}

	internal void SetHealth(int currentHealth, int maxHealth)
	{
		_runState.SetHealth(currentHealth, maxHealth);
		Persist();
	}

	internal void ReplaceEffectState(AdventureUpgradeEffectState effectState)
	{
		_runState.ReplaceEffectState(effectState);
		Persist();
	}

	internal void ClearRun()
	{
		_runState.Clear();
		Persist();
	}

	internal void ReloadFromActiveProfile()
	{
		_runState.Clear();
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string json = profile.PendingAdventureRunJson;
		if (string.IsNullOrWhiteSpace(json))
		{
			return;
		}

		bool shouldClearSavedRun = false;
		try
		{
			AdventureUpgradeRunSnapshot snapshot = JsonSerializer.Deserialize<AdventureUpgradeRunSnapshot>(json);
			_runState.RestoreFromSnapshot(snapshot);
			if (!_runState.IsActive)
			{
				GD.PushWarning("Failed to restore adventure upgrade run session: snapshot is invalid for the current adventure run schema.");
				shouldClearSavedRun = true;
			}
		}
		catch (Exception error)
		{
			GD.PushWarning($"Failed to restore adventure upgrade run session: {error.Message}");
			shouldClearSavedRun = true;
		}

		if (!shouldClearSavedRun)
		{
			return;
		}

		_runState.Clear();
		Error clearResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = string.Empty,
		});
		if (clearResult != Error.Ok)
		{
			GD.PushWarning($"Failed to clear invalid adventure upgrade run session: {clearResult}");
		}
	}

	private void Persist()
	{
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string json = _runState.IsActive
			? JsonSerializer.Serialize(_runState.ToSnapshot())
			: string.Empty;
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = json,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist adventure upgrade run session: {saveResult}");
		}
	}
}
