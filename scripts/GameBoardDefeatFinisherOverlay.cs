using Godot;

public partial class GameBoardDefeatFinisherOverlay : Control
{
	public TextureRect FinisherTexture { get; private set; } = default!;

	public override void _Ready()
	{
		FinisherTexture = GetNode<TextureRect>("%DefeatFinisherTexture");
	}
}
