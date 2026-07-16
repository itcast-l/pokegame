using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private void StartMultiplayerLobby()
	{
		if (_isStartingGameTransition)
		{
			return;
		}

		PackedScene? lobbyScene = GD.Load<PackedScene>(MultiplayerLobbyScenePath);
		if (lobbyScene == null)
		{
			GD.PushError($"Missing multiplayer lobby scene: {MultiplayerLobbyScenePath}");
			return;
		}

		_isStartingGameTransition = true;
		SetMainMenuButtonsDisabled(true);
		Node lobby = lobbyScene.Instantiate();
		GetTree().Root.AddChild(lobby);
		GetTree().CurrentScene = lobby;
		QueueFree();
	}

	private void SetMainMenuButtonsDisabled(bool disabled)
	{
		_avatarEntry.Button.Disabled = disabled;
		_singlePlayerButton.Disabled = disabled;
		_adventureButton.Disabled = disabled;
		_multiplayerButton.Disabled = disabled;
		_skillCodexButton.Disabled = disabled;
		_settingsButton.Disabled = disabled;
		_exitButton.Disabled = disabled;
	}
}
