using System;
using Godot;

public partial class GameBoardScene
{
	private const string DebugPendingTargetHandSelectionItemCardId = "Q";
	private bool _debugForceNextPendingTargetPlayerHeuristicFailure;
	private bool _debugForceNextPendingTargetHandHeuristicFailure;
	private bool _debugForceNextPendingHandLimitDiscardHeuristicFailure;
	private double _debugNetworkVisualPlaybackDelaySeconds;

	public bool DebugStartPendingHandLimitDiscardForSeat(int ownerPlayerIndex)
	{
		if (!item_mode
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| GetTotalHandCardCount(ownerPlayerIndex) <= 0
			|| _pendingHandLimitDiscardCompletion != null
			|| HasPendingItemSelection())
		{
			return false;
		}

		_ = ResolveHumanHandLimitDiscardAsync(ownerPlayerIndex, _turnSequenceGeneration);
		return true;
	}

	public bool DebugStartPendingTargetHandSelectionForSeat(int ownerPlayerIndex)
	{
		if (!item_mode
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| HasPendingItemSelection()
			|| _pendingHandLimitDiscardCompletion != null
			|| _pendingMagnetAttachmentCompletion != null
			|| !_itemCardCatalog.TryGetDefinition(DebugPendingTargetHandSelectionItemCardId, out _))
		{
			return false;
		}

		int targetPlayerIndex = -1;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (IsItemTargetPlayerCandidate(DebugPendingTargetHandSelectionItemCardId, ownerPlayerIndex, playerIndex))
			{
				targetPlayerIndex = playerIndex;
				break;
			}
		}

		if (targetPlayerIndex < 0)
		{
			return false;
		}

		Texture2D? placeholderTexture = _cardBackTexture;
		if (placeholderTexture == null)
		{
			for (int playerIndex = 0; playerIndex < _playerHands.Count && placeholderTexture == null; playerIndex++)
			{
				if (_playerHands[playerIndex].Count > 0)
				{
					placeholderTexture = _playerHands[playerIndex][0].Texture;
				}
			}
		}

		if (placeholderTexture == null
			|| ownerPlayerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		_playerItemHands[ownerPlayerIndex].Add(new ItemCard(
			DebugPendingTargetHandSelectionItemCardId,
			placeholderTexture));
		_isProcessingCardPlay = true;
		StartPendingItemSelection(
			DebugPendingTargetHandSelectionItemCardId,
			ownerPlayerIndex,
			_playerItemHands[ownerPlayerIndex].Count - 1,
			_turnSequenceGeneration);
		return true;
	}

	public string DebugGetPendingItemSelectionModeName()
	{
		return _pendingItemSelectionMode.ToString();
	}

	public bool DebugPlayFirstAvailableNetworkHostHandCard()
	{
		if (!IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| _isOpeningDealAnimationPlaying
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| HasPendingItemSelection())
		{
			return false;
		}

		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex != _currentTurnPlayerIndex
			|| localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| _playerHands[localPlayerIndex].Count == 0)
		{
			return false;
		}

		return TryStartNetworkHandCardPlayFromHost(
			localPlayerIndex,
			0,
			_turnSequenceGeneration,
			out _);
	}

	public bool DebugPlayFirstAvailableLocalMainPlayerHandCard()
	{
		if (IsNetworkMultiplayerMode()
			|| _isOpeningDealAnimationPlaying
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| HasPendingItemSelection())
		{
			return false;
		}

		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex != _currentTurnPlayerIndex
			|| localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| _playerHands[localPlayerIndex].Count == 0)
		{
			return false;
		}

		ConfirmLocalHandCardPlay(
			0,
			_turnSequenceGeneration,
			TryGetHandCardStartGlobalPosition(localPlayerIndex, 0));
		return true;
	}

	public void DebugForceNextPendingTargetSelectionAiFallback()
	{
		_debugForceNextPendingTargetPlayerHeuristicFailure = true;
		_debugForceNextPendingTargetHandHeuristicFailure = true;
	}

	public void DebugForceNextPendingHandLimitDiscardAiFallback()
	{
		_debugForceNextPendingHandLimitDiscardHeuristicFailure = true;
	}

	public string DebugGetGameLoopPhaseName()
	{
		return GetGameLoopPhase().ToString();
	}

	public int DebugGetCurrentTurnPlayerIndex()
	{
		return _currentTurnPlayerIndex;
	}

	public int DebugGetLocalPlayerViewSlotIndex()
	{
		return GetPlayerViewSlotIndex(GetLocalHandPlayerIndex());
	}

	public string DebugGetLocalSeatControlModeName()
	{
		return TryGetSeatControlStateForUi(GetLocalHandPlayerIndex(), out SeatControlState state)
			? state.Mode.ToString()
			: string.Empty;
	}

	public void DebugSetNetworkVisualPlaybackDelaySeconds(double seconds)
	{
		_debugNetworkVisualPlaybackDelaySeconds = Math.Max(0.0, seconds);
	}

	public int DebugGetTotalHandCardCountForPlayer(int playerIndex)
	{
		return GetTotalHandCardCount(playerIndex);
	}

	public Vector2 DebugGetCardPileCaptureTargetGlobalPosition(int playerIndex)
	{
		return GetCardPileCaptureTargetGlobalPosition(playerIndex);
	}

	public bool DebugAddScoreAndCheckWinner(int playerIndex, int points)
	{
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| points <= 0
			|| IsGameOver())
		{
			return false;
		}

		AddPlayerScore(playerIndex, points);
		CheckForWinner();
		SendBoardSnapshotIfHost();
		return true;
	}

	public bool DebugSetPlayerScore(int playerIndex, int score)
	{
		if (playerIndex < 0 || playerIndex >= player_count || score < 0)
		{
			return false;
		}

		_playerScores[playerIndex] = score;
		UpdatePlayerScoreMeter(playerIndex, false);
		return true;
	}

	public bool DebugIsNetworkVisualQueueProcessing()
	{
		return _networkVisualQueueProcessing;
	}

	public bool DebugIsReconnectCatchupPendingCompletion()
	{
		return _networkReconnectCatchupPendingCompletion;
	}

	public bool DebugIsReconnectOverlayVisible()
	{
		return _networkReconnectOverlayPanel?.Visible == true;
	}

	public int DebugGetCaptureDamageTargetPlayerIndexForHandCard(int playerIndex, int handIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return -1;
		}

		PlayingCard playedCard = _playerHands[playerIndex][handIndex];
		CardPlayPlan plan = ResolveCardPlayPlan(playedCard);
		if (plan.CaptureStartIndex < 0)
		{
			return -1;
		}

		int scorePlayerIndex = ScoreService.GetCapturedScorePlayerIndex(
			ToCaptureScoringTableCards(),
			plan.CaptureStartIndex,
			playerIndex,
			player_count);
		return adventure_mode
			? GetAdventureDamageTargetPlayerIndex(scorePlayerIndex)
			: scorePlayerIndex;
	}

	public int DebugGetCapturePointsForHandCard(int playerIndex, int handIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return 0;
		}

		PlayingCard playedCard = _playerHands[playerIndex][handIndex];
		CardPlayPlan plan = ResolveCardPlayPlan(playedCard);
		if (plan.CaptureStartIndex < 0)
		{
			return 0;
		}

		int scorePlayerIndex = ScoreService.GetCapturedScorePlayerIndex(
			ToCaptureScoringTableCards(),
			plan.CaptureStartIndex,
			playerIndex,
			player_count);
		return GetCapturedTablePoints(plan.CaptureStartIndex, scorePlayerIndex);
	}

	public bool DebugIsAdventureUpgradeSelectionVisible()
	{
		return ((_selectionOverlay != null
				&& GodotObject.IsInstanceValid(_selectionOverlay)
				&& _selectionOverlay.IsVisibleInTree())
			|| (_adventureUpgradeSelectionOverlay != null
				&& GodotObject.IsInstanceValid(_adventureUpgradeSelectionOverlay)
				&& _adventureUpgradeSelectionOverlay.IsVisibleInTree()))
			&& _adventureUpgradeSelectionCompletionSource != null;
	}

	public int DebugGetAdventureUpgradeOptionCount()
	{
		if (_adventureUpgradeSelectionCompletionSource != null
			&& _adventureUpgradeSelectionOverlay != null
			&& GodotObject.IsInstanceValid(_adventureUpgradeSelectionOverlay)
			&& _adventureUpgradeSelectionOverlay.IsVisibleInTree())
		{
			int visibleCount = 0;
			for (int optionIndex = 0; optionIndex < 3; optionIndex++)
			{
				Button button = _adventureUpgradeSelectionOverlay.GetNode<Button>($"Margin/Layout/CardsRow/Option{optionIndex + 1}Button");
				if (button.Visible && !button.Disabled)
				{
					visibleCount++;
				}
			}

			return visibleCount;
		}

		return _adventureUpgradeSelectionCompletionSource != null
			? _selectionOverlayActions.Count
			: 0;
	}

	public bool DebugSelectAdventureUpgradeOption(int optionIndex)
	{
		if (_adventureUpgradeSelectionCompletionSource == null || optionIndex < 0)
		{
			return false;
		}

		if (_adventureUpgradeSelectionOverlay != null
			&& GodotObject.IsInstanceValid(_adventureUpgradeSelectionOverlay)
			&& _adventureUpgradeSelectionOverlay.IsVisibleInTree())
		{
			int optionCount = DebugGetAdventureUpgradeOptionCount();
			if (optionIndex >= optionCount)
			{
				return false;
			}

			Button button = _adventureUpgradeSelectionOverlay.GetNode<Button>($"Margin/Layout/CardsRow/Option{optionIndex + 1}Button");
			button.EmitSignal(BaseButton.SignalName.Pressed);
			return true;
		}

		if (optionIndex >= _selectionOverlayActions.Count)
		{
			return false;
		}

		_selectionOverlayActions[optionIndex].Invoke();
		return true;
	}

	public int DebugGetAdventureAcquiredUpgradeCount()
	{
		return AdventureUpgradeRunState.GetAcquiredUpgradeIds().Count;
	}

	public int DebugGetPendingAdventureUpgradeTriggerCount()
	{
		return _pendingAdventureUpgradeTriggers.Count;
	}

	public int DebugGetPendingAdventureWinnerPlayerIndex()
	{
		return _pendingAdventureWinnerPlayerIndex;
	}

	public bool DebugIsAdventureUpgradeResolverBusy()
	{
		return _isResolvingAdventureUpgradeSelection;
	}

	public int DebugGetPlayerScoreValue(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerScores.Length)
		{
			return -1;
		}

		return _playerScores[playerIndex];
	}

	private bool ConsumeDebugForceNextPendingTargetPlayerHeuristicFailure()
	{
		if (!_debugForceNextPendingTargetPlayerHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingTargetPlayerHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextPendingTargetHandHeuristicFailure()
	{
		if (!_debugForceNextPendingTargetHandHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingTargetHandHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextPendingHandLimitDiscardHeuristicFailure()
	{
		if (!_debugForceNextPendingHandLimitDiscardHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingHandLimitDiscardHeuristicFailure = false;
		return true;
	}

	private double GetDebugNetworkVisualPlaybackDelaySeconds()
	{
		return _debugNetworkVisualPlaybackDelaySeconds;
	}
}
