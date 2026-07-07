using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private const string DebugRecordingSetupJellyfishCaptureShowcase01 = "jellyfish_capture_showcase_01";
	private const string DebugRecordingSetupShrimpCaptureVictoryOrder01 = "shrimp_capture_victory_order_01";
	private const string DebugRecordingSetupShrimpPairCaptureReturn01 = "shrimp_pair_capture_return_01";
	private const string DebugRecordingSetupShrimpQuestionRejudgeManual01 = "shrimp_question_rejudge_manual_01";
	private const string JellyfishEnemyAvatarId = "jellyfish";
	private const string ShrimpEnemyAvatarId = "shrimp";

	private bool TrySetupDebugRecordingDeal()
	{
		string setupId = debug_recording_setup_id?.Trim() ?? string.Empty;
		if (string.IsNullOrEmpty(setupId))
		{
			return false;
		}

		if (string.Equals(setupId, DebugRecordingSetupJellyfishCaptureShowcase01, StringComparison.Ordinal))
		{
			if (!adventure_mode
				|| player_count != 2
				|| !string.Equals(adventure_enemy_avatar_id, JellyfishEnemyAvatarId, StringComparison.Ordinal))
			{
				GD.PushWarning(
					$"Debug recording setup '{setupId}' requires 2-player adventure mode against '{JellyfishEnemyAvatarId}'; falling back to normal deal.");
				return false;
			}

			bool jellyfishSetupSucceeded = TrySetupJellyfishCaptureShowcaseRecordingDeal();
			if (!jellyfishSetupSucceeded)
			{
				GD.PushWarning($"Debug recording setup '{setupId}' failed; falling back to normal deal.");
				return false;
			}

			return true;
		}

		if (string.Equals(setupId, DebugRecordingSetupShrimpCaptureVictoryOrder01, StringComparison.Ordinal))
		{
			if (!adventure_mode
				|| player_count != 2
				|| !string.Equals(adventure_enemy_avatar_id, ShrimpEnemyAvatarId, StringComparison.Ordinal))
			{
				GD.PushWarning(
					$"Debug recording setup '{setupId}' requires 2-player adventure mode against '{ShrimpEnemyAvatarId}'; falling back to normal deal.");
				return false;
			}

			bool shrimpSetupSucceeded = TrySetupShrimpCaptureVictoryOrderRecordingDeal();
			if (!shrimpSetupSucceeded)
			{
				GD.PushWarning($"Debug recording setup '{setupId}' failed; falling back to normal deal.");
				return false;
			}

			return true;
		}

		if (string.Equals(setupId, DebugRecordingSetupShrimpPairCaptureReturn01, StringComparison.Ordinal))
		{
			if (!adventure_mode
				|| player_count != 2
				|| !string.Equals(adventure_enemy_avatar_id, ShrimpEnemyAvatarId, StringComparison.Ordinal))
			{
				GD.PushWarning(
					$"Debug recording setup '{setupId}' requires 2-player adventure mode against '{ShrimpEnemyAvatarId}'; falling back to normal deal.");
				return false;
			}

			bool pairCaptureSetupSucceeded = TrySetupShrimpPairCaptureReturnRecordingDeal();
			if (!pairCaptureSetupSucceeded)
			{
				GD.PushWarning($"Debug recording setup '{setupId}' failed; falling back to normal deal.");
				return false;
			}

			return true;
		}

		if (string.Equals(setupId, DebugRecordingSetupShrimpQuestionRejudgeManual01, StringComparison.Ordinal))
		{
			if (!adventure_mode
				|| player_count != 2
				|| !string.Equals(adventure_enemy_avatar_id, ShrimpEnemyAvatarId, StringComparison.Ordinal))
			{
				GD.PushWarning(
					$"Debug recording setup '{setupId}' requires 2-player adventure mode against '{ShrimpEnemyAvatarId}'; falling back to normal deal.");
				return false;
			}

			bool rejudgeSetupSucceeded = TrySetupShrimpQuestionRejudgeManualRecordingDeal();
			if (!rejudgeSetupSucceeded)
			{
				GD.PushWarning($"Debug recording setup '{setupId}' failed; falling back to normal deal.");
				return false;
			}

			return true;
		}

		GD.PushWarning($"Unknown debug recording setup '{setupId}'; falling back to normal deal.");
		return false;
	}

	private bool TrySetupShrimpCaptureVictoryOrderRecordingDeal()
	{
		return TrySetupSharedBigCaptureRecordingDeal();
	}

	private bool TrySetupShrimpPairCaptureReturnRecordingDeal()
	{
		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Clubs", "5"),
			("Spades", "A"),
			("Diamonds", "9"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Diamonds:9"]);

		_tableCards.Add(new TableCard(cards["Clubs:5"], true));

		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	private bool TrySetupJellyfishCaptureShowcaseRecordingDeal()
	{
		return TrySetupSharedBigCaptureRecordingDeal();
	}

	private bool TrySetupShrimpQuestionRejudgeManualRecordingDeal()
	{
		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "4"),
			("Clubs", "4"),
			("Diamonds", "6"),
			("Spades", "9"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		ItemCard? questionMark = CreateItemCard(QuestionMarkItemCardId);
		if (!questionMark.HasValue)
		{
			return false;
		}

		if (!DebugAcquireAdventureUpgrade("diamonds_rejudge_once"))
		{
			return false;
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:4"]);
		_playerDecks[MainPlayerIndex].Add(cards["Diamonds:6"]);
		_playerHands[1].Add(cards["Spades:9"]);

		_tableCards.Add(new TableCard(cards["Clubs:4"], true));
		_tableCards.Add(new TableCard(null, true, questionMark.Value));
		_tableCards.Add(new TableCard(cards["Diamonds:6"], true));

		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	private bool TrySetupSharedBigCaptureRecordingDeal()
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
			ToPotentialCaptureCard(playerIndex, _playerHands[playerIndex][handIndex]));
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
