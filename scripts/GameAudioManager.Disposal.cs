using System.Collections.Generic;
using Godot;

public partial class GameAudioManager
{
	public override void _ExitTree()
	{
		if (_uiBindingActive && GetTree() != null)
		{
			GetTree().NodeAdded -= OnUiNodeAdded;
			_uiBindingActive = false;
		}

		DisposeAllAudioImmediately();
	}

	public void DisposeAllAudioImmediately()
	{
		_gameplayTensionEnabled = false;
		StopOwnedAudioTweens();
		DisposeAllAudioPlayers();
		_musicPlayer = null;
		_ambiencePlayer = null;
		_tensionPlayer = null;
		_sfxPlayerPool.Clear();
		_streamCache.Clear();
		_musicStreamCache.Clear();
		_lastPlayedAt.Clear();
		_missingWarningIds.Clear();
		_missingMusicWarningIds.Clear();
		_sfxFinishedHandlers.Clear();
		_currentMusicId = string.Empty;
		_currentMusicSpec = null;
	}

	private static bool IsLive(AudioStreamPlayer? player)
	{
		return player != null && GodotObject.IsInstanceValid(player);
	}

	private AudioStreamPlayer? GetAudioPlayerById(ulong playerId)
	{
		return GodotObject.InstanceFromId(playerId) as AudioStreamPlayer;
	}

	private Tween CreateOwnedAudioTween()
	{
		Tween tween = CreateTween();
		_ownedAudioTweens.Add(tween);
		ulong tweenId = tween.GetInstanceId();
		tween.Finished += () => ForgetOwnedAudioTween(tweenId);
		return tween;
	}

	private void ForgetOwnedAudioTween(ulong tweenId)
	{
		Tween? tween = GodotObject.InstanceFromId(tweenId) as Tween;
		if (tween != null)
		{
			_ownedAudioTweens.Remove(tween);
		}
	}

	private void StopOwnedAudioTweens()
	{
		foreach (Tween tween in new List<Tween>(_ownedAudioTweens))
		{
			if (GodotObject.IsInstanceValid(tween))
			{
				tween.Kill();
			}
		}

		_ownedAudioTweens.Clear();
	}

	private void DisposeAudioPlayerById(ulong playerId)
	{
		DisposeAudioPlayer(GetAudioPlayerById(playerId));
	}

	private void DisposeAllAudioPlayers()
	{
		foreach (Node child in new List<Node>(GetChildren()))
		{
			if (child is AudioStreamPlayer player)
			{
				DisposeAudioPlayer(player);
			}
		}
	}

	private void DisposeAudioPlayer(AudioStreamPlayer? player)
	{
		if (!IsLive(player))
		{
			return;
		}

		AudioStreamPlayer livePlayer = player!;
		ulong playerId = livePlayer.GetInstanceId();
		if (_sfxFinishedHandlers.TryGetValue(playerId, out Action? recycleHandler))
		{
			livePlayer.Finished -= recycleHandler;
			_sfxFinishedHandlers.Remove(playerId);
		}

		if (ReferenceEquals(livePlayer, _musicPlayer))
		{
			_musicPlayer = null;
		}

		if (ReferenceEquals(livePlayer, _ambiencePlayer))
		{
			_ambiencePlayer = null;
		}

		if (ReferenceEquals(livePlayer, _tensionPlayer))
		{
			_tensionPlayer = null;
		}

		livePlayer.Stop();
		livePlayer.Stream = null;
		livePlayer.GetParent()?.RemoveChild(livePlayer);
		livePlayer.Free();
	}
}
