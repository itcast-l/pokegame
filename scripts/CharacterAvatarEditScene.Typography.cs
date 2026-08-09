using Godot;

public partial class CharacterAvatarEditScene
{
	private const float AvatarEditTitleFontWeight = 760.0f;
	private const float AvatarEditSectionFontWeight = 680.0f;
	private const float AvatarEditInteractiveFontWeight = 640.0f;
	private const float AvatarEditSupportingFontWeight = 600.0f;
	private static readonly Color AvatarEditFontOutline = new(0.0f, 0.025f, 0.045f, 0.92f);
	private static readonly Color AvatarEditKickerColor = new(0.95f, 0.82f, 0.43f, 1.0f);
	private static readonly Color AvatarEditHintColor = new(0.76f, 1.0f, 0.95f, 1.0f);

	private void ApplyAvatarEditTypography()
	{
		Font titleFont = LocalizationTypography.CreateCurrentFontVariation(
			AvatarEditTitleFontWeight,
			1.15f);
		Font sectionFont = LocalizationTypography.CreateCurrentFontVariation(
			AvatarEditSectionFontWeight,
			0.86f);
		Font interactiveFont = LocalizationTypography.CreateCurrentFontVariation(
			AvatarEditInteractiveFontWeight,
			0.70f);
		Font supportingFont = LocalizationTypography.CreateCurrentFontVariation(
			AvatarEditSupportingFontWeight,
			0.52f);

		ApplyAvatarEditTextStyle(_titleLabel, titleFont, 30, 2);
		ApplyAvatarEditTextStyle(_backButton, interactiveFont, 18, 1);
		ApplyAvatarEditTextStyle(_confirmButton, interactiveFont, 18, 1);
		ApplyAvatarEditTextStyle(_previewKickerLabel, sectionFont, 16, 1);
		ApplyAvatarEditTextStyle(_avatarListKickerLabel, sectionFont, 18, 1);
		ApplyAvatarEditTextStyle(_playerNameKickerLabel, sectionFont, 16, 1);
		ApplyAvatarEditTextStyle(_playerNameInput, interactiveFont, 19, 1);
		ApplyAvatarEditTextStyle(_playerNameHint, supportingFont, 15, 1);
		_titleLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_previewKickerLabel.AddThemeColorOverride("font_color", AvatarEditKickerColor);
		_avatarListKickerLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_playerNameKickerLabel.AddThemeColorOverride("font_color", AvatarEditKickerColor);
		_playerNameInput.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.PauseDerivedBodyText);
		_playerNameInput.AddThemeColorOverride("font_placeholder_color", AvatarEditHintColor);
		_playerNameHint.AddThemeColorOverride("font_color", AvatarEditHintColor);
	}

	private static void ApplyAvatarEditTextStyle(
		Control control,
		Font font,
		int fontSize,
		int outlineSize)
	{
		control.AddThemeFontOverride("font", font);
		control.AddThemeFontSizeOverride("font_size", fontSize);
		control.AddThemeColorOverride("font_outline_color", AvatarEditFontOutline);
		control.AddThemeConstantOverride("outline_size", outlineSize);
	}
}
