using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private AdventureDefeatResolution? _settledAdventureDefeatResolution;
	private AdventureSaveGateCoordinator? _adventureSaveGateCoordinator;
	private bool _adventureVictoryCommitCompleted;
	private bool _networkReturnToLobbyRequested;

	private AdventureSaveGateCoordinator AdventureSaveGate
		=> _adventureSaveGateCoordinator ??= new AdventureSaveGateCoordinator(
			() => _isLeavingGame || !GodotObject.IsInstanceValid(this),
			WaitForBoardDelayAsync,
			PresentAdventureSaveStatus,
			HandleAdventureSaveGateSucceeded,
			HandleAdventureSaveGateAbortStarted,
			() => ReturnToMenuSceneAsync(AdventureAvatarMenuScenePath));

	private bool AdventureSaveInProgress
		=> _adventureSaveGateCoordinator?.IsInProgress == true;

	private string AdventureSaveStatusLocalizationKey
		=> _adventureSaveGateCoordinator?.StatusLocalizationKey ?? string.Empty;

	private void CreateWinnerLabel()
	{
		PackedScene? resultBannerScene = LoadCachedCoreScene(
			ref _cachedResultBannerScene,
			ResultBannerScenePath);
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
		PackedScene? defeatFinisherScene = LoadCachedCoreScene(
			ref _cachedDefeatFinisherOverlayScene,
			DefeatFinisherOverlayScenePath);
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
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		bool isDraw = _matchOutcomeKind == MatchOutcomeKind.Draw;
		if (_winnerBannerRoot != null)
		{
			_winnerBannerRoot.SetActionsVisible(true);
			_winnerBannerRoot.ReturnButton.Disabled = false;
		}
		_showingDefeatResult = !isDraw && ShouldShowDefeatResult(playerIndex);
		_winnerLabel.Text = isDraw
			? Localize("GAME_DRAW")
			: _showingDefeatResult
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

		Tween tween = CreateBoardTween();
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
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureRunSessionStore.PreparedAdventureOperation<AdventureVictoryCommitResult> operation =
			store?.PrepareCommitMainAdventureVictory(adventure_enemy_avatar_id)
			?? AdventureRunSessionStore.PreparedAdventureOperation<AdventureVictoryCommitResult>.Rejected(
				AdventureRunWriteResult.SaveFailed,
				static result => AdventureVictoryCommitResult.SaveFailed);
		AdventureSaveGateResult<AdventureVictoryCommitResult> gate = await ExecutePreparedAdventureSaveGateAsync(
			operation,
			result => result is AdventureVictoryCommitResult.Succeeded or AdventureVictoryCommitResult.AlreadyCommitted,
			result => result == AdventureVictoryCommitResult.SaveFailed);
		if (!gate.Succeeded)
		{
			if (gate.Result is AdventureVictoryCommitResult.InvalidStage or AdventureVictoryCommitResult.OutOfSequence)
			{
				GD.PushError($"Adventure victory commit rejected for '{adventure_enemy_avatar_id}': {gate.Result}");
			}
			return false;
		}

		_adventureVictoryCommitCompleted = true;
		return true;
	}

	private async Task<bool> CommitAdventureTutorialVictoryWithRetryAsync()
	{
		string tutorialAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(adventure_enemy_avatar_id);
		if (!AdventureCampaignCatalog.IsTutorialAvatar(tutorialAvatarId))
		{
			return true;
		}

		ProfileWriteLease? lease = await GameSaveProfileWriteCoordinator.AcquireAsync(
			profile => profile with
			{
				CompletedAdventureTutorialIdsCsv = AdventureCampaignCatalog.AddCompletedTutorialId(
					profile.CompletedAdventureTutorialIdsCsv,
					tutorialAvatarId),
			},
			forcePersist: false);
		if (lease == null)
		{
			return false;
		}

		AdventureSaveGateResult<Error> gate = await AdventureSaveGate.ExecuteAsync(
			async () =>
			{
				try
				{
					return await lease.PersistAttemptAsync();
				}
				catch (Exception error)
				{
					GD.PushWarning($"Adventure tutorial victory persistence attempt failed: {error.Message}");
					return Error.Failed;
				}
			},
			result => result == Error.Ok,
			result => result != Error.Ok,
			abortOperation: lease.Abort,
			onSucceeded: () =>
			{
				lease.CompleteSuccess();
				return true;
			});
		return gate.Succeeded;
	}

	private async Task<AdventureSaveGateResult<T>> ExecuteAdventureSaveGateAsync<T>(
		Func<T> attempt,
		Func<T, bool> succeeded,
		Func<T, bool> retryable,
		Func<bool>? onRetryableFailure = null,
		Func<bool>? onSucceeded = null)
	{
		return await AdventureSaveGate.ExecuteAsync(
			() => Task.FromResult(attempt()),
			succeeded,
			retryable,
			onRetryableFailure: onRetryableFailure,
			onSucceeded: onSucceeded,
			reportPresentationException: error => GD.PushError(
				$"Adventure operation presentation callback threw after commit: {error}"));
	}

	private async Task<AdventureSaveGateResult<T>> ExecutePreparedAdventureSaveGateAsync<T>(
		AdventureRunSessionStore.PreparedAdventureOperation<T> operation,
		Func<T, bool> succeeded,
		Func<T, bool> retryable,
		Func<bool>? onRetryableFailure = null,
		Func<bool>? onSucceeded = null)
	{
		AdventureSaveGateResult<T> gate = await AdventureSaveGate.ExecutePreparedAsync(
			operation,
			succeeded,
			retryable,
			onRetryableFailure,
			onSucceeded,
			reportAttemptException: error => GD.PushError(
				$"Adventure prepared operation failed unexpectedly: {error}"),
			reportPresentationException: error => GD.PushError(
				$"Adventure operation presentation callback threw after commit: {error}"),
			reportPresentationFailure: () => GD.PushError(
				"Adventure operation committed but its presentation callback failed."));
		return gate;
	}

	private bool RestoreAdventureSaveGateRuleSnapshot(
		MatchStateSnapshot snapshot,
		string stateName)
	{
		if (TryRestoreAuthorityRuleSnapshotPreservingAdventureTransaction(
			snapshot,
			out string error))
		{
			return true;
		}

		GD.PushError($"Failed to restore adventure save gate {stateName} state: {error}");
		return false;
	}

	private bool IsAdventureSaveGateBlocking()
	{
		return _adventureSaveGateCoordinator?.IsBlocking == true;
	}

	private void HandleAdventureSaveGateSucceeded()
	{
		if (GodotObject.IsInstanceValid(this) && _winnerPlayerIndex < 0)
		{
			HideWinnerBanner();
		}
	}

	private void HandleAdventureSaveGateAbortStarted()
	{
		AdvanceTurnSequenceGeneration();
		ClearPendingAdventureUpgradeFlow();
		CancelAdventureUpgradeSelection();
		if (_playerHands.Count >= MaxPlayers
			&& _playerItemHands.Count >= MaxPlayers)
		{
			CancelPendingItemSelection();
		}
		else
		{
			ClearPendingItemSelectionState();
		}
		ClearAdventureInteractionStateForBattleEnd();
	}

	private void PresentAdventureSaveStatus(string localizationKey)
	{
		// Routine async saves should block input without flashing the result modal.
		// Retry and terminal failure states still need visible feedback.
		if (_winnerPlayerIndex < 0
			&& _adventureSaveGateCoordinator?.State == AdventureSaveGateState.Saving)
		{
			return;
		}

		_winnerLabel.Text = Localize(localizationKey);
		_winnerLabel.Visible = true;
		ApplyWinnerLabelLayout();
		if (_winnerBannerRoot != null)
		{
			_winnerBannerRoot.SetActionsVisible(false);
			_winnerBannerRoot.Visible = true;
			_winnerBannerRoot.Modulate = Colors.White;
		}
		if (_winnerBannerArt != null)
		{
			_winnerBannerArt.Visible = false;
		}
		if (_resultAvatar != null)
		{
			_resultAvatar.Visible = false;
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
		if (TryGetConfiguredPlayerName(playerIndex, out string configuredName))
		{
			return configuredName;
		}

		if ((IsNetworkMultiplayerMode() && IsLocalHandPlayer(playerIndex))
			|| (!IsNetworkMultiplayerMode() && playerIndex == MainPlayerIndex))
		{
			return PlayerNameRules.DefaultPlayerName;
		}

		if (adventure_mode && IsAdventureEnemyPlayer(playerIndex))
		{
			return LocalizeAvatarDisplayName(CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id));
		}

		return LocalizeFormat("GAME_PLAYER", playerIndex + 1);
	}

	private void EnsureLocalPlayerNameConfigured()
	{
		if (IsNetworkMultiplayerMode()
			|| (RuntimeConfiguration.PlayerNames.Length > MainPlayerIndex
				&& !string.IsNullOrWhiteSpace(RuntimeConfiguration.PlayerNames[MainPlayerIndex])))
		{
			return;
		}

		string normalizedName = PlayerNameRules.Normalize(GameSaveStore.LoadActiveProfile().PlayerName);
		ImmutableArray<string> names = RuntimeConfiguration.PlayerNames;
		_runtimeConfiguration = RuntimeConfiguration with
		{
			PlayerNames = names.IsDefaultOrEmpty
				? ImmutableArray.Create(normalizedName)
				: names.SetItem(0, normalizedName),
		};
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
			TrackBoardTask(DealCardsToPlayersWithAdventureGateAsync(playOpeningDealAnimation: true), nameof(DealCardsToPlayersWithAdventureGateAsync));
			return;
		}

		if (_settledAdventureDefeatResolution == AdventureDefeatResolution.RestartJourney)
		{
			TrackBoardTask(RestartAdventureRunFromWinnerAsync(beginNewRun: false), nameof(RestartAdventureRunFromWinnerAsync));
			return;
		}

		if (ShouldRestartAdventureRunFromWinner())
		{
			TrackBoardTask(RestartAdventureRunFromWinnerAsync(beginNewRun: true), nameof(RestartAdventureRunFromWinnerAsync));
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
		if (IsNetworkMultiplayerMode())
		{
			_networkReturnToLobbyRequested = true;
			UpdateWinnerPrimaryButton();
			if (GetNetworkManager()?.RequestReturnToLobbyAfterMatch() != true)
			{
				_networkReturnToLobbyRequested = false;
				UpdateWinnerPrimaryButton();
			}
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

		TrackBoardTask(StartNextAdventureStageWithTransitionAsync(gameBoardScene, nextAvatarId), nameof(StartNextAdventureStageWithTransitionAsync));
	}

	private bool ShouldRestartAdventureRunFromWinner()
	{
		return ShouldUseAdventureUpgradeSystem()
			&& _winnerPlayerIndex >= 0
			&& (_winnerPlayerIndex != MainPlayerIndex || !TryGetNextAdventureStageAvatarId(out _));
	}

	private async Task RestartAdventureRunFromWinnerAsync(bool beginNewRun)
	{
		string firstAvatarId = GetFirstMainAdventureAvatarId();
		string gameBoardScenePath = AdventureAvatarMenuScene.GetGameBoardScenePathForAdventureAvatar(firstAvatarId);
		PackedScene? gameBoardScene = GD.Load<PackedScene>(gameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {gameBoardScenePath}");
			HideWinnerBanner();
			await DealCardsToPlayersWithAdventureGateAsync(playOpeningDealAnimation: true);
			return;
		}

		if (beginNewRun)
		{
			AdventureRunSessionStore? store = GetAdventureRunSessionStore();
			AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult> operation =
				store?.PrepareRestartMainRun(1, GetFirstMainAdventureAvatarId())
				?? AdventureRunSessionStore.PreparedAdventureOperation<AdventureRunWriteResult>.Rejected(
					AdventureRunWriteResult.SaveFailed,
					static result => result);
			AdventureSaveGateResult<AdventureRunWriteResult> gate =
				await ExecutePreparedAdventureSaveGateAsync(
					operation,
					result => result == AdventureRunWriteResult.Succeeded,
					result => result == AdventureRunWriteResult.SaveFailed);
			if (!gate.Succeeded)
			{
				GD.PushWarning($"Failed to start a new adventure run from results: {gate.Result}");
				return;
			}
		}
		TrackBoardTask(StartNextAdventureStageWithTransitionAsync(gameBoardScene, firstAvatarId), nameof(StartNextAdventureStageWithTransitionAsync));
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
			GameBoardLaunchOptions options = GameBoardLaunchOptions.AdventureMain(
				nextAvatarId,
				TryGetConfiguredPlayerName(MainPlayerIndex, out string playerName)
					? playerName
					: string.Empty);
			if (!GameBoardSceneFactory.TryCreate(gameBoardScene, options, out GameBoardScene gameBoard, out string error))
			{
				GD.PushError($"Unable to create next adventure game board: {error}");
				return;
			}
			GetTree().Root.AddChild(gameBoard);
			GetTree().CurrentScene = gameBoard;
			QueueFree();
		});
	}

	private void UpdateWinnerPrimaryButton()
	{
		if (_winnerBannerRoot == null
			|| !GodotObject.IsInstanceValid(_winnerBannerRoot)
			|| _winnerReplayButton == null
			|| !GodotObject.IsInstanceValid(_winnerReplayButton))
		{
			return;
		}

		bool showPrimaryAction = !ModePolicy.IsAdventureTutorial;
		_winnerBannerRoot.SetPrimaryActionVisible(showPrimaryAction);
		if (!showPrimaryAction)
		{
			return;
		}

		bool shouldAdvanceAdventure = ShouldAdvanceAdventureFromWinner();
		bool hasAdventureDefeatAction = _settledAdventureDefeatResolution.HasValue;
		_winnerReplayButton.Disabled = _networkReturnToLobbyRequested
			|| !string.IsNullOrEmpty(AdventureSaveStatusLocalizationKey)
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
		_winnerBannerRoot.ReturnButton.Disabled = !ResultPresentationRules.ShouldEnableWinnerReturnAction(
			!string.IsNullOrEmpty(AdventureSaveStatusLocalizationKey));
	}

	private bool ShouldAdvanceAdventureFromWinner()
	{
		return ModePolicy.IsAdventureMain
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
		return ModePolicy.IsAdventure ? "GAME_RETURN_ADVENTURE" : "GAME_RETURN_MAIN_MENU";
	}

	private Action GetWinnerReturnAction()
	{
		return ModePolicy.IsAdventure ? ReturnToAdventureMenu : ReturnToMainMenu;
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

		if (_matchOutcomeKind == MatchOutcomeKind.Draw)
		{
			_winnerBannerArt.Visible = false;
			return;
		}

		_winnerBannerArt.Visible = true;
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
