using System;
using Godot;

public partial class MainMenuScene : Control
{
	private void OpenAvatarEditor()
	{
		if (_avatarEditDialogLayer != null && GodotObject.IsInstanceValid(_avatarEditDialogLayer))
		{
			return;
		}

		PackedScene? avatarEditScene = GD.Load<PackedScene>(AvatarEditScenePath);
		if (avatarEditScene == null)
		{
			GD.PushError($"Missing avatar edit scene: {AvatarEditScenePath}");
			return;
		}

		Node avatarEditNode = avatarEditScene.Instantiate();
		if (avatarEditNode is not CharacterAvatarEditScene avatarEditor)
		{
			GD.PushError($"{AvatarEditScenePath} root must use CharacterAvatarEditScene.");
			avatarEditNode.QueueFree();
			return;
		}

		avatarEditor.CurrentAvatarId = _selectedAvatarId;
		avatarEditor.AvatarSelected += OnAvatarSelected;

		_avatarEditDialogLayer = new CanvasLayer
		{
			Name = "AvatarEditDialogLayer",
			Layer = AvatarEditDialogLayer,
		};
		AddChild(_avatarEditDialogLayer);

		avatarEditor.CloseRequested += CloseAvatarEditor;
		_avatarEditDialogLayer.AddChild(avatarEditor);
	}

	private void CloseAvatarEditor()
	{
		if (_avatarEditDialogLayer == null || !GodotObject.IsInstanceValid(_avatarEditDialogLayer))
		{
			_avatarEditDialogLayer = null;
			return;
		}

		_avatarEditDialogLayer.QueueFree();
		_avatarEditDialogLayer = null;
	}

	private void OnAvatarSelected(string avatarId)
	{
		_selectedAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		ApplySelectedAvatar();
		SaveSettings();
	}

	private void ApplySelectedAvatar()
	{
		if (_avatarEntry == null || !GodotObject.IsInstanceValid(_avatarEntry))
		{
			return;
		}

		_avatarEntry.SetAvatar(CharacterAvatarCatalog.GetById(_selectedAvatarId));
	}
}
