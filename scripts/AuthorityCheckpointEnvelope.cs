using System;
using System.Security.Cryptography;

public sealed class AuthorityCheckpointEnvelope : IDisposable
{
	private byte[] _payload;
	private bool _disposed;

	public static AuthorityCheckpointEnvelope Empty { get; } = new(0, 0, Array.Empty<byte>(), string.Empty);

	internal AuthorityCheckpointEnvelope(long authorityEpoch, long checkpointId, byte[] ownedPayload, string hash)
	{
		AuthorityEpoch = authorityEpoch;
		CheckpointId = checkpointId;
		_payload = ownedPayload ?? throw new ArgumentNullException(nameof(ownedPayload));
		Hash = hash ?? string.Empty;
	}

	public long AuthorityEpoch { get; }
	public long CheckpointId { get; }
	public string Hash { get; }
	public bool HasPayload => _payload.Length > 0;
	public ReadOnlyMemory<byte> PayloadMemory => _payload;
	public ReadOnlySpan<byte> PayloadSpan => _payload;
	public int EncodedByteCount => _payload.Length;
	public long ExplicitPlaintextCopyCount => 0;

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		if (_payload.Length > 0)
		{
			CryptographicOperations.ZeroMemory(_payload);
			_payload = Array.Empty<byte>();
		}
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
