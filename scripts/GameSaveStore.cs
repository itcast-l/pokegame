using System;
using System.Collections.Generic;
using Godot;

public static class GameSaveStore
{
	public const string MasterVolumeKey = "settings.audio.master_volume";
	public const string MusicVolumeKey = "settings.audio.music_volume";
	public const string MuteAudioKey = "settings.audio.mute";
	public const string MuteBgmKey = "settings.audio.mute_bgm";
	public const string MuteSfxKey = "settings.audio.mute_sfx";
	public const string FullscreenKey = "settings.video.fullscreen";
	public const string ResolutionKey = "settings.video.resolution";
	public const string OceanMotionKey = "settings.video.ocean_motion";
	public const string MotionScaleKey = "settings.video.motion_scale";
	public const string LanguageKey = "settings.general.language";
	public const string InputBindingsKey = "settings.input.bindings";
	public const string SelectedAvatarKey = "settings.general.avatar";
	public const string PlayerNameKey = "settings.general.player_name";
	public const string HighestCompletedStageKey = "adventure.highest_completed_stage";
	public const string PendingAvatarUnlockQueueKey = "adventure.pending_avatar_unlock_queue";
	public const string PendingAvatarUnlockKey = "adventure.pending_avatar_unlock";
	public const string PendingAvatarUnlockStageKey = "adventure.pending_avatar_unlock_stage";
	public const string ActiveProfileSlotKey = "progress.active_profile_slot";
	public const string DefaultProfileSlotId = "profile_0";
	public const int ProfileSlotCount = 3;

	private const string SaveManagerNodeName = "SaveManager";
	private const string LegacySettingsConfigPath = "user://settings.cfg";
	private const string LegacyMigrationKey = "migration.settings_cfg.v1";
	private const string ProgressProfileMigrationKey = "migration.progress_profile.v1";
	private const string MissingSentinel = "__pokegame_save_missing_2B1E5D5F__";
	private const int CurrentProgressSchemaVersion = 1;
	private const string ProgressSchemaVersionKey = "schema_version";
	private const string ProgressHighestCompletedStageKey = "highest_completed_stage";
	private const string ProgressSelectedAvatarIdKey = "selected_avatar_id";
	private const string ProgressPlayerNameKey = "player_name";
	private const string ProgressPendingAvatarUnlockQueueKey = "pending_avatar_unlock_queue";
	private const string ProgressPlaytimeSecondsKey = "playtime_seconds";
	private const string ProgressCreatedUnixTimeKey = "created_unix_time";
	private const string ProgressUpdatedUnixTimeKey = "updated_unix_time";

	private static bool _attemptedLegacyMigration;
	private static bool _attemptedProgressProfileMigration;
	private static bool _warnedMissingSaveManager;

	public sealed record GameProgressData(
		int SchemaVersion,
		int HighestCompletedStage,
		string SelectedAvatarId,
		string PlayerName,
		string PendingAvatarUnlockQueue,
		double PlaytimeSeconds,
		long CreatedUnixTime,
		long UpdatedUnixTime)
	{
		public static GameProgressData CreateDefault()
		{
			long now = GetUnixTime();
			return new GameProgressData(
				CurrentProgressSchemaVersion,
				0,
				CharacterAvatarCatalog.DefaultAvatarId,
				PlayerNameRules.DefaultPlayerName,
				string.Empty,
				0.0,
				now,
				now);
		}

		public static GameProgressData FromDictionary(Godot.Collections.Dictionary data, GameProgressData fallback)
		{
			return new GameProgressData(
				ReadInt(data, ProgressSchemaVersionKey, fallback.SchemaVersion),
				ReadInt(data, ProgressHighestCompletedStageKey, fallback.HighestCompletedStage),
				ReadString(data, ProgressSelectedAvatarIdKey, fallback.SelectedAvatarId),
				ReadString(data, ProgressPlayerNameKey, fallback.PlayerName),
				ReadString(data, ProgressPendingAvatarUnlockQueueKey, fallback.PendingAvatarUnlockQueue),
				ReadDouble(data, ProgressPlaytimeSecondsKey, fallback.PlaytimeSeconds),
				ReadLong(data, ProgressCreatedUnixTimeKey, fallback.CreatedUnixTime),
				ReadLong(data, ProgressUpdatedUnixTimeKey, fallback.UpdatedUnixTime)).Normalize();
		}

		public Godot.Collections.Dictionary ToDictionary()
		{
			return new Godot.Collections.Dictionary
			{
				[ProgressSchemaVersionKey] = SchemaVersion,
				[ProgressHighestCompletedStageKey] = HighestCompletedStage,
				[ProgressSelectedAvatarIdKey] = SelectedAvatarId,
				[ProgressPlayerNameKey] = PlayerName,
				[ProgressPendingAvatarUnlockQueueKey] = PendingAvatarUnlockQueue,
				[ProgressPlaytimeSecondsKey] = PlaytimeSeconds,
				[ProgressCreatedUnixTimeKey] = CreatedUnixTime,
				[ProgressUpdatedUnixTimeKey] = UpdatedUnixTime,
			};
		}

		public SaveSlotMetadata ToMetadata(string slotId, bool exists)
		{
			return new SaveSlotMetadata(
				slotId,
				exists,
				PlayerName,
				SelectedAvatarId,
				HighestCompletedStage,
				PlaytimeSeconds,
				UpdatedUnixTime);
		}

		public GameProgressData Normalize()
		{
			long now = GetUnixTime();
			long createdUnixTime = CreatedUnixTime > 0 ? CreatedUnixTime : now;
			long updatedUnixTime = UpdatedUnixTime > 0 ? UpdatedUnixTime : createdUnixTime;
			string selectedAvatarId = string.IsNullOrWhiteSpace(SelectedAvatarId)
				? CharacterAvatarCatalog.DefaultAvatarId
				: SelectedAvatarId.Trim();

			return this with
			{
				SchemaVersion = Math.Max(1, SchemaVersion),
				HighestCompletedStage = Math.Max(0, HighestCompletedStage),
				SelectedAvatarId = selectedAvatarId,
				PlayerName = PlayerNameRules.Normalize(PlayerName),
				PendingAvatarUnlockQueue = PendingAvatarUnlockQueue.Trim(),
				PlaytimeSeconds = Math.Max(0.0, PlaytimeSeconds),
				CreatedUnixTime = createdUnixTime,
				UpdatedUnixTime = updatedUnixTime,
			};
		}
	}

	public readonly record struct SaveSlotMetadata(
		string SlotId,
		bool Exists,
		string PlayerName,
		string SelectedAvatarId,
		int HighestCompletedStage,
		double PlaytimeSeconds,
		long UpdatedUnixTime);

	public readonly struct SaveValue
	{
		public SaveValue(string key, Variant value)
		{
			Key = key;
			Value = value;
		}

		public string Key { get; }
		public Variant Value { get; }
	}

	public static void EnsureInitialized()
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return;
		}

		EnsureInitialized(saveManager);
	}

	public static string GetProfileSlotId(int slotIndex)
	{
		return slotIndex >= 0 && slotIndex < ProfileSlotCount ? $"profile_{slotIndex}" : DefaultProfileSlotId;
	}

	public static IReadOnlyList<SaveSlotMetadata> ListProfiles()
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Array.Empty<SaveSlotMetadata>();
		}

		EnsureInitialized(saveManager);
		var profiles = new List<SaveSlotMetadata>(ProfileSlotCount);
		for (int index = 0; index < ProfileSlotCount; index++)
		{
			string slotId = GetProfileSlotId(index);
			bool exists = ProfileSlotFileExists(saveManager, slotId);
			GameProgressData progressData = exists ? LoadProfileRaw(saveManager, slotId) : GameProgressData.CreateDefault();
			profiles.Add(progressData.ToMetadata(slotId, exists));
		}

		return profiles;
	}

	public static GameProgressData LoadActiveProfile()
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return GameProgressData.CreateDefault();
		}

		EnsureInitialized(saveManager);
		return LoadProfileRaw(saveManager, GetActiveProfileSlotIdRaw(saveManager));
	}

	public static GameProgressData LoadProfile(string slotId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return GameProgressData.CreateDefault();
		}

		EnsureInitialized(saveManager);
		return LoadProfileRaw(saveManager, NormalizeProfileSlotId(slotId));
	}

	public static Error SaveActiveProfile(GameProgressData progressData)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureInitialized(saveManager);
		return SaveProfileRaw(saveManager, GetActiveProfileSlotIdRaw(saveManager), progressData);
	}

	public static Error SaveProfile(string slotId, GameProgressData progressData)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureInitialized(saveManager);
		return SaveProfileRaw(saveManager, NormalizeProfileSlotId(slotId), progressData);
	}

	public static Error CreateProfile(string slotId, string playerName, string selectedAvatarId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureInitialized(saveManager);
		string normalizedSlotId = NormalizeProfileSlotId(slotId);
		long now = GetUnixTime();
		var progressData = new GameProgressData(
			CurrentProgressSchemaVersion,
			0,
			selectedAvatarId,
			playerName,
			string.Empty,
			0.0,
			now,
			now);

		Error saveResult = SaveProfileRaw(saveManager, normalizedSlotId, progressData);
		if (saveResult != Error.Ok)
		{
			return saveResult;
		}

		return SetActiveProfileRaw(saveManager, normalizedSlotId);
	}

	public static Error SetActiveProfile(string slotId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureInitialized(saveManager);
		string normalizedSlotId = NormalizeProfileSlotId(slotId);
		if (!ProfileSlotFileExists(saveManager, normalizedSlotId))
		{
			return Error.FileNotFound;
		}

		return SetActiveProfileRaw(saveManager, normalizedSlotId);
	}

	public static Error DeleteProfile(string slotId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureInitialized(saveManager);
		string normalizedSlotId = NormalizeProfileSlotId(slotId);
		Error removeResult = RemoveProfileSlotFiles(saveManager, normalizedSlotId);
		if (removeResult != Error.Ok)
		{
			return removeResult;
		}

		saveManager.Call("unregister_slot", new StringName(normalizedSlotId));
		if (!string.Equals(GetActiveProfileSlotIdRaw(saveManager), normalizedSlotId, StringComparison.Ordinal))
		{
			return Error.Ok;
		}

		string nextSlotId = FindFirstExistingProfileSlotId(saveManager);
		if (string.IsNullOrEmpty(nextSlotId))
		{
			nextSlotId = DefaultProfileSlotId;
			Error saveDefaultResult = SaveProfileRaw(saveManager, nextSlotId, GameProgressData.CreateDefault());
			if (saveDefaultResult != Error.Ok)
			{
				return saveDefaultResult;
			}
		}

		return SetActiveProfileRaw(saveManager, nextSlotId);
	}

	public static float GetFloat(string key, float defaultValue)
	{
		return GetValue(key, defaultValue).AsSingle();
	}

	public static bool GetBool(string key, bool defaultValue)
	{
		return GetValue(key, defaultValue).AsBool();
	}

	public static int GetInt(string key, int defaultValue)
	{
		return GetValue(key, defaultValue).AsInt32();
	}

	public static string GetString(string key, string defaultValue)
	{
		return GetValue(key, defaultValue).AsString();
	}

	public static Godot.Collections.Dictionary GetDictionary(string key)
	{
		Variant value = GetValue(key, new Godot.Collections.Dictionary());
		return value.VariantType == Variant.Type.Dictionary
			? value.AsGodotDictionary()
			: new Godot.Collections.Dictionary();
	}

	public static bool HasValue(string key)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return false;
		}

		EnsureLegacyMigration(saveManager);
		return HasRawValue(saveManager, key);
	}

	public static Error SetValue(string key, Variant value)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureLegacyMigration(saveManager);
		SetRawValue(saveManager, key, value);
		return Persist(saveManager);
	}

	public static Error SetValues(params SaveValue[] values)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}

		EnsureLegacyMigration(saveManager);
		foreach (SaveValue value in values)
		{
			SetRawValue(saveManager, value.Key, value.Value);
		}

		return Persist(saveManager);
	}

	private static Variant GetValue(string key, Variant defaultValue)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return defaultValue;
		}

		EnsureLegacyMigration(saveManager);
		return GetRawValue(saveManager, key, defaultValue);
	}

	private static Node? GetSaveManager()
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			WarnMissingSaveManager();
			return null;
		}

		Node? saveManager = tree.Root.GetNodeOrNull<Node>(SaveManagerNodeName);
		if (saveManager == null)
		{
			WarnMissingSaveManager();
		}

		return saveManager;
	}

	private static void WarnMissingSaveManager()
	{
		if (_warnedMissingSaveManager)
		{
			return;
		}

		_warnedMissingSaveManager = true;
		GD.PushWarning("SaveManager autoload is missing; game save data could not be persisted through SaveState.");
	}

	private static void EnsureInitialized(Node saveManager)
	{
		EnsureLegacyMigration(saveManager);
		EnsureProgressProfileMigration(saveManager);
	}

	private static void EnsureLegacyMigration(Node? saveManager = null)
	{
		if (_attemptedLegacyMigration)
		{
			return;
		}

		saveManager ??= GetSaveManager();
		if (saveManager == null)
		{
			return;
		}

		_attemptedLegacyMigration = true;
		if (GetRawValue(saveManager, LegacyMigrationKey, false).AsBool())
		{
			return;
		}

		var legacyConfig = new ConfigFile();
		Error loadResult = legacyConfig.Load(LegacySettingsConfigPath);
		if (loadResult == Error.FileNotFound)
		{
			return;
		}

		if (loadResult != Error.Ok)
		{
			GD.PushWarning($"Failed to migrate legacy settings into SaveState: {loadResult}");
			return;
		}

		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "master_volume", MasterVolumeKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "music_volume", MusicVolumeKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "mute", MuteAudioKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "mute", MuteBgmKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "mute", MuteSfxKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "video", "fullscreen", FullscreenKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "video", "resolution", ResolutionKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "video", "ocean_motion", OceanMotionKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "video", "motion_scale", MotionScaleKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "general", "language", LanguageKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "general", "avatar", SelectedAvatarKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "general", "player_name", PlayerNameKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "highest_completed_stage", HighestCompletedStageKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "pending_avatar_unlock_queue", PendingAvatarUnlockQueueKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "pending_avatar_unlock", PendingAvatarUnlockKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "pending_avatar_unlock_stage", PendingAvatarUnlockStageKey);
		SetRawValue(saveManager, LegacyMigrationKey, true);

		Error saveResult = Persist(saveManager);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist migrated SaveState data: {saveResult}");
		}
	}

	private static void EnsureProgressProfileMigration(Node? saveManager = null)
	{
		if (_attemptedProgressProfileMigration)
		{
			return;
		}

		saveManager ??= GetSaveManager();
		if (saveManager == null)
		{
			return;
		}

		EnsureLegacyMigration(saveManager);
		_attemptedProgressProfileMigration = true;

		bool hasActiveProfile = HasRawValue(saveManager, ActiveProfileSlotKey);
		bool migrationComplete = GetRawValue(saveManager, ProgressProfileMigrationKey, false).AsBool();
		if (migrationComplete && hasActiveProfile)
		{
			return;
		}

		if (!ProfileSlotFileExists(saveManager, DefaultProfileSlotId))
		{
			Error saveProfileResult = SaveProfileRaw(saveManager, DefaultProfileSlotId, CreateProgressProfileFromLegacyKv(saveManager));
			if (saveProfileResult != Error.Ok)
			{
				GD.PushWarning($"Failed to migrate adventure progress into profile slot: {saveProfileResult}");
				return;
			}
		}

		if (!hasActiveProfile)
		{
			SetRawValue(saveManager, ActiveProfileSlotKey, DefaultProfileSlotId);
		}

		SetRawValue(saveManager, ProgressProfileMigrationKey, true);
		Error saveResult = Persist(saveManager);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist progress profile migration marker: {saveResult}");
		}
	}

	private static GameProgressData CreateProgressProfileFromLegacyKv(Node saveManager)
	{
		long now = GetUnixTime();
		string pendingQueue = GetRawValue(saveManager, PendingAvatarUnlockQueueKey, string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(pendingQueue))
		{
			pendingQueue = GetRawValue(saveManager, PendingAvatarUnlockKey, string.Empty).AsString();
		}

		return new GameProgressData(
			CurrentProgressSchemaVersion,
			GetRawValue(saveManager, HighestCompletedStageKey, 0).AsInt32(),
			GetRawValue(saveManager, SelectedAvatarKey, CharacterAvatarCatalog.DefaultAvatarId).AsString(),
			GetRawValue(saveManager, PlayerNameKey, PlayerNameRules.DefaultPlayerName).AsString(),
			pendingQueue,
			0.0,
			now,
			now).Normalize();
	}

	private static GameProgressData LoadProfileRaw(Node saveManager, string slotId)
	{
		EnsureProfileSlot(saveManager, slotId);
		if (!ProfileSlotFileExists(saveManager, slotId))
		{
			return GameProgressData.CreateDefault();
		}

		Godot.Collections.Dictionary data = saveManager.Call("load_from_slot_sync", new StringName(slotId)).AsGodotDictionary();
		if (data.Count == 0)
		{
			GD.PushWarning($"Profile save slot '{slotId}' is empty or unreadable; using default progress for this session.");
			return GameProgressData.CreateDefault();
		}

		return GameProgressData.FromDictionary(data, GameProgressData.CreateDefault());
	}

	private static Error SaveProfileRaw(Node saveManager, string slotId, GameProgressData progressData)
	{
		EnsureProfileSlot(saveManager, slotId);
		GameProgressData normalizedProgress = progressData.Normalize();
		long now = GetUnixTime();
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

	private static string GetActiveProfileSlotIdRaw(Node saveManager)
	{
		return NormalizeProfileSlotId(GetRawValue(saveManager, ActiveProfileSlotKey, DefaultProfileSlotId).AsString());
	}

	private static Error SetActiveProfileRaw(Node saveManager, string slotId)
	{
		SetRawValue(saveManager, ActiveProfileSlotKey, NormalizeProfileSlotId(slotId));
		return Persist(saveManager);
	}

	private static string FindFirstExistingProfileSlotId(Node saveManager)
	{
		for (int index = 0; index < ProfileSlotCount; index++)
		{
			string slotId = GetProfileSlotId(index);
			if (ProfileSlotFileExists(saveManager, slotId))
			{
				return slotId;
			}
		}

		return string.Empty;
	}

	private static string NormalizeProfileSlotId(string slotId)
	{
		for (int index = 0; index < ProfileSlotCount; index++)
		{
			string candidate = GetProfileSlotId(index);
			if (string.Equals(slotId, candidate, StringComparison.Ordinal))
			{
				return candidate;
			}
		}

		return DefaultProfileSlotId;
	}

	private static bool ProfileSlotFileExists(Node saveManager, string slotId)
	{
		EnsureProfileSlot(saveManager, slotId);
		return Godot.FileAccess.FileExists(GetProfileSlotPath(saveManager, slotId));
	}

	private static void EnsureProfileSlot(Node saveManager, string slotId)
	{
		saveManager.Call("ensure_slot_for_file_base", new StringName(slotId), slotId);
	}

	private static string GetProfileSlotPath(Node saveManager, string slotId)
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

	private static Error RemoveProfileSlotFiles(Node saveManager, string slotId)
	{
		string path = GetProfileSlotPath(saveManager, slotId);
		Error mainRemoveResult = RemoveFileIfExists(path);
		if (mainRemoveResult != Error.Ok)
		{
			return mainRemoveResult;
		}

		return RemoveFileIfExists($"{path}.bak");
	}

	private static Error RemoveFileIfExists(string path)
	{
		if (!Godot.FileAccess.FileExists(path))
		{
			return Error.Ok;
		}

		return DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
	}

	private static void TryMigrateLegacyKey(
		Node saveManager,
		ConfigFile legacyConfig,
		string legacySection,
		string legacyKey,
		string saveKey)
	{
		if (!legacyConfig.HasSectionKey(legacySection, legacyKey) || HasRawValue(saveManager, saveKey))
		{
			return;
		}

		SetRawValue(saveManager, saveKey, legacyConfig.GetValue(legacySection, legacyKey));
	}

	private static bool HasRawValue(Node saveManager, string key)
	{
		return GetRawValue(saveManager, key, MissingSentinel).AsString() != MissingSentinel;
	}

	private static Variant GetRawValue(Node saveManager, string key, Variant defaultValue)
	{
		return saveManager.Call("get_value", new StringName(key), defaultValue);
	}

	private static void SetRawValue(Node saveManager, string key, Variant value)
	{
		saveManager.Call("set_value", new StringName(key), value);
	}

	private static Error Persist(Node saveManager)
	{
		return (Error)saveManager.Call("persist").AsInt32();
	}

	private static Variant ReadDictionaryValue(Godot.Collections.Dictionary data, string key, Variant defaultValue)
	{
		if (data.ContainsKey(key))
		{
			return data[key];
		}

		var stringNameKey = new StringName(key);
		return data.ContainsKey(stringNameKey) ? data[stringNameKey] : defaultValue;
	}

	private static string ReadString(Godot.Collections.Dictionary data, string key, string defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsString();
	}

	private static int ReadInt(Godot.Collections.Dictionary data, string key, int defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsInt32();
	}

	private static long ReadLong(Godot.Collections.Dictionary data, string key, long defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsInt64();
	}

	private static double ReadDouble(Godot.Collections.Dictionary data, string key, double defaultValue)
	{
		return ReadDictionaryValue(data, key, defaultValue).AsDouble();
	}

	private static long GetUnixTime()
	{
		return (long)Time.GetUnixTimeFromSystem();
	}
}
