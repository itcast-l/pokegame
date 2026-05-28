using System;
using Godot;

public partial class GameBoardScene
{
	private static readonly Vector2 OpponentHorizontalHandCardOffset = new(45, 0);
	private static readonly Vector2 VerticalHandCardOffset = new(0, 35);

	private void CreatePlayerCardPiles()
	{
		_playerCardPiles.Clear();

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control pileArea = GetNode<Control>($"%Player{playerIndex + 1}CardPileArea");
			var pile = new Control
			{
				Name = $"Player{playerIndex + 1}CardPile",
				MouseFilter = MouseFilterEnum.Stop,
				AnchorLeft = 0.5f,
				AnchorTop = 0.5f,
				AnchorRight = 0.5f,
				AnchorBottom = 0.5f,
				OffsetLeft = -CardPileSize.X / 2.0f,
				OffsetTop = -CardPileSize.Y / 2.0f,
				OffsetRight = CardPileSize.X / 2.0f,
				OffsetBottom = CardPileSize.Y / 2.0f,
			};
			pile.MouseEntered += () => SetCardPileCountVisible(pile, true);
			pile.MouseExited += () => SetCardPileCountVisible(pile, false);

			pileArea.AddChild(pile);
			_playerCardPiles.Add(pile);
		}
	}

	private void CreatePlayerHandAreas()
	{
		_playerHandAreas.Clear();

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control handArea = GetNode<Control>($"%Player{playerIndex + 1}HandArea");
			_playerHandAreas.Add(handArea);
		}
	}

	private void BindQuestionCardTransformArea()
	{
		_questionCardTransformArea = GetNode<Control>("%QuestionCardTransformArea");
		_questionCardTransformArea.PivotOffset = _questionCardTransformArea.Size / 2.0f;
		_questionCardTransformArea.Visible = false;

		var styleBox = new StyleBoxFlat
		{
			BgColor = new Color(0.04f, 0.30f, 0.38f, 0.16f),
			BorderColor = new Color(0.52f, 0.98f, 1.0f, 0.58f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomRight = 8,
			CornerRadiusBottomLeft = 8,
			ShadowColor = new Color(0.0f, 0.78f, 0.92f, 0.18f),
			ShadowSize = 10,
		};
		if (_questionCardTransformArea is Panel panel)
		{
			panel.AddThemeStyleboxOverride("panel", styleBox);
		}
	}

	private void CreateWinnerLabel()
	{
		_winnerBannerRoot = new Control
		{
			Name = "WinnerBannerRoot",
			Visible = false,
			MouseFilter = MouseFilterEnum.Stop,
			ZIndex = 1000,
		};
		_winnerBannerRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_winnerBannerRoot);

		var scrim = new ColorRect
		{
			Name = "WinnerScrim",
			Color = new Color(0.0f, 0.025f, 0.05f, 0.30f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		_winnerBannerRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "WinnerCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_winnerBannerRoot.AddChild(center);

		_winnerBannerPanel = new Control
		{
			Name = "WinnerBannerPanel",
			CustomMinimumSize = new Vector2(520.0f, 186.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		center.AddChild(_winnerBannerPanel);

		Texture2D? bannerTexture = LoadTextureWithImageFallback(VictoryBannerTexturePath);
		if (bannerTexture != null)
		{
			_winnerBannerArt = new TextureRect
			{
				Name = "GeneratedBannerArt",
				Texture = bannerTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			_winnerBannerArt.SetAnchorsPreset(LayoutPreset.FullRect);
			_winnerBannerPanel.AddChild(_winnerBannerArt);
		}
		else
		{
			var fallbackPanel = new Panel
			{
				Name = "FallbackBannerPanel",
				MouseFilter = MouseFilterEnum.Ignore,
			};
			fallbackPanel.SetAnchorsPreset(LayoutPreset.FullRect);
			fallbackPanel.AddThemeStyleboxOverride("panel", CreateWinnerBannerFallbackStyle());
			_winnerBannerPanel.AddChild(fallbackPanel);
			GD.PushWarning($"Missing victory banner texture: {VictoryBannerTexturePath}");
		}

		PackedScene? avatarScene = GD.Load<PackedScene>(CharacterAvatarScenePath);
		if (avatarScene != null)
		{
			Node avatarNode = avatarScene.Instantiate();
			if (avatarNode is CharacterAvatar resultAvatar)
			{
				_resultAvatar = resultAvatar;
				_resultAvatar.Name = "ResultAvatar";
				_resultAvatar.Visible = false;
				_resultAvatar.ShowFrame = false;
				_resultAvatar.LoadActionFrames = true;
				_resultAvatar.PortraitScale = 0.86f;
				_resultAvatar.MouseFilter = MouseFilterEnum.Ignore;
				_resultAvatar.CustomMinimumSize = new Vector2(84.0f, 84.0f);
				_resultAvatar.SetAnchorsPreset(LayoutPreset.CenterLeft);
				_resultAvatar.AnchorLeft = 0.0f;
				_resultAvatar.AnchorTop = 0.5f;
				_resultAvatar.AnchorRight = 0.0f;
				_resultAvatar.AnchorBottom = 0.5f;
				_resultAvatar.OffsetLeft = 96.0f;
				_resultAvatar.OffsetTop = -74.0f;
				_resultAvatar.OffsetRight = 180.0f;
				_resultAvatar.OffsetBottom = 10.0f;
				_winnerBannerPanel.AddChild(_resultAvatar);
			}
			else
			{
				avatarNode.QueueFree();
			}
		}

		_winnerLabel = new Label
		{
			Name = "WinnerLabel",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		_winnerLabel.SetAnchorsPreset(LayoutPreset.FullRect);
		_winnerLabel.OffsetLeft = 66.0f;
		_winnerLabel.OffsetTop = 28.0f;
		_winnerLabel.OffsetRight = -66.0f;
		_winnerLabel.OffsetBottom = -78.0f;
		_winnerLabel.AddThemeFontSizeOverride("font_size", 30);
		_winnerLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.94f, 0.58f));
		_winnerLabel.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.13f, 0.17f, 0.90f));
		_winnerLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.02f, 0.04f, 0.92f));
		_winnerLabel.AddThemeConstantOverride("outline_size", 4);
		_winnerLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		_winnerLabel.AddThemeConstantOverride("shadow_offset_y", 3);

		_winnerBannerPanel.AddChild(_winnerLabel);

		var buttonRow = new HBoxContainer
		{
			Name = "WinnerButtonRow",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		buttonRow.SetAnchorsPreset(LayoutPreset.FullRect);
		buttonRow.OffsetLeft = 89.0f;
		buttonRow.OffsetTop = 112.0f;
		buttonRow.OffsetRight = -89.0f;
		buttonRow.OffsetBottom = -28.0f;
		buttonRow.AddThemeConstantOverride("separation", 14);
		_winnerBannerPanel.AddChild(buttonRow);

		Control replayFrame = CreateWinnerButton("Replay", "GAME_REPLAY", new Color(0.58f, 1.0f, 0.92f), RestartGameFromWinner, false);
		Control mainMenuFrame = CreateWinnerButton("ReturnToMainMenu", GetWinnerReturnButtonKey(), new Color(1.0f, 0.86f, 0.54f), GetWinnerReturnAction(), true);
		buttonRow.AddChild(replayFrame);
		buttonRow.AddChild(mainMenuFrame);
	}

	private void CreateDefeatFinisherOverlay()
	{
		_defeatFinisherRoot = new Control
		{
			Name = "DefeatFinisherRoot",
			Visible = false,
			MouseFilter = MouseFilterEnum.Ignore,
			ZIndex = 950,
		};
		_defeatFinisherRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_defeatFinisherRoot);

		_defeatFinisherTexture = new TextureRect
		{
			Name = "DefeatFinisherTexture",
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		_defeatFinisherTexture.SetAnchorsPreset(LayoutPreset.FullRect);
		_defeatFinisherRoot.AddChild(_defeatFinisherTexture);
	}

	private void ShowWinnerBanner(int playerIndex)
	{
		_showingDefeatResult = ShouldShowDefeatResult(playerIndex);
		_winnerLabel.Text = _showingDefeatResult
			? Localize("GAME_DEFEAT")
			: LocalizeFormat("GAME_WINNER", playerIndex + 1);
		ApplyWinnerLabelLayout();
		_winnerLabel.Visible = true;
		ApplyWinnerBannerArt();
		UpdateResultAvatar();
		if (_winnerBannerRoot == null)
		{
			return;
		}

		_winnerBannerRoot.Visible = true;
		_winnerBannerRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		if (_winnerBannerPanel != null)
		{
			_winnerBannerPanel.Scale = new Vector2(0.92f, 0.92f);
			_winnerBannerPanel.PivotOffset = _winnerBannerPanel.CustomMinimumSize / 2.0f;
		}

		Tween tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(_winnerBannerRoot, "modulate:a", 1.0f, 0.24f);
		if (_winnerBannerPanel != null)
		{
			tween.Parallel().TweenProperty(_winnerBannerPanel, "scale", Vector2.One, 0.30f);
		}
		_winnerReplayButton?.GrabFocus();
	}

	private void HideWinnerBanner()
	{
		if (_winnerBannerRoot != null)
		{
			_winnerBannerRoot.Visible = false;
			_winnerBannerRoot.Modulate = Colors.White;
		}

		if (_winnerBannerPanel != null)
		{
			_winnerBannerPanel.Scale = Vector2.One;
		}

		if (_resultAvatar != null)
		{
			_resultAvatar.Visible = false;
		}

		_winnerLabel.Visible = false;
	}

	private void RestartGameFromWinner()
	{
		HideWinnerBanner();
		DealCardsToPlayers(playOpeningDealAnimation: true);
	}

	private string GetWinnerReturnButtonKey()
	{
		return adventure_mode ? "GAME_RETURN_ADVENTURE" : "GAME_RETURN_MAIN_MENU";
	}

	private Action GetWinnerReturnAction()
	{
		return adventure_mode ? ReturnToAdventureMenu : ReturnToMainMenu;
	}

	private Control CreateWinnerButton(string nodeName, string textKey, Color accentColor, Action pressedAction, bool flipOrnament)
	{
		var frame = new OceanMenuButtonFrame
		{
			Name = $"{nodeName}Frame",
			CustomMinimumSize = new Vector2(162.0f, 46.0f),
			AccentColor = accentColor,
			FlipOrnament = flipOrnament,
		};

		var button = new Button
		{
			Name = "Button",
			Text = Localize(textKey),
			Flat = true,
			FocusMode = FocusModeEnum.All,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.OffsetLeft = flipOrnament ? 20.0f : 4.0f;
		button.OffsetTop = 4.0f;
		button.OffsetRight = flipOrnament ? -4.0f : -20.0f;
		button.OffsetBottom = -4.0f;
		button.AddThemeFontSizeOverride("font_size", 16);
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

		if (_winnerReplayButton == null)
		{
			_winnerReplayButton = button;
		}

		TrackLocalizedGameButton(button, textKey);
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

		Image image = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
		return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
	}

	private static StyleBoxFlat CreateWinnerBannerFallbackStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.02f, 0.30f, 0.36f, 0.90f),
			BorderColor = new Color(0.98f, 0.80f, 0.36f, 0.82f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 8,
			CornerRadiusTopRight = 8,
			CornerRadiusBottomLeft = 8,
			CornerRadiusBottomRight = 8,
			ShadowColor = new Color(0.0f, 0.02f, 0.04f, 0.66f),
			ShadowSize = 22,
			ShadowOffset = new Vector2(0.0f, 9.0f),
		};
	}

	private bool ShouldShowDefeatResult(int winnerPlayerIndex)
	{
		if (winnerPlayerIndex == MainPlayerIndex)
		{
			return false;
		}

		return adventure_mode || solo_mode;
	}

	private void ApplyWinnerLabelLayout()
	{
		if (_winnerLabel == null || !GodotObject.IsInstanceValid(_winnerLabel))
		{
			return;
		}

		_winnerLabel.OffsetLeft = _showingDefeatResult ? 176.0f : 66.0f;
		_winnerLabel.OffsetTop = 28.0f;
		_winnerLabel.OffsetRight = -66.0f;
		_winnerLabel.OffsetBottom = -78.0f;
		_winnerLabel.HorizontalAlignment = _showingDefeatResult
			? HorizontalAlignment.Left
			: HorizontalAlignment.Center;
	}

	private void ApplyWinnerBannerArt()
	{
		if (_winnerBannerArt == null || !GodotObject.IsInstanceValid(_winnerBannerArt))
		{
			return;
		}

		string texturePath = _showingDefeatResult ? DefeatBannerTexturePath : VictoryBannerTexturePath;
		Texture2D? texture = LoadTextureWithImageFallback(texturePath);
		if (texture == null)
		{
			GD.PushWarning($"Missing result banner texture: {texturePath}");
			return;
		}

		_winnerBannerArt.Texture = texture;
	}

	private void UpdateResultAvatar()
	{
		if (_resultAvatar == null || !GodotObject.IsInstanceValid(_resultAvatar))
		{
			return;
		}

		if (!_showingDefeatResult)
		{
			_resultAvatar.Visible = false;
			return;
		}

		CharacterAvatarDefinition playerAvatarDefinition = CharacterAvatarCatalog.LoadSelectedAvatar();
		_resultAvatar.AvatarRoot = playerAvatarDefinition.RootPath;
		_resultAvatar.AccentColor = PlayerAvatarAccentColors[MainPlayerIndex];
		_resultAvatar.Visible = true;
		_resultAvatar.PlayAction(CharacterAvatar.AvatarAction.Defeat);
	}

	private static Vector2 GetHandAreaSize(int playerIndex)
	{
		Vector2 cardOffset = GetHandCardOffset(playerIndex);
		return new Vector2(
			CardVisualSize.X + Math.Abs(cardOffset.X) * (StartingHandCards - 1),
			CardVisualSize.Y + Math.Abs(cardOffset.Y) * (StartingHandCards - 1));
	}

	private static Vector2 GetHandCardOffset(int playerIndex)
	{
		return playerIndex switch
		{
			2 or 3 => VerticalHandCardOffset,
			1 => OpponentHorizontalHandCardOffset,
			_ => HorizontalHandCardOffset,
		};
	}

	private static Vector2 GetHandCardOffset(int playerIndex, int cardCount)
	{
		return GetHandCardOffset(playerIndex, cardCount, GetHandAreaSize(playerIndex));
	}

	private static Vector2 GetHandCardOffset(int playerIndex, int cardCount, Vector2 handAreaSize)
	{
		Vector2 baseOffset = GetHandCardOffset(playerIndex);
		if (cardCount <= 1)
		{
			return baseOffset;
		}

		if (!Mathf.IsZeroApprox(baseOffset.X))
		{
			float availableSpacing = Math.Max(0.0f, handAreaSize.X - CardVisualSize.X);
			float spacing = Math.Min(Math.Abs(baseOffset.X), availableSpacing / (cardCount - 1));
			return new Vector2(baseOffset.X < 0.0f ? -spacing : spacing, baseOffset.Y);
		}

		float availableVerticalSpacing = Math.Max(0.0f, handAreaSize.Y - CardVisualSize.Y);
		float verticalSpacing = Math.Min(Math.Abs(baseOffset.Y), availableVerticalSpacing / (cardCount - 1));
		return new Vector2(baseOffset.X, baseOffset.Y < 0.0f ? -verticalSpacing : verticalSpacing);
	}

	private static Vector2 GetHandFirstCardPosition(int playerIndex, int cardCount, Vector2 handAreaSize, Vector2 cardOffset)
	{
		if (cardCount <= 0)
		{
			return Vector2.Zero;
		}

		Vector2 groupSize = new(
			CardVisualSize.X + Math.Abs(cardOffset.X) * (cardCount - 1),
			CardVisualSize.Y + Math.Abs(cardOffset.Y) * (cardCount - 1));
		Vector2 startPosition = (handAreaSize - groupSize) / 2.0f;
		if (cardOffset.X < 0.0f)
		{
			startPosition.X += groupSize.X - CardVisualSize.X;
		}
		if (cardOffset.Y < 0.0f)
		{
			startPosition.Y += groupSize.Y - CardVisualSize.Y;
		}

		return startPosition;
	}

	private void SetCardPileCountVisible(Control pile, bool visible)
	{
		if (pile.GetNodeOrNull<Label>(CardCountLabelNodeName) is Label countLabel)
		{
			countLabel.Visible = visible;
		}
	}

	private void UpdatePlayerVisibility()
	{
		_bottomPlayerArea.Visible = player_count >= 1;
		_topPlayerArea.Visible = player_count >= 2;
		_leftPlayerArea.Visible = player_count >= 3;
		_rightPlayerArea.Visible = player_count >= 4;
	}
}
