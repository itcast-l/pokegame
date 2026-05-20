using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayLensCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
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

		List<int> targetIndexes = PickLensRevealTargetIndexes(playerIndex);
		if (targetIndexes.Count == 0)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard lensCard = itemHand[itemHandIndex];
		int displayCardCount = _playerHands[playerIndex].Count + itemHand.Count;
		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Control handArea = _playerHandAreas[playerIndex];
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		Vector2 startGlobalPosition = sourceCardVisual?.GlobalPosition
			?? handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		Vector2 splitGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		Vector2 splitGlobalCenter = splitGlobalPosition + CardVisualSize / 2.0f;
		itemHand.RemoveAt(itemHandIndex);
		RenderPlayerHands();

		await PlayLensCardSplitAnimationAsync(
			lensCard.Texture,
			startGlobalPosition,
			splitGlobalCenter,
			GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		await RevealLensTargetCardsAsync(targetIndexes, splitGlobalCenter, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderTableCards();
		CheckForWinner();
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private bool HasLensRevealTargets(int ownerPlayerIndex)
	{
		return BuildLensRevealTargetCandidates(ownerPlayerIndex).Count > 0;
	}

	private List<int> PickLensRevealTargetIndexes(int ownerPlayerIndex)
	{
		List<int> candidates = BuildLensRevealTargetCandidates(ownerPlayerIndex);
		ShuffleLensRevealTargetCandidates(candidates);
		int targetCount = Math.Min(LensRevealMaxCards, candidates.Count);
		return targetCount == candidates.Count ? candidates : candidates.GetRange(0, targetCount);
	}

	private void ShuffleLensRevealTargetCandidates(List<int> candidates)
	{
		for (int index = candidates.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(candidates[index], candidates[swapIndex]) = (candidates[swapIndex], candidates[index]);
		}
	}

	private List<int> BuildLensRevealTargetCandidates(int ownerPlayerIndex)
	{
		var candidates = new List<int>();
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsFaceUp || !tableCard.Card.HasValue)
			{
				continue;
			}

			if (tableCard.CardOwnerPlayerIndex == ownerPlayerIndex)
			{
				continue;
			}

			candidates.Add(tableCardIndex);
		}

		return candidates;
	}

	private async Task RevealLensTargetCardsAsync(
		IReadOnlyList<int> targetIndexes,
		Vector2 splitGlobalCenter,
		int turnSequenceGeneration)
	{
		if (targetIndexes.Count == 0)
		{
			return;
		}

		var wispTasks = new List<Task>();
		for (int targetNumber = 0; targetNumber < targetIndexes.Count; targetNumber++)
		{
			int tableCardIndex = targetIndexes[targetNumber];
			Vector2? targetGlobalCenter = GetTableCardGlobalCenter(tableCardIndex);
			if (!targetGlobalCenter.HasValue)
			{
				continue;
			}

			wispTasks.Add(PlayLensRevealWispFlightAsync(splitGlobalCenter, targetGlobalCenter.Value, targetNumber, tableCardIndex));
		}

		if (wispTasks.Count > 0)
		{
			await Task.WhenAll(wispTasks);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		foreach (int tableCardIndex in targetIndexes)
		{
			await RevealSingleLensTargetCardAsync(tableCardIndex, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
	}

	private Vector2 GetLensRevealFallbackGlobalCenter(int ownerPlayerIndex)
	{
		if (ownerPlayerIndex >= 0 && ownerPlayerIndex < _playerHandAreas.Count)
		{
			return _playerHandAreas[ownerPlayerIndex].GlobalPosition + _playerHandAreas[ownerPlayerIndex].Size / 2.0f;
		}

		return GlobalPosition + Size / 2.0f;
	}

	private async Task RevealSingleLensTargetCardAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (tableCard.IsFaceUp || !tableCard.Card.HasValue)
		{
			return;
		}

		await PlayTableCardRevealAnimationAsync(tableCardIndex, tableCard.Card.Value.Texture);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		tableCard.IsFaceUp = true;
		RenderTableCards();
		_ = PlayTableCardPointPopupAsync(tableCardIndex, GetCardScoreValue(tableCard.Card.Value));
		await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
	}
}
