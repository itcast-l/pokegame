using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
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

	private static void VisualUpdateThrottleCaps30HzFrom60HzInput()
	{
		var throttle = new VisualUpdateThrottle();
		int updates = 0;
		for (int frame = 0; frame < 60; frame++)
		{
			if (throttle.Advance(1.0 / 60.0, 30))
			{
				updates++;
			}
		}

		Equal(30, updates);
	}

	private static void VisualUpdateThrottleCaps30HzFrom120HzInput()
	{
		var throttle = new VisualUpdateThrottle();
		int updates = 0;
		for (int frame = 0; frame < 120; frame++)
		{
			if (throttle.Advance(1.0 / 120.0, 30))
			{
				updates++;
			}
		}

		Equal(30, updates);
	}

	private static void VisualUpdateThrottleDoesNotCatchUpMultipleFrames()
	{
		var throttle = new VisualUpdateThrottle();
		int updates = 0;

		if (throttle.Advance(2.0, 30))
		{
			updates++;
		}

		Equal(1, updates);
	}

	private static void VisualUpdateThrottleHandlesForceResetDeltaAndFps()
	{
		var throttle = new VisualUpdateThrottle();

		False(throttle.Advance(-1.0, 30));
		False(throttle.Advance(0.5, 0));
		True(throttle.Advance(0.5, -10));

		throttle.ForceNextUpdate();
		True(throttle.Advance(0.0, 30));
		False(throttle.Advance(0.0, 30));

		throttle.ForceNextUpdate();
		throttle.Reset();
		False(throttle.Advance(0.0, 30));
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
		var context = new CaptureScoringContext(
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

	private static void CardPlayResolverIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: true),
			new CardPlayTableCard("K", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("A", IsFaceUp: false, IsBuriedInSand: false),
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(tableCards, new CardPlayCard("A", CollectsTable: false));

		Equal(2, plan.MatchingTableCardIndex);
		Equal(2, plan.CaptureStartIndex);
		True(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverUsesLeftmostRankMatch()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("8", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("4", IsFaceUp: false, IsBuriedInSand: false),
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("6", IsFaceUp: false, IsBuriedInSand: false),
			new CardPlayTableCard("7", IsFaceUp: false, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("4", CollectsTable: false),
			alternateRanks: null,
			insertAtTableStart: false,
			basePlayCaptureThreshold: 3);

		Equal(0, plan.MatchingTableCardIndex);
		Equal(0, plan.CaptureStartIndex);
		Equal(5, plan.MiddleTableCaptureCount);
		False(plan.CaptureThresholdBlocked);
		False(plan.MatchedTableCardFlipped);
	}

	private static void PotentialCaptureUsesLeftmostRankMatch()
	{
		var tableCards = new[]
		{
			TableCard("4", 4),
			TableCard("8", 8),
			TableCard("4", 4),
			TableCard("5", 5),
			TableCard("6", 6),
			TableCard("7", 7),
		};
		var card = new PotentialCaptureCard(
			"4",
			CollectsTable: false,
			HasDoubleEffect: false,
			BasePlayCaptureThreshold: 3);

		Equal(7, PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card));
	}

	private static void PotentialCaptureEvaluationMatchesCardPlayResolver()
	{
		var tableCards = new[]
		{
			TableCard("4", 4, suit: "clubs"),
			TableCard("9", 9, isBuriedInSand: true, suit: "hearts"),
			TableCard("2", 2, suit: "diamonds", countsTowardBasePlayCaptureThreshold: false),
			TableCard("4", 4, suit: "spades"),
			TableCard("7", 7, suit: "hearts"),
		};
		var potentialCard = new PotentialCaptureCard(
			"4",
			CollectsTable: false,
			HasDoubleEffect: false,
			BasePlayCaptureThreshold: 1);

		PotentialCaptureEvaluation evaluation =
			PotentialCaptureRules.EvaluatePotentialNormalCapture(tableCards, potentialCard);
		CardPlayPlan directPlan = CardPlayResolver.Resolve(
			new[]
			{
				new CardPlayTableCard("4", true, false, "clubs"),
				new CardPlayTableCard("9", true, true, "hearts"),
				new CardPlayTableCard("2", true, false, "diamonds", false),
				new CardPlayTableCard("4", true, false, "spades"),
				new CardPlayTableCard("7", true, false, "hearts"),
			},
			new CardPlayCard("4", CollectsTable: false),
			alternateRanks: null,
			insertAtTableStart: false,
			basePlayCaptureThreshold: 1);

		Equal(directPlan.MatchingTableCardIndex, evaluation.PlayPlan.MatchingTableCardIndex);
		Equal(directPlan.CaptureStartIndex, evaluation.PlayPlan.CaptureStartIndex);
		Equal(directPlan.MiddleTableCaptureCount, evaluation.PlayPlan.MiddleTableCaptureCount);
		Equal(0, evaluation.PlayPlan.MatchingTableCardIndex);
		Equal(2, evaluation.PlayPlan.MiddleTableCaptureCount);
		Equal(5, evaluation.NormalCaptureCount);
		True(evaluation.HasCapturedMiddleTableCards);
		False(evaluation.IsPairOnlyCapture);
		Equal(13, PotentialCaptureRules.GetPotentialNormalCapturePoints(
			tableCards,
			potentialCard,
			DefaultScoringContext));

		PotentialCaptureEvaluation blocked = PotentialCaptureRules.EvaluatePotentialNormalCapture(
			tableCards,
			potentialCard with { BasePlayCaptureThreshold = 2 });
		True(blocked.PlayPlan.CaptureThresholdBlocked);
		Equal(2, blocked.PlayPlan.MiddleTableCaptureCount);
		Equal(0, blocked.NormalCaptureCount);

		PotentialCaptureEvaluation collectAll = PotentialCaptureRules.EvaluatePotentialNormalCapture(
			tableCards,
			new PotentialCaptureCard(
				"J",
				CollectsTable: true,
				HasDoubleEffect: false,
				BasePlayCaptureThreshold: 1));
		True(collectAll.PlayPlan.PlayedCardCollectsTable);
		Equal(0, collectAll.PlayPlan.CaptureStartIndex);
		Equal(5, collectAll.NormalCaptureCount);

		var alternateTable = new[]
		{
			TableCard("A", 1, suit: "clubs"),
			TableCard("2", 2, suit: "spades"),
			TableCard("3", 3, suit: "diamonds"),
		};
		PotentialCaptureEvaluation alternate = PotentialCaptureRules.EvaluatePotentialNormalCapture(
			alternateTable,
			new PotentialCaptureCard(
				"9",
				CollectsTable: false,
				HasDoubleEffect: false,
				AlternateRanks: new[] { "A" },
				BasePlayCaptureThreshold: 1));
		Equal(0, alternate.PlayPlan.MatchingTableCardIndex);
		Equal("A", alternate.PlayPlan.EffectiveRank);
		Equal(4, alternate.NormalCaptureCount);

		var suitTable = new[]
		{
			TableCard("2", 2, suit: "hearts"),
			TableCard("5", 5, suit: "clubs"),
			TableCard("8", 8, suit: "spades"),
		};
		var suitFallbackCard = new PotentialCaptureCard(
			"5",
			CollectsTable: false,
			HasDoubleEffect: false,
			Suit: "hearts",
			CollectBySuit: true,
			SuitMatchIsFallbackOnly: true);
		PotentialCaptureEvaluation rankFirst =
			PotentialCaptureRules.EvaluatePotentialNormalCapture(suitTable, suitFallbackCard);
		PotentialCaptureEvaluation suitFirst = PotentialCaptureRules.EvaluatePotentialNormalCapture(
			suitTable,
			suitFallbackCard with { SuitMatchIsFallbackOnly = false });
		Equal(1, rankFirst.PlayPlan.MatchingTableCardIndex);
		Equal(0, suitFirst.PlayPlan.MatchingTableCardIndex);
	}

	private static void CardPlayResolverCapturesTableForCollectCard()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("3", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(tableCards, new CardPlayCard("Q", CollectsTable: true));

		Equal(-1, plan.MatchingTableCardIndex);
		True(plan.PlayedCardCollectsTable);
		Equal(0, plan.CaptureStartIndex);
	}

	private static void JackHandVisualScaleIsNormalized()
	{
		Equal(1.0f, CardVisualTuningRules.GetPlayingCardHandVisualScale("A"));
		Equal(1.08f, CardVisualTuningRules.GetPlayingCardHandVisualScale("J"));
		Equal(1.08f, CardVisualTuningRules.GetItemCardHandVisualScale("J"));
	}

	private static void CardPlayResolverUsesAlternateRankForBorrowTideMatch()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("6", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("7", IsFaceUp: false, IsBuriedInSand: false),
			new CardPlayTableCard("10", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("8", CollectsTable: false),
			new[] { "7", "9" },
			insertAtTableStart: false);

		Equal(1, plan.MatchingTableCardIndex);
		Equal(1, plan.CaptureStartIndex);
		Equal("7", plan.EffectiveRank);
		True(plan.MatchedTableCardFlipped);
		Equal(3, plan.TableInsertIndex);
	}

	private static void BorrowTideOnlyGrantsAlternateRanksToTheEnchantedCard()
	{
		IReadOnlyList<string> noEffectRanks = BorrowTideRules.GetAlternateRanks(
			cardHasBorrowTideEffect: false,
			playedRank: "8");
		Equal(0, noEffectRanks.Count);

		IReadOnlyList<string> enchantedRanks = BorrowTideRules.GetAlternateRanks(
			cardHasBorrowTideEffect: true,
			playedRank: "8");
		Equal(2, enchantedRanks.Count);
		Equal("7", enchantedRanks[0]);
		Equal("9", enchantedRanks[1]);

		IReadOnlyList<string> nonNumericRanks = BorrowTideRules.GetAlternateRanks(
			cardHasBorrowTideEffect: true,
			playedRank: "Q");
		Equal(0, nonNumericRanks.Count);
	}

	private static void BorrowTideOnlyCountsAsUsedWhenAlternateRankMatched()
	{
		False(BorrowTideRules.DidUseBorrowTide(
			cardHasBorrowTideEffect: false,
			playedRank: "8",
			effectiveRank: "7"));
		False(BorrowTideRules.DidUseBorrowTide(
			cardHasBorrowTideEffect: true,
			playedRank: "8",
			effectiveRank: "8"));
		True(BorrowTideRules.DidUseBorrowTide(
			cardHasBorrowTideEffect: true,
			playedRank: "8",
			effectiveRank: "7"));
	}

	private static void CardPlayResolverInsertsMissAtTableStartForReverseFlowAnchor()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("4", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("9", CollectsTable: false),
			alternateRanks: null,
			insertAtTableStart: true);

		Equal(-1, plan.MatchingTableCardIndex);
		Equal(-1, plan.CaptureStartIndex);
		Equal("9", plan.EffectiveRank);
		Equal(0, plan.TableInsertIndex);
		False(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverUsesFirstMatchingSuitForSuitCollectAttachment()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
			new CardPlayTableCard("7", IsFaceUp: false, IsBuriedInSand: false, Suit: "hearts"),
			new CardPlayTableCard("9", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: false, Suit: "hearts"),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false, Suit: "hearts", CollectBySuit: true));

		Equal(1, plan.MatchingTableCardIndex);
		Equal(1, plan.CaptureStartIndex);
		Equal("5", plan.EffectiveRank);
		True(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverFallsBackToRankWhenSuitCollectHasNoSuitMatch()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
			new CardPlayTableCard("8", IsFaceUp: true, IsBuriedInSand: false, Suit: "diamonds"),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false, Suit: "hearts", CollectBySuit: true));

		Equal(0, plan.MatchingTableCardIndex);
		Equal(0, plan.CaptureStartIndex);
		Equal("5", plan.EffectiveRank);
		False(plan.MatchedTableCardFlipped);
	}

	private static void CardPlayResolverPrefersSuitStartOverLaterRankMatchForSuitCollect()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false, Suit: "hearts"),
			new CardPlayTableCard("5", IsFaceUp: true, IsBuriedInSand: false, Suit: "clubs"),
			new CardPlayTableCard("8", IsFaceUp: true, IsBuriedInSand: false, Suit: "spades"),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false, Suit: "hearts", CollectBySuit: true));

		Equal(0, plan.MatchingTableCardIndex);
		Equal(0, plan.CaptureStartIndex);
		Equal("5", plan.EffectiveRank);
	}

	private static void CardPlayResolverSupportsPendingInsertOverrideAndFaceUp()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("2", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("7", IsFaceUp: true, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(
			tableCards,
			new CardPlayCard("5", CollectsTable: false),
			alternateRanks: null,
			insertAtTableStart: false,
			tableInsertIndexOverride: 1,
			playedCardFacesUp: true);

		Equal(1, plan.TableInsertIndex);
		True(plan.PlayedCardFacesUp);
	}

	private static void CaptureResolverIdentifiesMiddleCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2, isBuriedInSand: true),
			TableCard("3", 3),
			TableCard("4", 4),
		};

		CapturePlan plan = CaptureResolver.Resolve(tableCards, 0);

		True(plan.Captured);
		True(plan.CapturedMiddleCards);
	}

	private static void ScoreServiceReturnsScoreBreakdown()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 5),
			TableCard("3", 7, hasDoubleCollectItemEffect: true),
			TableCard("4", 9),
		};

		ScoreBreakdown score = ScoreService.GetCapturedScore(
			tableCards,
			captureStartIndex: 0,
			scorePlayerIndex: 1,
			DefaultScoringContext);

		Equal(1, score.ScorePlayerIndex);
		True(score.HasDoubleEffect);
		Equal(24, score.Points);
	}

	private static void CaptureFlowPlannerBuildsCaptureSummary()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 2),
			TableCard("3", 3, hasDoubleCollectItemEffect: true),
			TableCard("4", 4),
		};
		var extras = new HashSet<int> { 1 };

		CaptureFlowPlan plan = CaptureFlowPlanner.Build(
			tableCards,
			captureStartIndex: 0,
			scorePlayerIndex: 2,
			DefaultScoringContext,
			extras);

		True(plan.Capture.Captured);
		True(plan.Capture.CapturedMiddleCards);
		Equal(2, plan.Score.ScorePlayerIndex);
		True(plan.Score.HasDoubleEffect);
		True(plan.Score.Points > 0);
		True(plan.CapturedScoringCards);
		True(plan.ShouldTrackQuestionCardRefresh);
		Equal(4, plan.CaptureCount);
		Equal(1, plan.ExtraCaptureCount);
		Equal(5, plan.TotalCapturedCardCount);
	}


}
