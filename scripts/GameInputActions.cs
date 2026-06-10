using System;
using System.Collections.Generic;
using Godot;

public static class GameInputActions
{
	public enum BindingSlot
	{
		Keyboard = 0,
		Gamepad = 1,
	}

	public sealed record BindingAction(
		string Id,
		string LabelKey,
		string[] InputMapActions,
		InputEvent DefaultKeyboardEvent,
		InputEvent DefaultGamepadEvent);

	public const string GamePause = "game_pause";
	public const string GameConfirm = "game_confirm";
	public const string GameCancel = "game_cancel";
	public const string HandPrevious = "hand_prev";
	public const string HandNext = "hand_next";

	private const string UiUp = "ui_up";
	private const string UiDown = "ui_down";
	private const string UiLeft = "ui_left";
	private const string UiRight = "ui_right";
	private const string UiAccept = "ui_accept";
	private const string UiCancel = "ui_cancel";
	private const float DefaultDeadzone = 0.5f;
	private const float HorizontalDeadzone = 0.45f;
	private static readonly BindingAction[] Actions =
	{
		new(
			"move_up",
			"SETTINGS_INPUT_MOVE_UP",
			new[] { UiUp },
			CreateKey(Key.Up),
			CreateJoyButton(JoyButton.DpadUp)),
		new(
			"move_down",
			"SETTINGS_INPUT_MOVE_DOWN",
			new[] { UiDown },
			CreateKey(Key.Down),
			CreateJoyButton(JoyButton.DpadDown)),
		new(
			"move_left",
			"SETTINGS_INPUT_MOVE_LEFT",
			new[] { UiLeft, HandPrevious },
			CreateKey(Key.Left),
			CreateJoyButton(JoyButton.DpadLeft)),
		new(
			"move_right",
			"SETTINGS_INPUT_MOVE_RIGHT",
			new[] { UiRight, HandNext },
			CreateKey(Key.Right),
			CreateJoyButton(JoyButton.DpadRight)),
		new(
			"confirm",
			"SETTINGS_INPUT_CONFIRM",
			new[] { UiAccept, GameConfirm },
			CreateKey(Key.Enter),
			CreateJoyButton(JoyButton.A)),
		new(
			"cancel",
			"SETTINGS_INPUT_CANCEL",
			new[] { UiCancel, GameCancel },
			CreateKey(Key.Escape),
			CreateJoyButton(JoyButton.B)),
		new(
			"pause",
			"SETTINGS_INPUT_PAUSE",
			new[] { GamePause },
			CreateKey(Key.P),
			CreateJoyButton(JoyButton.Start)),
	};

	private static readonly Dictionary<string, BindingAction> ActionsById = BuildActionsById();
	private static bool _registered;
	private static bool _bindingsLoaded;

	public static IReadOnlyList<BindingAction> All => Actions;

	public static void EnsureRegistered()
	{
		if (_registered)
		{
			return;
		}

		RegisterAction(UiUp, DefaultDeadzone);
		RegisterAction(UiDown, DefaultDeadzone);
		RegisterAction(UiLeft, HorizontalDeadzone);
		RegisterAction(UiRight, HorizontalDeadzone);
		RegisterAction(UiAccept, DefaultDeadzone);
		RegisterAction(UiCancel, DefaultDeadzone);
		RegisterAction(GamePause, DefaultDeadzone);
		RegisterAction(GameConfirm, DefaultDeadzone);
		RegisterAction(GameCancel, DefaultDeadzone);
		RegisterAction(HandPrevious, HorizontalDeadzone);
		RegisterAction(HandNext, HorizontalDeadzone);

		_registered = true;
		ApplyDefaultBindings();
		LoadBindingsFromSave();
	}

	public static bool IsCancelPressed(InputEvent inputEvent)
	{
		return inputEvent.IsActionPressed(GameCancel) || inputEvent.IsActionPressed(UiCancel);
	}

	public static InputEvent GetBinding(string actionId, BindingSlot slot)
	{
		EnsureRegistered();
		BindingAction action = GetBindingAction(actionId);
		return GetEventForSlot(action, slot).Duplicate() as InputEvent ?? CreateEmptyEventForSlot(slot);
	}

	public static string GetBindingText(string actionId, BindingSlot slot, Func<string, string>? localize = null)
	{
		return FormatEvent(GetBinding(actionId, slot), localize);
	}

	public static void Rebind(string actionId, BindingSlot slot, InputEvent inputEvent)
	{
		EnsureRegistered();
		BindingAction action = GetBindingAction(actionId);
		InputEvent sanitizedInput = SanitizeInputForSlot(slot, inputEvent);
		ClearSlotBindings(action, slot);
		ApplyBindingToInputMap(action, slot, sanitizedInput);
	}

	public static void ResetAllToDefaults()
	{
		EnsureRegistered();
		ApplyDefaultBindings();
	}

	public static Godot.Collections.Dictionary SerializeBindings()
	{
		EnsureRegistered();
		var root = new Godot.Collections.Dictionary();
		foreach (BindingAction action in Actions)
		{
			root[action.Id] = new Godot.Collections.Dictionary
			{
				["keyboard"] = SerializeEvents(GetEventsForSlot(action, BindingSlot.Keyboard)),
				["gamepad"] = SerializeEvents(GetEventsForSlot(action, BindingSlot.Gamepad)),
			};
		}

		return root;
	}

	public static void LoadBindings(Godot.Collections.Dictionary bindings)
	{
		if (!_registered)
		{
			throw new InvalidOperationException("Input actions must be registered before loading bindings.");
		}

		ApplyDefaultBindings();
		foreach (BindingAction action in Actions)
		{
			if (!TryGetDictionaryValue(bindings, action.Id, out Godot.Collections.Dictionary actionData))
			{
				continue;
			}

			if (TryParseEvents(actionData, "keyboard", BindingSlot.Keyboard, action.DefaultKeyboardEvent, out List<InputEvent> keyboardEvents))
			{
				ReplaceSlotBindings(action, BindingSlot.Keyboard, keyboardEvents);
			}

			if (TryParseEvents(actionData, "gamepad", BindingSlot.Gamepad, action.DefaultGamepadEvent, out List<InputEvent> gamepadEvents))
			{
				ReplaceSlotBindings(action, BindingSlot.Gamepad, gamepadEvents);
			}
		}

		_bindingsLoaded = true;
	}

	public static bool IsBindingCaptureEvent(InputEvent inputEvent, BindingSlot slot)
	{
		return slot switch
		{
			BindingSlot.Keyboard => inputEvent is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo,
			BindingSlot.Gamepad => inputEvent switch
			{
				InputEventJoypadButton joypadButton => joypadButton.Pressed,
				InputEventJoypadMotion joypadMotion => Mathf.Abs(joypadMotion.AxisValue) >= 0.5f,
				_ => false,
			},
			_ => false,
		};
	}

	public static string FormatEvent(InputEvent inputEvent, Func<string, string>? localize = null)
	{
		if (inputEvent is InputEventKey keyEvent)
		{
			return OS.GetKeycodeString(keyEvent.Keycode);
		}

		if (inputEvent is InputEventJoypadButton joypadButton)
		{
			return FormatJoyButton(joypadButton.ButtonIndex, localize);
		}

		if (inputEvent is InputEventJoypadMotion joypadMotion)
		{
			return FormatJoyAxis(joypadMotion.Axis, joypadMotion.AxisValue, localize);
		}

		return localize?.Invoke("SETTINGS_INPUT_UNBOUND") ?? "Unbound";
	}

	private static Dictionary<string, BindingAction> BuildActionsById()
	{
		var result = new Dictionary<string, BindingAction>(StringComparer.Ordinal);
		foreach (BindingAction action in Actions)
		{
			result[action.Id] = action;
		}

		return result;
	}

	private static void LoadBindingsFromSave()
	{
		if (_bindingsLoaded)
		{
			return;
		}

		Godot.Collections.Dictionary bindings = GameSaveStore.GetDictionary(GameSaveStore.InputBindingsKey);
		if (bindings.Count > 0)
		{
			LoadBindings(bindings);
			return;
		}

		_bindingsLoaded = true;
	}

	private static void RegisterAction(string action, float deadzone)
	{
		if (!InputMap.HasAction(action))
		{
			InputMap.AddAction(action, deadzone);
		}

		InputMap.ActionSetDeadzone(action, deadzone);
	}

	private static void ApplyDefaultBindings()
	{
		foreach (BindingAction action in Actions)
		{
			ClearSlotBindings(action, BindingSlot.Keyboard);
			ClearSlotBindings(action, BindingSlot.Gamepad);
			ApplyBindingToInputMap(action, BindingSlot.Keyboard, action.DefaultKeyboardEvent);
			ApplyBindingToInputMap(action, BindingSlot.Gamepad, action.DefaultGamepadEvent);
			ApplySupplementalDefaultBindings(action);
		}
	}

	private static void ApplySupplementalDefaultBindings(BindingAction action)
	{
		switch (action.Id)
		{
			case "move_up":
				AddBinding(action, CreateKey(Key.W));
				AddBinding(action, CreateJoyAxis(JoyAxis.LeftY, -1.0f));
				break;
			case "move_down":
				AddBinding(action, CreateKey(Key.S));
				AddBinding(action, CreateJoyAxis(JoyAxis.LeftY, 1.0f));
				break;
			case "confirm":
				AddBinding(action, CreateKey(Key.KpEnter));
				AddBinding(action, CreateKey(Key.Space));
				break;
			case "cancel":
				AddBinding(action, CreateKey(Key.Backspace));
				break;
			case "pause":
				AddBinding(action, CreateKey(Key.Escape));
				break;
			case "move_left":
				AddBinding(action, CreateKey(Key.A));
				AddBinding(action, CreateJoyAxis(JoyAxis.LeftX, -1.0f));
				break;
			case "move_right":
				AddBinding(action, CreateKey(Key.D));
				AddBinding(action, CreateJoyAxis(JoyAxis.LeftX, 1.0f));
				break;
		}
	}

	private static void AddBinding(BindingAction action, InputEvent inputEvent)
	{
		foreach (string inputMapAction in action.InputMapActions)
		{
			InputMap.ActionAddEvent(inputMapAction, inputEvent);
		}
	}

	private static BindingAction GetBindingAction(string actionId)
	{
		if (ActionsById.TryGetValue(actionId, out BindingAction? action))
		{
			return action;
		}

		throw new ArgumentOutOfRangeException(nameof(actionId), actionId, "Unknown input binding action.");
	}

	private static void ClearSlotBindings(BindingAction action, BindingSlot slot)
	{
		foreach (string inputMapAction in action.InputMapActions)
		{
			var eventsToRemove = new List<InputEvent>();
			foreach (InputEvent existingEvent in InputMap.ActionGetEvents(inputMapAction))
			{
				if (MatchesSlot(existingEvent, slot))
				{
					eventsToRemove.Add(existingEvent);
				}
			}

			foreach (InputEvent eventToRemove in eventsToRemove)
			{
				InputMap.ActionEraseEvent(inputMapAction, eventToRemove);
			}
		}
	}

	private static void ApplyBindingToInputMap(BindingAction action, BindingSlot slot, InputEvent inputEvent)
	{
		InputEvent sanitized = SanitizeInputForSlot(slot, inputEvent);
		foreach (string inputMapAction in action.InputMapActions)
		{
			InputMap.ActionAddEvent(inputMapAction, sanitized);
		}
	}

	private static void ReplaceSlotBindings(BindingAction action, BindingSlot slot, IReadOnlyList<InputEvent> inputEvents)
	{
		ClearSlotBindings(action, slot);
		foreach (InputEvent inputEvent in inputEvents)
		{
			ApplyBindingToInputMap(action, slot, inputEvent);
		}
	}

	private static InputEvent GetEventForSlot(BindingAction action, BindingSlot slot)
	{
		foreach (InputEvent inputEvent in GetEventsForSlot(action, slot))
		{
			return inputEvent;
		}

		return slot == BindingSlot.Keyboard
			? action.DefaultKeyboardEvent
			: action.DefaultGamepadEvent;
	}

	private static IReadOnlyList<InputEvent> GetEventsForSlot(BindingAction action, BindingSlot slot)
	{
		var result = new List<InputEvent>();
		if (action.InputMapActions.Length == 0)
		{
			return result;
		}

		foreach (InputEvent inputEvent in InputMap.ActionGetEvents(action.InputMapActions[0]))
		{
			if (MatchesSlot(inputEvent, slot))
			{
				result.Add(inputEvent);
			}
		}

		return result;
	}

	private static bool MatchesSlot(InputEvent inputEvent, BindingSlot slot)
	{
		return slot switch
		{
			BindingSlot.Keyboard => inputEvent is InputEventKey,
			BindingSlot.Gamepad => inputEvent is InputEventJoypadButton or InputEventJoypadMotion,
			_ => false,
		};
	}

	private static InputEvent SanitizeInputForSlot(BindingSlot slot, InputEvent inputEvent)
	{
		return slot switch
		{
			BindingSlot.Keyboard when inputEvent is InputEventKey keyEvent => new InputEventKey
			{
				Keycode = keyEvent.Keycode,
				PhysicalKeycode = keyEvent.PhysicalKeycode,
				Unicode = keyEvent.Unicode,
			},
			BindingSlot.Gamepad when inputEvent is InputEventJoypadButton joypadButton => new InputEventJoypadButton
			{
				ButtonIndex = joypadButton.ButtonIndex,
				Device = -1,
			},
			BindingSlot.Gamepad when inputEvent is InputEventJoypadMotion joypadMotion => CreateJoyAxis(
				joypadMotion.Axis,
				joypadMotion.AxisValue >= 0.0f ? 1.0f : -1.0f),
			_ => throw new ArgumentException("Input event does not match requested binding slot.", nameof(inputEvent)),
		};
	}

	private static Godot.Collections.Dictionary SerializeEvent(InputEvent inputEvent)
	{
		if (inputEvent is InputEventKey keyEvent)
		{
			return new Godot.Collections.Dictionary
			{
				["type"] = "key",
				["keycode"] = (long)keyEvent.Keycode,
				["physical_keycode"] = (long)keyEvent.PhysicalKeycode,
			};
		}

		if (inputEvent is InputEventJoypadButton joypadButton)
		{
			return new Godot.Collections.Dictionary
			{
				["type"] = "joy_button",
				["button"] = (long)joypadButton.ButtonIndex,
			};
		}

		if (inputEvent is InputEventJoypadMotion joypadMotion)
		{
			return new Godot.Collections.Dictionary
			{
				["type"] = "joy_axis",
				["axis"] = (long)joypadMotion.Axis,
				["axis_value"] = joypadMotion.AxisValue >= 0.0f ? 1.0f : -1.0f,
			};
		}

		return new Godot.Collections.Dictionary();
	}

	private static Godot.Collections.Array SerializeEvents(IReadOnlyList<InputEvent> inputEvents)
	{
		var serializedEvents = new Godot.Collections.Array();
		foreach (InputEvent inputEvent in inputEvents)
		{
			serializedEvents.Add(SerializeEvent(inputEvent));
		}

		return serializedEvents;
	}

	private static bool TryParseEvents(
		Godot.Collections.Dictionary dictionary,
		string key,
		BindingSlot slot,
		InputEvent defaultSlotEvent,
		out List<InputEvent> inputEvents)
	{
		inputEvents = new List<InputEvent>();
		if (!dictionary.ContainsKey(key))
		{
			return false;
		}

		Variant storedValue = dictionary[key];
		if (storedValue.VariantType == Variant.Type.Array)
		{
			foreach (Variant entry in storedValue.AsGodotArray())
			{
				if (entry.VariantType != Variant.Type.Dictionary)
				{
					continue;
				}

				if (TryParseSerializedEvent(entry.AsGodotDictionary(), slot, out InputEvent? inputEvent) && inputEvent != null)
				{
					inputEvents.Add(inputEvent);
				}
			}

			return inputEvents.Count > 0;
		}

		if (storedValue.VariantType == Variant.Type.Dictionary
			&& TryParseSerializedEvent(storedValue.AsGodotDictionary(), slot, out InputEvent? legacyEvent)
			&& legacyEvent != null)
		{
			if (AreEquivalentBindingEvents(legacyEvent, defaultSlotEvent))
			{
				return false;
			}

			inputEvents.Add(legacyEvent);
			return true;
		}

		return false;
	}

	private static bool AreEquivalentBindingEvents(InputEvent left, InputEvent right)
	{
		if (left is InputEventKey leftKey && right is InputEventKey rightKey)
		{
			return leftKey.Keycode == rightKey.Keycode
				&& leftKey.PhysicalKeycode == rightKey.PhysicalKeycode;
		}

		if (left is InputEventJoypadButton leftButton && right is InputEventJoypadButton rightButton)
		{
			return leftButton.ButtonIndex == rightButton.ButtonIndex;
		}

		if (left is InputEventJoypadMotion leftMotion && right is InputEventJoypadMotion rightMotion)
		{
			return leftMotion.Axis == rightMotion.Axis
				&& Mathf.Sign(leftMotion.AxisValue) == Mathf.Sign(rightMotion.AxisValue);
		}

		return false;
	}

	private static bool TryParseSerializedEvent(
		Godot.Collections.Dictionary eventData,
		BindingSlot slot,
		out InputEvent? inputEvent)
	{
		inputEvent = null;
		string type = ReadDictionaryString(eventData, "type");
		switch (type)
		{
			case "key" when slot == BindingSlot.Keyboard:
			{
				Key keycode = (Key)ReadDictionaryInt(eventData, "keycode");
				if (keycode == Key.None)
				{
					return false;
				}

				Key physicalKeycode = (Key)ReadDictionaryInt(eventData, "physical_keycode");
				inputEvent = new InputEventKey
				{
					Keycode = keycode,
					PhysicalKeycode = physicalKeycode,
				};
				return true;
			}
			case "joy_button" when slot == BindingSlot.Gamepad:
			{
				JoyButton button = (JoyButton)ReadDictionaryInt(eventData, "button");
				inputEvent = CreateJoyButton(button);
				return true;
			}
			case "joy_axis" when slot == BindingSlot.Gamepad:
			{
				JoyAxis axis = (JoyAxis)ReadDictionaryInt(eventData, "axis");
				float axisValue = ReadDictionaryFloat(eventData, "axis_value");
				if (Mathf.IsZeroApprox(axisValue))
				{
					axisValue = 1.0f;
				}

				inputEvent = CreateJoyAxis(axis, axisValue >= 0.0f ? 1.0f : -1.0f);
				return true;
			}
			default:
				return false;
		}
	}

	private static bool TryGetDictionaryValue(Godot.Collections.Dictionary dictionary, string key, out Godot.Collections.Dictionary value)
	{
		value = new Godot.Collections.Dictionary();
		if (!dictionary.ContainsKey(key) || dictionary[key].VariantType != Variant.Type.Dictionary)
		{
			return false;
		}

		value = dictionary[key].AsGodotDictionary();
		return true;
	}

	private static string ReadDictionaryString(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.ContainsKey(key) ? dictionary[key].AsString() : string.Empty;
	}

	private static int ReadDictionaryInt(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : 0;
	}

	private static float ReadDictionaryFloat(Godot.Collections.Dictionary dictionary, string key)
	{
		return dictionary.ContainsKey(key) ? dictionary[key].AsSingle() : 0.0f;
	}

	private static string FormatJoyButton(JoyButton button, Func<string, string>? localize)
	{
		return button switch
		{
			JoyButton.A => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_A") ?? "A",
			JoyButton.B => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_B") ?? "B",
			JoyButton.X => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_X") ?? "X",
			JoyButton.Y => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_Y") ?? "Y",
			JoyButton.Back => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_BACK") ?? "Back",
			JoyButton.Guide => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_GUIDE") ?? "Guide",
			JoyButton.Start => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_START") ?? "Start",
			JoyButton.LeftStick => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LEFT_STICK") ?? "L Stick",
			JoyButton.RightStick => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RIGHT_STICK") ?? "R Stick",
			JoyButton.LeftShoulder => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LB") ?? "LB",
			JoyButton.RightShoulder => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RB") ?? "RB",
			JoyButton.DpadUp => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_DPAD_UP") ?? "D-Pad Up",
			JoyButton.DpadDown => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_DPAD_DOWN") ?? "D-Pad Down",
			JoyButton.DpadLeft => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_DPAD_LEFT") ?? "D-Pad Left",
			JoyButton.DpadRight => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_DPAD_RIGHT") ?? "D-Pad Right",
			_ => $"Button {(int)button}",
		};
	}

	private static string FormatJoyAxis(JoyAxis axis, float axisValue, Func<string, string>? localize)
	{
		bool positive = axisValue >= 0.0f;
		return axis switch
		{
			JoyAxis.LeftX => positive
				? localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LEFT_STICK_RIGHT") ?? "Left Stick Right"
				: localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LEFT_STICK_LEFT") ?? "Left Stick Left",
			JoyAxis.LeftY => positive
				? localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LEFT_STICK_DOWN") ?? "Left Stick Down"
				: localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LEFT_STICK_UP") ?? "Left Stick Up",
			JoyAxis.RightX => positive
				? localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_RIGHT") ?? "Right Stick Right"
				: localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_LEFT") ?? "Right Stick Left",
			JoyAxis.RightY => positive
				? localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_DOWN") ?? "Right Stick Down"
				: localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_UP") ?? "Right Stick Up",
			JoyAxis.TriggerLeft => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_LT") ?? "LT",
			JoyAxis.TriggerRight => localize?.Invoke("SETTINGS_INPUT_GAMEPAD_RT") ?? "RT",
			_ => $"Axis {(int)axis}",
		};
	}

	private static InputEvent CreateKey(Key key)
	{
		return new InputEventKey { Keycode = key, PhysicalKeycode = key };
	}

	private static InputEvent CreateJoyButton(JoyButton button)
	{
		return new InputEventJoypadButton { ButtonIndex = button, Device = -1 };
	}

	private static InputEvent CreateJoyAxis(JoyAxis axis, float axisValue)
	{
		return new InputEventJoypadMotion
		{
			Axis = axis,
			AxisValue = axisValue >= 0.0f ? 1.0f : -1.0f,
			Device = -1,
		};
	}

	private static InputEvent CreateEmptyEventForSlot(BindingSlot slot)
	{
		return slot == BindingSlot.Keyboard ? new InputEventKey() : new InputEventJoypadButton();
	}
}
