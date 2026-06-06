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
		avatarEditor.CurrentPlayerName = _playerName;
		avatarEditor.ProfileSaved += OnProfileSaved;

		_avatarEditDialogLayer = new CanvasLayer
		{
			Name = "AvatarEditDialogLayer",
			Layer = AvatarEditDialogLayer,
		};
		AddChild(_avatarEditDialogLayer);

		_avatarEntry.Visible = false;
		SetMainMenuButtonsDisabled(true);
		avatarEditor.CloseRequested += CloseAvatarEditor;
		_avatarEditDialogLayer.AddChild(avatarEditor);
		avatarEditor.FocusDefaultControlDeferred();
	}

	private void CloseAvatarEditor()
	{
		_avatarEntry.Visible = true;
		SetMainMenuButtonsDisabled(false);

		if (_avatarEditDialogLayer == null || !GodotObject.IsInstanceValid(_avatarEditDialogLayer))
		{
			_avatarEditDialogLayer = null;
			CallDeferred(nameof(FocusInitialMainMenuButton));
			return;
		}

		_avatarEditDialogLayer.QueueFree();
		_avatarEditDialogLayer = null;
		CallDeferred(nameof(FocusInitialMainMenuButton));
	}

	private void OnProfileSaved(string avatarId, string playerName)
	{
		_selectedAvatarId = CharacterAvatarCatalog.NormalizeUnlockedAvatarId(avatarId);
		_playerName = PlayerNameRules.Normalize(playerName);
		ApplySelectedAvatar();
		ApplyPlayerName();
		SavePlayerProfile();
	}

	private void ApplySelectedAvatar()
	{
		if (_avatarEntry == null || !GodotObject.IsInstanceValid(_avatarEntry))
		{
			return;
		}

		_avatarEntry.SetAvatar(CharacterAvatarCatalog.GetById(_selectedAvatarId));
	}

	private void ApplyPlayerName()
	{
		if (_avatarEntry == null || !GodotObject.IsInstanceValid(_avatarEntry))
		{
			return;
		}

		_avatarEntry.SetPlayerName(_playerName);
	}

}
