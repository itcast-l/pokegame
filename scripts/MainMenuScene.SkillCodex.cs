using Godot;

public partial class MainMenuScene
{
	private const string SkillCodexButtonPath = "MenuSafeArea/MenuPanel/PanelMargin/MenuLayout/SkillCodexFrame/Button";
	private const string SkillCodexOverlayScenePath = "res://scenes/main_menu_skill_codex_overlay.tscn";

	private Button _skillCodexButton = default!;
	private MainMenuSkillCodexOverlay? _skillCodexOverlay;

	private void BindSkillCodexButton()
	{
		_skillCodexButton = GetNode<Button>(SkillCodexButtonPath);
		_skillCodexButton.Pressed += ShowSkillCodex;
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

	private void ShowSkillCodex()
	{
		if (_skillCodexOverlay == null
			|| !GodotObject.IsInstanceValid(_skillCodexOverlay)
			|| IsMenuOverlayVisible())
		{
			return;
		}

		SetMainMenuButtonsDisabled(true);
		_skillCodexOverlay.Open();
		SyncPrimaryMenuFocusState();
		PlayMenuSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.05);
	}

	private void HideSkillCodex()
	{
		if (_skillCodexOverlay == null || !GodotObject.IsInstanceValid(_skillCodexOverlay))
		{
			return;
		}

		_skillCodexOverlay.Close();
		SetMainMenuButtonsDisabled(false);
		SyncPrimaryMenuFocusState();
		CallDeferred(nameof(FocusSkillCodexButton));
		PlayMenuSfx(GameSfx.UiPanelClose, -2.0f, 0.98f, 1.02f, 0.05);
	}

	private void FocusSkillCodexButton()
	{
		GrabFocusIfAvailable(_skillCodexButton);
	}

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
}
