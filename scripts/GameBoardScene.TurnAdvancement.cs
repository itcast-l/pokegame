using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private async Task<bool> SetCurrentTurnPlayerIndexAsync(
		int nextTurnPlayerIndex,
		int? previousTurnPlayerIndexOverride = null,
		bool captured = false,
		int? actingPlayerIndexOverride = null,
		bool advanceQuestionRefresh = false,
		Action<RuleCommandResult>? onApplied = null)
	{
		int previousTurnPlayerIndex = previousTurnPlayerIndexOverride
			?? _currentTurnPlayerIndex;
		if (previousTurnPlayerIndex == MainPlayerIndex
			&& nextTurnPlayerIndex != MainPlayerIndex
			&& !await EndRealMainPlayerTurnAsync())
		{
			return false;
		}
		int actingPlayerIndex = actingPlayerIndexOverride ?? previousTurnPlayerIndex;
		if (!TrySubmitAdvanceTurnRuleCommand(
			actingPlayerIndex,
			captured,
			nextTurnPlayerIndex,
			advanceQuestionRefresh,
			HasTableSpecialCard(),
			out RuleCommandResult result))
		{
			return false;
		}
		onApplied?.Invoke(result);
		if (result.ReadModel.CurrentTurnSeat != nextTurnPlayerIndex)
		{
			GD.PushError(
				$"Turn rule command selected seat {result.ReadModel.CurrentTurnSeat}, "
				+ $"but the scene expected {nextTurnPlayerIndex}.");
			return false;
		}
		if (nextTurnPlayerIndex == MainPlayerIndex && previousTurnPlayerIndex != MainPlayerIndex)
		{
			TryUpdateMatchFlowState(state => state with
			{
				MainPlayerRealTurnActive = false,
				MainPlayerRealTurnStartPending = true,
			});
		}
		return true;
	}

	private async Task BeginRealMainPlayerTurnIfNeededAsync(int turnSequenceGeneration)
	{
		if (_currentTurnPlayerIndex != MainPlayerIndex
			|| CurrentMatchFlowState.MainPlayerRealTurnActive)
		{
			return;
		}

		if (!ShouldUseAdventureUpgradeSystem())
		{
			TryUpdateMatchFlowState(state => state with
			{
				MainPlayerRealTurnActive = true,
				MainPlayerRealTurnStartPending = false,
			});
			return;
		}

		ExtraActionQueueSnapshot extraActions = GetExtraActionReadModel();
		if (extraActions.ActiveRootChainId > 0
			&& extraActions.RootResumesNormalTurn)
		{
			TryUpdateMatchFlowState(state => state with
			{
				MainPlayerRealTurnActive = false,
				MainPlayerRealTurnStartPending = true,
			});
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			MainPlayerRealTurnActive = true,
			MainPlayerRealTurnStartPending = false,
		});
		if (!TrySubmitBeginRealTurnRuleCommand(
			MainPlayerIndex,
			turnSequenceGeneration))
		{
			TryUpdateMatchFlowState(state => state with
			{
				MainPlayerRealTurnActive = false,
				MainPlayerRealTurnStartPending = true,
			});
			return;
		}
		TryUpdateMatchFlowState(state => state with
		{
			SuccessfulCapturesThisTurn = 0,
		});
		RefreshAdventureHuntOrderMarkForMainPlayerTurn();
		await ApplyAdventureMainPlayerTurnStartEffectsAsync(
			MainPlayerIndex,
			turnSequenceGeneration);
	}

	private async Task<bool> EndRealMainPlayerTurnAsync()
	{
		if (!CurrentMatchFlowState.MainPlayerRealTurnActive
			&& !CurrentMatchFlowState.MainPlayerRealTurnStartPending)
		{
			return true;
		}

		bool commitMissBandage = GetExtraActionReadModel()
			.HasPendingRealTurnEndMarker(
				AdventureMissBandageRealTurnEndMarkerId);
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (ShouldUseAdventureUpgradeSystem() && store?.HasActiveRun == true)
		{
			AdventureSaveGateResult<AdventureRunWriteResult> gate =
				await ExecuteAdventureSaveGateAsync(
					() => store.ResolveRealTurnEnd(commitMissBandage),
					result => result == AdventureRunWriteResult.Succeeded,
					result => result == AdventureRunWriteResult.SaveFailed);
			if (!gate.Succeeded)
			{
				GD.PushWarning($"Failed to commit adventure real turn end: {gate.Result}");
				return false;
			}
		}

		if (ShouldUseAdventureUpgradeSystem()
			&& !TrySubmitEndRealTurnRuleCommand(
				MainPlayerIndex,
				_turnSequenceGeneration,
				out ExtraActionCommandOutcome endOutcome))
		{
			return false;
		}
		TryUpdateMatchFlowState(state => state with
		{
			MainPlayerRealTurnActive = false,
			MainPlayerRealTurnStartPending = false,
			SuccessfulCapturesThisTurn = 0,
			TurnCaptureMarkRuntimeId = 0,
		});
		if (store?.HasActiveRun == true)
		{
			MarkActivePlayerStatusTooltipForRefresh();
		}
		return true;
	}

	private async Task ResolveActionCompletionAsync(int playerIndex, PlayCardResult result)
	{
		int turnSequenceGeneration = _turnSequenceGeneration;
		await CheckForWinnerAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		bool shouldAdvance = result.CompletesTurn || !PlayerHasPlayableHandCards(playerIndex);
		if (!shouldAdvance)
		{
			return;
		}

		PlayCardResult completedResult = result.CompletesTurn
			? result
			: result with { Captured = false, CompletesTurn = true };
		ClearJellyfishBubbleIfMainPlayerTurnCompleted(playerIndex, completedResult);

		int previousTurnPlayerIndex = _currentTurnPlayerIndex;
		int candidateNextPlayerIndex = playerIndex;
		if (!completedResult.Captured
			&& !TryGetNextTurnPlayerIndex(playerIndex, out candidateNextPlayerIndex))
		{
			await CheckForWinnerAsync(turnSequenceGeneration);
			if (!IsGameOver())
			{
				SettleStalledMatch();
			}
			return;
		}

		int nextTurnPlayerIndex = completedResult.Captured
			? playerIndex
			: candidateNextPlayerIndex;
			RuleCommandResult? turnAdvanceResult = null;
			if (!await SetCurrentTurnPlayerIndexAsync(
					nextTurnPlayerIndex,
					previousTurnPlayerIndex,
					completedResult.Captured,
					playerIndex,
					advanceQuestionRefresh: !completedResult.Captured,
					onApplied: result => turnAdvanceResult = result))
		{
			return;
		}
		PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
		await ResolveAdventureEnemyTurnEndAbilityAsync(playerIndex, completedResult, _turnSequenceGeneration);
		if (IsGameOver())
		{
			return;
		}

		ResetMantaRayTideShiftTurnStateIfNeeded(playerIndex);
		ResetJellyfishBubbleTurnStateIfNeeded(playerIndex);
		if (!completedResult.Captured
			&& turnAdvanceResult?.Mutation.ShouldRefreshQuestionCard == true)
		{
			int refreshedQuestionCardIndex = PlaceQuestionCardOnNextTableSlot();
			if (refreshedQuestionCardIndex >= 0 && refreshedQuestionCardIndex < _tableCards.Count)
			{
				if (_networkAuthorityRuleTransactionActive)
				{
					await PlayQuestionCardRefreshAnimationAsync(refreshedQuestionCardIndex);
					}
					else
					{
						long refreshedRuntimeInstanceId =
							_tableCards[refreshedQuestionCardIndex].InstanceId;
						try
						{
							_presentationHiddenTableCardRuntimeInstanceIds.Add(
								refreshedRuntimeInstanceId);
							RenderTableCards();
						}
						finally
						{
							_presentationHiddenTableCardRuntimeInstanceIds.Remove(
								refreshedRuntimeInstanceId);
						}
						await PlayQuestionCardRefreshAnimationAsync(refreshedQuestionCardIndex);
						RenderTableCards();
				}
			}
		}

		await RefillCurrentTurnPlayerBaseHandAsync(_turnSequenceGeneration);
		if (!IsGameOver() && !PlayerHasPlayableHandCards(_currentTurnPlayerIndex))
		{
			await ResolveActionCompletionAsync(
				_currentTurnPlayerIndex,
				new PlayCardResult(false, false, CompletesTurn: false));
		}
		if (IsGameOver())
		{
			return;
		}
		BeginNetworkAiProxyTurnIfNeeded();
	}

	private long GetTurnLoopProgressSignature()
	{
		var hash = new HashCode();
		hash.Add(_currentTurnPlayerIndex);
		hash.Add(_matchOutcomeKind);
		hash.Add(_pendingItemSelectionMode);
		hash.Add(_pendingItemOwnerIndex);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			hash.Add(_playerScores[playerIndex]);
			foreach (PlayingCard card in _playerDecks[playerIndex]) hash.Add(card.InstanceId);
			foreach (PlayingCard card in _playerHands[playerIndex]) hash.Add(card.InstanceId);
			foreach (ItemCard card in _playerItemHands[playerIndex]) hash.Add(card.Id);
		}
		foreach (TableCard card in _tableCards)
		{
			hash.Add(card.InstanceId);
			hash.Add(card.IsFaceUp);
			hash.Add(card.IsBuriedInSand);
			hash.Add(card.IsSlimeCoated);
		}
		return hash.ToHashCode();
	}

	private void EnsureTurnLoopMadeProgress(
		long previousSignature,
		int previousPlayerIndex,
		int turnSequenceGeneration)
	{
		if (turnSequenceGeneration == _turnSequenceGeneration
			&& !_networkMigrationInputLocked
			&& !IsGameOver()
			&& previousPlayerIndex == _currentTurnPlayerIndex
			&& previousSignature == GetTurnLoopProgressSignature())
		{
			SettleStalledMatch();
		}
	}

	private async Task RefillCurrentTurnPlayerBaseHandAsync(int turnSequenceGeneration)
	{
		await RefillPlayerBaseHandAsync(_currentTurnPlayerIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await BeginRealMainPlayerTurnIfNeededAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await TryResolveAdventurePendingExtraBaseCardActionForAiAsync(turnSequenceGeneration);
	}

	private async Task RefillPlayerBaseHandAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| playerIndex >= _playerDecks.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count)
		{
			return;
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& _playerHands[playerIndex].Count < StartingHandCards
			&& GetTotalHandCardCount(playerIndex) < MaxTotalHandCards
			&& _playerDecks[playerIndex].Count > 0)
		{
			DrawnHandCard? drawnCard = DrawReplacementCard(playerIndex);
			if (!drawnCard.HasValue)
			{
				break;
			}

			await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, turnSequenceGeneration);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
	}

	private bool PlayerHasPlayableHandCards(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return MatchReadModel.HasPlayableAiTurnAction(
			_playerHands[playerIndex].Count,
			CountPlayableItemIndexes(playerIndex));
	}

	private bool HasPlayableItemCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		if (playerIndex == MainPlayerIndex && HasPendingAdventureExtraBaseCardAction())
		{
			return false;
		}

		IReadOnlyList<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				return true;
			}
		}

		return false;
	}

	private int CountPlayableItemIndexes(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return 0;
		}

		int playableCount = 0;
		IReadOnlyList<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (ShouldSkipNetworkAiBlockedItem(playerIndex, itemIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

			if (CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				playableCount++;
			}
		}

		return playableCount;
	}

	private int CountPlayableItemIndexes(int playerIndex, PlayerStateSnapshot player)
	{
		int playableCount = 0;
		for (int itemIndex = 0; itemIndex < player.ItemHand.Length; itemIndex++)
		{
			string itemCardId = player.ItemHand[itemIndex].Id;
			if (ShouldSkipNetworkAiBlockedItem(playerIndex, itemIndex, itemCardId))
			{
				continue;
			}

			if (CanPlayItemCard(playerIndex, itemCardId))
			{
				playableCount++;
			}
		}

		return playableCount;
	}

	private bool TryGetPlayableItemIndexAt(int playerIndex, int playableOrdinal, out int itemHandIndex)
	{
		itemHandIndex = -1;
		if (playableOrdinal < 0 || playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		int currentOrdinal = 0;
		IReadOnlyList<ItemCard> itemHand = _playerItemHands[playerIndex];
		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			if (ShouldSkipNetworkAiBlockedItem(playerIndex, itemIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

			if (!CanPlayItemCard(playerIndex, itemHand[itemIndex].Id))
			{
				continue;
			}

			if (currentOrdinal == playableOrdinal)
			{
				itemHandIndex = itemIndex;
				return true;
			}

			currentOrdinal++;
		}

		return false;
	}

	private bool TryGetPlayableItemIndexAt(
		int playerIndex,
		PlayerStateSnapshot player,
		int playableOrdinal,
		out int itemHandIndex)
	{
		itemHandIndex = -1;
		if (playableOrdinal < 0)
		{
			return false;
		}

		int currentOrdinal = 0;
		for (int itemIndex = 0; itemIndex < player.ItemHand.Length; itemIndex++)
		{
			string itemCardId = player.ItemHand[itemIndex].Id;
			if (ShouldSkipNetworkAiBlockedItem(playerIndex, itemIndex, itemCardId)
				|| !CanPlayItemCard(playerIndex, itemCardId))
			{
				continue;
			}

			if (currentOrdinal == playableOrdinal)
			{
				itemHandIndex = itemIndex;
				return true;
			}

			currentOrdinal++;
		}

		return false;
	}

	private bool ShouldSkipNetworkAiBlockedItem(int playerIndex, int itemHandIndex, string itemCardId)
	{
		return MatchReadModel.ShouldSkipBlockedItem(
			CurrentNetworkAiBlockedItem,
			playerIndex,
			itemCardId);
	}

	private bool CanPlayItemCard(int playerIndex, string itemCardId)
	{
		if ((playerIndex == MainPlayerIndex && HasPendingAdventureExtraBaseCardAction())
			|| !_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			|| !definition.EnabledInGame
			|| !definition.PlayableFromHand)
		{
			return false;
		}

		if (string.Equals(
				definition.EffectType,
				CardEffectTypes.DrawCard,
				StringComparison.Ordinal)
			&& !CanFulfillDrawItemCardEffect(playerIndex, definition))
		{
			return false;
		}

		if (_itemPlayPresentationValidators.TryGetValue(definition.PlayPresentation, out CanPlayItemCardPresentation? presentationValidator)
			&& !presentationValidator(playerIndex, definition))
		{
			return false;
		}

		if (RequiresPlayerTarget(itemCardId)
			&& !HasPlayableTargetForItemCard(itemCardId, playerIndex))
		{
			return false;
		}

		if (definition.EffectValues.GetString(CardEffectValueKeys.Target) == CardEffectTargets.PlayerDeck
			&& definition.EffectType != CardEffectTypes.StealDeckCards
			&& (playerIndex < 0 || playerIndex >= _playerDecks.Count || _playerDecks[playerIndex].Count == 0))
		{
			return false;
		}

		if (IsAttachmentItemCard(itemCardId) && !PlayerHasAttachableMagnetCard(playerIndex, itemCardId))
		{
			return false;
		}

		return true;
	}

	private bool CanFulfillDrawItemCardEffect(
		int playerIndex,
		ItemCardDefinition definition)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerDecks.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		int drawCount = definition.EffectValues.GetInt(
			CardEffectValueKeys.Count,
			1);
		return ItemCardEffectSupport.CanFulfillDrawCardEffect(
			_playerDecks[playerIndex].Count,
			GetTotalHandCardCount(playerIndex),
			MaxTotalHandCards,
			drawCount);
	}

}
