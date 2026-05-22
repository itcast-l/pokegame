using System;
using Godot;

public partial class MainMenuScene : Control
{
	private void BuildExitDialog()
	{
		_exitDialogRoot = new Control
		{
			Name = "ExitDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_exitDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_exitDialogRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.62f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HideExitDialog();
				GetViewport().SetInputAsHandled();
			}
		};
		_exitDialogRoot.AddChild(scrim);

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_exitDialogRoot.AddChild(center);

		_exitDialogPanel = new PanelContainer
		{
			Name = "ExitDialogPanel",
			CustomMinimumSize = new Vector2(500.0f, 250.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_exitDialogPanel.AddThemeStyleboxOverride("panel", CreateDialogPanelStyle());
		center.AddChild(_exitDialogPanel);

		var margin = new MarginContainer
		{
			Name = "DialogMargin",
		};
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 26);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 26);
		_exitDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "DialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 14);
		margin.AddChild(layout);

		var pearl = new Control
		{
			Name = "PearlDivider",
			CustomMinimumSize = new Vector2(0.0f, 20.0f),
		};
		pearl.Draw += () => DrawPearlDivider(pearl);
		layout.AddChild(pearl);

		var title = CreateLocalizedDialogLabel("EXIT_TITLE", 31, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateLocalizedDialogLabel("EXIT_MESSAGE", 21, new Color(0.66f, 0.96f, 0.92f, 0.9f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var hint = CreateLocalizedDialogLabel("EXIT_HINT", 16, new Color(0.95f, 0.84f, 0.58f, 0.76f), HorizontalAlignment.Center);
		layout.AddChild(hint);

		var spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 8.0f),
		};
		layout.AddChild(spacer);

		var actions = new HBoxContainer
		{
			Name = "DialogActions",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		actions.AddThemeConstantOverride("separation", 18);
		layout.AddChild(actions);

		Control cancelFrame = CreateLocalizedDialogButton("EXIT_CANCEL", new Color(0.58f, 1.0f, 0.92f), HideExitDialog, false);
		Control confirmFrame = CreateLocalizedDialogButton("EXIT_CONFIRM", new Color(0.95f, 0.68f, 0.56f), QuitGame, true);
		actions.AddChild(cancelFrame);
		actions.AddChild(confirmFrame);
	}

	private async void ShowExitDialog()
	{
		if (!BeginMenuCameraPushIn(true))
		{
			return;
		}

		_exitDialogTween?.Kill();
		_avatarEntry.Visible = false;
		_exitDialogRoot.Visible = true;
		_exitDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_exitDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		UpdateExitDialogPosition();
		_exitDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_exitDialogPanel.PivotOffset = _exitDialogPanel.Size * 0.5f;

		_exitDialogTween = CreateTween();
		_exitDialogTween.SetParallel(true);
		_exitDialogTween.TweenProperty(_exitDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_exitDialogTween.TweenProperty(_exitDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		await CompleteMenuCameraPushIn();
	}

	private void HideExitDialog()
	{
		_exitDialogTween?.Kill();
		_avatarEntry.Visible = true;
		_exitDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_exitDialogTween = CreateTween();
		_exitDialogTween.SetParallel(true);
		_exitDialogTween.TweenProperty(_exitDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_exitDialogTween.TweenProperty(_exitDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_exitDialogTween.Finished += () => _exitDialogRoot.Visible = false;
	}

	private void UpdateExitDialogPosition()
	{
		if (_exitDialogRoot != null && _exitDialogRoot.Visible)
		{
			_exitDialogRoot.Position = GetSecondaryMenuDepthPosition();
			_exitDialogRoot.Scale = GetSecondaryMenuWorldScale();
		}
	}

	private void QuitGame()
	{
		GetTree().Quit();
	}
}
