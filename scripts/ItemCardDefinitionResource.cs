using Godot;

[GlobalClass]
public partial class ItemCardDefinitionResource : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string DisplayName { get; set; } = string.Empty;
	[Export] public string SpriteName { get; set; } = string.Empty;
	[Export] public string DescriptionKey { get; set; } = string.Empty;
	[Export] public bool EnabledInGame { get; set; } = true;
	[Export] public bool PlayableFromHand { get; set; } = true;
	[Export] public bool CanBeQuestionReward { get; set; } = true;
	[Export] public string ArtRunName { get; set; } = "run-004";
	[Export] public string FramePathPattern { get; set; } = string.Empty;
	[Export] public int FrameCount { get; set; } = 4;
	[Export] public int SortOrder { get; set; } = 1000;
	[Export] public string Rarity { get; set; } = "common";
	[Export] public string TagsCsv { get; set; } = string.Empty;
	[Export] public string AttachmentOverlayTexturePath { get; set; } = string.Empty;
	[Export] public int AttachmentOverlayPriority { get; set; }
	[Export] public string PlayPresentation { get; set; } = CardPlayPresentationTypes.DirectTrigger;
	[Export] public string Trigger { get; set; } = CardGameEvents.OnPlay;
	[Export] public string EffectType { get; set; } = CardEffectTypes.None;
	[Export] public string EffectValuesRaw { get; set; } = string.Empty;
	[Export] public int Priority { get; set; }
	[Export] public string AttachedTrigger { get; set; } = string.Empty;
	[Export] public string AttachedEffectType { get; set; } = CardEffectTypes.None;
	[Export] public string AttachedEffectValuesRaw { get; set; } = string.Empty;
	[Export] public int AttachedPriority { get; set; }
	[Export] public string Icon { get; set; } = string.Empty;
	[Export] public int Weight { get; set; } = 100;
	[Export(PropertyHint.MultilineText)] public string Description { get; set; } = string.Empty;

	public ItemCardDefinition ToDefinition()
	{
		return new ItemCardDefinition(
			Id,
			DisplayName,
			SpriteName,
			DescriptionKey,
			EnabledInGame,
			PlayableFromHand,
			CanBeQuestionReward,
			ArtRunName,
			FramePathPattern,
			FrameCount,
			SortOrder,
			Rarity,
			TagsCsv,
			AttachmentOverlayTexturePath,
			AttachmentOverlayPriority,
			PlayPresentation,
			Trigger,
			EffectType,
			EffectValuesRaw,
			Priority,
			AttachedTrigger,
			AttachedEffectType,
			AttachedEffectValuesRaw,
			AttachedPriority,
			Icon,
			Weight,
			Description);
	}
}
