using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void CheckForWinner()
	{
		_ = CheckForWinnerAsync(_turnSequenceGeneration);
	}

	private async Task<bool> CheckForWinnerAsync(int turnSequenceGeneration, bool bigCaptureTriggered = false)
	{
		if (IsGameOver())
		{
			return false;
		}

		if (bigCaptureTriggered)
		{
			await QueueAndResolveAdventureUpgradeTriggersAsync(
				runStartTriggered: false,
				bigCaptureTriggered: true,
				victoryTriggered: false,
				turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame || IsGameOver())
			{
				return false;
			}
		}

		int scoreWinnerPlayerIndex = GetScoreWinnerPlayerIndex();
		if (scoreWinnerPlayerIndex >= 0)
		{
			bool victoryTriggered = ShouldDelayWinnerForAdventureVictoryUpgrade(scoreWinnerPlayerIndex);
			if (victoryTriggered)
			{
				await QueueAndResolveAdventureUpgradeTriggersAsync(
					runStartTriggered: false,
					bigCaptureTriggered: false,
					victoryTriggered: victoryTriggered,
					turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
				{
					return victoryTriggered;
				}
			}

			if (victoryTriggered)
			{
				return true;
			}

			DeclareWinner(scoreWinnerPlayerIndex);
			return false;
		}

		if (TryGetSingleRemainingPlayerIndex(out int remainingPlayerIndex))
		{
			DeclareWinner(remainingPlayerIndex);
		}

		return false;
	}

	private void BuildPlayerCardStates(Span<GameTurnRules.PlayerCardState> playerStates)
	{
		int checkedPlayerCount = Math.Min(player_count, playerStates.Length);
		for (int playerIndex = 0; playerIndex < checkedPlayerCount; playerIndex++)
			{
				playerStates[playerIndex] = new GameTurnRules.PlayerCardState(
					_playerHands[playerIndex].Count,
					_playerDecks[playerIndex].Count,
					HasPlayableItemCard(playerIndex));
			}
		}

	private bool TryGetSingleRemainingPlayerIndex(out int remainingPlayerIndex)
	{
		Span<GameTurnRules.PlayerCardState> playerStates = stackalloc GameTurnRules.PlayerCardState[MaxPlayers];
		BuildPlayerCardStates(playerStates);
		return GameTurnRules.TryGetSingleRemainingPlayer(playerStates, player_count, out remainingPlayerIndex);
	}

	private void ShowWinnerSequence(int playerIndex)
	{
		_ = ShowWinnerSequenceAsync(playerIndex);
	}

	private async Task ShowWinnerSequenceAsync(int playerIndex)
	{
		await PlayDefeatFinisherIfNeededAsync(playerIndex);
		if (_winnerPlayerIndex != playerIndex)
		{
			return;
		}

		ShowWinnerBanner(playerIndex);
	}

	private int GetMovableTableShuffleCardCount()
	{
		int movableCardCount = 0;
		foreach (TableCard tableCard in _tableCards)
		{
			if (!IsFixedDuringTableShuffle(tableCard))
			{
				movableCardCount++;
			}
		}

		return movableCardCount;
	}

	private void DeclareWinner(int playerIndex)
	{
		ClearAdventureBattleScopedStateForBattleEnd();
		_winnerPlayerIndex = playerIndex;
		if (adventure_mode && playerIndex == MainPlayerIndex)
		{
			CharacterAvatarCatalog.RecordAdventureVictory(adventure_enemy_avatar_id);
			if (ShouldUseAdventureUpgradeSystem() && !TryGetNextAdventureStageAvatarId(out _))
			{
				ClearAdventureUpgradeRun();
			}
		}

		AdvanceTurnSequenceGeneration();
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_currentTurnPlayerIndex = playerIndex;
		PlayResultMusic(playerIndex);

		for (int avatarPlayerIndex = 0; avatarPlayerIndex < player_count; avatarPlayerIndex++)
		{
			PlayPlayerAvatarAction(
				avatarPlayerIndex,
				avatarPlayerIndex == playerIndex ? CharacterAvatar.AvatarAction.Victory : CharacterAvatar.AvatarAction.Defeat);
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		ShowWinnerSequence(playerIndex);
	}

	private bool IsGameOver()
	{
		return _winnerPlayerIndex >= 0;
	}

	private bool CanMainPlayerAct()
	{
		if (IsAdventureTutorialBlockingMainPlayerAction())
		{
			return false;
		}

		return IsLocalSeatHumanConnected() && GetGameLoopPhase() == GameLoopPhase.AwaitingMainPlayerAction;
	}

	private bool IsMainPlayerMagnetSelectionActive()
	{
		if (!IsLocalSeatHumanConnected())
		{
			return false;
		}

		return GetGameLoopPhase() == GameLoopPhase.PendingMainPlayerSelection
			&& _pendingItemSelectionMode == PendingItemSelectionMode.AttachmentRewardHandCard
			&& _pendingItemOwnerIndex == GetPendingSelectionLocalOwnerIndex();
	}

	private bool IsMainPlayerHandLimitDiscardSelectionActive()
	{
		if (!IsLocalSeatHumanConnected())
		{
			return false;
		}

		return GetGameLoopPhase() == GameLoopPhase.PendingMainPlayerSelection
			&& _pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard
			&& _pendingItemOwnerIndex == GetPendingSelectionLocalOwnerIndex();
	}

	private int GetNextTurnPlayerIndex(int currentPlayerIndex)
	{
		return GameTurnRules.GetNextTurnPlayerIndex(ClockwiseTurnOrder, currentPlayerIndex, player_count, MainPlayerIndex);
	}
}
