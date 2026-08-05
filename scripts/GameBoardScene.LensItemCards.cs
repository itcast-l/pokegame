using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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

		IReadOnlyList<ItemCard> itemHand = _playerItemHands[playerIndex];
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
		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Vector2 startGlobalPosition;
		Vector2 splitGlobalPosition;
		if (_networkAuthorityRuleTransactionActive)
		{
			startGlobalPosition = GetNetworkAuthorityRuleVisualStartGlobalPosition(Vector2.Zero);
			splitGlobalPosition = startGlobalPosition;
		}
		else
		{
			int displayCardCount = _playerHands[playerIndex].Count + itemHand.Count;
			Control handArea = _playerHandAreas[playerIndex];
			Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
			Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
			Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
			TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
			startGlobalPosition = sourceCardVisual?.GlobalPosition
				?? handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
			if (sourceCardSlot != null)
			{
				sourceCardSlot.Visible = false;
			}

			splitGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(startGlobalPosition);
		}
		Vector2 splitGlobalCenter = splitGlobalPosition + CardVisualSize / 2.0f;
		if (!TrySubmitPlayItemFromHandRuleCommand(
			playerIndex,
			itemHandIndex,
			lensCard.Id,
			turnSequenceGeneration,
			RuleItemPlayDestination.Consume,
			tableIndex: -1,
			tableItemFacesUp: false,
			out _))
		{
			return new PlayCardResult(false, false);
		}
		ShiftHandDisplayStateAfterRemovedCard(playerIndex, displayIndex);
		MarkNetworkAuthorityPrimaryMutationApplied();
		RenderPlayerHands();

		PlaySpecialItemCardPlaySfx();
		await PlayLensCardSplitAnimationAsync(
			GetItemCardTexture(lensCard),
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
		var candidates = ImmutableArray.CreateBuilder<RuleTableCardReference>();
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!IsLensRevealTargetCandidate(tableCard, target, ownerPlayerIndex))
			{
				continue;
			}

			candidates.Add(new RuleTableCardReference(tableCardIndex, tableCard.InstanceId));
		}

		if (!TrySubmitChooseTableCardsRuleCommand(
			ownerPlayerIndex,
			candidates.ToImmutable(),
			targetLimit,
			_turnSequenceGeneration,
			out ImmutableArray<int> selectedIndexes))
		{
			return new List<int>();
		}

		return new List<int>(selectedIndexes);
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
		if (_networkAuthorityRuleTransactionActive)
		{
			var references = ImmutableArray.CreateBuilder<RuleTableCardReference>(targetIndexes.Count);
			foreach (int tableCardIndex in targetIndexes)
			{
				if (tableCardIndex >= 0
					&& tableCardIndex < _tableCards.Count
					&& _tableCards[tableCardIndex].Card.HasValue)
				{
					references.Add(new RuleTableCardReference(
						tableCardIndex,
						_tableCards[tableCardIndex].InstanceId));
				}
			}
			if (references.Count > 0)
			{
					TrySubmitSetTableCardsFaceUpRuleCommand(
						_currentTurnPlayerIndex,
						references.ToImmutable(),
						expectedFaceUp: false,
					faceUp: true,
					turnSequenceGeneration,
					out _);
			}
			return;
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
		await PlayTableCardRevealAnimationAsync(tableCardIndex, GetPlayingCardTexture(tableCard.Card.Value));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		if (_authorityRuleSession != null
			&& !TrySubmitSetTableCardsFaceUpRuleCommand(
				_currentTurnPlayerIndex,
				ImmutableArray.Create(new RuleTableCardReference(tableCardIndex, tableCard.InstanceId)),
				expectedFaceUp: false,
				faceUp: true,
				turnSequenceGeneration,
				out _))
		{
			return;
		}
		if (_authorityRuleSession == null && _replicaRuleSession != null
			&& !TrySetNetworkPresentationTableCardFaceUp(tableCardIndex, true))
		{
			return;
		}
		RenderTableCards();
		TrackBoardTask(PlayTableCardPointPopupAsync(tableCardIndex, GetCardScoreValue(tableCard.Card.Value)), nameof(PlayTableCardPointPopupAsync));
		await WaitForBoardDelayAsync(TableCardRevealPauseSeconds);
	}
}
