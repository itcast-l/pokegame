using System;
using System.Collections.Generic;

internal static class Program
{
	private static int _passed;
	private static int _failed;

	public static int Main()
	{
		Run("capture scoring applies double effect and skips buried cards", CaptureScoringAppliesDoubleEffectAndSkipsBuriedCards);
		Run("capture scoring suppresses slime coated cards", CaptureScoringSuppressesSlimeCoatedCards);
		Run("capture scoring redirects pair destroy owner", CaptureScoringRedirectsPairDestroyOwner);
		Run("potential capture ignores buried matches", PotentialCaptureIgnoresBuriedMatches);
		Run("potential capture prefers royal threshold", PotentialCapturePrefersRoyalThreshold);
		Run("game loop phase precedence is stable", GameLoopPhasePrecedenceIsStable);
		Run("turn rules skip inactive players", TurnRulesSkipInactivePlayers);
		Run("turn rules find single remaining player", TurnRulesFindSingleRemainingPlayer);

		Console.WriteLine($"{_passed} passed, {_failed} failed");
		return _failed == 0 ? 0 : 1;
	}

	private static void CaptureScoringAppliesDoubleEffectAndSkipsBuriedCards()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5),
			TableCard("3", 7, isBuriedInSand: true),
			TableCard("4", 3, hasDoubleCollectItemEffect: true),
			TableCard("5", 9),
		};

		int points = CaptureScoringRules.GetCapturedTablePoints(tableCards, 0, DefaultScoringContext);

		Equal(16, points);
	}

	private static void CaptureScoringSuppressesSlimeCoatedCards()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5, isSlimeCoated: true),
			TableCard("3", 4),
			TableCard("4", 9),
		};
		var context = new CaptureScoringRules.Context(
			SuppressSlimeCoatedScore: true,
			ApplyScoreReductionPerCard: false,
			ScoreReductionPerCard: 0);

		int points = CaptureScoringRules.GetCapturedTablePoints(tableCards, 0, context);

		Equal(4, points);
	}

	private static void CaptureScoringRedirectsPairDestroyOwner()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5, hasPairDestroyItemEffect: true, itemOwnerPlayerIndex: 1),
			TableCard("3", 4),
			TableCard("4", 9, hasPairDestroyItemEffect: true, itemOwnerPlayerIndex: 2),
		};

		int playerIndex = CaptureScoringRules.GetCapturedTableScorePlayerIndex(
			tableCards,
			captureStartIndex: 0,
			fallbackPlayerIndex: 0,
			playerCount: 4);

		Equal(2, playerIndex);
	}

	private static void PotentialCaptureIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			TableCard("A", 2, isBuriedInSand: true),
			TableCard("A", 3),
			TableCard("Q", 4),
		};
		var card = new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false);

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(3, captureCount);
		Equal(4, points);
	}

	private static void PotentialCapturePrefersRoyalThreshold()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 1),
			TableCard("3", 1),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = PotentialCaptureRules.TryPickBestCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			royalCaptureThreshold: 5,
			out int handIndex);

		True(picked);
		Equal(0, handIndex);
	}

	private static void GameLoopPhasePrecedenceIsStable()
	{
		Equal(GameLoopPhase.LeavingGame, GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			IsLeavingGame: true,
			IsGameOver: true,
			IsOpeningDeal: true,
			HasPendingItemSelection: true,
			IsProcessingCardPlay: true,
			IsResolvingTurnSequence: true,
			IsMainPlayerTurn: true)));
		Equal(GameLoopPhase.PendingMainPlayerSelection, GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			IsLeavingGame: false,
			IsGameOver: false,
			IsOpeningDeal: false,
			HasPendingItemSelection: true,
			IsProcessingCardPlay: true,
			IsResolvingTurnSequence: false,
			IsMainPlayerTurn: true)));
		Equal(GameLoopPhase.AwaitingMainPlayerAction, GameLoopPhaseRules.GetPhase(new GameLoopPhaseState(
			IsLeavingGame: false,
			IsGameOver: false,
			IsOpeningDeal: false,
			HasPendingItemSelection: false,
			IsProcessingCardPlay: false,
			IsResolvingTurnSequence: false,
			IsMainPlayerTurn: true)));
	}

	private static void TurnRulesSkipInactivePlayers()
	{
		int[] turnOrder = { 0, 2, 1, 3 };

		Equal(1, GameTurnRules.GetNextTurnPlayerIndex(turnOrder, currentPlayerIndex: 2, playerCount: 2, fallbackPlayerIndex: 0));
		Equal(0, GameTurnRules.GetNextTurnPlayerIndex(turnOrder, currentPlayerIndex: 7, playerCount: 2, fallbackPlayerIndex: 0));
	}

	private static void TurnRulesFindSingleRemainingPlayer()
	{
		var singleRemaining = new[]
		{
			new GameTurnRules.PlayerCardState(0, 0, false),
			new GameTurnRules.PlayerCardState(0, 0, true),
			new GameTurnRules.PlayerCardState(0, 0, false),
		};
		var multipleRemaining = new[]
		{
			new GameTurnRules.PlayerCardState(1, 0, false),
			new GameTurnRules.PlayerCardState(0, 1, false),
		};

		True(GameTurnRules.TryGetSingleRemainingPlayer(singleRemaining, playerCount: 3, out int remainingPlayerIndex));
		Equal(1, remainingPlayerIndex);
		False(GameTurnRules.TryGetSingleRemainingPlayer(multipleRemaining, playerCount: 2, out _));
	}

	private static CaptureScoringRules.Context DefaultScoringContext => new(
		SuppressSlimeCoatedScore: false,
		ApplyScoreReductionPerCard: false,
		ScoreReductionPerCard: 0);

	private static CaptureScoringRules.TableCard TableCard(
		string rank,
		int scoreValue,
		bool isBuriedInSand = false,
		bool isSlimeCoated = false,
		bool hasDoubleCollectItemEffect = false,
		bool hasPairDestroyItemEffect = false,
		int itemOwnerPlayerIndex = -1)
	{
		return new CaptureScoringRules.TableCard(
			new CaptureScoringRules.Card(rank, scoreValue, HasDoubleCollectEffect: false),
			isBuriedInSand,
			isSlimeCoated,
			hasDoubleCollectItemEffect,
			hasPairDestroyItemEffect,
			itemOwnerPlayerIndex);
	}

	private static void Run(string name, Action test)
	{
		try
		{
			test();
			_passed++;
			Console.WriteLine($"PASS {name}");
		}
		catch (Exception ex)
		{
			_failed++;
			Console.Error.WriteLine($"FAIL {name}: {ex.Message}");
		}
	}

	private static void Equal<T>(T expected, T actual)
	{
		if (!EqualityComparer<T>.Default.Equals(expected, actual))
		{
			throw new InvalidOperationException($"expected {expected}, got {actual}");
		}
	}

	private static void True(bool value)
	{
		if (!value)
		{
			throw new InvalidOperationException("expected true, got false");
		}
	}

	private static void False(bool value)
	{
		if (value)
		{
			throw new InvalidOperationException("expected false, got true");
		}
	}
}
