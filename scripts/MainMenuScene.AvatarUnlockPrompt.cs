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

	private void ShowPendingAdventureAvatarUnlockPrompt()
	{
		if (_isShowingAvatarUnlockPrompt)
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
		SetMainMenuButtonsDisabled(false);
		ShowPendingAdventureAvatarUnlockPrompt();
		CallDeferred(nameof(FocusInitialMainMenuButton));
	}

	private void EnsureAvatarUnlockPrompt()
	{
		if (_avatarUnlockPromptRoot != null && GodotObject.IsInstanceValid(_avatarUnlockPromptRoot))
		{
			return;
		}

		PackedScene? promptScene = GD.Load<PackedScene>(AvatarUnlockPromptScenePath);
		if (promptScene?.Instantiate() is not AvatarUnlockPrompt prompt)
		{
			GD.PushError($"Missing avatar unlock prompt scene: {AvatarUnlockPromptScenePath}");
			return;
		}

		_avatarUnlockPromptRoot = prompt;
		AddChild(prompt);
		_avatarUnlockPromptPanel = prompt.PromptPanel;
		_avatarUnlockPromptButton = prompt.ConfirmButton;
		_avatarUnlockPromptAvatar = prompt.UnlockedAvatar;
		prompt.CloseRequested += HideAvatarUnlockPrompt;
	}

	private void RefreshAvatarUnlockPromptText()
	{
		_avatarUnlockPromptRoot?.RefreshLocalizedText(Localize);
	}

	private static string GetMainMenuLocalizationFallback(string key)
	{
		string locale = TranslationServer.GetLocale();
		bool traditionalChinese = locale.StartsWith("zh_TW", System.StringComparison.OrdinalIgnoreCase)
			|| locale.StartsWith("zh_HK", System.StringComparison.OrdinalIgnoreCase);
		bool simplifiedChinese = locale.StartsWith("zh", System.StringComparison.OrdinalIgnoreCase) && !traditionalChinese;
		return key switch
		{
			"APP_NAME" => traditionalChinese ? "牌境紀行" : simplifiedChinese ? "牌境纪行" : "Deckland",
			"APP_SUBTITLE" => traditionalChinese ? "卡牌對局" : simplifiedChinese ? "卡牌对局" : "Card Battler",
			"AVATAR_UNLOCK_TITLE" => traditionalChinese ? "頭像已解鎖" : simplifiedChinese ? "头像已解锁" : "Avatar Unlocked",
			"AVATAR_UNLOCK_MESSAGE" => traditionalChinese ? "獲得了 {0} 的頭像" : simplifiedChinese ? "获得了 {0} 的头像" : "You got {0}'s avatar",
			"AVATAR_UNLOCK_CONFIRM" => traditionalChinese || simplifiedChinese ? "收下" : "Collect",
			"SETTINGS_RESOLUTION" => traditionalChinese ? "解析度" : simplifiedChinese ? "分辨率" : "Resolution",
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
