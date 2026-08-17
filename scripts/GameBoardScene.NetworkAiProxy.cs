using System;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	internal bool IsNetworkMatchClaimable()
	{
		return !IsGameOver();
	}

	private bool IsLocalSeatHumanConnected()
	{
		if (!IsNetworkMultiplayerMode())
		{
			return true;
		}

		NetworkManager? networkManager = GetNetworkManager();
		NetworkSessionState networkState = networkManager?.State ?? NetworkSessionState.Idle;
		int localSeatIndex = GetLocalHandPlayerIndex();
		bool seatStateFound = TryGetSeatControlStateForUi(localSeatIndex, out SeatControlState state);

		return LocalSeatControlRules.IsLocalHumanInputEnabled(
			isNetworkMultiplayer: true,
			networkState,
			seatStateFound,
			state.Mode);
	}

	private void PromoteReclaimedSeatAtCurrentTurnStartIfNeeded()
	{
		if (!IsNetworkMultiplayerMode() || !IsNetworkHost())
		{
			return;
		}

		if (GetNetworkManager()?.TryPromoteReclaimedSeatAtTurnStart(_currentTurnPlayerIndex) == true)
		{
			MarkSeatControlIndicatorsDirty();
			MarkActivePlayerStatusTooltipForRefresh();
			GetNetworkManager()?.SendBoardSnapshotsFromHost(CreateNextNetworkSnapshotId());
		}
	}

	internal void BeginNetworkAiProxyTurnIfNeeded()
	{
		if (!ShouldRunNetworkAiProxyTurn())
		{
			return;
		}

		TrackBoardTask(ResolveNetworkAiProxyTurnsAsync(_turnSequenceGeneration), nameof(ResolveNetworkAiProxyTurnsAsync));
	}

	private void ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded()
	{
		if (!MatchReadModel.ShouldResumeAiProxyTurnAfterAuthorityAction(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			IsGameOver(),
			_isProcessingCardPlay))
		{
			return;
		}

		BeginNetworkAiProxyTurnIfNeeded();
	}

	private void BeginNetworkAiProxyTurnForPendingSelectionIfNeeded()
	{
		if (!MatchReadModel.ShouldStartAiProxyForPendingSelection(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			ShouldResolvePendingNetworkAiProxySelection()))
		{
			return;
		}

		BeginNetworkAiProxyTurnIfNeeded();
	}

	private bool ShouldRunNetworkAiProxyTurn()
	{
		if (OS.GetEnvironment("CODEX_AUTHORITY_TRANSACTION_FIXTURE") == "1"
			|| OS.GetEnvironment("CODEX_OPENING_MIGRATION_FIXTURE") == "1"
			|| !IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| _networkAiProxyTurnRunning
			|| IsGameOver()
			|| _isOpeningDealAnimationPlaying)
		{
			return false;
		}

		if (ShouldResolvePendingNetworkAiProxySelection())
		{
			return true;
		}

		if (_isProcessingCardPlay)
		{
			return false;
		}

		if (HasPendingItemSelection())
		{
			return false;
		}

		return GetNetworkManager()?.IsSeatAiControlled(_currentTurnPlayerIndex) ?? false;
	}

	private bool IsNetworkHumanControlledSeat(int seatIndex)
	{
		return IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& !(GetNetworkManager()?.IsSeatAiControlled(seatIndex) ?? false);
	}

	private async Task ResolveNetworkAiProxyTurnsAsync(int turnSequenceGeneration)
	{
		if (_networkAiProxyTurnRunning)
		{
			return;
		}

		_networkAiProxyTurnRunning = true;
		bool wasResolvingTurnSequence = _isResolvingTurnSequence;
		_isResolvingTurnSequence = true;
		try
		{
			while (turnSequenceGeneration == _turnSequenceGeneration
				&& !IsGameOver()
				&& !_networkMigrationInputLocked
				&& (ShouldResolvePendingNetworkAiProxySelection()
					|| (GetNetworkManager()?.IsSeatAiControlled(_currentTurnPlayerIndex) ?? false)))
			{
				int failedSeatIndex = ShouldResolvePendingNetworkAiProxySelection()
					? _pendingItemOwnerIndex
					: _currentTurnPlayerIndex;
				int previousPlayerIndex = _currentTurnPlayerIndex;
				long previousProgressSignature = GetTurnLoopProgressSignature();
				try
				{
					if (await TryResolvePendingNetworkAiProxySelectionAsync(turnSequenceGeneration))
					{
						continue;
					}

					int playerIndex = _currentTurnPlayerIndex;
					if (playerIndex < 0 || playerIndex >= player_count)
					{
						return;
					}

					await RefillPlayerBaseHandAsync(playerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || _networkMigrationInputLocked)
					{
						return;
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
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || _networkMigrationInputLocked)
					{
						return;
					}

					PlayCardResult result = await PlayNetworkAiProxyHandEntryAsync(playerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || _networkMigrationInputLocked)
					{
						return;
					}

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

					await ResolveActionCompletionAsync(playerIndex, result);
				}
				catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
				{
					GD.PushWarning($"AI proxy turn loop failed for seat {failedSeatIndex}: {ex.Message}");
					if (!await RecoverNetworkAiProxyTurnAfterFailureAsync(failedSeatIndex, turnSequenceGeneration))
					{
						return;
					}
				}
				finally
				{
					EnsureTurnLoopMadeProgress(
						previousProgressSignature,
						previousPlayerIndex,
						turnSequenceGeneration);
				}
			}
		}
		finally
		{
			_networkAiProxyTurnRunning = false;
			_isResolvingTurnSequence = wasResolvingTurnSequence;
			RenderPlayerHands();
			if (IsNetworkHost())
			{
				GetNetworkManager()?.SendBoardSnapshotsFromHost(CreateNextNetworkSnapshotId());
			}
		}
	}

	private async Task<bool> RecoverNetworkAiProxyTurnAfterFailureAsync(int failedSeatIndex, int turnSequenceGeneration)
	{
		if (!MatchReadModel.ShouldRecoverFromUnhandledAiTurnFailure(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			IsGameOver()))
		{
			return false;
		}

		if (_networkMigrationInputLocked)
		{
			return false;
		}

		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return true;
		}

		if (ShouldResolvePendingNetworkAiProxySelection())
		{
			int ownerSeatIndex = _pendingItemOwnerIndex;
			CancelPendingNetworkAiProxySelectionForFailedItem(ownerSeatIndex);
			await WaitForNextBoardFrameAsync();
			return true;
		}

		_isProcessingCardPlay = false;
		if (failedSeatIndex < 0 || failedSeatIndex >= player_count)
		{
			return false;
		}

		if (!TryGetNextTurnPlayerIndex(failedSeatIndex, out int nextPlayerIndex))
		{
			SettleStalledMatch();
			return false;
		}
		if (!await SetCurrentTurnPlayerIndexAsync(nextPlayerIndex))
			{
				return false;
			}
		PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
		try
		{
			await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
		}
		catch (Exception recoveryEx) when (recoveryEx is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushWarning($"AI proxy turn recovery refill failed after seat {failedSeatIndex}: {recoveryEx.Message}");
		}

		return true;
	}

	private bool ShouldResolvePendingNetworkAiProxySelection()
	{
		if (!HasPendingItemSelection()
			|| _pendingItemTurnSequenceGeneration != _turnSequenceGeneration
			|| _pendingItemOwnerIndex < 0
			|| _pendingItemOwnerIndex >= player_count
			|| (_networkAuthorityActionState.IsActive
				&& AuthorityActionStateRules.HasCommittedSelection(_networkAuthorityActionState)))
		{
			return false;
		}

		if (_pendingItemSelectionMode is not (
			PendingItemSelectionMode.HandLimitDiscard
			or PendingItemSelectionMode.AttachmentRewardHandCard))
		{
			return false;
		}

		return GetNetworkManager()?.IsSeatAiControlled(_pendingItemOwnerIndex) ?? false;
	}

	private async Task<bool> TryResolvePendingNetworkAiProxySelectionAsync(int turnSequenceGeneration)
	{
		if (!ShouldResolvePendingNetworkAiProxySelection())
		{
			return false;
		}

		if (_networkMigrationInputLocked)
		{
			return false;
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& !_networkMigrationInputLocked
			&& ShouldResolvePendingNetworkAiProxySelection())
		{
			int ownerSeatIndex = _pendingItemOwnerIndex;
			try
			{
				int selectionIndex;
				switch (_pendingItemSelectionMode)
				{
					case PendingItemSelectionMode.HandLimitDiscard:
						selectionIndex = PickPendingHandLimitDiscardIndexForNetworkAiProxy(ownerSeatIndex);
						if (selectionIndex < 0)
						{
							GD.PushWarning($"AI proxy could not find a legal discard index for seat {ownerSeatIndex}.");
							await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
							return true;
						}
						break;
					case PendingItemSelectionMode.AttachmentRewardHandCard:
						selectionIndex = PickRandomAttachableMagnetHandIndex(ownerSeatIndex, _pendingItemCardId);
						if (selectionIndex < 0)
						{
							await CompleteMagnetAttachmentSelectionAsync(-1);
							await WaitForNextBoardFrameAsync();
							continue;
						}
						break;
					default:
						return false;
				}

				if (!TryStartNetworkPendingItemSelectionFromHost(
					ownerSeatIndex,
					selectionIndex,
					turnSequenceGeneration,
					out MultiplayerBoardActionRejectReason rejectReason))
				{
					GD.PushWarning($"AI proxy pending item selection rejected: {rejectReason}.");
					await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
					return true;
				}

				await WaitForNextBoardFrameAsync();
			}
			catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
			{
				GD.PushWarning($"AI proxy pending item selection failed for seat {ownerSeatIndex}: {ex.Message}");
				await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
				return true;
			}
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& !_networkMigrationInputLocked
			&& (_isProcessingCardPlay || HasPendingItemSelection()))
		{
			await WaitForNextBoardFrameAsync();
		}

		return true;
	}

	private async Task ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(int ownerSeatIndex, int turnSequenceGeneration)
	{
		if (_networkMigrationInputLocked
			|| !HasPendingItemSelection()
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| ownerSeatIndex != _pendingItemOwnerIndex)
		{
			return;
		}

		try
		{
			int selectionIndex;
			switch (_pendingItemSelectionMode)
			{
				case PendingItemSelectionMode.HandLimitDiscard:
					selectionIndex = PickPendingHandLimitDiscardIndexForNetworkAiProxy(ownerSeatIndex);
					if (selectionIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							selectionIndex,
							turnSequenceGeneration,
							out _))
					{
						await WaitForNextBoardFrameAsync();
						return;
					}
					break;
				case PendingItemSelectionMode.AttachmentRewardHandCard:
					selectionIndex = PickRandomAttachableMagnetHandIndex(ownerSeatIndex, _pendingItemCardId);
					if (selectionIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							selectionIndex,
							turnSequenceGeneration,
							out _))
					{
						await WaitForNextBoardFrameAsync();
						return;
					}

					await CompleteMagnetAttachmentSelectionAsync(-1);
					await WaitForNextBoardFrameAsync();
					return;
			}
		}
		catch (Exception fallbackEx) when (fallbackEx is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushWarning($"AI proxy pending item fallback failed for seat {ownerSeatIndex}: {fallbackEx.Message}");
		}

		CancelPendingNetworkAiProxySelectionForFailedItem(ownerSeatIndex);
	}

	private void RememberBlockedPendingNetworkAiItem(int ownerSeatIndex)
	{
		if (string.IsNullOrWhiteSpace(_pendingItemCardId))
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			BlockedAutomatedItemSeat = ownerSeatIndex,
			BlockedAutomatedItemId = _pendingItemCardId,
		});
	}

	private void CancelPendingNetworkAiProxySelectionForFailedItem(int ownerSeatIndex)
	{
		RememberBlockedPendingNetworkAiItem(ownerSeatIndex);
		CancelPendingItemSelection();
	}

	private void CancelPendingItemSelectionForAuthorityFailure(int ownerSeatIndex)
	{
		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& ownerSeatIndex >= 0
			&& (GetNetworkManager()?.IsSeatAiControlled(ownerSeatIndex) ?? false))
		{
			CancelPendingNetworkAiProxySelectionForFailedItem(ownerSeatIndex);
			return;
		}

		CancelPendingItemSelection();
	}

	private async Task<PlayCardResult> PlayNetworkAiProxyHandEntryAsync(int playerIndex, int turnSequenceGeneration)
	{
		try
		{
			if (TryPickEarlyTableNewCardHandIndex(playerIndex, out int earlyTableHandIndex))
			{
				return await PlayCardFromHandAsync(playerIndex, earlyTableHandIndex, turnSequenceGeneration);
			}

			if (TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
				&& TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
			{
				CaptureScoringTableCard[] tableCards = ToCaptureScoringTableCards(
					readModel.TableCards);
				PotentialCaptureCard[] handCards = ToPotentialCaptureCards(
					playerIndex,
					player.Hand);
				CaptureScoringContext scoringContext = BuildCaptureScoringContext(playerIndex);
				if (TrySubmitChooseRandomIndexRuleCommand(
						playerIndex,
						0,
						99,
						turnSequenceGeneration,
						out int decisionRollPercent)
					&& TrySubmitChooseRandomIndexRuleCommand(
						playerIndex,
						0,
						9999,
						turnSequenceGeneration,
						out int randomCaptureOrdinal)
					&& MatchReadModel.TryPickCaptureHandIndexForNetworkAiProxy(
						tableCards,
						handCards,
						scoringContext,
						decisionRollPercent,
						randomCaptureOrdinal,
						out int captureHandIndex))
				{
					return await PlayCardFromHandAsync(playerIndex, captureHandIndex, turnSequenceGeneration);
				}
			}

			return await PlayRandomPlayableHandEntryWithoutCapturePriorityAsync(playerIndex, turnSequenceGeneration);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushWarning($"AI proxy turn heuristic failed for seat {playerIndex}: {ex.Message}");
		}

		try
		{
			return await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushWarning($"AI proxy fallback action failed for seat {playerIndex}: {ex.Message}");
			return new PlayCardResult(false, false);
		}
	}

	private int PickPendingHandLimitDiscardIndexForNetworkAiProxy(int ownerSeatIndex)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, ownerSeatIndex, out PlayerStateSnapshot player))
		{
			return -1;
		}

		try
		{
#if DECKLAND_TEST_HOOKS
			if (ConsumeDebugForceNextPendingHandLimitDiscardHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced hand-limit discard heuristic failure");
			}
#endif

			int targetHandIndex = PickRandomHandDisplayIndex(ownerSeatIndex);
			if (IsValidHandLimitDiscardSelection(player, targetHandIndex))
			{
				return targetHandIndex;
			}
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !IsBoardStopping)
		{
			GD.PushWarning($"AI proxy discard heuristic failed for seat {ownerSeatIndex}: {ex.Message}");
		}

		int totalHandCardCount = player.Hand.Length + player.ItemHand.Length;
		if (!TrySubmitChooseRandomIndexRuleCommand(
				ownerSeatIndex,
				0,
				int.MaxValue,
				_turnSequenceGeneration,
				out int randomOrdinal))
		{
			for (int displayIndex = 0; displayIndex < totalHandCardCount; displayIndex++)
			{
				if (IsValidHandLimitDiscardSelection(player, displayIndex))
				{
					return displayIndex;
				}
			}

			return -1;
		}

		return MatchReadModel.PickRandomLegalCandidate(
			totalHandCardCount,
			displayIndex => IsValidHandLimitDiscardSelection(player, displayIndex),
			randomOrdinal);
	}

}
