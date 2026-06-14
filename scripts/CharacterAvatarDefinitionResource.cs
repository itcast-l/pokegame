using Godot;

[GlobalClass]
public partial class CharacterAvatarDefinitionResource : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export] public string RootPath { get; set; } = string.Empty;
	[Export] public int IdleFrameCount { get; set; } = 1;
	[Export] public float IdleFrameSeconds { get; set; }
	[Export] public int ActionFrameCount { get; set; } = 4;
	[Export] public int AdventureStageNumber { get; set; }
	[Export] public string AdventureAbilityNameKey { get; set; } = "ADVENTURE_ABILITY_NONE_NAME";
	[Export] public string AdventureAbilityDescriptionKey { get; set; } = "ADVENTURE_ABILITY_NONE_DESC";
	[Export] public string AbilityIdsCsv { get; set; } = string.Empty;
	[Export] public string AbilityValuesRaw { get; set; } = string.Empty;
	[Export] public string DamageEffectFramePattern { get; set; } = string.Empty;
	[Export] public int DamageEffectFrameCount { get; set; }
	[Export] public bool DamageEffectPreferImageFile { get; set; }
	[Export] public string AttackFxRoot { get; set; } = string.Empty;
	[Export] public float AttackEffectFlightSeconds { get; set; }
	[Export] public string AttackEffectStyle { get; set; } = string.Empty;
	[Export] public string AccentColorHtml { get; set; } = string.Empty;
	[Export] public bool BossBorrowedAbilityFxEligible { get; set; } = true;
	[Export] public CharacterAudioConfig? AudioConfig { get; set; }

	public CharacterAvatarDefinition ToDefinition()
	{
		return new CharacterAvatarDefinition(
			Id,
			DisplayName,
			RootPath,
			IdleFrameCount,
			IdleFrameSeconds,
			ActionFrameCount,
			AdventureStageNumber,
			AdventureAbilityNameKey,
			AdventureAbilityDescriptionKey,
			AbilityIdsCsv,
			AbilityValuesRaw,
			DamageEffectFramePattern,
			DamageEffectFrameCount,
			DamageEffectPreferImageFile,
			AttackFxRoot,
			AttackEffectFlightSeconds,
			AttackEffectStyle,
			AccentColorHtml,
			BossBorrowedAbilityFxEligible,
			AudioConfig);
	}
}
