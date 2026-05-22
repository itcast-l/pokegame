using Godot;

public partial class MainMenuScene : Control
{
	private async void StartAdventureMode()
	{
		if (!BeginMenuCameraPushIn(true))
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

		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(adventureAvatarMenu))
		{
			return;
		}

		UpdateAdventureTransitionPosition();
		bool completed = await CompleteMenuCameraPushIn();
		if (!completed || !GodotObject.IsInstanceValid(adventureAvatarMenu))
		{
			return;
		}

		PromoteAdventureTransition(adventureAvatarMenu);
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
		_adventureTransitionRoot?.QueueFree();

		_adventureTransitionRoot = new Control
		{
			Name = "AdventureTransitionRoot",
			MouseFilter = MouseFilterEnum.Stop,
			Modulate = Colors.White,
			Size = GetViewportRect().Size,
		};
		_adventureTransitionRoot.Position = GetSecondaryMenuDepthPosition();
		_adventureTransitionRoot.Scale = GetSecondaryMenuWorldScale();
		AddChild(_adventureTransitionRoot);

		_adventureTransitionMotion = adventureAvatarMenu;
		_adventureTransitionRoot.AddChild(adventureAvatarMenu);
		ApplyFixedAdventureTransitionLayout();
		QueueAdventureTransitionLayout(_adventureTransitionRoot);
		UpdateAdventureTransitionPosition();

		var inputBlocker = new Control
		{
			Name = "TransitionInputBlocker",
			MouseFilter = MouseFilterEnum.Stop,
		};
		inputBlocker.SetAnchorsPreset(LayoutPreset.FullRect);
		_adventureTransitionRoot.AddChild(inputBlocker);
	}

	private void PromoteAdventureTransition(Control adventureAvatarMenu)
	{
		Node? parent = adventureAvatarMenu.GetParent();
		parent?.RemoveChild(adventureAvatarMenu);
		adventureAvatarMenu.Position = Vector2.Zero;
		adventureAvatarMenu.Scale = Vector2.One;

		GetTree().Root.AddChild(adventureAvatarMenu);
		ApplyFinalAdventureLayout(adventureAvatarMenu);
		GetTree().CurrentScene = adventureAvatarMenu;
		_adventureTransitionRoot?.QueueFree();
		_adventureTransitionRoot = null;
		_adventureTransitionMotion = null;
		QueueFree();
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
			_adventureTransitionRoot.Position = GetSecondaryMenuDepthPosition();
			_adventureTransitionRoot.Scale = GetSecondaryMenuWorldScale();
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

	private static void ApplyFinalAdventureLayout(Control adventureAvatarMenu)
	{
		adventureAvatarMenu.SetAnchorsPreset(LayoutPreset.FullRect);
		adventureAvatarMenu.OffsetLeft = 0.0f;
		adventureAvatarMenu.OffsetTop = 0.0f;
		adventureAvatarMenu.OffsetRight = 0.0f;
		adventureAvatarMenu.OffsetBottom = 0.0f;
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
