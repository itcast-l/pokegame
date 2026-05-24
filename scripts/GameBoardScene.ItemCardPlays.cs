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
		if (!_itemCardCatalog.IsPlayable(itemCard.Id))
		{
			return new PlayCardResult(false, false);
		}

		if (IsItemEffectType(itemCard.Id, CardEffectTypes.PlaceTablePairDestroy))
		{
			return await PlayBombCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (IsItemEffectType(itemCard.Id, CardEffectTypes.RevealCards))
		{
			return await PlayLensCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (IsItemEffectType(itemCard.Id, CardEffectTypes.DrawCard))
		{
			return await PlayDrawCardItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (IsItemEffectType(itemCard.Id, CardEffectTypes.ShuffleTable))
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
			targetHandIndex = IsItemEffectType(itemCard.Id, CardEffectTypes.StealCard) ? PickTargetHandIndex(targetPlayerIndex) : -1;
			if (targetPlayerIndex < 0 || (IsItemEffectType(itemCard.Id, CardEffectTypes.StealCard) && targetHandIndex < 0))
			{
				return new PlayCardResult(false, false);
			}
		}

		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex);
		itemHand.RemoveAt(itemHandIndex);
		ShiftJellyfishHiddenHandSlotAfterRemovedCard(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
		RenderPlayerHands();
		await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, itemGlobalPosition ?? Vector2.Zero, 0, GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = (await ResolveItemCardOnPlayAsync(
			itemCard,
			playerIndex,
			itemHandIndex,
			targetPlayerIndex,
			targetHandIndex,
			itemGlobalPosition ?? Vector2.Zero,
			turnSequenceGeneration)) with { CompletesTurn = false };
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
		if (!IsItemEffectType(drawCard.Id, CardEffectTypes.DrawCard))
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
		ShiftJellyfishHiddenHandSlotAfterRemovedCard(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
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

		PlayCardResult result = await ResolveItemCardOnPlayAsync(
			drawCard,
			playerIndex,
			itemHandIndex,
			-1,
			-1,
			transformAreaGlobalPosition,
			turnSequenceGeneration);
		return result with { CompletesTurn = false };
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
		ShiftJellyfishHiddenHandSlotAfterRemovedCard(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
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

		PlayCardResult result = await ResolveItemCardOnPlayAsync(
			itemCard,
			playerIndex,
			itemHandIndex,
			-1,
			-1,
			itemGlobalPosition ?? Vector2.Zero,
			turnSequenceGeneration);
		return result with { CompletesTurn = false };
	}

}
