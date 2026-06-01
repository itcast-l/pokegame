using System.Collections.Generic;
using Godot;

public partial class AdventureAvatarMenuScene : Control
{
	private const string MainMenuScenePath = "res://scenes/main_menu_scene.tscn";
	private const string GameBoardScenePath = "res://scenes/game_board_scene.tscn";
	private const string TutorialGameBoardScenePath = "res://scenes/adventure_tutorial_game_board_scene.tscn";
	private const string ItemTutorialGameBoardScenePath = "res://scenes/adventure_item_tutorial_game_board_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const double SmoothScrollTweenSeconds = 0.22;
	private const int SmoothScrollWheelStep = 96;
	private const float ReturnFadeSeconds = 0.18f;
	private const float ReturnScale = 0.98f;
	private readonly Dictionary<string, OceanMenuButtonFrame> _adventureFrames = new();
	private readonly Dictionary<string, Button> _adventureButtons = new();
	private readonly Dictionary<string, OceanMenuButtonFrame> _avatarFrames = new();
	private readonly Dictionary<string, int> _stageNumbers = new();
	private readonly Dictionary<string, Button> _stageButtons = new();
	private readonly Dictionary<string, Label> _stageStatusLabels = new();
	private readonly List<(Label Label, string Key)> _localizedLabels = new();
	private readonly List<(Button Button, string Key)> _localizedButtons = new();
	private Button _startButton = default!;
	private Button _backButton = default!;
	private ScrollContainer _levelScroll = default!;
	private VBoxContainer _levelList = default!;
	private Label _previewStageLabel = default!;
	private Label _previewNameLabel = default!;
	private Label _previewStatusLabel = default!;
	private Label _previewAbilityTitleLabel = default!;
	private Label _previewAbilityNameLabel = default!;
	private Label _previewAbilityDescriptionLabel = default!;
	private ScrollContainer _previewAbilityDescriptionScroll = default!;
	private CharacterAvatar? _previewAvatar;
	private AvatarUnlockPrompt? _avatarUnlockPromptRoot;
	private Control? _avatarUnlockPromptPanel;
	private Button? _avatarUnlockPromptButton;
	private CharacterAvatar? _avatarUnlockPromptAvatar;
	private Tween? _levelScrollTween;
	private Tween? _avatarUnlockPromptTween;
	private GameAudioManager? _gameAudio;
	private int _levelScrollTarget;
	private string _selectedAvatarId = string.Empty;
	private string _selectedAdventureId = DeepTideThroneAdventureId;
	private bool _isReturningToMainMenu;
	private bool _isStartingAdventure;
	private bool _isShowingAvatarUnlockPrompt;

	[Export]
	public string selected_avatar_id { get; set; } = string.Empty;

	public override void _Ready()
	{
		Localization.EnsureLoaded();
		MouseFilter = MouseFilterEnum.Stop;

		BindUi();
		_levelScrollTarget = 0;
		_selectedAvatarId = string.IsNullOrWhiteSpace(selected_avatar_id)
			? GetInitialAvatarId()
			: NormalizeAdventureAvatarId(selected_avatar_id);
		RefreshSelection();
		ClampScrollTarget();
		ScrollSelectedEntryIntoView();
		RefreshLocalizedText();
		FocusDefaultControlDeferred();
		CallDeferred(nameof(PlayAdventureMenuMusicIfStandalone));
		CallDeferred(nameof(ShowPendingAdventureAvatarUnlockPrompt));
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
			ScrollSelectedEntryIntoView();
		}
		else if (what == NotificationVisibilityChanged && IsNodeReady() && Visible)
		{
			ShowPendingAdventureAvatarUnlockPrompt();
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

	private void ScrollSelectedEntryIntoView()
	{
		if (_levelScroll == null || !GodotObject.IsInstanceValid(_levelScroll))
		{
			return;
		}

		if (!_avatarFrames.TryGetValue(_selectedAvatarId, out OceanMenuButtonFrame? frame) || !GodotObject.IsInstanceValid(frame))
		{
			return;
		}

		float target = Mathf.Clamp(
			frame.Position.Y + frame.Size.Y * 0.5f - _levelScroll.Size.Y * 0.5f,
			0.0f,
			GetMaxScrollTarget());
		_levelScrollTarget = (int)Mathf.Round(target);
		_levelScroll.ScrollVertical = _levelScrollTarget;
	}

	private GameAudioManager? GameAudio => _gameAudio ??= GetNodeOrNull<GameAudioManager>("/root/GameAudio");

	private void PlayMenuSfx(
		string sfxId,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		GameAudio?.PlaySfx(sfxId, volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	private void PlayAdventureMenuMusicIfStandalone()
	{
		if (!Visible || GetEmbeddedMainMenuParent() != null)
		{
			return;
		}

		GameAudio?.PlayMusic(GameMusic.AdventureMenu, 1.2f);
	}
}
