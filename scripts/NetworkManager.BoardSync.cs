using System;
using System.Collections.Generic;
using Godot;

public partial class NetworkManager
{
	internal void BroadcastCardPlayedEventFromHost(
		int actorSeatIndex,
		int handIndex,
		string playedCardCode,
		bool playedCardIsFaceUp,
		NetworkHandPlayVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.PlayHandCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = actorSeatIndex,
			[NetworkBoardVisualEventTypes.HandIndexKey] = handIndex,
			[NetworkBoardVisualEventTypes.PlayedCardCodeKey] = playedCardCode,
			[NetworkBoardVisualEventTypes.PlayedCardIsFaceUpKey] = playedCardIsFaceUp,
			[NetworkBoardVisualEventTypes.TableCardIndexKey] = visualSync.TableCardIndex,
			[NetworkBoardVisualEventTypes.MatchingTableCardIndexKey] = visualSync.MatchingTableCardIndex,
			[NetworkBoardVisualEventTypes.MatchedTableCardFlippedKey] = visualSync.MatchedTableCardFlipped,
			[NetworkBoardVisualEventTypes.BorrowTideAdjustedRankKey] = visualSync.BorrowTideAdjustedRank,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		BroadcastBoardVisualEventFromHost(payload);
	}

	internal void BroadcastItemCardPlayedVisualEventFromHost(
		int actorSeatIndex,
		NetworkItemPlayVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.PlayItemCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = actorSeatIndex,
			[NetworkBoardVisualEventTypes.HandIndexKey] = visualSync.ItemHandIndex,
			[NetworkBoardVisualEventTypes.ItemCardIdKey] = visualSync.ItemCardId,
			[NetworkBoardVisualEventTypes.PlayPresentationKey] = visualSync.PlayPresentation,
			[NetworkBoardVisualEventTypes.TargetTableCardIndexKey] = visualSync.TargetTableCardIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		if (visualSync.PlayPresentation == CardPlayPresentationTypes.TableOverlay)
		{
			BroadcastBoardVisualEventFromHost(payload);
			return;
		}

		SendBoardVisualEventToSeatFromHost(actorSeatIndex, payload);
	}

	internal void BroadcastCaptureVisualEventFromHost(
		NetworkCaptureVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.CaptureToDeck,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = visualSync.PlayerIndex,
			[NetworkBoardVisualEventTypes.CaptureStartIndexKey] = visualSync.CaptureStartIndex,
			[NetworkBoardVisualEventTypes.ExtraTableCardIndexesKey] = new Godot.Collections.Array<int>(visualSync.ExtraTableCardIndexes),
			[NetworkBoardVisualEventTypes.FlightStaggerSecondsKey] = visualSync.FlightStaggerSeconds,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		BroadcastBoardVisualEventFromHost(payload);
	}

	internal void BroadcastCaptureRevealVisualEventFromHost(
		NetworkCaptureRevealVisualSync visualSync,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.CaptureReveal,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = visualSync.PlayerIndex,
			[NetworkBoardVisualEventTypes.RevealTableCardIndexesKey] = new Godot.Collections.Array<int>(visualSync.RevealTableCardIndexes),
			[NetworkBoardVisualEventTypes.RevealCardCodesKey] = new Godot.Collections.Array<string>(visualSync.RevealCardCodes),
			[NetworkBoardVisualEventTypes.RevealAttachedItemCardIdsCsvKey] = new Godot.Collections.Array<string>(visualSync.RevealAttachedItemCardIdsCsv),
			[NetworkBoardVisualEventTypes.RevealPointValuesKey] = new Godot.Collections.Array<int>(visualSync.RevealPointValues),
			[NetworkBoardVisualEventTypes.RevealPointDoubleFlagsKey] = new Godot.Collections.Array<bool>(visualSync.RevealPointDoubleFlags),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(visualSync.PlayerIndex, payload);
	}

	internal void BroadcastDrawCardVisualEventFromHost(
		int playerIndex,
		int displayIndex,
		string drawnCardCode,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
				IsHost,
				_state,
				player.IsConnected,
				player.IsHost))
			{
				continue;
			}

			var payload = new Godot.Collections.Dictionary
			{
				[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.DrawCard,
				[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = playerIndex,
				[NetworkBoardVisualEventTypes.DisplayIndexKey] = displayIndex,
				[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
				[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
			};
			if (player.SeatIndex == playerIndex && !string.IsNullOrWhiteSpace(drawnCardCode))
			{
				payload[NetworkBoardVisualEventTypes.DrawnCardCodeKey] = drawnCardCode;
			}

			RpcId(player.PeerId, MethodName.RpcCardPlayedEvent, payload);
		}
	}

	internal void BroadcastDeckShuffleVisualEventFromHost(int playerIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.DeckShuffle,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = playerIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(playerIndex, payload);
	}

	internal void BroadcastStealHandCardVisualEventFromHost(
		int ownerSeatIndex,
		int targetSeatIndex,
		int targetHandIndex,
		string stolenCardCode,
		IReadOnlyList<string> attachedItemCardIds,
		string stolenItemCardId,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.StealHandCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerSeatIndex,
			[NetworkBoardVisualEventTypes.TargetSeatIndexKey] = targetSeatIndex,
			[NetworkBoardVisualEventTypes.TargetHandIndexKey] = targetHandIndex,
			[NetworkBoardVisualEventTypes.PlayedCardCodeKey] = stolenCardCode,
			[NetworkBoardVisualEventTypes.ItemCardIdKey] = stolenItemCardId,
			[NetworkBoardVisualEventTypes.AttachedItemCardIdsKey] = new Godot.Collections.Array<string>(attachedItemCardIds ?? Array.Empty<string>()),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerSeatIndex, payload);
	}

	internal void BroadcastStealDeckCardsVisualEventFromHost(
		int ownerSeatIndex,
		int targetSeatIndex,
		int movedCardCount,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame || movedCardCount <= 0)
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.StealDeckCards,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerSeatIndex,
			[NetworkBoardVisualEventTypes.TargetSeatIndexKey] = targetSeatIndex,
			[NetworkBoardVisualEventTypes.MovedCardCountKey] = movedCardCount,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerSeatIndex, payload);
	}

	internal void BroadcastLensRevealVisualEventFromHost(
		int ownerPlayerIndex,
		Vector2 splitGlobalCenter,
		IReadOnlyList<int> targetIndexes,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(ownerPlayerIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.LensReveal,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerPlayerIndex,
			[NetworkBoardVisualEventTypes.SplitCenterXKey] = splitGlobalCenter.X,
			[NetworkBoardVisualEventTypes.SplitCenterYKey] = splitGlobalCenter.Y,
			[NetworkBoardVisualEventTypes.TargetTableCardIndexesKey] = new Godot.Collections.Array<int>(targetIndexes),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	internal void BroadcastTableShuffleVisualEventFromHost(
		int ownerPlayerIndex,
		IReadOnlyList<int> targetIndexes,
		int turnSequenceGeneration,
		int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.TableShuffle,
			[NetworkBoardVisualEventTypes.ShuffleTargetIndexesKey] = new Godot.Collections.Array<int>(targetIndexes),
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void BroadcastQuestionRefreshVisualEventFromHost(int ownerPlayerIndex, int tableCardIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.QuestionRefresh,
			[NetworkBoardVisualEventTypes.TableCardIndexKey] = tableCardIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void BroadcastBombPairVisualEventFromHost(int ownerPlayerIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.BombPair,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void BroadcastMantaTideShiftVisualEventFromHost(int ownerPlayerIndex, int turnSequenceGeneration, int snapshotId)
	{
		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.MantaTideShift,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
	}

	internal void SendQuestionRewardVisualEventToSeatFromHost(
		int ownerSeatIndex,
		QuestionCardRewardAnimation rewardAnimation,
		int turnSequenceGeneration,
		int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(ownerSeatIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		var payload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.QuestionReward,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = ownerSeatIndex,
			[NetworkBoardVisualEventTypes.ItemCardIdKey] = rewardAnimation.ItemCard.Id,
			[NetworkBoardVisualEventTypes.HandIndexKey] = rewardAnimation.ItemHandIndex,
			[NetworkBoardVisualEventTypes.StartGlobalXKey] = rewardAnimation.StartGlobalPosition.X,
			[NetworkBoardVisualEventTypes.StartGlobalYKey] = rewardAnimation.StartGlobalPosition.Y,
			[NetworkBoardVisualEventTypes.RequiresMagnetAttachmentKey] = rewardAnimation.RequiresMagnetAttachment,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	private void BroadcastBoardVisualEventFromHost(Godot.Collections.Dictionary payload)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!NetworkReconnectRules.ShouldSendRealtimeBoardEventToPeer(
				IsHost,
				_state,
				player.IsConnected,
				player.IsHost))
			{
				continue;
			}

			RpcId(player.PeerId, MethodName.RpcCardPlayedEvent, payload);
		}
	}

	private void SendBoardVisualEventToSeatFromHost(int seatIndex, Godot.Collections.Dictionary payload)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(seatIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	public void SendInitialBoardSnapshotsFromHost(GameBoardScene gameBoard)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		SendReconnectCredentialsToConnectedPeers();
		SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
	}

	public void SendBoardSnapshotsFromHost(GameBoardScene gameBoard, int snapshotId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		foreach (NetworkPlayerPublicState player in _players)
		{
			if (!player.IsConnected)
			{
				continue;
			}

			if (player.IsHost)
			{
				continue;
			}

			SendBoardSnapshotToPeerFromHost(gameBoard, player.PeerId, player.SeatIndex, snapshotId);
		}
	}

	private void SendBoardSnapshotToPeerFromHost(GameBoardScene gameBoard, int peerId, int seatIndex, int snapshotId)
	{
		if (!IsHost
			|| _state != NetworkSessionState.InGame
			|| peerId <= 0
			|| seatIndex < 0)
		{
			return;
		}

		Godot.Collections.Dictionary snapshot = gameBoard.BuildNetworkBoardSnapshotForSeat(seatIndex, snapshotId);
		RpcId(peerId, MethodName.RpcBoardSnapshot, snapshot);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, handIndex);
			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat)
			{
				SendBoardSnapshotToPeerFromHost(
					gameBoardForRejectedSeat,
					senderPeerId,
					actorSeatIndex,
					gameBoardForRejectedSeat.CreateNextNetworkSnapshotId());
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			return;
		}

		if (!gameBoard.TryStartNetworkHandCardPlayFromHost(
			actorSeatIndex,
			handIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, handIndex);
			SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayItemCard(int itemHandIndex, int turnSequenceGeneration)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat)
			{
				SendBoardSnapshotToPeerFromHost(
					gameBoardForRejectedSeat,
					senderPeerId,
					actorSeatIndex,
					gameBoardForRejectedSeat.CreateNextNetworkSnapshotId());
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (!gameBoard.TryStartNetworkItemCardPlayFromHost(
			actorSeatIndex,
			itemHandIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, itemHandIndex);
			SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPendingItemSelection(int targetPlayerIndex, int targetHandIndex, int turnSequenceGeneration)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, -1);
			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat)
			{
				SendBoardSnapshotToPeerFromHost(
					gameBoardForRejectedSeat,
					senderPeerId,
					actorSeatIndex,
					gameBoardForRejectedSeat.CreateNextNetworkSnapshotId());
			}
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			return;
		}

		if (!gameBoard.TryStartNetworkPendingItemSelectionFromHost(
			actorSeatIndex,
			targetPlayerIndex,
			targetHandIndex,
			turnSequenceGeneration,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, -1);
			SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectPlayer(string matchId, string reconnectToken, string stableUserId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.IsNullOrWhiteSpace(reconnectToken))
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "TokenMismatch", -1);
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "ActionInProgress", -1);
			return;
		}

		if (!TryFindSeatControlStateByToken(reconnectToken, out SeatControlState currentSeatState, out int stateIndex))
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "TokenMismatch", -1);
			return;
		}

		if (NetworkReconnectRules.ShouldDeferReconnectClaimWhileSeatStillConnected(
			currentSeatState.Mode,
			currentSeatState.PeerId,
			senderPeerId,
			currentSeatState.ReconnectToken,
			reconnectToken,
			currentSeatState.StableUserId,
			stableUserId))
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, "ActionInProgress", currentSeatState.SeatIndex);
			return;
		}

		_reconnectClaimsBySeat.TryGetValue(currentSeatState.SeatIndex, out ReconnectClaimRecord existingClaim);
		ReconnectClaimResult result = SeatControlRules.ApplyReconnectClaim(
			_config.MatchId,
			gameBoard.IsNetworkMatchClaimable(),
			currentSeatState,
			existingClaim,
			matchId,
			reconnectToken,
			stableUserId,
			senderPeerId);
		if (!result.Accepted)
		{
			RpcId(senderPeerId, MethodName.RpcReconnectResult, false, result.RejectReason.ToString(), currentSeatState.SeatIndex);
			return;
		}

		_seatControlStates[stateIndex] = result.SeatControlState;
		_reconnectClaimsBySeat[currentSeatState.SeatIndex] = result.ClaimRecord;
		if (currentSeatState.PeerId != senderPeerId)
		{
			BindReconnectedPeerToSeat(currentSeatState.SeatIndex, senderPeerId);
		}

		RpcId(senderPeerId, MethodName.RpcReconnectResult, true, string.Empty, currentSeatState.SeatIndex);
		if (result.StateChanged)
		{
			SendReconnectCredentialsToConnectedPeers();
		}

		if (result.ShouldSendSnapshot)
		{
			SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
		}
	}

	private void SendRejectBoardAction(
		int peerId,
		MultiplayerBoardActionRejectReason reason,
		int turnSequenceGeneration,
		int rejectedHandIndex)
	{
		if (peerId <= 0)
		{
			return;
		}

		RpcId(peerId, MethodName.RpcRejectBoardAction, reason.ToString(), turnSequenceGeneration, rejectedHandIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRejectBoardAction(string reason, int turnSequenceGeneration, int rejectedHandIndex)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.RejectNetworkBoardAction(reason, turnSequenceGeneration, rejectedHandIndex);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCardPlayedEvent(Godot.Collections.Dictionary payload)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.QueueNetworkCardPlayedEvent(payload);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.ApplyNetworkBoardSnapshot(snapshot);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveReconnectCredentials(string matchId, int seatIndex, string reconnectToken, string stableUserId)
	{
		if (!string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal))
		{
			return;
		}

		if (!string.IsNullOrWhiteSpace(matchId))
		{
			_config = _config with { MatchId = matchId };
		}

		_localReconnectToken = reconnectToken?.Trim() ?? string.Empty;
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectResult(bool accepted, string reason, int seatIndex)
	{
		_reconnectAttemptInFlight = false;
		if (!accepted)
		{
			if (_state == NetworkSessionState.Reconnecting
				&& string.Equals(reason, "ActionInProgress", StringComparison.Ordinal))
			{
				ScheduleReconnectRetry();
				return;
			}

			Fail(string.IsNullOrWhiteSpace(reason) ? "Reconnect rejected." : $"Reconnect rejected: {reason}");
			return;
		}

		UpdateLocalPlayerAfterReconnect(seatIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcHostEndedMatch(string message)
	{
		if (IsHost)
		{
			return;
		}

		_hostEndedMatchPending = true;
		_hostEndedMatchMessage = string.IsNullOrWhiteSpace(message) ? "Host disconnected." : message;
		_reconnectAttemptInFlight = false;
		_nextReconnectAttemptAt = 0.0;
		Fail(_hostEndedMatchMessage);
	}
}
