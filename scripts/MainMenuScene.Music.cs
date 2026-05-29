using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuScene : Control
{
	private async void PrepareBackgroundMusic()
	{
		Node? musicManager = GetMusicManager();
		if (musicManager == null)
		{
			GD.PushWarning("Missing Resonate MusicManager autoload; background music will not play.");
			return;
		}

		if (!musicManager.Get("has_loaded").AsBool())
		{
			await ToSignal(musicManager, "loaded");
			if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(musicManager))
			{
				return;
			}
		}

		if (!musicManager.Call("is_playing", BackgroundMusicBankLabel, BackgroundMusicTrackName).AsBool())
		{
			bool started = musicManager.Call(
				"play",
				BackgroundMusicBankLabel,
				BackgroundMusicTrackName,
				BackgroundMusicFadeSeconds,
				true).AsBool();
			if (!started)
			{
				return;
			}
		}

		musicManager.Call(
			"stop_on_exit",
			this,
			BackgroundMusicBankLabel,
			BackgroundMusicTrackName,
			BackgroundMusicFadeSeconds);
	}

	private Node? GetMusicManager()
	{
		return GetNodeOrNull<Node>("/root/MusicManager");
	}
}
