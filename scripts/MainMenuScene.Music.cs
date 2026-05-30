using Godot;

public partial class MainMenuScene : Control
{
	private void PrepareBackgroundMusic()
	{
		GameAudio?.PlayMusic(GameMusic.MainMenu, BackgroundMusicFadeSeconds);
	}

	private Node? GetMusicManager()
	{
		return GetNodeOrNull<Node>("/root/MusicManager");
	}
}
