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
		SuppressDefaultPressedSfx(_avatarUnlockPromptButton);
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
			"SETTINGS_CONTROLS" => traditionalChinese ? "操作設定" : simplifiedChinese ? "操作设置" : "Controls",
			"SETTINGS_CONTROLS_EDIT" => traditionalChinese ? "編輯" : simplifiedChinese ? "编辑" : "Edit",
			"SETTINGS_CONTROLS_TITLE" => traditionalChinese ? "鍵盤 / 手把映射" : simplifiedChinese ? "键盘 / 手柄映射" : "Keyboard / Gamepad Mapping",
			"SETTINGS_CONTROLS_MESSAGE" => traditionalChinese ? "分別調整鍵盤與手把的主要操作。左右也會同步控制手牌切換。" : simplifiedChinese ? "分别调整键盘与手柄的主要操作。左右也会同步控制手牌切换。" : "Adjust primary keyboard and gamepad controls. Left and right also drive hand selection.",
			"SETTINGS_CONTROLS_KEYBOARD_HEADER" => traditionalChinese ? "鍵盤" : simplifiedChinese ? "键盘" : "Keyboard",
			"SETTINGS_CONTROLS_GAMEPAD_HEADER" => traditionalChinese ? "手把" : simplifiedChinese ? "手柄" : "Gamepad",
			"SETTINGS_CONTROLS_HINT_IDLE" => traditionalChinese ? "選擇一個欄位即可重新綁定。" : simplifiedChinese ? "选择一个栏位即可重新绑定。" : "Select a field to rebind it.",
			"SETTINGS_CONTROLS_HINT_CAPTURE" => traditionalChinese ? "按下新的輸入來綁定「{0}」。" : simplifiedChinese ? "按下新的输入来绑定“{0}”。" : "Press a new input to bind \"{0}\".",
			"SETTINGS_CONTROLS_LISTENING" => traditionalChinese ? "等待輸入..." : simplifiedChinese ? "等待输入..." : "Listening...",
			"SETTINGS_INPUT_MOVE_UP" => traditionalChinese ? "上移" : simplifiedChinese ? "上移" : "Move Up",
			"SETTINGS_INPUT_MOVE_DOWN" => traditionalChinese ? "下移" : simplifiedChinese ? "下移" : "Move Down",
			"SETTINGS_INPUT_MOVE_LEFT" => traditionalChinese ? "左移 / 上一張" : simplifiedChinese ? "左移 / 上一张" : "Move Left / Prev",
			"SETTINGS_INPUT_MOVE_RIGHT" => traditionalChinese ? "右移 / 下一張" : simplifiedChinese ? "右移 / 下一张" : "Move Right / Next",
			"SETTINGS_INPUT_CONFIRM" => traditionalChinese ? "確認" : simplifiedChinese ? "确认" : "Confirm",
			"SETTINGS_INPUT_CANCEL" => traditionalChinese ? "返回" : simplifiedChinese ? "返回" : "Cancel",
			"SETTINGS_INPUT_PAUSE" => traditionalChinese ? "暫停" : simplifiedChinese ? "暂停" : "Pause",
			"SETTINGS_INPUT_UNBOUND" => traditionalChinese ? "未綁定" : simplifiedChinese ? "未绑定" : "Unbound",
			"SETTINGS_INPUT_GAMEPAD_A" => "A",
			"SETTINGS_INPUT_GAMEPAD_B" => "B",
			"SETTINGS_INPUT_GAMEPAD_X" => "X",
			"SETTINGS_INPUT_GAMEPAD_Y" => "Y",
			"SETTINGS_INPUT_GAMEPAD_BACK" => traditionalChinese ? "返回鍵" : simplifiedChinese ? "返回键" : "Back",
			"SETTINGS_INPUT_GAMEPAD_GUIDE" => traditionalChinese ? "主頁鍵" : simplifiedChinese ? "主页键" : "Guide",
			"SETTINGS_INPUT_GAMEPAD_START" => "Start",
			"SETTINGS_INPUT_GAMEPAD_LEFT_STICK" => traditionalChinese ? "左搖桿" : simplifiedChinese ? "左摇杆" : "L Stick",
			"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK" => traditionalChinese ? "右搖桿" : simplifiedChinese ? "右摇杆" : "R Stick",
			"SETTINGS_INPUT_GAMEPAD_LB" => "LB",
			"SETTINGS_INPUT_GAMEPAD_RB" => "RB",
			"SETTINGS_INPUT_GAMEPAD_DPAD_UP" => traditionalChinese ? "方向鍵上" : simplifiedChinese ? "方向键上" : "D-Pad Up",
			"SETTINGS_INPUT_GAMEPAD_DPAD_DOWN" => traditionalChinese ? "方向鍵下" : simplifiedChinese ? "方向键下" : "D-Pad Down",
			"SETTINGS_INPUT_GAMEPAD_DPAD_LEFT" => traditionalChinese ? "方向鍵左" : simplifiedChinese ? "方向键左" : "D-Pad Left",
			"SETTINGS_INPUT_GAMEPAD_DPAD_RIGHT" => traditionalChinese ? "方向鍵右" : simplifiedChinese ? "方向键右" : "D-Pad Right",
			"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_UP" => traditionalChinese ? "左搖桿上" : simplifiedChinese ? "左摇杆上" : "Left Stick Up",
			"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_DOWN" => traditionalChinese ? "左搖桿下" : simplifiedChinese ? "左摇杆下" : "Left Stick Down",
			"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_LEFT" => traditionalChinese ? "左搖桿左" : simplifiedChinese ? "左摇杆左" : "Left Stick Left",
			"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_RIGHT" => traditionalChinese ? "左搖桿右" : simplifiedChinese ? "左摇杆右" : "Left Stick Right",
			"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_UP" => traditionalChinese ? "右搖桿上" : simplifiedChinese ? "右摇杆上" : "Right Stick Up",
			"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_DOWN" => traditionalChinese ? "右搖桿下" : simplifiedChinese ? "右摇杆下" : "Right Stick Down",
			"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_LEFT" => traditionalChinese ? "右搖桿左" : simplifiedChinese ? "右摇杆左" : "Right Stick Left",
			"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_RIGHT" => traditionalChinese ? "右搖桿右" : simplifiedChinese ? "右摇杆右" : "Right Stick Right",
			"SETTINGS_INPUT_GAMEPAD_LT" => "LT",
			"SETTINGS_INPUT_GAMEPAD_RT" => "RT",
			"SETTINGS_RESOLUTION" => traditionalChinese ? "解析度" : simplifiedChinese ? "分辨率" : "Resolution",
			"SETTINGS_MAIN_MENU_MUSIC_TRACK" => traditionalChinese ? "主選單音樂曲目" : simplifiedChinese ? "主菜单音乐曲目" : "Main Menu Music Track",
			"SETTINGS_BACKGROUND_MUSIC" => traditionalChinese ? "背景音樂" : simplifiedChinese ? "背景音乐" : "Background Music",
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
