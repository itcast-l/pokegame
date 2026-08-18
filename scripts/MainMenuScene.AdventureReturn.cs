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
		ResetMenuCamera();
		_avatarEntry.Visible = true;
		uint returnToken = BeginPrimaryMenuReturn(
			() =>
			{
				_adventureTransitionRoot?.QueueFree();
				_adventureTransitionRoot = null;
				_adventureTransitionMotion = null;
				_avatarEntry.Visible = true;
				PrepareAdventureAvatarMenuCache();
			},
			() => CallDeferred(nameof(FocusInitialMainMenuButton)));
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		await ToSignal(
			GetTree().CreateTimer(GetMenuCameraHomeTotalSeconds()),
			SceneTreeTimer.SignalName.Timeout);

		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		CompletePrimaryMenuReturn(returnToken);
	}
}
