using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private delegate Task<PlayCardResult> PlayItemCardPresentationAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration);
	private delegate bool CanPlayItemCardPresentation(int playerIndex, ItemCardDefinition definition);

	private Dictionary<string, PlayItemCardPresentationAsync> _itemPlayPresentationHandlers = default!;
	private Dictionary<string, CanPlayItemCardPresentation> _itemPlayPresentationValidators = default!;

	private Dictionary<string, PlayItemCardPresentationAsync> CreateItemPlayPresentationHandlers()
	{
		var handlers = new Dictionary<string, PlayItemCardPresentationAsync>(StringComparer.Ordinal)
		{
			[CardPlayPresentationTypes.TableOverlay] = PlayBombCardFromHandAsync,
			[CardPlayPresentationTypes.LensSplit] = PlayLensCardFromHandAsync,
			[CardPlayPresentationTypes.TransformAreaTrigger] = PlayTransformAreaItemCardFromHandAsync,
		};
		if (!ItemCardEffectContractRegistry.HasExactPresentationCoverage(
			handlers.Keys,
			ItemCardEffectContractRegistry.GenericPresentations,
			out string coverageError))
		{
			throw new InvalidOperationException(coverageError);
		}

		return handlers;
	}

	private Dictionary<string, CanPlayItemCardPresentation> CreateItemPlayPresentationValidators()
	{
		var validators = new Dictionary<string, CanPlayItemCardPresentation>(StringComparer.Ordinal)
		{
			[CardPlayPresentationTypes.TableOverlay] = (_, _) => _tableCards.Count > 0,
			[CardPlayPresentationTypes.LensSplit] = (playerIndex, definition) => HasLensRevealTargets(playerIndex, definition.EffectValues),
			[CardPlayPresentationTypes.TransformAreaTrigger] = (_, definition) =>
				definition.EffectValues.GetString(CardEffectValueKeys.Target) != CardEffectTargets.Table
				|| GetMovableTableShuffleCardCount() >= 2,
		};
		if (!ItemCardEffectContractRegistry.HasExactPresentationCoverage(
			validators.Keys,
			ItemCardEffectContractRegistry.GenericPresentations,
			out string coverageError))
		{
			throw new InvalidOperationException(coverageError);
		}

		return validators;
	}

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
		if (!_itemCardCatalog.TryGetDefinition(itemCard.Id, out ItemCardDefinition definition)
			|| !definition.PlayableFromHand
			|| !definition.EnabledInGame
			|| !CanPlayItemCard(playerIndex, itemCard.Id))
		{
			return new PlayCardResult(false, false);
		}

		if (_itemPlayPresentationHandlers.TryGetValue(definition.PlayPresentation, out PlayItemCardPresentationAsync? presentationHandler))
		{
			return await presentationHandler(playerIndex, itemHandIndex, turnSequenceGeneration);
		}

		if (TryStartPendingTargetedItemSelection(itemCard, playerIndex, itemHandIndex, turnSequenceGeneration, out PlayCardResult pendingSelectionResult))
		{
			return pendingSelectionResult;
		}

		int targetPlayerIndex = -1;
		int targetHandIndex = -1;
		if (RequiresPlayerTarget(itemCard.Id))
		{
			targetPlayerIndex = PickItemTargetPlayer(itemCard.Id, playerIndex);
			targetHandIndex = IsPlayerHandTargetItemCard(itemCard.Id) ? PickTargetHandIndex(targetPlayerIndex) : -1;
			if (targetPlayerIndex < 0 || (IsPlayerHandTargetItemCard(itemCard.Id) && targetHandIndex < 0))
			{
				return new PlayCardResult(false, false);
			}
		}

		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex);
		itemHand.RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
		RenderPlayerHands();
		PlaySpecialItemCardPlaySfx();
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
			turnSequenceGeneration)) with
		{
			CompletesTurn = false,
			ItemPlayVisualSync = new NetworkItemPlayVisualSync(itemCard.Id, definition.PlayPresentation, itemHandIndex),
		};
		await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerCardPiles();
		if (!QuestionRewardPresentationRules.ShouldDelayTableRenderForQuestionReward(
			result.QuestionRewardAnimation.HasValue,
			IsLocalHandPlayer(playerIndex)))
		{
			RenderTableCards();
		}
		SendQuestionRewardVisualEventIfNeeded(playerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
		await PlayQuestionCardRewardAnimationAsync(playerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}
		await ApplyAdventureCaptureRewardEffectsAsync(
			playerIndex,
			result.AdventureQuestionRewardEffects,
			result.AdventurePostCaptureEffects,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await MaybeResolveAppliedAdventureQuestionRewardRejudgeAsync(
			playerIndex,
			result.QuestionRewardAnimation,
			turnSequenceGeneration);
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
		return result with
		{
			ItemPlayVisualSync = new NetworkItemPlayVisualSync(itemCard.Id, definition.PlayPresentation, itemHandIndex),
		};
	}

	private bool TryStartPendingTargetedItemSelection(
		ItemCard itemCard,
		int playerIndex,
		int itemHandIndex,
		int turnSequenceGeneration,
		out PlayCardResult result)
	{
		result = new PlayCardResult(false, false);
		if (!_itemCardCatalog.TryGetDefinition(itemCard.Id, out ItemCardDefinition definition)
			|| definition.PlayPresentation != CardPlayPresentationTypes.TargetedTrigger
			|| !RequiresPlayerTarget(itemCard.Id))
		{
			return false;
		}

		if (!HasPlayableTargetForItemCard(itemCard.Id, playerIndex))
		{
			return true;
		}

		if (!ShouldUseInteractiveTargetedItemSelection(playerIndex))
		{
			return false;
		}

		StartPendingItemSelection(itemCard.Id, playerIndex, itemHandIndex, turnSequenceGeneration);
		result = new PlayCardResult(true, false, CompletesTurn: false);
		return true;
	}

	private bool ShouldUseInteractiveTargetedItemSelection(int playerIndex)
	{
		if (!IsNetworkMultiplayerMode())
		{
			return playerIndex == MainPlayerIndex;
		}

		return !(GetNetworkManager()?.IsSeatAiControlled(playerIndex) ?? false);
	}

	private async Task<PlayCardResult> PlayTransformAreaItemCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
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

		Vector2 startGlobalPosition = GetHandItemCardGlobalPosition(playerIndex, itemHandIndex)
			?? _playerHandAreas[playerIndex].GlobalPosition + (_playerHandAreas[playerIndex].Size - CardVisualSize) / 2.0f;
		Control? sourceCardSlot = _playerHandAreas[playerIndex].GetNodeOrNull<Control>($"HandCard_{_playerHands[playerIndex].Count + itemHandIndex}");
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		Vector2 transformAreaGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		itemHand.RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(playerIndex, _playerHands[playerIndex].Count + itemHandIndex);
		RenderPlayerHands();
		PlaySpecialItemCardPlaySfx();

		await PlayItemCardToTransformAreaAnimationAsync(
			itemCard.Texture,
			startGlobalPosition,
			transformAreaGlobalPosition,
			GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await PlaySpecialCardTriggerEffectAsync(itemCard.Texture, transformAreaGlobalPosition, 0);
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
			transformAreaGlobalPosition,
			turnSequenceGeneration);
		return result with
		{
			CompletesTurn = false,
			ItemPlayVisualSync = new NetworkItemPlayVisualSync(itemCard.Id, CardPlayPresentationTypes.TransformAreaTrigger, itemHandIndex),
		};
	}

}
