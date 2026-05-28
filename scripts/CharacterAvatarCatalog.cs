using System;
using System.Collections.Generic;
using Godot;

public static class AdventureEnemyAbilities
{
	public const string TideGuard = "tide_guard";
	public const string BubbleCover = "bubble_cover";
	public const string TideShift = "tide_shift";
	public const string SandAmbush = "sand_ambush";
	public const string ColorCurrent = "color_current";
	public const string SpiralShell = "spiral_shell";
	public const string TuskBreak = "tusk_break";
	public const string LeafyMirage = "leafy_mirage";
	public const string DragonPrince = "dragon_prince";
	public const string DragonQueen = "dragon_queen";
	public const string DragonKing = "dragon_king";
}

public static class AdventureAttackEffectStyles
{
	public const string FishBeam = "fish_beam";
	public const string CrabPincer = "crab_pincer";
	public const string JellyfishTentacle = "jellyfish_tentacle";
	public const string MantaRayCyclone = "manta_ray_cyclone";
	public const string SeaSlugMarker = "sea_slug_marker";
	public const string GardenEelBurrow = "garden_eel_burrow";
	public const string LeafySeadragonSlash = "leafy_seadragon_slash";
	public const string DragonPrinceHit = "dragon_prince_hit";
	public const string DragonQueenJaw = "dragon_queen_jaw";
	public const string BossKRoyalSeizure = "boss_k_royal_seizure";
}

public static class AdventureEnemyAbilityValueKeys
{
	public const string ScoreReductionPerCard = "score_reduction_per_card";
	public const string CapturesBeforeSandBite = "captures_before_sand_bite";
	public const string TideShiftMinTableCards = "tide_shift_min_table_cards";
	public const string MissesBeforeSlimeCoat = "misses_before_slime_coat";
	public const string CapturesBeforeBomb = "captures_before_bomb";
	public const string CapturesBeforeDouble = "captures_before_double";
	public const string RoyalCaptureThreshold = "royal_capture_threshold";
	public const string DragonQueenCaptureThreshold = "dragon_queen_capture_threshold";
	public const string CapturesBeforeSteal = "captures_before_steal";
	public const string BladeBreakTableCardThreshold = "blade_break_table_card_threshold";
	public const string BladeBreakCooldownTurns = "blade_break_cooldown_turns";
	public const string BombItemCardId = "bomb_item_card_id";
	public const string DoubleItemCardId = "double_item_card_id";
	public const string SpecialJackRank = "special_jack_rank";
	public const string SpecialJackTexturePath = "special_jack_texture_path";
}

public sealed class CharacterAvatarDefinition
{
	public CharacterAvatarDefinition(
		string id,
		string displayName,
		string rootPath,
		int idleFrameCount,
		int actionFrameCount = 4,
		int adventureStageNumber = 0,
		string adventureAbilityNameKey = "ADVENTURE_ABILITY_NONE_NAME",
		string adventureAbilityDescriptionKey = "ADVENTURE_ABILITY_NONE_DESC",
		string abilityIdsCsv = "",
		string abilityValuesRaw = "",
		string damageEffectFramePattern = "",
		int damageEffectFrameCount = 0,
		bool damageEffectPreferImageFile = false,
		string attackFxRoot = "",
		float attackEffectFlightSeconds = 0.0f,
		string attackEffectStyle = "",
		string accentColorHtml = "",
		bool bossBorrowedAbilityFxEligible = true)
	{
		Id = id;
		DisplayName = displayName;
		RootPath = rootPath;
		IdleFrameCount = idleFrameCount;
		ActionFrameCount = actionFrameCount;
		AdventureStageNumber = adventureStageNumber;
		AdventureAbilityNameKey = adventureAbilityNameKey;
		AdventureAbilityDescriptionKey = adventureAbilityDescriptionKey;
		AbilityIdsCsv = abilityIdsCsv;
		AbilityIds = ParseAbilityIds(abilityIdsCsv);
		AbilityValuesRaw = abilityValuesRaw;
		AbilityValues = CardEffectValues.Parse(abilityValuesRaw);
		DamageEffectFramePattern = damageEffectFramePattern;
		DamageEffectFrameCount = damageEffectFrameCount;
		DamageEffectPreferImageFile = damageEffectPreferImageFile;
		AttackFxRoot = attackFxRoot;
		AttackEffectFlightSeconds = attackEffectFlightSeconds;
		AttackEffectStyle = attackEffectStyle;
		AccentColorHtml = accentColorHtml;
		BossBorrowedAbilityFxEligible = bossBorrowedAbilityFxEligible;
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string RootPath { get; }
	public int IdleFrameCount { get; }
	public int ActionFrameCount { get; }
	public int AdventureStageNumber { get; }
	public string AdventureAbilityNameKey { get; }
	public string AdventureAbilityDescriptionKey { get; }
	public string AbilityIdsCsv { get; }
	public IReadOnlyList<string> AbilityIds { get; }
	public string AbilityValuesRaw { get; }
	public CardEffectValues AbilityValues { get; }
	public string DamageEffectFramePattern { get; }
	public int DamageEffectFrameCount { get; }
	public bool DamageEffectPreferImageFile { get; }
	public string AttackFxRoot { get; }
	public float AttackEffectFlightSeconds { get; }
	public string AttackEffectStyle { get; }
	public string AccentColorHtml { get; }
	public bool BossBorrowedAbilityFxEligible { get; }

	public bool HasAbility(string abilityId)
	{
		foreach (string existingAbilityId in AbilityIds)
		{
			if (string.Equals(existingAbilityId, abilityId, StringComparison.Ordinal))
			{
				return true;
			}
		}

		return false;
	}

	public int GetAbilityValueInt(string key, int fallback)
	{
		return AbilityValues.GetInt(key, fallback);
	}

	public string GetAbilityValueString(string key, string fallback)
	{
		return AbilityValues.GetString(key, fallback);
	}

	private static IReadOnlyList<string> ParseAbilityIds(string abilityIdsCsv)
	{
		return string.IsNullOrWhiteSpace(abilityIdsCsv)
			? Array.Empty<string>()
			: abilityIdsCsv.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}
}

public static class CharacterAvatarCatalog
{
	public const string SettingsConfigPath = "user://settings.cfg";
	public const string DefaultAvatarId = "cat";
	private const bool UnlockAllAdventureStagesForTesting = false;
	private const string AdventureProgressSection = "adventure";
	private const string HighestCompletedStageKey = "highest_completed_stage";
	private const string PendingAvatarUnlockQueueKey = "pending_avatar_unlock_queue";
	private const string LegacyPendingAvatarUnlockKey = "pending_avatar_unlock";
	private const string LegacyPendingAvatarUnlockStageKey = "pending_avatar_unlock_stage";
	private const string AvatarResourceDirectory = "res://resources/character_avatars";

	private static CharacterAvatarDefinition[]? _avatars;
	private static readonly CharacterAvatarDefinition EmptyDefault = new(DefaultAvatarId, DefaultAvatarId, string.Empty, 1);

	private static CharacterAvatarDefinition[] Avatars => _avatars ??= LoadAvatarDefinitions();

	public static IReadOnlyList<CharacterAvatarDefinition> All => Avatars;
	public static CharacterAvatarDefinition Default => Avatars.Length > 0 ? Avatars[0] : EmptyDefault;

	public static CharacterAvatarDefinition GetById(string avatarId)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.Id, avatarId, StringComparison.Ordinal)
				|| string.Equals(avatar.DisplayName, avatarId, StringComparison.Ordinal))
			{
				return avatar;
			}
		}

		return Default;
	}

	public static bool TryGetAvatarIdForAbility(string abilityId, out string avatarId, bool requireBossBorrowedFxEligible = false)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (!avatar.HasAbility(abilityId)
				|| (requireBossBorrowedFxEligible && !avatar.BossBorrowedAbilityFxEligible))
			{
				continue;
			}

			avatarId = avatar.Id;
			return true;
		}

		avatarId = string.Empty;
		return false;
	}

	public static string NormalizeAvatarId(string avatarId)
	{
		return GetById(avatarId).Id;
	}

	public static string NormalizeUnlockedAvatarId(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		return IsAvatarUnlocked(normalized) ? normalized : DefaultAvatarId;
	}

	public static IReadOnlyList<CharacterAvatarDefinition> GetUnlockedAvatars()
	{
		var unlockedAvatars = new List<CharacterAvatarDefinition> { Default };
		int highestCompletedStage = LoadHighestCompletedAdventureStage();
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (avatar.AdventureStageNumber > 0 && avatar.AdventureStageNumber <= highestCompletedStage)
			{
				unlockedAvatars.Add(avatar);
			}
		}

		return unlockedAvatars;
	}

	public static bool IsAvatarUnlocked(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		if (normalized == DefaultAvatarId)
		{
			return true;
		}

		int stageNumber = GetAdventureStageNumber(normalized);
		return stageNumber > 0 && stageNumber <= LoadHighestCompletedAdventureStage();
	}

	public static int GetAdventureStageCount()
	{
		int stageCount = 0;
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			stageCount = Math.Max(stageCount, avatar.AdventureStageNumber);
		}

		return stageCount;
	}

	public static bool IsAdventureStageCompleted(int stageNumber)
	{
		return stageNumber > 0 && stageNumber <= LoadHighestCompletedAdventureStage();
	}

	public static bool IsAdventureStageAvailable(int stageNumber)
	{
		return stageNumber > 0
			&& stageNumber <= GetAdventureStageCount()
			&& stageNumber == LoadHighestCompletedAdventureStage() + 1;
	}

	public static bool IsAdventureStageUnlocked(int stageNumber)
	{
		return stageNumber > 0
			&& stageNumber <= GetAdventureStageCount()
			&& (UnlockAllAdventureStagesForTesting || stageNumber <= LoadHighestCompletedAdventureStage() + 1);
	}

	public static int GetAdventureStageNumber(string avatarId)
	{
		string normalized = NormalizeAvatarId(avatarId);
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.Id, normalized, StringComparison.Ordinal))
			{
				return avatar.AdventureStageNumber;
			}
		}

		return 0;
	}

	public static string GetAdventureAvatarIdForStage(int stageNumber)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (avatar.AdventureStageNumber == stageNumber)
			{
				return avatar.Id;
			}
		}

		return DefaultAvatarId;
	}

	public static int LoadHighestCompletedAdventureStage()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return 0;
		}

		int highestCompletedStage = config.GetValue(AdventureProgressSection, HighestCompletedStageKey, 0).AsInt32();
		return Mathf.Clamp(highestCompletedStage, 0, GetAdventureStageCount());
	}

	public static void RecordAdventureVictory(string enemyAvatarId)
	{
		string normalizedAvatarId = NormalizeAvatarId(enemyAvatarId);
		int stageNumber = GetAdventureStageNumber(normalizedAvatarId);
		if (stageNumber <= 0)
		{
			return;
		}

		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok && loadResult != Error.FileNotFound)
		{
			GD.PushWarning($"Failed to load settings for adventure progress: {loadResult}");
			return;
		}

		int previousHighestStage = config.GetValue(AdventureProgressSection, HighestCompletedStageKey, 0).AsInt32();
		if (stageNumber <= previousHighestStage)
		{
			return;
		}

		config.SetValue(AdventureProgressSection, HighestCompletedStageKey, stageNumber);
		var pendingUnlocks = LoadPendingAdventureAvatarUnlockQueue(config);
		pendingUnlocks.Add(normalizedAvatarId);
		SavePendingAdventureAvatarUnlockQueue(config, pendingUnlocks);
		Error saveResult = config.Save(SettingsConfigPath);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to save adventure progress: {saveResult}");
		}
	}

	public static string ConsumePendingAdventureAvatarUnlock()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return string.Empty;
		}

		List<string> pendingUnlocks = LoadPendingAdventureAvatarUnlockQueue(config);
		if (pendingUnlocks.Count == 0)
		{
			return string.Empty;
		}

		while (pendingUnlocks.Count > 0)
		{
			string avatarId = pendingUnlocks[0];
			pendingUnlocks.RemoveAt(0);
			string normalizedAvatarId = NormalizeAvatarId(avatarId);
			int stageNumber = GetAdventureStageNumber(normalizedAvatarId);
			if (stageNumber > 0 && stageNumber <= LoadHighestCompletedAdventureStage())
			{
				SavePendingAdventureAvatarUnlockQueue(config, pendingUnlocks);
				Error consumeSaveResult = config.Save(SettingsConfigPath);
				if (consumeSaveResult != Error.Ok)
				{
					GD.PushWarning($"Failed to clear pending adventure avatar unlock: {consumeSaveResult}");
				}

				return normalizedAvatarId;
			}
		}

		SavePendingAdventureAvatarUnlockQueue(config, pendingUnlocks);
		Error saveResult = config.Save(SettingsConfigPath);
		if (saveResult != Error.Ok)
		{
			GD.PushWarning($"Failed to clear pending adventure avatar unlock: {saveResult}");
		}

		return string.Empty;
	}

	public static int GetIdleFrameCount(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.IdleFrameCount;
			}
		}

		return Default.IdleFrameCount;
	}

	public static int GetActionFrameCount(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.ActionFrameCount;
			}
		}

		return Default.ActionFrameCount;
	}

	public static string GetAttackFxRoot(string avatarRoot)
	{
		foreach (CharacterAvatarDefinition avatar in Avatars)
		{
			if (string.Equals(avatar.RootPath, avatarRoot, StringComparison.Ordinal))
			{
				return avatar.AttackFxRoot;
			}
		}

		return string.Empty;
	}

	public static CharacterAvatarDefinition LoadSelectedAvatar()
	{
		var config = new ConfigFile();
		Error loadResult = config.Load(SettingsConfigPath);
		if (loadResult != Error.Ok)
		{
			return Default;
		}

		string avatarId = config.GetValue("general", "avatar", DefaultAvatarId).AsString();
		return GetById(NormalizeUnlockedAvatarId(avatarId));
	}

	private static CharacterAvatarDefinition[] LoadAvatarDefinitions()
	{
		var definitions = new List<CharacterAvatarDefinition>();
		using DirAccess? directory = DirAccess.Open(AvatarResourceDirectory);
		if (directory == null)
		{
			GD.PushWarning($"Missing character avatar resource directory: {AvatarResourceDirectory}");
			return Array.Empty<CharacterAvatarDefinition>();
		}

		directory.ListDirBegin();
		while (true)
		{
			string fileName = directory.GetNext();
			if (string.IsNullOrEmpty(fileName))
			{
				break;
			}

			if (directory.CurrentIsDir() || !TryGetResourceFileName(fileName, out string resourceFileName))
			{
				continue;
			}

			string path = $"{AvatarResourceDirectory}/{resourceFileName}";
			CharacterAvatarDefinition? definition = LoadAvatarDefinition(path);
			if (definition == null)
			{
				GD.PushWarning($"Invalid character avatar definition resource: {path}");
				continue;
			}

			if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.RootPath))
			{
				GD.PushWarning($"Character avatar definition resource is missing id or root path: {path}");
				continue;
			}

			definitions.Add(definition);
		}

		if (definitions.Count == 0)
		{
			GD.PushWarning($"No character avatar definitions found in {AvatarResourceDirectory}");
			return Array.Empty<CharacterAvatarDefinition>();
		}

		definitions.Sort((a, b) =>
		{
			int stageComparison = a.AdventureStageNumber.CompareTo(b.AdventureStageNumber);
			return stageComparison != 0 ? stageComparison : string.CompareOrdinal(a.Id, b.Id);
		});
		return definitions.ToArray();
	}

	private static bool TryGetResourceFileName(string fileName, out string resourceFileName)
	{
		const string ResourceExtension = ".tres";
		const string RemapExtension = ".remap";

		if (fileName.EndsWith(ResourceExtension, StringComparison.OrdinalIgnoreCase))
		{
			resourceFileName = fileName;
			return true;
		}

		if (fileName.EndsWith(ResourceExtension + RemapExtension, StringComparison.OrdinalIgnoreCase))
		{
			resourceFileName = fileName[..^RemapExtension.Length];
			return true;
		}

		resourceFileName = string.Empty;
		return false;
	}

	private static List<string> LoadPendingAdventureAvatarUnlockQueue(ConfigFile config)
	{
		string rawQueue = config.GetValue(AdventureProgressSection, PendingAvatarUnlockQueueKey, string.Empty).AsString();
		List<string> queue = ParsePendingAdventureAvatarUnlockQueue(rawQueue);
		if (queue.Count > 0)
		{
			return queue;
		}

		string legacyAvatarId = config.GetValue(AdventureProgressSection, LegacyPendingAvatarUnlockKey, string.Empty).AsString();
		if (!string.IsNullOrWhiteSpace(legacyAvatarId))
		{
			queue.Add(legacyAvatarId);
		}

		return queue;
	}

	private static List<string> ParsePendingAdventureAvatarUnlockQueue(string rawQueue)
	{
		if (string.IsNullOrWhiteSpace(rawQueue))
		{
			return new List<string>();
		}

		return new List<string>(rawQueue.Split(new[] { '|', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
	}

	private static void SavePendingAdventureAvatarUnlockQueue(ConfigFile config, IReadOnlyList<string> queue)
	{
		if (queue.Count > 0)
		{
			config.SetValue(AdventureProgressSection, PendingAvatarUnlockQueueKey, string.Join("|", queue));
		}
		else
		{
			config.SetValue(AdventureProgressSection, PendingAvatarUnlockQueueKey, string.Empty);
		}

		config.SetValue(AdventureProgressSection, LegacyPendingAvatarUnlockKey, string.Empty);
		config.SetValue(AdventureProgressSection, LegacyPendingAvatarUnlockStageKey, 0);
	}

	private static CharacterAvatarDefinition? LoadAvatarDefinition(string path)
	{
		Resource? resource = GD.Load(path);
		if (resource is CharacterAvatarDefinitionResource avatarResource)
		{
			return avatarResource.ToDefinition();
		}

		if (resource == null)
		{
			return null;
		}

		return new CharacterAvatarDefinition(
			GetResourceString(resource, "Id"),
			GetResourceString(resource, "DisplayName"),
			GetResourceString(resource, "RootPath"),
			GetResourceInt(resource, "IdleFrameCount", 1),
			GetResourceInt(resource, "ActionFrameCount", 4),
			GetResourceInt(resource, "AdventureStageNumber"),
			GetResourceString(resource, "AdventureAbilityNameKey", "ADVENTURE_ABILITY_NONE_NAME"),
			GetResourceString(resource, "AdventureAbilityDescriptionKey", "ADVENTURE_ABILITY_NONE_DESC"),
			GetResourceString(resource, "AbilityIdsCsv"),
			GetResourceString(resource, "AbilityValuesRaw"),
			GetResourceString(resource, "DamageEffectFramePattern"),
			GetResourceInt(resource, "DamageEffectFrameCount"),
			GetResourceBool(resource, "DamageEffectPreferImageFile"),
			GetResourceString(resource, "AttackFxRoot"),
			GetResourceFloat(resource, "AttackEffectFlightSeconds"),
			GetResourceString(resource, "AttackEffectStyle"),
			GetResourceString(resource, "AccentColorHtml"),
			GetResourceBool(resource, "BossBorrowedAbilityFxEligible", true));
	}

	private static string GetResourceString(Resource resource, string propertyName, string fallback = "")
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
	}

	private static int GetResourceInt(Resource resource, string propertyName, int fallback = 0)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
	}

	private static float GetResourceFloat(Resource resource, string propertyName, float fallback = 0.0f)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsSingle();
	}

	private static bool GetResourceBool(Resource resource, string propertyName, bool fallback = false)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
	}

}
