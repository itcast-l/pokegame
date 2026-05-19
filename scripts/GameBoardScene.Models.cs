using Godot;

public partial class GameBoardScene
{
	private readonly struct PlayingCard
	{
		public PlayingCard(string suit, string rank, Texture2D texture, bool hasMagnetEffect = false, bool hasDoubleEffect = false)
		{
			Suit = suit;
			Rank = rank;
			Texture = texture;
			HasMagnetEffect = hasMagnetEffect;
			HasDoubleEffect = hasDoubleEffect;
		}

		public string Suit { get; }
		public string Rank { get; }
		public Texture2D Texture { get; }
		public bool HasMagnetEffect { get; }
		public bool HasDoubleEffect { get; }

		public PlayingCard WithMagnetEffect()
		{
			return HasMagnetEffect ? this : new PlayingCard(Suit, Rank, Texture, true, HasDoubleEffect);
		}

		public PlayingCard WithDoubleEffect()
		{
			return HasDoubleEffect ? this : new PlayingCard(Suit, Rank, Texture, HasMagnetEffect, true);
		}

		public PlayingCard WithoutItemEffects()
		{
			return HasMagnetEffect || HasDoubleEffect ? new PlayingCard(Suit, Rank, Texture) : this;
		}
	}

	private readonly record struct PlayCardResult(
		bool Played,
		bool Captured,
		bool DeckCollected = false,
		QuestionCardRewardAnimation? QuestionRewardAnimation = null,
		bool CompletesTurn = true);

	private readonly record struct CapturedTableCardVisual(Texture2D StartTexture, Texture2D? ItemOverlayTexture, Vector2 GlobalPosition, int ZIndex);

	private readonly record struct TableSpecialCardTrigger(int TableCardIndex, Texture2D Texture, Vector2 GlobalPosition, int ZIndex);

	private readonly record struct PendingItemAttachmentReward(int PlayerIndex, string ItemCardId);

	private readonly record struct QuestionCardRewardGrant(ItemCard ItemCard, int ItemHandIndex, bool RequiresMagnetAttachment = false);

	private readonly record struct QuestionCardRewardAnimation(ItemCard ItemCard, int ItemHandIndex, Vector2 StartGlobalPosition, bool RequiresMagnetAttachment = false);

	private enum PendingItemSelectionMode
	{
		None,
		TargetPlayer,
		TargetHandCard,
		MagnetHandCard,
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
		public TableCard(
			PlayingCard? card,
			bool isFaceUp,
			ItemCard? itemOverlay = null,
			int itemOwnerPlayerIndex = -1,
			int cardOwnerPlayerIndex = -1)
		{
			Card = card;
			IsFaceUp = isFaceUp;
			ItemOverlay = itemOverlay;
			ItemOwnerPlayerIndex = itemOwnerPlayerIndex;
			CardOwnerPlayerIndex = cardOwnerPlayerIndex;
		}

		public PlayingCard? Card { get; }
		public bool IsFaceUp { get; set; }
		public bool IsHiddenDuringCapture { get; set; }
		public ItemCard? ItemOverlay { get; }
		public int ItemOwnerPlayerIndex { get; }
		public int CardOwnerPlayerIndex { get; }
	}
}
