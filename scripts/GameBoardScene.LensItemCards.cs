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

		if (!_itemCardCatalog.TryGetDefinition(itemHand[itemHandIndex].Id, out ItemCardDefinition definition))
		{
			return new PlayCardResult(false, false);
		}

		if (!HasLensRevealTargets(playerIndex, definition.EffectValues))
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
		ShiftHandDisplayStateAfterRemovedCard(playerIndex, displayIndex);
		MarkNetworkAuthorityPrimaryMutationApplied();
		RenderPlayerHands();

		PlaySpecialItemCardPlaySfx();
		await PlayLensCardSplitAnimationAsync(
			lensCard.Texture,
			startGlobalPosition,
			splitGlobalCenter,
			GetCardRotationDegreesForPlayer(playerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = await ResolveItemCardOnPlayAsync(
			lensCard,
			playerIndex,
			itemHandIndex,
			-1,
			-1,
			splitGlobalCenter,
			turnSequenceGeneration);
		return result with { CompletesTurn = false };
	}

	private bool HasLensRevealTargets(int ownerPlayerIndex, CardEffectValues effectValues)
	{
		string target = effectValues.GetString(CardEffectValueKeys.Target, CardEffectTargets.OpponentTableFaceDown);
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (IsLensRevealTargetCandidate(_tableCards[tableCardIndex], target, ownerPlayerIndex))
			{
				return true;
			}
		}

		return false;
	}

	private List<int> PickLensRevealTargetIndexes(int ownerPlayerIndex, CardEffectValues effectValues)
	{
		string target = effectValues.GetString(CardEffectValueKeys.Target, CardEffectTargets.OpponentTableFaceDown);
		int targetLimit = Mathf.Max(1, effectValues.GetInt(CardEffectValueKeys.Count, LensRevealMaxCards));
		var targets = new List<int>(Math.Min(targetLimit, _tableCards.Count));
		int candidateCount = 0;
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!IsLensRevealTargetCandidate(tableCard, target, ownerPlayerIndex))
			{
				continue;
			}

			candidateCount++;
			if (targets.Count < targetLimit)
			{
				targets.Add(tableCardIndex);
				continue;
			}

			int replacementIndex = _rng.RandiRange(0, candidateCount - 1);
			if (replacementIndex < targetLimit)
			{
				targets[replacementIndex] = tableCardIndex;
			}
		}

		ShuffleLensRevealTargetCandidates(targets);
		return targets;
	}

	private void ShuffleLensRevealTargetCandidates(List<int> candidates)
	{
		for (int index = candidates.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(candidates[index], candidates[swapIndex]) = (candidates[swapIndex], candidates[index]);
		}
	}

	private static bool IsLensRevealTargetCandidate(TableCard tableCard, string target, int ownerPlayerIndex)
	{
		if (tableCard.IsFaceUp || !tableCard.Card.HasValue)
		{
			return false;
		}

		return target != CardEffectTargets.OpponentTableFaceDown
			|| tableCard.CardOwnerPlayerIndex != ownerPlayerIndex;
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

		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastLensRevealVisualEventFromHost(
				_currentTurnPlayerIndex,
				splitGlobalCenter,
				targetIndexes,
				turnSequenceGeneration,
				snapshotId);
		}

		Task[] wispTasks = new Task[targetIndexes.Count];
		int wispTaskCount = 0;
		for (int targetNumber = 0; targetNumber < targetIndexes.Count; targetNumber++)
		{
			int tableCardIndex = targetIndexes[targetNumber];
			Vector2? targetGlobalCenter = GetTableCardGlobalCenter(tableCardIndex);
			if (!targetGlobalCenter.HasValue)
			{
				continue;
			}

			wispTasks[wispTaskCount++] = PlayLensRevealWispFlightAsync(splitGlobalCenter, targetGlobalCenter.Value, targetNumber, tableCardIndex);
		}

		if (wispTaskCount > 0)
		{
			if (wispTaskCount == wispTasks.Length)
			{
				await Task.WhenAll(wispTasks);
			}
			else if (wispTaskCount == 1)
			{
				await wispTasks[0];
			}
			else
			{
				Task[] activeWispTasks = new Task[wispTaskCount];
				Array.Copy(wispTasks, activeWispTasks, wispTaskCount);
				await Task.WhenAll(activeWispTasks);
			}

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

		PlayGameSfx(GameSfx.LensReveal, -1.5f, 0.97f, 1.04f, 0.06);
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
