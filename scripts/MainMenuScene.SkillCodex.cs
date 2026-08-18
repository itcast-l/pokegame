using Godot;

public partial class MainMenuScene
{
	private const string SkillCodexButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SkillCodexFrame/Button";
	private const string SkillCodexOverlayScenePath = "res://scenes/main_menu_skill_codex_overlay.tscn";

	private Button _skillCodexButton = default!;
	private MainMenuSkillCodexOverlay? _skillCodexOverlay;
	private Tween? _skillCodexTransitionTween;
	private bool _isSkillCodexTransitioning;

	private void BindSkillCodexButton()
	{
		_skillCodexButton = GetNode<Button>(SkillCodexButtonPath);
		_skillCodexButton.Pressed += () => DispatchPrimaryMenuAction(PrimaryMenuAction.SkillCodex);
		SuppressDefaultPressedSfx(_skillCodexButton);
	}

	private void BuildSkillCodexOverlay()
	{
		PackedScene? overlayScene = GD.Load<PackedScene>(SkillCodexOverlayScenePath);
		if (overlayScene?.Instantiate() is not MainMenuSkillCodexOverlay overlay)
		{
			GD.PushError($"Missing skill codex overlay scene: {SkillCodexOverlayScenePath}");
			return;
		}

		AddChild(overlay);
		_skillCodexOverlay = overlay;
		overlay.CloseRequested += HideSkillCodex;
	}

	private async void ShowSkillCodex()
	{
		if (_skillCodexOverlay == null
			|| !GodotObject.IsInstanceValid(_skillCodexOverlay)
			|| _isSkillCodexTransitioning
			|| IsMenuOverlayVisible())
		{
			return;
		}

		if (!BeginMenuCameraPushIn(true))
		{
			return;
		}

		_isSkillCodexTransitioning = true;
		_skillCodexTransitionTween?.Kill();
		_avatarEntry.Visible = false;
		_skillCodexOverlay.Open();
		Control transitionRoot = _skillCodexOverlay.TransitionRoot;
		transitionRoot.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Inherited;
		transitionRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		SyncPrimaryMenuFocusState();
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);

		_skillCodexTransitionTween = CreateTween();
		_skillCodexTransitionTween.TweenProperty(
			transitionRoot,
			"modulate:a",
			1.0f,
			DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		await CompleteMenuCameraPushIn();
		if (!GodotObject.IsInstanceValid(this)
			|| _skillCodexOverlay == null
			|| !GodotObject.IsInstanceValid(_skillCodexOverlay)
			|| !_skillCodexOverlay.IsOpen)
		{
			return;
		}

		transitionRoot.Modulate = Colors.White;
		SetMainMenuButtonsDisabled(true);
		_isSkillCodexTransitioning = false;
	}

	private async void HideSkillCodex()
	{
		if (_skillCodexOverlay == null
			|| !GodotObject.IsInstanceValid(_skillCodexOverlay)
			|| !_skillCodexOverlay.IsOpen
			|| _isSkillCodexTransitioning)
		{
			return;
		}

		_isSkillCodexTransitioning = true;
		SetMainMenuButtonsDisabled(true);
		_skillCodexOverlay.BeginClose();
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarEntry.Visible = true;
		ResetMenuCamera();

		Control transitionRoot = _skillCodexOverlay.TransitionRoot;
		transitionRoot.MouseBehaviorRecursive = Control.MouseBehaviorRecursiveEnum.Disabled;
		_skillCodexTransitionTween?.Kill();
		_skillCodexTransitionTween = CreateTween();
		_skillCodexTransitionTween.TweenProperty(
			transitionRoot,
			"modulate:a",
			0.0f,
			DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		Tween closingTween = _skillCodexTransitionTween;
		MainMenuSkillCodexOverlay closingOverlay = _skillCodexOverlay;
		uint returnToken = BeginPrimaryMenuReturn(
			() =>
			{
				closingTween.Kill();
				if (GodotObject.IsInstanceValid(closingOverlay))
				{
					closingOverlay.Close();
					transitionRoot.Modulate = Colors.White;
				}
				if (_skillCodexTransitionTween == closingTween)
				{
					_skillCodexTransitionTween = null;
				}
				_isSkillCodexTransitioning = false;
			},
			() => CallDeferred(nameof(FocusSkillCodexButton)));

		float closeSeconds = Mathf.Max(DialogFadeSeconds, GetMenuCameraHomeTotalSeconds());
		await ToSignal(
			GetTree().CreateTimer(closeSeconds),
			SceneTreeTimer.SignalName.Timeout);
		if (!GodotObject.IsInstanceValid(this)
			|| _skillCodexOverlay == null
			|| !GodotObject.IsInstanceValid(_skillCodexOverlay))
		{
			return;
		}

		CompletePrimaryMenuReturn(returnToken);
	}

	private void FocusSkillCodexButton()
	{
		GrabFocusIfAvailable(_skillCodexButton);
	}

#if DECKLAND_TEST_HOOKS
	public bool DebugOpenSkillCodex()
	{
		ShowSkillCodex();
		return _skillCodexOverlay?.IsOpen == true;
	}

	public bool DebugIsSkillCodexVisible()
	{
		return _skillCodexOverlay?.IsOpen == true;
	}

	public bool DebugHandleSkillCodexCancel()
	{
		return _skillCodexOverlay?.HandleCancel() == true;
	}

	public bool DebugIsSkillCodexTransitioning()
	{
		return _isSkillCodexTransitioning;
	}

	public bool DebugIsMainMenuAvatarVisible()
	{
		return _avatarEntry.Visible;
	}

	public bool DebugAreMainMenuButtonsDisabled()
	{
		return _avatarEntry.Button.Disabled
			&& _singlePlayerButton.Disabled
			&& _adventureButton.Disabled
			&& _multiplayerButton.Disabled
			&& _skillCodexButton.Disabled
			&& _settingsButton.Disabled
			&& _exitButton.Disabled;
	}
#endif
}
