using System.Globalization;
using Godot;

public partial class AdventureAvatarMenuScene
{
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
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarUnlockPromptRoot.Visible = false;
		_avatarUnlockPromptRoot.MouseFilter = MouseFilterEnum.Ignore;
		_avatarUnlockPromptRoot.Modulate = Colors.White;
		if (_avatarUnlockPromptPanel != null)
		{
			_avatarUnlockPromptPanel.Scale = Vector2.One;
		}

		_isShowingAvatarUnlockPrompt = false;
		SetAdventureMenuButtonsDisabled(false);
		RefreshSelection();
		if (showNext)
		{
			ShowPendingAdventureAvatarUnlockPrompt();
		}
		if (!_isShowingAvatarUnlockPrompt)
		{
			FocusDefaultControlDeferred();
		}
	}

	private void EnsureAvatarUnlockPrompt()
	{
		if (_avatarUnlockPromptRoot != null && GodotObject.IsInstanceValid(_avatarUnlockPromptRoot))
		{
			return;
		}

		_avatarUnlockPromptRoot = GetNodeOrNull<AvatarUnlockPrompt>("%AvatarUnlockPromptRoot");
		if (_avatarUnlockPromptRoot == null)
		{
			GD.PushError("Adventure avatar menu scene is missing AvatarUnlockPromptRoot.");
			return;
		}

		_avatarUnlockPromptPanel = _avatarUnlockPromptRoot.PromptPanel;
		_avatarUnlockPromptButton = _avatarUnlockPromptRoot.ConfirmButton;
		_avatarUnlockPromptAvatar = _avatarUnlockPromptRoot.UnlockedAvatar;
		_avatarUnlockPromptRoot.CloseRequested += HideAvatarUnlockPrompt;
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
