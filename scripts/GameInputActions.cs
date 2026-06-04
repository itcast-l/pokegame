using Godot;

public static class GameInputActions
{
	public const string GamePause = "game_pause";
	public const string GameConfirm = "game_confirm";
	public const string GameCancel = "game_cancel";
	public const string HandPrevious = "hand_prev";
	public const string HandNext = "hand_next";

	private static bool _registered;

	public static void EnsureRegistered()
	{
		if (_registered)
		{
			return;
		}

		RegisterAction(GamePause, 0.5f, action =>
		{
			AddKey(action, Key.Escape);
			AddKey(action, Key.P);
			AddJoyButton(action, JoyButton.Start);
		});
		RegisterAction(GameConfirm, 0.5f, action =>
		{
			AddKey(action, Key.Enter);
			AddKey(action, Key.KpEnter);
			AddKey(action, Key.Space);
			AddJoyButton(action, JoyButton.A);
		});
		RegisterAction(GameCancel, 0.5f, action =>
		{
			AddKey(action, Key.Escape);
			AddKey(action, Key.Backspace);
			AddJoyButton(action, JoyButton.B);
		});
		RegisterAction(HandPrevious, 0.45f, action =>
		{
			AddKey(action, Key.Left);
			AddKey(action, Key.A);
			AddJoyButton(action, JoyButton.DpadLeft);
			AddJoyAxis(action, JoyAxis.LeftX, -1.0f);
		});
		RegisterAction(HandNext, 0.45f, action =>
		{
			AddKey(action, Key.Right);
			AddKey(action, Key.D);
			AddJoyButton(action, JoyButton.DpadRight);
			AddJoyAxis(action, JoyAxis.LeftX, 1.0f);
		});

		_registered = true;
	}

	public static bool IsCancelPressed(InputEvent inputEvent)
	{
		return inputEvent.IsActionPressed(GameCancel) || inputEvent.IsActionPressed("ui_cancel");
	}

	private static void RegisterAction(string action, float deadzone, System.Action<string> addDefaults)
	{
		if (InputMap.HasAction(action))
		{
			return;
		}

		InputMap.AddAction(action, deadzone);
		addDefaults(action);
	}

	private static void AddKey(string action, Key key)
	{
		InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
	}

	private static void AddJoyButton(string action, JoyButton button)
	{
		InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
	}

	private static void AddJoyAxis(string action, JoyAxis axis, float axisValue)
	{
		InputMap.ActionAddEvent(action, new InputEventJoypadMotion { Axis = axis, AxisValue = axisValue });
	}
}
