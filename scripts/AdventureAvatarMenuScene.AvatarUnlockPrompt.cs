using System.Globalization;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private const float AvatarUnlockPromptFadeSeconds = 0.18f;

	private async void ShowPendingAdventureAvatarUnlockPrompt()
	{
		if (_isEmbeddedVisualWarmup
			|| _isShowingAvatarUnlockPrompt
			|| _isConsumingPendingAvatarUnlock
			|| _isStartingAdventure
			|| _isReturningToMainMenu
			|| !IsVisibleInTree())
		{
			return;
		}
		if (!TryEnsureAvatarUnlockPrompt())
		{
			RestoreAdventureMenuAfterAvatarUnlockPromptFailure();
			return;
		}

		_isConsumingPendingAvatarUnlock = true;
		SetAdventureMenuButtonsDisabled(true);
		string unlockedAvatarId = string.Empty;
		try
		{
			unlockedAvatarId = await CharacterAvatarCatalog.ConsumePendingAdventureAvatarUnlockAsync();
		}
		catch (System.Exception error)
		{
			GD.PushWarning($"Failed to consume pending adventure avatar unlock: {error.Message}");
		}
		finally
		{
			_isConsumingPendingAvatarUnlock = false;
		}
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		LoadAdventureProfileSnapshot();
		if (_isShowingAvatarUnlockPrompt
			|| _isStartingAdventure
			|| _isReturningToMainMenu
			|| !IsVisibleInTree())
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(unlockedAvatarId))
		{
			RestoreAdventureMenuAfterAvatarUnlockPromptFailure();
			return;
		}

		CharacterAvatarDefinition unlockedAvatar = CharacterAvatarCatalog.GetById(unlockedAvatarId);
		if (unlockedAvatar.AdventureStageNumber <= 0)
		{
			RestoreAdventureMenuAfterAvatarUnlockPromptFailure();
			return;
		}

		if (!TryShowAvatarUnlockPrompt(unlockedAvatar))
		{
			RestoreAdventureMenuAfterAvatarUnlockPromptFailure();
		}
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
		SetAdventureMenuButtonsDisabled(true);

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

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
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
		RefreshSelection();
		if (showNext)
		{
			ShowPendingAdventureAvatarUnlockPrompt();
		}

		bool disableBackground =
			_isShowingAvatarUnlockPrompt
			|| _isShowingRunResumeDialog
			|| _isStartingAdventure
			|| _isReturningToMainMenu;
		SetAdventureMenuButtonsDisabled(disableBackground);
		if (_isShowingAvatarUnlockPrompt)
		{
			GrabFocusIfAvailable(_avatarUnlockPromptButton);
			return;
		}

		if (_isStartingAdventure || _isReturningToMainMenu)
		{
			return;
		}

		if (_isShowingRunResumeDialog)
		{
			_runResumeDialog?.FocusContinueButton();
			return;
		}

		FocusDefaultControlDeferred();
	}

#if DECKLAND_TEST_HOOKS
	public bool DebugShowAdventureAvatarUnlockPrompt(string avatarId)
	{
		CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(avatarId);
		if (avatarDefinition.AdventureStageNumber <= 0)
		{
			return false;
		}

		return TryShowAvatarUnlockPrompt(avatarDefinition)
			&& DebugIsAdventureAvatarUnlockPromptVisible();
	}

	public bool DebugIsAdventureAvatarUnlockPromptVisible()
	{
		return _isShowingAvatarUnlockPrompt
			&& _avatarUnlockPromptRoot != null
			&& GodotObject.IsInstanceValid(_avatarUnlockPromptRoot)
			&& _avatarUnlockPromptRoot.Visible;
	}

	public bool DebugIsAdventureAvatarUnlockConsumptionInProgress()
	{
		return _isConsumingPendingAvatarUnlock;
	}
#endif

	private bool TryEnsureAvatarUnlockPrompt()
	{
		if (_avatarUnlockPromptRoot != null && GodotObject.IsInstanceValid(_avatarUnlockPromptRoot))
		{
			return true;
		}

		_avatarUnlockPromptRoot = GetNodeOrNull<AvatarUnlockPrompt>("%AvatarUnlockPromptRoot");
		if (_avatarUnlockPromptRoot == null)
		{
			GD.PushError("Adventure avatar menu scene is missing AvatarUnlockPromptRoot.");
			return false;
		}

		_avatarUnlockPromptPanel = _avatarUnlockPromptRoot.PromptPanel;
		_avatarUnlockPromptButton = _avatarUnlockPromptRoot.ConfirmButton;
		_avatarUnlockPromptAvatar = _avatarUnlockPromptRoot.UnlockedAvatar;
		SuppressDefaultPressedSfx(_avatarUnlockPromptButton);
		_avatarUnlockPromptRoot.CloseRequested += HideAvatarUnlockPrompt;
		return true;
	}

	private void RestoreAdventureMenuAfterAvatarUnlockPromptFailure()
	{
		bool disableBackground =
			_isShowingAvatarUnlockPrompt
			|| _isShowingRunResumeDialog
			|| _isStartingAdventure
			|| _isReturningToMainMenu;
		SetAdventureMenuButtonsDisabled(disableBackground);
		if (!disableBackground && IsVisibleInTree())
		{
			FocusDefaultControlDeferred();
		}
	}

	private void RefreshAvatarUnlockPromptText()
	{
		_avatarUnlockPromptRoot?.RefreshLocalizedText(Localize);
	}

	private void SetAdventureMenuButtonsDisabled(bool disabled)
	{
		_backButton.Disabled = disabled;
		_startButton.Disabled = disabled || !IsStageUnlocked(_selectedAvatarId);
		foreach (Button adventureButton in _adventureButtons.Values)
		{
			if (GodotObject.IsInstanceValid(adventureButton))
			{
				adventureButton.Disabled = disabled;
			}
		}

		foreach ((string avatarId, Button stageButton) in _stageButtons)
		{
			if (GodotObject.IsInstanceValid(stageButton))
			{
				stageButton.Disabled = disabled || !IsStageUnlocked(avatarId);
			}
		}
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
