using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void AdventureAiExposesAllTwelveMainStageProfiles()
	{
		var expected = new[]
		{
			new EnemyAiRules.AdventureProfile(45, 25, 55, 55, false),
			new EnemyAiRules.AdventureProfile(55, 25, 65, 55, false),
			new EnemyAiRules.AdventureProfile(65, 20, 70, 55, false),
			new EnemyAiRules.AdventureProfile(70, 20, 75, 35, false),
			new EnemyAiRules.AdventureProfile(75, 15, 80, 35, true),
			new EnemyAiRules.AdventureProfile(78, 15, 85, 35, true),
			new EnemyAiRules.AdventureProfile(85, 10, 90, 20, true),
			new EnemyAiRules.AdventureProfile(90, 5, 92, 20, true),
			new EnemyAiRules.AdventureProfile(92, 5, 94, 20, true),
			new EnemyAiRules.AdventureProfile(95, 5, 96, 10, true),
			new EnemyAiRules.AdventureProfile(97, 3, 98, 10, true),
			new EnemyAiRules.AdventureProfile(65, 10, 90, 10, true),
		};

		for (int stage = 1; stage <= expected.Length; stage++)
		{
			Equal(expected[stage - 1], EnemyAiRules.GetAdventureProfile(stage));
		}
	}

	private static void AdventureAiSwitchesFromQuantityToPointsAtStageFive()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("5", 5),
			TableCard("6", 6),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: true),
		};

		EnemyAiRules.AdventureSelectionResult stageFour = EnemyAiRules.SelectAdventureCard(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(4),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCandidateOrdinal: 0);
		EnemyAiRules.AdventureSelectionResult stageFive = EnemyAiRules.SelectAdventureCard(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(5),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCandidateOrdinal: 0);

		Equal(0, stageFour.HandIndex);
		Equal(1, stageFive.HandIndex);
	}

	private static void AdventureAiBestCaptureThresholdIsExclusive()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(0, SelectAdventure(tableCards, handCards, 44, 1).HandIndex);
		Equal(1, SelectAdventure(tableCards, handCards, 45, 1).HandIndex);
	}

	private static void AdventureAiAnyCaptureThresholdIsExclusive()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(EnemyAiRules.AdventureSelectionKind.Capture, SelectAdventure(tableCards, handCards, 69, 0).Kind);
		Equal(EnemyAiRules.AdventureSelectionKind.None, SelectAdventure(tableCards, handCards, 70, 0).Kind);
	}

	private static void AdventureAiPairOnlyThresholdIsExclusive()
	{
		var tableCards = new[] { TableCard("A", 1) };
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(EnemyAiRules.AdventureSelectionKind.Capture, SelectAdventure(tableCards, handCards, 54, 0).Kind);
		Equal(EnemyAiRules.AdventureSelectionKind.None, SelectAdventure(tableCards, handCards, 55, 0).Kind);
	}

	private static void AdventureAiPairOnlyOrdinalSelectsAcrossHandPositions()
	{
		var tableCards = new[] { TableCard("A", 1) };
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(2, SelectAdventure(tableCards, handCards, 0, 1).HandIndex);
	}

	private static void AdventureAiRejectedCaptureNeverFallsThroughToBuild()
	{
		var tableCards = new[] { TableCard("A", 1), TableCard("2", 2) };
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(EnemyAiRules.AdventureSelectionKind.None, SelectAdventure(tableCards, handCards, 99, 1).Kind);
	}

	private static void AdventureAiBuildsOnlyWithAtMostTwoTableCards()
	{
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("Q", CollectsTable: false, HasDoubleEffect: false),
		};

		for (int tableCount = 0; tableCount <= 4; tableCount++)
		{
			var tableCards = new List<CaptureScoringRules.TableCard>();
			for (int index = 0; index < tableCount; index++)
			{
				tableCards.Add(TableCard((index + 2).ToString(), index + 2));
			}

			EnemyAiRules.AdventureSelectionKind expected = tableCount <= 2
				? EnemyAiRules.AdventureSelectionKind.Build
				: EnemyAiRules.AdventureSelectionKind.None;
			Equal(expected, SelectAdventure(tableCards, handCards, 54, 0).Kind);
		}
	}

	private static void AdventureAiBossRoyalThresholdWeightingPreservesLeftTieBreak()
	{
		var tableCards = new[]
		{
			TableCard("A", 0),
			TableCard("2", 0),
			TableCard("3", 0),
			TableCard("K", 0),
			TableCard("10", 10),
			TableCard("Q", 0),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: true),
		};
		EnemyAiRules.AdventureProfile pointsProfile = EnemyAiRules.GetAdventureProfile(12);

		EnemyAiRules.AdventureSelectionResult withoutBossThreshold = EnemyAiRules.SelectAdventureCard(
			tableCards, handCards, DefaultScoringContext, pointsProfile, int.MaxValue, 0, 0);
		EnemyAiRules.AdventureSelectionResult withBossThreshold = EnemyAiRules.SelectAdventureCard(
			tableCards, handCards, DefaultScoringContext, pointsProfile, 5, 0, 0);
		var tiedHandCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};
		EnemyAiRules.AdventureSelectionResult leftTie = EnemyAiRules.SelectAdventureCard(
			tableCards, tiedHandCards, DefaultScoringContext, pointsProfile, 5, 0, 0);

		Equal(1, withoutBossThreshold.HandIndex);
		Equal(0, withBossThreshold.HandIndex);
		Equal(0, leftTie.HandIndex);
	}

	private static EnemyAiRules.AdventureSelectionResult SelectAdventure(
		IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
		IReadOnlyList<PotentialCaptureRules.Card> handCards,
		int roll,
		int ordinal)
	{
		return EnemyAiRules.SelectAdventureCard(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: roll,
			randomCandidateOrdinal: ordinal);
	}

	private static void AiEarlyTableNewCardPriorityPicksNonMatchingCardBeforeCapture()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
		};
		var handCards = new[]
		{
			new CardPlayCard("A", CollectsTable: false),
			new CardPlayCard("K", CollectsTable: false),
		};

		bool picked = AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal: 0,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void AiEarlyTableNewCardPriorityYieldsAfterFourTableCards()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("3", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false),
		};
		var handCards = new[]
		{
			new CardPlayCard("K", CollectsTable: false),
		};

		bool picked = AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AiEarlyTableNewCardPriorityIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("K", IsFaceUp: true, IsBuriedInSand: true),
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false),
		};
		var handCards = new[]
		{
			new CardPlayCard("A", CollectsTable: false),
			new CardPlayCard("K", CollectsTable: false),
		};

		bool picked = AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal: 0,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void SoloAiDifficultyProfilesExposeExpectedValues()
	{
		SoloAiRules.Profile low = SoloAiRules.GetProfile(SoloAiRules.Difficulty.Low);
		SoloAiRules.Profile medium = SoloAiRules.GetProfile(SoloAiRules.Difficulty.Medium);
		SoloAiRules.Profile high = SoloAiRules.GetProfile(SoloAiRules.Difficulty.High);

		Equal(25, low.BestCaptureChancePercent);
		Equal(25, low.AnyCaptureChancePercent);
		False(low.UsePointScoring);
		False(low.AvoidPairOnlyCaptures);

		Equal(55, medium.BestCaptureChancePercent);
		Equal(20, medium.AnyCaptureChancePercent);
		False(medium.UsePointScoring);
		True(medium.AvoidPairOnlyCaptures);

		Equal(78, high.BestCaptureChancePercent);
		Equal(12, high.AnyCaptureChancePercent);
		True(high.UsePointScoring);
		True(high.AvoidPairOnlyCaptures);
	}

	private static void SoloAiMediumCapturePrefersMoreCardsWhileHighPrefersPoints()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("3", 3),
			TableCard("K", 0),
			TableCard("10", 10),
			TableCard("Q", 0),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: true),
		};

		bool mediumPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.Medium,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int mediumHandIndex);
		bool highPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.High,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int highHandIndex);

		True(mediumPicked);
		True(highPicked);
		Equal(0, mediumHandIndex);
		Equal(1, highHandIndex);
	}

	private static void SoloAiMediumAndHighSkipPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool mediumPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.Medium,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 60,
			randomCaptureOrdinal: 7,
			out int mediumHandIndex);
		bool highPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.High,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 80,
			randomCaptureOrdinal: 7,
			out int highHandIndex);
		bool lowPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiRules.Difficulty.Low,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 0,
			out int lowHandIndex);

		True(mediumPicked);
		True(highPicked);
		True(lowPicked);
		Equal(1, mediumHandIndex);
		Equal(1, highHandIndex);
		Equal(0, lowHandIndex);
	}

	private static void SoloAiTargetPlayerStrategyPicksLargestRelevantOpponent()
	{
		var candidates = new[]
		{
			new SoloAiRules.PlayerTargetCandidate(PlayerIndex: 1, TotalHandCardCount: 5, DeckCount: 2, Score: 14),
			new SoloAiRules.PlayerTargetCandidate(PlayerIndex: 2, TotalHandCardCount: 7, DeckCount: 8, Score: 12),
			new SoloAiRules.PlayerTargetCandidate(PlayerIndex: 3, TotalHandCardCount: 6, DeckCount: 8, Score: 18),
		};

		Equal(2, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.High,
			SoloAiRules.TargetPreferenceKind.PlayerHand,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(3, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.High,
			SoloAiRules.TargetPreferenceKind.PlayerScore,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.High,
			SoloAiRules.TargetPreferenceKind.PlayerDeck,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(1, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.Low,
			SoloAiRules.TargetPreferenceKind.PlayerScore,
			candidates,
			strategyRollPercent: 0,
			tieBreakOrdinal: 0));
		Equal(1, SoloAiRules.PickItemTargetPlayer(
			SoloAiRules.Difficulty.Medium,
			SoloAiRules.TargetPreferenceKind.PlayerScore,
			candidates,
			strategyRollPercent: 95,
			tieBreakOrdinal: 0));
	}

	private static void SoloAiDiscardAndMagnetHeuristicsKeepImmediateCapturesWhenPossible()
	{
		var discardCandidates = new[]
		{
			new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true),
			new SoloAiRules.HandCardDecisionCandidate(Index: 1, ScoreValue: 3, CanCaptureNow: false),
			new SoloAiRules.HandCardDecisionCandidate(Index: 2, ScoreValue: 2, CanCaptureNow: false),
		};

		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiRules.Difficulty.Low,
			discardCandidates,
			totalDisplayCount: 4,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiRules.Difficulty.Medium,
			new[] { new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true) },
			totalDisplayCount: 3,
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiRules.Difficulty.High,
			discardCandidates,
			totalDisplayCount: 4,
			tieBreakOrdinal: 0));

		var magnetCandidates = new[]
		{
			new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true),
			new SoloAiRules.HandCardDecisionCandidate(Index: 1, ScoreValue: 3, CanCaptureNow: false),
			new SoloAiRules.HandCardDecisionCandidate(Index: 2, ScoreValue: 2, CanCaptureNow: false),
		};

		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiRules.Difficulty.Low,
			magnetCandidates,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiRules.Difficulty.Medium,
			new[] { new SoloAiRules.HandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true) },
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickMagnetHandIndex(
			SoloAiRules.Difficulty.High,
			magnetCandidates,
			tieBreakOrdinal: 0));
	}


}
