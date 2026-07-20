using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void CheckForWinner()
	{
		TrackBoardTask(CheckForWinnerAsync(_turnSequenceGeneration), nameof(CheckForWinnerAsync));
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

		var playerStates = new GameTurnRules.PlayerCardState[MaxPlayers];
		BuildPlayerCardStates(playerStates);
		MatchOutcome outcome = MatchOutcomeRules.Evaluate(
			playerStates,
			_playerScores,
			player_count);
		if (outcome.Kind == MatchOutcomeKind.Winner)
		{
			DeclareWinner(outcome.WinnerPlayerIndex);
		}
		else if (outcome.Kind == MatchOutcomeKind.Draw)
		{
			DeclareDraw();
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
					_playerItemHands[playerIndex].Count,
					HasPlayableItemCard(playerIndex));
			}
		}

	private void ShowWinnerSequence(int playerIndex)
	{
		TrackBoardTask(ShowWinnerSequenceAsync(playerIndex), nameof(ShowWinnerSequenceAsync));
	}

	private async Task ShowWinnerSequenceAsync(int playerIndex)
	{
		if (_gameModePolicy.IsAdventureMain && playerIndex != MainPlayerIndex)
		{
			AdventureRunSessionStore? store = GetAdventureRunSessionStore();
			AdventureSaveGateResult<AdventureDefeatCommitResult> gate = await ExecuteAdventureSaveGateAsync(
				() => store?.ResolveMainAdventureDefeat()
					?? new AdventureDefeatCommitResult(AdventureRunWriteResult.SaveFailed, null),
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
			if (!gate.Succeeded)
			{
				return;
			}
			_settledAdventureDefeatResolution = gate.Result.Resolution;
		}

		await PlayDefeatFinisherIfNeededAsync(playerIndex);
		if (_winnerPlayerIndex != playerIndex)
		{
			return;
		}
		if (_gameModePolicy.IsAdventureMain
			&& playerIndex == MainPlayerIndex
			&& !await CommitAdventureVictoryWithRetryAsync())
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
		_matchOutcomeKind = MatchOutcomeKind.Winner;
		_winnerPlayerIndex = playerIndex;
		if (adventure_mode && playerIndex == MainPlayerIndex)
		{
			if (!_gameModePolicy.IsAdventureMain)
			{
				CharacterAvatarCatalog.RecordAdventureVictory(adventure_enemy_avatar_id);
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
		if (IsNetworkMultiplayerMode() && IsNetworkHost())
		{
			int winnerSnapshotId = CreateNextNetworkSnapshotId();
			GetNetworkManager()?.SendBoardSnapshotsFromHost(this, winnerSnapshotId);
		}
		ShowWinnerSequence(playerIndex);
	}

	private void DeclareDraw()
	{
		ClearAdventureBattleScopedStateForBattleEnd();
		_matchOutcomeKind = MatchOutcomeKind.Draw;
		_winnerPlayerIndex = -1;
		AdvanceTurnSequenceGeneration();
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		if (IsNetworkMultiplayerMode() && IsNetworkHost())
		{
			int drawSnapshotId = CreateNextNetworkSnapshotId();
			GetNetworkManager()?.SendBoardSnapshotsFromHost(this, drawSnapshotId);
		}
		ShowDrawSequence();
	}

	private void SettleStalledMatch()
	{
		if (IsGameOver())
		{
			return;
		}

		MatchOutcome outcome = MatchOutcomeRules.ResolveStalemate(_playerScores, player_count);
		GD.PushError("Turn resolution stalled without changing the board or active seat; settling the match.");
		if (outcome.Kind == MatchOutcomeKind.Winner)
		{
			DeclareWinner(outcome.WinnerPlayerIndex);
		}
		else
		{
			DeclareDraw();
		}
	}

	private void ShowDrawSequence()
	{
		ShowWinnerBanner(-1);
	}

	private bool IsGameOver()
	{
		return _matchOutcomeKind != MatchOutcomeKind.InProgress;
	}

	private bool CanMainPlayerAct()
	{
		if (_adventureSaveInProgress)
		{
			return false;
		}
		if (IsAdventureTutorialBlockingMainPlayerAction())
		{
			return false;
		}

			if (IsNetworkMultiplayerMode()
				&& _networkMigrationInputLocked)
			{
				return false;
			}

			if (IsNetworkMultiplayerMode()
				&& !IsNetworkHost()
			&& NetworkVisualPlayback.IsProcessing)
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

	private bool TryGetNextTurnPlayerIndex(int currentPlayerIndex, out int nextPlayerIndex)
	{
		Span<GameTurnRules.PlayerCardState> playerStates = stackalloc GameTurnRules.PlayerCardState[MaxPlayers];
		BuildPlayerCardStates(playerStates);
		return GameTurnRules.TryGetNextTurnPlayerIndex(
			ClockwiseTurnOrder,
			playerStates,
			currentPlayerIndex,
			player_count,
			out nextPlayerIndex);
	}
}
