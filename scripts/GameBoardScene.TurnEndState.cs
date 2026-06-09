using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void CheckForWinner()
	{
		if (IsGameOver())
		{
			return;
		}

		int scoreWinnerPlayerIndex = GetScoreWinnerPlayerIndex();
		if (scoreWinnerPlayerIndex >= 0)
		{
			DeclareWinner(scoreWinnerPlayerIndex);
			return;
		}

		Span<GameTurnRules.PlayerCardState> playerStates = stackalloc GameTurnRules.PlayerCardState[MaxPlayers];
		BuildPlayerCardStates(playerStates);
		if (GameTurnRules.TryGetSingleRemainingPlayer(playerStates, player_count, out int remainingPlayerIndex))
		{
			DeclareWinner(remainingPlayerIndex);
		}
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
		_winnerPlayerIndex = playerIndex;
		if (adventure_mode && playerIndex == MainPlayerIndex)
		{
			CharacterAvatarCatalog.RecordAdventureVictory(adventure_enemy_avatar_id);
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
			&& _pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
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
