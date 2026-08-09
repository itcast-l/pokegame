using Godot;

public partial class AdventureAvatarMenuScene
{
	private const float AdventureTitleFontWeight = 740.0f;
	private const float AdventureDisplayFontWeight = 660.0f;
	private const float AdventureInteractiveFontWeight = 610.0f;
	private const float AdventureBodyFontWeight = 540.0f;
	private static readonly Color AdventureFontOutline = new(0.0f, 0.03f, 0.05f, 0.92f);

	private void ApplyAdventureTypography()
	{
		Font titleFont = LocalizationTypography.CreateCurrentFontVariation(AdventureTitleFontWeight, 0.9f);
		Font displayFont = LocalizationTypography.CreateCurrentFontVariation(AdventureDisplayFontWeight, 0.62f);
		Font interactiveFont = LocalizationTypography.CreateCurrentFontVariation(AdventureInteractiveFontWeight, 0.48f);
		Font bodyFont = LocalizationTypography.CreateCurrentFontVariation(AdventureBodyFontWeight, 0.34f);

		ApplyAdventureTextStyle(GetNode<Label>("%TitleLabel"), titleFont, 2);
		ApplyAdventureTextStyle(_backButton, interactiveFont, 1);
		ApplyAdventureTextStyle(_startButton, interactiveFont, 1);
		ApplyAdventureTextStyle(GetNode<Label>("%AdventureListTitle"), bodyFont, 1);
		ApplyAdventureTextStyle(GetNode<Label>("%EnemyListTitle"), bodyFont, 1);

		ApplyAdventureTextStyle(_previewStageLabel, interactiveFont, 1);
		ApplyAdventureTextStyle(_previewNameLabel, displayFont, 1);
		ApplyAdventureTextStyle(_previewStatusLabel, interactiveFont, 1);

		foreach (OceanMenuButtonFrame frame in _adventureFrames.Values)
		{
			ApplyAdventureTextStyle(
				frame.GetNode<Label>("AdventureEntryContent/AdventureName"),
				interactiveFont,
				1);
		}

		foreach (OceanMenuButtonFrame frame in _avatarFrames.Values)
		{
			ApplyAdventureTextStyle(frame.GetNode<Label>("EntryContent/EntryRow/StageBadge/StageNumber"), interactiveFont, 1);
			ApplyAdventureTextStyle(frame.GetNode<Label>("EntryContent/EntryRow/EntryName"), displayFont, 1);
			ApplyAdventureTextStyle(frame.GetNode<Label>("EntryContent/EntryRow/EntryStatus"), bodyFont, 1);
			ApplyAdventureTextStyle(frame.GetNode<Label>("EntryContent/EntryRow/EntryArrow"), bodyFont, 1);
		}
	}

	private static void ApplyAdventureTextStyle(Control control, Font font, int outlineSize)
	{
		control.AddThemeFontOverride("font", font);
		control.AddThemeColorOverride("font_outline_color", AdventureFontOutline);
		control.AddThemeConstantOverride("outline_size", outlineSize);
	}
}
