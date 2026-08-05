public enum NetworkSessionCloseReason
{
	UserRequested,
	ClientRequested,
	SessionReplacement,
	ConnectionCreationFailed,
	ProtocolRejected,
	MigrationHandoff,
	InternalFailure,
	ApplicationExit,
}

public readonly record struct NetworkSessionClosePolicy(
	bool BroadcastHostEndedMatch,
	bool WaitOneProcessFrame,
	double ReliablePacketGraceSeconds,
	bool Immediate)
{
	public const double HostReliablePacketGraceSeconds = 0.15;

	public static NetworkSessionClosePolicy Resolve(
		NetworkSessionCloseReason reason,
		bool shouldBroadcastHostEndedMatch)
	{
		bool immediate = reason is NetworkSessionCloseReason.ConnectionCreationFailed
			or NetworkSessionCloseReason.ProtocolRejected
			or NetworkSessionCloseReason.MigrationHandoff
			or NetworkSessionCloseReason.InternalFailure
			or NetworkSessionCloseReason.ApplicationExit;
		bool broadcast = !immediate
			&& shouldBroadcastHostEndedMatch
			&& reason is (NetworkSessionCloseReason.UserRequested
				or NetworkSessionCloseReason.SessionReplacement);
		return new NetworkSessionClosePolicy(
			broadcast,
			broadcast,
			broadcast ? HostReliablePacketGraceSeconds : 0.0,
			immediate || !broadcast);
	}
}
