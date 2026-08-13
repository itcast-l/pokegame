using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolveOpponentTurnsAsync(int turnSequenceGeneration)
	{
		if (!MatchReadModel.CanBeginOpponentSequence(BuildGameLoopPhaseState()))
		{
			return;
		}

		_isResolvingTurnSequence = true;
		try
		{
			while (turnSequenceGeneration == _turnSequenceGeneration
				&& _currentTurnPlayerIndex != MainPlayerIndex
				&& !IsGameOver()
				&& !IsNetworkHumanControlledSeat(_currentTurnPlayerIndex))
			{
				int playerIndex = _currentTurnPlayerIndex;
				long previousProgressSignature = GetTurnLoopProgressSignature();
				try
				{
				if (playerIndex >= player_count)
				{
					if (!TryGetNextTurnPlayerIndex(playerIndex, out int nextPlayerIndex))
						{
							SettleStalledMatch();
							return;
						}
					if (!await SetCurrentTurnPlayerIndexAsync(nextPlayerIndex))
					{
						return;
					}
					PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
					await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
					continue;
				}

				await RefillPlayerBaseHandAsync(playerIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				await ResolveAdventureEnemyTurnStartAbilityAsync(playerIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				PlayCardResult turnStartResult = await ResolveDragonPrinceJackTurnStartAsync(playerIndex, turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				if (turnStartResult.Played)
				{
					await ResolveActionCompletionAsync(playerIndex, turnStartResult);
					continue;
				}

				if (!PlayerHasPlayableHandCards(playerIndex))
				{
					if (!TryGetNextTurnPlayerIndex(playerIndex, out int nextPlayerIndex))
						{
							SettleStalledMatch();
							return;
						}
					if (!await SetCurrentTurnPlayerIndexAsync(nextPlayerIndex))
					{
						return;
					}
					PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
					await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
					continue;
				}

				await WaitForBoardDelayAsync(AiTurnDelaySeconds);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				PlayCardResult result = await PlayRandomHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
				if (!result.Played)
				{
					if (!TryGetNextTurnPlayerIndex(playerIndex, out int nextPlayerIndex))
						{
							SettleStalledMatch();
							return;
						}
					if (!await SetCurrentTurnPlayerIndexAsync(nextPlayerIndex))
					{
						return;
					}
					PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
					await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
					continue;
				}

				if (HandleAdventureTutorialOpponentResult(playerIndex, result))
				{
					return;
				}

				if (IsGameOver())
				{
					return;
				}

				await ResolveActionCompletionAsync(playerIndex, result);
				}
				finally
				{
					EnsureTurnLoopMadeProgress(
						previousProgressSignature,
						playerIndex,
						turnSequenceGeneration);
				}
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				_isResolvingTurnSequence = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayMainPlayerCardAndAdvanceAsync(int handIndex, int turnSequenceGeneration, Vector2? startGlobalPositionOverride = null)
	{
		if (!MatchReadModel.CanBeginMainPlayerAction(BuildGameLoopPhaseState()))
		{
			return;
		}

		_isProcessingCardPlay = true;
		try
		{
			PlayCardResult result = await PlayCardFromHandAsync(MainPlayerIndex, handIndex, turnSequenceGeneration, startGlobalPositionOverride);
			if (!result.Played)
			{
				if (result.ConsumedAdventureExtraActionGrant.HasValue)
				{
					bool resumeNormalTurn = AbortAdventureExtraActionRootChain(
						"accepted_action_did_not_complete");
					await SettleAdventureExtraActionChainAbortAsync(
						resumeNormalTurn,
						turnSequenceGeneration);
				}

				return;
			}

			if (HandleAdventureTutorialMainPlayerCardResult(result))
			{
				return;
			}

			if (IsGameOver())
			{
				return;
			}

			if (result.ConsumedAdventureExtraActionGrant is ExtraActionGrantState consumedGrant)
			{
				ExtraActionCommandOutcome completion =
					CompleteConsumedAdventureExtraActionGrant(consumedGrant);
				if (completion.CompletionKind == ExtraActionCompletionKind.ContinueChain)
				{
					_isProcessingCardPlay = false;
					await RefillPlayerBaseHandAsync(MainPlayerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					if (_playerHands[MainPlayerIndex].Count == 0)
					{
						bool resumeNormalTurn = AbortAdventureExtraActionRootChain(
							"no_base_card_available_after_action");
						await SettleAdventureExtraActionChainAbortAsync(
							resumeNormalTurn,
							turnSequenceGeneration);
						return;
					}

					await TryResolveAdventurePendingExtraBaseCardActionForAiAsync(turnSequenceGeneration);
					return;
				}

				if (completion.CompletionKind == ExtraActionCompletionKind.ResumeNormalTurn)
				{
					_isProcessingCardPlay = false;
					await RefillPlayerBaseHandAsync(MainPlayerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					await BeginRealMainPlayerTurnIfNeededAsync(turnSequenceGeneration);
					return;
				}
			}

			await ResolveActionCompletionAsync(MainPlayerIndex, result);
			_isProcessingCardPlay = false;
			if (!IsGameOver() && _currentTurnPlayerIndex != MainPlayerIndex)
			{
				await ResolveOpponentTurnsAsync(turnSequenceGeneration);
			}
			else if (!IsGameOver() && !result.CompletesTurn)
			{
				await TryResolveAdventurePendingExtraBaseCardActionForAiAsync(turnSequenceGeneration);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayMainPlayerItemCardAndAdvanceAsync(int itemHandIndex, int turnSequenceGeneration)
	{
		// Mouse confirmation starts this task from the card slot's GuiInput callback.
		// Let signal dispatch finish before RenderPlayerHands replaces that slot.
		await WaitForNextBoardFrameAsync();
		if (!MatchReadModel.CanBeginMainPlayerAction(BuildGameLoopPhaseState()))
		{
			return;
		}

		_isProcessingCardPlay = true;
		RenderPlayerHands();
		try
		{
			await BeginRealMainPlayerTurnIfNeededAsync(turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration
				|| IsGameOver()
				|| HasPendingAdventureExtraBaseCardAction())
			{
				return;
			}

			PlayCardResult result = await PlayItemCardFromHandAsync(MainPlayerIndex, itemHandIndex, turnSequenceGeneration);
			if (!result.Played || HasPendingItemSelection())
			{
				return;
			}

			if (HandleAdventureTutorialMainPlayerItemResult(result))
			{
				return;
			}

			if (IsGameOver())
			{
				return;
			}

			await ResolveActionCompletionAsync(MainPlayerIndex, result);
			_isProcessingCardPlay = false;
			if (!IsGameOver() && _currentTurnPlayerIndex != MainPlayerIndex)
			{
				await ResolveOpponentTurnsAsync(turnSequenceGeneration);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver() && !HasPendingItemSelection())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}
		}
	}

}
