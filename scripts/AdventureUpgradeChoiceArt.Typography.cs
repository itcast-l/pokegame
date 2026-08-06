using Godot;

public partial class AdventureUpgradeChoiceArt
{
	private const float UpgradeDescriptionFontWeight = 560.0f;
	private Font? _descriptionFont;

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			ApplyUpgradeChoiceTypography();
			LayoutLayers();
		}
	}

	private void ApplyUpgradeChoiceTypography()
	{
		if (_descriptionLayer == null)
		{
			return;
		}

		_descriptionFont = LocalizationTypography.CreateCurrentFontVariation(
			UpgradeDescriptionFontWeight,
			0.38f);
		_descriptionLayer.AddThemeFontOverride("font", _descriptionFont);
		_descriptionLayer.AddThemeColorOverride("font_color", new Color(0.97f, 0.99f, 0.97f, 1.0f));
		_descriptionLayer.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.045f, 0.06f, 0.92f));
		_descriptionLayer.AddThemeConstantOverride("outline_size", 1);
	}
}
