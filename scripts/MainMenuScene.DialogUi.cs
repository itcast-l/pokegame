using Godot;

public partial class MainMenuScene : Control
{
	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? GetMainMenuLocalizationFallback(key) : text;
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

		if (_subtitleLabel != null)
		{
			_subtitleLabel.Text = Localize("APP_SUBTITLE");
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
