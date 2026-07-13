using System;
using Godot;

internal static class GameSaveMigration
{
	internal const string LegacySettingsConfigPath = "user://settings.cfg";
	internal const string LegacyMigrationKey = "migration.settings_cfg.v1";
	internal const string ProgressProfileMigrationKey = "migration.progress_profile.v1";

	internal static void EnsureInitialized(Node saveManager)
	{
		EnsureLegacyMigration(saveManager);
		EnsureProgressProfileMigration(saveManager);
	}

internal static void EnsureLegacyMigration(Node? saveManager = null)
	{
		if (GameSaveStore.AttemptedLegacyMigration)
		{
			return;
		}

		saveManager ??= GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return;
		}

		GameSaveStore.AttemptedLegacyMigration = true;
		if (GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveMigration.LegacyMigrationKey, false).AsBool())
		{
			return;
		}

		var legacyConfig = new ConfigFile();
		Error loadResult = legacyConfig.Load(GameSaveMigration.LegacySettingsConfigPath);
		if (loadResult == Error.FileNotFound)
		{
			return;
		}

		if (loadResult != Error.Ok)
		{
			GD.PushWarning($"Failed to migrate legacy settings into SaveState: {loadResult}");
			return;
		}

		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "master_volume", GameSaveStore.MasterVolumeKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "music_volume", GameSaveStore.MusicVolumeKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "mute", GameSaveStore.MuteAudioKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "mute", GameSaveStore.MuteBgmKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "audio", "mute", GameSaveStore.MuteSfxKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "video", "fullscreen", GameSaveStore.FullscreenKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "video", "resolution", GameSaveStore.ResolutionKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "general", "language", GameSaveStore.LanguageKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "general", "avatar", GameSaveStore.SelectedAvatarKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "general", "player_name", GameSaveStore.PlayerNameKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "highest_completed_stage", GameSaveStore.HighestCompletedStageKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "pending_avatar_unlock_queue", GameSaveStore.PendingAvatarUnlockQueueKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "pending_avatar_unlock", GameSaveStore.PendingAvatarUnlockKey);
		TryMigrateLegacyKey(saveManager, legacyConfig, "adventure", "pending_avatar_unlock_stage", GameSaveStore.PendingAvatarUnlockStageKey);
		GameSaveLegacyAdapter.SetRawValue(saveManager, GameSaveMigration.LegacyMigrationKey, true);

		Error saveResult = GameSaveLegacyAdapter.Persist(saveManager);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist migrated SaveState data: {saveResult}");
		}
	}

internal static void EnsureProgressProfileMigration(Node? saveManager = null)
	{
		if (GameSaveStore.AttemptedProgressProfileMigration)
		{
			return;
		}

		saveManager ??= GameSaveStore.GetSaveManager();
		if (saveManager == null)
		{
			return;
		}

		EnsureLegacyMigration(saveManager);
		GameSaveStore.AttemptedProgressProfileMigration = true;

		bool hasActiveProfile = GameSaveLegacyAdapter.HasRawValue(saveManager, GameSaveStore.ActiveProfileSlotKey);
		bool migrationComplete = GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveMigration.ProgressProfileMigrationKey, false).AsBool();
		if (migrationComplete && hasActiveProfile)
		{
			return;
		}

		if (!GameSaveProfileRepository.ProfileSlotFileExists(saveManager, GameSaveStore.DefaultProfileSlotId))
		{
			Error saveProfileResult = GameSaveProfileRepository.SaveProfileRaw(saveManager, GameSaveStore.DefaultProfileSlotId, CreateProgressProfileFromLegacyKv(saveManager));
			if (saveProfileResult != Error.Ok)
			{
				GD.PushWarning($"Failed to migrate adventure progress into profile slot: {saveProfileResult}");
				return;
			}
		}

		if (!hasActiveProfile)
		{
			GameSaveLegacyAdapter.SetRawValue(saveManager, GameSaveStore.ActiveProfileSlotKey, GameSaveStore.DefaultProfileSlotId);
		}

		GameSaveLegacyAdapter.SetRawValue(saveManager, GameSaveMigration.ProgressProfileMigrationKey, true);
		Error saveResult = GameSaveLegacyAdapter.Persist(saveManager);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to persist progress profile migration marker: {saveResult}");
		}
	}

internal static GameSaveStore.GameProgressData CreateProgressProfileFromLegacyKv(Node saveManager)
	{
		long now = GameSaveStore.GetUnixTime();
		string pendingQueue = GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveStore.PendingAvatarUnlockQueueKey, string.Empty).AsString();
		if (string.IsNullOrWhiteSpace(pendingQueue))
		{
			pendingQueue = GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveStore.PendingAvatarUnlockKey, string.Empty).AsString();
		}

		return new GameSaveStore.GameProgressData(
			GameSaveProfileRepository.CurrentProgressSchemaVersion,
			GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveStore.HighestCompletedStageKey, 0).AsInt32(),
			GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveStore.SelectedAvatarKey, CharacterAvatarCatalog.DefaultAvatarId).AsString(),
			GameSaveLegacyAdapter.GetRawValue(saveManager, GameSaveStore.PlayerNameKey, PlayerNameRules.DefaultPlayerName).AsString(),
			pendingQueue,
			string.Empty,
			0.0,
			now,
			now).Normalize();
	}

internal static void TryMigrateLegacyKey(
		Node saveManager,
		ConfigFile legacyConfig,
		string legacySection,
		string legacyKey,
		string saveKey)
	{
		if (!legacyConfig.HasSectionKey(legacySection, legacyKey) || GameSaveLegacyAdapter.HasRawValue(saveManager, saveKey))
		{
			return;
		}

		GameSaveLegacyAdapter.SetRawValue(saveManager, saveKey, legacyConfig.GetValue(legacySection, legacyKey));
	}
}
