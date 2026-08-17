using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private async void StartMultiplayerLobby()
	{
		if (_isStartingGameTransition)
		{
			return;
		}

		PackedScene? lobbyScene = GD.Load<PackedScene>(MultiplayerLobbyScenePath);
		if (!TryLockMultiplayerLobbyTransition(lobbyScene, reportMissingResource: true))
		{
			return;
		}

		await CircularSceneTransition.PlaySceneSwapAsync(this, () =>
		{
			Node lobby = lobbyScene.Instantiate();
			GetTree().Root.AddChild(lobby);
			GetTree().CurrentScene = lobby;
			QueueFree();
		});

		if (GodotObject.IsInstanceValid(this) && IsInsideTree())
		{
			_isStartingGameTransition = false;
			SetMainMenuButtonsDisabled(false);
		}
	}

	private bool TryLockMultiplayerLobbyTransition(PackedScene? lobbyScene, bool reportMissingResource)
	{
		if (_isStartingGameTransition)
		{
			return false;
		}

		if (lobbyScene == null)
		{
			if (reportMissingResource)
			{
				GD.PushError($"Missing multiplayer lobby scene: {MultiplayerLobbyScenePath}");
			}
			return false;
		}

		_isStartingGameTransition = true;
		SetMainMenuButtonsDisabled(true);
		return true;
	}

#if DECKLAND_TEST_HOOKS
	public bool DebugRejectMissingMultiplayerLobbyResource()
	{
		return !TryLockMultiplayerLobbyTransition(null, reportMissingResource: false)
			&& !_isStartingGameTransition;
	}
#endif

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
