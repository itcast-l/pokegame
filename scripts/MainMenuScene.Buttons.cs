using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private void SetMainMenuButtonsDisabled(bool disabled)
	{
		_singlePlayerButton.Disabled = disabled;
		_adventureButton.Disabled = disabled;
		_multiplayerButton.Disabled = disabled;
		_settingsButton.Disabled = disabled;
		_exitButton.Disabled = disabled;
	}
}
