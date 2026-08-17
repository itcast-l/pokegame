using System;
using Godot;

public partial class GameAudioManager
{
	private void BindUiBehaviorRecursive(Node node)
	{
		BindUiBehaviorNode(node);
		foreach (Node child in node.GetChildren())
		{
			BindUiBehaviorRecursive(child);
		}
	}

	private void OnUiNodeAdded(Node node)
	{
		if (node is BaseButton || node is Godot.Range and not SpinBox)
		{
			QueueUiBehaviorBinding(node);
		}
	}

	private void BindUiBehaviorNode(Node node)
	{
		if (!GodotObject.IsInstanceValid(node))
		{
			return;
		}

		if (!node.IsInsideTree())
		{
			QueueUiBehaviorBinding(node);
			return;
		}

		if (node is BaseButton button)
		{
			SuppressButtonTooltip(button);
			button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			if (!button.HasMeta(UiStyleBoundMeta))
			{
				button.SetMeta(UiStyleBoundMeta, true);
				MainMenuInteractiveStyle.ApplyUnifiedButtonText(button);
			}
			if (!button.HasMeta(UiDeferredStyleBoundMeta))
			{
				button.SetMeta(UiDeferredStyleBoundMeta, true);
				CallDeferred(nameof(ApplyUnifiedButtonTextDeferred), button);
			}
			BindButtonAudio(button);
		}

		if (node is Godot.Range range && node is not SpinBox)
		{
			BindRangeAudio(range);
		}
	}

	private void QueueUiBehaviorBinding(Node node)
	{
		if (node.HasMeta(UiBindingQueuedMeta))
		{
			return;
		}

		node.SetMeta(UiBindingQueuedMeta, true);
		CallDeferred(nameof(BindUiBehaviorNodeDeferred), node);
	}

	private void BindUiBehaviorNodeDeferred(Node node)
	{
		if (!GodotObject.IsInstanceValid(node))
		{
			return;
		}

		node.RemoveMeta(UiBindingQueuedMeta);
		if (!_uiBindingActive || !node.IsInsideTree())
		{
			return;
		}

		BindUiBehaviorNode(node);
	}

	private static void SuppressButtonTooltip(BaseButton button)
	{
		button.TooltipText = string.Empty;
		button.ShortcutInTooltip = false;
		if (button.HasMeta(UiTooltipSuppressionBoundMeta))
		{
			return;
		}

		button.SetMeta(UiTooltipSuppressionBoundMeta, true);
		button.MouseEntered += () =>
		{
			if (!GodotObject.IsInstanceValid(button))
			{
				return;
			}

			button.TooltipText = string.Empty;
			button.ShortcutInTooltip = false;
		};
	}

	private void ApplyUnifiedButtonTextDeferred(BaseButton button)
	{
		if (GodotObject.IsInstanceValid(button))
		{
			MainMenuInteractiveStyle.ApplyUnifiedButtonText(button);
		}
	}

	private void BindButtonAudio(BaseButton button)
	{
		if (button.HasMeta(UiAudioBoundMeta))
		{
			return;
		}

		button.SetMeta(UiAudioBoundMeta, true);
		button.MouseEntered += () =>
		{
			if (!button.Disabled)
			{
				PlaySfx(GameSfx.UiButtonHover, -12.0f, 0.97f, 1.04f, 0.05);
			}
		};

		if (button is CheckButton checkButton)
		{
			checkButton.Toggled += _ => PlaySfx(GameSfx.UiToggleOption, -4.0f, 0.98f, 1.03f, 0.04);
			return;
		}

		if (button is OptionButton optionButton)
		{
			optionButton.Pressed += () => PlaySfx(GameSfx.UiToggleOption, -5.0f, 0.98f, 1.03f, 0.06);
			optionButton.ItemSelected += _ => PlaySfx(GameSfx.UiToggleOption, -4.0f, 0.98f, 1.03f, 0.04);
			return;
		}

		button.Pressed += () =>
		{
			if (button.HasMeta(UiSuppressPressedSfxMeta) && button.GetMeta(UiSuppressPressedSfxMeta).AsBool())
			{
				return;
			}

			PlaySfx(ResolveButtonPressedSfx(button), -3.0f, 0.98f, 1.03f, 0.03);
		};
	}

	private void BindRangeAudio(Godot.Range range)
	{
		if (range.HasMeta(UiRangeAudioBoundMeta))
		{
			return;
		}

		range.SetMeta(UiRangeAudioBoundMeta, true);
		range.ValueChanged += _ =>
		{
			if (!range.HasFocus() && !Input.IsMouseButtonPressed(MouseButton.Left))
			{
				return;
			}

			PlaySfx(GameSfx.UiSliderTick, -12.0f, 0.98f, 1.04f, 0.04);
		};
	}

	private static string ResolveButtonPressedSfx(BaseButton button)
	{
		string name = button.Name.ToString();
		if (name.Contains("Cancel", StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Back", StringComparison.OrdinalIgnoreCase))
		{
			return GameSfx.UiCancel;
		}

		if (IsUnderNodeNamed(button, "SelectionPanel"))
		{
			return name.Contains("Cancel", StringComparison.OrdinalIgnoreCase)
				? GameSfx.UiCancel
				: GameSfx.UiTargetSelect;
		}

		return GameSfx.UiButtonClick;
	}

	private static bool IsUnderNodeNamed(Node node, string ancestorName)
	{
		Node? current = node;
		while (current != null)
		{
			if (current.Name == ancestorName)
			{
				return true;
			}

			current = current.GetParent();
		}

		return false;
	}
}
