using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private const string DebugRecordingSetupJellyfishCaptureShowcase01 = "jellyfish_capture_showcase_01";
	private const string DebugRecordingSetupShrimpCaptureVictoryOrder01 = "shrimp_capture_victory_order_01";
	private const string DebugRecordingSetupShrimpPairCaptureReturn01 = "shrimp_pair_capture_return_01";
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

		ImmutableArray<PlayerStateSnapshot> players = ImmutableArray.Create(
			CreateScriptedPlayerState(
				MainPlayerIndex,
				new[] { cards["Spades:A"] },
				new[] { cards["Hearts:5"] }),
			CreateScriptedPlayerState(
				1,
				Array.Empty<PlayingCard>(),
				new[] { cards["Diamonds:9"] }));
		ImmutableArray<TableCardState> tableCards = ImmutableArray.Create(
			ToRuleTableCardState(new TableCard(cards["Clubs:5"], true)));
		if (!TryInitializeScriptedRuleMatch(players, tableCards))
		{
			return false;
		}

		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	private bool TrySetupJellyfishCaptureShowcaseRecordingDeal()
	{
		return TrySetupSharedBigCaptureRecordingDeal();
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

		ImmutableArray<PlayerStateSnapshot> players = ImmutableArray.Create(
			CreateScriptedPlayerState(
				MainPlayerIndex,
				new[] { cards["Spades:A"] },
				new[] { cards["Hearts:10"] }),
			CreateScriptedPlayerState(
				1,
				new[] { cards["Diamonds:4"] },
				new[] { cards["Clubs:3"] }));
		ImmutableArray<TableCardState> tableCards = ImmutableArray.Create(
			ToRuleTableCardState(new TableCard(cards["Clubs:10"], true)),
			ToRuleTableCardState(new TableCard(cards["Diamonds:3"], false)),
			ToRuleTableCardState(new TableCard(cards["Spades:4"], true)),
			ToRuleTableCardState(new TableCard(cards["Hearts:5"], false)),
			ToRuleTableCardState(new TableCard(cards["Clubs:6"], true)),
			ToRuleTableCardState(new TableCard(cards["Diamonds:8"], false)),
			ToRuleTableCardState(new TableCard(cards["Spades:7"], true)),
			ToRuleTableCardState(new TableCard(cards["Hearts:9"], false)),
			ToRuleTableCardState(new TableCard(cards["Clubs:2"], true)),
			ToRuleTableCardState(new TableCard(cards["Clubs:A"], false)));
		if (!TryInitializeScriptedRuleMatch(players, tableCards))
		{
			return false;
		}

		_isOpeningDealAnimationPlaying = false;
		return true;
	}

	private static PlayerStateSnapshot CreateScriptedPlayerState(
		int seat,
		IReadOnlyList<PlayingCard> deck,
		IReadOnlyList<PlayingCard> hand)
	{
		return new PlayerStateSnapshot(
			seat,
			deck.Select(ToRuleCardState).ToImmutableArray(),
			hand.Select(ToRuleCardState).ToImmutableArray(),
			ImmutableArray<LogicalItemState>.Empty,
			Score: 0);
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

	public int DebugGetTableCardReferenceIdentity(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return 0;
		}

		return RuntimeHelpers.GetHashCode(_tableCards[tableCardIndex]);
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

		return MatchReadModel.GetPotentialNormalCaptureCount(
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
