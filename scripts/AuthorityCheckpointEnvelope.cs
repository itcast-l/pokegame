using System;

public sealed class AuthorityCheckpointEnvelope
{
	private readonly byte[] _payload;

	public static AuthorityCheckpointEnvelope Empty { get; } = new(0, 0, Array.Empty<byte>(), string.Empty);

	public AuthorityCheckpointEnvelope(long authorityEpoch, long checkpointId, ReadOnlySpan<byte> payload, string hash)
	{
		AuthorityEpoch = authorityEpoch;
		CheckpointId = checkpointId;
		_payload = payload.ToArray();
		Hash = hash ?? string.Empty;
	}

	public long AuthorityEpoch { get; }
	public long CheckpointId { get; }
	public string Hash { get; }
	public bool HasPayload => _payload.Length > 0;
	public ReadOnlyMemory<byte> PayloadMemory => _payload;

	public byte[] GetPayloadCopy()
	{
		return (byte[])_payload.Clone();
	}

	public static bool TryCreate(
		NetworkAuthorityCheckpoint checkpoint,
		out AuthorityCheckpointEnvelope envelope,
		out string error)
	{
		envelope = Empty;
		if (!NetworkAuthorityCheckpointCodec.TryEncode(checkpoint, out byte[] payload, out string hash, out error))
		{
			return false;
		}

		envelope = new AuthorityCheckpointEnvelope(
			checkpoint.AuthorityEpoch,
			checkpoint.CheckpointId,
			payload,
			hash);
		return true;
	}
}
