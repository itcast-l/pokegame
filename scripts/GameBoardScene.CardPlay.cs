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
		int captureStartIndex = AddPlayedCardToTable(playerIndex, playedCard, out bool matchedTableCardFlipped);
		int playedTableCardIndex = _tableCards.Count - 1;
		bool playedTableCardIsFaceUp = _tableCards[playedTableCardIndex].IsFaceUp;
		var pendingFaceUpMagnetTriggerIndexes = new HashSet<int>();
		var resolvedMagnetTriggerIndexes = new HashSet<int>();
		var magnetExtraCaptureIndexes = new HashSet<int>();
		var resolvedDoubleTriggerIndexes = new HashSet<int>();
		var resolvedOvertimeTriggerIndexes = new HashSet<int>();
		if (captureStartIndex >= 0)
		{
			TableCard matchedTableCard = _tableCards[captureStartIndex];
			if (matchedTableCardFlipped && matchedTableCard.Card.HasValue && matchedTableCard.Card.Value.HasMagnetEffect)
			{
				pendingFaceUpMagnetTriggerIndexes.Add(captureStartIndex);
			}

			if (playedCard.HasMagnetEffect)
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
			await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			await RevealCapturedBaseCardsFromBottomAsync(
				captureStartIndex,
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

			TableSpecialCardTrigger? specialTrigger = GetCapturedSpecialCardTrigger(captureStartIndex);
			if (specialTrigger.HasValue)
			{
				await PlayTableSpecialCardTriggerEffectAsync(specialTrigger.Value);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new PlayCardResult(false, false);
				}
			}

			List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(captureStartIndex, false, false, magnetExtraCaptureIndexes);
			int collectedPoints = GetCapturedTablePoints(captureStartIndex, magnetExtraCaptureIndexes);
			int scorePlayerIndex = GetCapturedTableScorePlayerIndex(captureStartIndex, playerIndex);
			if (collectedPoints > 0)
			{
				List<CapturedTableCardVisual> scoringCardVisuals = BuildCapturedTableCardVisuals(captureStartIndex, false, true, magnetExtraCaptureIndexes);
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

			await PlayCapturedCardsToDeckAnimationAsync(playerIndex, captureStartIndex, capturedCardVisuals, turnSequenceGeneration, magnetExtraCaptureIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			int collectedCardCount = CaptureTableCards(
				playerIndex,
				captureStartIndex,
				true,
				false,
				out QuestionCardRewardAnimation? questionRewardAnimation,
				magnetExtraCaptureIndexes);
			if (IsGameOver())
			{
				return new PlayCardResult(true, true, collectedCardCount > 0);
			}

			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderTableCards();
			await PlayQuestionCardRewardAnimationAsync(playerIndex, questionRewardAnimation, turnSequenceGeneration);
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
		else
		{
			if (playedCard.HasOvertimeEffect)
			{
				await ResolveOvertimeTableCardTriggerAsync(playedTableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new PlayCardResult(false, false);
				}
			}

			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
		}

		CheckForWinner();
		return new PlayCardResult(true, captureStartIndex >= 0, captureStartIndex >= 0, CompletesTurn: resolvedOvertimeTriggerIndexes.Count == 0);
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
			if (tableCard.HasValue && tableCard.Value.Rank == playedCard.Rank)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}
}
