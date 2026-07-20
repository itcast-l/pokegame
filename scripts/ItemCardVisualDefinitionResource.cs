using Godot;

public readonly record struct ItemCardVisualDefinition(
	string Id,
	string SpriteName,
	string ArtRunName,
	string FramePathPattern,
	int FrameCount,
	int SortOrder,
	string AttachmentOverlayTexturePath,
	int AttachmentOverlayPriority,
	string Icon);

[GlobalClass]
public partial class ItemCardVisualDefinitionResource : Resource
{
	[Export] public string Id { get; set; } = string.Empty;
	[Export] public string SpriteName { get; set; } = string.Empty;
	[Export] public string ArtRunName { get; set; } = "run-004";
	[Export] public string FramePathPattern { get; set; } = string.Empty;
	[Export] public int FrameCount { get; set; } = 4;
	[Export] public int SortOrder { get; set; } = 1000;
	[Export] public string AttachmentOverlayTexturePath { get; set; } = string.Empty;
	[Export] public int AttachmentOverlayPriority { get; set; }
	[Export] public string Icon { get; set; } = string.Empty;

	public ItemCardVisualDefinition ToDefinition()
	{
		return new ItemCardVisualDefinition(
			Id,
			SpriteName,
			ArtRunName,
			FramePathPattern,
			FrameCount,
			SortOrder,
			AttachmentOverlayTexturePath,
			AttachmentOverlayPriority,
			Icon);
	}
}
