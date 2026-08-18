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
			new EnemyAiAdventureProfile(45, 25, 55, 55, false),
			new EnemyAiAdventureProfile(55, 25, 65, 55, false),
			new EnemyAiAdventureProfile(65, 20, 70, 55, false),
			new EnemyAiAdventureProfile(70, 20, 75, 35, false),
			new EnemyAiAdventureProfile(75, 15, 80, 35, true),
			new EnemyAiAdventureProfile(78, 15, 85, 35, true),
			new EnemyAiAdventureProfile(85, 10, 90, 20, true),
			new EnemyAiAdventureProfile(90, 5, 92, 20, true),
			new EnemyAiAdventureProfile(92, 5, 94, 20, true),
			new EnemyAiAdventureProfile(95, 5, 96, 10, true),
			new EnemyAiAdventureProfile(97, 3, 98, 10, true),
			new EnemyAiAdventureProfile(65, 10, 90, 10, true),
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
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: true),
		};

		EnemyAiAdventureSelectionResult stageFour = EnemyAiRules.SelectAdventureCard(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(4),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCandidateOrdinal: 0);
		EnemyAiAdventureSelectionResult stageFive = EnemyAiRules.SelectAdventureCard(
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
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
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
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(EnemyAiAdventureSelectionKind.Capture, SelectAdventure(tableCards, handCards, 69, 0).Kind);
		Equal(EnemyAiAdventureSelectionKind.None, SelectAdventure(tableCards, handCards, 70, 0).Kind);
	}

	private static void AdventureAiPairOnlyCaptureIsBlockedByMiddleThreshold()
	{
		var tableCards = new[] { TableCard("A", 1) };
		var handCards = new[]
		{
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(EnemyAiAdventureSelectionKind.Build, SelectAdventure(tableCards, handCards, 54, 0).Kind);
		Equal(EnemyAiAdventureSelectionKind.None, SelectAdventure(tableCards, handCards, 55, 0).Kind);
	}

	private static void AdventureAiBlockedPairsEnterBuildOrdinalPool()
	{
		var tableCards = new[] { TableCard("A", 1) };
		var handCards = new[]
		{
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(1, SelectAdventure(tableCards, handCards, 0, 1).HandIndex);
	}

	private static void AdventureAiRejectedCaptureNeverFallsThroughToBuild()
	{
		var tableCards = new[] { TableCard("A", 1), TableCard("2", 2) };
		var handCards = new[]
		{
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
		};

		Equal(EnemyAiAdventureSelectionKind.None, SelectAdventure(tableCards, handCards, 99, 1).Kind);
	}

	private static void AdventureAiFiltersThresholdBlockedCaptureCandidates()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("3", 3),
		};
		var mixedHand = new[]
		{
			new PotentialCaptureCard(
				"3",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
			new PotentialCaptureCard(
				"A",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
		};

		EnemyAiAdventureSelectionResult mixed = SelectAdventure(tableCards, mixedHand, 0, 0);
		Equal(EnemyAiAdventureSelectionKind.Capture, mixed.Kind);
		Equal(1, mixed.HandIndex);

		var lowTable = new[] { TableCard("A", 1), TableCard("2", 2) };
		var blockedHand = new[]
		{
			new PotentialCaptureCard(
				"A",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
		};
		Equal(EnemyAiAdventureSelectionKind.Build, SelectAdventure(lowTable, blockedHand, 0, 0).Kind);
		Equal(EnemyAiAdventureSelectionKind.None, SelectAdventure(lowTable, blockedHand, 55, 0).Kind);
	}

	private static void AdventureAiBuildsOnlyWithAtMostTwoTableCards()
	{
		var handCards = new[]
		{
			new PotentialCaptureCard("Q", CollectsTable: false, HasDoubleEffect: false),
		};

		for (int tableCount = 0; tableCount <= 4; tableCount++)
		{
			var tableCards = new List<CaptureScoringTableCard>();
			for (int index = 0; index < tableCount; index++)
			{
				tableCards.Add(TableCard((index + 2).ToString(), index + 2));
			}

			EnemyAiAdventureSelectionKind expected = tableCount <= 2
				? EnemyAiAdventureSelectionKind.Build
				: EnemyAiAdventureSelectionKind.None;
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
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: true),
		};
		EnemyAiAdventureProfile pointsProfile = EnemyAiRules.GetAdventureProfile(12);

		EnemyAiAdventureSelectionResult withoutBossThreshold = EnemyAiRules.SelectAdventureCard(
			tableCards, handCards, DefaultScoringContext, pointsProfile, int.MaxValue, 0, 0);
		EnemyAiAdventureSelectionResult withBossThreshold = EnemyAiRules.SelectAdventureCard(
			tableCards, handCards, DefaultScoringContext, pointsProfile, 5, 0, 0);
		var tiedHandCards = new[]
		{
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
		};
		EnemyAiAdventureSelectionResult leftTie = EnemyAiRules.SelectAdventureCard(
			tableCards, tiedHandCards, DefaultScoringContext, pointsProfile, 5, 0, 0);

		Equal(1, withoutBossThreshold.HandIndex);
		Equal(0, withBossThreshold.HandIndex);
		Equal(0, leftTie.HandIndex);
	}

	private static EnemyAiAdventureSelectionResult SelectAdventure(
		IReadOnlyList<CaptureScoringTableCard> tableCards,
		IReadOnlyList<PotentialCaptureCard> handCards,
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

	private static void SoloAiDifficultyProfilesExposeExpectedValues()
	{
		SoloAiProfile low = SoloAiRules.GetProfile(SoloAiDifficulty.Low);
		SoloAiProfile medium = SoloAiRules.GetProfile(SoloAiDifficulty.Medium);
		SoloAiProfile high = SoloAiRules.GetProfile(SoloAiDifficulty.High);

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
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: true),
		};

		bool mediumPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiDifficulty.Medium,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int mediumHandIndex);
		bool highPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiDifficulty.High,
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
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool mediumPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiDifficulty.Medium,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 60,
			randomCaptureOrdinal: 7,
			out int mediumHandIndex);
		bool highPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiDifficulty.High,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 80,
			randomCaptureOrdinal: 7,
			out int highHandIndex);
		bool lowPicked = SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			SoloAiDifficulty.Low,
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 0,
			out int lowHandIndex);

		True(mediumPicked);
		True(highPicked);
		True(lowPicked);
		Equal(1, mediumHandIndex);
		Equal(1, highHandIndex);
		Equal(1, lowHandIndex);
	}

	private static void SoloAiDiscardAndMagnetHeuristicsKeepImmediateCapturesWhenPossible()
	{
		var discardCandidates = new[]
		{
			new SoloAiHandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true),
			new SoloAiHandCardDecisionCandidate(Index: 1, ScoreValue: 3, CanCaptureNow: false),
			new SoloAiHandCardDecisionCandidate(Index: 2, ScoreValue: 2, CanCaptureNow: false),
		};

		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiDifficulty.Low,
			discardCandidates,
			totalDisplayCount: 4,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiDifficulty.Medium,
			new[] { new SoloAiHandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true) },
			totalDisplayCount: 3,
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiDifficulty.High,
			discardCandidates,
			totalDisplayCount: 4,
			tieBreakOrdinal: 0));

		var magnetCandidates = new[]
		{
			new SoloAiHandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true),
			new SoloAiHandCardDecisionCandidate(Index: 1, ScoreValue: 3, CanCaptureNow: false),
			new SoloAiHandCardDecisionCandidate(Index: 2, ScoreValue: 2, CanCaptureNow: false),
		};

		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiDifficulty.Low,
			magnetCandidates,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiDifficulty.Medium,
			new[] { new SoloAiHandCardDecisionCandidate(Index: 0, ScoreValue: 1, CanCaptureNow: true) },
			tieBreakOrdinal: 0));
		Equal(2, SoloAiRules.PickMagnetHandIndex(
			SoloAiDifficulty.High,
			magnetCandidates,
			tieBreakOrdinal: 0));
	}

	private static void SoloAiThresholdFilteringCoversAllDifficultiesAndProtectionHeuristics()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
			TableCard("3", 3),
		};
		var mixedHand = new[]
		{
			new PotentialCaptureCard(
				"K",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
			new PotentialCaptureCard(
				"A",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
		};
		foreach (SoloAiDifficulty difficulty in Enum.GetValues<SoloAiDifficulty>())
		{
			SoloAiProfile profile = SoloAiRules.GetProfile(difficulty);
			True(SoloAiRules.TryPickCaptureCardIndex(
				tableCards,
				mixedHand,
				DefaultScoringContext,
				difficulty,
				royalCaptureThreshold: int.MaxValue,
				decisionRollPercent: 0,
				randomCaptureOrdinal: 0,
				out int bestHandIndex));
			Equal(1, bestHandIndex);
			True(SoloAiRules.TryPickCaptureCardIndex(
				tableCards,
				mixedHand,
				DefaultScoringContext,
				difficulty,
				royalCaptureThreshold: int.MaxValue,
				decisionRollPercent: profile.BestCaptureChancePercent,
				randomCaptureOrdinal: 0,
				out int anyHandIndex));
			Equal(1, anyHandIndex);
		}

		bool blockedCanCapture = PotentialCaptureRules.GetPotentialNormalCaptureCount(
			tableCards,
			mixedHand[0]) > 0;
		False(blockedCanCapture);
		var protectionCandidates = new[]
		{
			new SoloAiHandCardDecisionCandidate(0, ScoreValue: 1, blockedCanCapture),
			new SoloAiHandCardDecisionCandidate(1, ScoreValue: 10, CanCaptureNow: true),
		};
		Equal(0, SoloAiRules.PickDiscardDisplayIndex(
			SoloAiDifficulty.High,
			protectionCandidates,
			totalDisplayCount: 2,
			tieBreakOrdinal: 0));
		Equal(0, SoloAiRules.PickMagnetHandIndex(
			SoloAiDifficulty.High,
			protectionCandidates,
			tieBreakOrdinal: 0));
	}

	private static void NetworkAiProxyFiltersThresholdBlockedCaptureCandidates()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("K", 12),
			TableCard("3", 3),
		};
		var handCards = new[]
		{
			new PotentialCaptureCard(
				"K",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
			new PotentialCaptureCard(
				"A",
				CollectsTable: false,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1),
		};

		True(NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
			tableCards,
			handCards,
			DefaultScoringContext,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int handIndex));
		Equal(1, handIndex);
		False(NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
			tableCards,
			new[] { handCards[0] },
			DefaultScoringContext,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out _));
	}


}
