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
			await PlayEnemySpecialAbilityAvatarFxAsync(playerIndex, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new PlayCardResult(false, false);
			}
		}

		playedCard = PrepareAdventureEnemyPlayedCard(playerIndex, playedCard);
		bool playedJackCollectsTable = ShouldJackCollectTable(playedCard) && _tableCards.Count > 0;
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
		int captureStartIndex = AddPlayedCardToTable(playerIndex, playedCard, out bool matchedTableCardFlipped);
		int playedTableCardIndex = _tableCards.Count - 1;

		if (playedJackCollectsTable)
		{
			captureStartIndex = 0;
			_tableCards[playedTableCardIndex].IsFaceUp = true;
		}

		bool playedTableCardIsFaceUp = _tableCards[playedTableCardIndex].IsFaceUp;
		var pendingFaceUpMagnetTriggerIndexes = new HashSet<int>();
		var resolvedMagnetTriggerIndexes = new HashSet<int>();
		var magnetExtraCaptureIndexes = new HashSet<int>();
		var resolvedDoubleTriggerIndexes = new HashSet<int>();
		var resolvedOvertimeTriggerIndexes = new HashSet<int>();
		if (captureStartIndex >= 0)
		{
			TableCard matchedTableCard = _tableCards[captureStartIndex];
			if (matchedTableCardFlipped && matchedTableCard.Card.HasValue && IsAttachedModifier(matchedTableCard.Card.Value, CardAttachmentModifiers.Magnet))
			{
				pendingFaceUpMagnetTriggerIndexes.Add(captureStartIndex);
			}

			if (IsAttachedModifier(playedCard, CardAttachmentModifiers.Magnet))
			{
				pendingFaceUpMagnetTriggerIndexes.Add(playedTableCardIndex);
			}
		}

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
		if (captureStartIndex >= 0)
		{
			PairCaptureEdgeFloatHold? pairCaptureEdgeFloat = null;
			bool pairCaptureEdgeFloatHandedToDeck = false;
			try
			{
				int scorePlayerIndex = GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex);
				if (ShouldPlayPairCaptureEdgeFloat(captureStartIndex, playedTableCardIndex, playedJackCollectsTable))
				{
					pairCaptureEdgeFloat = await PlayPairCaptureEdgeFloatHoldAnimationAsync(captureStartIndex, playedTableCardIndex);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return new PlayCardResult(false, false);
					}
				}

				await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new PlayCardResult(false, false);
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
					return new PlayCardResult(false, false);
				}

				foreach (TableSpecialCardTrigger specialTrigger in GetCapturedSpecialCardTriggers(captureStartIndex, magnetExtraCaptureIndexes))
				{
					await PlayTableSpecialCardTriggerEffectAsync(specialTrigger);
					if (turnSequenceGeneration != _turnSequenceGeneration)
					{
						return new PlayCardResult(false, false);
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
						return new PlayCardResult(false, false);
					}
				}

				Dictionary<int, Vector2>? pairFloatFlightStartOverrides = pairCaptureEdgeFloat?.BuildFlightStartPositionOverrides();
				List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
					captureStartIndex,
					false,
					false,
					magnetExtraCaptureIndexes,
					pairFloatFlightStartOverrides);
				bool gardenEelSandBiteTriggered = ShouldApplyGardenEelSandBite(playerIndex, captureStartIndex, playedJackCollectsTable);
				if (gardenEelSandBiteTriggered)
				{
					await PlayEnemySpecialAbilityAvatarFxAsync(playerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return new PlayCardResult(false, false);
					}
				}

				bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
				int gardenEelSandBiteBonusPoints = gardenEelSandBiteTriggered
					? ConsumeGardenEelSandBiteBonusPoints(playerIndex, captureStartIndex, scorePlayerIndex, hasDoubleEffect, playedJackCollectsTable)
					: 0;
				if (gardenEelSandBiteBonusPoints > 0)
				{
					_ = PlayTableCardPointPopupAsync(captureStartIndex, gardenEelSandBiteBonusPoints, hasDoubleEffect);
				}

				int collectedPoints = GetCapturedTablePoints(captureStartIndex, scorePlayerIndex, magnetExtraCaptureIndexes)
					+ gardenEelSandBiteBonusPoints;
				if (ShouldPlayCrabScoreReductionAbility(scorePlayerIndex, captureStartIndex, magnetExtraCaptureIndexes))
				{
					await PlayEnemySpecialAbilityAvatarFxAsync(1, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return new PlayCardResult(false, false);
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
						return new PlayCardResult(false, false);
					}
				}

				float captureFlightStaggerSeconds = pairCaptureEdgeFloat != null ? 0.0f : TableCaptureFlightStaggerSeconds;
				pairCaptureEdgeFloat?.QueueFreeOverlays();
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
					return new PlayCardResult(false, false);
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
					return new PlayCardResult(true, true, collectedCardCount > 0);
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
					return new PlayCardResult(false, false);
				}

				await PlayQuestionCardRewardAnimationAsync(playerIndex, captureResult.QuestionRewardAnimation, turnSequenceGeneration);
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
				if (collectedCardCount > 0)
				{
					await PlayPhysicalDeckShuffleAsync(playerIndex);
				}
			}
			finally
			{
				if (!pairCaptureEdgeFloatHandedToDeck && pairCaptureEdgeFloat != null)
				{
					RestorePairCaptureEdgeFloatHold(pairCaptureEdgeFloat);
				}
			}
		}
		else
		{
			if (IsAttachedModifier(playedCard, CardAttachmentModifiers.Overtime))
			{
				await ResolveOvertimeTableCardTriggerAsync(playedTableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new PlayCardResult(false, false);
				}
			}

			await ApplySeaSlugSlimeCoatAfterMissAsync(playerIndex, playedTableCardIndex, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new PlayCardResult(false, false);
			}

			DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);
			RenderTableCards();
			await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}
		}

		CheckForWinner();
		bool captured = captureStartIndex >= 0;
		return new PlayCardResult(true, captured, captured, CompletesTurn: resolvedOvertimeTriggerIndexes.Count == 0);
	}

	private int AddPlayedCardToTable(int playerIndex, PlayingCard playedCard, out bool matchedTableCardFlipped)
	{
		int matchingIndex = FindMatchingTableCardIndex(playedCard);
		bool isFaceUp = matchingIndex >= 0;
		matchedTableCardFlipped = false;
		if (isFaceUp)
		{
			matchedTableCardFlipped = !_tableCards[matchingIndex].IsFaceUp;
			_tableCards[matchingIndex].IsFaceUp = true;
		}

		_tableCards.Add(new TableCard(playedCard, isFaceUp, cardOwnerPlayerIndex: playerIndex));
		return matchingIndex;
	}

	private int FindMatchingTableCardIndex(PlayingCard playedCard)
	{
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			PlayingCard? tableCard = _tableCards[tableCardIndex].Card;
			if (!_tableCards[tableCardIndex].IsBuriedInSand && tableCard.HasValue && tableCard.Value.Rank == playedCard.Rank)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private bool ShouldJackCollectTable(PlayingCard playedCard)
	{
		return !item_mode && playedCard.Rank == JackItemCardId;
	}
}
