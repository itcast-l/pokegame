using System;
using System.Collections.Generic;

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

