using System;
using System.Collections.Generic;
using Godot;

public static partial class GameSaveStore
{
	public const string MasterVolumeKey = "settings.audio.master_volume";
	public const string MusicVolumeKey = "settings.audio.music_volume";
	public const string MuteAudioKey = "settings.audio.mute";
	public const string MuteBgmKey = "settings.audio.mute_bgm";
	public const string MuteSfxKey = "settings.audio.mute_sfx";
	public const string FullscreenKey = "settings.video.fullscreen";
	public const string ResolutionKey = "settings.video.resolution";
	public const string LanguageKey = "settings.general.language";
	public const string MainMenuMusicTrackKey = "settings.audio.main_menu_music_track";
	public const string InputBindingsKey = "settings.input.bindings";
	public const string SelectedAvatarKey = "settings.general.avatar";
	public const string PlayerNameKey = "settings.general.player_name";
	public const string HighestCompletedMainAdventureStageKey = "adventure.highest_completed_main_adventure_stage";
	public const string CompletedAdventureTutorialIdsCsvKey = "adventure.completed_adventure_tutorial_ids_csv";
	// Legacy v1 key. It remains readable but is never written to a current profile.
	public const string HighestCompletedStageKey = "adventure.highest_completed_stage";
	public const string PendingAvatarUnlockQueueKey = "adventure.pending_avatar_unlock_queue";
	public const string PendingAvatarUnlockKey = "adventure.pending_avatar_unlock";
	public const string PendingAvatarUnlockStageKey = "adventure.pending_avatar_unlock_stage";
	public const string ActiveProfileSlotKey = "progress.active_profile_slot";
	public const string DefaultProfileSlotId = "profile_0";
	public const int ProfileSlotCount = 3;

	private static bool _attemptedLegacyMigration;
	private static bool _attemptedProgressProfileMigration;
	private static bool _warnedMissingSaveManager;

public sealed record GameProgressData(
		int SchemaVersion,
		int HighestCompletedMainAdventureStage,
		string CompletedAdventureTutorialIdsCsv,
		string SelectedAvatarId,
		string PlayerName,
		string PendingAvatarUnlockQueue,
		string PendingAdventureRunJson,
		string UnlockedSkillCodexUpgradeIdsCsv,
		double PlaytimeSeconds,
		long CreatedUnixTime,
		long UpdatedUnixTime)
	{
		public static GameProgressData CreateDefault()
		{
			long now = GameSaveStore.GetUnixTime();
			return new GameProgressData(
				GameSaveProfileRepository.CurrentProgressSchemaVersion,
				0,
				string.Empty,
				CharacterAvatarCatalog.DefaultAvatarId,
				PlayerNameRules.DefaultPlayerName,
				string.Empty,
				string.Empty,
				string.Empty,
				0.0,
				now,
				now);
		}

		public static GameProgressData FromDictionary(Godot.Collections.Dictionary data, GameProgressData fallback)
		{
			int schemaVersion = GameSaveProfileRepository.ReadInt(
				data,
				GameSaveProfileRepository.ProgressSchemaVersionKey,
				1);
			int legacyHighestStage = GameSaveProfileRepository.ReadInt(
				data,
				GameSaveProfileRepository.ProgressHighestCompletedStageKey,
				0);
			int mappedMainStage = AdventureCampaignCatalog.MapLegacyCompletedMainStage(legacyHighestStage);
			int highestMainStage = GameSaveProfileRepository.ReadInt(
				data,
				GameSaveProfileRepository.ProgressHighestCompletedMainAdventureStageKey,
				mappedMainStage);

			return new GameProgressData(
				schemaVersion,
				highestMainStage,
				GameSaveProfileRepository.ReadString(
					data,
					GameSaveProfileRepository.ProgressCompletedAdventureTutorialIdsCsvKey,
					fallback.CompletedAdventureTutorialIdsCsv),
				GameSaveProfileRepository.ReadString(data, GameSaveProfileRepository.ProgressSelectedAvatarIdKey, fallback.SelectedAvatarId),
				GameSaveProfileRepository.ReadString(data, GameSaveProfileRepository.ProgressPlayerNameKey, fallback.PlayerName),
				GameSaveProfileRepository.ReadString(data, GameSaveProfileRepository.ProgressPendingAvatarUnlockQueueKey, fallback.PendingAvatarUnlockQueue),
				GameSaveProfileRepository.ReadString(data, GameSaveProfileRepository.ProgressPendingAdventureRunJsonKey, fallback.PendingAdventureRunJson),
				schemaVersion >= 3
					? GameSaveProfileRepository.ReadString(
						data,
						GameSaveProfileRepository.ProgressUnlockedSkillCodexUpgradeIdsCsvKey,
						fallback.UnlockedSkillCodexUpgradeIdsCsv)
					: string.Empty,
				GameSaveProfileRepository.ReadDouble(data, GameSaveProfileRepository.ProgressPlaytimeSecondsKey, fallback.PlaytimeSeconds),
				GameSaveProfileRepository.ReadLong(data, GameSaveProfileRepository.ProgressCreatedUnixTimeKey, fallback.CreatedUnixTime),
				GameSaveProfileRepository.ReadLong(data, GameSaveProfileRepository.ProgressUpdatedUnixTimeKey, fallback.UpdatedUnixTime)).Normalize();
		}

		public Godot.Collections.Dictionary ToDictionary()
		{
			return new Godot.Collections.Dictionary
			{
				[GameSaveProfileRepository.ProgressSchemaVersionKey] = SchemaVersion,
				[GameSaveProfileRepository.ProgressHighestCompletedMainAdventureStageKey] = HighestCompletedMainAdventureStage,
				[GameSaveProfileRepository.ProgressCompletedAdventureTutorialIdsCsvKey] = CompletedAdventureTutorialIdsCsv,
				[GameSaveProfileRepository.ProgressSelectedAvatarIdKey] = SelectedAvatarId,
				[GameSaveProfileRepository.ProgressPlayerNameKey] = PlayerName,
				[GameSaveProfileRepository.ProgressPendingAvatarUnlockQueueKey] = PendingAvatarUnlockQueue,
				[GameSaveProfileRepository.ProgressPendingAdventureRunJsonKey] = PendingAdventureRunJson,
				[GameSaveProfileRepository.ProgressUnlockedSkillCodexUpgradeIdsCsvKey] = UnlockedSkillCodexUpgradeIdsCsv,
				[GameSaveProfileRepository.ProgressPlaytimeSecondsKey] = PlaytimeSeconds,
				[GameSaveProfileRepository.ProgressCreatedUnixTimeKey] = CreatedUnixTime,
				[GameSaveProfileRepository.ProgressUpdatedUnixTimeKey] = UpdatedUnixTime,
			};
		}

		public SaveSlotMetadata ToMetadata(string slotId, bool exists)
		{
			return new SaveSlotMetadata(
				slotId,
				exists,
				PlayerName,
				SelectedAvatarId,
				HighestCompletedMainAdventureStage,
				PlaytimeSeconds,
				UpdatedUnixTime);
		}

		public GameProgressData Normalize()
		{
			long now = GameSaveStore.GetUnixTime();
			long createdUnixTime = CreatedUnixTime > 0 ? CreatedUnixTime : now;
			long updatedUnixTime = UpdatedUnixTime > 0 ? UpdatedUnixTime : createdUnixTime;
			string selectedAvatarId = string.IsNullOrWhiteSpace(SelectedAvatarId)
				? CharacterAvatarCatalog.DefaultAvatarId
				: SelectedAvatarId.Trim();

			return this with
			{
				SchemaVersion = Math.Clamp(SchemaVersion, 1, GameSaveProfileRepository.CurrentProgressSchemaVersion),
				HighestCompletedMainAdventureStage = Math.Clamp(
					HighestCompletedMainAdventureStage,
					0,
					AdventureCampaignCatalog.MainStageCount),
				CompletedAdventureTutorialIdsCsv = AdventureCampaignCatalog.NormalizeCompletedTutorialIdsCsv(
					CompletedAdventureTutorialIdsCsv),
				SelectedAvatarId = selectedAvatarId,
				PlayerName = PlayerNameRules.Normalize(PlayerName),
				PendingAvatarUnlockQueue = AdventureCampaignCatalog.NormalizePendingAvatarUnlockQueueCsv(
					PendingAvatarUnlockQueue),
				PendingAdventureRunJson = string.IsNullOrWhiteSpace(PendingAdventureRunJson)
					? string.Empty
					: PendingAdventureRunJson,
				UnlockedSkillCodexUpgradeIdsCsv = SkillCodexUnlockRules.NormalizeUpgradeIdsCsv(
					UnlockedSkillCodexUpgradeIdsCsv),
				PlaytimeSeconds = Math.Max(0.0, PlaytimeSeconds),
				CreatedUnixTime = createdUnixTime,
				UpdatedUnixTime = updatedUnixTime,
			};
		}

		[Obsolete("Use HighestCompletedMainAdventureStage.")]
		public int HighestCompletedStage => HighestCompletedMainAdventureStage;
	}

	public readonly record struct SaveSlotMetadata(
		string SlotId,
		bool Exists,
		string PlayerName,
		string SelectedAvatarId,
		int HighestCompletedMainAdventureStage,
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
		if (saveManager != null)
		{
			GameSaveMigration.EnsureInitialized(saveManager);
		}
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

		GameSaveMigration.EnsureInitialized(saveManager);
		return GameSaveProfileRepository.ListProfiles(saveManager);
	}

	public static GameProgressData LoadActiveProfile()
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return GameProgressData.CreateDefault();
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		return GameSaveProfileRepository.LoadProfileRaw(saveManager, GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager));
	}

	public static GameProgressData LoadProfile(string slotId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return GameProgressData.CreateDefault();
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		return GameSaveProfileRepository.LoadProfileRaw(saveManager, GameSaveProfileRepository.NormalizeProfileSlotId(slotId));
	}

	public static Error SaveActiveProfile(GameProgressData progressData)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}
		using ProfileExclusiveLease? exclusiveLease =
			GameSaveProfileWriteCoordinator.TryAcquireExclusiveLease(out Error exclusiveError);
		if (exclusiveLease == null)
		{
			return exclusiveError;
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		string slotId = GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager);
		GameProgressData candidate = GameSaveProfileRepository.PrepareForSave(progressData);
		Error result = GameSaveProfileRepository.SavePreparedProfileRaw(saveManager, slotId, candidate);
		if (result == Error.Ok)
		{
			GameSaveProfileWriteCoordinator.ObserveCommitted(slotId, candidate);
		}
		return result;
	}

	public static Error SaveProfile(string slotId, GameProgressData progressData)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}
		using ProfileExclusiveLease? exclusiveLease =
			GameSaveProfileWriteCoordinator.TryAcquireExclusiveLease(out Error exclusiveError);
		if (exclusiveLease == null)
		{
			return exclusiveError;
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		string normalizedSlotId = GameSaveProfileRepository.NormalizeProfileSlotId(slotId);
		GameProgressData candidate = GameSaveProfileRepository.PrepareForSave(progressData);
		Error result = GameSaveProfileRepository.SavePreparedProfileRaw(saveManager, normalizedSlotId, candidate);
		if (result == Error.Ok
			&& string.Equals(
				GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager),
				normalizedSlotId,
				StringComparison.Ordinal))
		{
			GameSaveProfileWriteCoordinator.ObserveCommitted(normalizedSlotId, candidate);
		}
		return result;
	}

	public static Error CreateProfile(string slotId, string playerName, string selectedAvatarId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}
		using ProfileExclusiveLease? topologyLease =
			GameSaveProfileWriteCoordinator.TryAcquireExclusiveLease(out Error topologyError);
		if (topologyLease == null)
		{
			return topologyError;
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		string normalizedSlotId = GameSaveProfileRepository.NormalizeProfileSlotId(slotId);
		long now = GetUnixTime();
		var progressData = new GameProgressData(
			GameSaveProfileRepository.CurrentProgressSchemaVersion,
			0,
			string.Empty,
			selectedAvatarId,
			playerName,
			string.Empty,
			string.Empty,
			string.Empty,
			0.0,
			now,
			now);

		Error saveResult = GameSaveProfileRepository.SaveProfileRaw(saveManager, normalizedSlotId, progressData);
		if (saveResult != Error.Ok)
		{
			return saveResult;
		}

		Error activeResult = GameSaveProfileRepository.SetActiveProfileRaw(saveManager, normalizedSlotId);
		if (activeResult == Error.Ok)
		{
			GameSaveProfileWriteCoordinator.Invalidate();
		}
		return activeResult;
	}

	public static Error SetActiveProfile(string slotId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}
		using ProfileExclusiveLease? topologyLease =
			GameSaveProfileWriteCoordinator.TryAcquireExclusiveLease(out Error topologyError);
		if (topologyLease == null)
		{
			return topologyError;
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		string normalizedSlotId = GameSaveProfileRepository.NormalizeProfileSlotId(slotId);
		if (!GameSaveProfileRepository.ProfileSlotFileExists(saveManager, normalizedSlotId))
		{
			return Error.FileNotFound;
		}

		Error result = GameSaveProfileRepository.SetActiveProfileRaw(saveManager, normalizedSlotId);
		if (result == Error.Ok)
		{
			GameSaveProfileWriteCoordinator.Invalidate();
		}
		return result;
	}

	public static Error DeleteProfile(string slotId)
	{
		Node? saveManager = GetSaveManager();
		if (saveManager == null)
		{
			return Error.Unavailable;
		}
		using ProfileExclusiveLease? topologyLease =
			GameSaveProfileWriteCoordinator.TryAcquireExclusiveLease(out Error topologyError);
		if (topologyLease == null)
		{
			return topologyError;
		}

		GameSaveMigration.EnsureInitialized(saveManager);
		string normalizedSlotId = GameSaveProfileRepository.NormalizeProfileSlotId(slotId);
		string activeSlotId = GameSaveProfileRepository.GetActiveProfileSlotIdRaw(saveManager);
		bool deletingActiveSlot = string.Equals(activeSlotId, normalizedSlotId, StringComparison.Ordinal);
		if (deletingActiveSlot)
		{
			string replacementSlotId = GameSaveProfileRepository.FindFirstExistingProfileSlotId(saveManager, normalizedSlotId);
			if (string.IsNullOrEmpty(replacementSlotId))
			{
				replacementSlotId = GameSaveProfileRepository.FindFirstEmptyProfileSlotId(saveManager, normalizedSlotId);
				if (string.IsNullOrEmpty(replacementSlotId))
				{
					return Error.Unavailable;
				}

				Error saveReplacementResult = GameSaveProfileRepository.SaveProfileRaw(saveManager, replacementSlotId, GameProgressData.CreateDefault());
				if (saveReplacementResult != Error.Ok)
				{
					return saveReplacementResult;
				}
			}

			Error setActiveResult = GameSaveProfileRepository.SetActiveProfileRaw(saveManager, replacementSlotId);
			if (setActiveResult != Error.Ok)
			{
				return setActiveResult;
			}
		}

		Error removeResult = GameSaveProfileRepository.RemoveProfileSlotFiles(saveManager, normalizedSlotId);
		if (removeResult != Error.Ok)
		{
			return removeResult;
		}

		saveManager.Call("unregister_slot", new StringName(normalizedSlotId));
		GameSaveProfileWriteCoordinator.Invalidate();
		return Error.Ok;
	}

	public static float GetFloat(string key, float defaultValue)
		=> GameSaveLegacyAdapter.GetValue(key, defaultValue).AsSingle();

	public static bool GetBool(string key, bool defaultValue)
		=> GameSaveLegacyAdapter.GetValue(key, defaultValue).AsBool();

	public static int GetInt(string key, int defaultValue)
		=> GameSaveLegacyAdapter.GetValue(key, defaultValue).AsInt32();

	public static string GetString(string key, string defaultValue)
		=> GameSaveLegacyAdapter.GetValue(key, defaultValue).AsString();

	public static Godot.Collections.Dictionary GetDictionary(string key)
	{
		Variant value = GameSaveLegacyAdapter.GetValue(key, new Godot.Collections.Dictionary());
		return value.VariantType == Variant.Type.Dictionary ? value.AsGodotDictionary() : new Godot.Collections.Dictionary();
	}

	public static bool HasValue(string key)
		=> GameSaveLegacyAdapter.HasValue(key);

	public static Error SetValue(string key, Variant value)
		=> GameSaveLegacyAdapter.SetValue(key, value);

	public static Error SetValues(params SaveValue[] values)
		=> GameSaveLegacyAdapter.SetValues(values);


	internal static Node? GetSaveManager()
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			WarnMissingSaveManager();
			return null;
		}

		Node? saveManager = tree.Root.GetNodeOrNull<Node>("SaveManager");
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

	internal static long GetUnixTime()
	{
		return (long)Time.GetUnixTimeFromSystem();
	}

	internal static bool AttemptedLegacyMigration
	{
		get => _attemptedLegacyMigration;
		set => _attemptedLegacyMigration = value;
	}

	internal static bool AttemptedProgressProfileMigration
	{
		get => _attemptedProgressProfileMigration;
		set => _attemptedProgressProfileMigration = value;
	}

}
