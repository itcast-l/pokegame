using System.Globalization;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private const string AvatarUnlockPromptTexturePath = "res://art/ui/avatar_unlock_prompt/run-003/processed/clean.png";
	private const float AvatarUnlockPromptFadeSeconds = 0.18f;

	private void ShowPendingAdventureAvatarUnlockPrompt()
	{
		if (_isShowingAvatarUnlockPrompt || _isStartingAdventure || _isReturningToMainMenu || !Visible)
		{
			return;
		}

		string unlockedAvatarId = CharacterAvatarCatalog.ConsumePendingAdventureAvatarUnlock();
		if (string.IsNullOrWhiteSpace(unlockedAvatarId))
		{
			return;
		}

		CharacterAvatarDefinition unlockedAvatar = CharacterAvatarCatalog.GetById(unlockedAvatarId);
		if (unlockedAvatar.AdventureStageNumber <= 0)
		{
			return;
		}

		ShowAvatarUnlockPrompt(unlockedAvatar);
	}

	private void ShowAvatarUnlockPrompt(CharacterAvatarDefinition avatarDefinition)
	{
		EnsureAvatarUnlockPrompt();
		if (_avatarUnlockPromptRoot == null)
		{
			return;
		}

		_isShowingAvatarUnlockPrompt = true;
		RefreshAvatarUnlockPromptText();
		SetAdventureMenuButtonsDisabled(true);

		if (_avatarUnlockPromptAvatar != null && GodotObject.IsInstanceValid(_avatarUnlockPromptAvatar))
		{
			_avatarUnlockPromptAvatar.AvatarRoot = avatarDefinition.RootPath;
			_avatarUnlockPromptAvatar.AccentColor = GetAvatarUnlockAccentColor(avatarDefinition);
			_avatarUnlockPromptAvatar.Visible = true;
			_avatarUnlockPromptAvatar.PlayVictory();
		}

		_avatarUnlockPromptTween?.Kill();
		_avatarUnlockPromptRoot.Visible = true;
		_avatarUnlockPromptRoot.MouseFilter = MouseFilterEnum.Stop;
		_avatarUnlockPromptRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptPanel.Scale = new Vector2(0.94f, 0.94f);
			_avatarUnlockPromptPanel.PivotOffset = _avatarUnlockPromptPanel.CustomMinimumSize / 2.0f;
		}

		_avatarUnlockPromptTween = CreateTween();
		_avatarUnlockPromptTween.SetTrans(Tween.TransitionType.Cubic);
		_avatarUnlockPromptTween.SetEase(Tween.EaseType.Out);
		_avatarUnlockPromptTween.TweenProperty(_avatarUnlockPromptRoot, "modulate:a", 1.0f, AvatarUnlockPromptFadeSeconds);
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptTween.Parallel().TweenProperty(_avatarUnlockPromptPanel, "scale", Vector2.One, AvatarUnlockPromptFadeSeconds);
		}

		_avatarUnlockPromptButton?.GrabFocus();
	}

	private void HideAvatarUnlockPrompt()
	{
		HideAvatarUnlockPrompt(showNext: true);
	}

	private void HideAvatarUnlockPrompt(bool showNext)
	{
		if (_avatarUnlockPromptRoot == null || !GodotObject.IsInstanceValid(_avatarUnlockPromptRoot))
		{
			return;
		}

		_avatarUnlockPromptTween?.Kill();
		_avatarUnlockPromptTween = null;
		_avatarUnlockPromptRoot.Visible = false;
		_avatarUnlockPromptRoot.MouseFilter = MouseFilterEnum.Ignore;
		_avatarUnlockPromptRoot.Modulate = Colors.White;
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptPanel.Scale = Vector2.One;
		}

		_isShowingAvatarUnlockPrompt = false;
		SetAdventureMenuButtonsDisabled(false);
		if (showNext)
		{
			ShowPendingAdventureAvatarUnlockPrompt();
		}
	}

	private void EnsureAvatarUnlockPrompt()
	{
		if (_avatarUnlockPromptRoot != null && GodotObject.IsInstanceValid(_avatarUnlockPromptRoot))
		{
			return;
		}

		_avatarUnlockPromptRoot = new Control
		{
			Name = "AvatarUnlockPromptRoot",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 1500,
		};
		_avatarUnlockPromptRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_avatarUnlockPromptRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.025f, 0.05f, 0.50f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HideAvatarUnlockPrompt();
				GetViewport().SetInputAsHandled();
			}
		};
		_avatarUnlockPromptRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "PromptCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		center.OffsetTop = -18.0f;
		center.OffsetBottom = -18.0f;
		_avatarUnlockPromptRoot.AddChild(center);

		_avatarUnlockPromptPanel = BuildAvatarUnlockPromptPanel();
		center.AddChild(_avatarUnlockPromptPanel);
	}

	private Control BuildAvatarUnlockPromptPanel()
	{
		var panel = new Control
		{
			Name = "AvatarUnlockPromptPanel",
			CustomMinimumSize = new Vector2(560.0f, 500.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};

		Texture2D? frameTexture = LoadTextureWithImageFallback(AvatarUnlockPromptTexturePath);
		if (frameTexture != null)
		{
			var frameArt = new TextureRect
			{
				Name = "GeneratedUnlockFrame",
				Texture = frameTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			frameArt.SetAnchorsPreset(LayoutPreset.FullRect);
			frameArt.OffsetLeft = -10.0f;
			frameArt.OffsetTop = -12.0f;
			frameArt.OffsetRight = 10.0f;
			frameArt.OffsetBottom = 12.0f;
			panel.AddChild(frameArt);
		}
		else
		{
			var fallbackPanel = new Panel
			{
				Name = "FallbackPanel",
				MouseFilter = MouseFilterEnum.Ignore,
			};
			fallbackPanel.SetAnchorsPreset(LayoutPreset.FullRect);
			fallbackPanel.AddThemeStyleboxOverride("panel", CreateMenuPanelStyle());
			panel.AddChild(fallbackPanel);
			GD.PushWarning($"Missing avatar unlock prompt texture: {AvatarUnlockPromptTexturePath}");
		}

		var margin = new MarginContainer
		{
			Name = "PromptMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.SetAnchorsPreset(LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 76);
		margin.AddThemeConstantOverride("margin_top", 64);
		margin.AddThemeConstantOverride("margin_right", 76);
		margin.AddThemeConstantOverride("margin_bottom", 58);
		panel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "PromptLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 14);
		margin.AddChild(layout);

		_avatarUnlockPromptTitleLabel = CreatePromptLabel(string.Empty, 32, new Color(1.0f, 0.94f, 0.58f), HorizontalAlignment.Center);
		layout.AddChild(_avatarUnlockPromptTitleLabel);

		var avatarSlot = new CenterContainer
		{
			Name = "AvatarSlot",
			CustomMinimumSize = new Vector2(0.0f, 172.0f),
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddChild(avatarSlot);
		_avatarUnlockPromptAvatar = CreateAvatarUnlockPromptAvatar();
		if (_avatarUnlockPromptAvatar != null)
		{
			avatarSlot.AddChild(_avatarUnlockPromptAvatar);
		}

		var actions = new HBoxContainer
		{
			Name = "PromptActions",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddChild(actions);
		Control confirmFrame = CreatePromptButton(Localize("AVATAR_UNLOCK_CONFIRM"), new Color(0.58f, 1.0f, 0.92f), HideAvatarUnlockPrompt, true, 170.0f);
		_avatarUnlockPromptButton = confirmFrame.GetNodeOrNull<Button>("Button");
		if (_avatarUnlockPromptButton != null)
		{
			_avatarUnlockPromptButton.FocusMode = FocusModeEnum.All;
		}
		actions.AddChild(confirmFrame);

		return panel;
	}

	private CharacterAvatar? CreateAvatarUnlockPromptAvatar()
	{
		if (_characterAvatarScene?.Instantiate() is not CharacterAvatar avatar)
		{
			return null;
		}

		avatar.Name = "UnlockedAvatar";
		avatar.CustomMinimumSize = new Vector2(156.0f, 156.0f);
		avatar.PortraitScale = 0.92f;
		avatar.ShowFrame = true;
		avatar.LoadActionFrames = true;
		avatar.MouseFilter = MouseFilterEnum.Ignore;
		return avatar;
	}

	private Label CreatePromptLabel(string text, int fontSize, Color color, HorizontalAlignment alignment)
	{
		var label = new Label
		{
			Text = text,
			HorizontalAlignment = alignment,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		label.AddThemeFontSizeOverride("font_size", fontSize);
		label.AddThemeColorOverride("font_color", color);
		label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.9f));
		label.AddThemeConstantOverride("shadow_offset_x", 1);
		label.AddThemeConstantOverride("shadow_offset_y", 2);
		return label;
	}

	private Control CreatePromptButton(string text, Color accentColor, System.Action pressedAction, bool flipOrnament, float width)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{text}Frame",
			CustomMinimumSize = new Vector2(width, 56.0f),
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
		button.OffsetLeft = flipOrnament ? 22.0f : 4.0f;
		button.OffsetTop = 5.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -22.0f;
		button.OffsetBottom = -5.0f;
		button.AddThemeFontSizeOverride("font_size", 19);
		button.AddThemeColorOverride("font_color", new Color(0.92f, 1.0f, 0.97f));
		button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 1.0f, 0.78f));
		button.AddThemeColorOverride("font_pressed_color", new Color(0.78f, 1.0f, 0.96f));
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.Pressed += pressedAction;
		frame.AddChild(button);
		return frame;
	}

	private void RefreshAvatarUnlockPromptText()
	{
		if (_avatarUnlockPromptTitleLabel != null && GodotObject.IsInstanceValid(_avatarUnlockPromptTitleLabel))
		{
			_avatarUnlockPromptTitleLabel.Text = Localize("AVATAR_UNLOCK_TITLE");
		}

		if (_avatarUnlockPromptButton != null && GodotObject.IsInstanceValid(_avatarUnlockPromptButton))
		{
			_avatarUnlockPromptButton.Text = Localize("AVATAR_UNLOCK_CONFIRM");
		}
	}

	private void SetAdventureMenuButtonsDisabled(bool disabled)
	{
		_backButton.Disabled = disabled;
		_startButton.Disabled = disabled;
		foreach (Button adventureButton in _adventureButtons.Values)
		{
			if (GodotObject.IsInstanceValid(adventureButton))
			{
				adventureButton.Disabled = disabled;
			}
		}

		foreach (Button stageButton in _stageButtons.Values)
		{
			if (GodotObject.IsInstanceValid(stageButton))
			{
				stageButton.Disabled = disabled;
			}
		}
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

	private static string GetAdventureMenuLocalizationFallback(string key)
	{
		string locale = TranslationServer.GetLocale();
		bool traditionalChinese = locale.StartsWith("zh_TW", System.StringComparison.OrdinalIgnoreCase)
			|| locale.StartsWith("zh_HK", System.StringComparison.OrdinalIgnoreCase);
		bool simplifiedChinese = locale.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase) && !traditionalChinese;
		return key switch
		{
			"ADVENTURE_MENU_TITLE" => traditionalChinese ? "冒險模式" : simplifiedChinese ? "冒险模式" : "Adventure",
			"ADVENTURE_LIST_TITLE" => traditionalChinese ? "冒險" : simplifiedChinese ? "冒险" : "Adventure",
			"ADVENTURE_ENEMY_LIST_TITLE" => traditionalChinese ? "敵人" : simplifiedChinese ? "敌人" : "Enemies",
			"ADVENTURE_DEEP_TIDE_THRONE" => "深潮王座",
			"AVATAR_UNLOCK_TITLE" => traditionalChinese ? "頭像已解鎖" : simplifiedChinese ? "头像已解锁" : "Avatar Unlocked",
			"AVATAR_UNLOCK_MESSAGE" => traditionalChinese ? "獲得了 {0} 的頭像" : simplifiedChinese ? "获得了 {0} 的头像" : "You got {0}'s avatar",
			"AVATAR_UNLOCK_CONFIRM" => traditionalChinese || simplifiedChinese ? "收下" : "Collect",
			_ => key,
		};
	}

	private static Color GetAvatarUnlockAccentColor(CharacterAvatarDefinition avatarDefinition)
	{
		return TryParseAvatarUnlockAccentColor(avatarDefinition.AccentColorHtml, out Color accentColor)
			? accentColor
			: new Color(0.62f, 1.0f, 0.92f);
	}

	private static bool TryParseAvatarUnlockAccentColor(string rawValue, out Color color)
	{
		color = default;
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return false;
		}

		string hex = rawValue.Trim().TrimStart('#');
		if (hex.Length != 6 && hex.Length != 8)
		{
			return false;
		}

		if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
		{
			return false;
		}

		float r;
		float g;
		float b;
		float a = 1.0f;
		if (hex.Length == 6)
		{
			r = ((value >> 16) & 0xff) / 255.0f;
			g = ((value >> 8) & 0xff) / 255.0f;
			b = (value & 0xff) / 255.0f;
		}
		else
		{
			r = ((value >> 24) & 0xff) / 255.0f;
			g = ((value >> 16) & 0xff) / 255.0f;
			b = ((value >> 8) & 0xff) / 255.0f;
			a = (value & 0xff) / 255.0f;
		}

		color = new Color(r, g, b, a);
		return true;
	}
}
