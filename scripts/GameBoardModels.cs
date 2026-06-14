using System;
using System.Collections.Generic;
using Godot;

internal readonly struct PlayingCard
{
	public PlayingCard(string suit, string rank, Texture2D texture, string attachedItemCardIdsCsv = "")
	{
		Suit = suit;
		Rank = rank;
		Texture = texture;
		AttachedItemCardIdsCsv = attachedItemCardIdsCsv;
		_attachedItemCardIds = ParseAttachedItemCardIds(attachedItemCardIdsCsv);
	}

	private PlayingCard(string suit, string rank, Texture2D texture, string[] attachedItemCardIds)
	{
		Suit = suit;
		Rank = rank;
		Texture = texture;
		_attachedItemCardIds = attachedItemCardIds;
		AttachedItemCardIdsCsv = attachedItemCardIds.Length == 0 ? string.Empty : string.Join(",", attachedItemCardIds);
	}

	public string Suit { get; }
	public string Rank { get; }
	public Texture2D Texture { get; }
	public string AttachedItemCardIdsCsv { get; }
	public bool HasItemEffect => AttachedItemCardIds.Count > 0;

	private IReadOnlyList<string> AttachedItemCardIds => _attachedItemCardIds ?? Array.Empty<string>();

	private readonly string[]? _attachedItemCardIds;

	public PlayingCard WithAttachedItemEffect(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId) || HasAttachedItemEffect(itemCardId))
		{
			return this;
		}

		IReadOnlyList<string> existingIds = AttachedItemCardIds;
		var attachedItemCardIds = new string[existingIds.Count + 1];
		for (int index = 0; index < existingIds.Count; index++)
		{
			attachedItemCardIds[index] = existingIds[index];
		}

		attachedItemCardIds[^1] = itemCardId;
		return new PlayingCard(Suit, Rank, Texture, attachedItemCardIds);
	}

	public PlayingCard WithoutAttachedItemEffect(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId) || !HasAttachedItemEffect(itemCardId))
		{
			return this;
		}

		IReadOnlyList<string> existingIds = AttachedItemCardIds;
		if (existingIds.Count == 1)
		{
			return new PlayingCard(Suit, Rank, Texture);
		}

		var remainingIds = new string[existingIds.Count - 1];
		int nextIndex = 0;
		foreach (string attachedItemCardId in existingIds)
		{
			if (attachedItemCardId != itemCardId)
			{
				remainingIds[nextIndex++] = attachedItemCardId;
			}
		}

		return nextIndex == 0 ? new PlayingCard(Suit, Rank, Texture) : new PlayingCard(Suit, Rank, Texture, remainingIds);
	}

	public bool HasAttachedItemEffect(string itemCardId)
	{
		foreach (string attachedItemCardId in AttachedItemCardIds)
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
		return AttachedItemCardIds;
	}

	public PlayingCard WithoutItemEffects()
	{
		return HasItemEffect ? new PlayingCard(Suit, Rank, Texture) : this;
	}

	private static string[] ParseAttachedItemCardIds(string attachedItemCardIdsCsv)
	{
		return string.IsNullOrWhiteSpace(attachedItemCardIdsCsv)
			? Array.Empty<string>()
			: attachedItemCardIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
	}
}

internal readonly record struct PlayCardResult(
	bool Played,
	bool Captured,
	bool DeckCollected = false,
	QuestionCardRewardAnimation? QuestionRewardAnimation = null,
	bool CompletesTurn = true,
	NetworkItemPlayVisualSync? ItemPlayVisualSync = null,
	NetworkCaptureVisualSync? CaptureVisualSync = null);

internal readonly record struct CapturedTableCardVisual(Texture2D StartTexture, Texture2D? ItemOverlayTexture, Vector2 GlobalPosition, int ZIndex);

internal readonly record struct ItemAttachmentOverlay(Texture2D Texture, int Priority);

internal readonly record struct ShuffledTableCardVisual(
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

internal readonly record struct TableSpecialCardTrigger(int TableCardIndex, Texture2D Texture, Vector2 GlobalPosition, int ZIndex, int Priority);

internal readonly record struct PendingItemAttachmentReward(int PlayerIndex, string ItemCardId);

internal readonly record struct QuestionCardRewardGrant(ItemCard ItemCard, int ItemHandIndex, bool RequiresMagnetAttachment = false);

internal readonly record struct QuestionCardRewardAnimation(ItemCard ItemCard, int ItemHandIndex, Vector2 StartGlobalPosition, bool RequiresMagnetAttachment = false);

internal readonly record struct DrawnHandCard(PlayingCard Card, int HandIndex);

internal readonly record struct NetworkItemPlayVisualSync(
	string ItemCardId,
	string PlayPresentation,
	int ItemHandIndex,
	int TargetTableCardIndex = -1);

internal readonly record struct NetworkCaptureVisualSync(
	int PlayerIndex,
	int CaptureStartIndex,
	int[] ExtraTableCardIndexes,
	float FlightStaggerSeconds);

internal readonly record struct CaptureTableCardsResult(
	int CollectedCardCount,
	int RemovedTableCardCount,
	QuestionCardRewardAnimation? QuestionRewardAnimation);

internal enum PendingItemSelectionMode
{
	None,
	TargetPlayer,
	TargetHandCard,
	MagnetHandCard,
	HandLimitDiscard,
}

internal readonly struct ItemCard
{
	public ItemCard(string id, Texture2D texture)
	{
		Id = id;
		Texture = texture;
	}

	public string Id { get; }
	public Texture2D Texture { get; }
}

internal sealed class TableCard
{
	public TableCard(
		PlayingCard? card,
		bool isFaceUp,
		ItemCard? itemOverlay = null,
		int itemOwnerPlayerIndex = -1,
		int cardOwnerPlayerIndex = -1,
		bool removeCardOnCapture = false)
	{
		Card = card;
		IsFaceUp = isFaceUp;
		ItemOverlay = itemOverlay;
		ItemOwnerPlayerIndex = itemOwnerPlayerIndex;
		CardOwnerPlayerIndex = cardOwnerPlayerIndex;
		RemoveCardOnCapture = removeCardOnCapture;
	}

	public PlayingCard? Card { get; private set; }
	public bool IsFaceUp { get; set; }
	public bool IsHiddenDuringCapture { get; set; }
	public bool IsBuriedInSand { get; set; }
	public bool IsSlimeCoated { get; set; }
	public ItemCard? ItemOverlay { get; }
	public int ItemOwnerPlayerIndex { get; }
	public int CardOwnerPlayerIndex { get; }
	public bool RemoveCardOnCapture { get; }

	public void ClearAttachedItemEffect(string itemCardId)
	{
		if (Card.HasValue)
		{
			Card = Card.Value.WithoutAttachedItemEffect(itemCardId);
		}
	}
}
