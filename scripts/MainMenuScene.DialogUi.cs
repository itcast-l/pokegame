using System;
using Godot;

public partial class MainMenuScene : Control
{
	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? GetMainMenuLocalizationFallback(key) : text;
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private Label CreateLocalizedDialogLabel(string key, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = CreateDialogLabel(Localize(key), fontSize, color, alignment);
		_localizedLabels.Add((label, key));
		return label;
	}

	private Control CreateLocalizedDialogButton(string key, Color accentColor, Action pressedAction, bool flipOrnament, float width = 190.0f)
	{
		var frame = CreateDialogButton(Localize(key), accentColor, pressedAction, flipOrnament, width);
		if (frame.GetNodeOrNull<Button>("Button") is Button button)
		{
			_localizedButtons.Add((button, key));
		}

		return frame;
	}

	private void RefreshLocalizedText()
	{
		if (_titleLabel != null)
		{
			_titleLabel.Text = Localize("APP_NAME");
		}

		if (_subtitleLabel != null)
		{
			_subtitleLabel.Text = Localize("APP_SUBTITLE");
		}

		if (_singlePlayerButton != null)
		{
			_singlePlayerButton.Text = Localize("MENU_SINGLE_PLAYER");
		}

		if (_adventureButton != null)
		{
			_adventureButton.Text = Localize("MENU_ADVENTURE");
		}

		if (_multiplayerButton != null)
		{
			_multiplayerButton.Text = Localize("MENU_MULTIPLAYER");
		}

		if (_settingsButton != null)
		{
			_settingsButton.Text = Localize("MENU_SETTINGS");
		}

		if (_exitButton != null)
		{
			_exitButton.Text = Localize("MENU_EXIT");
		}

		for (int index = _localizedLabels.Count - 1; index >= 0; index--)
		{
			(Label label, string key) = _localizedLabels[index];
			if (!GodotObject.IsInstanceValid(label))
			{
				_localizedLabels.RemoveAt(index);
				continue;
			}

			label.Text = Localize(key);
		}

		for (int index = _localizedButtons.Count - 1; index >= 0; index--)
		{
			(Button button, string key) = _localizedButtons[index];
			if (!GodotObject.IsInstanceValid(button))
			{
				_localizedButtons.RemoveAt(index);
				continue;
			}

			button.Text = Localize(key);
		}

		RefreshWindowModeOptions();
		RefreshResolutionOptions();
		RefreshLanguageOptions();
		RefreshPlayerCountDialogOptions();
		RefreshAvatarUnlockPromptText();
	}

	private Label CreateDialogLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		return label;
	}

	private Control CreateDialogButton(string text, Color accentColor, Action pressedAction, bool flipOrnament, float width = 190.0f)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{text}Frame",
			CustomMinimumSize = new Vector2(width, 64.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
		};

		var button = new Button
		{
			Name = "Button",
			Text = text,
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.OffsetLeft = flipOrnament ? 26.0f : 4.0f;
		button.OffsetTop = 5.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -26.0f;
		button.OffsetBottom = -5.0f;
		button.AddThemeFontSizeOverride("font_size", 20);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		button.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		button.AddThemeConstantOverride("shadow_offset_x", 1);
		button.AddThemeConstantOverride("shadow_offset_y", 2);
		StyleBoxEmpty emptyStyle = new();
		button.AddThemeStyleboxOverride("normal", emptyStyle);
		button.AddThemeStyleboxOverride("hover", emptyStyle);
		button.AddThemeStyleboxOverride("pressed", emptyStyle);
		button.AddThemeStyleboxOverride("focus", emptyStyle);
		button.Pressed += pressedAction;

		frame.AddChild(button);
		return frame;
	}

	private static Texture2D? LoadTextureWithImageFallback(string path)
	{
		Texture2D? texture = GD.Load<Texture2D>(path);
		if (texture != null)
		{
			return texture;
		}

		Image image = Image.LoadFromFile(path);
		return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
	}

	private static StyleBoxFlat CreateDialogPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.012f, 0.14f, 0.18f, 0.9f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.62f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.68f),
			ShadowSize = 26,
			ShadowOffset = new Vector2(0.0f, 10.0f),
		};
	}

	private static StyleBoxFlat CreateSettingsRowStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.01f, 0.18f, 0.22f, 0.48f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.16f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
	}

	private static StyleBoxFlat CreateSliderTrackStyle(bool filled)
	{
		return new StyleBoxFlat
		{
			BgColor = filled
				? new Color(0.56f, 1.0f, 0.92f, 0.52f)
				: new Color(0.0f, 0.08f, 0.12f, 0.72f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, filled ? 0.28f : 0.18f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
		};
	}

	private static StyleBoxFlat CreateSliderGrabberStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(1.0f, 1.0f, 0.78f, 0.95f)
				: new Color(0.84f, 1.0f, 0.96f, 0.92f),
			BorderColor = new Color(0.08f, 0.34f, 0.38f, 0.9f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
		};
	}

	private static StyleBoxFlat CreateOptionButtonStyle(bool hovered)
	{
		return new StyleBoxFlat
		{
			BgColor = hovered
				? new Color(0.07f, 0.37f, 0.42f, 0.92f)
				: new Color(0.015f, 0.18f, 0.23f, 0.78f),
			BorderColor = hovered
				? new Color(0.82f, 1.0f, 0.98f, 0.82f)
				: new Color(0.62f, 1.0f, 0.94f, 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ContentMarginLeft = 12,
			ContentMarginRight = 12,
			ContentMarginTop = 7,
			ContentMarginBottom = 7,
		};
	}

	private static StyleBoxFlat CreateDropdownPopupPanelStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.006f, 0.105f, 0.135f, 0.96f),
			BorderColor = new Color(0.62f, 1.0f, 0.94f, 0.48f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 7,
			CornerRadiusTopRight = 7,
			CornerRadiusBottomLeft = 7,
			CornerRadiusBottomRight = 7,
			ContentMarginLeft = 8,
			ContentMarginRight = 8,
			ContentMarginTop = 8,
			ContentMarginBottom = 8,
			ShadowColor = new Color(0.0f, 0.015f, 0.03f, 0.74f),
			ShadowSize = 18,
			ShadowOffset = new Vector2(0.0f, 8.0f),
		};
	}

	private static StyleBoxFlat CreateDropdownPopupHoverStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.66f, 0.68f, 0.82f),
			BorderColor = new Color(0.94f, 1.0f, 0.78f, 0.86f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 5,
			CornerRadiusTopRight = 5,
			CornerRadiusBottomLeft = 5,
			CornerRadiusBottomRight = 5,
			ContentMarginLeft = 6,
			ContentMarginRight = 6,
			ContentMarginTop = 5,
			ContentMarginBottom = 5,
			ExpandMarginLeft = 3.0f,
			ExpandMarginRight = 3.0f,
			ExpandMarginTop = 2.0f,
			ExpandMarginBottom = 2.0f,
		};
	}

	private static void StyleDropdownOptionButton(OptionButton option)
	{
		option.AddThemeFontSizeOverride("font_size", 17);
		option.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		option.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		option.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		option.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		option.AddThemeColorOverride("icon_normal_color", new Color(0.7f, 1.0f, 0.92f, 0.78f));
		option.AddThemeColorOverride("icon_hover_color", new Color(1.0f, 1.0f, 0.78f, 0.95f));
		option.AddThemeColorOverride("icon_pressed_color", new Color(0.78f, 1.0f, 0.96f, 0.95f));
		option.AddThemeConstantOverride("shadow_offset_x", 1);
		option.AddThemeConstantOverride("shadow_offset_y", 2);
		option.AddThemeConstantOverride("arrow_margin", 12);
		option.AddThemeConstantOverride("modulate_arrow", 1);
		option.AddThemeStyleboxOverride("normal", CreateOptionButtonStyle(false));
		option.AddThemeStyleboxOverride("hover", CreateOptionButtonStyle(true));
		option.AddThemeStyleboxOverride("pressed", CreateOptionButtonStyle(true));
		option.AddThemeStyleboxOverride("focus", CreateOptionButtonStyle(true));

		PopupMenu popup = option.GetPopup();
		popup.PreferNativeMenu = false;
		popup.ShrinkWidth = false;
		popup.ShrinkHeight = true;
		popup.MinSize = new Vector2I(Mathf.RoundToInt(option.CustomMinimumSize.X), 0);
		popup.AddThemeFontSizeOverride("font_size", 18);
		popup.AddThemeColorOverride("font_color", new Color(0.9f, 1.0f, 0.96f));
		popup.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		popup.AddThemeColorOverride("font_disabled_color", new Color(0.54f, 0.76f, 0.75f, 0.58f));
		popup.AddThemeColorOverride("font_separator_color", new Color(0.62f, 1.0f, 0.94f, 0.54f));
		popup.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.025f, 0.045f, 0.86f));
		popup.AddThemeConstantOverride("outline_size", 2);
		popup.AddThemeConstantOverride("h_separation", 8);
		popup.AddThemeConstantOverride("v_separation", 8);
		popup.AddThemeConstantOverride("item_start_padding", 14);
		popup.AddThemeConstantOverride("item_end_padding", 16);
		popup.AddThemeStyleboxOverride("panel", CreateDropdownPopupPanelStyle());
		popup.AddThemeStyleboxOverride("hover", CreateDropdownPopupHoverStyle());
		popup.WindowInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseMotion)
			{
				FocusDropdownPopupItemAtMousePosition(popup, popup.GetMousePosition());
			}
		};
	}

	private static void FocusDropdownPopupItemAtMousePosition(PopupMenu popup, Vector2 mousePosition)
	{
		if (popup.ItemCount <= 0)
		{
			return;
		}

		const float panelVerticalMargin = 8.0f;
		float listHeight = Mathf.Max(popup.Size.Y - panelVerticalMargin * 2.0f, 1.0f);
		float itemHeight = listHeight / popup.ItemCount;
		int itemIndex = Mathf.FloorToInt((mousePosition.Y - panelVerticalMargin) / itemHeight);
		if (itemIndex < 0 || itemIndex >= popup.ItemCount)
		{
			return;
		}

		if (popup.IsItemDisabled(itemIndex) || popup.IsItemSeparator(itemIndex))
		{
			return;
		}

		if (popup.GetFocusedItem() != itemIndex)
		{
			popup.SetFocusedItem(itemIndex);
		}
	}

	private static StyleBoxFlat CreatePlayerCountBadgeStyle(Color accentColor)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.13f),
			BorderColor = new Color(accentColor.R, accentColor.G, accentColor.B, 0.36f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
		};
	}

	private static void DrawPearlDivider(Control target)
	{
		Vector2 center = target.Size * 0.5f;
		float lineWidth = Mathf.Max(target.Size.X * 0.34f, 80.0f);
		Color lineColor = new(0.58f, 1.0f, 0.92f, 0.22f);
		target.DrawLine(center - new Vector2(lineWidth, 0.0f), center - new Vector2(14.0f, 0.0f), lineColor, 1.4f, true);
		target.DrawLine(center + new Vector2(14.0f, 0.0f), center + new Vector2(lineWidth, 0.0f), lineColor, 1.4f, true);
		target.DrawCircle(center, 7.0f, new Color(0.88f, 1.0f, 0.94f, 0.82f));
		target.DrawCircle(center - new Vector2(2.5f, 2.5f), 2.3f, new Color(1.0f, 1.0f, 0.86f, 0.88f));
	}
}
