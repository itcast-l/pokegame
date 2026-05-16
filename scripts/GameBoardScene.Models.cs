using Godot;

public partial class GameBoardScene
{
	private readonly struct PlayingCard
	{
		public PlayingCard(string suit, string rank, Texture2D texture)
		{
			Suit = suit;
			Rank = rank;
			Texture = texture;
		}

		public string Suit { get; }
		public string Rank { get; }
		public Texture2D Texture { get; }
	}

	private readonly record struct PlayCardResult(bool Played, bool Captured, bool DeckCollected = false);

	private readonly record struct CapturedTableCardVisual(Texture2D StartTexture, Texture2D? ItemOverlayTexture, Vector2 GlobalPosition, int ZIndex);

	private enum PendingItemSelectionMode
	{
		None,
		TargetPlayer,
		TargetHandCard,
	}

	private readonly struct ItemCard
	{
		public ItemCard(string id, Texture2D texture)
		{
			Id = id;
			Texture = texture;
		}

		public string Id { get; }
		public Texture2D Texture { get; }
	}

	private sealed class TableCard
	{
		public TableCard(PlayingCard? card, bool isFaceUp, ItemCard? itemOverlay = null)
		{
			Card = card;
			IsFaceUp = isFaceUp;
			ItemOverlay = itemOverlay;
		}

		public PlayingCard? Card { get; }
		public bool IsFaceUp { get; set; }
		public bool IsHiddenDuringCapture { get; set; }
		public ItemCard? ItemOverlay { get; }
	}
}
