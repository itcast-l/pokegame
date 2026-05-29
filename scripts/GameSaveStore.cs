using Godot;

public static class GameSaveStore
{
	public const string MasterVolumeKey = "settings.audio.master_volume";
	public const string MusicVolumeKey = "settings.audio.music_volume";
	public const string MuteAudioKey = "settings.audio.mute";
	public const string FullscreenKey = "settings.video.fullscreen";
	public const string ResolutionKey = "settings.video.resolution";
	public const string OceanMotionKey = "settings.video.ocean_motion";
	public const string MotionScaleKey = "settings.video.motion_scale";
	public const string LanguageKey = "settings.general.language";
	public const string SelectedAvatarKey = "settings.general.avatar";
	public const string PlayerNameKey = "settings.general.player_name";
	public const string HighestCompletedStageKey = "adventure.highest_completed_stage";
	public const string PendingAvatarUnlockQueueKey = "adventure.pending_avatar_unlock_queue";
	public const string PendingAvatarUnlockKey = "adventure.pending_avatar_unlock";
	public const string PendingAvatarUnlockStageKey = "adventure.pending_avatar_unlock_stage";

	private const string SaveManagerNodeName = "SaveManager";
	private const string LegacySettingsConfigPath = "user://settings.cfg";
	private const string LegacyMigrationKey = "migration.settings_cfg.v1";
	private const string MissingSentinel = "__pokegame_save_missing_2B1E5D5F__";

	private static bool _attemptedLegacyMigration;
	private static bool _warnedMissingSaveManager;

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
		EnsureLegacyMigration();
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
}
