using System;

public partial class NetworkManager
{
	private readonly NetworkMatchEndpointRegistrationStore _matchEndpointRegistrationStore = new();
	private NetworkBoardInitialization? _pendingBoardInitialization;
	private long _endpointRegistrationCount;
	private long _endpointReplacementCount;
	private long _staleEndpointUnregistrationCount;
	private long _missingAuthorityEndpointRejectionCount;
	private long _missingReplicaEndpointRejectionCount;
	private long _missingPresentationEndpointRejectionCount;
	private long _pendingInitializationReplacementCount;

	internal NetworkMatchEndpointRegistrationToken RegisterMatchEndpoint(
		string sessionId,
		string matchId,
		long authorityEpoch,
		INetworkAuthorityEndpoint authority,
		INetworkReplicaEndpoint replica,
		INetworkPresentationEndpoint presentation)
	{
		if (authority == null || replica == null || presentation == null
			|| string.IsNullOrWhiteSpace(sessionId)
			|| matchId is null
			|| authorityEpoch < 0
			|| (!string.IsNullOrEmpty(_config.SessionId)
				&& !string.Equals(sessionId, _config.SessionId, StringComparison.Ordinal))
			|| (!string.IsNullOrEmpty(_config.MatchId)
				&& !string.Equals(matchId, _config.MatchId, StringComparison.Ordinal))
			|| authorityEpoch < _authorityEpoch)
		{
			return default;
		}

		if (_matchEndpointRegistrationStore.Active != null)
		{
			_endpointReplacementCount++;
		}
		NetworkMatchEndpointRegistrationToken token = _matchEndpointRegistrationStore.Register(
			sessionId,
			matchId,
			authorityEpoch,
			authority,
			replica,
			presentation);
		_endpointRegistrationCount++;
		TryApplyPendingBoardInitialization();
		return token;
	}

	internal bool UnregisterMatchEndpoint(NetworkMatchEndpointRegistrationToken token)
	{
		if (!_matchEndpointRegistrationStore.Unregister(token))
		{
			_staleEndpointUnregistrationCount++;
			return false;
		}

		return true;
	}

	public NetworkEndpointDiagnosticsSnapshot DebugGetNetworkEndpointDiagnostics()
	{
		return new NetworkEndpointDiagnosticsSnapshot(
			_endpointRegistrationCount,
			_endpointReplacementCount,
			_staleEndpointUnregistrationCount,
			_missingAuthorityEndpointRejectionCount,
			_missingReplicaEndpointRejectionCount,
			_missingPresentationEndpointRejectionCount,
			_pendingInitializationReplacementCount,
			_matchEndpointRegistrationStore.Active?.IsAlive == true,
			_pendingBoardInitialization != null);
	}

	public bool DebugHasActiveMatchEndpointRegistration()
	{
		return _matchEndpointRegistrationStore.Active?.IsAlive == true;
	}

	public long DebugGetMatchEndpointRegistrationCount()
	{
		return _endpointRegistrationCount;
	}

	public long DebugGetMatchEndpointReplacementCount()
	{
		return _endpointReplacementCount;
	}

	public long DebugGetStaleMatchEndpointUnregistrationCount()
	{
		return _staleEndpointUnregistrationCount;
	}

	private bool TryGetAuthorityEndpoint(out INetworkAuthorityEndpoint endpoint)
	{
		if (TryGetActiveEndpointRegistration(out NetworkMatchEndpointRegistration registration))
		{
			endpoint = registration.Authority;
			return true;
		}
		_missingAuthorityEndpointRejectionCount++;
		endpoint = null!;
		return false;
	}

	private bool TryGetReplicaEndpoint(out INetworkReplicaEndpoint endpoint)
	{
		if (TryGetActiveEndpointRegistration(out NetworkMatchEndpointRegistration registration))
		{
			endpoint = registration.Replica;
			return true;
		}
		_missingReplicaEndpointRejectionCount++;
		endpoint = null!;
		return false;
	}

	private bool TryGetPresentationEndpoint(out INetworkPresentationEndpoint endpoint)
	{
		if (TryGetActiveEndpointRegistration(out NetworkMatchEndpointRegistration registration))
		{
			endpoint = registration.Presentation;
			return true;
		}
		_missingPresentationEndpointRejectionCount++;
		endpoint = null!;
		return false;
	}

	private bool TryGetActiveEndpointRegistration(out NetworkMatchEndpointRegistration registration)
	{
		registration = _matchEndpointRegistrationStore.Active!;
		if (registration == null || !registration.IsAlive)
		{
			_matchEndpointRegistrationStore.Clear();
			return false;
		}
		if (!string.Equals(registration.SessionId, _config.SessionId, StringComparison.Ordinal)
			|| !string.Equals(registration.MatchId, _config.MatchId, StringComparison.Ordinal))
		{
			return false;
		}
		return true;
	}

	private void RetainPendingBoardInitialization(NetworkBoardInitialization initialization)
	{
		if (_pendingBoardInitialization != null)
		{
			_pendingInitializationReplacementCount++;
		}
		_pendingBoardInitialization = initialization;
	}

	private void TryApplyPendingBoardInitialization()
	{
		if (_pendingBoardInitialization is not { } initialization
			|| !TryGetReplicaEndpoint(out INetworkReplicaEndpoint replica)
			|| !replica.TryInitialize(initialization))
		{
			return;
		}
		_pendingBoardInitialization = null;
		CompleteBoardInitialization(initialization);
	}

	private void ClearMatchEndpointRegistration()
	{
		_matchEndpointRegistrationStore.Clear();
		_pendingBoardInitialization = null;
	}
}
