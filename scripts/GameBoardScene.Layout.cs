using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private static readonly Vector2 BoardDesignSize = new(1920.0f, 1080.0f);
	private static readonly Vector2 OpponentHorizontalHandCardOffset = new(45, 0);
	private static readonly Vector2 VerticalHandCardOffset = new(0, 35);

	private void RefreshResponsiveBoardLayout()
	{
		if (_boardArt == null
			|| !GodotObject.IsInstanceValid(_boardArt)
			|| _layoutAreas == null
			|| !GodotObject.IsInstanceValid(_layoutAreas))
		{
			return;
		}

		Rect2 boardVisibleRect = GetBoardArtVisibleRect();
		_layoutAreas.AnchorLeft = 0.0f;
		_layoutAreas.AnchorTop = 0.0f;
		_layoutAreas.AnchorRight = 0.0f;
		_layoutAreas.AnchorBottom = 0.0f;
		_layoutAreas.Position = boardVisibleRect.Position;
		_layoutAreas.Size = boardVisibleRect.Size;

		if (_questionCardTransformArea != null && GodotObject.IsInstanceValid(_questionCardTransformArea))
		{
			_questionCardTransformArea.PivotOffset = _questionCardTransformArea.Size / 2.0f;
		}
	}

	private Rect2 GetBoardArtVisibleRect()
	{
		if (_boardArt == null || !GodotObject.IsInstanceValid(_boardArt))
		{
			return new Rect2(Vector2.Zero, Size);
		}

		Rect2 boardRect = _boardArt.GetGlobalRect();
		Vector2 textureSize = GetBoardArtTextureSize();
		if (textureSize.X <= 0.0f || textureSize.Y <= 0.0f || boardRect.Size.X <= 0.0f || boardRect.Size.Y <= 0.0f)
		{
			return boardRect;
		}

		float textureAspect = textureSize.X / textureSize.Y;
		float controlAspect = boardRect.Size.X / boardRect.Size.Y;
		Vector2 visibleSize = controlAspect > textureAspect
			? new Vector2(boardRect.Size.Y * textureAspect, boardRect.Size.Y)
			: new Vector2(boardRect.Size.X, boardRect.Size.X / textureAspect);
		Vector2 visiblePosition = boardRect.Position + (boardRect.Size - visibleSize) / 2.0f;
		return new Rect2(visiblePosition, visibleSize);
	}

	private Vector2 GetBoardArtTextureSize()
	{
		if (_boardArt?.Texture != null)
		{
			Vector2 textureSize = _boardArt.Texture.GetSize();
			if (textureSize.X > 0.0f && textureSize.Y > 0.0f)
			{
				return textureSize;
			}
		}

		return BoardDesignSize;
	}

	private void CreatePlayerCardPiles()
	{
		_playerCardPiles.Clear();

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			int viewSlotIndex = GetPlayerViewSlotIndex(playerIndex);
			Control pileArea = GetNode<Control>($"%Player{viewSlotIndex + 1}CardPileArea");
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
			int viewSlotIndex = GetPlayerViewSlotIndex(playerIndex);
			Control handArea = GetNode<Control>($"%Player{viewSlotIndex + 1}HandArea");
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
			: GetWinnerText(playerIndex);
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

		if (_winnerReplayButton != null && !_winnerReplayButton.Disabled)
		{
			_winnerReplayButton.GrabFocus();
		}
		else
		{
			_winnerBannerRoot.ReturnButton.GrabFocus();
		}
	}

	private string GetWinnerText(int playerIndex)
	{
		string playerDisplayName = GetPlayerDisplayName(playerIndex);
		string winnerText = LocalizeFormat("GAME_WINNER", playerDisplayName);
		string legacyPlayerPrefix = LocalizeFormat("GAME_PLAYER", playerDisplayName);
		return winnerText.StartsWith(legacyPlayerPrefix, StringComparison.Ordinal)
			? playerDisplayName + winnerText[legacyPlayerPrefix.Length..]
			: winnerText;
	}

	private string GetPlayerDisplayName(int playerIndex)
	{
		if (!adventure_mode && TryGetNetworkPlayerName(playerIndex, out string networkName))
		{
			return networkName;
		}

		if (!adventure_mode && IsLocalHandPlayer(playerIndex))
		{
			return PlayerNameRules.Normalize(GameSaveStore.LoadActiveProfile().PlayerName);
		}

		if (adventure_mode && IsAdventureEnemyPlayer(playerIndex))
		{
			return LocalizeAvatarDisplayName(CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id));
		}

		return LocalizeFormat("GAME_PLAYER", playerIndex + 1);
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
		if (_winnerReplayButton != null && _winnerReplayButton.Disabled)
		{
			return;
		}

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

		string gameBoardScenePath = AdventureAvatarMenuScene.GetGameBoardScenePathForAdventureAvatar(nextAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(gameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {gameBoardScenePath}");
			RestartGameFromWinner();
			return;
		}

		_ = StartNextAdventureStageWithTransitionAsync(gameBoardScene, nextAvatarId);
	}

	private async Task StartNextAdventureStageWithTransitionAsync(PackedScene gameBoardScene, string nextAvatarId)
	{
		if (_isLeavingGame)
		{
			return;
		}

		_isLeavingGame = true;
		if (_winnerReplayButton != null)
		{
			_winnerReplayButton.Disabled = true;
		}

		await CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			Node gameBoard = gameBoardScene.Instantiate();
			gameBoard.Set("player_count", 2);
			gameBoard.Set("item_mode", true);
			gameBoard.Set("solo_mode", true);
			gameBoard.Set("adventure_mode", true);
			gameBoard.Set("adventure_enemy_avatar_id", nextAvatarId);
			GetTree().Root.AddChild(gameBoard);
			GetTree().CurrentScene = gameBoard;
			QueueFree();
		});
	}

	private void UpdateWinnerPrimaryButton()
	{
		if (_winnerReplayButton == null || !GodotObject.IsInstanceValid(_winnerReplayButton))
		{
			return;
		}

		bool shouldAdvanceAdventure = ShouldAdvanceAdventureFromWinner();
		_winnerReplayButton.Disabled = !ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			IsNetworkMultiplayerMode(),
			shouldAdvanceAdventure);
		_winnerReplayButton.Text = Localize(shouldAdvanceAdventure ? "GAME_NEXT_ADVENTURE_STAGE" : "GAME_REPLAY");
	}

	private void UpdateWinnerReturnButton()
	{
		if (_winnerBannerRoot == null || !GodotObject.IsInstanceValid(_winnerBannerRoot))
		{
			return;
		}

		_winnerBannerRoot.ReturnButton.Text = Localize(GetWinnerReturnButtonKey());
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
		return FrameAnimationHelpers.LoadTextureResource(path, allowEditorImageFallback: true);
	}

	private bool ShouldShowDefeatResult(int winnerPlayerIndex)
	{
		return ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex,
			IsNetworkMultiplayerMode() ? GetLocalHandPlayerIndex() : MainPlayerIndex,
			IsNetworkMultiplayerMode(),
			adventure_mode,
			solo_mode);
	}

	private void ApplyWinnerLabelLayout()
	{
		if (_winnerLabel == null || !GodotObject.IsInstanceValid(_winnerLabel))
		{
			return;
		}

		_winnerLabel.OffsetLeft = 76.0f;
		_winnerLabel.OffsetTop = 86.0f;
		_winnerLabel.OffsetRight = -76.0f;
		_winnerLabel.OffsetBottom = -118.0f;
		_winnerLabel.HorizontalAlignment = HorizontalAlignment.Center;
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

		_resultAvatar.Visible = false;
	}

	private Vector2 GetHandAreaSize(int playerIndex)
	{
		Vector2 cardOffset = GetHandCardOffset(playerIndex);
		return new Vector2(
			CardVisualSize.X + Math.Abs(cardOffset.X) * (StartingHandCards - 1),
			CardVisualSize.Y + Math.Abs(cardOffset.Y) * (StartingHandCards - 1));
	}

	private Vector2 GetHandCardOffset(int playerIndex)
	{
		return GetPlayerViewSlotIndex(playerIndex) switch
		{
			2 or 3 => VerticalHandCardOffset,
			1 => OpponentHorizontalHandCardOffset,
			_ => HorizontalHandCardOffset,
		};
	}

	private Vector2 GetHandCardOffset(int playerIndex, int cardCount)
	{
		return GetHandCardOffset(playerIndex, cardCount, GetHandAreaSize(playerIndex));
	}

	private Vector2 GetHandCardOffset(int playerIndex, int cardCount, Vector2 handAreaSize)
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

	private Vector2 GetHandFirstCardPosition(int playerIndex, int cardCount, Vector2 handAreaSize, Vector2 cardOffset)
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
		MarkSeatControlIndicatorLayoutDirty();
		MarkActivePlayerStatusTooltipLayoutDirty();
	}
}
