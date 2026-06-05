using System;
using Godot;

public partial class GameBoardScene
{
	private bool TryGetNetworkPlayerName(int playerIndex, out string playerName)
	{
		playerName = string.Empty;
		if (string.IsNullOrWhiteSpace(_playerNamesCsv))
		{
			return false;
		}

		string[] names = SplitNetworkCsv(_playerNamesCsv);
		if (playerIndex < 0 || playerIndex >= names.Length)
		{
			return false;
		}

		playerName = PlayerNameRules.Normalize(names[playerIndex]);
		return true;
	}

	private bool TryGetNetworkAvatarId(int playerIndex, out string avatarId)
	{
		avatarId = string.Empty;
		if (string.IsNullOrWhiteSpace(_playerAvatarIdsCsv))
		{
			return false;
		}

		string[] avatarIds = SplitNetworkCsv(_playerAvatarIdsCsv);
		if (playerIndex < 0 || playerIndex >= avatarIds.Length)
		{
			return false;
		}

		avatarId = CharacterAvatarCatalog.NormalizeAvatarId(avatarIds[playerIndex]);
		return !string.IsNullOrWhiteSpace(avatarId);
	}

	private static string[] SplitNetworkCsv(string csv)
	{
		return csv.Split(',', StringSplitOptions.TrimEntries);
	}
}
