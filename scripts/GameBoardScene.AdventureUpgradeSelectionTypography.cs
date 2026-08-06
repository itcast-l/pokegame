using Godot;

public partial class GameBoardScene
{
	private const float AdventureUpgradeSelectionTitleFontWeight = 740.0f;

	private static void ApplyAdventureUpgradeSelectionTitleTypography(Label title)
	{
		Font titleFont = LocalizationTypography.CreateCurrentFontVariation(
			AdventureUpgradeSelectionTitleFontWeight,
			0.9f);
		title.AddThemeFontOverride("font", titleFont);
		title.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.035f, 0.055f, 0.96f));
		title.AddThemeConstantOverride("outline_size", 2);
	}
}
