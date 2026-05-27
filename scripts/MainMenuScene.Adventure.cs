using Godot;

public partial class MainMenuScene : Control
{
	private async void StartAdventureMode()
	{
		if (!BeginMenuCameraAdventureDive())
		{
			return;
		}

		_avatarEntry.Visible = false;
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);

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

		UpdateAdventureTransitionPosition();
		bool completed = await CompleteMenuCameraAdventureDive();
		if (!completed || !GodotObject.IsInstanceValid(adventureAvatarMenu))
		{
			return;
		}
	}

	private void PrepareAdventureAvatarMenuCache()
	{
		if (_preparedAdventureAvatarMenu != null && GodotObject.IsInstanceValid(_preparedAdventureAvatarMenu))
		{
			return;
		}

		_preparedAdventureAvatarMenu = InstantiateAdventureAvatarMenu();
		if (_preparedAdventureAvatarMenu == null)
		{
			return;
		}

		_preparedAdventureAvatarMenu.Visible = false;
		_preparedAdventureAvatarMenu.ProcessMode = ProcessModeEnum.Disabled;
		AddChild(_preparedAdventureAvatarMenu);
	}

	private Control? TakePreparedAdventureAvatarMenu()
	{
		Control? adventureAvatarMenu = _preparedAdventureAvatarMenu;
		_preparedAdventureAvatarMenu = null;

		if (adventureAvatarMenu == null || !GodotObject.IsInstanceValid(adventureAvatarMenu))
		{
			adventureAvatarMenu = InstantiateAdventureAvatarMenu();
		}

		if (adventureAvatarMenu == null)
		{
			return null;
		}

		Node? parent = adventureAvatarMenu.GetParent();
		parent?.RemoveChild(adventureAvatarMenu);
		adventureAvatarMenu.Visible = true;
		adventureAvatarMenu.ProcessMode = ProcessModeEnum.Inherit;
		return adventureAvatarMenu;
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
		_adventureTransitionRoot?.QueueFree();

		_adventureTransitionRoot = new Control
		{
			Name = "AdventureTransitionRoot",
			Visible = true,
			MouseFilter = MouseFilterEnum.Ignore,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			Size = GetViewportRect().Size,
		};
		_adventureTransitionRoot.Position = GetAdventureSecondaryMenuDepthPosition();
		_adventureTransitionRoot.Scale = GetAdventureSecondaryMenuWorldScale();
		AddChild(_adventureTransitionRoot);

		var scrim = CreatePlayerCountDialogScrim();
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HideAdventureAvatarMenu(adventureAvatarMenu);
				GetViewport().SetInputAsHandled();
			}
		};
		_adventureTransitionRoot.AddChild(scrim);

		_adventureTransitionMotion = adventureAvatarMenu;
		adventureAvatarMenu.MouseFilter = MouseFilterEnum.Ignore;
		_adventureTransitionRoot.AddChild(adventureAvatarMenu);
		ApplyFixedAdventureTransitionLayout();
		QueueAdventureTransitionLayout(_adventureTransitionRoot);
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
		adventureAvatarMenu.MouseFilter = MouseFilterEnum.Stop;
		UpdateAdventureTransitionPosition();

		_adventureTransitionTween?.Kill();
		_adventureTransitionTween = CreateTween();
		_adventureTransitionTween.TweenProperty(root, "modulate:a", 1.0f, AdventureMenuFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	public void HideAdventureAvatarMenu(Control adventureAvatarMenu)
	{
		if (_adventureTransitionRoot == null || !GodotObject.IsInstanceValid(_adventureTransitionRoot))
		{
			return;
		}

		Control root = _adventureTransitionRoot;
		root.MouseFilter = MouseFilterEnum.Ignore;
		adventureAvatarMenu.MouseFilter = MouseFilterEnum.Ignore;
		_avatarEntry.Visible = true;
		ResetMenuCamera();
		SetMainMenuButtonsDisabled(false);
		_isMenuCameraTransitioning = false;

		_adventureTransitionTween?.Kill();
		_adventureTransitionTween = CreateTween();
		_adventureTransitionTween.TweenProperty(root, "modulate:a", 0.0f, AdventureMenuFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_adventureTransitionTween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(root))
			{
				root.QueueFree();
			}

			if (_adventureTransitionRoot == root)
			{
				_adventureTransitionRoot = null;
				_adventureTransitionMotion = null;
				_adventureTransitionTween = null;
			}

			PrepareAdventureAvatarMenuCache();
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
			_adventureTransitionRoot.Size = GetViewportRect().Size;
			_adventureTransitionRoot.Position = GetAdventureSecondaryMenuDepthPosition();
			_adventureTransitionRoot.Scale = GetAdventureSecondaryMenuWorldScale();
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

		foreach (Node child in node.GetChildren())
		{
			QueueAdventureTransitionLayout(child);
		}
	}
}
