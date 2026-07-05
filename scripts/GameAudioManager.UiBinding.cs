using System;
using Godot;

public partial class GameAudioManager
{
	private void BindUiAudioRecursive(Node node)
	{
		if (node is BaseButton button)
		{
			BindButtonAudio(button);
		}

		if (node is Godot.Range range && node is not SpinBox)
		{
			BindRangeAudio(range);
		}

		foreach (Node child in node.GetChildren())
		{
			BindUiAudioRecursive(child);
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
		if (range.HasMeta(UiAudioBoundMeta))
		{
			return;
		}

		range.SetMeta(UiAudioBoundMeta, true);
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
