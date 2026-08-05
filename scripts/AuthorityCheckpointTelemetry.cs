using System;
using System.Diagnostics;

public readonly record struct AuthorityCheckpointStageTelemetrySnapshot(
	long CallCount,
	long LastAllocatedBytes,
	long MaximumAllocatedBytes,
	double LastElapsedMilliseconds,
	double MaximumElapsedMilliseconds,
	int LastOutputSize,
	int MaximumOutputSize);

public readonly record struct AuthorityCheckpointTelemetrySnapshot(
	AuthorityCheckpointStageTelemetrySnapshot Projection,
	AuthorityCheckpointStageTelemetrySnapshot Encoding,
	AuthorityCheckpointStageTelemetrySnapshot Encryption,
	long ExplicitPlaintextCopyCount)
{
	public long ProjectionCount => Projection.CallCount;
	public long LastProjectionAllocatedBytes => Projection.LastAllocatedBytes;
	public long MaximumProjectionAllocatedBytes => Projection.MaximumAllocatedBytes;
	public double LastProjectionMilliseconds => Projection.LastElapsedMilliseconds;
	public double MaximumProjectionMilliseconds => Projection.MaximumElapsedMilliseconds;
	public int LastProjectedEntryCount => Projection.LastOutputSize;
	public int MaximumProjectedEntryCount => Projection.MaximumOutputSize;

	public long EncodeCount => Encoding.CallCount;
	public long LastEncodeAllocatedBytes => Encoding.LastAllocatedBytes;
	public long MaximumEncodeAllocatedBytes => Encoding.MaximumAllocatedBytes;
	public double LastEncodeMilliseconds => Encoding.LastElapsedMilliseconds;
	public double MaximumEncodeMilliseconds => Encoding.MaximumElapsedMilliseconds;
	public int LastEncodedBytes => Encoding.LastOutputSize;
	public int MaximumEncodedBytes => Encoding.MaximumOutputSize;

	public long EncryptionCount => Encryption.CallCount;
	public long LastEncryptionAllocatedBytes => Encryption.LastAllocatedBytes;
	public long MaximumEncryptionAllocatedBytes => Encryption.MaximumAllocatedBytes;
	public double LastEncryptionMilliseconds => Encryption.LastElapsedMilliseconds;
	public double MaximumEncryptionMilliseconds => Encryption.MaximumElapsedMilliseconds;
	public int LastCiphertextBytes => Encryption.LastOutputSize;
	public int MaximumCiphertextBytes => Encryption.MaximumOutputSize;
}

public static class AuthorityCheckpointTelemetry
{
	private static readonly object Sync = new();
	private static StageTelemetry _projection;
	private static StageTelemetry _encoding;
	private static StageTelemetry _encryption;
	private static long _explicitPlaintextCopyCount;

	public static AuthorityCheckpointTelemetrySnapshot GetSnapshot()
	{
		lock (Sync)
		{
			return new AuthorityCheckpointTelemetrySnapshot(
				_projection.Capture(),
				_encoding.Capture(),
				_encryption.Capture(),
				_explicitPlaintextCopyCount);
		}
	}

	public static void RecordProjection(
		int projectedEntryCount,
		long allocatedBytes,
		long elapsedTicks)
	{
		lock (Sync)
		{
			_projection.Record(projectedEntryCount, allocatedBytes, elapsedTicks);
		}
	}

	public static void RecordEncoding(
		int encodedBytes,
		long allocatedBytes,
		long elapsedTicks)
	{
		lock (Sync)
		{
			_encoding.Record(encodedBytes, allocatedBytes, elapsedTicks);
		}
	}

	public static void RecordEncryption(
		int ciphertextBytes,
		long allocatedBytes,
		long elapsedTicks)
	{
		lock (Sync)
		{
			_encryption.Record(ciphertextBytes, allocatedBytes, elapsedTicks);
		}
	}

	internal static void RecordExplicitPlaintextCopy()
	{
		lock (Sync)
		{
			_explicitPlaintextCopyCount++;
		}
	}

	private struct StageTelemetry
	{
		private long _callCount;
		private long _lastAllocatedBytes;
		private long _maximumAllocatedBytes;
		private double _lastElapsedMilliseconds;
		private double _maximumElapsedMilliseconds;
		private int _lastOutputSize;
		private int _maximumOutputSize;

		internal void Record(int outputSize, long allocatedBytes, long elapsedTicks)
		{
			long normalizedAllocatedBytes = Math.Max(0, allocatedBytes);
			double elapsedMilliseconds = elapsedTicks <= 0
				? 0
				: elapsedTicks * 1000.0 / Stopwatch.Frequency;
			int normalizedOutputSize = Math.Max(0, outputSize);
			_callCount++;
			_lastAllocatedBytes = normalizedAllocatedBytes;
			_maximumAllocatedBytes = Math.Max(
				_maximumAllocatedBytes,
				normalizedAllocatedBytes);
			_lastElapsedMilliseconds = elapsedMilliseconds;
			_maximumElapsedMilliseconds = Math.Max(
				_maximumElapsedMilliseconds,
				elapsedMilliseconds);
			_lastOutputSize = normalizedOutputSize;
			_maximumOutputSize = Math.Max(_maximumOutputSize, normalizedOutputSize);
		}

		internal readonly AuthorityCheckpointStageTelemetrySnapshot Capture()
		{
			return new AuthorityCheckpointStageTelemetrySnapshot(
				_callCount,
				_lastAllocatedBytes,
				_maximumAllocatedBytes,
				_lastElapsedMilliseconds,
				_maximumElapsedMilliseconds,
				_lastOutputSize,
				_maximumOutputSize);
		}
	}
}
