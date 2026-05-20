using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayItemCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard itemCard = itemHand[itemHandIndex];
		if (!PlayableSpecialItemCardIds.Contains(itemCard.Id))
		{
			return new PlayCardResult(false, false);
		}

		if (itemCard.Id == BombItemCardId)
		{
			return await PlayBombCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (itemCard.Id == LensItemCardId)
		{
			return await PlayLensCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (itemCard.Id == DrawCardItemCardId)
		{
			return await PlayDrawCardItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (itemCard.Id == ShuffleItemCardId)
		{
			return await PlayShuffleItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (IsAttachmentItemCard(itemCard.Id))
		{
			return await PlayAttachmentItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (playerIndex == MainPlayerIndex && RequiresPlayerTarget(itemCard.Id))
		{
			if (!HasPlayableTargetForItemCard(itemCard.Id, playerIndex))
			{
				return new PlayCardResult(false, false);
			}

			StartPendingItemSelection(itemCard.Id, playerIndex, itemHandIndex, turnSequenceGeneration);
			return new PlayCardResult(true, false, CompletesTurn: false);
		}

		int targetPlayerIndex = -1;
		int targetHandIndex = -1;
		if (RequiresPlayerTarget(itemCard.Id))
		{
			targetPlayerIndex = PickItemTargetPlayer(itemCard.Id, playerIndex);
			targetHandIndex = itemCard.Id == QueenItemCardId ? PickTargetHandIndex(targetPlayerIndex) : -1;
			if (targetPlayerIndex < 0 || (itemCard.Id == QueenItemCardId && targetHandIndex < 0))
			{
				return new PlayCardResult(false, false);
			}
		}

		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex);
		itemHand.RemoveAt(itemHandIndex);
		RenderPlayerHands();
		await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, itemGlobalPosition ?? Vector2.Zero, 0, GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = ResolveItemCardEffect(itemCard.Id, playerIndex, targetPlayerIndex, targetHandIndex) with { CompletesTurn = false };
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerCardPiles();
		RenderTableCards();
		await PlayQuestionCardRewardAnimationAsync(playerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await ResolvePendingMagnetAttachmentsAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		if (result.DeckCollected)
		{
			await PlayPhysicalDeckShuffleAsync(playerIndex);
		}

		CheckForWinner();
		return result;
	}

	private async Task<PlayCardResult> PlayDrawCardItemCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| playerIndex >= _playerHandAreas.Count
			|| playerIndex >= _playerDecks.Count
			|| _playerDecks[playerIndex].Count == 0)
		{
			return new PlayCardResult(false, false);
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard drawCard = itemHand[itemHandIndex];
		if (drawCard.Id != DrawCardItemCardId)
		{
			return new PlayCardResult(false, false);
		}

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex)
			?? _playerHandAreas[playerIndex].GlobalPosition + (_playerHandAreas[playerIndex].Size - CardVisualSize) / 2.0f;
		Control? sourceCardSlot = _playerHandAreas[playerIndex].GetNodeOrNull<Control>($"HandCard_{_playerHands[playerIndex].Count + itemHandIndex}");
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		itemHand.RemoveAt(itemHandIndex);
		RenderPlayerHands();

		await PlayItemCardToTransformAreaAnimationAsync(
			drawCard.Texture,
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await PlaySpecialCardTriggerEffectAsync(drawCard.Texture, transformAreaGlobalPosition, 0);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		DrawCardFromDeckToHand(playerIndex);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		CheckForWinner();
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> PlayAttachmentItemCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| playerIndex >= _playerHandAreas.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard itemCard = itemHand[itemHandIndex];
		if (!IsAttachmentItemCard(itemCard.Id) || !PlayerHasAttachableMagnetCard(playerIndex, itemCard.Id))
		{
			return new PlayCardResult(false, false);
		}

		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex);
		itemHand.RemoveAt(itemHandIndex);
		RenderPlayerHands();
		await PlaySpecialCardTriggerEffectAsync(
			itemCard.Texture,
			itemGlobalPosition ?? Vector2.Zero,
			0,
			GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		if (playerIndex == MainPlayerIndex)
		{
			await ResolveMainPlayerMagnetAttachmentAsync(itemCard.Id, turnSequenceGeneration);
		}
		else
		{
			AttachMagnetToRandomHandCard(playerIndex, itemCard.Id);
		}

		RenderPlayerHands();
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

}
