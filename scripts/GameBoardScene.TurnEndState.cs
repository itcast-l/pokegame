using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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

				ResolveRuleMatchOutcomeAndPresent(scoreWinnerPlayerIndex);
				return false;
			}

			ResolveRuleMatchOutcomeAndPresent(scoreOrLifeWinnerPlayerIndex: -1);

		return false;
	}

	private void BuildPlayerCardStates(Span<GameTurnPlayerCardState> playerStates)
	{
		int checkedPlayerCount = Math.Min(player_count, playerStates.Length);
		for (int playerIndex = 0; playerIndex < checkedPlayerCount; playerIndex++)
			{
				playerStates[playerIndex] = new GameTurnPlayerCardState(
					_playerHands[playerIndex].Count,
					_playerDecks[playerIndex].Count,
					_playerItemHands[playerIndex].Count,
					HasPlayableItemCard(playerIndex));
			}
		}

	private void ShowWinnerSequence(int playerIndex)
	{
		RefreshCaptureRequirementWatermark(animateChange: false);
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		TrackBoardTask(ShowWinnerSequenceAsync(playerIndex), nameof(ShowWinnerSequenceAsync));
	}

	private async Task ShowWinnerSequenceAsync(int playerIndex)
	{
		if (ModePolicy.IsAdventureMain && playerIndex != MainPlayerIndex)
		{
			AdventureRunSessionStore? store = GetAdventureRunSessionStore();
			AdventureRunSessionStore.PreparedAdventureOperation<AdventureDefeatCommitResult> operation =
				store?.PrepareResolveMainAdventureDefeat()
				?? AdventureRunSessionStore.PreparedAdventureOperation<AdventureDefeatCommitResult>.Rejected(
					AdventureRunWriteResult.SaveFailed,
					static result => new AdventureDefeatCommitResult(result, null));
			AdventureSaveGateResult<AdventureDefeatCommitResult> gate = await ExecutePreparedAdventureSaveGateAsync(
				operation,
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
		if (ModePolicy.IsAdventureMain
			&& playerIndex == MainPlayerIndex
			&& !await CommitAdventureVictoryWithRetryAsync())
		{
			return;
		}
		if (ModePolicy.IsAdventureTutorial
			&& playerIndex == MainPlayerIndex
			&& !await CommitAdventureTutorialVictoryWithRetryAsync())
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
		ResolveRuleMatchOutcomeAndPresent(playerIndex);
	}

	private void PresentDeclaredWinner(int playerIndex)
	{
		ClearAdventureBattleScopedStateForBattleEnd();
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
		if (!_networkAuthorityRuleTransactionActive
			&& IsNetworkMultiplayerMode()
			&& IsNetworkHost())
		{
			int winnerSnapshotId = CreateNextNetworkSnapshotId();
			GetNetworkManager()?.SendBoardSnapshotsFromHost(winnerSnapshotId);
		}
		ShowWinnerSequence(playerIndex);
	}

	private void DeclareDraw()
	{
		ResolveRuleMatchOutcomeAndPresent(
			scoreOrLifeWinnerPlayerIndex: -1,
			settleRegardlessOfAvailableActions: true);
	}

	private void PresentDeclaredDraw()
	{
		ClearAdventureBattleScopedStateForBattleEnd();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		if (!_networkAuthorityRuleTransactionActive
			&& IsNetworkMultiplayerMode()
			&& IsNetworkHost())
		{
			int drawSnapshotId = CreateNextNetworkSnapshotId();
			GetNetworkManager()?.SendBoardSnapshotsFromHost(drawSnapshotId);
		}
		ShowDrawSequence();
	}

	private void SettleStalledMatch()
	{
		if (IsGameOver())
		{
			return;
		}

		GD.PushError("Turn resolution stalled without changing the board or active seat; settling the match.");
		ResolveRuleMatchOutcomeAndPresent(
			scoreOrLifeWinnerPlayerIndex: -1,
			settleRegardlessOfAvailableActions: true);
	}

	private bool ResolveRuleMatchOutcomeAndPresent(
		int scoreOrLifeWinnerPlayerIndex,
		bool settleRegardlessOfAvailableActions = false)
	{
		var playableItemActions = ImmutableArray.CreateBuilder<bool>(player_count);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			playableItemActions.Add(HasPlayableItemCard(playerIndex));
		}

		if (!TrySubmitEvaluateMatchOutcomeRuleCommand(
			scoreOrLifeWinnerPlayerIndex,
			playableItemActions.MoveToImmutable(),
			settleRegardlessOfAvailableActions,
			out RuleCommandResult result)
			|| !result.Mutation.MatchOutcomeChanged)
		{
			return false;
		}

		if (result.ReadModel.Outcome == Deckland.Rules.MatchOutcome.Won)
		{
			PresentDeclaredWinner(result.ReadModel.WinnerSeat);
		}
		else if (result.ReadModel.Outcome == Deckland.Rules.MatchOutcome.Draw)
		{
			PresentDeclaredDraw();
		}
		return true;
	}

	private void ShowDrawSequence()
	{
		RefreshCaptureRequirementWatermark(animateChange: false);
		ShowWinnerBanner(-1);
	}

	private void PresentCommittedNetworkAuthorityMatchOutcome()
	{
		if (_matchOutcomeKind == MatchOutcomeKind.Winner)
		{
			PlayResultMusic(_winnerPlayerIndex);
			for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
			{
				PlayPlayerAvatarAction(
					playerIndex,
					playerIndex == _winnerPlayerIndex
						? CharacterAvatar.AvatarAction.Victory
						: CharacterAvatar.AvatarAction.Defeat);
			}
			ShowWinnerSequence(_winnerPlayerIndex);
		}
		else if (_matchOutcomeKind == MatchOutcomeKind.Draw)
		{
			ShowDrawSequence();
		}
	}

	private bool IsGameOver()
	{
		return _matchOutcomeKind != MatchOutcomeKind.InProgress;
	}

	private bool CanMainPlayerAct()
	{
		if (AdventureSaveInProgress)
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
		Span<GameTurnPlayerCardState> playerStates = stackalloc GameTurnPlayerCardState[MaxPlayers];
		BuildPlayerCardStates(playerStates);
		return MatchReadModel.TryGetNextTurnPlayerIndex(
			ClockwiseTurnOrder,
			playerStates,
			currentPlayerIndex,
			player_count,
			out nextPlayerIndex);
	}
}
