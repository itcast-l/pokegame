using Godot;

public partial class MainMenuScene : Control
{
	private async void StartAdventureMode()
	{
		if (!BeginMenuCameraAdventureDive())
		{
			return;
		}

		// The embedded adventure menu reloads this profile synchronously when revealed.
		await _pendingPlayerProfileSaveTask;
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
		_avatarEntry.Visible = false;
		CallDeferred(nameof(ShowPreparedAdventureMenu));
	}

	private void ShowPreparedAdventureMenu()
	{
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		Control? adventureAvatarMenu = TakePreparedAdventureAvatarMenu();
		if (adventureAvatarMenu == null)
		{
			_avatarEntry.Visible = true;
			CancelMenuCameraPushIn();
			return;
		}

		ShowAdventureTransition(adventureAvatarMenu);
		RevealAdventureTransition(adventureAvatarMenu);

		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(adventureAvatarMenu))
		{
			return;
		}

		ScheduleMenuCameraAdventureDiveCompletion();
	}

	private void PrepareAdventureAvatarMenuCache()
	{
		if (_preparedAdventureAvatarMenu != null
			&& GodotObject.IsInstanceValid(_preparedAdventureAvatarMenu)
			&& _adventureTransitionRoot != null
			&& GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			return;
		}

		PackedScene? transitionScene = GD.Load<PackedScene>(AdventureTransitionScenePath);
		if (transitionScene?.Instantiate() is not MainMenuAdventureTransition transitionRoot)
		{
			GD.PushError($"Missing adventure transition scene: {AdventureTransitionScenePath}");
			return;
		}

		transitionRoot.Visible = false;
		transitionRoot.ProcessMode = ProcessModeEnum.Disabled;
		transitionRoot.MouseFilter = MouseFilterEnum.Ignore;
		transitionRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		AddChild(transitionRoot);
		_adventureTransitionRoot = transitionRoot;

		_preparedAdventureAvatarMenu = InstantiateAdventureAvatarMenu();
		if (_preparedAdventureAvatarMenu == null)
		{
			transitionRoot.QueueFree();
			_adventureTransitionRoot = null;
			return;
		}

		_preparedAdventureAvatarMenu.Visible = true;
		_preparedAdventureAvatarMenu.ProcessMode = ProcessModeEnum.Disabled;
		transitionRoot.MenuHost.AddChild(_preparedAdventureAvatarMenu);
		transitionRoot.CloseRequested += () =>
		{
			if (_preparedAdventureAvatarMenu != null
				&& GodotObject.IsInstanceValid(_preparedAdventureAvatarMenu))
			{
				HideAdventureAvatarMenu(_preparedAdventureAvatarMenu);
			}
		};

		_adventureTransitionMotion = _preparedAdventureAvatarMenu;
		UpdateAdventureTransitionPosition();
		QueueAdventureTransitionLayout(transitionRoot);
		CallDeferred(nameof(WarmAdventureMenuResources));
		CallDeferred(nameof(WarmAdventureTransitionVisuals));
	}

	private Control? TakePreparedAdventureAvatarMenu()
	{
		Control? adventureAvatarMenu = _preparedAdventureAvatarMenu;

		if (adventureAvatarMenu == null || !GodotObject.IsInstanceValid(adventureAvatarMenu))
		{
			PrepareAdventureAvatarMenuCache();
			adventureAvatarMenu = _preparedAdventureAvatarMenu;
		}

		if (adventureAvatarMenu == null)
		{
			return null;
		}

		adventureAvatarMenu.Visible = true;
		adventureAvatarMenu.ProcessMode = ProcessModeEnum.Inherit;
		return adventureAvatarMenu;
	}

	private void WarmAdventureMenuResources()
	{
		GameAudio?.WarmupSfx(GameSfx.UiPanelOpen);
		GameAudio?.WarmupSfx(GameSfx.UiPanelClose);
		GameAudio?.WarmupMusic(GameMusic.AdventureMenu);
	}

	private async void WarmAdventureTransitionVisuals()
	{
		if (_adventureTransitionRoot == null
			|| !GodotObject.IsInstanceValid(_adventureTransitionRoot)
			|| _adventureTransitionRoot.Visible
			|| _isMenuCameraTransitioning)
		{
			return;
		}

		Control? adventureAvatarMenu = _preparedAdventureAvatarMenu;
		AdventureAvatarMenuScene? adventureScene = adventureAvatarMenu as AdventureAvatarMenuScene;
		if (adventureScene != null && GodotObject.IsInstanceValid(adventureScene))
		{
			adventureScene.SetEmbeddedVisualWarmup(true);
		}

		MainMenuAdventureTransition transitionRoot = _adventureTransitionRoot;
		transitionRoot.Visible = true;
		transitionRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.001f);
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

		if (GodotObject.IsInstanceValid(transitionRoot))
		{
			transitionRoot.Visible = false;
			transitionRoot.ProcessMode = ProcessModeEnum.Disabled;
			transitionRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		}

		if (adventureScene != null && GodotObject.IsInstanceValid(adventureScene))
		{
			adventureScene.SetEmbeddedVisualWarmup(false);
		}
	}

	private Control? InstantiateAdventureAvatarMenu()
	{
		PackedScene? adventureAvatarMenuScene = GD.Load<PackedScene>(AdventureAvatarMenuScenePath);
		if (adventureAvatarMenuScene == null)
		{
			GD.PushError($"Missing adventure avatar menu scene: {AdventureAvatarMenuScenePath}");
			return null;
		}

		Node adventureAvatarMenuNode = adventureAvatarMenuScene.Instantiate();
		if (adventureAvatarMenuNode is Control adventureAvatarMenu)
		{
			return adventureAvatarMenu;
		}

		GD.PushError($"{AdventureAvatarMenuScenePath} root must be a Control.");
		adventureAvatarMenuNode.QueueFree();
		return null;
	}

	private void ShowAdventureTransition(Control adventureAvatarMenu)
	{
		_adventureTransitionTween?.Kill();
		if (_adventureTransitionRoot == null || !GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			PrepareAdventureAvatarMenuCache();
		}

		if (_adventureTransitionRoot == null || !GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			return;
		}

		MainMenuAdventureTransition transitionRoot = _adventureTransitionRoot;
		transitionRoot.ProcessMode = ProcessModeEnum.Inherit;
		transitionRoot.Visible = true;
		transitionRoot.MouseFilter = MouseFilterEnum.Ignore;
		transitionRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

		_adventureTransitionMotion = adventureAvatarMenu;
		adventureAvatarMenu.MouseFilter = MouseFilterEnum.Ignore;
		UpdateAdventureTransitionPosition();
	}

	private void RevealAdventureTransition(Control adventureAvatarMenu)
	{
		if (_adventureTransitionRoot == null || !GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			return;
		}

		Control root = _adventureTransitionRoot;
		root.Visible = true;
		root.MouseFilter = MouseFilterEnum.Stop;
		root.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		adventureAvatarMenu.MouseFilter = MouseFilterEnum.Ignore;
		if (adventureAvatarMenu is AdventureAvatarMenuScene adventureScene)
		{
			adventureScene.PrepareForEmbeddedDisplayForInputMode(_primaryMenuFocusHighlightVisible);
			adventureScene.FocusDefaultControlDeferred();
		}

		_adventureTransitionTween?.Kill();
		_adventureTransitionTween = CreateTween();
		_adventureTransitionTween.TweenProperty(root, "modulate:a", 1.0f, AdventureMenuFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		GameAudio?.PlayMusic(GameMusic.AdventureMenu, 1.2f);
	}

	public void HideAdventureAvatarMenu(Control adventureAvatarMenu)
	{
		if (_adventureTransitionRoot == null || !GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			return;
		}

		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
		Control root = _adventureTransitionRoot;
		root.MouseFilter = MouseFilterEnum.Ignore;
		adventureAvatarMenu.MouseFilter = MouseFilterEnum.Ignore;
		_avatarEntry.Visible = true;
		ResetMenuCamera();
		SetMainMenuButtonsDisabled(false);
		_isMenuCameraTransitioning = false;
		PrepareBackgroundMusic();

		_adventureTransitionTween?.Kill();
		_adventureTransitionTween = CreateTween();
		_adventureTransitionTween.TweenProperty(root, "modulate:a", 0.0f, AdventureMenuFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_adventureTransitionTween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(adventureAvatarMenu))
			{
				adventureAvatarMenu.ProcessMode = ProcessModeEnum.Disabled;
			}

			if (GodotObject.IsInstanceValid(root))
			{
				root.Visible = false;
				root.ProcessMode = ProcessModeEnum.Disabled;
				root.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
			}

			_adventureTransitionTween = null;
			CallDeferred(nameof(FocusInitialMainMenuButton));
		};
	}

	private void UpdateAdventureTransitionPosition()
	{
		if (_adventureTransitionMotion == null || !GodotObject.IsInstanceValid(_adventureTransitionMotion))
		{
			_adventureTransitionMotion = null;
			return;
		}

		if (_adventureTransitionRoot != null && GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			_adventureTransitionRoot.MenuHost.Position = GetAdventureSecondaryMenuDepthPosition();
			_adventureTransitionRoot.MenuHost.Scale = GetAdventureSecondaryMenuWorldScale();
		}

		ApplyFixedAdventureTransitionLayout();
	}

	private void ApplyFixedAdventureTransitionLayout()
	{
		if (_adventureTransitionMotion == null || !GodotObject.IsInstanceValid(_adventureTransitionMotion))
		{
			return;
		}

		Vector2 viewportSize = GetViewportRect().Size;
		_adventureTransitionMotion.AnchorLeft = 0.0f;
		_adventureTransitionMotion.AnchorTop = 0.0f;
		_adventureTransitionMotion.AnchorRight = 0.0f;
		_adventureTransitionMotion.AnchorBottom = 0.0f;
		_adventureTransitionMotion.OffsetLeft = 0.0f;
		_adventureTransitionMotion.OffsetTop = 0.0f;
		_adventureTransitionMotion.OffsetRight = viewportSize.X;
		_adventureTransitionMotion.OffsetBottom = viewportSize.Y;
		_adventureTransitionMotion.Position = Vector2.Zero;
		_adventureTransitionMotion.Scale = Vector2.One;
	}

	private static void QueueAdventureTransitionLayout(Node node)
	{
		if (node is Container container)
		{
			container.QueueSort();
		}

		for (int childIndex = 0; childIndex < node.GetChildCount(); childIndex++)
		{
			Node child = node.GetChild(childIndex);
			QueueAdventureTransitionLayout(child);
		}
	}
}
