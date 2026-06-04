using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

internal static class Program
{
	private static int _passed;
	private static int _failed;

	public static int Main()
	{
		Run("capture scoring applies double effect and skips buried cards", CaptureScoringAppliesDoubleEffectAndSkipsBuriedCards);
		Run("capture scoring suppresses slime coated cards", CaptureScoringSuppressesSlimeCoatedCards);
		Run("capture scoring redirects pair destroy owner", CaptureScoringRedirectsPairDestroyOwner);
		Run("card play resolver ignores buried matches", CardPlayResolverIgnoresBuriedMatches);
		Run("card play resolver captures table for collect card", CardPlayResolverCapturesTableForCollectCard);
		Run("capture resolver identifies middle captures", CaptureResolverIdentifiesMiddleCaptures);
		Run("score service returns score breakdown", ScoreServiceReturnsScoreBreakdown);
		Run("reward resolver uses weighted reservoir selection", RewardResolverUsesWeightedReservoirSelection);
		Run("item card effect support blocks reserved empty effects", ItemCardEffectSupportBlocksReservedEmptyEffects);
		Run("potential capture ignores buried matches", PotentialCaptureIgnoresBuriedMatches);
		Run("potential capture prefers royal threshold", PotentialCapturePrefersRoyalThreshold);
		Run("potential capture identifies pair only captures", PotentialCaptureIdentifiesPairOnlyCaptures);
		Run("adventure ai early profile can skip obvious capture", AdventureAiEarlyProfileCanSkipObviousCapture);
		Run("adventure ai early profile skips pair only capture", AdventureAiEarlyProfileSkipsPairOnlyCapture);
		Run("adventure ai point scoring starts after early stages", AdventureAiPointScoringStartsAfterEarlyStages);
		Run("adventure ai random capture can pick non-best capture", AdventureAiRandomCaptureCanPickNonBestCapture);
		Run("adventure ai late best capture skips pair only captures", AdventureAiLateBestCaptureSkipsPairOnlyCaptures);
		Run("adventure ai late random capture skips pair only captures", AdventureAiLateRandomCaptureSkipsPairOnlyCaptures);
		Run("adventure ai final profile can skip best capture", AdventureAiFinalProfileCanSkipBestCapture);
		Run("character audio config resources point to existing files", CharacterAudioConfigResourcesPointToExistingFiles);
		Run("game loop phase precedence is stable", GameLoopPhasePrecedenceIsStable);
		Run("turn state machine advances context generation", TurnStateMachineAdvancesContextGeneration);
		Run("turn state machine gates action starts", TurnStateMachineGatesActionStarts);
		Run("turn rules skip inactive players", TurnRulesSkipInactivePlayers);
		Run("turn rules find single remaining player", TurnRulesFindSingleRemainingPlayer);
		Run("turn progress starts and resets question card tracking", TurnProgressStartsAndResetsQuestionCardTracking);
		Run("turn progress waits for anchor turn before refreshing question card", TurnProgressWaitsForAnchorTurnBeforeRefreshingQuestionCard);
		Run("turn progress refreshes question card after configured cycle", TurnProgressRefreshesQuestionCardAfterConfiguredCycle);
		Run("turn progress stays blocked when table special cards are present", TurnProgressStaysBlockedWhenTableSpecialCardsArePresent);

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

	private static void CardPlayResolverIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			new CardPlayTableCard("A", IsFaceUp: true, IsBuriedInSand: true),
			new CardPlayTableCard("K", IsFaceUp: true, IsBuriedInSand: false),
			new CardPlayTableCard("A", IsFaceUp: false, IsBuriedInSand: false),
		};

		CardPlayPlan plan = CardPlayResolver.Resolve(tableCards, new CardPlayCard("A", CollectsTable: false));

		Equal(2, plan.MatchingTableCardIndex);
		Equal(2, plan.CaptureStartIndex);
		True(plan.MatchedTableCardFlipped);
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

	private static void RewardResolverUsesWeightedReservoirSelection()
	{
		var candidates = new[]
		{
			new RewardCandidate("bomb", 2),
			new RewardCandidate("magnet", 3),
			new RewardCandidate("lens", 5),
		};
		int[] rolls = { 2, 1, 6 };
		int rollIndex = 0;

		RewardPlan reward = RewardResolver.PickWeightedReward(candidates, maxInclusive => rolls[rollIndex++]);

		True(reward.HasReward);
		Equal("magnet", reward.ItemCardId);
	}

	private static void ItemCardEffectSupportBlocksReservedEmptyEffects()
	{
		True(ItemCardEffectSupport.IsSupportedEffectType("grant_reward"));
		False(ItemCardEffectSupport.IsSupportedEffectType("block_match"));
		False(ItemCardEffectSupport.IsSupportedEffectType("reverse_collect"));
		False(ItemCardEffectSupport.IsSupportedEffectType(string.Empty));
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

	private static void PotentialCaptureIdentifiesPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var card = new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false);

		True(PotentialCaptureRules.IsPairOnlyCapture(tableCards, card));
	}

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

	private static void CharacterAudioConfigResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string audioConfigDirectory = Path.Combine(repoRoot, "resources", "character_audio");
		True(Directory.Exists(audioConfigDirectory));

		string[] configPaths = Directory.GetFiles(audioConfigDirectory, "*.tres");
		True(configPaths.Length > 0);

		int audioReferenceCount = 0;
		foreach (string configPath in configPaths)
		{
			string configText = File.ReadAllText(configPath);
			foreach (Match match in Regex.Matches(configText, "path=\"res://([^\"]+\\.(?:mp3|wav))\""))
			{
				audioReferenceCount++;
				string path = Path.Combine(repoRoot, match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
				True(File.Exists(path));
				True(new FileInfo(path).Length > 1024);
			}
		}

		True(audioReferenceCount > 0);
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

	private static void TurnStateMachineAdvancesContextGeneration()
	{
		var stateMachine = new TurnStateMachine();
		TurnContext initialContext = stateMachine.CurrentContext;

		int generation = stateMachine.AdvanceGeneration();
		TurnContext nextContext = stateMachine.CurrentContext;

		Equal(1, generation);
		False(initialContext.IsCurrent(stateMachine.Generation));
		True(nextContext.IsCurrent(stateMachine.Generation));
	}

	private static void TurnStateMachineGatesActionStarts()
	{
		var stateMachine = new TurnStateMachine();
		var awaitingMainPlayer = new GameLoopPhaseState(
			IsLeavingGame: false,
			IsGameOver: false,
			IsOpeningDeal: false,
			HasPendingItemSelection: false,
			IsProcessingCardPlay: false,
			IsResolvingTurnSequence: false,
			IsMainPlayerTurn: true);
		var pendingSelection = awaitingMainPlayer with { HasPendingItemSelection = true };
		var opponentTurn = awaitingMainPlayer with { IsMainPlayerTurn = false };

		True(stateMachine.CanBeginMainPlayerAction(awaitingMainPlayer));
		False(stateMachine.CanBeginMainPlayerAction(pendingSelection));
		False(stateMachine.CanBeginOpponentSequence(awaitingMainPlayer));
		True(stateMachine.CanBeginOpponentSequence(opponentTurn));
		False(stateMachine.CanBeginOpponentSequence(opponentTurn with { IsResolvingTurnSequence = true }));
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

	private static void TurnProgressStartsAndResetsQuestionCardTracking()
	{
		QuestionCardRefreshState tracking = TurnProgressRules.StartQuestionCardRefreshTracking(2);
		QuestionCardRefreshState reset = TurnProgressRules.ResetQuestionCardRefreshTracking();

		Equal(0, tracking.CompletedTurnsSinceQuestionCardRefresh);
		Equal(2, tracking.QuestionCardRefreshAnchorPlayerIndex);
		Equal(0, reset.CompletedTurnsSinceQuestionCardRefresh);
		Equal(-1, reset.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressWaitsForAnchorTurnBeforeRefreshingQuestionCard()
	{
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: false,
			new QuestionCardRefreshState(1, 2),
			nextTurnPlayerIndex: 1,
			playerCount: 4,
			refreshCyclesAfterCapture: 2);

		False(result.ShouldRefreshQuestionCard);
		Equal(2, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(2, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressRefreshesQuestionCardAfterConfiguredCycle()
	{
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: false,
			new QuestionCardRefreshState(3, 2),
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2);

		True(result.ShouldRefreshQuestionCard);
		Equal(0, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(-1, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressStaysBlockedWhenTableSpecialCardsArePresent()
	{
		QuestionCardRefreshState currentState = new(3, 2);
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: true,
			currentState,
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2);

		False(result.ShouldRefreshQuestionCard);
		Equal(currentState.CompletedTurnsSinceQuestionCardRefresh, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(currentState.QuestionCardRefreshAnchorPlayerIndex, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static CaptureScoringRules.Context DefaultScoringContext => new(
		SuppressSlimeCoatedScore: false,
		ApplyScoreReductionPerCard: false,
		ScoreReductionPerCard: 0);

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory != null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "project.godot")))
			{
				return directory.FullName;
			}

			directory = directory.Parent;
		}

		throw new DirectoryNotFoundException("Could not locate project.godot.");
	}

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
