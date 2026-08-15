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
	private const int StageNameBaseFontSize = 24;
	private const int StageNameMinFontSize = 16;
	private const float StageNameWidthTolerance = 2.0f;
	private const string LockedStageNamePlaceholder = "???";
	private readonly Dictionary<string, OceanMenuButtonFrame> _adventureFrames = new();
	private readonly Dictionary<string, Button> _adventureButtons = new();
	private readonly Dictionary<string, OceanMenuButtonFrame> _avatarFrames = new();
	private readonly Dictionary<string, CharacterAvatar> _stageAvatars = new();
	private readonly Dictionary<string, int> _displayStageNumbers = new();
	private readonly Dictionary<string, int> _stageNumbers = new();
	private readonly Dictionary<string, Button> _stageButtons = new();
	private readonly Dictionary<string, Label> _stageNameLabels = new();
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
	private CharacterAvatar? _previewAvatar;
	private AvatarUnlockPrompt? _avatarUnlockPromptRoot;
	private Control? _avatarUnlockPromptPanel;
	private Button? _avatarUnlockPromptButton;
	private CharacterAvatar? _avatarUnlockPromptAvatar;
	private Tween? _levelScrollTween;
	private Tween? _avatarUnlockPromptTween;
	private GameAudioManager? _gameAudio;
	private AdventureRunSessionStore? _adventureRunSessionStore;
	private AdventureRunResumeDialog? _runResumeDialog;
	private int _levelScrollTarget;
	private string _selectedAvatarId = string.Empty;
	private string _selectedAdventureId = TutorialAdventureId;
	private bool _isReturningToMainMenu;
	private bool _isStartingAdventure;
	private bool _isShowingAvatarUnlockPrompt;
	private bool _isShowingRunResumeDialog;
	private RunResumeDialogMode _runResumeDialogMode;
	private bool _runRecoverySaveFailed;
	private bool _stageNameSizingRefreshQueued;
	private bool _isEmbeddedVisualWarmup;
	private bool _listSelectionHighlightVisible = true;

	[Export]
	public string selected_avatar_id { get; set; } = string.Empty;

	[Export]
	public PackedScene? LevelEntryScene { get; set; }

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		MouseFilter = MouseFilterEnum.Ignore;

		BindUi();
		_levelScrollTarget = 0;
		_selectedAdventureId = string.IsNullOrWhiteSpace(selected_avatar_id)
			? GetInitialAdventureId()
			: GetAdventureIdForAvatar(selected_avatar_id);
		_selectedAvatarId = string.IsNullOrWhiteSpace(selected_avatar_id)
			? GetInitialAvatarId()
			: NormalizeAdventureAvatarId(selected_avatar_id);
		AdventureRunSessionStore? runSessionStore = GetAdventureRunSessionStore();
		runSessionStore?.ConfigureForPolicy(
			_selectedAdventureId == AdventureCampaignCatalog.MainCampaignId
				? GameModePolicy.AdventureMain
				: GameModePolicy.AdventureTutorial);
		runSessionStore?.ReloadFromActiveProfile();
		RebuildLevelList();
		RefreshSelection();
		ClampScrollTarget();
		ScrollSelectedEntryIntoView();
		LocalizationTypography.Apply(this, useLocaleLayoutDirection: true);
		ApplyAdventureTypography();
		RefreshLocalizedText();
		QueueStageNameSizingRefresh();
		FocusDefaultControlDeferred();
		CallDeferred(nameof(PlayAdventureMenuMusicIfStandalone));
		CallDeferred(nameof(ShowPendingAdventureAvatarUnlockPrompt));
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			LocalizationTypography.Apply(this, useLocaleLayoutDirection: true);
			ApplyAdventureTypography();
			RefreshLocalizedText();
		}
		else if (what == NotificationResized && IsNodeReady())
		{
			ApplyResponsiveAdventureLayout();
			ClampScrollTarget();
			ScrollSelectedEntryIntoView();
			QueueStageNameSizingRefresh();
		}
		else if (what == NotificationVisibilityChanged
			&& IsNodeReady()
			&& Visible
			&& !_isEmbeddedVisualWarmup)
		{
			ShowPendingAdventureAvatarUnlockPrompt();
		}
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (!GameInputActions.IsCancelPressed(inputEvent))
		{
			return;
		}

		if (_isShowingAvatarUnlockPrompt)
		{
			HideAvatarUnlockPrompt();
		}
		else if (_isShowingRunResumeDialog)
		{
			HideRunResumeDialog();
		}
		else
		{
			ReturnToMainMenu();
		}

		GetViewport().SetInputAsHandled();
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

	private static void SuppressDefaultPressedSfx(BaseButton? button)
	{
		if (button == null || !GodotObject.IsInstanceValid(button))
		{
			return;
		}

		button.SetMeta(GameAudioManager.UiSuppressPressedSfxMeta, true);
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

internal enum RunResumeDialogMode
{
	Resume,
	Recovery,
}
