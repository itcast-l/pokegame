using System;
using System.Collections.Generic;
using Godot;

internal static class GameSaveProfileRepository
{
	internal const int CurrentProgressSchemaVersion = 3;
	internal const string ProgressSchemaVersionKey = "schema_version";
	internal const string ProgressHighestCompletedMainAdventureStageKey = "highest_completed_main_adventure_stage";
	internal const string ProgressCompletedAdventureTutorialIdsCsvKey = "completed_adventure_tutorial_ids_csv";
	// Kept only for reading v1 profiles and legacy settings migration.
	internal const string ProgressHighestCompletedStageKey = "highest_completed_stage";
	internal const string ProgressSelectedAvatarIdKey = "selected_avatar_id";
	internal const string ProgressPlayerNameKey = "player_name";
	internal const string ProgressPendingAvatarUnlockQueueKey = "pending_avatar_unlock_queue";
	internal const string ProgressPendingAdventureRunJsonKey = "pending_adventure_run_json";
	internal const string ProgressUnlockedSkillCodexUpgradeIdsCsvKey = "unlocked_skill_codex_upgrade_ids_csv";
	internal const string ProgressPlaytimeSecondsKey = "playtime_seconds";
	internal const string ProgressCreatedUnixTimeKey = "created_unix_time";
	internal const string ProgressUpdatedUnixTimeKey = "updated_unix_time";

internal static IReadOnlyList<GameSaveStore.SaveSlotMetadata> ListProfiles(Node saveManager)
	{
		var profiles = new List<GameSaveStore.SaveSlotMetadata>(GameSaveStore.ProfileSlotCount);
		for (int index = 0; index < GameSaveStore.ProfileSlotCount; index++)
		{
			string slotId = GameSaveStore.GetProfileSlotId(index);
			bool exists = ProfileSlotFileExists(saveManager, slotId);
			GameSaveStore.GameProgressData progressData = exists ? LoadProfileRaw(saveManager, slotId) : GameSaveStore.GameProgressData.CreateDefault();
			profiles.Add(progressData.ToMetadata(slotId, exists));
		}

		return profiles;
	}

internal static GameSaveStore.GameProgressData LoadProfileRaw(Node saveManager, string slotId)
	{
		EnsureProfileSlot(saveManager, slotId);
		if (!ProfileSlotFileExists(saveManager, slotId))
		{
			return GameSaveStore.GameProgressData.CreateDefault();
		}

		Godot.Collections.Dictionary data = saveManager.Call("load_from_slot_sync", new StringName(slotId)).AsGodotDictionary();
		if (data.Count == 0)
		{
			GD.PushWarning($"Profile save slot '{slotId}' is empty or unreadable; using default progress for this session.");
			return GameSaveStore.GameProgressData.CreateDefault();
		}

		GameSaveStore.GameProgressData progressData = GameSaveStore.GameProgressData.FromDictionary(
			data,
			GameSaveStore.GameProgressData.CreateDefault());
		if (RequiresCanonicalV3Rewrite(data))
		{
			progressData = progressData with { SchemaVersion = CurrentProgressSchemaVersion };
			Error rewriteResult = SaveProfileRaw(saveManager, slotId, progressData);
			if (rewriteResult != Error.Ok)
			{
				GD.PushWarning($"Failed to rewrite migrated profile slot '{slotId}': {rewriteResult}");
			}
		}

		return progressData;
	}

internal static Error SaveProfileRaw(Node saveManager, string slotId, GameSaveStore.GameProgressData progressData)
	{
		EnsureProfileSlot(saveManager, slotId);
		GameSaveStore.GameProgressData normalizedProgress = progressData.Normalize();
		long now = GameSaveStore.GetUnixTime();
		if (normalizedProgress.CreatedUnixTime <= 0)
		{
			normalizedProgress = normalizedProgress with { CreatedUnixTime = now };
		}

		normalizedProgress = normalizedProgress with
		{
			SchemaVersion = CurrentProgressSchemaVersion,
			UpdatedUnixTime = now,
		};

		return (Error)saveManager.Call(
			"save_to_slot_sync",
			new StringName(slotId),
			normalizedProgress.ToDictionary()).AsInt32();
	}

internal static string GetActiveProfileSlotIdRaw(Node saveManager)
	{
		return NormalizeProfileSlotId(GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveStore.ActiveProfileSlotKey, GameSaveStore.DefaultProfileSlotId).AsString());
	}

internal static Error SetActiveProfileRaw(Node saveManager, string slotId)
	{
		string previousSlotId = GetActiveProfileSlotIdRaw(saveManager);
		string normalizedSlotId = NormalizeProfileSlotId(slotId);
		GameSaveLegacyAdapter.SetRawValue(saveManager, GameSaveStore.ActiveProfileSlotKey, normalizedSlotId);
		Error persistResult = GameSaveLegacyAdapter.Persist(saveManager);
		if (persistResult == Error.Ok)
		{
			return Error.Ok;
		}

		GameSaveLegacyAdapter.SetRawValue(saveManager, GameSaveStore.ActiveProfileSlotKey, previousSlotId);
		return persistResult;
	}

internal static string FindFirstExistingProfileSlotId(Node saveManager, string excludedSlotId = "")
	{
		for (int index = 0; index < GameSaveStore.ProfileSlotCount; index++)
		{
			string slotId = GameSaveStore.GetProfileSlotId(index);
			if (!string.Equals(slotId, excludedSlotId, StringComparison.Ordinal)
				&& ProfileSlotFileExists(saveManager, slotId))
			{
				return slotId;
			}
		}

		return string.Empty;
	}

internal static string FindFirstEmptyProfileSlotId(Node saveManager, string excludedSlotId = "")
	{
		for (int index = 0; index < GameSaveStore.ProfileSlotCount; index++)
		{
			string slotId = GameSaveStore.GetProfileSlotId(index);
			if (!string.Equals(slotId, excludedSlotId, StringComparison.Ordinal)
				&& !ProfileSlotFileExists(saveManager, slotId))
			{
				return slotId;
			}
		}

		return string.Empty;
	}

internal static string NormalizeProfileSlotId(string slotId)
	{
		for (int index = 0; index < GameSaveStore.ProfileSlotCount; index++)
		{
			string candidate = GameSaveStore.GetProfileSlotId(index);
			if (string.Equals(slotId, candidate, StringComparison.Ordinal))
			{
				return candidate;
			}
		}

		return GameSaveStore.DefaultProfileSlotId;
	}

internal static bool ProfileSlotFileExists(Node saveManager, string slotId)
	{
		EnsureProfileSlot(saveManager, slotId);
		string path = GetProfileSlotPath(saveManager, slotId);
		return Godot.FileAccess.FileExists(path) || Godot.FileAccess.FileExists($"{path}.bak");
	}

internal static void EnsureProfileSlot(Node saveManager, string slotId)
	{
		saveManager.Call("ensure_slot_for_file_base", new StringName(slotId), slotId);
	}

internal static string GetProfileSlotPath(Node saveManager, string slotId)
	{
		string saveRoot = saveManager.Get("save_root").AsString().Trim();
		if (string.IsNullOrEmpty(saveRoot))
		{
			saveRoot = "user://savestate";
		}

		if (!saveRoot.EndsWith("/", StringComparison.Ordinal))
		{
			saveRoot += "/";
		}

		string extension = saveManager.Get("use_json").AsBool() ? "json" : "bin";
		return $"{saveRoot}{slotId}.{extension}";
	}

internal static Error RemoveProfileSlotFiles(Node saveManager, string slotId)
	{
		string path = GetProfileSlotPath(saveManager, slotId);
		Error mainRemoveResult = RemoveFileIfExists(path);
		if (mainRemoveResult != Error.Ok)
		{
			return mainRemoveResult;
		}

		return RemoveFileIfExists($"{path}.bak");
	}

internal static Error RemoveFileIfExists(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
		{
			return Error.Ok;
		}

		return DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
	}

internal static Variant ReadDictionaryValue(Godot.Collections.Dictionary data, string key, Variant defaultValue)
	{
		if (data.ContainsKey(key))
		{
			return data[key];
		}

		var stringNameKey = new StringName(key);
		return data.ContainsKey(stringNameKey) ? data[stringNameKey] : defaultValue;
	}

internal static bool ContainsDictionaryKey(Godot.Collections.Dictionary data, string key)
	{
		return data.ContainsKey(key) || data.ContainsKey(new StringName(key));
	}

internal static string ReadString(Godot.Collections.Dictionary data, string key, string defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsString();
	}

internal static int ReadInt(Godot.Collections.Dictionary data, string key, int defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsInt32();
	}

internal static long ReadLong(Godot.Collections.Dictionary data, string key, long defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsInt64();
	}

internal static double ReadDouble(Godot.Collections.Dictionary data, string key, double defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsDouble();
	}

private static bool RequiresCanonicalV3Rewrite(Godot.Collections.Dictionary data)
	{
		int schemaVersion = ReadInt(data, ProgressSchemaVersionKey, 1);
		return schemaVersion != CurrentProgressSchemaVersion
			|| !ContainsDictionaryKey(data, ProgressHighestCompletedMainAdventureStageKey)
			|| !ContainsDictionaryKey(data, ProgressCompletedAdventureTutorialIdsCsvKey)
			|| !ContainsDictionaryKey(data, ProgressUnlockedSkillCodexUpgradeIdsCsvKey);
	}
}
