using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private AdventureDefeatResolution? _settledAdventureDefeatResolution;
	private bool _adventureVictoryCommitInProgress;
	private bool _adventureVictoryCommitCompleted;
	private string _adventureVictoryStatusLocalizationKey = string.Empty;
	private static readonly double[] AdventureVictorySaveRetryDelays = { 0.5, 1.0, 2.0 };

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
		if (_winnerBannerRoot != null)
		{
			_winnerBannerRoot.ReturnButton.Disabled = false;
		}
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

	private async Task<bool> CommitAdventureVictoryWithRetryAsync()
	{
		if (_adventureVictoryCommitCompleted)
		{
			return true;
		}
		if (_adventureVictoryCommitInProgress || _isLeavingGame)
		{
			return false;
		}

		_adventureVictoryCommitInProgress = true;
		ShowAdventureVictorySaveStatus("GAME_ADVENTURE_SAVING");
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		for (int attemptIndex = 0; attemptIndex <= AdventureVictorySaveRetryDelays.Length; attemptIndex++)
		{
			if (_isLeavingGame || !GodotObject.IsInstanceValid(this))
			{
				_adventureVictoryCommitInProgress = false;
				return false;
			}

			AdventureVictoryCommitResult result = store?.CommitMainAdventureVictory(adventure_enemy_avatar_id)
				?? AdventureVictoryCommitResult.SaveFailed;
			if (result is AdventureVictoryCommitResult.Succeeded or AdventureVictoryCommitResult.AlreadyCommitted)
			{
				_adventureVictoryCommitCompleted = true;
				_adventureVictoryCommitInProgress = false;
				_adventureVictoryStatusLocalizationKey = string.Empty;
				return true;
			}
			if (result is AdventureVictoryCommitResult.InvalidStage or AdventureVictoryCommitResult.OutOfSequence)
			{
				GD.PushError($"Adventure victory commit rejected for '{adventure_enemy_avatar_id}': {result}");
				break;
			}
			if (attemptIndex < AdventureVictorySaveRetryDelays.Length)
			{
				ShowAdventureVictorySaveStatus("GAME_ADVENTURE_SAVE_RETRYING");
				await ToSignal(GetTree().CreateTimer(AdventureVictorySaveRetryDelays[attemptIndex]), SceneTreeTimer.SignalName.Timeout);
			}
		}

		_adventureVictoryCommitInProgress = false;
		ShowAdventureVictorySaveStatus("GAME_ADVENTURE_SAVE_FAILED_REPLAY");
		await ToSignal(GetTree().CreateTimer(0.75), SceneTreeTimer.SignalName.Timeout);
		if (_isLeavingGame || !GodotObject.IsInstanceValid(this))
		{
			return false;
		}
		ShowAdventureVictorySaveStatus("GAME_ADVENTURE_RETURNING_TO_MENU");
		await ToSignal(GetTree().CreateTimer(0.75), SceneTreeTimer.SignalName.Timeout);
		if (!_isLeavingGame && GodotObject.IsInstanceValid(this))
		{
			await ReturnToMenuSceneAsync(AdventureAvatarMenuScenePath);
		}
		return false;
	}

	private void ShowAdventureVictorySaveStatus(string localizationKey)
	{
		_adventureVictoryStatusLocalizationKey = localizationKey;
		_winnerLabel.Text = Localize(localizationKey);
		_winnerLabel.Visible = true;
		ApplyWinnerLabelLayout();
		if (_winnerBannerRoot != null)
		{
			_winnerBannerRoot.Visible = true;
			_winnerBannerRoot.Modulate = Colors.White;
			_winnerBannerRoot.ReturnButton.Disabled = true;
		}
		if (_winnerReplayButton != null)
		{
			_winnerReplayButton.Disabled = true;
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
		if (_settledAdventureDefeatResolution == AdventureDefeatResolution.RetryCurrentStage)
		{
			HideWinnerBanner();
			DealCardsToPlayers(playOpeningDealAnimation: true);
			return;
		}

		if (_settledAdventureDefeatResolution == AdventureDefeatResolution.RestartJourney)
		{
			RestartAdventureRunFromWinner(beginNewRun: false);
			return;
		}

		if (ShouldRestartAdventureRunFromWinner())
		{
			RestartAdventureRunFromWinner(beginNewRun: true);
			return;
		}

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

	private bool ShouldRestartAdventureRunFromWinner()
	{
		return ShouldUseAdventureUpgradeSystem()
			&& _winnerPlayerIndex >= 0
			&& (_winnerPlayerIndex != MainPlayerIndex || !TryGetNextAdventureStageAvatarId(out _));
	}

	private void RestartAdventureRunFromWinner(bool beginNewRun)
	{
		string firstAvatarId = GetFirstMainAdventureAvatarId();
		string gameBoardScenePath = AdventureAvatarMenuScene.GetGameBoardScenePathForAdventureAvatar(firstAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(gameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {gameBoardScenePath}");
			HideWinnerBanner();
			DealCardsToPlayers(playOpeningDealAnimation: true);
			return;
		}

		if (beginNewRun)
		{
			AdventureRunWriteResult writeResult = GetAdventureRunSessionStore()?.BeginNewMainRun(
				1,
				GetFirstMainAdventureAvatarId()) ?? AdventureRunWriteResult.SaveFailed;
			if (writeResult != AdventureRunWriteResult.Succeeded)
			{
				GD.PushWarning($"Failed to start a new adventure run from results: {writeResult}");
				return;
			}
		}
		_ = StartNextAdventureStageWithTransitionAsync(gameBoardScene, firstAvatarId);
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
		bool hasAdventureDefeatAction = _settledAdventureDefeatResolution.HasValue;
		_winnerReplayButton.Disabled = !string.IsNullOrEmpty(_adventureVictoryStatusLocalizationKey)
			|| !ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			IsNetworkMultiplayerMode(),
			shouldAdvanceAdventure || hasAdventureDefeatAction);
		string buttonKey = _settledAdventureDefeatResolution switch
		{
			AdventureDefeatResolution.RetryCurrentStage => "GAME_RETRY_ADVENTURE_STAGE",
			AdventureDefeatResolution.RestartJourney => "GAME_RESTART_ADVENTURE_JOURNEY",
			_ when shouldAdvanceAdventure => "GAME_NEXT_ADVENTURE_STAGE",
			_ => "GAME_REPLAY",
		};
		_winnerReplayButton.Text = Localize(buttonKey);
	}

	private void UpdateWinnerReturnButton()
	{
		if (_winnerBannerRoot == null || !GodotObject.IsInstanceValid(_winnerBannerRoot))
		{
			return;
		}

		_winnerBannerRoot.ReturnButton.Text = Localize(GetWinnerReturnButtonKey());
		_winnerBannerRoot.ReturnButton.Disabled = !string.IsNullOrEmpty(_adventureVictoryStatusLocalizationKey);
	}

	private bool ShouldAdvanceAdventureFromWinner()
	{
		return _gameModePolicy.IsAdventureMain
			&& _winnerPlayerIndex == MainPlayerIndex
			&& TryGetNextAdventureStageAvatarId(out _);
	}

	private bool TryGetNextAdventureStageAvatarId(out string nextAvatarId)
	{
		if (!AdventureCampaignCatalog.TryGetMainStageIndex(adventure_enemy_avatar_id, out int currentStage))
		{
			nextAvatarId = string.Empty;
			return false;
		}

		int nextStage = currentStage + 1;
		if (nextStage > AdventureCampaignCatalog.MainStageCount
			|| !CharacterAvatarCatalog.IsAdventureStageUnlocked(nextStage))
		{
			nextAvatarId = string.Empty;
			return false;
		}

		nextAvatarId = AdventureCampaignCatalog.GetMainAvatarId(nextStage);
		return !string.IsNullOrEmpty(nextAvatarId);
	}

	private string GetWinnerReturnButtonKey()
	{
		return _gameModePolicy.IsAdventure ? "GAME_RETURN_ADVENTURE" : "GAME_RETURN_MAIN_MENU";
	}

	private Action GetWinnerReturnAction()
	{
		return _gameModePolicy.IsAdventure ? ReturnToAdventureMenu : ReturnToMainMenu;
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
}
