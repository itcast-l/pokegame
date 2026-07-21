using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private bool BeginMenuCameraPushIn(bool startCameraImmediately)
	{
		if (_isMenuCameraTransitioning)
		{
			return false;
		}

		_isMenuCameraTransitioning = true;
		SetMainMenuButtonsDisabled(true);
		if (startCameraImmediately)
		{
			SetMenuCameraPushInActive();
		}

		return true;
	}

	private bool BeginMenuCameraAdventureDive()
	{
		if (_isMenuCameraTransitioning)
		{
			return false;
		}

		_isMenuCameraTransitioning = true;
		SetMainMenuButtonsDisabled(true);
		if (!SetMenuCameraAdventureDiveActive())
		{
			SetMenuCameraPushInActive();
		}

		return true;
	}

	private void CancelMenuCameraPushIn()
	{
		ResetMenuCamera();
		SetMainMenuButtonsDisabled(false);
		_isMenuCameraTransitioning = false;
	}

	private async Task<bool> CompleteMenuCameraPushIn()
	{
		return await CompleteMenuCameraTransition();
	}

	private void ScheduleMenuCameraAdventureDiveCompletion()
	{
		SceneTreeTimer timer = GetTree().CreateTimer(GetMenuCameraTransitionRemainingSeconds());
		timer.Timeout += () =>
		{
			if (!GodotObject.IsInstanceValid(this))
			{
				return;
			}

			SetMainMenuButtonsDisabled(false);
			_isMenuCameraTransitioning = false;
		};
	}

	private async Task<bool> CompleteMenuCameraTransition()
	{
		try
		{
			await WaitForMenuCameraTransition();
			return GodotObject.IsInstanceValid(this);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(this))
			{
				SetMainMenuButtonsDisabled(false);
				_isMenuCameraTransitioning = false;
			}
		}
	}

	private async Task WaitForMenuCameraTransition()
	{
		SceneTree tree = GetTree();
		await ToSignal(
			tree.CreateTimer(GetMenuCameraTransitionRemainingSeconds()),
			SceneTreeTimer.SignalName.Timeout);
	}

	private float GetMenuCameraPushInTotalSeconds()
	{
		return GetMenuCameraPushInDuration() + MenuCameraTransitionPaddingSeconds;
	}

	private float GetMenuCameraAdventureDiveTotalSeconds()
	{
		return GetMenuCameraAdventureDiveDuration() + MenuCameraTransitionPaddingSeconds;
	}

	private float GetMenuCameraHomeTotalSeconds()
	{
		return GetMenuCameraHomeDuration() + MenuCameraTransitionPaddingSeconds;
	}

	private float GetMenuCameraHomeDuration()
	{
		if (_menuCameraHome == null)
		{
			return 0.0f;
		}

		return Mathf.Max(0.0f, _menuCameraHome.Call("get_tween_duration").AsSingle());
	}

	private float GetMenuCameraPushInDuration()
	{
		if (_menuCameraPushIn == null)
		{
			return 0.0f;
		}

		return Mathf.Max(0.0f, _menuCameraPushIn.Call("get_tween_duration").AsSingle());
	}

	private float GetMenuCameraAdventureDiveDuration()
	{
		if (_menuCameraAdventureDive == null)
		{
			return GetMenuCameraPushInDuration();
		}

		return Mathf.Max(0.0f, _menuCameraAdventureDive.Call("get_tween_duration").AsSingle());
	}

	private float GetMenuCameraTransitionRemainingSeconds()
	{
		float elapsedSeconds = 0.0f;
		if (_menuCameraTransitionStartMsec > 0)
		{
			elapsedSeconds = (Time.GetTicksMsec() - _menuCameraTransitionStartMsec) / 1000.0f;
		}

		return Mathf.Max(0.0f, _menuCameraTransitionTotalSeconds - elapsedSeconds);
	}

	private void StartMenuCameraTransition(float totalSeconds)
	{
		_menuCameraTransitionStartMsec = Time.GetTicksMsec();
		_menuCameraTransitionTotalSeconds = Mathf.Max(0.0f, totalSeconds);
	}

	private void ClearMenuCameraTransition()
	{
		_menuCameraTransitionStartMsec = 0;
		_menuCameraTransitionTotalSeconds = 0.0f;
	}

	private void SetMenuCameraPushInActive()
	{
		if (_menuCameraHome == null || _menuCameraPushIn == null)
		{
			return;
		}

		_menuCameraPushIn.Position = GetViewportRect().Size * 0.5f;
		StartMenuCameraTransition(GetMenuCameraPushInTotalSeconds());
		_menuCameraAdventureDive?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
		_menuCameraPushIn.Call("set_priority", MenuCameraPushInPriority);
	}

	private bool SetMenuCameraAdventureDiveActive()
	{
		if (_menuCameraHome == null || _menuCameraAdventureDive == null)
		{
			return false;
		}

		_menuCameraAdventureDive.Position = GetMenuCameraAdventureDivePosition();
		StartMenuCameraTransition(GetMenuCameraAdventureDiveTotalSeconds());
		_menuCameraPushIn?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
		_menuCameraAdventureDive.Call("set_priority", MenuCameraAdventureDivePriority);
		return true;
	}

	private void SetMenuCameraPushInInitial()
	{
		if (_menuCameraPushIn == null)
		{
			return;
		}

		Vector2 center = GetViewportRect().Size * 0.5f;
		_menuCameraPushIn.Position = center;
		if (_menuCamera != null)
		{
			_menuCamera.Position = center;
			_menuCamera.Zoom = GetMenuCameraPushInZoom();
		}

		if (_menuCameraHome != null)
		{
			_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
		}

		_menuCameraAdventureDive?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraPushIn.Call("set_priority", MenuCameraPushInPriority);
		ClearMenuCameraTransition();
	}

	private void ResetMenuCamera()
	{
		if (_menuCameraHome == null || _menuCameraPushIn == null)
		{
			return;
		}

		ClearMenuCameraTransition();
		_menuCameraPushIn.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraAdventureDive?.Call("set_priority", MenuCameraInactivePriority);
		_menuCameraHome.Call("set_priority", MenuCameraHomePriority);
	}

	private void SyncMenuCameraToViewport()
	{
		Vector2 center = GetViewportRect().Size * 0.5f;

		if (_menuCamera != null)
		{
			_menuCamera.Position = center;
		}

		if (_menuCameraHome != null)
		{
			_menuCameraHome.Position = center;
		}

		if (_menuCameraPushIn != null)
		{
			_menuCameraPushIn.Position = center;
		}

		if (_menuCameraAdventureDive != null)
		{
			_menuCameraAdventureDive.Position = GetMenuCameraAdventureDivePosition();
		}
	}

	private Vector2 GetSecondaryMenuDepthPosition()
	{
		return GetSecondaryMenuDepthPosition(GetViewportRect().Size);
	}

	private Vector2 GetSecondaryMenuDepthPosition(Vector2 contentSize)
	{
		return GetSecondaryMenuDepthPosition(
			contentSize,
			GetViewportRect().Size * 0.5f,
			GetMenuCameraPushInZoom());
	}

	private Vector2 GetSecondaryMenuWorldScale()
	{
		return GetSecondaryMenuWorldScale(GetViewportRect().Size);
	}

	private Vector2 GetSecondaryMenuWorldScale(Vector2 contentSize)
	{
		return GetSecondaryMenuWorldScale(contentSize, GetMenuCameraPushInZoom());
	}

	private Vector2 GetAdventureSecondaryMenuDepthPosition()
	{
		return GetSecondaryMenuDepthPosition(
			GetViewportRect().Size,
			GetMenuCameraAdventureDivePosition(),
			GetMenuCameraAdventureDiveZoom());
	}

	private Vector2 GetAdventureSecondaryMenuWorldScale()
	{
		return GetSecondaryMenuWorldScale(GetViewportRect().Size, GetMenuCameraAdventureDiveZoom());
	}

	private Vector2 GetSecondaryMenuDepthPosition(Vector2 contentSize, Vector2 cameraPosition, Vector2 zoom)
	{
		Vector2 scale = GetSecondaryMenuWorldScale(contentSize, zoom);
		return cameraPosition - contentSize * scale * 0.5f;
	}

	private Vector2 GetSecondaryMenuWorldScale(Vector2 contentSize, Vector2 zoom)
	{
		Vector2 viewportSize = GetViewportRect().Size;
		return new Vector2(
			viewportSize.X / Mathf.Max(contentSize.X, 1.0f) / zoom.X,
			viewportSize.Y / Mathf.Max(contentSize.Y, 1.0f) / zoom.Y);
	}

	private Vector2 GetMenuCameraPushInZoom()
	{
		if (_menuCameraPushIn == null)
		{
			return Vector2.One;
		}

		Vector2 zoom = _menuCameraPushIn.Call("get_zoom").AsVector2();
		return new Vector2(
			Mathf.Max(zoom.X, 0.001f),
			Mathf.Max(zoom.Y, 0.001f));
	}

	private Vector2 GetMenuCameraAdventureDiveZoom()
	{
		if (_menuCameraAdventureDive == null)
		{
			return GetMenuCameraPushInZoom();
		}

		Vector2 zoom = _menuCameraAdventureDive.Call("get_zoom").AsVector2();
		return new Vector2(
			Mathf.Max(zoom.X, 0.001f),
			Mathf.Max(zoom.Y, 0.001f));
	}

	private Vector2 GetMenuCameraAdventureDivePosition()
	{
		return GetViewportRect().Size * 0.5f;
	}
}
