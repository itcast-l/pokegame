using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayCardFromHandAsync(int playerIndex, int handIndex, int turnSequenceGeneration, Vector2? startGlobalPositionOverride = null)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return new PlayCardResult(false, false);
		}

		PlayingCard playedCard = hand[handIndex];
		bool narwhalDoubleAbilityTriggered = ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, playedCard);
		if (narwhalDoubleAbilityTriggered)
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.TuskBreak);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new PlayCardResult(false, false);
			}
		}

		playedCard = PrepareAdventureEnemyPlayedCard(playerIndex, playedCard);
		CardPlayPlan cardPlayPlan = ResolveCardPlayPlan(playedCard);
		bool playedRankCollectsTable = cardPlayPlan.PlayedCardCollectsTable;
		Control handArea = _playerHandAreas[playerIndex];
		Vector2 cardOffset = GetHandCardOffset(playerIndex, hand.Count, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, hand.Count, handArea.Size, cardOffset);
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		Vector2 startGlobalPosition = startGlobalPositionOverride
			?? sourceCardVisual?.GlobalPosition
			?? handArea.GlobalPosition + firstCardPosition + handIndex * cardOffset;
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		hand.RemoveAt(handIndex);
		ShiftHandDisplayStateAfterRemovedCard(playerIndex, handIndex);
		int captureStartIndex = AddPlayedCardToTable(playerIndex, playedCard, cardPlayPlan, out bool matchedTableCardFlipped);
		int playedTableCardIndex = _tableCards.Count - 1;

		if (playedRankCollectsTable)
		{
			captureStartIndex = 0;
			_tableCards[playedTableCardIndex].IsFaceUp = true;
		}

		bool playedTableCardIsFaceUp = _tableCards[playedTableCardIndex].IsFaceUp;

		await PlayCardToTableAnimationAsync(
			playerIndex,
			playedCard,
			startGlobalPosition,
			playedTableCardIndex,
			playedTableCardIsFaceUp);

		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		RenderTableCards();

		CardPlayBranchResult branchResult;
		if (captureStartIndex >= 0)
		{
			branchResult = await ResolvePlayedCardCaptureAsync(
				playerIndex,
				playedCard,
				captureStartIndex,
				playedTableCardIndex,
				matchedTableCardFlipped,
				playedRankCollectsTable,
				turnSequenceGeneration);
		}
		else
		{
			branchResult = await ResolveMissedPlayedCardAsync(playerIndex, playedCard, playedTableCardIndex, turnSequenceGeneration);
		}

		if (branchResult.ShouldReturn)
		{
			return branchResult.Result;
		}

		CheckForWinner();
		bool captured = captureStartIndex >= 0;
		return new PlayCardResult(true, captured, captured, CompletesTurn: !branchResult.OvertimeTriggered);
	}

	private async Task<CardPlayBranchResult> ResolvePlayedCardCaptureAsync(
		int playerIndex,
		PlayingCard playedCard,
		int captureStartIndex,
		int playedTableCardIndex,
		bool matchedTableCardFlipped,
		bool playedRankCollectsTable,
		int turnSequenceGeneration)
	{
		PlayGameSfx(GameSfx.CaptureBite, -1.0f, 0.97f, 1.03f);
		ClearCardPlayTriggerScratchSets();
		HashSet<int> pendingFaceUpMagnetTriggerIndexes = _cardPlayPendingFaceUpMagnetTriggerIndexes;
		HashSet<int> resolvedMagnetTriggerIndexes = _cardPlayResolvedMagnetTriggerIndexes;
		HashSet<int> magnetExtraCaptureIndexes = _cardPlayMagnetExtraCaptureIndexes;
		HashSet<int> resolvedDoubleTriggerIndexes = _cardPlayResolvedDoubleTriggerIndexes;
		HashSet<int> resolvedOvertimeTriggerIndexes = _cardPlayResolvedOvertimeTriggerIndexes;
		TableCard matchedTableCard = _tableCards[captureStartIndex];
		if (matchedTableCardFlipped && matchedTableCard.Card.HasValue && IsAttachedEffectType(matchedTableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
		{
			pendingFaceUpMagnetTriggerIndexes.Add(captureStartIndex);
		}

		if (IsAttachedEffectType(playedCard, CardEffectTypes.MagnetExtraCapture))
		{
			pendingFaceUpMagnetTriggerIndexes.Add(playedTableCardIndex);
		}

		PairCaptureEdgeFloatHold? pairCaptureEdgeFloat = null;
		bool pairCaptureEdgeFloatHandedToDeck = false;
		try
		{
			int scorePlayerIndex = GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex);
			bool capturedMiddleCards = HasCapturedMiddleTableCards(captureStartIndex);
			if (ShouldPlayPairCaptureEdgeFloat(captureStartIndex, playedTableCardIndex, playedRankCollectsTable))
			{
				pairCaptureEdgeFloat = await PlayPairCaptureEdgeFloatHoldAnimationAsync(captureStartIndex, playedTableCardIndex);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			await RevealCapturedBaseCardsFromBottomAsync(
				captureStartIndex,
				scorePlayerIndex,
				turnSequenceGeneration,
				pendingFaceUpMagnetTriggerIndexes,
				resolvedMagnetTriggerIndexes,
				magnetExtraCaptureIndexes,
				resolvedDoubleTriggerIndexes,
				resolvedOvertimeTriggerIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			if (capturedMiddleCards)
			{
				List<TableSpecialCardTrigger> specialTriggers = _capturedSpecialCardTriggers;
				BuildCapturedSpecialCardTriggers(captureStartIndex, magnetExtraCaptureIndexes, specialTriggers);
				try
				{
					for (int triggerIndex = 0; triggerIndex < specialTriggers.Count; triggerIndex++)
					{
						TableSpecialCardTrigger specialTrigger = specialTriggers[triggerIndex];
						await PlayTableSpecialCardTriggerEffectAsync(specialTrigger);
						if (turnSequenceGeneration != _turnSequenceGeneration)
						{
							return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
						}

						await PublishAndResolveCardEventAsync(
							new CardGameEvent(
								CardGameEvents.OnCollect,
								playerIndex,
								sourceTableCardIndex: specialTrigger.TableCardIndex,
								captureStartIndex: captureStartIndex,
								extraTableCardIndexes: magnetExtraCaptureIndexes,
								originGlobalCenter: specialTrigger.GlobalPosition + CardVisualSize / 2.0f),
							turnSequenceGeneration);
						if (turnSequenceGeneration != _turnSequenceGeneration)
						{
							return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
						}
					}
				}
				finally
				{
					specialTriggers.Clear();
				}
			}

			List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
				captureStartIndex,
				false,
				false,
				magnetExtraCaptureIndexes,
				pairCaptureEdgeFloat);
			bool gardenEelSandBiteTriggered = capturedMiddleCards
				&& ShouldApplyGardenEelSandBite(playerIndex, captureStartIndex, playedRankCollectsTable);
			if (gardenEelSandBiteTriggered)
			{
				await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.SandAmbush);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
			int gardenEelSandBiteBonusPoints = gardenEelSandBiteTriggered
				? ConsumeGardenEelSandBiteBonusPoints(playerIndex, captureStartIndex, scorePlayerIndex, hasDoubleEffect, playedRankCollectsTable)
				: 0;
			if (gardenEelSandBiteBonusPoints > 0)
			{
				_ = PlayTableCardPointPopupAsync(captureStartIndex, gardenEelSandBiteBonusPoints, hasDoubleEffect);
			}

			int collectedPoints = GetCapturedTablePoints(captureStartIndex, scorePlayerIndex, magnetExtraCaptureIndexes)
				+ gardenEelSandBiteBonusPoints;
			if (capturedMiddleCards && ShouldPlayCrabScoreReductionAbility(scorePlayerIndex, captureStartIndex, magnetExtraCaptureIndexes))
			{
				await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(1, turnSequenceGeneration, AdventureEnemyAbilities.TideGuard);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			if (collectedPoints > 0)
			{
				List<CapturedTableCardVisual> scoringCardVisuals = BuildCapturedTableCardVisuals(
					captureStartIndex,
					false,
					true,
					magnetExtraCaptureIndexes,
					includeCaptureStartScoring: gardenEelSandBiteBonusPoints > 0,
					scorePlayerIndex: scorePlayerIndex);
				await PlayScoreFillCollectionAsync(
					scorePlayerIndex,
					scoringCardVisuals,
					turnSequenceGeneration,
					() => AddPlayerScore(scorePlayerIndex, collectedPoints));
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			float captureFlightStaggerSeconds = pairCaptureEdgeFloat != null ? 0.0f : TableCaptureFlightStaggerSeconds;
			pairCaptureEdgeFloat?.QueueFreeOverlays(_animationPresenter);
			pairCaptureEdgeFloatHandedToDeck = pairCaptureEdgeFloat != null;
			await PlayCapturedCardsToDeckAnimationAsync(
				playerIndex,
				captureStartIndex,
				capturedCardVisuals,
				turnSequenceGeneration,
				magnetExtraCaptureIndexes,
				captureFlightStaggerSeconds);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			CaptureTableCardsResult captureResult = await CaptureTableCardsAsync(
				playerIndex,
				captureStartIndex,
				true,
				false,
				turnSequenceGeneration,
				magnetExtraCaptureIndexes,
				suppressGardenEelCaptureRecord: gardenEelSandBiteBonusPoints > 0);
			int collectedCardCount = captureResult.CollectedCardCount;
			if (IsGameOver())
			{
				return new CardPlayBranchResult(true, new PlayCardResult(true, true, collectedCardCount > 0), false);
			}

			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);
			RenderTableCards();
			if (captureResult.QuestionRewardAnimation.HasValue && !captureResult.QuestionRewardAnimation.Value.RequiresMagnetAttachment)
			{
				HideHandDisplaySlot(playerIndex, _playerHands[playerIndex].Count + captureResult.QuestionRewardAnimation.Value.ItemHandIndex);
			}

			await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			await PlayQuestionCardRewardAnimationAsync(playerIndex, captureResult.QuestionRewardAnimation, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			await ResolvePendingMagnetAttachmentsAsync(turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			RenderPlayerHands();
			if (collectedCardCount > 0)
			{
				await PlayPhysicalDeckShuffleAsync(playerIndex);
			}

			return new CardPlayBranchResult(false, default, resolvedOvertimeTriggerIndexes.Count > 0);
		}
		finally
		{
			ClearCardPlayTriggerScratchSets();
			if (!pairCaptureEdgeFloatHandedToDeck && pairCaptureEdgeFloat != null)
			{
				RestorePairCaptureEdgeFloatHold(pairCaptureEdgeFloat);
			}
		}
	}

	private async Task<CardPlayBranchResult> ResolveMissedPlayedCardAsync(
		int playerIndex,
		PlayingCard playedCard,
		int playedTableCardIndex,
		int turnSequenceGeneration)
	{
		bool overtimeTriggered = false;
		if (IsAttachedEffectType(playedCard, CardEffectTypes.ExtraTurn))
		{
			HashSet<int> resolvedOvertimeTriggerIndexes = _cardPlayResolvedOvertimeTriggerIndexes;
			resolvedOvertimeTriggerIndexes.Clear();
			try
			{
				await ResolveOvertimeTableCardTriggerAsync(playedTableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}

				overtimeTriggered = resolvedOvertimeTriggerIndexes.Count > 0;
			}
			finally
			{
				resolvedOvertimeTriggerIndexes.Clear();
			}
		}

		await ApplySeaSlugSlimeCoatAfterMissAsync(playerIndex, playedTableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}

		DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);
		RenderTableCards();
		await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}

		return new CardPlayBranchResult(false, default, overtimeTriggered);
	}

	private readonly record struct CardPlayBranchResult(bool ShouldReturn, PlayCardResult Result, bool OvertimeTriggered);

	private void ClearCardPlayTriggerScratchSets()
	{
		_cardPlayPendingFaceUpMagnetTriggerIndexes.Clear();
		_cardPlayResolvedMagnetTriggerIndexes.Clear();
		_cardPlayMagnetExtraCaptureIndexes.Clear();
		_cardPlayResolvedDoubleTriggerIndexes.Clear();
		_cardPlayResolvedOvertimeTriggerIndexes.Clear();
	}

	private int AddPlayedCardToTable(int playerIndex, PlayingCard playedCard, CardPlayPlan plan, out bool matchedTableCardFlipped)
	{
		bool isFaceUp = plan.MatchingTableCardIndex >= 0;
		matchedTableCardFlipped = plan.MatchedTableCardFlipped;
		if (isFaceUp)
		{
			_tableCards[plan.MatchingTableCardIndex].IsFaceUp = true;
		}

		_tableCards.Add(new TableCard(playedCard, isFaceUp, cardOwnerPlayerIndex: playerIndex));
		return plan.CaptureStartIndex;
	}

	private int FindMatchingTableCardIndex(PlayingCard playedCard)
	{
		return CardPlayResolver.FindMatchingTableCardIndex(ToCardPlayTableCards(), playedCard.Rank);
	}

	private bool ShouldPlayedCardCollectTable(PlayingCard playedCard)
	{
		return !item_mode && CardRankCatalog.ShouldCollectTableWhenItemModeDisabled(playedCard.Rank);
	}

	private CardPlayPlan ResolveCardPlayPlan(PlayingCard playedCard)
	{
		return CardPlayResolver.Resolve(
			ToCardPlayTableCards(),
			new CardPlayCard(playedCard.Rank, ShouldPlayedCardCollectTable(playedCard)));
	}

	private CardPlayTableCard[] ToCardPlayTableCards()
	{
		var tableCards = new CardPlayTableCard[_tableCards.Count];
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			tableCards[tableCardIndex] = new CardPlayTableCard(
				tableCard.Card?.Rank,
				tableCard.IsFaceUp,
				tableCard.IsBuriedInSand);
		}

		return tableCards;
	}
}
