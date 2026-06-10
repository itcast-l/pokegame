using System;
using Godot;

public partial class MainMenuScene : Control
{
	private async void OpenAvatarEditor()
	{
		if (!BeginMenuCameraPushIn(true))
		{
			return;
		}

		if (_avatarEditDialogRoot != null && GodotObject.IsInstanceValid(_avatarEditDialogRoot))
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
		_avatarEditDialogRoot = avatarEditor;
		_avatarEditDialogPanel = avatarEditor.DialogPanel;

		AddChild(avatarEditor);
		_avatarEntry.Visible = false;
		avatarEditor.CloseRequested += CloseAvatarEditor;
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		avatarEditor.Visible = true;
		SyncPrimaryMenuFocusState();
		avatarEditor.MouseFilter = MouseFilterEnum.Stop;
		avatarEditor.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		UpdateAvatarEditorPosition();
		if (_avatarEditDialogPanel != null)
		{
			_avatarEditDialogPanel.Scale = new Vector2(0.94f, 0.94f);
			_avatarEditDialogPanel.PivotOffset = _avatarEditDialogPanel.Size * 0.5f;
		}

		_avatarEditDialogTween?.Kill();
		_avatarEditDialogTween = CreateTween();
		_avatarEditDialogTween.SetParallel(true);
		_avatarEditDialogTween.TweenProperty(avatarEditor, "modulate:a", 1.0f, DialogFadeSeconds);
		if (_avatarEditDialogPanel != null)
		{
			_avatarEditDialogTween.TweenProperty(_avatarEditDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}

		avatarEditor.FocusDefaultControlDeferred();
		await CompleteMenuCameraPushIn();
	}

	private void CloseAvatarEditor()
	{
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarEntry.Visible = true;
		if (_avatarEditDialogRoot == null || !GodotObject.IsInstanceValid(_avatarEditDialogRoot))
		{
			_avatarEditDialogRoot = null;
			_avatarEditDialogPanel = null;
			_avatarEditDialogTween = null;
			ResetMenuCamera();
			CallDeferred(nameof(FocusInitialMainMenuButton));
			return;
		}

		CharacterAvatarEditScene avatarEditor = _avatarEditDialogRoot;
		PanelContainer? dialogPanel = _avatarEditDialogPanel;
		avatarEditor.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_avatarEditDialogTween?.Kill();
		_avatarEditDialogTween = CreateTween();
		_avatarEditDialogTween.SetParallel(true);
		_avatarEditDialogTween.TweenProperty(avatarEditor, "modulate:a", 0.0f, DialogFadeSeconds);
		if (dialogPanel != null && GodotObject.IsInstanceValid(dialogPanel))
		{
			_avatarEditDialogTween.TweenProperty(dialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.In);
		}

		_avatarEditDialogTween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(avatarEditor))
			{
				avatarEditor.Visible = false;
				avatarEditor.QueueFree();
			}

			if (_avatarEditDialogRoot == avatarEditor)
			{
				_avatarEditDialogRoot = null;
				_avatarEditDialogPanel = null;
				_avatarEditDialogTween = null;
			}

			SyncPrimaryMenuFocusState();
			CallDeferred(nameof(FocusInitialMainMenuButton));
		};
	}

	private void UpdateAvatarEditorPosition()
	{
		if (_avatarEditDialogRoot != null && GodotObject.IsInstanceValid(_avatarEditDialogRoot) && _avatarEditDialogRoot.Visible)
		{
			_avatarEditDialogRoot.Position = GetSecondaryMenuDepthPosition();
			_avatarEditDialogRoot.Scale = GetSecondaryMenuWorldScale();
		}
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
