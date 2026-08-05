using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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

		DirectItemHandCardSource handCardSource = DirectItemTargetRules.ResolveHandCardSource(
			_playerHands[targetPlayerIndex].Count,
			_playerItemHands[targetPlayerIndex].Count,
			targetHandIndex);
		if (handCardSource == DirectItemHandCardSource.BaseHand)
		{
			if (!HasRoomForNewHandCard(ownerPlayerIndex)
				&& !await DiscardHandCardForNewItemCardAsync(ownerPlayerIndex, turnSequenceGeneration))
			{
				return new PlayCardResult(false, false);
			}

			PlayingCard stolenCard = _playerHands[targetPlayerIndex][targetHandIndex];
			long expectedStolenCardRuntimeInstanceId = stolenCard.InstanceId;
			bool shouldPlayLocalVisual = ShouldPlayOwnerOnlyVisualLocally(ownerPlayerIndex);
			TryBroadcastStealHandCardVisualEventFromHost(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				stolenCard,
				null,
				turnSequenceGeneration);
			if (shouldPlayLocalVisual)
			{
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
			}

			if (!TrySubmitTransferHandCardRuleCommand(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				expectedStolenCardRuntimeInstanceId,
				string.Empty,
				MaxTotalHandCards,
				turnSequenceGeneration,
				out _))
			{
				RenderPlayerHands();
				return new PlayCardResult(false, false);
			}

			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			MarkNetworkAuthorityTransferMutationApplied();
			if (shouldPlayLocalVisual)
			{
				PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			}

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

		bool shouldPlayLocalItemVisual = ShouldPlayOwnerOnlyVisualLocally(ownerPlayerIndex);
		TryBroadcastStealHandCardVisualEventFromHost(
			ownerPlayerIndex,
			targetPlayerIndex,
			targetHandIndex,
			null,
			stolenItemCard,
			turnSequenceGeneration);
		if (shouldPlayLocalItemVisual)
		{
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
		}

		if (!TrySubmitTransferHandCardRuleCommand(
			ownerPlayerIndex,
			targetPlayerIndex,
			targetHandIndex,
			0,
			stolenItemCard.Id,
			MaxTotalHandCards,
			turnSequenceGeneration,
			out _))
		{
			RenderPlayerHands();
			return new PlayCardResult(false, false);
		}

		ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
		MarkNetworkAuthorityTransferMutationApplied();
		if (shouldPlayLocalItemVisual)
		{
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

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
		if (_networkAuthorityRuleTransactionActive || cardTexture == null || !IsInsideTree())
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
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null
			? _cardBackTexture
			: GetPlayingCardTexture(card);
	}

	private Texture2D? GetVisibleStolenPlayingCardAttachmentOverlayTexture(int sourcePlayerIndex, PlayingCard card)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) ? null : GetCardAttachmentOverlayTexture(card);
	}

	private Texture2D? GetVisibleStolenItemCardTexture(int sourcePlayerIndex, ItemCard itemCard)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null
			? _cardBackTexture
			: GetItemCardTexture(itemCard);
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
		return !IsLocalHandPlayer(playerIndex) && _cardBackTexture != null;
	}

	private Task<PlayCardResult> ResolveKingEffectAsync(int ownerPlayerIndex, int targetPlayerIndex, int turnSequenceGeneration)
	{
		return ResolveKingEffectAsync(ownerPlayerIndex, targetPlayerIndex, 3, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> ResolveKingEffectAsync(int ownerPlayerIndex, int targetPlayerIndex, int cardsToMoveLimit, int turnSequenceGeneration)
	{
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= _playerDecks.Count || targetPlayerIndex < 0 || targetPlayerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (targetPlayerIndex == ownerPlayerIndex)
		{
			return new PlayCardResult(false, false);
		}

		int cardsToMove = DirectItemTargetRules.GetDeckTransferCount(
			_playerDecks[targetPlayerIndex].Count,
			cardsToMoveLimit);
		var expectedSourceTopCardIds = ImmutableArray.CreateBuilder<long>(cardsToMove);
		for (int cardIndex = 0; cardIndex < cardsToMove; cardIndex++)
		{
			expectedSourceTopCardIds.Add(_playerDecks[targetPlayerIndex][cardIndex].InstanceId);
		}
		bool shouldPlayLocalVisual = ShouldPlayOwnerOnlyVisualLocally(ownerPlayerIndex);
		if (cardsToMove > 0)
		{
			TryBroadcastStealDeckCardsVisualEventFromHost(ownerPlayerIndex, targetPlayerIndex, cardsToMove, turnSequenceGeneration);
			if (shouldPlayLocalVisual)
			{
				PlayGameSfx(GameSfx.StealCard, -1.5f, 0.97f, 1.04f, 0.08);
				await PlayStealDeckCardsAnimationAsync(ownerPlayerIndex, targetPlayerIndex, cardsToMove, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					RenderPlayerCardPiles();
					return new PlayCardResult(false, false);
				}
			}
		}

		if (!TrySubmitTransferDeckCardsRuleCommand(
			ownerPlayerIndex,
			targetPlayerIndex,
			cardsToMoveLimit,
			ShouldPreserveCardEffectsWhenEnteringDeck(),
			expectedSourceTopCardIds.MoveToImmutable(),
			turnSequenceGeneration,
			out RuleCommandResult transferResult))
		{
			RenderPlayerCardPiles();
			return new PlayCardResult(false, false);
		}
		cardsToMove = transferResult.Mutation.CollectedCardCount;
		if (cardsToMove > 0)
		{
			MarkNetworkAuthorityTransferMutationApplied();
		}

		if (cardsToMove > 0)
		{
			if (shouldPlayLocalVisual)
			{
				PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			}
		}

		RenderPlayerCardPiles();
		return new PlayCardResult(true, false, cardsToMove > 0);
	}

	private async Task PlayStealDeckCardsAnimationAsync(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int cardsToMove,
		int turnSequenceGeneration)
	{
		if (_networkAuthorityRuleTransactionActive || cardsToMove <= 0 || !IsInsideTree())
		{
			return;
		}

		Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(targetPlayerIndex);
		Vector2 targetGlobalPosition = GetDrawCardStartGlobalPosition(ownerPlayerIndex);
		float startRotationDegrees = GetCardPileRotationDegreesForPlayer(targetPlayerIndex);
		float targetRotationDegrees = GetCardPileRotationDegreesForPlayer(ownerPlayerIndex);
		for (int cardIndex = 0; cardIndex < cardsToMove; cardIndex++)
		{
			await PlayStolenDeckCardFlightAsync(
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				targetRotationDegrees);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}
	}

	private bool ShouldPlayOwnerOnlyVisualLocally(int ownerPlayerIndex)
	{
		return !_networkAuthorityRuleTransactionActive
			&& (!IsNetworkMultiplayerMode() || IsLocalHandPlayer(ownerPlayerIndex));
	}

	private void TryBroadcastStealHandCardVisualEventFromHost(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int targetHandIndex,
		PlayingCard? stolenPlayingCard,
		ItemCard? stolenItemCard,
		int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| !TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			return;
		}

		string stolenCardCode = stolenPlayingCard.HasValue ? ToNetworkCardCode(stolenPlayingCard.Value) : string.Empty;
		IReadOnlyList<string> attachedItemCardIds = stolenPlayingCard.HasValue
			? stolenPlayingCard.Value.GetAttachedItemCardIds()
			: Array.Empty<string>();
		GetNetworkManager()?.BroadcastStealHandCardVisualEventFromHost(
			ownerPlayerIndex,
			targetPlayerIndex,
			targetHandIndex,
			stolenCardCode,
			attachedItemCardIds,
			stolenItemCard.HasValue ? stolenItemCard.Value.Id : string.Empty,
			turnSequenceGeneration,
			snapshotId);
	}

	private void TryBroadcastStealDeckCardsVisualEventFromHost(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int movedCardCount,
		int turnSequenceGeneration)
	{
		if (!IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| movedCardCount <= 0
			|| !TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			return;
		}

		GetNetworkManager()?.BroadcastStealDeckCardsVisualEventFromHost(
			ownerPlayerIndex,
			targetPlayerIndex,
			movedCardCount,
			turnSequenceGeneration,
			snapshotId);
	}

}
