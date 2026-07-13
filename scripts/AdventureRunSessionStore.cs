using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

public sealed partial class AdventureRunSessionStore : Node
{
	private static readonly HashSet<string> CanonicalV5PropertyNames = new(
		StringComparer.Ordinal)
	{
		nameof(AdventureUpgradeRunSnapshot.SchemaVersion),
		nameof(AdventureUpgradeRunSnapshot.CurrentAvatarStageNumber),
		nameof(AdventureUpgradeRunSnapshot.ShouldGrantRunStartUpgrade),
		nameof(AdventureUpgradeRunSnapshot.ShouldGrantVictoryUpgrade),
		nameof(AdventureUpgradeRunSnapshot.AcquiredUpgradeIds),
		nameof(AdventureUpgradeRunSnapshot.CurrentHealth),
		nameof(AdventureUpgradeRunSnapshot.MaxHealth),
		nameof(AdventureUpgradeRunSnapshot.PermanentCaptureDamageBonus),
		nameof(AdventureUpgradeRunSnapshot.QuestionRewardCount),
		nameof(AdventureUpgradeRunSnapshot.HealingOverflowRemainder),
	};

	private readonly AdventureUpgradeRunState _runState = new();
	private string? _deferredPayload;
	private string? _preservedFailurePayload;
	private string? _lastObservedPayload;
	private string? _lastWarnedInvalidPayload;

	public bool HasActiveRun => _runState.IsActive;
	internal AdventureUpgradeRestoreResult LastRestoreResult { get; private set; } =
		AdventureUpgradeRestoreResult.NoSnapshot;

	public override void _Ready()
	{
		Node? pandora = GetNodeOrNull<Node>("/root/Pandora");
		if (pandora != null)
		{
			if (pandora.Call("is_loaded").AsBool())
			{
				_ = AdventureSuitUpgradeCatalog.Load(this);
			}
			else
			{
				Callable dataLoadedCallback = Callable.From(OnPandoraDataLoaded);
				if (!pandora.IsConnected("data_loaded", dataLoadedCallback))
				{
					pandora.Connect("data_loaded", dataLoadedCallback);
				}
			}
		}

		ReloadFromActiveProfile();
	}

	private void OnPandoraDataLoaded()
	{
		_ = AdventureSuitUpgradeCatalog.Load(this);
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

	internal AdventureUpgradeRestoreResult SetManifest(AdventureUpgradeManifest manifest)
	{
		if (manifest == null || !manifest.IsReady)
		{
			return AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
		}

		_runState.SetManifest(manifest);
		if (string.IsNullOrEmpty(_deferredPayload))
		{
			return LastRestoreResult;
		}

		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string currentPayload = profile.PendingAdventureRunJson;
		if (!string.Equals(currentPayload, _deferredPayload, StringComparison.Ordinal))
		{
			return ReloadFromActiveProfile();
		}

		return RestorePayload(profile, _deferredPayload);
	}

	internal void BeginNewRun(int avatarStageNumber)
	{
		if (HasRetainedPayload)
		{
			return;
		}

		_runState.BeginNewRun(avatarStageNumber);
		Persist();
	}

	internal void AdvanceToStage(int avatarStageNumber)
	{
		if (HasRetainedPayload)
		{
			return;
		}

		_runState.AdvanceToStage(avatarStageNumber);
		Persist();
	}

	internal void MarkRunStartUpgradeGranted()
	{
		if (HasRetainedPayload)
		{
			return;
		}

		_runState.MarkRunStartUpgradeGranted();
		Persist();
	}

	internal void MarkVictoryUpgradeGranted()
	{
		if (HasRetainedPayload)
		{
			return;
		}

		_runState.MarkVictoryUpgradeGranted();
		Persist();
	}

	internal AdventureUpgradeAcquireResult AcquireUpgrade(string upgradeId)
	{
		if (HasRetainedPayload)
		{
			return AdventureUpgradeAcquireResult.DeferredManifestUnavailable;
		}

		AdventureUpgradeAcquireResult result = _runState.AcquireUpgrade(upgradeId);
		if (result == AdventureUpgradeAcquireResult.Acquired)
		{
			Persist();
		}

		return result;
	}

	internal void SetHealth(int currentHealth, int maxHealth)
	{
		if (HasRetainedPayload)
		{
			return;
		}

		_runState.SetHealth(currentHealth, maxHealth);
		Persist();
	}

	internal void ReplaceEffectState(AdventureUpgradeEffectState effectState)
	{
		if (HasRetainedPayload)
		{
			return;
		}

		_runState.ReplaceEffectState(effectState);
		Persist();
	}

	internal void ClearRun()
	{
		_deferredPayload = null;
		_preservedFailurePayload = null;
		_runState.Clear();
		LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
		Persist();
	}

	internal AdventureUpgradeRestoreResult ReloadFromActiveProfile()
	{
		_runState.Clear();
		_deferredPayload = null;
		_preservedFailurePayload = null;
		GameSaveStore.GameProgressData profile = GameSaveStore.LoadActiveProfile();
		string json = profile.PendingAdventureRunJson;
		ObservePayload(json);
		if (string.IsNullOrWhiteSpace(json))
		{
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
			return LastRestoreResult;
		}

		return RestorePayload(profile, json);
	}

	private AdventureUpgradeRestoreResult RestorePayload(
		GameSaveStore.GameProgressData profile,
		string json)
	{
		bool requiresCanonicalV5Rewrite;
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			requiresCanonicalV5Rewrite = RequiresCanonicalV5Rewrite(document.RootElement);
		}
		catch (JsonException error)
		{
			_runState.Clear();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidJsonPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session JSON; the original payload was preserved: {error.Message}");
			return LastRestoreResult;
		}

		AdventureUpgradeRunSnapshot snapshot;
		try
		{
			snapshot = JsonSerializer.Deserialize<AdventureUpgradeRunSnapshot>(json);
		}
		catch (Exception error)
		{
			_runState.Clear();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidSnapshotPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session snapshot; the original payload was preserved: {error.Message}");
			return LastRestoreResult;
		}

		AdventureUpgradeRestoreResult result = _runState.RestoreFromSnapshot(snapshot);
		if (result == AdventureUpgradeRestoreResult.Restored && requiresCanonicalV5Rewrite)
		{
			result = AdventureUpgradeRestoreResult.Migrated;
		}
		LastRestoreResult = result;
		switch (result)
		{
			case AdventureUpgradeRestoreResult.Restored:
				_deferredPayload = null;
				_preservedFailurePayload = null;
				return result;
			case AdventureUpgradeRestoreResult.Migrated:
				_deferredPayload = null;
				_preservedFailurePayload = null;
				PersistMigratedSnapshot(profile);
				return result;
			case AdventureUpgradeRestoreResult.DeferredManifestUnavailable:
				_deferredPayload = json;
				_preservedFailurePayload = null;
				return result;
			case AdventureUpgradeRestoreResult.InvalidSnapshotPreserved:
				_deferredPayload = null;
				_preservedFailurePayload = json;
				WarnPreservedPayloadOnce(
					json,
					"Failed to restore adventure upgrade run session snapshot; the original payload was preserved.");
				return result;
			default:
				_runState.Clear();
				_deferredPayload = null;
				_preservedFailurePayload = null;
				return result;
		}
	}

	private void PersistMigratedSnapshot(GameSaveStore.GameProgressData profile)
	{
		if (!_runState.IsActive)
		{
			return;
		}

		string json = JsonSerializer.Serialize(_runState.ToSnapshot());
		Error saveResult = GameSaveStore.SaveActiveProfile(profile with
		{
			PendingAdventureRunJson = json,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist migrated adventure upgrade run session: {saveResult}");
			return;
		}

		ObservePayload(json);
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
			return;
		}

		ObservePayload(json);
	}

	private void ObservePayload(string? payload)
	{
		string normalizedPayload = payload ?? string.Empty;
		if (string.Equals(_lastObservedPayload, normalizedPayload, StringComparison.Ordinal))
		{
			return;
		}

		_lastObservedPayload = normalizedPayload;
		_lastWarnedInvalidPayload = null;
	}

	private void WarnPreservedPayloadOnce(string payload, string warning)
	{
		if (string.Equals(_lastWarnedInvalidPayload, payload, StringComparison.Ordinal))
		{
			return;
		}

		GD.PushWarning(warning);
		_lastWarnedInvalidPayload = payload;
	}

	private bool HasRetainedPayload =>
		_deferredPayload != null || _preservedFailurePayload != null;

	private static bool RequiresCanonicalV5Rewrite(JsonElement root)
	{
		if (root.ValueKind != JsonValueKind.Object)
		{
			return false;
		}

		if (!root.TryGetProperty(
			nameof(AdventureUpgradeRunSnapshot.SchemaVersion),
			out JsonElement schemaVersionElement)
			|| schemaVersionElement.ValueKind != JsonValueKind.Number
			|| !schemaVersionElement.TryGetInt32(out int schemaVersion)
			|| schemaVersion != AdventureUpgradeRunSnapshot.CurrentSchemaVersion)
		{
			return false;
		}

		var observedPropertyNames = new HashSet<string>(StringComparer.Ordinal);
		foreach (JsonProperty property in root.EnumerateObject())
		{
			if (!CanonicalV5PropertyNames.Contains(property.Name)
				|| !observedPropertyNames.Add(property.Name))
			{
				return true;
			}
		}

		return observedPropertyNames.Count != CanonicalV5PropertyNames.Count;
	}
}
