using System;
using System.Security.Cryptography;
using System.Text;

public sealed record RoomPasswordVerifier
{
	public static RoomPasswordVerifier Empty { get; } = new();

	public int Version { get; init; }
	public int Iterations { get; init; }
	public string Salt { get; init; } = string.Empty;
	public string Verifier { get; init; } = string.Empty;
	public bool HasPassword => Version > 0
		&& Iterations > 0
		&& !string.IsNullOrEmpty(Salt)
		&& !string.IsNullOrEmpty(Verifier);
}

public static class RoomPasswordRules
{
	public const int MaxLength = 24;
	public const int VerifierVersion = 1;
	public const int VerifierIterations = 210_000;
	public const int SaltBytes = 16;
	public const int ChallengeBytes = 32;
	public const int DerivedKeyBytes = 32;

	public static string Normalize(string value)
	{
		string normalized = value?.Trim() ?? string.Empty;
		return normalized.Length <= MaxLength ? normalized : normalized[..MaxLength];
	}

	public static bool HasPassword(string value)
	{
		return !string.IsNullOrEmpty(Normalize(value));
	}

	public static RoomPasswordVerifier CreateVerifier(string password)
	{
		string normalized = Normalize(password);
		if (string.IsNullOrEmpty(normalized))
		{
			return RoomPasswordVerifier.Empty;
		}

		byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);
		byte[] verifier = Rfc2898DeriveBytes.Pbkdf2(
			normalized,
			salt,
			VerifierIterations,
			HashAlgorithmName.SHA256,
			DerivedKeyBytes);
		return new RoomPasswordVerifier
		{
			Version = VerifierVersion,
			Iterations = VerifierIterations,
			Salt = EncodeBase64Url(salt),
			Verifier = EncodeBase64Url(verifier),
		};
	}

	public static byte[] CreateChallenge()
	{
		return RandomNumberGenerator.GetBytes(ChallengeBytes);
	}

	public static bool TryComputeChallengeProof(
		string password,
		RoomPasswordVerifier verifier,
		ReadOnlySpan<byte> challenge,
		string sessionId,
		out byte[] proof)
	{
		if (verifier is null || !verifier.HasPassword)
		{
			proof = Array.Empty<byte>();
			return false;
		}

		return TryComputeChallengeProof(
			password,
			verifier.Version,
			verifier.Iterations,
			verifier.Salt,
			challenge,
			sessionId,
			out proof);
	}

	public static bool TryComputeChallengeProof(
		string password,
		int verifierVersion,
		int verifierIterations,
		string verifierSalt,
		ReadOnlySpan<byte> challenge,
		string sessionId,
		out byte[] proof)
	{
		proof = Array.Empty<byte>();
		if (!TryDecodeKdfParameters(verifierVersion, verifierIterations, verifierSalt, out byte[] salt)
			|| challenge.Length != ChallengeBytes
			|| string.IsNullOrWhiteSpace(sessionId))
		{
			return false;
		}

		byte[] derivedKey = Rfc2898DeriveBytes.Pbkdf2(
			Normalize(password),
			salt,
			verifierIterations,
			HashAlgorithmName.SHA256,
			DerivedKeyBytes);
		try
		{
			proof = HMACSHA256.HashData(derivedKey, BuildChallengeMessage(challenge, sessionId));
			return true;
		}
		finally
		{
			CryptographicOperations.ZeroMemory(derivedKey);
		}
	}

	public static bool VerifyChallengeProof(
		RoomPasswordVerifier verifier,
		ReadOnlySpan<byte> challenge,
		string sessionId,
		ReadOnlySpan<byte> proof)
	{
		if (!TryDecodeVerifier(verifier, out _, out byte[] verifierBytes)
			|| challenge.Length != ChallengeBytes
			|| proof.Length != SHA256.HashSizeInBytes
			|| string.IsNullOrWhiteSpace(sessionId))
		{
			return false;
		}

		try
		{
			byte[] expected = HMACSHA256.HashData(verifierBytes, BuildChallengeMessage(challenge, sessionId));
			return CryptographicOperations.FixedTimeEquals(expected, proof);
		}
		finally
		{
			CryptographicOperations.ZeroMemory(verifierBytes);
		}
	}

	public static bool IsVerifierValid(RoomPasswordVerifier? verifier)
	{
		return verifier is not null
			&& (verifier.HasPassword
				? TryDecodeVerifier(verifier, out _, out _)
				: verifier.Version == 0
					&& verifier.Iterations == 0
					&& string.IsNullOrEmpty(verifier.Salt)
					&& string.IsNullOrEmpty(verifier.Verifier));
	}

	private static bool TryDecodeVerifier(
		RoomPasswordVerifier verifier,
		out byte[] salt,
		out byte[] verifierBytes)
	{
		salt = Array.Empty<byte>();
		verifierBytes = Array.Empty<byte>();
		if (verifier is null
			|| !TryDecodeKdfParameters(verifier.Version, verifier.Iterations, verifier.Salt, out salt)
			|| !TryDecodeBase64Url(verifier.Verifier, out verifierBytes)
			|| verifierBytes.Length != DerivedKeyBytes)
		{
			salt = Array.Empty<byte>();
			verifierBytes = Array.Empty<byte>();
			return false;
		}

		return true;
	}

	private static bool TryDecodeKdfParameters(
		int verifierVersion,
		int verifierIterations,
		string verifierSalt,
		out byte[] salt)
	{
		salt = Array.Empty<byte>();
		if (verifierVersion != VerifierVersion
			|| verifierIterations < 100_000
			|| verifierIterations > 1_000_000
			|| !TryDecodeBase64Url(verifierSalt, out salt)
			|| salt.Length != SaltBytes)
		{
			salt = Array.Empty<byte>();
			return false;
		}

		return true;
	}

	private static byte[] BuildChallengeMessage(ReadOnlySpan<byte> challenge, string sessionId)
	{
		byte[] sessionBytes = Encoding.UTF8.GetBytes(sessionId);
		byte[] message = new byte[challenge.Length + sessionBytes.Length + sizeof(int)];
		challenge.CopyTo(message);
		BitConverter.TryWriteBytes(message.AsSpan(challenge.Length, sizeof(int)), sessionBytes.Length);
		sessionBytes.CopyTo(message, challenge.Length + sizeof(int));
		return message;
	}

	private static string EncodeBase64Url(ReadOnlySpan<byte> bytes)
	{
		return Convert.ToBase64String(bytes)
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}

	private static bool TryDecodeBase64Url(string value, out byte[] bytes)
	{
		bytes = Array.Empty<byte>();
		if (string.IsNullOrWhiteSpace(value))
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
			return true;
		}
		catch (FormatException)
		{
			return false;
		}
	}
}
