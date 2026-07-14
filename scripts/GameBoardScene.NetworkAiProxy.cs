using System;
using System.Threading.Tasks;
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
			GetNetworkManager()?.SendBoardSnapshotsFromHost(this, CreateNextNetworkSnapshotId());
		}
	}

	internal void BeginNetworkAiProxyTurnIfNeeded()
	{
		if (!ShouldRunNetworkAiProxyTurn())
		{
			return;
		}

		_ = ResolveNetworkAiProxyTurnsAsync(_turnSequenceGeneration);
	}

	private void ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded()
	{
		if (!NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
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
		if (!NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
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
		if (!IsNetworkMultiplayerMode()
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
				&& (ShouldResolvePendingNetworkAiProxySelection()
					|| (GetNetworkManager()?.IsSeatAiControlled(_currentTurnPlayerIndex) ?? false)))
			{
				int failedSeatIndex = ShouldResolvePendingNetworkAiProxySelection()
					? _pendingItemOwnerIndex
					: _currentTurnPlayerIndex;
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
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
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

					PlayCardResult result = await PlayNetworkAiProxyHandEntryAsync(playerIndex, turnSequenceGeneration);
					if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
					{
						return;
					}

					if (!result.Played)
					{
						SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(playerIndex));
						PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
						await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
						continue;
					}

					if (result.CompletesTurn)
					{
						await AdvanceTurnAfterPlayedResultAsync(playerIndex, result);
					}
				}
				catch (Exception ex)
				{
					GD.PushWarning($"AI proxy turn loop failed for seat {failedSeatIndex}: {ex.Message}");
					if (!await RecoverNetworkAiProxyTurnAfterFailureAsync(failedSeatIndex, turnSequenceGeneration))
					{
						return;
					}
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
				GetNetworkManager()?.SendBoardSnapshotsFromHost(this, CreateNextNetworkSnapshotId());
			}
		}
	}

	private async Task<bool> RecoverNetworkAiProxyTurnAfterFailureAsync(int failedSeatIndex, int turnSequenceGeneration)
	{
		if (!NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			IsNetworkMultiplayerMode(),
			IsNetworkHost(),
			IsGameOver()))
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
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			return true;
		}

		_isProcessingCardPlay = false;
		if (failedSeatIndex < 0 || failedSeatIndex >= player_count)
		{
			return false;
		}

		SetCurrentTurnPlayerIndex(GetNextTurnPlayerIndex(failedSeatIndex));
		PromoteReclaimedSeatAtCurrentTurnStartIfNeeded();
		try
		{
			await RefillCurrentTurnPlayerBaseHandAsync(turnSequenceGeneration);
		}
		catch (Exception recoveryEx)
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
			|| _pendingItemOwnerIndex >= player_count)
		{
			return false;
		}

		if (_pendingItemSelectionMode is not (
			PendingItemSelectionMode.TargetPlayer
			or PendingItemSelectionMode.TargetHandCard
			or PendingItemSelectionMode.HandLimitDiscard
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

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& ShouldResolvePendingNetworkAiProxySelection())
		{
			int ownerSeatIndex = _pendingItemOwnerIndex;
			try
			{
				int targetPlayerIndex;
				int targetHandIndex = -1;
				switch (_pendingItemSelectionMode)
				{
					case PendingItemSelectionMode.TargetPlayer:
						targetPlayerIndex = PickPendingItemTargetPlayerForNetworkAiProxy();
						if (targetPlayerIndex < 0)
						{
							GD.PushWarning($"AI proxy could not find a legal target player for pending item '{_pendingItemCardId}'.");
							await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
							return true;
						}
						break;
					case PendingItemSelectionMode.TargetHandCard:
						targetPlayerIndex = _revealedPlayerHandIndex;
						targetHandIndex = PickPendingItemTargetHandIndexForNetworkAiProxy(targetPlayerIndex);
						if (targetHandIndex < 0)
						{
							GD.PushWarning($"AI proxy could not find a legal target hand index for pending item '{_pendingItemCardId}'.");
							await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
							return true;
						}
						break;
					case PendingItemSelectionMode.HandLimitDiscard:
						targetPlayerIndex = -1;
						targetHandIndex = PickPendingHandLimitDiscardIndexForNetworkAiProxy(ownerSeatIndex);
						if (targetHandIndex < 0)
						{
							GD.PushWarning($"AI proxy could not find a legal discard index for seat {ownerSeatIndex}.");
							await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
							return true;
						}
						break;
					case PendingItemSelectionMode.AttachmentRewardHandCard:
						targetPlayerIndex = -1;
						targetHandIndex = PickRandomAttachableMagnetHandIndex(ownerSeatIndex, _pendingItemCardId);
						if (targetHandIndex < 0)
						{
							await CompleteMagnetAttachmentSelectionAsync(-1);
							await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
							continue;
						}
						break;
					default:
						return false;
				}

				if (!TryStartNetworkPendingItemSelectionFromHost(
					ownerSeatIndex,
					targetPlayerIndex,
					targetHandIndex,
					turnSequenceGeneration,
					out MultiplayerBoardActionRejectReason rejectReason))
				{
					GD.PushWarning($"AI proxy pending item selection rejected: {rejectReason}.");
					await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
					return true;
				}

				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
			catch (Exception ex)
			{
				GD.PushWarning($"AI proxy pending item selection failed for seat {ownerSeatIndex}: {ex.Message}");
				await ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(ownerSeatIndex, turnSequenceGeneration);
				return true;
			}
		}

		while (turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver()
			&& (_isProcessingCardPlay || HasPendingItemSelection()))
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		return true;
	}

	private async Task ForceResolvePendingNetworkAiProxySelectionAfterFailureAsync(int ownerSeatIndex, int turnSequenceGeneration)
	{
		if (!HasPendingItemSelection()
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| ownerSeatIndex != _pendingItemOwnerIndex)
		{
			return;
		}

		try
		{
			int targetPlayerIndex;
			int targetHandIndex;
			switch (_pendingItemSelectionMode)
			{
				case PendingItemSelectionMode.TargetPlayer:
					targetPlayerIndex = PickRandomLegalPendingItemTargetPlayer(ownerSeatIndex);
					if (targetPlayerIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							targetPlayerIndex,
							-1,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}
					break;
				case PendingItemSelectionMode.TargetHandCard:
					targetPlayerIndex = _revealedPlayerHandIndex;
					targetHandIndex = PickRandomLegalPendingItemTargetHandIndex(ownerSeatIndex, targetPlayerIndex);
					if (targetHandIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							targetPlayerIndex,
							targetHandIndex,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}
					break;
				case PendingItemSelectionMode.HandLimitDiscard:
					targetHandIndex = PickPendingHandLimitDiscardIndexForNetworkAiProxy(ownerSeatIndex);
					if (targetHandIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							-1,
							targetHandIndex,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}
					break;
				case PendingItemSelectionMode.AttachmentRewardHandCard:
					targetHandIndex = PickRandomAttachableMagnetHandIndex(ownerSeatIndex, _pendingItemCardId);
					if (targetHandIndex >= 0
						&& TryStartNetworkPendingItemSelectionFromHost(
							ownerSeatIndex,
							-1,
							targetHandIndex,
							turnSequenceGeneration,
							out _))
					{
						await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
						return;
					}

					await CompleteMagnetAttachmentSelectionAsync(-1);
					await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
					return;
			}
		}
		catch (Exception fallbackEx)
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

		_networkAiBlockedItem = new NetworkAiBlockedItemState(
			ownerSeatIndex,
			_pendingItemCardId);
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

			if (playerIndex >= 0
				&& playerIndex < _playerHands.Count
				&& NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
					ToCaptureScoringTableCards(),
					ToPotentialCaptureCards(playerIndex, _playerHands[playerIndex]),
					BuildCaptureScoringContext(playerIndex),
					_rng.RandiRange(0, 99),
					_rng.RandiRange(0, 9999),
					out int captureHandIndex))
			{
				return await PlayCardFromHandAsync(playerIndex, captureHandIndex, turnSequenceGeneration);
			}

			return await PlayRandomPlayableHandEntryWithoutCapturePriorityAsync(playerIndex, turnSequenceGeneration);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy turn heuristic failed for seat {playerIndex}: {ex.Message}");
		}

		try
		{
			return await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy fallback action failed for seat {playerIndex}: {ex.Message}");
			return new PlayCardResult(false, false);
		}
	}

	private int PickPendingItemTargetPlayerForNetworkAiProxy()
	{
		try
		{
			if (ConsumeDebugForceNextPendingTargetPlayerHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced target-player heuristic failure");
			}

			int targetPlayerIndex = PickItemTargetPlayer(_pendingItemCardId, _pendingItemOwnerIndex);
			if (IsItemTargetPlayerCandidate(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex))
			{
				return targetPlayerIndex;
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy target-player heuristic failed for pending item '{_pendingItemCardId}': {ex.Message}");
		}

		return PickRandomLegalPendingItemTargetPlayer(_pendingItemOwnerIndex);
	}

	private int PickPendingItemTargetHandIndexForNetworkAiProxy(int targetPlayerIndex)
	{
		try
		{
			if (ConsumeDebugForceNextPendingTargetHandHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced target-hand heuristic failure");
			}

			int targetHandIndex = PickTargetHandIndex(targetPlayerIndex);
			if (IsPendingItemTargetValid(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex, targetHandIndex))
			{
				return targetHandIndex;
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy target-hand heuristic failed for pending item '{_pendingItemCardId}': {ex.Message}");
		}

		return PickRandomLegalPendingItemTargetHandIndex(_pendingItemOwnerIndex, targetPlayerIndex);
	}

	private int PickPendingHandLimitDiscardIndexForNetworkAiProxy(int ownerSeatIndex)
	{
		if (ownerSeatIndex < 0
			|| ownerSeatIndex >= _playerHands.Count
			|| ownerSeatIndex >= _playerItemHands.Count)
		{
			return -1;
		}

		try
		{
			if (ConsumeDebugForceNextPendingHandLimitDiscardHeuristicFailure())
			{
				throw new InvalidOperationException("debug forced hand-limit discard heuristic failure");
			}

			int targetHandIndex = PickRandomHandDisplayIndex(ownerSeatIndex);
			if (IsValidHandLimitDiscardSelection(ownerSeatIndex, targetHandIndex))
			{
				return targetHandIndex;
			}
		}
		catch (Exception ex)
		{
			GD.PushWarning($"AI proxy discard heuristic failed for seat {ownerSeatIndex}: {ex.Message}");
		}

		int totalHandCardCount = GetTotalHandCardCount(ownerSeatIndex);
		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalHandCardCount,
			displayIndex => IsValidHandLimitDiscardSelection(ownerSeatIndex, displayIndex),
			_rng.RandiRange(0, int.MaxValue));
	}

	private int PickRandomLegalPendingItemTargetPlayer(int ownerSeatIndex)
	{
		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			player_count,
			targetPlayerIndex => IsItemTargetPlayerCandidate(_pendingItemCardId, ownerSeatIndex, targetPlayerIndex),
			_rng.RandiRange(0, int.MaxValue));
	}

	private int PickRandomLegalPendingItemTargetHandIndex(int ownerSeatIndex, int targetPlayerIndex)
	{
		int totalHandCardCount = GetTotalHandCardCount(targetPlayerIndex);
		return NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalHandCardCount,
			handIndex => IsPendingItemTargetValid(_pendingItemCardId, ownerSeatIndex, targetPlayerIndex, handIndex),
			_rng.RandiRange(0, int.MaxValue));
	}
}
