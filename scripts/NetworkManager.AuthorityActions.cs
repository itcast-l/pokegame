using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class NetworkManager
{
	private bool EnsureItemCardCatalogReady()
	{
		if (ItemCardCatalogPreflight.TryValidate(this, out _))
		{
			return true;
		}

		EmitSignal(SignalName.ErrorRaised, ItemCardCatalogPreflight.ErrorBodyKey);
		return false;
	}

	private readonly Dictionary<int, long> _lastAcceptedClientActionIds = new();
	private long _nextClientActionId;
	private AuthorityCheckpointEnvelope _latestAuthorityCheckpointEnvelope = AuthorityCheckpointEnvelope.Empty;
	private int _authorityCheckpointCommitFailureCount;
	private bool _authorityCheckpointCommitFailureReported;

	internal long NextClientActionId()
	{
		_nextClientActionId++;
		return _nextClientActionId;
	}

	internal NetworkRpcContext GetCurrentRpcContext()
	{
		return new NetworkRpcContext(
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch());
	}

	internal bool MatchesCurrentRpcContext(
		string sessionId,
		string matchId,
		long authorityEpoch,
		bool allowHigherEpoch = false)
	{
		NetworkRpcContext incoming = new(sessionId ?? string.Empty, matchId ?? string.Empty, authorityEpoch);
		return NetworkProtocolRules.Matches(
			incoming,
			_config.SessionId,
			_config.MatchId,
			GetCurrentAuthorityEpoch(),
			allowHigherEpoch);
	}

	internal static bool TryReadRpcContext(
		Godot.Collections.Dictionary payload,
		out NetworkRpcContext context)
	{
		context = default;
		if (!NetworkVariantReader.TryReadString(payload, "session_id", NetworkProtocol.MaxSessionIdLength, out string sessionId, allowEmpty: false)
			|| !NetworkVariantReader.TryReadString(payload, "match_id", NetworkProtocol.MaxMatchIdLength, out string matchId)
			|| !NetworkVariantReader.TryReadInt64(payload, "authority_epoch", 0, long.MaxValue, out long authorityEpoch))
		{
			return false;
		}

		context = new NetworkRpcContext(sessionId, matchId, authorityEpoch);
		return context.IsValid;
	}

	internal bool IsDuplicateOrOlderClientAction(int seatIndex, long clientActionId)
	{
		if (clientActionId <= 0)
		{
			return false;
		}

		return _lastAcceptedClientActionIds.TryGetValue(seatIndex, out long lastAccepted)
			&& AuthorityActionStateRules.ClassifyClientAction(clientActionId, lastAccepted)
			== ClientActionAcceptance.DuplicateOrOlder;
	}

	internal bool TryAcceptClientAction(int seatIndex, long clientActionId)
	{
		if (clientActionId == 0)
		{
			return true;
		}

		_lastAcceptedClientActionIds.TryGetValue(seatIndex, out long lastAccepted);
		if (!AuthorityActionStateRules.TryAcceptClientAction(
			clientActionId,
			lastAccepted,
			out long nextLastAccepted))
		{
			return false;
		}

		_lastAcceptedClientActionIds[seatIndex] = nextLastAccepted;
		return true;
	}

	public Godot.Collections.Dictionary GetLastAcceptedClientActionIdsVariant()
	{
		var result = new Godot.Collections.Dictionary();
		foreach (KeyValuePair<int, long> pair in _lastAcceptedClientActionIds)
		{
			result[pair.Key] = pair.Value;
		}

		return result;
	}

	internal List<NetworkMigrationRosterEntry> GetCheckpointRoster()
	{
		var result = new List<NetworkMigrationRosterEntry>(_players.Count);
		foreach (NetworkPlayerPublicState player in _players)
		{
			result.Add(new NetworkMigrationRosterEntry
			{
				PlayerSessionId = player.PlayerSessionId,
				DisplayName = player.DisplayName,
				AvatarId = player.AvatarId,
				SeatIndex = player.SeatIndex,
				PeerId = player.PeerId,
				IsHost = player.IsHost,
					IsReady = player.IsReady,
					IsConnected = player.IsConnected,
					CredentialsAcknowledged = player.CredentialsAcknowledged,
			});
		}

		return result;
	}

	internal List<NetworkAuthoritySeatControl> GetCheckpointSeatControls()
	{
		var result = new List<NetworkAuthoritySeatControl>(_seatControlStates.Count);
		foreach (SeatControlState state in _seatControlStates)
		{
			result.Add(new NetworkAuthoritySeatControl
			{
				SeatIndex = state.SeatIndex,
				Mode = state.Mode,
				PeerId = state.PeerId,
				PlayerSessionId = state.PlayerSessionId,
					CredentialVerifier = state.CredentialVerifier,
					CredentialGeneration = state.CredentialGeneration,
					PendingCredentialVerifier = state.PendingCredentialVerifier,
					PendingCredentialGeneration = state.PendingCredentialGeneration,
					PendingCredentialPeerId = state.PendingCredentialPeerId,
			});
		}

		return result;
	}

	internal List<NetworkAuthorityReconnectClaim> GetCheckpointReconnectClaims()
	{
		var result = new List<NetworkAuthorityReconnectClaim>(_reconnectClaimsBySeat.Count);
		bool claimsFollowSeatControlOrder = _seatControlStates.Count == _reconnectClaimsBySeat.Count;
		foreach (SeatControlState state in _seatControlStates)
		{
			if (!_reconnectClaimsBySeat.TryGetValue(state.SeatIndex, out ReconnectClaimRecord claim))
			{
				claimsFollowSeatControlOrder = false;
				break;
			}

			result.Add(new NetworkAuthorityReconnectClaim
			{
				MatchId = claim.MatchId,
				SeatIndex = claim.SeatIndex,
					CredentialGeneration = claim.CredentialGeneration,
				PlayerSessionId = claim.PlayerSessionId,
				ClaimedPeerId = claim.ClaimedPeerId,
				ClaimState = claim.ClaimState.ToString(),
			});
		}

		if (claimsFollowSeatControlOrder)
		{
			return result;
		}

		// Keep a defensive path for an invalid/stale in-memory pairing. Normal
		// checkpoints follow the already ordered seat-control list above.
		result.Clear();
		foreach (KeyValuePair<int, ReconnectClaimRecord> pair in _reconnectClaimsBySeat.OrderBy(pair => pair.Key))
		{
			ReconnectClaimRecord claim = pair.Value;
			result.Add(new NetworkAuthorityReconnectClaim
			{
				MatchId = claim.MatchId,
				SeatIndex = claim.SeatIndex,
					CredentialGeneration = claim.CredentialGeneration,
				PlayerSessionId = claim.PlayerSessionId,
				ClaimedPeerId = claim.ClaimedPeerId,
				ClaimState = claim.ClaimState.ToString(),
			});
		}

		return result;
	}

	internal void ResetClientActionIds()
	{
		_lastAcceptedClientActionIds.Clear();
		_nextClientActionId = 0;
	}

	internal void RestoreLastAcceptedClientActionIds(IReadOnlyDictionary<int, long> values)
	{
		_lastAcceptedClientActionIds.Clear();
		_nextClientActionId = 0;
		foreach (KeyValuePair<int, long> pair in values)
		{
			if (pair.Key < 0 || pair.Key >= NetworkSessionDefaults.MaxPlayers || pair.Value < 0)
			{
				continue;
			}

			_lastAcceptedClientActionIds[pair.Key] = pair.Value;
			_nextClientActionId = Math.Max(_nextClientActionId, pair.Value);
		}
	}

	internal AuthorityCheckpointEnvelope GetLatestAuthorityCheckpointEnvelope() => _latestAuthorityCheckpointEnvelope;
	internal byte[] GetLatestAuthorityCheckpoint() => _latestAuthorityCheckpointEnvelope.GetPayloadCopy();
	internal string GetLatestAuthorityCheckpointHash() => _latestAuthorityCheckpointEnvelope.Hash;
	internal long GetLatestAuthorityCheckpointId() => _latestAuthorityCheckpointEnvelope.CheckpointId;

	internal AuthorityCheckpointCommitResult CommitAuthorityCheckpointFromBoard(GameBoardScene gameBoard)
	{
		if (!IsHost || _state != NetworkSessionState.InGame)
		{
			return AuthorityCheckpointCommitResult.Rejected(
				AuthorityCheckpointCommitStatus.RulesStateUnavailable,
				"board_checkpoint_not_authority");
		}

		if (!TryInvalidateMigrationAvailability(out MigrationCheckpointDispatchStatus invalidationStatus))
		{
			AuthorityCheckpointCommitStatus status = invalidationStatus == MigrationCheckpointDispatchStatus.SendFailed
				? AuthorityCheckpointCommitStatus.SendFailed
				: AuthorityCheckpointCommitStatus.SigningFailed;
			RecordAuthorityCheckpointCommitFailure($"migration_invalidation_failed status={invalidationStatus}");
			return AuthorityCheckpointCommitResult.Rejected(status, invalidationStatus.ToString());
		}

		long candidateCheckpointId;
		try
		{
			candidateCheckpointId = checked(_latestAuthorityCheckpointEnvelope.CheckpointId + 1);
		}
		catch (OverflowException)
		{
			RecordAuthorityCheckpointCommitFailure();
			return AuthorityCheckpointCommitResult.Rejected(
				AuthorityCheckpointCommitStatus.EncodingFailed,
				"checkpoint_id_overflow");
		}

		if (!gameBoard.TryBuildAuthorityCheckpointEnvelope(
				_lastAcceptedClientActionIds,
				candidateCheckpointId,
				out AuthorityCheckpointEnvelope envelope,
				out string buildError))
		{
			RecordAuthorityCheckpointCommitFailure(buildError);
			return AuthorityCheckpointCommitResult.Rejected(
				NetworkMigrationRules.ClassifyCheckpointBuildFailure(buildError),
				buildError);
		}

		_latestAuthorityCheckpointEnvelope = envelope;
		gameBoard.CommitNetworkCheckpointEnvelope(envelope);
		_migrationEligibleCheckpointId = envelope.CheckpointId;
		MigrationCheckpointDispatchStatus dispatchStatus = SendCheckpointToHotStandbyIfNeeded(Time.GetTicksMsec() / 1000.0);
		return AuthorityCheckpointCommitResult.Committed(envelope.CheckpointId, dispatchStatus);
	}

	internal AuthorityCheckpointCommitResult CommitAuthorityCheckpointFromLobby()
	{
		if (!IsHost || _state != NetworkSessionState.Lobby || _players.Count == 0)
		{
			return AuthorityCheckpointCommitResult.Rejected(
				AuthorityCheckpointCommitStatus.RulesStateUnavailable,
				"lobby_checkpoint_not_authority");
		}

		if (!TryInvalidateMigrationAvailability(out MigrationCheckpointDispatchStatus invalidationStatus))
		{
			AuthorityCheckpointCommitStatus status = invalidationStatus == MigrationCheckpointDispatchStatus.SendFailed
				? AuthorityCheckpointCommitStatus.SendFailed
				: AuthorityCheckpointCommitStatus.SigningFailed;
			RecordAuthorityCheckpointCommitFailure($"migration_invalidation_failed status={invalidationStatus}");
			return AuthorityCheckpointCommitResult.Rejected(status, invalidationStatus.ToString());
		}

		EnsureLobbySeatControlStates();
		string manifestHash = GetLobbyGameplayManifestHash();
		if (string.IsNullOrWhiteSpace(manifestHash))
		{
			EmitSignal(SignalName.ErrorRaised, ItemCardCatalogPreflight.ErrorBodyKey);
			return AuthorityCheckpointCommitResult.Rejected(
				AuthorityCheckpointCommitStatus.RulesStateUnavailable,
				"manifest_unavailable");
		}

		long candidateCheckpointId;
		try
		{
			candidateCheckpointId = checked(_latestAuthorityCheckpointEnvelope.CheckpointId + 1);
		}
		catch (OverflowException)
		{
			RecordAuthorityCheckpointCommitFailure();
			return AuthorityCheckpointCommitResult.Rejected(
				AuthorityCheckpointCommitStatus.EncodingFailed,
				"checkpoint_id_overflow");
		}
		int playerCount = _config.PlayerCount;
		var checkpoint = new NetworkAuthorityCheckpoint
		{
			Phase = NetworkAuthorityPhase.Lobby,
			SessionId = _config.SessionId,
			MatchId = string.Empty,
			RoomName = _config.RoomName,
			RoomPasswordVerifierVersion = _config.RoomPasswordVerifier.Version,
			RoomPasswordVerifierIterations = _config.RoomPasswordVerifier.Iterations,
			RoomPasswordVerifierSalt = _config.RoomPasswordVerifier.Salt,
			RoomPasswordVerifier = _config.RoomPasswordVerifier.Verifier,
			MatchSeed = _config.MatchSeed,
			PlayerCount = playerCount,
			ItemMode = _config.ItemMode,
			WinningScore = _config.WinningScore,
			Transport = _config.Transport,
			ManifestHash = manifestHash,
			Roster = GetCheckpointRoster(),
			SeatControls = GetCheckpointSeatControls(),
			LastAcceptedClientActionIds = new Dictionary<int, long>(_lastAcceptedClientActionIds),
			Decks = Enumerable.Range(0, playerCount).Select(_ => new List<NetworkCheckpointCard>()).ToList(),
			Hands = Enumerable.Range(0, playerCount).Select(_ => new List<NetworkCheckpointCard>()).ToList(),
			ItemHands = Enumerable.Range(0, playerCount).Select(_ => new List<string>()).ToList(),
			Scores = new int[playerCount],
			CurrentTurnPlayerIndex = 0,
			WinnerPlayerIndex = -1,
			TurnSequenceGeneration = 0,
			LastAuthorityActionId = 0,
			AuthorityAction = AuthorityActionState.Empty,
			RngSeed = _config.MatchSeed,
			RngState = "1",
			SnapshotId = 0,
			AuthorityEpoch = _authorityEpoch,
			CheckpointId = candidateCheckpointId,
			ReconnectClaims = GetCheckpointReconnectClaims(),
		};

		if (!AuthorityCheckpointEnvelope.TryCreate(checkpoint, out AuthorityCheckpointEnvelope envelope, out string encodeError))
		{
			NetworkAuthorityCheckpointCodec.Validate(checkpoint, out string validationError);
			RecordAuthorityCheckpointCommitFailure(string.Join(
				" ",
				encodeError,
				validationError,
				$"phase={checkpoint.Phase}",
				$"counts={checkpoint.Roster.Count}/{checkpoint.SeatControls.Count}/{checkpoint.ReconnectClaims.Count}",
				$"roster={string.Join(',', checkpoint.Roster.Select(player => $"{player.SeatIndex}:{player.PeerId}:{player.CredentialsAcknowledged}"))}",
				$"controls={string.Join(',', checkpoint.SeatControls.Select(control => $"{control.SeatIndex}:{control.Mode}:{control.PeerId}:{control.CredentialGeneration}"))}",
				$"claims={string.Join(',', checkpoint.ReconnectClaims.Select(claim => $"{claim.SeatIndex}:{claim.CredentialGeneration}:{claim.ClaimState}:{claim.ClaimedPeerId}"))}"));
			return AuthorityCheckpointCommitResult.Rejected(
				NetworkMigrationRules.ClassifyCheckpointBuildFailure(encodeError),
				encodeError);
		}

		_latestAuthorityCheckpointEnvelope = envelope;
		_migrationEligibleCheckpointId = envelope.CheckpointId;
		MigrationCheckpointDispatchStatus dispatchStatus = SendCheckpointToHotStandbyIfNeeded(Time.GetTicksMsec() / 1000.0);
		return AuthorityCheckpointCommitResult.Committed(envelope.CheckpointId, dispatchStatus);
	}

	private void RecordAuthorityCheckpointCommitFailure(string localDetail = "")
	{
		_authorityCheckpointCommitFailureCount++;
		if (_authorityCheckpointCommitFailureReported)
		{
			return;
		}

		_authorityCheckpointCommitFailureReported = true;
		if (!string.IsNullOrWhiteSpace(localDetail))
		{
			GD.PrintErr($"AUTHORITY_CHECKPOINT_LOCAL_DIAGNOSTIC {localDetail}");
		}
		GD.PushError("Authority checkpoint commit rejected.");
		EmitSignal(SignalName.ErrorRaised, "checkpoint_commit_failed");
	}

	private string GetLobbyGameplayManifestHash()
	{
		if (ItemCardCatalog.TryLoad(this, out ItemCardCatalog catalog, out string errorMessage))
		{
			return catalog.GameplayManifestHash;
		}

		GD.PushError($"Unable to load lobby gameplay manifest: {errorMessage}");
		return string.Empty;
	}
}
