using System;
using System.Collections.Generic;
using Godot;

public static class GameSfx
{
	public const string OpeningIntro = "opening_intro";
	public const string CardDeal = "card_deal";
	public const string CardHover = "card_hover";
	public const string CardPickup = "card_pickup";
	public const string CardFlight = "card_flight";
	public const string CardLand = "card_land";
	public const string CardFlip = "card_flip";
	public const string CaptureBite = "capture_bite";
	public const string CaptureLift = "capture_lift";
	public const string CaptureCollect = "capture_collect";
	public const string CardDraw = "card_draw";
	public const string DeckShuffle = "deck_shuffle";
	public const string ScoreFlight = "score_flight";
	public const string ScoreArrive = "score_arrive";
	public const string CardCollectPointPopup = "card_collect_point_popup";
	public const string SpecialItemCardPlay = "special_item_card_play";
	public const string SpecialTrigger = "special_trigger";
	public const string BombPlace = "bomb_place";
	public const string BombPairLock = "bomb_pair_lock";
	public const string BombWarning = "bomb_warning";
	public const string BombExplosion = "bomb_explosion";
	public const string BombDestroyCard = "bomb_destroy_card";
	public const string LensScan = "lens_scan";
	public const string LensReveal = "lens_reveal";
	public const string QuestionRewardTransform = "question_reward_transform";
	public const string QuestionRewardReveal = "question_reward_reveal_magic_take1";
	public const string QuestionRewardInsert = "question_reward_insert";
	public const string EnchantTargetSelect = "base_card_enchant_sparkle_flash_take14";
	public const string MagnetAttach = "magnet_attach";
	public const string MagnetExtraCapture = "magnet_extra_capture";
	public const string DoubleCollect = "double_collect";
	public const string ExtraTurn = "extra_turn";
	public const string ItemDrawCard = "item_draw_card";
	public const string TableShuffle = "table_shuffle";
	public const string StealCard = "steal_card";
	public const string UiButtonClick = "ui_button_click";
	public const string UiButtonHover = "ui_button_hover";
	public const string UiPanelOpen = "ui_panel_open";
	public const string UiPanelClose = "ui_panel_close";
	public const string UiSliderTick = "ui_slider_tick";
	public const string UiToggleOption = "ui_toggle_option";
	public const string UiPauseOpen = "ui_pause_open";
	public const string UiPauseClose = "ui_pause_close";
	public const string UiTargetSelect = "ui_target_select";
	public const string UiCancel = "ui_cancel";
	public const string UiInvalid = "ui_invalid";
	public const string UiSceneTransition = "ui_scene_transition";
}

public static class GameMusic
{
	public const string MainMenu = "main_menu";
	public const string AdventureMenu = "adventure_menu";
	public const string Gameplay = "gameplay";
	public const string GameplayTension = "gameplay_tension";
	public const string UnderwaterAmbience = "underwater_ambience";
	public const string Victory = "victory";
	public const string Defeat = "defeat";
}

public partial class GameAudioManager : Node
{
	private const string PreferredSoundBus = "Sound";
	private const string PreferredMusicBus = "Music";
	private const string FallbackSoundBus = "Master";
	private const string FallbackMusicBus = "Master";
	private const string SuppressAudioEnvironmentVariable = "DECKLAND_TEST_MUTE_AUDIO";
	private const float MutedVolumeDb = -80.0f;
	private const float DefaultMusicLinearVolume = 0.76f;
	private const float AmbienceRelativeVolumeDb = -12.0f;
	private const float TensionRelativeVolumeDb = -5.0f;
	private const int MaxPooledSfxPlayers = 32;
	private static readonly StringName UiAudioBoundMeta = new("game_audio_ui_bound");
	private static readonly StringName UiTooltipSuppressionBoundMeta = new("game_ui_tooltip_suppression_bound");
	public static readonly StringName UiSuppressPressedSfxMeta = new("game_audio_ui_suppress_pressed_sfx");

	private sealed class MusicTrackSpec
	{
		public MusicTrackSpec(string path, bool loop, float relativeVolumeDb, bool usesAmbience)
		{
			Path = path;
			Loop = loop;
			RelativeVolumeDb = relativeVolumeDb;
			UsesAmbience = usesAmbience;
		}

		public string Path { get; }
		public bool Loop { get; }
		public float RelativeVolumeDb { get; }
		public bool UsesAmbience { get; }
	}

	private static readonly IReadOnlyDictionary<string, string> SfxPaths = new Dictionary<string, string>
	{
		[GameSfx.OpeningIntro] = "res://asset/audio/sfx/core/opening_intro.mp3",
		[GameSfx.CardDeal] = "res://asset/audio/sfx/core/card_deal.mp3",
		[GameSfx.CardHover] = "res://asset/audio/sfx/core/card_hover.mp3",
		[GameSfx.CardPickup] = "res://asset/audio/sfx/core/card_pickup.mp3",
		[GameSfx.CardFlight] = "res://asset/audio/sfx/core/card_flight_soft_fly.wav",
		[GameSfx.CardLand] = "res://asset/audio/sfx/core/card_land.mp3",
		[GameSfx.CardFlip] = "res://asset/audio/sfx/core/card_flip.mp3",
		[GameSfx.CaptureBite] = "res://asset/audio/sfx/core/capture_bite.mp3",
		[GameSfx.CaptureLift] = "res://asset/audio/sfx/core/capture_lift.mp3",
		[GameSfx.CaptureCollect] = "res://asset/audio/sfx/core/capture_collect.mp3",
		[GameSfx.CardDraw] = "res://asset/audio/sfx/core/card_draw.mp3",
		[GameSfx.DeckShuffle] = "res://asset/audio/sfx/core/deck_shuffle.mp3",
		[GameSfx.ScoreFlight] = "res://asset/audio/sfx/core/score_flight.mp3",
		[GameSfx.ScoreArrive] = "res://asset/audio/sfx/core/score_arrive.mp3",
		[GameSfx.CardCollectPointPopup] = "res://asset/audio/sfx/core/card_collect_point_popup.mp3",
		[GameSfx.SpecialItemCardPlay] = "res://asset/audio/sfx/special/item_card_play.mp3",
		[GameSfx.SpecialTrigger] = "res://asset/audio/sfx/special/special_trigger.mp3",
		[GameSfx.BombPlace] = "res://asset/audio/sfx/special/bomb_place.mp3",
		[GameSfx.BombPairLock] = "res://asset/audio/sfx/special/bomb_pair_lock.mp3",
		[GameSfx.BombWarning] = "res://asset/audio/sfx/special/bomb_warning.mp3",
		[GameSfx.BombExplosion] = "res://asset/audio/sfx/special/bomb_explosion.mp3",
		[GameSfx.BombDestroyCard] = "res://asset/audio/sfx/special/bomb_destroy_card.mp3",
		[GameSfx.LensScan] = "res://asset/audio/sfx/special/lens_scan.mp3",
		[GameSfx.LensReveal] = "res://asset/audio/sfx/special/lens_reveal.mp3",
		[GameSfx.QuestionRewardTransform] = "res://asset/audio/sfx/special/question_reward_transform.mp3",
		[GameSfx.QuestionRewardReveal] = "res://asset/audio/sfx/special/question_reward_reveal_magic_take1.mp3",
		[GameSfx.QuestionRewardInsert] = "res://asset/audio/sfx/special/question_reward_insert.mp3",
		[GameSfx.EnchantTargetSelect] = "res://asset/audio/sfx/special/base_card_enchant_sparkle_flash_take14.mp3",
		[GameSfx.MagnetAttach] = "res://asset/audio/sfx/special/magnet_attach.mp3",
		[GameSfx.MagnetExtraCapture] = "res://asset/audio/sfx/special/magnet_extra_capture.mp3",
		[GameSfx.DoubleCollect] = "res://asset/audio/sfx/special/double_collect.mp3",
		[GameSfx.ExtraTurn] = "res://asset/audio/sfx/special/extra_turn.mp3",
		[GameSfx.ItemDrawCard] = "res://asset/audio/sfx/special/item_draw_card.mp3",
		[GameSfx.TableShuffle] = "res://asset/audio/sfx/special/table_shuffle.mp3",
		[GameSfx.StealCard] = "res://asset/audio/sfx/special/steal_card.mp3",
		[GameSfx.UiButtonClick] = "res://asset/audio/sfx/ui/ui_button_click.wav",
		[GameSfx.UiButtonHover] = "res://asset/audio/sfx/ui/ui_button_hover.wav",
		[GameSfx.UiPanelOpen] = "res://asset/audio/sfx/ui/ui_panel_open.wav",
		[GameSfx.UiPanelClose] = "res://asset/audio/sfx/ui/ui_panel_close.wav",
		[GameSfx.UiSliderTick] = "res://asset/audio/sfx/ui/ui_slider_tick.wav",
		[GameSfx.UiToggleOption] = "res://asset/audio/sfx/ui/ui_toggle_option.wav",
		[GameSfx.UiPauseOpen] = "res://asset/audio/sfx/ui/ui_pause_open.wav",
		[GameSfx.UiPauseClose] = "res://asset/audio/sfx/ui/ui_pause_close.wav",
		[GameSfx.UiTargetSelect] = "res://asset/audio/sfx/ui/ui_target_select.wav",
		[GameSfx.UiCancel] = "res://asset/audio/sfx/ui/ui_cancel.wav",
		[GameSfx.UiInvalid] = "res://asset/audio/sfx/ui/ui_invalid.wav",
		[GameSfx.UiSceneTransition] = "res://asset/audio/sfx/ui/ui_scene_transition.wav",
	};

	private static readonly IReadOnlyDictionary<string, MusicTrackSpec> MusicPaths = new Dictionary<string, MusicTrackSpec>
	{
		[GameMusic.MainMenu] = new("res://asset/audio/bgm/main_menu_custom/Beneath_The_Sunken_Coast.mp3", true, 0.0f, true),
		[GameMusic.AdventureMenu] = new("res://asset/audio/bgm/adventure_menu_loop.mp3", true, -1.0f, true),
		[GameMusic.Gameplay] = new("res://asset/audio/bgm/gameplay_calm_loop.mp3", true, -1.5f, true),
		[GameMusic.GameplayTension] = new("res://asset/audio/bgm/gameplay_tension_loop.mp3", true, 0.0f, false),
		[GameMusic.UnderwaterAmbience] = new("res://asset/audio/bgm/underwater_ambience_loop.mp3", true, 0.0f, false),
		[GameMusic.Victory] = new("res://asset/audio/bgm/victory_stinger_v2.mp3", false, -0.5f, false),
		[GameMusic.Defeat] = new("res://asset/audio/bgm/defeat_stinger_v7.wav", false, -0.5f, false),
	};

	private readonly Dictionary<string, AudioStream> _streamCache = new();
	private readonly Dictionary<string, AudioStream> _musicStreamCache = new();
	private readonly Dictionary<string, ulong> _lastPlayedAt = new();
	private readonly HashSet<string> _missingWarningIds = new();
	private readonly HashSet<string> _missingMusicWarningIds = new();
	private readonly Dictionary<ulong, Action> _sfxFinishedHandlers = new();
	private readonly Stack<AudioStreamPlayer> _sfxPlayerPool = new();
	private readonly List<Tween> _ownedAudioTweens = new();
	private readonly RandomNumberGenerator _rng = new();
	private bool _suppressPlayback;
	private bool _uiBindingActive;
	private string _soundBus = FallbackSoundBus;
	private string _musicBus = FallbackMusicBus;
	private string _currentMusicId = string.Empty;
	private MusicTrackSpec? _currentMusicSpec;
	private float _musicVolumeDb = LinearVolumeToDb(DefaultMusicLinearVolume);
	private bool _gameplayTensionEnabled;
	private AudioStreamPlayer? _musicPlayer;
	private AudioStreamPlayer? _ambiencePlayer;
	private AudioStreamPlayer? _tensionPlayer;

	public override void _Ready()
	{
		_suppressPlayback = OS.GetEnvironment(SuppressAudioEnvironmentVariable) == "1";
		_rng.Randomize();
		_soundBus = AudioServer.GetBusIndex(PreferredSoundBus) >= 0 ? PreferredSoundBus : FallbackSoundBus;
		_musicBus = AudioServer.GetBusIndex(PreferredMusicBus) >= 0 ? PreferredMusicBus : FallbackMusicBus;
		_musicVolumeDb = LoadSavedMusicVolumeDb();
		BindUiBehaviorRecursive(GetTree().Root);
		GetTree().NodeAdded += BindUiBehaviorRecursive;
		_uiBindingActive = true;
	}
}
