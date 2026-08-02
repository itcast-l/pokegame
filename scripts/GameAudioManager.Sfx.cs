using System;
using Godot;

public partial class GameAudioManager
{
	public void PlaySfx(
		string sfxId,
		float volumeDb = 0.0f,
		float pitchMin = 0.98f,
		float pitchMax = 1.02f,
		double cooldownSeconds = 0.0)
	{
		if (_suppressPlayback || string.IsNullOrWhiteSpace(sfxId))
		{
			return;
		}

		if (cooldownSeconds > 0.0 && IsCoolingDown(sfxId, cooldownSeconds))
		{
			return;
		}

		AudioStream? stream = GetSfxStream(sfxId);
		if (_suppressPlayback || stream == null)
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
		if (_suppressPlayback || stream == null)
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
		AudioStream? stream = audioConfig?.GetAttackStyleStream(attackEffectStyle);
		PlaySfx(RefreshStream(stream), volumeDb, pitchMin, pitchMax, cooldownSeconds);
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

	public void WarmupSfx(string sfxId)
	{
		if (!_suppressPlayback && !string.IsNullOrWhiteSpace(sfxId))
		{
			_ = GetSfxStream(sfxId);
		}
	}

	public void WarmupSfx(AudioStream? stream)
	{
		if (!_suppressPlayback && stream != null)
		{
			_ = GetStreamCooldownKey(stream);
		}
	}

	public void WarmupCharacterAudioConfig(CharacterAudioConfig? audioConfig)
	{
		if (_suppressPlayback || audioConfig == null)
		{
			return;
		}

		foreach (AudioStream stream in audioConfig.GetWarmupStreams())
		{
			WarmupSfx(stream);
		}
	}

	private void PlaySfxStream(
		AudioStream stream,
		string playerName,
		float volumeDb,
		float pitchMin,
		float pitchMax)
	{
		if (_suppressPlayback)
		{
			return;
		}

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
		ulong playerId = player.GetInstanceId();
		Action recycleHandler = () => RecycleSfxPlayerById(playerId);
		_sfxFinishedHandlers[playerId] = recycleHandler;
		player.Finished += recycleHandler;
		AddChild(player);
		return player;
	}

	private void RecycleSfxPlayerById(ulong playerId)
	{
		RecycleSfxPlayer(GetAudioPlayerById(playerId));
	}

	private void RecycleSfxPlayer(AudioStreamPlayer? player)
	{
		if (!IsLive(player))
		{
			return;
		}

		AudioStreamPlayer livePlayer = player!;
		if (livePlayer.IsQueuedForDeletion())
		{
			return;
		}

		livePlayer.Stop();
		livePlayer.Stream = null;
		livePlayer.Name = "SfxPooled";
		livePlayer.Bus = _soundBus;
		livePlayer.VolumeDb = 0.0f;
		livePlayer.PitchScale = 1.0f;

		if (livePlayer.GetParent() != this || _sfxPlayerPool.Count >= MaxPooledSfxPlayers)
		{
			DisposeAudioPlayer(livePlayer);
			return;
		}

		_sfxPlayerPool.Push(livePlayer);
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

	private static AudioStream? RefreshStream(AudioStream? stream)
	{
		if (stream == null || string.IsNullOrWhiteSpace(stream.ResourcePath))
		{
			return stream;
		}

		return ResourceLoader.Load<AudioStream>(stream.ResourcePath) ?? stream;
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

	private static AudioStream? LoadAudioStream(string path, bool loop = false)
	{
		AudioStream? stream = GD.Load<AudioStream>(path);
		if (stream != null)
		{
			ApplyLoopFlag(stream, loop);
			return stream;
		}

		if (path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
		{
			AudioStreamWav? wav = AudioStreamWav.LoadFromFile(path, new Godot.Collections.Dictionary());
			if (wav != null)
			{
				ApplyLoopFlag(wav, loop);
			}

			return wav;
		}

		if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
		{
			AudioStreamOggVorbis? ogg = AudioStreamOggVorbis.LoadFromFile(path);
			if (ogg != null)
			{
				ApplyLoopFlag(ogg, loop);
			}

			return ogg;
		}

		if (!path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
		{
			return stream;
		}

		AudioStreamMP3? mp3 = AudioStreamMP3.LoadFromFile(path);
		if (mp3 != null)
		{
			ApplyLoopFlag(mp3, loop);
			return mp3;
		}

		return null;
	}

	private static void ApplyLoopFlag(AudioStream stream, bool loop)
	{
		if (stream is AudioStreamMP3 mp3)
		{
			mp3.Loop = loop;
		}
		else if (stream is AudioStreamOggVorbis ogg)
		{
			ogg.Loop = loop;
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
}
