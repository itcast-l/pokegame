using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

internal static partial class Program
{
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

	private static void TurnRulesSkipOutOfRangeSeats()
	{
		int[] turnOrder = { 0, 2, 1, 3 };

		Equal(1, GameTurnRules.GetNextPlayerIndex(
			turnOrder,
			currentPlayerIndex: 2,
			playerCount: 2,
			fallbackPlayerIndex: 0));
	}

	private static void TurnRulesSkipPlayersWithoutCards()
	{
		int[] turnOrder = { 0, 2, 1, 3 };
		var threePlayerStates = new[]
		{
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
		};
		var fourPlayerStates = new[]
		{
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
		};

		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			threePlayerStates,
			currentPlayerIndex: 0,
			playerCount: 3,
			out int nextPlayerIndex));
		Equal(1, nextPlayerIndex);
		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			threePlayerStates,
			currentPlayerIndex: 1,
			playerCount: 3,
			out nextPlayerIndex));
		Equal(0, nextPlayerIndex);
		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			fourPlayerStates,
			currentPlayerIndex: 2,
			playerCount: 4,
			out nextPlayerIndex));
		Equal(0, nextPlayerIndex);
	}

	private static void TurnRulesSeparateResourcesFromImmediateActions()
	{
		int[] turnOrder = { 0, 2, 1, 3 };
		var playerStates = new[]
		{
			new GameTurnPlayerCardState(1, 0, 0, false),
			new GameTurnPlayerCardState(0, 1, 0, false),
			new GameTurnPlayerCardState(0, 0, 1, true),
		};

		True(playerStates[1].HasResources);
		False(playerStates[1].CanActNow);
		True(playerStates[2].HasResources);
		True(playerStates[2].CanActNow);
		True(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex: 0,
			playerCount: 3,
			out int nextPlayerIndex));
		Equal(2, nextPlayerIndex);
	}

	private static void TurnRulesReportNoEligibleNextPlayer()
	{
		int[] turnOrder = { 0, 2, 1, 3 };
		var playerStates = new[]
		{
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 1, false),
			new GameTurnPlayerCardState(0, 1, 0, false),
		};

		False(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex: 0,
			playerCount: 3,
			out int nextPlayerIndex));
		Equal(-1, nextPlayerIndex);
		False(GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex: 7,
			playerCount: 3,
			out nextPlayerIndex));
	}

	private static void MatchOutcomeRulesCoverResourcesBlocksAndDraws()
	{
		var zeroResources = new[]
		{
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 0, false),
		};
		MatchOutcome tiedEmpty = MatchOutcomeRules.Evaluate(zeroResources, new[] { 12, 12 }, 2);
		Equal(MatchOutcomeKind.Draw, tiedEmpty.Kind);
		Equal(-1, tiedEmpty.WinnerPlayerIndex);

		var singleResourceOwner = new[]
		{
			new GameTurnPlayerCardState(0, 0, 0, false),
			new GameTurnPlayerCardState(0, 0, 1, false),
		};
		MatchOutcome resourceWinner = MatchOutcomeRules.Evaluate(singleResourceOwner, new[] { 99, 1 }, 2);
		Equal(MatchOutcomeKind.Winner, resourceWinner.Kind);
		Equal(1, resourceWinner.WinnerPlayerIndex);

		var blockedWithTemporaryItem = new[]
		{
			new GameTurnPlayerCardState(0, 0, 1, false),
			new GameTurnPlayerCardState(0, 1, 0, false),
		};
		MatchOutcome blockedWinner = MatchOutcomeRules.Evaluate(blockedWithTemporaryItem, new[] { 5, 8 }, 2);
		Equal(MatchOutcomeKind.Winner, blockedWinner.Kind);
		Equal(1, blockedWinner.WinnerPlayerIndex);

		MatchOutcome scorePriority = MatchOutcomeRules.Evaluate(singleResourceOwner, new[] { 1, 99 }, 2, 0);
		Equal(0, scorePriority.WinnerPlayerIndex);
		True(MatchOutcomeRules.IsSerializedOutcomeValid(MatchOutcomeKind.Draw, -1, 2));
		False(MatchOutcomeRules.IsSerializedOutcomeValid(MatchOutcomeKind.Draw, 0, 2));
		True(MatchOutcomeRules.IsSerializedOutcomeValid(MatchOutcomeKind.Winner, 1, 2));

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
			hasQuestionCard: false,
			new QuestionCardRefreshState(1, 2),
			nextTurnPlayerIndex: 1,
			playerCount: 4,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 0);

		False(result.ShouldRefreshQuestionCard);
		Equal(2, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(2, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressRefreshesQuestionCardAfterConfiguredCycle()
	{
		QuestionCardRefreshAdvanceResult result = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: false,
			hasQuestionCard: false,
			new QuestionCardRefreshState(3, 2),
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 0);

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
			hasQuestionCard: false,
			currentState,
			nextTurnPlayerIndex: 2,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 0);

		False(result.ShouldRefreshQuestionCard);
		Equal(currentState.CompletedTurnsSinceQuestionCardRefresh, result.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(currentState.QuestionCardRefreshAnchorPlayerIndex, result.State.QuestionCardRefreshAnchorPlayerIndex);
	}

	private static void TurnProgressExpiresUncollectedQuestionCardBeforeRestartingRefresh()
	{
		QuestionCardRefreshAdvanceResult firstTurn = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: true,
			hasQuestionCard: true,
			new QuestionCardRefreshState(0, 0),
			nextTurnPlayerIndex: 1,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 2);

		False(firstTurn.ShouldExpireQuestionCard);
		Equal(1, firstTurn.State.CompletedTurnsSinceQuestionCardRefresh);

		QuestionCardRefreshAdvanceResult expired = TurnProgressRules.AdvanceQuestionCardRefresh(
			itemMode: true,
			hasTableSpecialCard: true,
			hasQuestionCard: true,
			firstTurn.State,
			nextTurnPlayerIndex: 0,
			playerCount: 2,
			refreshCyclesAfterCapture: 2,
			questionCardLifetimeTurns: 2);

		True(expired.ShouldExpireQuestionCard);
		False(expired.ShouldRefreshQuestionCard);
		Equal(0, expired.State.CompletedTurnsSinceQuestionCardRefresh);
		Equal(0, expired.State.QuestionCardRefreshAnchorPlayerIndex);

		QuestionCardRefreshAdvanceResult refresh = expired;
		for (int completedTurn = 1; completedTurn <= 4; completedTurn++)
		{
			refresh = TurnProgressRules.AdvanceQuestionCardRefresh(
				itemMode: true,
				hasTableSpecialCard: false,
				hasQuestionCard: false,
				refresh.State,
				nextTurnPlayerIndex: completedTurn % 2,
				playerCount: 2,
				refreshCyclesAfterCapture: 2,
				questionCardLifetimeTurns: 0);
			Equal(completedTurn == 4, refresh.ShouldRefreshQuestionCard);
		}
	}

}
