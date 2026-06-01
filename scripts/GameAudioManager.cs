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
	private const float MutedVolumeDb = -80.0f;
	private const float DefaultMusicLinearVolume = 0.76f;
	private const float AmbienceRelativeVolumeDb = -12.0f;
	private const float TensionRelativeVolumeDb = -5.0f;
	private const int MaxPooledSfxPlayers = 32;
	private static readonly StringName UiAudioBoundMeta = new("game_audio_ui_bound");

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
		[GameSfx.CardFlight] = "res://asset/audio/sfx/core/card_flight_v4.mp3",
		[GameSfx.CardLand] = "res://asset/audio/sfx/core/card_land.mp3",
		[GameSfx.CardFlip] = "res://asset/audio/sfx/core/card_flip.mp3",
		[GameSfx.CaptureBite] = "res://asset/audio/sfx/core/capture_bite.mp3",
		[GameSfx.CaptureLift] = "res://asset/audio/sfx/core/capture_lift.mp3",
		[GameSfx.CaptureCollect] = "res://asset/audio/sfx/core/capture_collect.mp3",
		[GameSfx.CardDraw] = "res://asset/audio/sfx/core/card_draw.mp3",
		[GameSfx.DeckShuffle] = "res://asset/audio/sfx/core/deck_shuffle.mp3",
		[GameSfx.ScoreFlight] = "res://asset/audio/sfx/core/score_flight.mp3",
		[GameSfx.ScoreArrive] = "res://asset/audio/sfx/core/score_arrive.mp3",
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
		[GameMusic.MainMenu] = new("res://asset/audio/background.mp3", true, 0.0f, true),
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
	private readonly Stack<AudioStreamPlayer> _sfxPlayerPool = new();
	private readonly RandomNumberGenerator _rng = new();
	private string _soundBus = FallbackSoundBus;
	private string _musicBus = FallbackMusicBus;
	private string _currentMusicId = string.Empty;
	private float _musicVolumeDb = LinearVolumeToDb(DefaultMusicLinearVolume);
	private bool _gameplayTensionEnabled;
	private AudioStreamPlayer? _musicPlayer;
	private AudioStreamPlayer? _ambiencePlayer;
	private AudioStreamPlayer? _tensionPlayer;

	public override void _Ready()
	{
		_rng.Randomize();
		_soundBus = AudioServer.GetBusIndex(PreferredSoundBus) >= 0 ? PreferredSoundBus : FallbackSoundBus;
		_musicBus = AudioServer.GetBusIndex(PreferredMusicBus) >= 0 ? PreferredMusicBus : FallbackMusicBus;
		_musicVolumeDb = LoadSavedMusicVolumeDb();
		BindUiAudioRecursive(GetTree().Root);
		GetTree().NodeAdded += BindUiAudioRecursive;
	}

	public void PlaySfx(
		string sfxId,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		if (string.IsNullOrWhiteSpace(sfxId))
		{
			return;
		}

		if (cooldownSeconds > 0.0 && IsCoolingDown(sfxId, cooldownSeconds))
		{
			return;
		}

		AudioStream? stream = GetSfxStream(sfxId);
		if (stream == null)
		{
			return;
		}

		PlaySfxStream(stream, $"Sfx_{sfxId}", volumeDb, pitchMin, pitchMax);
	}

	public void PlaySfx(
		AudioStream? stream,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		if (stream == null)
		{
			return;
		}

		string cooldownKey = GetStreamCooldownKey(stream);
		if (cooldownSeconds > 0.0 && IsCoolingDown(cooldownKey, cooldownSeconds))
		{
			return;
		}

		PlaySfxStream(stream, "Sfx_Character", volumeDb, pitchMin, pitchMax);
	}

	public void PlayCharacterActionSfx(
		CharacterAudioConfig? audioConfig,
		CharacterAvatar.AvatarAction action,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		PlaySfx(audioConfig?.GetActionStream(action), volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	public void PlayCharacterAttackFlightSfx(
		CharacterAudioConfig? audioConfig,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		PlaySfx(audioConfig?.GetAttackFlightStream(), volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	public void PlayCharacterAttackImpactSfx(
		CharacterAudioConfig? audioConfig,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		PlaySfx(audioConfig?.GetAttackImpactStream(), volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	public void PlayCharacterAttackStyleSfx(
		CharacterAudioConfig? audioConfig,
		string attackEffectStyle,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		PlaySfx(audioConfig?.GetAttackStyleStream(attackEffectStyle), volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	public void PlayCharacterAbilitySfx(
		CharacterAudioConfig? audioConfig,
		string abilityId,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		PlaySfx(audioConfig?.GetAbilityStream(abilityId), volumeDb, pitchMin, pitchMax, cooldownSeconds);
	}

	private void PlaySfxStream(
		AudioStream stream,
		string playerName,
		float volumeDb,
		float pitchMin,
		float pitchMax)
	{
		float safePitchMin = Mathf.Max(0.01f, Mathf.Min(pitchMin, pitchMax));
		float safePitchMax = Mathf.Max(safePitchMin, Mathf.Max(pitchMin, pitchMax));
		AudioStreamPlayer player = RentSfxPlayer();
		player.Name = playerName;
		player.Stream = stream;
		player.Bus = _soundBus;
		player.VolumeDb = volumeDb;
		player.PitchScale = _rng.RandfRange(safePitchMin, safePitchMax);
		player.Play();
	}

	private AudioStreamPlayer RentSfxPlayer()
	{
		while (_sfxPlayerPool.Count > 0)
		{
			AudioStreamPlayer pooledPlayer = _sfxPlayerPool.Pop();
			if (IsLive(pooledPlayer) && !pooledPlayer.IsQueuedForDeletion())
			{
				return pooledPlayer;
			}
		}

		return CreateSfxPlayer();
	}

	private AudioStreamPlayer CreateSfxPlayer()
	{
		var player = new AudioStreamPlayer
		{
			Name = "SfxPooled",
			Bus = _soundBus,
		};
		player.Finished += () => RecycleSfxPlayer(player);
		AddChild(player);
		return player;
	}

	private void RecycleSfxPlayer(AudioStreamPlayer player)
	{
		if (!IsLive(player) || player.IsQueuedForDeletion())
		{
			return;
		}

		player.Stop();
		player.Stream = null;
		player.Name = "SfxPooled";
		player.Bus = _soundBus;
		player.VolumeDb = 0.0f;
		player.PitchScale = 1.0f;

		if (player.GetParent() != this || _sfxPlayerPool.Count >= MaxPooledSfxPlayers)
		{
			player.QueueFree();
			return;
		}

		_sfxPlayerPool.Push(player);
	}

	public void PlayMusic(string musicId, float fadeSeconds = 1.5f, bool restartIfSame = false)
	{
		if (string.IsNullOrWhiteSpace(musicId))
		{
			return;
		}

		if (!MusicPaths.TryGetValue(musicId, out MusicTrackSpec? spec))
		{
			WarnMissingMusic(musicId, "No path is registered.");
			return;
		}

		AudioStreamPlayer? currentPlayer = _musicPlayer;
		if (!restartIfSame
			&& musicId == _currentMusicId
			&& currentPlayer != null
			&& IsLive(currentPlayer)
			&& currentPlayer.Playing)
		{
			SyncMusicLayers(spec, fadeSeconds);
			return;
		}

		AudioStream? stream = GetMusicStream(musicId, spec);
		if (stream == null)
		{
			return;
		}

		AudioStreamPlayer? oldPlayer = _musicPlayer;
		var nextPlayer = new AudioStreamPlayer
		{
			Name = $"Music_{musicId}",
			Stream = stream,
			Bus = _musicBus,
			VolumeDb = MutedVolumeDb,
		};

		AddChild(nextPlayer);
		nextPlayer.Play();
		_musicPlayer = nextPlayer;
		_currentMusicId = musicId;
		FadePlayerTo(nextPlayer, GetEffectiveMusicVolumeDb(spec.RelativeVolumeDb), fadeSeconds);
		FadeOutAndFree(oldPlayer, fadeSeconds);
		SyncMusicLayers(spec, fadeSeconds);
	}

	public void StopMusic(float fadeSeconds = 1.0f)
	{
		_currentMusicId = string.Empty;
		FadeOutAndFree(_musicPlayer, fadeSeconds);
		_musicPlayer = null;
		SetAmbienceEnabled(false, fadeSeconds);
		SetTensionEnabled(false, fadeSeconds);
	}

	public void SetGameplayTensionMusic(bool enabled, float fadeSeconds = 1.0f)
	{
		_gameplayTensionEnabled = enabled;
		if (_currentMusicId != GameMusic.Gameplay)
		{
			return;
		}

		SetTensionEnabled(enabled, fadeSeconds);
	}

	public void SetMusicVolumeDb(float volumeDb)
	{
		_musicVolumeDb = Mathf.Clamp(volumeDb, MutedVolumeDb, 6.0f);
		UpdateActiveMusicVolumes();
	}

	public void WarmupSfx(string sfxId)
	{
		if (!string.IsNullOrWhiteSpace(sfxId))
		{
			_ = GetSfxStream(sfxId);
		}
	}

	public void WarmupSfx(AudioStream? stream)
	{
		if (stream != null)
		{
			_ = GetStreamCooldownKey(stream);
		}
	}

	public void WarmupCharacterAudioConfig(CharacterAudioConfig? audioConfig)
	{
		if (audioConfig == null)
		{
			return;
		}

		foreach (AudioStream stream in audioConfig.GetWarmupStreams())
		{
			WarmupSfx(stream);
		}
	}

	public void WarmupMusic(string musicId)
	{
		if (!string.IsNullOrWhiteSpace(musicId) && MusicPaths.TryGetValue(musicId, out MusicTrackSpec? spec))
		{
			_ = GetMusicStream(musicId, spec);
		}
	}

	private bool IsCoolingDown(string sfxId, double cooldownSeconds)
	{
		ulong now = Time.GetTicksMsec();
		ulong cooldownMs = (ulong)Mathf.RoundToInt((float)cooldownSeconds * 1000.0f);
		if (_lastPlayedAt.TryGetValue(sfxId, out ulong lastPlayedAt) && now - lastPlayedAt < cooldownMs)
		{
			return true;
		}

		_lastPlayedAt[sfxId] = now;
		return false;
	}

	private static string GetStreamCooldownKey(AudioStream stream)
	{
		return string.IsNullOrWhiteSpace(stream.ResourcePath)
			? "character_audio_stream"
			: stream.ResourcePath;
	}

	private AudioStream? GetSfxStream(string sfxId)
	{
		if (_streamCache.TryGetValue(sfxId, out AudioStream? cachedStream))
		{
			return cachedStream;
		}

		if (!SfxPaths.TryGetValue(sfxId, out string? path))
		{
			WarnMissingSfx(sfxId, "No path is registered.");
			return null;
		}

		AudioStream? stream = LoadAudioStream(path);
		if (stream == null)
		{
			WarnMissingSfx(sfxId, $"Could not load {path}.");
			return null;
		}

		_streamCache[sfxId] = stream;
		return stream;
	}

	private AudioStream? GetMusicStream(string musicId, MusicTrackSpec spec)
	{
		if (_musicStreamCache.TryGetValue(musicId, out AudioStream? cachedStream))
		{
			return cachedStream;
		}

		AudioStream? stream = LoadAudioStream(spec.Path, spec.Loop);
		if (stream == null)
		{
			WarnMissingMusic(musicId, $"Could not load {spec.Path}.");
			return null;
		}

		_musicStreamCache[musicId] = stream;
		return stream;
	}

	private static AudioStream? LoadAudioStream(string path, bool loop = false)
	{
		AudioStream? stream = GD.Load<AudioStream>(path);
		if (stream != null)
		{
			ApplyLoopFlag(stream, loop);
			return stream;
		}

		if (path.EndsWith(".wav", System.StringComparison.OrdinalIgnoreCase))
		{
			AudioStreamWav? wav = AudioStreamWav.LoadFromFile(path, new Godot.Collections.Dictionary());
			if (wav != null)
			{
				ApplyLoopFlag(wav, loop);
			}

			return wav;
		}

		if (!path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase))
		{
			return stream;
		}

		Godot.FileAccess? file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
		if (file == null)
		{
			return null;
		}

		byte[] data = file.GetBuffer((long)file.GetLength());
		file.Close();
		return new AudioStreamMP3
		{
			Data = data,
			Loop = loop,
		};
	}

	private static void ApplyLoopFlag(AudioStream stream, bool loop)
	{
		if (stream is AudioStreamMP3 mp3)
		{
			mp3.Loop = loop;
		}
		else if (stream is AudioStreamWav wav)
		{
			wav.LoopMode = loop ? AudioStreamWav.LoopModeEnum.Forward : AudioStreamWav.LoopModeEnum.Disabled;
		}
	}

	private void WarnMissingSfx(string sfxId, string message)
	{
		if (_missingWarningIds.Add(sfxId))
		{
			GD.PushWarning($"Missing SFX '{sfxId}': {message}");
		}
	}

	private void WarnMissingMusic(string musicId, string message)
	{
		if (_missingMusicWarningIds.Add(musicId))
		{
			GD.PushWarning($"Missing music '{musicId}': {message}");
		}
	}

	private void SyncMusicLayers(MusicTrackSpec spec, float fadeSeconds)
	{
		SetAmbienceEnabled(spec.UsesAmbience, fadeSeconds);
		SetTensionEnabled(_currentMusicId == GameMusic.Gameplay && _gameplayTensionEnabled, fadeSeconds);
	}

	private void SetAmbienceEnabled(bool enabled, float fadeSeconds)
	{
		if (enabled)
		{
			_ambiencePlayer = EnsureLayerPlayer(_ambiencePlayer, GameMusic.UnderwaterAmbience, "MusicAmbience");
			FadePlayerTo(_ambiencePlayer, GetEffectiveMusicVolumeDb(AmbienceRelativeVolumeDb), fadeSeconds);
			return;
		}

		AudioStreamPlayer? oldPlayer = _ambiencePlayer;
		_ambiencePlayer = null;
		FadeOutAndFree(oldPlayer, fadeSeconds);
	}

	private void SetTensionEnabled(bool enabled, float fadeSeconds)
	{
		if (enabled)
		{
			_tensionPlayer = EnsureLayerPlayer(_tensionPlayer, GameMusic.GameplayTension, "MusicTension");
			FadePlayerTo(_tensionPlayer, GetEffectiveMusicVolumeDb(TensionRelativeVolumeDb), fadeSeconds);
			return;
		}

		AudioStreamPlayer? oldPlayer = _tensionPlayer;
		_tensionPlayer = null;
		FadeOutAndFree(oldPlayer, fadeSeconds);
	}

	private AudioStreamPlayer? EnsureLayerPlayer(AudioStreamPlayer? player, string musicId, string nodeName)
	{
		if (player != null && IsLive(player) && player.Playing)
		{
			return player;
		}

		if (!MusicPaths.TryGetValue(musicId, out MusicTrackSpec? spec))
		{
			WarnMissingMusic(musicId, "No path is registered.");
			return null;
		}

		AudioStream? stream = GetMusicStream(musicId, spec);
		if (stream == null)
		{
			return null;
		}

		var nextPlayer = new AudioStreamPlayer
		{
			Name = nodeName,
			Stream = stream,
			Bus = _musicBus,
			VolumeDb = MutedVolumeDb,
		};
		AddChild(nextPlayer);
		nextPlayer.Play();
		return nextPlayer;
	}

	private void FadePlayerTo(AudioStreamPlayer? player, float volumeDb, float fadeSeconds)
	{
		if (player == null || !GodotObject.IsInstanceValid(player))
		{
			return;
		}

		AudioStreamPlayer livePlayer = player;
		if (fadeSeconds <= 0.0f)
		{
			livePlayer.VolumeDb = volumeDb;
			return;
		}

		Tween tween = CreateTween();
		tween.TweenProperty(livePlayer, "volume_db", volumeDb, fadeSeconds)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.Out);
	}

	private void FadeOutAndFree(AudioStreamPlayer? player, float fadeSeconds)
	{
		if (player == null || !GodotObject.IsInstanceValid(player))
		{
			return;
		}

		AudioStreamPlayer livePlayer = player;
		if (fadeSeconds <= 0.0f)
		{
			livePlayer.QueueFree();
			return;
		}

		Tween tween = CreateTween();
		tween.TweenProperty(livePlayer, "volume_db", MutedVolumeDb, fadeSeconds)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(livePlayer))
			{
				livePlayer.QueueFree();
			}
		};
	}

	private void UpdateActiveMusicVolumes()
	{
		AudioStreamPlayer? currentPlayer = _musicPlayer;
		if (_currentMusicId != string.Empty
			&& MusicPaths.TryGetValue(_currentMusicId, out MusicTrackSpec? spec)
			&& IsLive(currentPlayer)
			&& currentPlayer != null)
		{
			currentPlayer.VolumeDb = GetEffectiveMusicVolumeDb(spec.RelativeVolumeDb);
		}

		AudioStreamPlayer? ambiencePlayer = _ambiencePlayer;
		if (IsLive(ambiencePlayer) && ambiencePlayer != null)
		{
			ambiencePlayer.VolumeDb = GetEffectiveMusicVolumeDb(AmbienceRelativeVolumeDb);
		}

		AudioStreamPlayer? tensionPlayer = _tensionPlayer;
		if (IsLive(tensionPlayer) && tensionPlayer != null)
		{
			tensionPlayer.VolumeDb = GetEffectiveMusicVolumeDb(TensionRelativeVolumeDb);
		}
	}

	private float GetEffectiveMusicVolumeDb(float relativeVolumeDb)
	{
		return _musicVolumeDb <= MutedVolumeDb + 0.1f
			? MutedVolumeDb
			: Mathf.Clamp(_musicVolumeDb + relativeVolumeDb, MutedVolumeDb, 6.0f);
	}

	private static bool IsLive(AudioStreamPlayer? player)
	{
		return player != null && GodotObject.IsInstanceValid(player);
	}

	private static float LoadSavedMusicVolumeDb()
	{
		GameSaveStore.EnsureInitialized();
		float savedVolume = Mathf.Clamp(
			GameSaveStore.GetFloat(GameSaveStore.MusicVolumeKey, DefaultMusicLinearVolume),
			0.0f,
			1.0f);
		return LinearVolumeToDb(savedVolume);
	}

	private static float LinearVolumeToDb(float value)
	{
		return value <= 0.001f ? MutedVolumeDb : Mathf.LinearToDb(value);
	}

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

		button.Pressed += () => PlaySfx(ResolveButtonPressedSfx(button), -3.0f, 0.98f, 1.03f, 0.03);
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
		if (name.Contains("Cancel", System.StringComparison.OrdinalIgnoreCase)
			|| name.Contains("Back", System.StringComparison.OrdinalIgnoreCase))
		{
			return GameSfx.UiCancel;
		}

		if (IsUnderNodeNamed(button, "SelectionPanel"))
		{
			return name.Contains("Cancel", System.StringComparison.OrdinalIgnoreCase)
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
