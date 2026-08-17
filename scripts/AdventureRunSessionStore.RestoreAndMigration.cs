using Godot;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using System.Threading.Tasks;
using Deckland.Rules;

public sealed partial class AdventureRunSessionStore : Node
{
	internal AdventureUpgradeRestoreResult ReloadFromActiveProfile()
	{
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			ResetAdventureAuthorityForRestore();
			_deferredPayload = null;
			_preservedFailurePayload = null;
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
			return LastRestoreResult;
		}

		if (!TryGetActiveProfileSnapshot(out GameSaveStore.GameProgressData profile))
		{
			LastRestoreResult = AdventureUpgradeRestoreResult.DeferredManifestUnavailable;
			return LastRestoreResult;
		}
		return ReloadFromProfileSnapshot(profile);
	}

	internal AdventureUpgradeRestoreResult ReloadFromProfileSnapshot(
		GameSaveStore.GameProgressData profile)
	{
		Node? saveManager = GameSaveStore.GetSaveManager();
		if (saveManager != null)
		{
			GameSaveMigration.EnsureInitialized(saveManager);
			_profileSnapshotSlotId = GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager);
			_profileSnapshot = profile;
		}

		ResetAdventureAuthorityForRestore();
		_deferredPayload = null;
		_preservedFailurePayload = null;
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			LastRestoreResult = AdventureUpgradeRestoreResult.NoSnapshot;
			return LastRestoreResult;
		}

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
		if (!_gameModePolicy.CanReadAdventureRun)
		{
			ResetAdventureAuthorityForRestore();
			return AdventureUpgradeRestoreResult.NoSnapshot;
		}

		bool requiresCanonicalCurrentRewrite;
		bool migratedBySchema;
		try
		{
			using JsonDocument document = JsonDocument.Parse(json);
			requiresCanonicalCurrentRewrite = RequiresCanonicalCurrentRewrite(document.RootElement);
		}
		catch (JsonException error)
		{
			ResetAdventureAuthorityForRestore();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidJsonPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session JSON; the original payload was preserved: {error.Message}");
			return LastRestoreResult;
		}

		if (!AdventureUpgradeRunSnapshot.TryDeserialize(
			json,
			out AdventureUpgradeRunSnapshot snapshot,
			out migratedBySchema,
			out string failure))
		{
			ResetAdventureAuthorityForRestore();
			_deferredPayload = null;
			_preservedFailurePayload = json;
			LastRestoreResult = AdventureUpgradeRestoreResult.InvalidSnapshotPreserved;
			WarnPreservedPayloadOnce(
				json,
				$"Failed to restore adventure upgrade run session snapshot; the original payload was preserved: {failure}");
			return LastRestoreResult;
		}

		AdventureUpgradeRestoreResult result =
			_authoritySession.RestoreAdventurePersistedRun(snapshot);
		if (result == AdventureUpgradeRestoreResult.Restored
			&& (requiresCanonicalCurrentRewrite || migratedBySchema))
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
				PersistMigratedSnapshot();
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
				ResetAdventureAuthorityForRestore();
				_deferredPayload = null;
				_preservedFailurePayload = null;
				return result;
		}
	}

	private void PersistMigratedSnapshot()
	{
		if (!_gameModePolicy.CanWriteAdventureRun
			|| !TryGetSnapshot(out AdventureUpgradeRunSnapshot snapshot))
		{
			return;
		}

		string json = JsonSerializer.Serialize(snapshot);
		_ = PersistMigratedSnapshotAsync(json);
	}

	private async Task PersistMigratedSnapshotAsync(string json)
	{
		Error saveResult = await GameSaveStore.UpdateActiveProfileAsync(profile => profile with
		{
			PendingAdventureRunJson = json,
		});
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist migrated adventure upgrade run session: {saveResult}");
			return;
		}

		InvalidateProfileSnapshot();
		ObservePayload(json);
	}


	private void ResetAdventureAuthorityForRestore()
	{
		if (_manifest?.IsReady == true)
		{
			_ = _authoritySession.ConfigureAdventureManifest(_manifest);
			_ = _authoritySession.RestoreAdventurePersistedRun(null);
			return;
		}

		if (_boundBoardAuthoritySession == null)
		{
			_authoritySession = CreateDetachedAuthoritySession();
		}
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

	private static bool RequiresCanonicalCurrentRewrite(JsonElement root)
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
			if (!CanonicalCurrentPropertyNames.Contains(property.Name)
				|| !observedPropertyNames.Add(property.Name))
			{
				return true;
			}
		}

		return observedPropertyNames.Count != CanonicalCurrentPropertyNames.Count;
	}
}
