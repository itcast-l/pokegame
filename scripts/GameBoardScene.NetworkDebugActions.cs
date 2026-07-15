using Godot;

public partial class GameBoardScene
{
	private const string NetworkDebugActionNone = "None";
	private const string NetworkDebugActionBaseCard = "BaseCard";
	private const string NetworkDebugActionItemCard = "ItemCard";
	private const string NetworkDebugActionPendingSelection = "PendingSelection";

	public Godot.Collections.Dictionary DebugGetFirstLegalNetworkAction()
	{
		var action = new Godot.Collections.Dictionary
		{
			["kind"] = NetworkDebugActionNone,
			["seat"] = GetLocalHandPlayerIndex(),
			["hand_index"] = -1,
			["item_hand_index"] = -1,
			["target_player_index"] = -1,
			["target_hand_index"] = -1,
			["turn_sequence_generation"] = _turnSequenceGeneration,
		};

		int localSeat = GetLocalHandPlayerIndex();
		if (!IsNetworkMultiplayerMode()
			|| localSeat < 0
			|| localSeat >= player_count
		|| IsGameOver()
			|| _isOpeningDealAnimationPlaying
			|| _networkMigrationInputLocked
		|| _isProcessingCardPlay
		|| _isResolvingTurnSequence
		|| (!IsNetworkHost() && NetworkVisualPlayback.IsProcessing)
		|| _networkPendingHandIndex >= 0
		|| _networkPendingItemHandIndex >= 0
		|| _networkPendingSelectionRequestInFlight)
		{
			return action;
		}

		if (HasPendingItemSelection())
		{
			if (_pendingItemOwnerIndex != localSeat)
			{
				return action;
			}

			switch (_pendingItemSelectionMode)
			{
				case PendingItemSelectionMode.TargetPlayer:
					for (int targetPlayerIndex = 0; targetPlayerIndex < player_count; targetPlayerIndex++)
					{
						if (!IsItemTargetPlayerCandidate(_pendingItemCardId, localSeat, targetPlayerIndex))
						{
							continue;
						}

						return WithAction(action, NetworkDebugActionPendingSelection, targetPlayerIndex, -1, -1);
					}
					break;
				case PendingItemSelectionMode.TargetHandCard:
					if (_revealedPlayerHandIndex >= 0)
					{
						int targetPlayerIndex = _revealedPlayerHandIndex;
						int targetCount = GetTotalHandCardCount(targetPlayerIndex);
						for (int targetHandIndex = 0; targetHandIndex < targetCount; targetHandIndex++)
						{
							if (IsPendingItemTargetValid(_pendingItemCardId, localSeat, targetPlayerIndex, targetHandIndex))
							{
								return WithAction(action, NetworkDebugActionPendingSelection, targetPlayerIndex, targetHandIndex, -1);
							}
						}
					}
					break;
				case PendingItemSelectionMode.HandLimitDiscard:
					int totalHandCards = GetTotalHandCardCount(localSeat);
					for (int displayIndex = 0; displayIndex < totalHandCards; displayIndex++)
					{
						if (IsValidHandLimitDiscardSelection(localSeat, displayIndex))
						{
							return WithAction(action, NetworkDebugActionPendingSelection, -1, displayIndex, -1);
						}
					}
					break;
				case PendingItemSelectionMode.AttachmentRewardHandCard:
					if (localSeat < _playerHands.Count)
					{
						for (int handIndex = 0; handIndex < _playerHands[localSeat].Count; handIndex++)
						{
							if (IsValidMagnetAttachmentHandIndex(localSeat, handIndex, _pendingItemCardId))
							{
								return WithAction(action, NetworkDebugActionPendingSelection, -1, handIndex, -1);
							}
						}
					}
					break;
			}

			return action;
		}

		if (GetGameLoopPhase() != GameLoopPhase.AwaitingMainPlayerAction
			|| _currentTurnPlayerIndex != localSeat)
		{
			return action;
		}

		if (localSeat < _playerItemHands.Count)
		{
			for (int itemHandIndex = 0; itemHandIndex < _playerItemHands[localSeat].Count; itemHandIndex++)
			{
				if (CanPlayItemCard(localSeat, _playerItemHands[localSeat][itemHandIndex].Id))
				{
					return WithAction(action, NetworkDebugActionItemCard, -1, -1, itemHandIndex);
				}
			}
		}

		if (localSeat < _playerHands.Count)
		{
			if (_playerHands[localSeat].Count > 0)
			{
				return WithAction(action, NetworkDebugActionBaseCard, -1, -1, -1, 0);
			}
		}

		return action;
	}

	public bool DebugSubmitFirstLegalNetworkAction()
	{
		Godot.Collections.Dictionary action = DebugGetFirstLegalNetworkAction();
		string kind = action["kind"].AsString();
		int generation = action["turn_sequence_generation"].AsInt32();
		if (kind == NetworkDebugActionPendingSelection)
		{
			RequestNetworkPendingItemSelection(
				action["target_player_index"].AsInt32(),
				action["target_hand_index"].AsInt32(),
				generation);
			return true;
		}

		if (kind == NetworkDebugActionItemCard)
		{
			RequestNetworkItemCardPlay(action["item_hand_index"].AsInt32(), generation);
			return true;
		}

		if (kind == NetworkDebugActionBaseCard)
		{
			int handIndex = action["hand_index"].AsInt32();
			if (IsNetworkHost())
			{
				ConfirmNetworkHostHandCardPlay(handIndex, generation);
			}
			else
			{
				RequestNetworkHandCardPlay(handIndex, generation);
			}

			return true;
		}

		return false;
	}

	public string DebugGetNetworkBoardViewFingerprintForSeat(int recipientSeatIndex)
	{
		if (recipientSeatIndex < 0 || recipientSeatIndex >= player_count)
		{
			return string.Empty;
		}

		int snapshotId = IsNetworkHost() ? _networkSnapshotId : _networkLastAppliedSnapshotId;
		return NetworkBoardViewFingerprint.Compute(BuildNetworkBoardSnapshotForSeat(recipientSeatIndex, snapshotId));
	}

	public string DebugGetAppliedNetworkBoardViewFingerprint()
	{
		return DebugGetNetworkBoardViewFingerprintForSeat(GetLocalHandPlayerIndex());
	}

	public int DebugGetNetworkSnapshotId()
	{
		return IsNetworkHost() ? _networkSnapshotId : _networkLastAppliedSnapshotId;
	}

	public Godot.Collections.Dictionary DebugGetNetworkActionState()
	{
		return new Godot.Collections.Dictionary
		{
			["processing_card_play"] = _isProcessingCardPlay,
			["resolving_turn_sequence"] = _isResolvingTurnSequence,
			["opening_deal_animation"] = _isOpeningDealAnimationPlaying,
			["pending_item_selection"] = HasPendingItemSelection(),
			["pending_selection_mode"] = _pendingItemSelectionMode.ToString(),
			["pending_selection_owner"] = _pendingItemOwnerIndex,
			["pending_selection_generation"] = _pendingItemTurnSequenceGeneration,
			["current_turn"] = _currentTurnPlayerIndex,
			["generation"] = _turnSequenceGeneration,
			["local_seat"] = GetLocalHandPlayerIndex(),
			["network_visual_snapshot"] = _networkAuthoritativeVisualSnapshotId,
			["snapshot"] = DebugGetNetworkSnapshotId(),
			["visual_processing"] = NetworkVisualPlayback.IsProcessing,
			["visual_pending_events"] = NetworkVisualPlayback.PendingEventCount,
			["visual_fast_forwarding"] = NetworkVisualPlayback.IsFastForwarding,
			["authority_action_id"] = _networkAuthorityActionState.ActionId,
			["authority_action_client_id"] = _networkAuthorityActionState.ClientActionId,
			["authority_action_kind"] = _networkAuthorityActionState.Kind.ToString(),
			["authority_action_phase"] = _networkAuthorityActionState.Phase.ToString(),
		};
	}

	public bool DebugNetworkBoardViewHasPrivateLeak(int recipientSeatIndex)
	{
		if (recipientSeatIndex < 0 || recipientSeatIndex >= player_count)
		{
			return true;
		}

		Godot.Collections.Dictionary snapshot = BuildNetworkBoardSnapshotForSeat(
			recipientSeatIndex,
			IsNetworkHost() ? _networkSnapshotId : _networkLastAppliedSnapshotId);
		return snapshot.ContainsKey("hidden_hand_cards")
			|| snapshot.ContainsKey("hidden_item_hand_cards")
			|| snapshot.ContainsKey("all_hand_cards")
			|| snapshot.ContainsKey("all_item_hand_cards");
	}

	private static Godot.Collections.Dictionary WithAction(
		Godot.Collections.Dictionary action,
		string kind,
		int targetPlayerIndex,
		int targetHandIndex,
		int itemHandIndex,
		int handIndex = -1)
	{
		action["kind"] = kind;
		action["target_player_index"] = targetPlayerIndex;
		action["target_hand_index"] = targetHandIndex;
		action["item_hand_index"] = itemHandIndex;
		action["hand_index"] = handIndex;
		return action;
	}
}
