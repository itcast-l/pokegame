using Godot;

public partial class CardGallery : Control
{
	private const string CardRoot = "res://art/cards/standard_deck";
	private const string CardBackPath = "res://art/cards/ocean_animated_deck/back/processed/frames/frame-001.png";
	private const string CardGridPath = "Margin/Layout/Scroll/CardGrid";
	private static readonly Vector2 CardSize = new(64, 84);
	private static readonly string[] Suits = { "spades", "hearts", "diamonds", "clubs" };
	private static readonly string[] Ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };

	private GridContainer _grid = default!;

	public override void _Ready()
	{
		_grid = GetNode<GridContainer>(CardGridPath);
		PopulateCards();
	}

	private void PopulateCards()
	{
		foreach (Node child in _grid.GetChildren())
		{
			child.QueueFree();
		}

		foreach (string suit in Suits)
		{
			foreach (string rank in Ranks)
			{
				string path = $"{CardRoot}/{suit}/{rank}.png";
				AddCard(path, $"{suit} {rank}");
			}
		}

		AddCard(CardBackPath, "card back");
	}

	private void AddCard(string path, string tooltip)
	{
		Texture2D? texture = GD.Load<Texture2D>(path);
		if (texture == null)
		{
			GD.PushWarning($"Missing card texture: {path}");
			return;
		}

		var card = new TextureRect
		{
			Texture = texture,
			CustomMinimumSize = CardSize,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			TooltipText = tooltip,
		};
		_grid.AddChild(card);
	}
}
