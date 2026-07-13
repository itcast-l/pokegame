using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void AdventureAiEarlyProfileCanSkipObviousCapture()
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

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 90,
			randomCaptureOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AdventureAiEarlyProfileSkipsPairOnlyCapture()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AdventureAiPointScoringStartsAfterEarlyStages()
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

		bool earlyPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(6),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int earlyHandIndex);
		bool laterPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(7),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out int laterHandIndex);

		True(earlyPicked);
		True(laterPicked);
		Equal(0, earlyHandIndex);
		Equal(1, laterHandIndex);
	}

	private static void AdventureAiRandomCaptureCanPickNonBestCapture()
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

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(1),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 30,
			randomCaptureOrdinal: 1,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void AdventureAiLateBestCaptureSkipsPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var handCards = new[]
		{
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(10),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 0,
			randomCaptureOrdinal: 0,
			out _);

		False(picked);
	}

	private static void AdventureAiLateRandomCaptureSkipsPairOnlyCaptures()
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

		bool picked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(10),
			royalCaptureThreshold: int.MaxValue,
			decisionRollPercent: 92,
			randomCaptureOrdinal: 7,
			out int handIndex);

		True(picked);
		Equal(1, handIndex);
	}

	private static void AdventureAiFinalProfileCanSkipBestCapture()
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

		bool lowRollPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(14),
			royalCaptureThreshold: 5,
			decisionRollPercent: 64,
			randomCaptureOrdinal: 0,
			out int lowRollHandIndex);
		bool highRollPicked = EnemyAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			EnemyAiRules.GetAdventureProfile(14),
			royalCaptureThreshold: 5,
			decisionRollPercent: 99,
			randomCaptureOrdinal: 0,
			out _);

		True(lowRollPicked);
		Equal(0, lowRollHandIndex);
		False(highRollPicked);
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

