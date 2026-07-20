using System;
using System.Security.Cryptography;

public static class SeatCredentialRules
{
	public const int PlayerSessionIdBytes = 16;
	public const int CredentialBytes = 32;
	public const long InitialGeneration = 1;

	public static string IssuePlayerSessionId()
	{
		return Convert.ToHexString(RandomNumberGenerator.GetBytes(PlayerSessionIdBytes)).ToLowerInvariant();
	}

	public static string IssueCredential()
	{
		return Convert.ToBase64String(RandomNumberGenerator.GetBytes(CredentialBytes))
			.TrimEnd('=')
			.Replace('+', '-')
			.Replace('/', '_');
	}

	public static string ComputeVerifier(string credential)
	{
		if (!IsCredentialEncodingValid(credential))
		{
			return string.Empty;
		}

		return Convert.ToHexString(
			SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(credential)))
			.ToLowerInvariant();
	}

	public static bool Verify(string credential, string verifier)
	{
		if (!IsCredentialEncodingValid(credential)
			|| verifier is not { Length: 64 })
		{
			return false;
		}

		try
		{
			byte[] actual = SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(credential));
			byte[] expected = Convert.FromHexString(verifier);
			return expected.Length == actual.Length
				&& CryptographicOperations.FixedTimeEquals(actual, expected);
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static bool IsCredentialEncodingValid(string? credential)
	{
		if (string.IsNullOrWhiteSpace(credential)
			|| credential.Length > NetworkProtocol.MaxCredentialEncodedLength)
		{
			return false;
		}

		foreach (char character in credential)
		{
			if (!(character is >= 'A' and <= 'Z'
				or >= 'a' and <= 'z'
				or >= '0' and <= '9'
				or '-' or '_'))
			{
				return false;
			}
		}

		return true;
	}

	public static bool IsVerifierValid(string? verifier)
	{
		if (verifier is not { Length: 64 })
		{
			return false;
		}

		foreach (char character in verifier)
		{
			if (!(character is >= '0' and <= '9' or >= 'a' and <= 'f'))
			{
				return false;
			}
		}

		return true;
	}

	public static bool TryBeginRotation(
		SeatControlState current,
		int boundPeerId,
		out SeatControlState pending,
		out string nextCredential)
	{
		pending = current;
		nextCredential = string.Empty;
		if (boundPeerId <= 0
			|| current.CredentialGeneration < InitialGeneration
			|| !IsVerifierValid(current.CredentialVerifier)
			|| current.PendingCredentialGeneration != 0
			|| !string.IsNullOrEmpty(current.PendingCredentialVerifier)
			|| current.PendingCredentialPeerId != 0
			|| current.CredentialGeneration == long.MaxValue)
		{
			return false;
		}

		nextCredential = IssueCredential();
		pending = current with
		{
			PendingCredentialVerifier = ComputeVerifier(nextCredential),
			PendingCredentialGeneration = current.CredentialGeneration + 1,
			PendingCredentialPeerId = boundPeerId,
		};
		return true;
	}

	public static bool TryAcknowledge(
		SeatControlState current,
		int senderPeerId,
		long generation,
		string credential,
		out SeatControlState committed)
	{
		committed = current;
		if (senderPeerId <= 0)
		{
			return false;
		}

		if (current.PendingCredentialGeneration > 0)
		{
			if (current.PendingCredentialPeerId != senderPeerId
				|| generation != current.PendingCredentialGeneration
				|| !Verify(credential, current.PendingCredentialVerifier))
			{
				return false;
			}

			committed = current with
			{
				CredentialVerifier = current.PendingCredentialVerifier,
				CredentialGeneration = current.PendingCredentialGeneration,
				PendingCredentialVerifier = string.Empty,
				PendingCredentialGeneration = 0,
				PendingCredentialPeerId = 0,
			};
			return true;
		}

		return generation == current.CredentialGeneration
			&& current.PeerId == senderPeerId
			&& Verify(credential, current.CredentialVerifier);
	}

	public static SeatControlState CancelPendingRotation(SeatControlState current)
	{
		return current with
		{
			PendingCredentialVerifier = string.Empty,
			PendingCredentialGeneration = 0,
			PendingCredentialPeerId = 0,
		};
	}
}
