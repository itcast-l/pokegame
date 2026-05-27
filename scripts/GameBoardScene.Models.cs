using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private readonly struct PlayingCard
	{
		public PlayingCard(string suit, string rank, Texture2D texture, string attachedItemCardIdsCsv = "")
		{
			Suit = suit;
			Rank = rank;
			Texture = texture;
			AttachedItemCardIdsCsv = attachedItemCardIdsCsv;
		}

		public string Suit { get; }
		public string Rank { get; }
		public Texture2D Texture { get; }
		public string AttachedItemCardIdsCsv { get; }
		public bool HasItemEffect => !string.IsNullOrEmpty(AttachedItemCardIdsCsv);

		public PlayingCard WithAttachedItemEffect(string itemCardId)
		{
			if (string.IsNullOrWhiteSpace(itemCardId) || HasAttachedItemEffect(itemCardId))
			{
				return this;
			}

			string attachedItemCardIdsCsv = string.IsNullOrEmpty(AttachedItemCardIdsCsv)
				? itemCardId
				: $"{AttachedItemCardIdsCsv},{itemCardId}";
			return new PlayingCard(Suit, Rank, Texture, attachedItemCardIdsCsv);
		}

		public PlayingCard WithoutAttachedItemEffect(string itemCardId)
		{
			if (string.IsNullOrWhiteSpace(itemCardId) || !HasAttachedItemEffect(itemCardId))
			{
				return this;
			}

			var remainingIds = new List<string>();
			foreach (string attachedItemCardId in GetAttachedItemCardIds())
			{
				if (attachedItemCardId != itemCardId)
				{
					remainingIds.Add(attachedItemCardId);
				}
			}

			return remainingIds.Count == 0
				? new PlayingCard(Suit, Rank, Texture)
				: new PlayingCard(Suit, Rank, Texture, string.Join(",", remainingIds));
		}

		public bool HasAttachedItemEffect(string itemCardId)
		{
			foreach (string attachedItemCardId in GetAttachedItemCardIds())
			{
				if (attachedItemCardId == itemCardId)
				{
					return true;
				}
			}

			return false;
		}

		public IReadOnlyList<string> GetAttachedItemCardIds()
		{
			return string.IsNullOrWhiteSpace(AttachedItemCardIdsCsv)
				? Array.Empty<string>()
				: AttachedItemCardIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		}

		public PlayingCard WithoutItemEffects()
		{
			return HasItemEffect ? new PlayingCard(Suit, Rank, Texture) : this;
		}
	}

	private readonly record struct PlayCardResult(
		bool Played,
		bool Captured,
		bool DeckCollected = false,
		QuestionCardRewardAnimation? QuestionRewardAnimation = null,
		bool CompletesTurn = true);

	private readonly record struct CapturedTableCardVisual(Texture2D StartTexture, Texture2D? ItemOverlayTexture, Vector2 GlobalPosition, int ZIndex);

	private readonly record struct ShuffledTableCardVisual(
		Texture2D MainTexture,
		Texture2D? ItemOverlayTexture,
		Vector2 ItemOverlayPosition,
		Vector2 ItemOverlaySize,
		bool AnimateCardBack,
		float Alpha,
		Vector2 StartGlobalPosition,
		Vector2 TargetGlobalPosition,
		Vector2 ShuffleCenterGlobalPosition,
		int ZIndex);

	private readonly record struct TableSpecialCardTrigger(int TableCardIndex, Texture2D Texture, Vector2 GlobalPosition, int ZIndex);

	private readonly record struct PendingItemAttachmentReward(int PlayerIndex, string ItemCardId);

	private readonly record struct QuestionCardRewardGrant(ItemCard ItemCard, int ItemHandIndex, bool RequiresMagnetAttachment = false);

	private readonly record struct QuestionCardRewardAnimation(ItemCard ItemCard, int ItemHandIndex, Vector2 StartGlobalPosition, bool RequiresMagnetAttachment = false);

	private readonly record struct DrawnHandCard(PlayingCard Card, int HandIndex);

	private readonly record struct CaptureTableCardsResult(
		int CollectedCardCount,
		int RemovedTableCardCount,
		QuestionCardRewardAnimation? QuestionRewardAnimation);

	private enum PendingItemSelectionMode
	{
		None,
		TargetPlayer,
		TargetHandCard,
		MagnetHandCard,
		HandLimitDiscard,
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

		public PlayingCard? Card { get; private set; }
		public bool IsFaceUp { get; set; }
		public bool IsHiddenDuringCapture { get; set; }
		public bool IsBuriedInSand { get; set; }
		public bool IsSlimeCoated { get; set; }
		public ItemCard? ItemOverlay { get; }
		public int ItemOwnerPlayerIndex { get; }
		public int CardOwnerPlayerIndex { get; }

		public void ClearAttachedItemEffect(string itemCardId)
		{
			if (Card.HasValue)
			{
				Card = Card.Value.WithoutAttachedItemEffect(itemCardId);
			}
		}
	}
}
