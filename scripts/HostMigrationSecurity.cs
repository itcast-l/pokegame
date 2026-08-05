using System;
using System.Buffers.Binary;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Diagnostics;

public readonly record struct MigrationCheckpointSecurityContext(
	string SessionId,
	string MatchId,
	long AuthorityEpoch,
	long CheckpointId,
	string CandidatePlayerSessionId,
	long PlanRevision,
	int ProtocolVersion)
{
	public bool IsValid => !string.IsNullOrWhiteSpace(SessionId)
		&& MatchId is not null
		&& AuthorityEpoch >= 0
		&& CheckpointId >= 0
		&& !string.IsNullOrWhiteSpace(CandidatePlayerSessionId)
		&& PlanRevision > 0
		&& ProtocolVersion == NetworkProtocol.CurrentVersion;
}

public sealed class EncryptedAuthorityCheckpointEnvelope
{
	private readonly byte[] _nonce;
	private readonly byte[] _ciphertext;
	private readonly byte[] _tag;
	private readonly byte[] _signature;

	public static EncryptedAuthorityCheckpointEnvelope Empty { get; } = new(
		default,
		Array.Empty<byte>(),
		Array.Empty<byte>(),
		Array.Empty<byte>(),
		string.Empty,
		string.Empty,
		string.Empty,
		string.Empty,
		Array.Empty<byte>());

	public EncryptedAuthorityCheckpointEnvelope(
		MigrationCheckpointSecurityContext context,
		byte[] ownedNonce,
		byte[] ownedCiphertext,
		byte[] ownedTag,
		string plaintextHash,
		string ciphertextHash,
		string previousCheckpointHash,
		string sourceAuthorityKeyId,
		byte[] ownedSignature)
	{
		Context = context;
		_nonce = ownedNonce ?? throw new ArgumentNullException(nameof(ownedNonce));
		_ciphertext = ownedCiphertext ?? throw new ArgumentNullException(nameof(ownedCiphertext));
		_tag = ownedTag ?? throw new ArgumentNullException(nameof(ownedTag));
		PlaintextHash = plaintextHash ?? string.Empty;
		CiphertextHash = ciphertextHash ?? string.Empty;
		PreviousCheckpointHash = previousCheckpointHash ?? string.Empty;
		SourceAuthorityKeyId = sourceAuthorityKeyId ?? string.Empty;
		_signature = ownedSignature ?? throw new ArgumentNullException(nameof(ownedSignature));
	}

	public MigrationCheckpointSecurityContext Context { get; }
	public string PlaintextHash { get; }
	public string CiphertextHash { get; }
	public string PreviousCheckpointHash { get; }
	public string SourceAuthorityKeyId { get; }
	public ReadOnlyMemory<byte> NonceMemory => _nonce;
	public ReadOnlyMemory<byte> CiphertextMemory => _ciphertext;
	public ReadOnlyMemory<byte> TagMemory => _tag;
	public ReadOnlyMemory<byte> SignatureMemory => _signature;
	public byte[] GetNonceCopy() => (byte[])_nonce.Clone();
	public byte[] GetCiphertextCopy() => (byte[])_ciphertext.Clone();
	public byte[] GetTagCopy() => (byte[])_tag.Clone();
	public byte[] GetSignatureCopy() => (byte[])_signature.Clone();
}

public sealed class MigrationNonceReplayGuard
{
	private readonly HashSet<string> _nonceKeys = new(StringComparer.Ordinal);
	private long _authorityEpoch = -1;

	public int Count => _nonceKeys.Count;

	public bool HasSeen(long authorityEpoch, ReadOnlySpan<byte> nonce)
	{
		return authorityEpoch == _authorityEpoch
			&& TryCreateNonceKey(nonce, out string nonceKey)
			&& _nonceKeys.Contains(nonceKey);
	}

	public bool TryRegister(long authorityEpoch, ReadOnlySpan<byte> nonce)
	{
		if (authorityEpoch < 0 || !TryCreateNonceKey(nonce, out string nonceKey))
		{
			return false;
		}

		if (_authorityEpoch != authorityEpoch)
		{
			Reset();
			_authorityEpoch = authorityEpoch;
		}

		return _nonceKeys.Add(nonceKey);
	}

	public void Reset()
	{
		_nonceKeys.Clear();
		_authorityEpoch = -1;
	}

	private static bool TryCreateNonceKey(ReadOnlySpan<byte> nonce, out string nonceKey)
	{
		nonceKey = string.Empty;
		if (nonce.Length != HostMigrationSecurity.AesNonceBytes)
		{
			return false;
		}

		nonceKey = Convert.ToHexString(nonce).ToLowerInvariant();
		return true;
	}
}

public static class HostMigrationSecurity
{
	public const int SessionKeyBytes = 32;
	public const int AesNonceBytes = 12;
	public const int AesTagBytes = 16;
	public const string EncryptionAlgorithm = "aes-256-gcm-v1";
	public const string SignatureAlgorithm = "ecdsa-p256-sha256-v1";

	public static ECDiffieHellman CreateKeyAgreement()
	{
		return ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
	}

	public static ECDsa CreateSigner()
	{
		return ECDsa.Create(ECCurve.NamedCurves.nistP256);
	}

	public static byte[] ExportPublicKey(ECDiffieHellman keyAgreement)
	{
		return keyAgreement.ExportSubjectPublicKeyInfo();
	}

	public static byte[] ExportPublicKey(ECDsa signer)
	{
		return signer.ExportSubjectPublicKeyInfo();
	}

	public static string ComputeKeyId(ReadOnlySpan<byte> publicKey)
	{
		return Convert.ToHexString(SHA256.HashData(publicKey)).ToLowerInvariant();
	}

	public static string EncodeBytes(ReadOnlySpan<byte> bytes)
	{
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}

	public static bool TryDecodeBytes(string value, int maxBytes, out byte[] bytes)
	{
		bytes = Array.Empty<byte>();
		if (string.IsNullOrWhiteSpace(value) || value.Length > maxBytes * 2)
		{
			return false;
		}

		string padded = value.Replace('-', '+').Replace('_', '/');
		padded += (padded.Length % 4) switch
		{
			2 => "==",
			3 => "=",
			_ => string.Empty,
		};
		try
		{
			bytes = Convert.FromBase64String(padded);
			return bytes.Length > 0 && bytes.Length <= maxBytes;
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static bool IsSigningPublicKeyBindingValid(string encodedPublicKey, string keyId)
	{
		if (!TryDecodeBytes(encodedPublicKey, 512, out byte[] publicKey)
			|| !IsSha256Hash(keyId)
			|| !string.Equals(ComputeKeyId(publicKey), keyId, StringComparison.Ordinal))
		{
			return false;
		}

		try
		{
			using ECDsa verifier = CreateSigner();
			verifier.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
			return bytesRead == publicKey.Length && verifier.KeySize == 256;
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	public static bool TrySignMigrationPlan(
		NetworkMigrationPlan plan,
		ECDsa sourceAuthoritySigner,
		out NetworkMigrationPlan signedPlan)
	{
		signedPlan = plan;
		if (sourceAuthoritySigner is null
			|| !NetworkMigrationRules.IsUnsignedPlanValid(plan)
			|| !string.Equals(
				plan.SourceAuthorityKeyId,
				ComputeKeyId(ExportPublicKey(sourceAuthoritySigner)),
				StringComparison.Ordinal))
		{
			return false;
		}

		try
		{
			byte[] signature = sourceAuthoritySigner.SignData(
				BuildMigrationPlanSignatureBytes(plan),
				HashAlgorithmName.SHA256);
			signedPlan = plan with { SourceSignature = EncodeBytes(signature) };
			return true;
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	public static bool VerifyMigrationPlan(
		NetworkMigrationPlan plan,
		ReadOnlySpan<byte> sourceAuthorityPublicKey)
	{
		if (!NetworkMigrationRules.IsPlanVisibleToClients(plan)
			|| sourceAuthorityPublicKey.IsEmpty
			|| !string.Equals(
				plan.SourceAuthorityKeyId,
				ComputeKeyId(sourceAuthorityPublicKey),
				StringComparison.Ordinal)
			|| !TryDecodeBytes(plan.SourceSignature, 256, out byte[] signature))
		{
			return false;
		}

		try
		{
			using ECDsa verifier = CreateSigner();
			verifier.ImportSubjectPublicKeyInfo(sourceAuthorityPublicKey, out int bytesRead);
			return bytesRead == sourceAuthorityPublicKey.Length
				&& verifier.VerifyData(
					BuildMigrationPlanSignatureBytes(plan),
					signature,
					HashAlgorithmName.SHA256);
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	public static bool TrySignMigrationPlanRevocation(
		NetworkMigrationPlanRevocation revocation,
		ECDsa sourceAuthoritySigner,
		out NetworkMigrationPlanRevocation signedRevocation)
	{
		signedRevocation = revocation;
		if (sourceAuthoritySigner is null
			|| !NetworkMigrationRules.IsUnsignedPlanRevocationValid(revocation)
			|| !string.Equals(
				revocation.SourceAuthorityKeyId,
				ComputeKeyId(ExportPublicKey(sourceAuthoritySigner)),
				StringComparison.Ordinal))
		{
			return false;
		}

		try
		{
			byte[] signature = sourceAuthoritySigner.SignData(
				BuildMigrationPlanRevocationSignatureBytes(revocation),
				HashAlgorithmName.SHA256);
			signedRevocation = revocation with { SourceSignature = EncodeBytes(signature) };
			return true;
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	public static bool VerifyMigrationPlanRevocation(
		NetworkMigrationPlanRevocation revocation,
		ReadOnlySpan<byte> sourceAuthorityPublicKey)
	{
		if (!NetworkMigrationRules.IsPlanRevocationVisibleToClients(revocation)
			|| sourceAuthorityPublicKey.IsEmpty
			|| !string.Equals(
				revocation.SourceAuthorityKeyId,
				ComputeKeyId(sourceAuthorityPublicKey),
				StringComparison.Ordinal)
			|| !TryDecodeBytes(revocation.SourceSignature, 256, out byte[] signature))
		{
			return false;
		}

		try
		{
			using ECDsa verifier = CreateSigner();
			verifier.ImportSubjectPublicKeyInfo(sourceAuthorityPublicKey, out int bytesRead);
			return bytesRead == sourceAuthorityPublicKey.Length
				&& verifier.VerifyData(
					BuildMigrationPlanRevocationSignatureBytes(revocation),
					signature,
					HashAlgorithmName.SHA256);
		}
		catch (CryptographicException)
		{
			return false;
		}
	}

	public static bool TryDeriveSessionKey(
		ECDiffieHellman localKeyAgreement,
		ReadOnlySpan<byte> peerPublicKey,
		string sessionId,
		out byte[] sessionKey)
	{
		sessionKey = Array.Empty<byte>();
		if (localKeyAgreement is null
			|| peerPublicKey.IsEmpty
			|| string.IsNullOrWhiteSpace(sessionId))
		{
			return false;
		}

		try
		{
			using ECDiffieHellman peer = CreateKeyAgreement();
			peer.ImportSubjectPublicKeyInfo(peerPublicKey, out int bytesRead);
			if (bytesRead != peerPublicKey.Length)
			{
				return false;
			}

			byte[] context = Encoding.UTF8.GetBytes($"deckland-host-migration|{NetworkProtocol.CurrentVersion}|{sessionId}");
			sessionKey = localKeyAgreement.DeriveKeyFromHash(
				peer.PublicKey,
				HashAlgorithmName.SHA256,
				context,
				null);
			return sessionKey.Length == SessionKeyBytes;
		}
		catch (CryptographicException)
		{
			sessionKey = Array.Empty<byte>();
			return false;
		}
	}

	public static bool TryEncryptCheckpoint(
		ReadOnlySpan<byte> plaintext,
		string plaintextHash,
		ReadOnlySpan<byte> sessionKey,
		MigrationCheckpointSecurityContext context,
		string previousCheckpointHash,
		ECDsa sourceAuthoritySigner,
		out EncryptedAuthorityCheckpointEnvelope envelope,
		out string error)
	{
		envelope = EncryptedAuthorityCheckpointEnvelope.Empty;
		error = string.Empty;
		if (plaintext.IsEmpty
			|| sessionKey.Length != SessionKeyBytes
			|| !context.IsValid
			|| !IsSha256Hash(plaintextHash)
			|| (!string.IsNullOrEmpty(previousCheckpointHash) && !IsSha256Hash(previousCheckpointHash))
			|| sourceAuthoritySigner is null)
		{
			error = "migration_checkpoint_encrypt_invalid";
			return false;
		}

		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		long startedAt = Stopwatch.GetTimestamp();
		int ciphertextBytes = 0;
		bool envelopeOwnsEncryptedBuffers = false;
		byte[] nonce = Array.Empty<byte>();
		byte[] ciphertext = Array.Empty<byte>();
		byte[] tag = Array.Empty<byte>();
		byte[] signature = Array.Empty<byte>();
		byte[] aad = Array.Empty<byte>();
		byte[] signaturePayload = Array.Empty<byte>();
		try
		{
			nonce = RandomNumberGenerator.GetBytes(AesNonceBytes);
			ciphertext = new byte[plaintext.Length];
			tag = new byte[AesTagBytes];
			aad = BuildCheckpointContextBytes(context);
			using (var aes = new AesGcm(sessionKey, AesTagBytes))
			{
				aes.Encrypt(nonce, plaintext, ciphertext, tag, aad);
			}

			string ciphertextHash = ComputeCiphertextHash(nonce, ciphertext, tag);
			byte[] sourcePublicKey = ExportPublicKey(sourceAuthoritySigner);
			string sourceKeyId = ComputeKeyId(sourcePublicKey);
			signaturePayload = BuildEnvelopeSignatureBytes(
				aad,
				nonce,
				tag,
				plaintextHash,
				ciphertextHash,
				previousCheckpointHash,
				sourceKeyId);
			signature = sourceAuthoritySigner.SignData(
				signaturePayload,
				HashAlgorithmName.SHA256);
			envelope = new EncryptedAuthorityCheckpointEnvelope(
				context,
				nonce,
				ciphertext,
				tag,
				plaintextHash,
				ciphertextHash,
				previousCheckpointHash,
				sourceKeyId,
				signature);
			envelopeOwnsEncryptedBuffers = true;
			ciphertextBytes = ciphertext.Length;
			return true;
		}
		catch (CryptographicException)
		{
			error = "migration_checkpoint_encrypt_failed";
			return false;
		}
		finally
		{
			if (!envelopeOwnsEncryptedBuffers)
			{
				CryptographicOperations.ZeroMemory(nonce);
				CryptographicOperations.ZeroMemory(ciphertext);
				CryptographicOperations.ZeroMemory(tag);
				CryptographicOperations.ZeroMemory(signature);
			}
			CryptographicOperations.ZeroMemory(signaturePayload);
			CryptographicOperations.ZeroMemory(aad);
			long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
			long allocatedBytes =
				GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
			AuthorityCheckpointTelemetry.RecordEncryption(
				ciphertextBytes,
				allocatedBytes,
				elapsedTicks);
		}
	}

	public static bool TryDecryptCheckpoint(
		EncryptedAuthorityCheckpointEnvelope envelope,
		ReadOnlySpan<byte> sessionKey,
		ReadOnlySpan<byte> sourceAuthorityPublicKey,
		out byte[] plaintext,
		out string error)
	{
		plaintext = Array.Empty<byte>();
		error = string.Empty;
		if (envelope is null
			|| sessionKey.Length != SessionKeyBytes
			|| sourceAuthorityPublicKey.IsEmpty
			|| !envelope.Context.IsValid
			|| !IsSha256Hash(envelope.PlaintextHash)
			|| !IsSha256Hash(envelope.CiphertextHash)
			|| (!string.IsNullOrEmpty(envelope.PreviousCheckpointHash) && !IsSha256Hash(envelope.PreviousCheckpointHash))
			|| !string.Equals(
				envelope.SourceAuthorityKeyId,
				ComputeKeyId(sourceAuthorityPublicKey),
				StringComparison.Ordinal))
		{
			error = "migration_checkpoint_decrypt_invalid";
			return false;
		}

		ReadOnlySpan<byte> nonce = envelope.NonceMemory.Span;
		ReadOnlySpan<byte> ciphertext = envelope.CiphertextMemory.Span;
		ReadOnlySpan<byte> tag = envelope.TagMemory.Span;
		ReadOnlySpan<byte> signature = envelope.SignatureMemory.Span;
		if (nonce.Length != AesNonceBytes
			|| ciphertext.Length == 0
			|| tag.Length != AesTagBytes
			|| signature.Length == 0
			|| !string.Equals(
				ComputeCiphertextHash(nonce, ciphertext, tag),
				envelope.CiphertextHash,
				StringComparison.Ordinal))
		{
			error = "migration_checkpoint_ciphertext_invalid";
			return false;
		}

		byte[] aad = Array.Empty<byte>();
		byte[] signaturePayload = Array.Empty<byte>();
		bool plaintextOwnershipTransferred = false;
		try
		{
			aad = BuildCheckpointContextBytes(envelope.Context);
			signaturePayload = BuildEnvelopeSignatureBytes(
				aad,
				nonce,
				tag,
				envelope.PlaintextHash,
				envelope.CiphertextHash,
				envelope.PreviousCheckpointHash,
				envelope.SourceAuthorityKeyId);
			using ECDsa verifier = CreateSigner();
			verifier.ImportSubjectPublicKeyInfo(sourceAuthorityPublicKey, out int bytesRead);
			if (bytesRead != sourceAuthorityPublicKey.Length
				|| !verifier.VerifyData(
					signaturePayload,
					signature,
					HashAlgorithmName.SHA256))
			{
				error = "migration_checkpoint_signature_invalid";
				return false;
			}

			plaintext = new byte[ciphertext.Length];
			using (var aes = new AesGcm(sessionKey, AesTagBytes))
			{
				aes.Decrypt(nonce, ciphertext, tag, plaintext, aad);
			}

			if (!string.Equals(
				NetworkAuthorityCheckpointCodec.ComputeHash(plaintext),
				envelope.PlaintextHash,
				StringComparison.Ordinal))
			{
				CryptographicOperations.ZeroMemory(plaintext);
				plaintext = Array.Empty<byte>();
				error = "migration_checkpoint_plaintext_hash_invalid";
				return false;
			}

			plaintextOwnershipTransferred = true;
			return true;
		}
		catch (CryptographicException)
		{
			if (plaintext.Length > 0)
			{
				CryptographicOperations.ZeroMemory(plaintext);
			}
			plaintext = Array.Empty<byte>();
			error = "migration_checkpoint_decrypt_failed";
			return false;
		}
		finally
		{
			if (!plaintextOwnershipTransferred && plaintext.Length > 0)
			{
				CryptographicOperations.ZeroMemory(plaintext);
				plaintext = Array.Empty<byte>();
			}
			CryptographicOperations.ZeroMemory(signaturePayload);
			CryptographicOperations.ZeroMemory(aad);
		}
	}

	private static byte[] BuildCheckpointContextBytes(MigrationCheckpointSecurityContext context)
	{
		int length = checked(
			GetEncodedStringSize(EncryptionAlgorithm)
			+ GetEncodedStringSize(context.SessionId)
			+ GetEncodedStringSize(context.MatchId)
			+ sizeof(long)
			+ sizeof(long)
			+ GetEncodedStringSize(context.CandidatePlayerSessionId)
			+ sizeof(long)
			+ sizeof(int));
		byte[] result = new byte[length];
		var writer = new ExactBufferWriter(result);
		writer.WriteString(EncryptionAlgorithm);
		writer.WriteString(context.SessionId);
		writer.WriteString(context.MatchId);
		writer.WriteInt64(context.AuthorityEpoch);
		writer.WriteInt64(context.CheckpointId);
		writer.WriteString(context.CandidatePlayerSessionId);
		writer.WriteInt64(context.PlanRevision);
		writer.WriteInt32(context.ProtocolVersion);
		return result;
	}

	private static byte[] BuildMigrationPlanSignatureBytes(NetworkMigrationPlan plan)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		WriteString(writer, SignatureAlgorithm);
		writer.Write(plan.ProtocolVersion);
		WriteString(writer, plan.SessionId);
		WriteString(writer, plan.MatchId);
		writer.Write(plan.SourceEpoch);
		writer.Write(plan.TargetEpoch);
		writer.Write(plan.CheckpointId);
		WriteString(writer, plan.CheckpointHash);
		WriteString(writer, plan.CheckpointCiphertextHash);
		WriteString(writer, plan.PreviousCheckpointHash);
		writer.Write(plan.PlanRevision);
		WriteString(writer, plan.CandidatePlayerSessionId);
		writer.Write(plan.CandidateSeat);
		WriteString(writer, plan.CandidateSuccessorPublicKey);
		WriteString(writer, plan.CandidateSuccessorKeyId);
		writer.Write((int)plan.Transport);
		WriteMigrationTransportDescriptor(writer, plan);
		WriteString(writer, plan.SourcePlayerSessionId);
		WriteString(writer, plan.SourceAuthorityKeyId);
		writer.Write((int)plan.Phase);
		writer.Flush();
		return stream.ToArray();
	}

	private static void WriteMigrationTransportDescriptor(BinaryWriter writer, NetworkMigrationPlan plan)
	{
		switch (plan.Transport)
		{
			case NetworkTransport.Lan when plan.TransportDescriptor.Lan is { } lan:
				WriteString(writer, lan.ObservedHostAddress);
				WriteString(writer, lan.CandidateHostAddress);
				writer.Write(lan.Port);
				writer.Write(lan.MaxClients);
				writer.Write((int)lan.PeerRebuildMode);
				break;
			case NetworkTransport.Steam when plan.TransportDescriptor.Steam is { } steam:
				writer.Write(steam.LobbyId);
				writer.Write(steam.SourceLobbyOwnerSteamId);
				writer.Write(steam.CandidateSteamId);
				writer.Write(steam.ExpectedLobbyOwnerSteamId);
				writer.Write((int)steam.PeerRebuildMode);
				break;
		}
	}

	private static byte[] BuildMigrationPlanRevocationSignatureBytes(NetworkMigrationPlanRevocation revocation)
	{
		using var stream = new MemoryStream();
		using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
		WriteString(writer, SignatureAlgorithm);
		writer.Write(revocation.ProtocolVersion);
		writer.Write(revocation.PlanRevision);
		writer.Write(revocation.RevokedPlanRevision);
		writer.Write(revocation.RevokedCheckpointId);
		WriteString(writer, revocation.SessionId);
		WriteString(writer, revocation.MatchId);
		writer.Write(revocation.SourceEpoch);
		WriteString(writer, revocation.SourceAuthorityKeyId);
		writer.Flush();
		return stream.ToArray();
	}

	private static byte[] BuildEnvelopeSignatureBytes(
		ReadOnlySpan<byte> checkpointContextBytes,
		ReadOnlySpan<byte> nonce,
		ReadOnlySpan<byte> tag,
		string plaintextHash,
		string ciphertextHash,
		string previousCheckpointHash,
		string sourceAuthorityKeyId)
	{
		int length = checked(
			checkpointContextBytes.Length
			+ sizeof(int) + nonce.Length
			+ sizeof(int) + tag.Length
			+ GetEncodedStringSize(plaintextHash)
			+ GetEncodedStringSize(ciphertextHash)
			+ GetEncodedStringSize(previousCheckpointHash)
			+ GetEncodedStringSize(sourceAuthorityKeyId));
		byte[] result = new byte[length];
		var writer = new ExactBufferWriter(result);
		writer.WriteRawBytes(checkpointContextBytes);
		writer.WriteBytes(nonce);
		writer.WriteBytes(tag);
		writer.WriteString(plaintextHash);
		writer.WriteString(ciphertextHash);
		writer.WriteString(previousCheckpointHash);
		writer.WriteString(sourceAuthorityKeyId);
		return result;
	}

	private static string ComputeCiphertextHash(
		ReadOnlySpan<byte> nonce,
		ReadOnlySpan<byte> ciphertext,
		ReadOnlySpan<byte> tag)
	{
		using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
		hash.AppendData(nonce);
		hash.AppendData(ciphertext);
		hash.AppendData(tag);
		return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
	}

	private static bool IsSha256Hash(string value)
	{
		if (value is not { Length: 64 })
		{
			return false;
		}

		foreach (char character in value)
		{
			if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
			{
				return false;
			}
		}

		return true;
	}

	private static void WriteString(BinaryWriter writer, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
		WriteBytes(writer, bytes);
	}

	private static void WriteBytes(BinaryWriter writer, ReadOnlySpan<byte> bytes)
	{
		writer.Write(bytes.Length);
		writer.Write(bytes);
	}

	private static int GetEncodedStringSize(string? value)
	{
		return checked(sizeof(int) + Encoding.UTF8.GetByteCount(value ?? string.Empty));
	}

	private ref struct ExactBufferWriter
	{
		private readonly Span<byte> _buffer;
		private int _offset;

		internal ExactBufferWriter(Span<byte> buffer)
		{
			_buffer = buffer;
			_offset = 0;
		}

		internal void WriteInt32(int value)
		{
			BinaryPrimitives.WriteInt32LittleEndian(_buffer[_offset..], value);
			_offset += sizeof(int);
		}

		internal void WriteInt64(long value)
		{
			BinaryPrimitives.WriteInt64LittleEndian(_buffer[_offset..], value);
			_offset += sizeof(long);
		}

		internal void WriteString(string value)
		{
			int byteCount = Encoding.UTF8.GetByteCount(value);
			WriteInt32(byteCount);
			_offset += Encoding.UTF8.GetBytes(value, _buffer[_offset..]);
		}

		internal void WriteBytes(ReadOnlySpan<byte> bytes)
		{
			WriteInt32(bytes.Length);
			WriteRawBytes(bytes);
		}

		internal void WriteRawBytes(ReadOnlySpan<byte> bytes)
		{
			bytes.CopyTo(_buffer[_offset..]);
			_offset += bytes.Length;
		}
	}
}
