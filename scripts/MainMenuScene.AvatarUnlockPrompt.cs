using System.Globalization;
using Godot;

public partial class MainMenuScene : Control
{
	private const float AvatarUnlockPromptFadeSeconds = 0.18f;

	private AvatarUnlockPrompt? _avatarUnlockPromptRoot;
	private Control? _avatarUnlockPromptPanel;
	private Button? _avatarUnlockPromptButton;
	private CharacterAvatar? _avatarUnlockPromptAvatar;
	private Tween? _avatarUnlockPromptTween;
	private bool _isShowingAvatarUnlockPrompt;
	private bool _isConsumingPendingAvatarUnlock;
	private bool _isProbingPendingAvatarUnlock;
	private uint _pendingAvatarUnlockProbeToken;
#if DECKLAND_TEST_HOOKS
	private ProfileWriteLease? _debugPendingAvatarUnlockProbeLease;
#endif

	private async void ShowPendingAdventureAvatarUnlockPrompt()
	{
		if (_isShowingAvatarUnlockPrompt
			|| _isConsumingPendingAvatarUnlock
			|| _isProbingPendingAvatarUnlock
			|| !CanShowPendingAvatarUnlockPrompt())
		{
			return;
		}

		uint probeToken = ++_pendingAvatarUnlockProbeToken;
		_isProbingPendingAvatarUnlock = true;
		string unlockedAvatarId = string.Empty;
		try
		{
			unlockedAvatarId = await CharacterAvatarCatalog.PeekPendingAdventureAvatarUnlockAsync();
		}
		catch (System.Exception error)
		{
			GD.PushWarning($"Failed to inspect pending adventure avatar unlock: {error.Message}");
		}
		finally
		{
			_isProbingPendingAvatarUnlock = false;
		}
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}
		if (probeToken != _pendingAvatarUnlockProbeToken)
		{
			ShowPendingAdventureAvatarUnlockPrompt();
			return;
		}
		if (string.IsNullOrWhiteSpace(unlockedAvatarId))
		{
			return;
		}

		CharacterAvatarDefinition unlockedAvatar = CharacterAvatarCatalog.GetById(unlockedAvatarId);
		if (unlockedAvatar.AdventureStageNumber <= 0
			|| !CanShowPendingAvatarUnlockPrompt()
			|| !TryShowAvatarUnlockPrompt(unlockedAvatar))
		{
			RestoreMainMenuAfterAvatarUnlockPromptFailure();
			return;
		}

		_isConsumingPendingAvatarUnlock = true;
		Error consumeResult;
		try
		{
			consumeResult = await CharacterAvatarCatalog.ConsumePendingAdventureAvatarUnlockAsync(unlockedAvatarId);
		}
		catch (System.Exception error)
		{
			GD.PushWarning($"Failed to consume pending adventure avatar unlock: {error.Message}");
			consumeResult = Error.Failed;
		}
		finally
		{
			_isConsumingPendingAvatarUnlock = false;
		}
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		if (consumeResult == Error.Ok)
		{
			if (!_isShowingAvatarUnlockPrompt)
			{
				ShowPendingAdventureAvatarUnlockPrompt();
			}
			return;
		}

		HideAvatarUnlockPrompt(showNext: consumeResult == Error.DoesNotExist);
	}

	private bool TryShowAvatarUnlockPrompt(CharacterAvatarDefinition avatarDefinition)
	{
		if (!TryEnsureAvatarUnlockPrompt())
		{
			return false;
		}
		AvatarUnlockPrompt promptRoot = _avatarUnlockPromptRoot!;

		_isShowingAvatarUnlockPrompt = true;
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		RefreshAvatarUnlockPromptText();
		SetMainMenuButtonsDisabled(true);

		if (_avatarUnlockPromptAvatar != null && GodotObject.IsInstanceValid(_avatarUnlockPromptAvatar))
		{
			_avatarUnlockPromptAvatar.AvatarRoot = avatarDefinition.RootPath;
			_avatarUnlockPromptAvatar.AudioConfig = avatarDefinition.AudioConfig;
			_avatarUnlockPromptAvatar.AccentColor = GetAvatarUnlockAccentColor(avatarDefinition);
			_avatarUnlockPromptAvatar.Visible = true;
			_avatarUnlockPromptAvatar.PlayVictory();
		}

		_avatarUnlockPromptTween?.Kill();
		promptRoot.Visible = true;
		promptRoot.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Inherited;
		promptRoot.MouseFilter = MouseFilterEnum.Stop;
		promptRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptPanel.Scale = new Vector2(0.94f, 0.94f);
			_avatarUnlockPromptPanel.PivotOffset = _avatarUnlockPromptPanel.CustomMinimumSize / 2.0f;
		}

		_avatarUnlockPromptTween = CreateTween();
		_avatarUnlockPromptTween.SetTrans(Tween.TransitionType.Cubic);
		_avatarUnlockPromptTween.SetEase(Tween.EaseType.Out);
		_avatarUnlockPromptTween.TweenProperty(promptRoot, "modulate:a", 1.0f, AvatarUnlockPromptFadeSeconds);
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptTween.Parallel().TweenProperty(_avatarUnlockPromptPanel, "scale", Vector2.One, AvatarUnlockPromptFadeSeconds);
		}

		_avatarUnlockPromptButton?.GrabFocus();
		return true;
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
		if (!_isShowingAvatarUnlockPrompt)
		{
			if (showNext)
			{
				ShowPendingAdventureAvatarUnlockPrompt();
			}
			return;
		}

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarUnlockPromptTween?.Kill();
		_avatarUnlockPromptTween = null;
		_avatarUnlockPromptRoot.Visible = false;
		_avatarUnlockPromptRoot.MouseFilter = MouseFilterEnum.Ignore;
		_avatarUnlockPromptRoot.MouseBehaviorRecursive = MouseBehaviorRecursiveEnum.Disabled;
		_avatarUnlockPromptRoot.Modulate = Colors.White;
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptPanel.Scale = Vector2.One;
		}

		_isShowingAvatarUnlockPrompt = false;
		if (CanShowPendingAvatarUnlockPrompt())
		{
			SetMainMenuButtonsDisabled(false);
			SyncPrimaryMenuFocusState();
			if (showNext)
			{
				ShowPendingAdventureAvatarUnlockPrompt();
			}
			CallDeferred(nameof(FocusInitialMainMenuButton));
		}
	}

	private bool TryEnsureAvatarUnlockPrompt()
	{
		if (_avatarUnlockPromptRoot != null && GodotObject.IsInstanceValid(_avatarUnlockPromptRoot))
		{
			return true;
		}

		PackedScene? promptScene = GD.Load<PackedScene>(AvatarUnlockPromptScenePath);
		if (promptScene?.Instantiate() is not AvatarUnlockPrompt prompt)
		{
			GD.PushError($"Missing avatar unlock prompt scene: {AvatarUnlockPromptScenePath}");
			return false;
		}

		_avatarUnlockPromptRoot = prompt;
		AddChild(prompt);
		_avatarUnlockPromptPanel = prompt.PromptPanel;
		_avatarUnlockPromptButton = prompt.ConfirmButton;
		_avatarUnlockPromptAvatar = prompt.UnlockedAvatar;
		SuppressDefaultPressedSfx(_avatarUnlockPromptButton);
		prompt.CloseRequested += HideAvatarUnlockPrompt;
		return true;
	}

	private void RestoreMainMenuAfterAvatarUnlockPromptFailure()
	{
		if (!CanShowPendingAvatarUnlockPrompt())
		{
			return;
		}

		SetMainMenuButtonsDisabled(false);
		SyncPrimaryMenuFocusState();
		CallDeferred(nameof(FocusInitialMainMenuButton));
	}

	private void InvalidatePendingAvatarUnlockProbe()
	{
		_pendingAvatarUnlockProbeToken++;
	}

	private bool CanShowPendingAvatarUnlockPrompt()
	{
		return !_quitInProgress
			&& !_isStartingGameTransition
			&& !_isMenuCameraTransitioning
			&& !IsPrimaryMenuReturning()
			&& !IsMenuOverlayVisible();
	}

	private void RefreshAvatarUnlockPromptText()
	{
		_avatarUnlockPromptRoot?.RefreshLocalizedText(Localize);
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

#if DECKLAND_TEST_HOOKS
	public bool DebugHoldPendingAvatarUnlockProbeLock()
	{
		if (_debugPendingAvatarUnlockProbeLease != null)
		{
			return false;
		}

		_debugPendingAvatarUnlockProbeLease = GameSaveProfileWriteCoordinator.AcquireAsync(
			profile => profile,
			forcePersist: false).GetAwaiter().GetResult();
		return _debugPendingAvatarUnlockProbeLease != null;
	}

	public void DebugReleasePendingAvatarUnlockProbeLock()
	{
		_debugPendingAvatarUnlockProbeLease?.Abort();
		_debugPendingAvatarUnlockProbeLease = null;
	}

	public bool DebugIsPendingAvatarUnlockProbeActive()
	{
		return _isProbingPendingAvatarUnlock;
	}
#endif
}
