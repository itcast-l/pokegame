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

		StageBoardVisualEventForLocalHost(payload);
		if (LocalSeatIndex != actorSeatIndex)
		{
			SendBoardVisualEventToSeatFromHost(actorSeatIndex, payload);
		}
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

		var hostPayload = new Godot.Collections.Dictionary
		{
			[NetworkBoardVisualEventTypes.EventTypeKey] = NetworkBoardVisualEventTypes.DrawCard,
			[NetworkBoardVisualEventTypes.ActorSeatIndexKey] = playerIndex,
			[NetworkBoardVisualEventTypes.DisplayIndexKey] = displayIndex,
			[NetworkBoardVisualEventTypes.TurnSequenceGenerationKey] = turnSequenceGeneration,
			[NetworkBoardVisualEventTypes.SnapshotIdKey] = snapshotId,
		};
		if (LocalSeatIndex == playerIndex && !string.IsNullOrWhiteSpace(drawnCardCode))
		{
			hostPayload[NetworkBoardVisualEventTypes.DrawnCardCodeKey] = drawnCardCode;
		}
		StageBoardVisualEventForLocalHost(hostPayload);

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

			DispatchBoardVisualEventToPeer(player.PeerId, payload);
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
		SendBoardVisualEventToSeatFromHost(ownerPlayerIndex, payload);
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
		SendBoardVisualEventToSeatFromHost(ownerSeatIndex, payload);
	}

	private void BroadcastBoardVisualEventFromHost(Godot.Collections.Dictionary payload)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		StageBoardVisualEventForLocalHost(payload);

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

			DispatchBoardVisualEventToPeer(player.PeerId, payload);
		}
	}

	private void SendBoardVisualEventToSeatFromHost(int seatIndex, Godot.Collections.Dictionary payload)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return;
		}

		if (LocalSeatIndex == seatIndex)
		{
			StageBoardVisualEventForLocalHost(payload);
			return;
		}

		if (!TryGetConnectedRemotePlayerBySeatIndex(seatIndex, out NetworkPlayerPublicState recipient))
		{
			return;
		}

		DispatchBoardVisualEventToPeer(recipient.PeerId, payload);
	}

	private void StageBoardVisualEventForLocalHost(
		Godot.Collections.Dictionary payload)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			gameBoard.TryStageNetworkAuthorityHostVisualEvent(payload);
		}
	}

	private void DispatchBoardVisualEventToPeer(
		int peerId,
		Godot.Collections.Dictionary payload)
	{
		if (peerId <= 0)
		{
			return;
		}

		if (GetTree().CurrentScene is GameBoardScene gameBoard
			&& gameBoard.TryStageNetworkAuthorityVisualDispatch(peerId, payload))
		{
			return;
		}

		StampBoardVisualEvent(payload);
		RpcId(peerId, MethodName.RpcCardPlayedEvent, payload);
	}

	private void StampBoardVisualEvent(Godot.Collections.Dictionary payload)
	{
		if (GetTree().CurrentScene is GameBoardScene gameBoard)
		{
			payload[NetworkBoardVisualEventTypes.SessionIdKey] = _config.SessionId;
			payload[NetworkBoardVisualEventTypes.MatchIdKey] = _config.MatchId;
			payload[NetworkBoardVisualEventTypes.AuthorityEpochKey] = gameBoard.NetworkAuthorityEpoch;
			payload[NetworkBoardVisualEventTypes.CheckpointIdKey] = gameBoard.NetworkCheckpointId;
			payload[NetworkBoardVisualEventTypes.ActionIdKey] = gameBoard.CurrentNetworkAuthorityActionId;
		}
	}

	internal void PublishCommittedBoardActionFromHost(
		GameBoardScene gameBoard,
		int actorSeatIndex,
		AuthorityRuleTransactionResult transaction)
	{
		if (!IsHost || _state != NetworkSessionState.InGame || !transaction.Committed)
		{
			return;
		}

		if (transaction.ClientActionId > 0
			&& TryGetConnectedRemotePlayerBySeatIndex(actorSeatIndex, out NetworkPlayerPublicState actor))
		{
			RpcId(
				actor.PeerId,
				MethodName.RpcBoardActionCommitted,
				_config.SessionId,
				_config.MatchId,
				transaction.AuthorityEpoch,
				transaction.ActionId,
				transaction.ClientActionId,
				transaction.CheckpointId,
				transaction.SnapshotId,
				transaction.TurnSequenceGeneration);
		}

		foreach (ImmutableNetworkVisualDispatch dispatch in transaction.VisualEvents)
		{
			Godot.Collections.Dictionary payload = dispatch.CreatePayloadCopy();
			StampCommittedBoardVisualEvent(payload, transaction);
			RpcId(dispatch.PeerId, MethodName.RpcCardPlayedEvent, payload);
		}

		SendCommittedBoardSnapshotsFromHost(gameBoard, transaction.SnapshotId);
	}

	private void StampCommittedBoardVisualEvent(
		Godot.Collections.Dictionary payload,
		AuthorityRuleTransactionResult transaction)
	{
		payload[NetworkBoardVisualEventTypes.SessionIdKey] = _config.SessionId;
		payload[NetworkBoardVisualEventTypes.MatchIdKey] = _config.MatchId;
		payload[NetworkBoardVisualEventTypes.AuthorityEpochKey] = transaction.AuthorityEpoch;
		payload[NetworkBoardVisualEventTypes.CheckpointIdKey] = transaction.CheckpointId;
		payload[NetworkBoardVisualEventTypes.ActionIdKey] = transaction.ActionId;
		payload[NetworkBoardVisualEventTypes.SnapshotIdKey] = transaction.SnapshotId;
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

		SendCommittedBoardSnapshotsFromHost(gameBoard, snapshotId);
	}

	private void SendCommittedBoardSnapshotsFromHost(GameBoardScene gameBoard, int snapshotId)
	{
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


}
