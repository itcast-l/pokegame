using Godot;

public partial class CharacterAvatarEditScene : Control
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";

	private Button _backButton = default!;

	public override void _Ready()
	{
		_backButton = GetNode<Button>("Header/HeaderMargin/HeaderLayout/BackButton");
		_backButton.Pressed += ReturnToMainMenu;
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			ReturnToMainMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	private void ReturnToMainMenu()
	{
		PackedScene? mainMenuScene = GD.Load<PackedScene>(MainMenuScenePath);
		if (mainMenuScene == null)
		{
			GD.PushError($"Missing main menu scene: {MainMenuScenePath}");
			return;
		}

		GetTree().ChangeSceneToPacked(mainMenuScene);
	}
}
