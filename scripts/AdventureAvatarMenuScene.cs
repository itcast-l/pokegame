using System.Collections.Generic;
using Godot;

public partial class AdventureAvatarMenuScene : Control
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const double SmoothScrollTweenSeconds = 0.22;
	private const int SmoothScrollWheelStep = 96;
	private const float ReturnFadeSeconds = 0.18f;
	private const float ReturnScale = 0.98f;

	private readonly Dictionary<string, OceanMenuButtonFrame> _avatarFrames = new();
	private readonly Dictionary<string, int> _stageNumbers = new();
	private readonly List<(Label Label, string Key)> _localizedLabels = new();
	private readonly List<(Button Button, string Key)> _localizedButtons = new();
	private Button _startButton = default!;
	private Button _backButton = default!;
	private ScrollContainer _levelScroll = default!;
	private Label _previewStageLabel = default!;
	private Label _previewNameLabel = default!;
	private CharacterAvatar? _previewAvatar;
	private Tween? _levelScrollTween;
	private int _levelScrollTarget;
	private string _selectedAvatarId = string.Empty;
	private PackedScene? _characterAvatarScene;
	private bool _isReturningToMainMenu;

	[Export]
	public string selected_avatar_id { get; set; } = string.Empty;

	public override void _Ready()
	{
		Localization.EnsureLoaded();
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Stop;
		_characterAvatarScene = GD.Load<PackedScene>(CharacterAvatarScenePath);

		BuildUi();
		_levelScrollTarget = 0;
		_selectedAvatarId = string.IsNullOrWhiteSpace(selected_avatar_id)
			? GetInitialAvatarId()
			: NormalizeAdventureAvatarId(selected_avatar_id);
		RefreshSelection();
		ClampScrollTarget();
		RefreshLocalizedText();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			RefreshLocalizedText();
		}
		else if (what == NotificationResized && IsNodeReady())
		{
			ClampScrollTarget();
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (inputEvent.IsActionPressed("ui_cancel"))
		{
			ReturnToMainMenu();
			GetViewport().SetInputAsHandled();
		}
	}
}
