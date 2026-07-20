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
			[NetworkBoardVisualEventTypes.RedirectedTableCardIndexKey] = visualSync.RedirectedTableCardIndex,
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

			StampBoardVisualEvent(payload);
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
		StampBoardVisualEvent(payload);
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
		StampBoardVisualEvent(payload);
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

			StampBoardVisualEvent(payload);
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

		StampBoardVisualEvent(payload);
		RpcId(recipient.PeerId, MethodName.RpcCardPlayedEvent, payload);
	}

	private void StampBoardVisualEvent(Godot.Collections.Dictionary payload)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			payload[NetworkBoardVisualEventTypes.SessionIdKey] = _config.SessionId;
			payload[NetworkBoardVisualEventTypes.MatchIdKey] = _config.MatchId;
			payload[NetworkBoardVisualEventTypes.AuthorityEpochKey] = gameBoard.NetworkAuthorityEpoch;
			payload[NetworkBoardVisualEventTypes.CheckpointIdKey] = gameBoard.NetworkCheckpointId;
		}
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
		if (!IsHost
			|| _state != NetworkSessionState.InGame
			|| !gameBoard.CanPublishNetworkBoardSnapshot)
		{
			return;
		}

		CommitAuthorityCheckpointFromBoard(gameBoard);

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
			|| !gameBoard.CanPublishNetworkBoardSnapshot
			|| peerId <= 0
			|| seatIndex < 0)
		{
			return;
		}

		Godot.Collections.Dictionary snapshot = gameBoard.BuildNetworkBoardSnapshotForSeat(seatIndex, snapshotId);
		RpcId(peerId, MethodName.RpcBoardSnapshot, snapshot);
	}

	private void RpcRequestPlayHandCard(int handIndex, int turnSequenceGeneration)
	{
		NetworkRpcContext context = GetCurrentRpcContext();
		RpcRequestPlayHandCard(context.SessionId, context.MatchId, context.AuthorityEpoch, handIndex, turnSequenceGeneration, 0);
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayHandCard(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int handIndex,
		int turnSequenceGeneration,
		long clientActionId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, handIndex);
			return;
		}

		if (IsDuplicateOrOlderClientAction(actorSeatIndex, clientActionId))
		{
			if (GetTree().CurrentScene is GameBoardScene duplicateBoard
				&& !duplicateBoard.IsNetworkGameOver())
			{
				SendBoardSnapshotToPeerFromHost(
					duplicateBoard,
					senderPeerId,
					actorSeatIndex,
					duplicateBoard.CreateNextNetworkSnapshotId());
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, handIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat
				&& !gameBoardForRejectedSeat.IsNetworkGameOver())
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
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, handIndex);
			if (!gameBoard.IsNetworkGameOver())
			{
				SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPlayItemCard(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int itemHandIndex,
		int turnSequenceGeneration,
		long clientActionId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, itemHandIndex);
			return;
		}

		if (IsDuplicateOrOlderClientAction(actorSeatIndex, clientActionId))
		{
			if (GetTree().CurrentScene is GameBoardScene duplicateBoard
				&& !duplicateBoard.IsNetworkGameOver())
			{
				SendBoardSnapshotToPeerFromHost(
					duplicateBoard,
					senderPeerId,
					actorSeatIndex,
					duplicateBoard.CreateNextNetworkSnapshotId());
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, itemHandIndex);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat
				&& !gameBoardForRejectedSeat.IsNetworkGameOver())
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
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, itemHandIndex);
			if (!gameBoard.IsNetworkGameOver())
			{
				SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRequestPendingItemSelection(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int selectionIndex,
		int turnSequenceGeneration,
		long clientActionId)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (!MultiplayerBoardSyncRules.TryResolveSeatFromPeer(_players, senderPeerId, out int actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.UnknownSender, turnSequenceGeneration, -1);
			return;
		}

		if (IsDuplicateOrOlderClientAction(actorSeatIndex, clientActionId))
		{
			if (GetTree().CurrentScene is GameBoardScene duplicateBoard
				&& !duplicateBoard.IsNetworkGameOver())
			{
				SendBoardSnapshotToPeerFromHost(
					duplicateBoard,
					senderPeerId,
					actorSeatIndex,
					duplicateBoard.CreateNextNetworkSnapshotId());
			}

			return;
		}

		if (!CanAcceptRemoteBoardActionFromSeat(actorSeatIndex))
		{
			SendRejectBoardAction(senderPeerId, MultiplayerBoardActionRejectReason.ActionInProgress, turnSequenceGeneration, -1);
			if (GetTree().CurrentScene is GameBoardScene gameBoardForRejectedSeat
				&& !gameBoardForRejectedSeat.IsNetworkGameOver())
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
			selectionIndex,
			turnSequenceGeneration,
			clientActionId,
			out MultiplayerBoardActionRejectReason rejectReason))
		{
			SendRejectBoardAction(senderPeerId, rejectReason, turnSequenceGeneration, -1);
			if (!gameBoard.IsNetworkGameOver())
			{
				SendBoardSnapshotToPeerFromHost(gameBoard, senderPeerId, actorSeatIndex, gameBoard.CreateNextNetworkSnapshotId());
			}
		}
	}

	[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReconnectPlayer(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string reconnectToken,
		string stableUserId)
	{
		if (IsHost && (_state == NetworkSessionState.InGame || _state == NetworkSessionState.MigratingHost))
		{
			GD.Print($"MIGRATION_RECONNECT_REQUEST sender={Multiplayer.GetRemoteSenderId()} state={_state} epoch={authorityEpoch} stable={stableUserId} token_len={reconnectToken.Length}");
		}

		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene migratingBoard
			&& migratingBoard.IsNetworkMigrationInputLocked
			&& !IsMigrationReadinessFenceActive())
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ActionInProgress sender={Multiplayer.GetRemoteSenderId()} lock=true");
			SendReconnectResult(
				Multiplayer.GetRemoteSenderId(),
				false,
				"ActionInProgress",
				-1);
			return;
		}

		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ContextMismatch sender={Multiplayer.GetRemoteSenderId()} epoch={authorityEpoch} current={GetCurrentAuthorityEpoch()}");
			return;
		}

		int senderPeerId = Multiplayer.GetRemoteSenderId();
		if (string.IsNullOrWhiteSpace(reconnectToken))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=TokenMismatch sender={senderPeerId} detail=empty");
			SendReconnectResult(senderPeerId, false, "TokenMismatch", -1);
			return;
		}

		if (GetTree().CurrentScene is not GameBoardScene gameBoard)
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ActionInProgress sender={senderPeerId} detail=no_board");
			SendReconnectResult(senderPeerId, false, "ActionInProgress", -1);
			return;
		}

		if (!TryFindSeatControlStateByToken(reconnectToken, out SeatControlState currentSeatState, out int stateIndex))
		{
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=TokenMismatch sender={senderPeerId} detail=unknown_token");
			SendReconnectResult(senderPeerId, false, "TokenMismatch", -1);
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
			GD.Print($"MIGRATION_RECONNECT_REJECT reason=ActionInProgress sender={senderPeerId} seat={currentSeatState.SeatIndex} detail=seat_connected");
			SendReconnectResult(senderPeerId, false, "ActionInProgress", currentSeatState.SeatIndex);
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
			GD.Print($"MIGRATION_RECONNECT_REJECT reason={result.RejectReason} sender={senderPeerId} seat={currentSeatState.SeatIndex}");
			SendReconnectResult(senderPeerId, false, result.RejectReason.ToString(), currentSeatState.SeatIndex);
			return;
		}

		SeatControlState acceptedSeatControlState = result.SeatControlState;
		if (IsMigrationReadinessFenceActive()
			&& acceptedSeatControlState.Mode == SeatControlMode.HumanReclaimPending)
		{
			// Migration input remains locked until every expected claimant has a hard
			// snapshot, so the normal turn-start promotion can happen atomically at
			// the readiness fence instead of leaving recovery waiting for a turn.
			acceptedSeatControlState = acceptedSeatControlState with
			{
				Mode = SeatControlMode.HumanConnected,
			};
		}
		_seatControlStates[stateIndex] = acceptedSeatControlState;
		_reconnectClaimsBySeat[currentSeatState.SeatIndex] = result.ClaimRecord;
		if (currentSeatState.PeerId != senderPeerId)
		{
			BindReconnectedPeerToSeat(currentSeatState.SeatIndex, senderPeerId);
		}

		SendReconnectResult(senderPeerId, true, string.Empty, currentSeatState.SeatIndex);
		GD.Print($"MIGRATION_RECONNECT_ACCEPT sender={senderPeerId} seat={currentSeatState.SeatIndex} stable={stableUserId}");
		if (result.StateChanged)
		{
			SendReconnectCredentialsToConnectedPeers();
		}

			if (result.ShouldSendSnapshot)
			{
				SendBoardSnapshotsFromHost(gameBoard, gameBoard.CreateNextNetworkSnapshotId());
				TryCompleteMigrationReadiness();
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

		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(peerId, MethodName.RpcRejectBoardAction, context.SessionId, context.MatchId, context.AuthorityEpoch, reason.ToString(), turnSequenceGeneration, rejectedHandIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcRejectBoardAction(string sessionId, string matchId, long authorityEpoch, string reason, int turnSequenceGeneration, int rejectedHandIndex)
	{
		if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.RejectNetworkBoardAction(reason, turnSequenceGeneration, rejectedHandIndex);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcCardPlayedEvent(Godot.Collections.Dictionary payload)
	{
		if (!TryReadRpcContext(payload, out NetworkRpcContext context)
			|| !MatchesCurrentRpcContext(
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch))
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.QueueNetworkCardPlayedEvent(payload);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void RpcBoardSnapshot(Godot.Collections.Dictionary snapshot)
	{
		if (!TryReadRpcContext(snapshot, out NetworkRpcContext context)
			|| !MatchesCurrentRpcContext(
				context.SessionId,
				context.MatchId,
				context.AuthorityEpoch,
				allowHigherEpoch: true))
		{
			return;
		}

		if (_state == NetworkSessionState.MigratingHost)
			{
			GD.Print($"MIGRATION_SNAPSHOT_RECEIVED epoch={context.AuthorityEpoch} snapshot={snapshot.GetValueOrDefault("snapshot_id", -1)} checkpoint={snapshot.GetValueOrDefault("checkpoint_id", -1)}");
			}

		if (GetTree().CurrentScene is GameBoardScene gameBoard)
			{
			gameBoard.ApplyNetworkBoardSnapshot(snapshot);
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcReceiveReconnectCredentials(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int seatIndex,
		string reconnectToken,
		string stableUserId)
	{
		bool isInitialMatchCredential = string.IsNullOrWhiteSpace(_config.MatchId)
			&& !string.IsNullOrWhiteSpace(matchId)
			&& string.Equals(_config.SessionId, sessionId, StringComparison.Ordinal)
			&& _state is NetworkSessionState.Lobby or NetworkSessionState.Starting or NetworkSessionState.LoadingScene;
		if ((!isInitialMatchCredential
				&& !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
			|| !string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal))
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
	private void RpcReceiveSessionMigrationToken(
		string sessionId,
		string matchId,
		long authorityEpoch,
		int seatIndex,
		string sessionMigrationToken,
		string stableUserId)
	{
		bool isInitialLobbyCredential = _state == NetworkSessionState.Joining
			&& string.IsNullOrWhiteSpace(_config.MatchId)
			&& string.IsNullOrWhiteSpace(matchId)
			&& !string.IsNullOrWhiteSpace(sessionId);
		if ((!isInitialLobbyCredential
				&& !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
			|| !string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal)
			|| seatIndex < 0
			|| seatIndex >= NetworkSessionDefaults.MaxPlayers)
		{
			return;
		}

		if (isInitialLobbyCredential)
		{
			_config = _config with { SessionId = sessionId };
		}
		ApplySessionMigrationToken(seatIndex, sessionMigrationToken);
		// Lobby credentials can arrive before RpcSyncLobby establishes the local
		// seat. Stable identity keeps the local token usable during early
		// active-lobby migration.
		if (string.Equals(GetStableUserId(), stableUserId, StringComparison.Ordinal))
		{
			_localSessionMigrationToken = sessionMigrationToken.Trim();
		}
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
		private void RpcReconnectResult(string sessionId, string matchId, long authorityEpoch, bool accepted, string reason, int seatIndex)
		{
		if (_state == NetworkSessionState.MigratingHost)
		{
			GD.Print($"MIGRATION_RECONNECT_RESULT accepted={accepted} reason={reason} seat={seatIndex} epoch={authorityEpoch}");
		}

			bool isMigrationResult = _state == NetworkSessionState.MigratingHost
				&& _networkMigrationPlan is { } migrationPlan
				&& authorityEpoch == migrationPlan.TargetEpoch;
			if (!MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch, allowHigherEpoch: isMigrationResult))
			{
				return;
			}
			if (isMigrationResult)
			{
				SetAuthorityEpoch(authorityEpoch);
			}

		_reconnectAttemptInFlight = false;
		if (!accepted)
		{
			if ((_state is NetworkSessionState.Reconnecting or NetworkSessionState.MigratingHost)
				&& string.Equals(reason, "ActionInProgress", StringComparison.Ordinal))
			{
				if (_state == NetworkSessionState.MigratingHost)
				{
					HandleMigrationConnectionFailure();
					return;
				}

				ScheduleReconnectRetry();
				return;
			}

			Fail(string.IsNullOrWhiteSpace(reason) ? "Reconnect rejected." : $"Reconnect rejected: {reason}");
			return;
		}

		UpdateLocalPlayerAfterReconnect(seatIndex);
	}

	private void SendReconnectResult(int peerId, bool accepted, string reason, int seatIndex)
	{
		NetworkRpcContext context = GetCurrentRpcContext();
		RpcId(peerId, MethodName.RpcReconnectResult, context.SessionId, context.MatchId, context.AuthorityEpoch, accepted, reason, seatIndex);
	}

	[Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
	private void RpcHostEndedMatch(
		string sessionId,
		string matchId,
		long authorityEpoch,
		string message)
	{
		if (IsHost
			|| !MatchesCurrentRpcContext(sessionId, matchId, authorityEpoch))
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
