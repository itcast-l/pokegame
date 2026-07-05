using Godot;

public partial class GameAudioManager
{
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
			_currentMusicSpec = spec;
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
		_currentMusicSpec = spec;
		FadePlayerTo(nextPlayer, GetEffectiveMusicVolumeDb(spec.RelativeVolumeDb), fadeSeconds);
		FadeOutAndFree(oldPlayer, fadeSeconds);
		SyncMusicLayers(spec, fadeSeconds);
	}

	public void PlayExternalMusic(
		string musicId,
		string absolutePath,
		bool loop = true,
		float relativeVolumeDb = 0.0f,
		bool usesAmbience = true,
		float fadeSeconds = 1.5f,
		bool restartIfSame = false)
	{
		if (string.IsNullOrWhiteSpace(musicId) || string.IsNullOrWhiteSpace(absolutePath))
		{
			return;
		}

		var spec = new MusicTrackSpec(absolutePath, loop, relativeVolumeDb, usesAmbience);
		AudioStreamPlayer? currentPlayer = _musicPlayer;
		if (!restartIfSame
			&& musicId == _currentMusicId
			&& currentPlayer != null
			&& IsLive(currentPlayer)
			&& currentPlayer.Playing)
		{
			_currentMusicSpec = spec;
			SyncMusicLayers(spec, fadeSeconds);
			return;
		}

		AudioStream? stream = LoadAudioStream(absolutePath, loop);
		if (stream == null)
		{
			WarnMissingMusic(musicId, $"Could not load {absolutePath}.");
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
		_currentMusicSpec = spec;
		FadePlayerTo(nextPlayer, GetEffectiveMusicVolumeDb(spec.RelativeVolumeDb), fadeSeconds);
		FadeOutAndFree(oldPlayer, fadeSeconds);
		SyncMusicLayers(spec, fadeSeconds);
	}

	public void StopMusic(float fadeSeconds = 1.0f)
	{
		_currentMusicId = string.Empty;
		_currentMusicSpec = null;
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

	public void WarmupMusic(string musicId)
	{
		if (!string.IsNullOrWhiteSpace(musicId) && MusicPaths.TryGetValue(musicId, out MusicTrackSpec? spec))
		{
			_ = GetMusicStream(musicId, spec);
		}
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

		Tween tween = CreateOwnedAudioTween();
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
			DisposeAudioPlayer(livePlayer);
			return;
		}

		ulong playerId = livePlayer.GetInstanceId();
		Tween tween = CreateOwnedAudioTween();
		tween.TweenProperty(livePlayer, "volume_db", MutedVolumeDb, fadeSeconds)
			.SetTrans(Tween.TransitionType.Quad)
			.SetEase(Tween.EaseType.In);
		tween.Finished += () => DisposeAudioPlayerById(playerId);
	}

	private void UpdateActiveMusicVolumes()
	{
		AudioStreamPlayer? currentPlayer = _musicPlayer;
		if (_currentMusicSpec != null
			&& IsLive(currentPlayer)
			&& currentPlayer != null)
		{
			currentPlayer.VolumeDb = GetEffectiveMusicVolumeDb(_currentMusicSpec.RelativeVolumeDb);
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
}
