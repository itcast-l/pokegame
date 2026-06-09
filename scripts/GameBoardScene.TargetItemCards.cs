using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> ResolveQueenEffectAsync(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int targetHandIndex,
		int turnSequenceGeneration)
	{
		if (ownerPlayerIndex < 0
			|| ownerPlayerIndex >= _playerHands.Count
			|| ownerPlayerIndex >= _playerItemHands.Count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= _playerHands.Count
			|| targetPlayerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (targetPlayerIndex == ownerPlayerIndex || targetHandIndex < 0 || targetHandIndex >= GetTotalHandCardCount(targetPlayerIndex))
		{
			return new PlayCardResult(false, false);
		}

		if (targetHandIndex < _playerHands[targetPlayerIndex].Count)
		{
			if (!HasRoomForNewHandCard(ownerPlayerIndex)
				&& !await DiscardHandCardForNewItemCardAsync(ownerPlayerIndex, turnSequenceGeneration))
			{
				return new PlayCardResult(false, false);
			}

			PlayingCard stolenCard = _playerHands[targetPlayerIndex][targetHandIndex];
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenPlayingCardTexture(targetPlayerIndex, stolenCard),
				GetVisibleStolenPlayingCardAttachmentOverlayTexture(targetPlayerIndex, stolenCard),
				1.0f,
				GetStolenPlayingCardTargetDisplayIndex(ownerPlayerIndex));
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				RenderPlayerHands();
				return new PlayCardResult(false, false);
			}

			_playerHands[targetPlayerIndex].RemoveAt(targetHandIndex);
			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			_playerHands[ownerPlayerIndex].Add(stolenCard);
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			RenderPlayerHands();
			return new PlayCardResult(true, false);
		}

		int itemHandIndex = targetHandIndex - _playerHands[targetPlayerIndex].Count;
		ItemCard stolenItemCard = _playerItemHands[targetPlayerIndex][itemHandIndex];
		if (!HasRoomForNewHandCard(ownerPlayerIndex)
			&& !await DiscardHandCardForNewItemCardAsync(ownerPlayerIndex, turnSequenceGeneration))
		{
			return new PlayCardResult(false, false);
		}

		PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
		await PlayStealHandCardAnimationAsync(
			ownerPlayerIndex,
			targetPlayerIndex,
			targetHandIndex,
			GetVisibleStolenItemCardTexture(targetPlayerIndex, stolenItemCard),
			null,
			GetVisibleStolenItemCardScale(targetPlayerIndex, stolenItemCard),
			GetStolenItemCardTargetDisplayIndex(ownerPlayerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			RenderPlayerHands();
			return new PlayCardResult(false, false);
		}

		_playerItemHands[targetPlayerIndex].RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
		_playerItemHands[ownerPlayerIndex].Add(stolenItemCard);
		PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerHands();
		return new PlayCardResult(true, false);
	}

	private async Task PlayStealHandCardAnimationAsync(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int sourceDisplayIndex,
		Texture2D? cardTexture,
		Texture2D? attachmentOverlayTexture,
		float visualScale,
		int targetHandDisplayIndex)
	{
		if (cardTexture == null || !IsInsideTree())
		{
			return;
		}

		Vector2 startGlobalPosition = GetHandDisplayCardSlotGlobalPosition(targetPlayerIndex, sourceDisplayIndex);
		if (targetPlayerIndex >= 0 && targetPlayerIndex < _playerHandAreas.Count)
		{
			Control? sourceCardSlot = _playerHandAreas[targetPlayerIndex].GetNodeOrNull<Control>($"HandCard_{sourceDisplayIndex}");
			startGlobalPosition = sourceCardSlot?.GlobalPosition ?? startGlobalPosition;
			if (sourceCardSlot != null)
			{
				sourceCardSlot.Visible = false;
			}
		}

		int ownerDisplayCardCountAfterSteal = GetTotalHandCardCount(ownerPlayerIndex) + 1;
		Vector2 targetGlobalPosition = GetHandDisplayCardSlotGlobalPosition(
			ownerPlayerIndex,
			targetHandDisplayIndex,
			ownerDisplayCardCountAfterSteal);
		float targetVisualScale = GetHandCardVisualScaleForPlayer(ownerPlayerIndex) * visualScale;
		await PlayStolenHandCardFlightAsync(
			cardTexture,
			attachmentOverlayTexture,
			startGlobalPosition,
			targetGlobalPosition,
			GetCardRotationDegreesForPlayer(targetPlayerIndex),
			GetCardRotationDegreesForPlayer(ownerPlayerIndex),
			visualScale,
			targetVisualScale);
	}

	private Texture2D? GetVisibleStolenPlayingCardTexture(int sourcePlayerIndex, PlayingCard card)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null ? _cardBackTexture : card.Texture;
	}

	private Texture2D? GetVisibleStolenPlayingCardAttachmentOverlayTexture(int sourcePlayerIndex, PlayingCard card)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) ? null : GetCardAttachmentOverlayTexture(card);
	}

	private Texture2D? GetVisibleStolenItemCardTexture(int sourcePlayerIndex, ItemCard itemCard)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null ? _cardBackTexture : itemCard.Texture;
	}

	private float GetVisibleStolenItemCardScale(int sourcePlayerIndex, ItemCard itemCard)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null ? 1.0f : GetItemCardVisualScale(itemCard.Id);
	}

	private int GetStolenPlayingCardTargetDisplayIndex(int ownerPlayerIndex)
	{
		return ownerPlayerIndex >= 0 && ownerPlayerIndex < _playerHands.Count ? _playerHands[ownerPlayerIndex].Count : 0;
	}

	private int GetStolenItemCardTargetDisplayIndex(int ownerPlayerIndex)
	{
		return GetTotalHandCardCount(ownerPlayerIndex);
	}

	private bool IsPlayerHandHidden(int playerIndex)
	{
		return !IsLocalHandPlayer(playerIndex) && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;
	}

	private PlayCardResult ResolveKingEffect(int ownerPlayerIndex, int targetPlayerIndex)
	{
		return ResolveKingEffect(ownerPlayerIndex, targetPlayerIndex, 3);
	}

	private PlayCardResult ResolveKingEffect(int ownerPlayerIndex, int targetPlayerIndex, int cardsToMoveLimit)
	{
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= _playerDecks.Count || targetPlayerIndex < 0 || targetPlayerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (targetPlayerIndex == ownerPlayerIndex)
		{
			return new PlayCardResult(false, false);
		}

		int cardsToMove = Math.Min(cardsToMoveLimit, _playerDecks[targetPlayerIndex].Count);
		for (int cardIndex = 0; cardIndex < cardsToMove; cardIndex++)
		{
			PlayingCard topCard = _playerDecks[targetPlayerIndex][0];
			_playerDecks[targetPlayerIndex].RemoveAt(0);
			AddCardToPlayerDeck(ownerPlayerIndex, topCard);
		}

		Shuffle(_playerDecks[ownerPlayerIndex]);
		Shuffle(_playerDecks[targetPlayerIndex]);
		if (cardsToMove > 0)
		{
			PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

		return new PlayCardResult(true, false, cardsToMove > 0);
	}

}
