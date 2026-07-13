using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolveOpponentTurnsAsync(int turnSequenceGeneration)
	{
		if (!_turnStateMachine.CanBeginOpponentSequence(BuildGameLoopPhaseState()))
		{
			return;
		}

		_isResolvingTurnSequence = true;
		try
		{
			while (turnSequenceGeneration == _turnSequenceGeneration && _currentTurnPlayerIndex != MainPlayerIndex && !IsGameOver())
			{
				int playerIndex = _currentTurnPlayerIndex;
				if (playerIndex >= player_count)
				{
					SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(playerIndex));
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
					if (turnStartResult.CompletesTurn)
					{
						await AdvanceTurnAfterPlayedResultAsync(playerIndex, turnStartResult);
					}

					continue;
				}

				if (!PlayerHasPlayableHandCards(playerIndex))
				{
					SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(playerIndex));
					PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
					await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
					continue;
				}

				await ToSignal(GetTree().CreateTimer(AiTurnDelaySeconds), SceneTreeTimer.SignalName.Timeout);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				PlayCardResult result = await PlayRandomHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
				if (!result.Played)
				{
					SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(playerIndex));
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

				if (result.CompletesTurn)
				{
					await AdvanceTurnAfterPlayedResultAsync(playerIndex, result);
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
		if (!_turnStateMachine.CanBeginMainPlayerAction(BuildGameLoopPhaseState()))
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

			if (result.ConsumedAdventureExtraActionGrant is AdventureExtraActionGrant consumedGrant)
			{
				AdventureExtraActionCompletionResult completion =
					CompleteConsumedAdventureExtraActionGrant(consumedGrant);
				if (completion.Kind == AdventureExtraActionCompletionKind.ContinueChain)
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

				if (completion.Kind == AdventureExtraActionCompletionKind.ResumeNormalTurn)
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

			if (result.CompletesTurn)
			{
				await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);
				_isProcessingCardPlay = false;
				if (!result.Captured)
				{
					await ResolveOpponentTurnsAsync(turnSequenceGeneration);
				}
			}
			else
			{
				_isProcessingCardPlay = false;
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
		if (!_turnStateMachine.CanBeginMainPlayerAction(BuildGameLoopPhaseState()))
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

			if (result.CompletesTurn)
			{
				await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);
				_isProcessingCardPlay = false;
				if (!result.Captured)
				{
					await ResolveOpponentTurnsAsync(turnSequenceGeneration);
				}
			}
			else
			{
				_isProcessingCardPlay = false;
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
