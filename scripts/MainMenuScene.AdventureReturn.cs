using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	public void PrepareAdventureReturnTransition()
	{
		_playAdventureReturnTransition = true;
	}

	private async void PlayAdventureReturnTransition()
	{
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		ResetMenuCamera();
		await ToSignal(
			GetTree().CreateTimer(GetMenuCameraHomeTotalSeconds()),
			SceneTreeTimer.SignalName.Timeout);

		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		_adventureTransitionRoot?.QueueFree();
		_adventureTransitionRoot = null;
		_adventureTransitionMotion = null;
		_avatarEntry.Visible = true;
		SetMainMenuButtonsDisabled(false);
		PrepareAdventureAvatarMenuCache();
		ShowPendingAdventureAvatarUnlockPrompt();
		CallDeferred(nameof(FocusInitialMainMenuButton));
	}
}
