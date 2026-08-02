using System;
using Godot;

internal sealed class ImmutableNetworkVisualDispatch
{
	private readonly Godot.Collections.Dictionary _payload;

	internal ImmutableNetworkVisualDispatch(int peerId, Godot.Collections.Dictionary payload)
	{
		PeerId = peerId;
		_payload = payload.Duplicate(deep: true);
	}

	internal int PeerId { get; }

	internal Godot.Collections.Dictionary CreatePayloadCopy()
	{
		return _payload.Duplicate(deep: true);
	}
}

internal sealed record AuthorityRuleTransactionResult(
	bool Committed,
	long ActionId,
	long ClientActionId,
	long AuthorityEpoch,
	long CheckpointId,
	int SnapshotId,
	int TurnSequenceGeneration,
	ImmutableNetworkVisualDispatch[] VisualEvents,
	string DeferredFaultPoint)
{
	internal static AuthorityRuleTransactionResult Rejected => new(
		false,
		0,
		0,
		0,
		-1,
		-1,
		-1,
		Array.Empty<ImmutableNetworkVisualDispatch>(),
		string.Empty);
}
