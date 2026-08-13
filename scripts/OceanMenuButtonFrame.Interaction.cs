using System;
using System.Collections.Generic;
using Godot;

public partial class OceanMenuButtonFrame : Control
{
	private Button? _button;
	private readonly Dictionary<Control, InteractionBinding> _interactionBindings = new();
	private readonly HashSet<Control> _hoveredControls = new();
	private readonly HashSet<Control> _focusedControls = new();
	private readonly HashSet<Control> _pressedControls = new();
	private bool _interactionBindingsReady;
	private bool _rebuildBindingsDeferred;
	private SceneTree? _subscribedTree;

	private Button? FindDirectButtonChild()
	{
		for (int index = 0; index < GetChildCount(); index++)
		{
			if (GetChild(index) is Button button)
			{
				return button;
			}
		}

		return null;
	}

	private void RebuildInteractionBindings()
	{
		_rebuildBindingsDeferred = false;
		ClearInteractionBindings();
		_button = GetNodeOrNull<Button>("Button") ?? FindDirectButtonChild();
		BindInteractiveDescendants(this);
		_interactionBindingsReady = true;
		RefreshTargetState();
	}

	private void BindInteractiveDescendants(Node node)
	{
		for (int index = 0; index < node.GetChildCount(); index++)
		{
			Node child = node.GetChild(index);
			if (child is Control control && IsInteractiveControl(control))
			{
				BindInteractiveControl(control);
			}
			BindInteractiveDescendants(child);
		}
	}

	private static bool IsInteractiveControl(Control control)
	{
		return control is BaseButton or Godot.Range or LineEdit;
	}

	private void BindInteractiveControl(Control control)
	{
		if (_interactionBindings.ContainsKey(control))
		{
			return;
		}

		var binding = new InteractionBinding(control);
		binding.MouseEntered = () => SetControlState(_hoveredControls, control, true);
		binding.MouseExited = () => SetControlState(_hoveredControls, control, false);
		binding.FocusEntered = () => SetControlState(_focusedControls, control, true);
		binding.FocusExited = () => SetControlState(_focusedControls, control, false);
		binding.VisibilityChanged = RefreshTargetState;
		binding.TreeExiting = () => OnInteractiveControlExiting(control);
		control.MouseEntered += binding.MouseEntered;
		control.MouseExited += binding.MouseExited;
		control.FocusEntered += binding.FocusEntered;
		control.FocusExited += binding.FocusExited;
		control.VisibilityChanged += binding.VisibilityChanged;
		control.TreeExiting += binding.TreeExiting;

		if (control is BaseButton button)
		{
			binding.ButtonDown = () => SetControlState(_pressedControls, control, true);
			binding.ButtonUp = () => SetControlState(_pressedControls, control, button.ToggleMode && button.ButtonPressed);
			binding.Toggled = toggled => SetControlState(_pressedControls, control, toggled);
			button.ButtonDown += binding.ButtonDown;
			button.ButtonUp += binding.ButtonUp;
			button.Toggled += binding.Toggled;
			if (button.ButtonPressed)
			{
				_pressedControls.Add(control);
			}
		}
		if (control.HasFocus())
		{
			_focusedControls.Add(control);
		}
		_interactionBindings.Add(control, binding);
	}

	private void UnbindInteractiveControl(Control control, InteractionBinding binding)
	{
		if (GodotObject.IsInstanceValid(control))
		{
			control.MouseEntered -= binding.MouseEntered;
			control.MouseExited -= binding.MouseExited;
			control.FocusEntered -= binding.FocusEntered;
			control.FocusExited -= binding.FocusExited;
			control.VisibilityChanged -= binding.VisibilityChanged;
			control.TreeExiting -= binding.TreeExiting;
			if (control is BaseButton button)
			{
				if (binding.ButtonDown != null)
				{
					button.ButtonDown -= binding.ButtonDown;
				}
				if (binding.ButtonUp != null)
				{
					button.ButtonUp -= binding.ButtonUp;
				}
				if (binding.Toggled != null)
				{
					button.Toggled -= binding.Toggled;
				}
			}
		}
		_hoveredControls.Remove(control);
		_focusedControls.Remove(control);
		_pressedControls.Remove(control);
	}

	private void ClearInteractionBindings()
	{
		foreach (KeyValuePair<Control, InteractionBinding> pair in _interactionBindings)
		{
			UnbindInteractiveControl(pair.Key, pair.Value);
		}
		_interactionBindings.Clear();
		_hoveredControls.Clear();
		_focusedControls.Clear();
		_pressedControls.Clear();
	}

	private void OnInteractiveControlExiting(Control control)
	{
		if (_interactionBindings.Remove(control, out InteractionBinding? binding))
		{
			UnbindInteractiveControl(control, binding);
		}
		RefreshTargetState();
	}

	private void SetControlState(HashSet<Control> stateSet, Control control, bool enabled)
	{
		if (enabled)
		{
			stateSet.Add(control);
		}
		else
		{
			stateSet.Remove(control);
		}
		RefreshTargetState();
	}

	private void OnFrameVisibilityChanged()
	{
		RefreshTargetState();
	}

	private void OnTreeNodeAdded(Node node)
	{
		if (node != this && IsAncestorOf(node))
		{
			QueueInteractionBindingsRebuild();
		}
	}

	private void OnTreeNodeRemoved(Node node)
	{
		if (node is Control control && _interactionBindings.ContainsKey(control))
		{
			OnInteractiveControlExiting(control);
			QueueInteractionBindingsRebuild();
		}
	}

	private void QueueInteractionBindingsRebuild()
	{
		if (_rebuildBindingsDeferred || !IsInsideTree())
		{
			return;
		}
		_rebuildBindingsDeferred = true;
		CallDeferred(nameof(RebuildInteractionBindings));
	}

	private void RefreshTargetState()
	{
		if (!_interactionBindingsReady)
		{
			return;
		}

		float target = Selected ? 0.56f : 0.0f;
		if (HasVisibleControl(_hoveredControls)
			|| (HighlightFocus && HasVisibleControl(_focusedControls)))
		{
			target = 1.0f;
		}
		if (HasVisibleControl(_pressedControls))
		{
			target = 0.62f;
		}
		if (!ShowIdleTexture && target <= GlowEpsilon)
		{
			target = 0.0f;
		}

		_targetGlow = target;
		QueueRedraw();
		if (!IsVisibleInTree())
		{
			SetProcess(false);
			return;
		}
		if (Mathf.Abs(_glow - _targetGlow) <= GlowEpsilon)
		{
			_glow = _targetGlow;
			SetProcess(false);
			return;
		}
		SetProcess(true);
	}

	private static bool HasVisibleControl(HashSet<Control> controls)
	{
		foreach (Control control in controls)
		{
			if (GodotObject.IsInstanceValid(control) && control.IsVisibleInTree())
			{
				return true;
			}
		}
		return false;
	}

	private sealed class InteractionBinding
	{
		public InteractionBinding(Control control)
		{
			Control = control;
		}

		public Control Control { get; }
		public Action MouseEntered { get; set; } = null!;
		public Action MouseExited { get; set; } = null!;
		public Action FocusEntered { get; set; } = null!;
		public Action FocusExited { get; set; } = null!;
		public Action VisibilityChanged { get; set; } = null!;
		public Action TreeExiting { get; set; } = null!;
		public Action? ButtonDown { get; set; }
		public Action? ButtonUp { get; set; }
		public BaseButton.ToggledEventHandler? Toggled { get; set; }
	}
}
