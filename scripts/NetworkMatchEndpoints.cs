using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

internal interface INetworkMatchEndpoint
{
	bool IsAlive { get; }
}

internal interface INetworkAuthorityEndpoint : INetworkMatchEndpoint
{
	bool IsGameOver { get; }
	bool IsMatchClaimable { get; }
	bool IsMigrationInputLocked { get; }
	bool CanPublishSnapshot { get; }
	long AuthorityEpoch { get; }
	long CheckpointId { get; }
	long CurrentAuthorityActionId { get; }
	int CreateNextSnapshotId();
	bool TryStartHandCardPlay(int actorSeatIndex, int handIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason);
	bool TryStartItemCardPlay(int actorSeatIndex, int itemHandIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason);
	bool TryStartPendingItemSelection(int actorSeatIndex, int selectionIndex, int turnSequenceGeneration, long clientActionId, out MultiplayerBoardActionRejectReason rejectReason);
	bool TryBuildCheckpoint(IReadOnlyDictionary<int, long> lastAcceptedClientActionIds, long candidateCheckpointId, out AuthorityCheckpointEnvelope envelope, out string error);
	void CommitCheckpoint(AuthorityCheckpointEnvelope envelope);
	void AdvanceCheckpointSequence(long checkpointId);
	Godot.Collections.Dictionary BuildSnapshotForSeat(int recipientSeatIndex, int snapshotId);
	bool TryStageVisualDispatch(int peerId, Godot.Collections.Dictionary payload);
	bool TryStageHostVisualEvent(Godot.Collections.Dictionary payload);
	Task ResumeAuthorityActionAsync();
	void BeginAiProxyTurnIfNeeded();
}

internal interface INetworkReplicaEndpoint : INetworkMatchEndpoint
{
	bool TryInitialize(NetworkBoardInitialization initialization);
	void ApplySnapshot(Godot.Collections.Dictionary snapshot);
	void QueueVisualEvent(Godot.Collections.Dictionary payload);
	void RejectAction(string reason, int turnSequenceGeneration, int rejectedHandIndex);
	void ConfirmAction(long actionId, long clientActionId, long checkpointId, int snapshotId, int turnSequenceGeneration);
	bool TryRestoreCheckpoint(ReadOnlySpan<byte> payload, string expectedHash, out string error);
	void AdvanceAuthorityEpoch(long authorityEpoch);
}

internal interface INetworkPresentationEndpoint : INetworkMatchEndpoint
{
	void BeginHostMigration();
	void CompleteHostMigration();
}

internal sealed record NetworkMatchEndpointRegistration(
	string SessionId,
	string MatchId,
	long AuthorityEpoch,
	long RegistrationGeneration,
	INetworkAuthorityEndpoint Authority,
	INetworkReplicaEndpoint Replica,
	INetworkPresentationEndpoint Presentation)
{
	public bool IsAlive => Authority.IsAlive && Replica.IsAlive && Presentation.IsAlive;
}

internal readonly record struct NetworkMatchEndpointRegistrationToken(
	string SessionId,
	string MatchId,
	long RegistrationGeneration)
{
	public bool IsValid => RegistrationGeneration > 0 && SessionId is not null && MatchId is not null;
}

internal sealed class NetworkMatchEndpointRegistrationStore
{
	private long _nextGeneration;

	public NetworkMatchEndpointRegistration? Active { get; private set; }

	public NetworkMatchEndpointRegistrationToken Register(
		string sessionId,
		string matchId,
		long authorityEpoch,
		INetworkAuthorityEndpoint authority,
		INetworkReplicaEndpoint replica,
		INetworkPresentationEndpoint presentation)
	{
		long generation = checked(++_nextGeneration);
		Active = new NetworkMatchEndpointRegistration(
			sessionId,
			matchId,
			authorityEpoch,
			generation,
			authority,
			replica,
			presentation);
		return new NetworkMatchEndpointRegistrationToken(sessionId, matchId, generation);
	}

	public bool Unregister(NetworkMatchEndpointRegistrationToken token)
	{
		if (!token.IsValid
			|| Active is not { } active
			|| active.RegistrationGeneration != token.RegistrationGeneration
			|| !string.Equals(active.SessionId, token.SessionId, StringComparison.Ordinal)
			|| !string.Equals(active.MatchId, token.MatchId, StringComparison.Ordinal))
		{
			return false;
		}

		Active = null;
		return true;
	}

	public void Clear()
	{
		Active = null;
	}
}

public enum NetworkEndpointRequestKind
{
	AuthorityAction,
	Snapshot,
	VisualEvent,
	MigrationPresentation,
	Initialization,
}

#if DECKLAND_TEST_HOOKS
public sealed record NetworkEndpointDiagnosticsSnapshot(
	long RegistrationCount,
	long ReplacementCount,
	long StaleUnregistrationCount,
	long MissingAuthorityRejectionCount,
	long MissingReplicaRejectionCount,
	long MissingPresentationRejectionCount,
	long PendingInitializationReplacementCount,
	bool HasActiveRegistration,
	bool HasPendingInitialization);
#endif

internal sealed record NetworkBoardInitialization(
	NetworkSessionConfig Config,
	long AuthorityEpoch,
	int LocalPlayerIndex,
	string[] PlayerNames,
	string[] PlayerAvatarIds)
{
	public Godot.Collections.Dictionary ToVariant()
	{
		return new Godot.Collections.Dictionary
		{
			["protocol_version"] = NetworkProtocol.CurrentVersion,
			["session_id"] = Config.SessionId,
			["match_id"] = Config.MatchId,
			["authority_epoch"] = AuthorityEpoch,
			["player_count"] = Config.PlayerCount,
			["item_mode"] = Config.ItemMode,
			["winning_score"] = Config.WinningScore,
			["match_seed"] = Config.MatchSeed,
			["player_names_csv"] = string.Join(',', PlayerNames),
			["player_avatar_ids_csv"] = string.Join(',', PlayerAvatarIds),
			["network_wait_for_initialize"] = false,
		};
	}
}
