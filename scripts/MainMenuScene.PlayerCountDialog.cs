using Godot;

public partial class MainMenuScene : Control
{
	private const float PlayerCountDialogHideSeconds = 0.16f;
	private const int DefaultMenuWinningScore = 100;
	private static readonly int[] MenuWinningScoreOptions = { 50, 100, 200 };

	private void BuildPlayerCountDialog()
	{
		PackedScene? dialogScene = GD.Load<PackedScene>(PlayerCountDialogScenePath);
		if (dialogScene?.Instantiate() is not MainMenuPlayerCountDialog dialog)
		{
			GD.PushError($"Missing player count dialog scene: {PlayerCountDialogScenePath}");
			return;
		}

		_playerCountDialog = dialog;
		_playerCountDialogLayer = new CanvasLayer
		{
			Name = "PlayerCountDialogLayer",
		};
		AddChild(_playerCountDialogLayer);
		_playerCountDialogRoot = dialog;
		_playerCountDialogLayer.AddChild(dialog);
		_playerCountDialogMotion = dialog.MotionRoot;
		_playerCountDialogPanel = dialog.DialogPanel;
		_playerCountOption = dialog.PlayerCountOption;
		_itemModeOption = dialog.ItemModeOption;
		_winningScoreOption = dialog.WinningScoreOption;
		SuppressDefaultPressedSfx(_singlePlayerButton);
		dialog.CloseRequested += HidePlayerCountDialog;
		dialog.StartGameRequested += StartGameWithOptions;
	}

	private void RefreshPlayerCountDialogOptions()
	{
		_playerCountDialog?.RefreshLocalizedText(Localize, GetPlayerCountOptionText, GetWinningScoreOptionText);
	}

	private string GetPlayerCountOptionText(int playerCount)
	{
		return playerCount switch
		{
			2 => Localize("PLAYER_COUNT_2_TITLE"),
			3 => Localize("PLAYER_COUNT_3_TITLE"),
			4 => Localize("PLAYER_COUNT_4_TITLE"),
			_ => playerCount.ToString(),
		};
	}

	private string GetWinningScoreOptionText(int winningScore)
	{
		return LocalizeFormat("PLAYER_COUNT_WINNING_SCORE_OPTION", winningScore);
	}

	private void ShowPlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		_playerCountDialogRoot.Visible = true;
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_playerCountDialogRoot.Modulate = Colors.White;
		UpdatePlayerCountDialogPanelPosition();
		_playerCountDialogMotion.Position = Vector2.Zero;
		_playerCountDialogMotion.Scale = Vector2.One;
		_playerCountDialogPanel.PivotOffset = _playerCountDialogPanel.Size * 0.5f;
		CallDeferred(nameof(FocusPlayerCountDialogDefault));
	}

	private void HidePlayerCountDialog()
	{
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_playerCountDialogTween?.Kill();
		_avatarEntry.Visible = true;
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_playerCountDialogTween = CreateTween();
		_playerCountDialogTween.TweenProperty(_playerCountDialogRoot, "modulate:a", 0.0f, PlayerCountDialogHideSeconds);
		_playerCountDialogTween.Finished += () =>
		{
			_playerCountDialogRoot.Visible = false;
			GrabFocusIfAvailable(_singlePlayerButton);
		};
	}

	private void UpdatePlayerCountDialogPanelPosition()
	{
		if (_playerCountDialogRoot != null && _playerCountDialogRoot.Visible)
		{
			_playerCountDialogRoot.Position = Vector2.Zero;
			_playerCountDialogRoot.Scale = Vector2.One;
		}
	}

	private void StartGameWithSelectedOptions()
	{
		int playerCount = _playerCountOption.GetSelectedId();
		bool itemMode = _itemModeOption.GetSelectedId() == 1;
		int winningScore = _winningScoreOption.GetSelectedId();
		StartGameWithOptions(playerCount, itemMode, winningScore);
	}

	private async void StartGameWithOptions(int playerCount, bool itemMode, int winningScore)
	{
		if (_isStartingGameTransition)
		{
			return;
		}

		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		_isStartingGameTransition = true;
		SetMainMenuButtonsDisabled(true);
		await CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			Node gameBoard = gameBoardScene.Instantiate();
			gameBoard.Set("player_count", playerCount);
			gameBoard.Set("item_mode", itemMode);
			gameBoard.Set("winning_score", winningScore);
			gameBoard.Set("solo_mode", true);
			GetTree().Root.AddChild(gameBoard);
			GetTree().CurrentScene = gameBoard;
			QueueFree();
		});
	}
}
