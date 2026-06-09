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
