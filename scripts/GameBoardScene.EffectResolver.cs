using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> ResolvePlaceTablePairDestroyAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		await ResolveBombPairAsync(turnSequenceGeneration);
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> ResolveDrawCardEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		int drawCount = Mathf.Max(1, request.Rule.EffectValues.GetInt(CardEffectValueKeys.Count, 1));
		bool drewAny = false;
		PlayGameSfx(GameSfx.ItemDrawCard, -1.5f, 0.97f, 1.04f, 0.08);
		for (int drawIndex = 0; drawIndex < drawCount; drawIndex++)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			DrawnHandCard? drawnCard = DrawCardFromDeckToHand(request.Event.ActorPlayerIndex);
			drewAny |= drawnCard.HasValue;
			await PlayReplacementDrawAnimationAsync(request.Event.ActorPlayerIndex, drawnCard, turnSequenceGeneration);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
		CheckForWinner();
		return new PlayCardResult(drewAny, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> ResolveAttachModifierEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		string itemCardId = request.Definition.Id;
		int playerIndex = request.Event.ActorPlayerIndex;
		if (!PlayerHasAttachableMagnetCard(playerIndex, itemCardId))
		{
			return new PlayCardResult(false, false);
		}

		if (ShouldUseInteractiveHumanItemFollowup(playerIndex))
		{
			await ResolveHumanMagnetAttachmentAsync(playerIndex, itemCardId, turnSequenceGeneration);
		}
		else
		{
			if (AttachMagnetToAutoSelectedHandCard(playerIndex, itemCardId))
			{
				PlayGameSfx(GameSfx.MagnetAttach, -1.0f, 0.97f, 1.03f, 0.08);
			}
		}

		RenderPlayerHands();
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> ResolveRevealCardsEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		int ownerPlayerIndex = request.Event.ActorPlayerIndex;
		List<int> targetIndexes = PickLensRevealTargetIndexes(ownerPlayerIndex, request.Rule.EffectValues);
		if (targetIndexes.Count == 0)
		{
			return new PlayCardResult(false, false);
		}

		Vector2 splitGlobalCenter = request.Event.OriginGlobalCenter
			?? (_networkAuthorityRuleTransactionActive
				? GetNetworkAuthorityRuleVisualStartGlobalPosition(Vector2.Zero) + CardVisualSize / 2.0f
				: GetLensRevealFallbackGlobalCenter(ownerPlayerIndex));
		await RevealLensTargetCardsAsync(targetIndexes, splitGlobalCenter, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderTableCards();
		CheckForWinner();
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> ResolveShuffleTableEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		List<TableCard> shuffledTableCards = BuildShuffledTableCards();
		bool shuffleCompleted = await PlayTableCardsShuffleAnimationAsync(shuffledTableCards, turnSequenceGeneration);
		if (!shuffleCompleted || turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		ApplyShuffledTableCards(shuffledTableCards);
		RenderTableCards();
		CheckForWinner();
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> ResolveCollectTableEffectAsync(
	CardTriggerRequest request,
	int turnSequenceGeneration)
{
	int ownerPlayerIndex = request.Event.ActorPlayerIndex;
	if (ownerPlayerIndex < 0
		|| ownerPlayerIndex >= _playerDecks.Count)
	{
		return new PlayCardResult(false, false);
	}

	var captureTableReferenceSnapshot =
		new List<TableCard>(_tableCards);
	CaptureTableCardsResult captureResult =
		await CaptureTableCardsAsync(
			ownerPlayerIndex,
			0,
			turnSequenceGeneration:
				turnSequenceGeneration,
			prePlayTableCards:
				captureTableReferenceSnapshot,
			expectedTableCardReferences:
				captureTableReferenceSnapshot,
			actionSource: $"effect_collect_table:{request.Definition.Id}",
			matchSource: AdventureCaptureMatchSource.CollectAll);
	if (captureResult.Aborted)
	{
		return new PlayCardResult(false, false);
	}

	if (captureResult.Applied && !IsGameOver())
	{
		Shuffle(_playerDecks[ownerPlayerIndex]);
		Cut(_playerDecks[ownerPlayerIndex]);
	}

	return new PlayCardResult(
		true,
		captureResult.Applied,
		captureResult.CollectedCardCount > 0,
		captureResult.QuestionRewardAnimation,
		AdventureQuestionRewardEffects:
			captureResult.AdventureQuestionRewardEffects,
		AdventurePostCaptureEffects:
			captureResult.AdventurePostCaptureEffects);
}

	private async Task<PlayCardResult> ResolveStealCardEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		return await ResolveQueenEffectAsync(
			request.Event.ActorPlayerIndex,
			request.Event.TargetPlayerIndex,
			request.Event.TargetHandIndex,
			turnSequenceGeneration);
	}

	private async Task<PlayCardResult> ResolveStealDeckCardsEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		int ownerPlayerIndex = request.Event.ActorPlayerIndex;
		int targetPlayerIndex = request.Event.TargetPlayerIndex;
		int cardsToMoveLimit = Mathf.Max(1, request.Rule.EffectValues.GetInt(CardEffectValueKeys.Count, 3));
		return await ResolveKingEffectAsync(ownerPlayerIndex, targetPlayerIndex, cardsToMoveLimit, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> ResolveDoubleCollectEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		if (request.Event.SourceTableCardIndex >= 0)
		{
			await ResolveDoubleTableCardTriggerAsync(request.Event.SourceTableCardIndex, turnSequenceGeneration, null);
		}

		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task<PlayCardResult> ResolveExtraTurnEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		if (request.Event.SourceTableCardIndex >= 0)
		{
			await ResolveOvertimeTableCardTriggerAsync(request.Event.SourceTableCardIndex, turnSequenceGeneration, null);
		}

		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private PlayCardResult ResolveGrantRewardEffect(CardTriggerRequest request)
	{
		// Compatibility marker for Question Mark; the actual reward is granted in table capture flow.
		return new PlayCardResult(true, false);
	}

	private async Task<PlayCardResult> ResolveDestroyCardsEffectAsync(CardTriggerRequest request, int turnSequenceGeneration)
	{
		await ResolveBombPairAsync(turnSequenceGeneration);
		return new PlayCardResult(true, false, CompletesTurn: false);
	}
}
