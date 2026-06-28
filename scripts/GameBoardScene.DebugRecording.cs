using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private const string DebugRecordingSetupJellyfishCaptureShowcase01 = "jellyfish_capture_showcase_01";
	private const string JellyfishEnemyAvatarId = "jellyfish";

	private bool TrySetupDebugRecordingDeal()
	{
		string setupId = debug_recording_setup_id?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(setupId))
		{
			return false;
		}

		if (!string.Equals(setupId, DebugRecordingSetupJellyfishCaptureShowcase01, StringComparison.Ordinal))
		{
			GD.PushWarning($"Unknown debug recording setup '{setupId}'; falling back to normal deal.");
			return false;
		}

		if (!adventure_mode
			|| player_count != 2
			|| !string.Equals(adventure_enemy_avatar_id, JellyfishEnemyAvatarId, StringComparison.Ordinal))
		{
			GD.PushWarning(
				$"Debug recording setup '{setupId}' requires 2-player adventure mode against '{JellyfishEnemyAvatarId}'; falling back to normal deal.");
			return false;
		}

		bool setupSucceeded = TrySetupJellyfishCaptureShowcaseRecordingDeal();
		if (!setupSucceeded)
		{
			GD.PushWarning($"Debug recording setup '{setupId}' failed; falling back to normal deal.");
			return false;
		}

		return true;
	}

	private bool TrySetupJellyfishCaptureShowcaseRecordingDeal()
	{
		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "10"),
			("Spades", "A"),
			("Clubs", "3"),
			("Diamonds", "4"),
			("Clubs", "10"),
			("Diamonds", "3"),
			("Spades", "4"),
			("Hearts", "5"),
			("Clubs", "6"),
			("Diamonds", "8"),
			("Spades", "7"),
			("Hearts", "9"),
			("Clubs", "2"),
			("Clubs", "A"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:10"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Clubs:3"]);
		_playerDecks[1].Add(cards["Diamonds:4"]);

		_tableCards.Add(new TableCard(cards["Clubs:10"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:3"], false));
		_tableCards.Add(new TableCard(cards["Spades:4"], true));
		_tableCards.Add(new TableCard(cards["Hearts:5"], false));
		_tableCards.Add(new TableCard(cards["Clubs:6"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:8"], false));
		_tableCards.Add(new TableCard(cards["Spades:7"], true));
		_tableCards.Add(new TableCard(cards["Hearts:9"], false));
		_tableCards.Add(new TableCard(cards["Clubs:2"], true));
		_tableCards.Add(new TableCard(cards["Clubs:A"], false));

		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	public int DebugGetTableCardCount()
	{
		return _tableCards.Count;
	}

	public string DebugGetTableCardCode(int tableCardIndex)
	{
		if (tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count
			|| !_tableCards[tableCardIndex].Card.HasValue)
		{
			return string.Empty;
		}

		return FormatPlayingCardCode(_tableCards[tableCardIndex].Card!.Value);
	}

	public bool DebugIsTableCardFaceUp(int tableCardIndex)
	{
		return tableCardIndex >= 0
			&& tableCardIndex < _tableCards.Count
			&& _tableCards[tableCardIndex].IsFaceUp;
	}

	public string DebugGetHandCardCode(int playerIndex, int handIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return string.Empty;
		}

		return FormatPlayingCardCode(_playerHands[playerIndex][handIndex]);
	}

	public int DebugGetPotentialCaptureCountForHandCard(int playerIndex, int handIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return 0;
		}

		return PotentialCaptureRules.GetPotentialNormalCaptureCount(
			ToCaptureScoringTableCards(),
			ToPotentialCaptureCard(_playerHands[playerIndex][handIndex]));
	}

	public int DebugGetDeckCardCountForPlayer(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return 0;
		}

		return _playerDecks[playerIndex].Count;
	}

	public string DebugGetDeckCardCode(int playerIndex, int deckIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerDecks.Count
			|| deckIndex < 0
			|| deckIndex >= _playerDecks[playerIndex].Count)
		{
			return string.Empty;
		}

		return FormatPlayingCardCode(_playerDecks[playerIndex][deckIndex]);
	}

	private static string FormatPlayingCardCode(PlayingCard card)
	{
		return $"{card.Suit}:{card.Rank}";
	}
}
