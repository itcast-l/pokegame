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
		PackedScene? resultBannerScene = GD.Load<PackedScene>(ResultBannerScenePath);
		if (resultBannerScene?.Instantiate() is not GameBoardResultBanner resultBanner)
		{
			GD.PushError($"Missing result banner scene: {ResultBannerScenePath}");
			return;
		}

		AddChild(resultBanner);
		_winnerBannerRoot = resultBanner;
		_winnerBannerPanel = resultBanner.BannerPanel;
		_winnerBannerArt = resultBanner.BannerArt;
		_winnerLabel = resultBanner.WinnerLabel;
		_resultAvatar = resultBanner.ResultAvatar;
		_winnerReplayButton = resultBanner.PrimaryButton;
		resultBanner.PrimaryPressed += PressWinnerPrimaryButton;
		resultBanner.ReturnPressed += PressWinnerReturnButton;
		UpdateWinnerPrimaryButton();
		UpdateWinnerReturnButton();
	}

	private void CreateDefeatFinisherOverlay()
	{
		PackedScene? defeatFinisherScene = GD.Load<PackedScene>(DefeatFinisherOverlayScenePath);
		if (defeatFinisherScene?.Instantiate() is not GameBoardDefeatFinisherOverlay defeatFinisher)
		{
			GD.PushError($"Missing defeat finisher overlay scene: {DefeatFinisherOverlayScenePath}");
			return;
		}

		AddChild(defeatFinisher);
		_defeatFinisherRoot = defeatFinisher;
		_defeatFinisherTexture = defeatFinisher.FinisherTexture;
	}

	private void ShowWinnerBanner(int playerIndex)
	{
		_showingDefeatResult = ShouldShowDefeatResult(playerIndex);
		_winnerLabel.Text = _showingDefeatResult
			? Localize("GAME_DEFEAT")
			: LocalizeFormat("GAME_WINNER", playerIndex + 1);
		UpdateWinnerPrimaryButton();
		UpdateWinnerReturnButton();
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

	private void PressWinnerPrimaryButton()
	{
		if (ShouldAdvanceAdventureFromWinner())
		{
			StartNextAdventureStageFromWinner();
			return;
		}

		RestartGameFromWinner();
	}

	private void PressWinnerReturnButton()
	{
		GetWinnerReturnAction().Invoke();
	}

	private void StartNextAdventureStageFromWinner()
	{
		if (!TryGetNextAdventureStageAvatarId(out string nextAvatarId))
		{
			RestartGameFromWinner();
			return;
		}

		HideWinnerBanner();
		adventure_enemy_avatar_id = nextAvatarId;
		RefreshAdventureEnemyAvatar();
		DealCardsToPlayers(playOpeningDealAnimation: true);
	}

	private void RefreshAdventureEnemyAvatar()
	{
		if (!adventure_mode
			|| player_count <= 1
			|| _playerAvatars[1] == null
			|| !GodotObject.IsInstanceValid(_playerAvatars[1]))
		{
			return;
		}

		CharacterAvatarDefinition enemyDefinition = CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id);
		CharacterAvatar enemyAvatar = _playerAvatars[1]!;
		enemyAvatar.AvatarRoot = enemyDefinition.RootPath;
		enemyAvatar.AccentColor = PlayerAvatarAccentColors[1];
		enemyAvatar.PlayAction(CharacterAvatar.AvatarAction.Idle);
	}

	private void UpdateWinnerPrimaryButton()
	{
		if (_winnerReplayButton == null || !GodotObject.IsInstanceValid(_winnerReplayButton))
		{
			return;
		}

		_winnerReplayButton.Text = Localize(GetWinnerPrimaryButtonKey());
	}

	private void UpdateWinnerReturnButton()
	{
		if (_winnerBannerRoot == null || !GodotObject.IsInstanceValid(_winnerBannerRoot))
		{
			return;
		}

		_winnerBannerRoot.ReturnButton.Text = Localize(GetWinnerReturnButtonKey());
	}

	private string GetWinnerPrimaryButtonKey()
	{
		return ShouldAdvanceAdventureFromWinner() ? "GAME_NEXT_ADVENTURE_STAGE" : "GAME_REPLAY";
	}

	private bool ShouldAdvanceAdventureFromWinner()
	{
		return adventure_mode
			&& _winnerPlayerIndex == MainPlayerIndex
			&& TryGetNextAdventureStageAvatarId(out _);
	}

	private bool TryGetNextAdventureStageAvatarId(out string nextAvatarId)
	{
		int currentStage = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		int nextStage = currentStage + 1;
		if (currentStage <= 0
			|| nextStage > CharacterAvatarCatalog.GetAdventureStageCount()
			|| !CharacterAvatarCatalog.IsAdventureStageUnlocked(nextStage))
		{
			nextAvatarId = string.Empty;
			return false;
		}

		nextAvatarId = CharacterAvatarCatalog.GetAdventureAvatarIdForStage(nextStage);
		return nextAvatarId != CharacterAvatarCatalog.DefaultAvatarId;
	}

	private string GetWinnerReturnButtonKey()
	{
		return adventure_mode ? "GAME_RETURN_ADVENTURE" : "GAME_RETURN_MAIN_MENU";
	}

	private Action GetWinnerReturnAction()
	{
		return adventure_mode ? ReturnToAdventureMenu : ReturnToMainMenu;
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
