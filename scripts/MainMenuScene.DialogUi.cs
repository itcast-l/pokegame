using Godot;

public partial class MainMenuScene : Control
{
	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		if (text == key)
		{
			GD.PushError($"Missing main menu localization key: {key}");
		}

		return text;
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private void RefreshLocalizedText()
	{
		if (_titleLabel != null)
		{
			_titleLabel.Text = Localize("APP_NAME");
		}

		if (_singlePlayerButton != null)
		{
			_singlePlayerButton.Text = Localize("MENU_SINGLE_PLAYER");
		}

		if (_adventureButton != null)
		{
			_adventureButton.Text = Localize("MENU_ADVENTURE");
		}

		if (_multiplayerButton != null)
		{
			_multiplayerButton.Text = Localize("MENU_MULTIPLAYER");
		}

		if (_skillCodexButton != null)
		{
			_skillCodexButton.Text = Localize("MENU_SKILL_CODEX");
		}

		if (_settingsButton != null)
		{
			_settingsButton.Text = Localize("MENU_SETTINGS");
		}

		if (_exitButton != null)
		{
			_exitButton.Text = Localize("MENU_EXIT");
		}

		_settingsDialog?.RefreshLocalizedText(Localize);
		RefreshControlsDialogText();
		_exitDialog?.RefreshLocalizedText(Localize);
		RefreshWindowModeOptions();
		RefreshResolutionOptions();
		RefreshLanguageOptions();
		RefreshPlayerCountDialogOptions();
		RefreshAvatarUnlockPromptText();
	}
}
