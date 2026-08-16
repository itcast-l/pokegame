using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private bool TrySubmitPlayCardFromHandRuleCommand(
		int playerIndex,
		int handIndex,
		int turnSequenceGeneration,
		CardPlayRuleOptions options,
		int tableInsertIndexOverride,
		bool playedCardFacesUp,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot play an authoritative card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlayCardFromHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			handIndex,
			options.PrimaryMatchRank,
			options.AlternateMatchRanks,
			options.CollectEntireTable,
			options.CollectBySuit,
			options.SuitMatchIsFallbackOnly,
			options.InsertAtTableStart,
			tableInsertIndexOverride,
			playedCardFacesUp));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Card play rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitCaptureCardsRuleCommand(
		int playerIndex,
		int captureStartIndex,
		int turnSequenceGeneration,
		IReadOnlyCollection<int>? extraTableCardIndexes,
		bool preserveCardEffectsWhenEnteringDeck,
		IReadOnlyCollection<int>? preservedTableCardIndexes,
		int redirectedTableCardIndex,
		int redirectedPlayerIndex,
		ImmutableArray<RuleScoreMutation> scoreMutations,
		bool restartQuestionRefreshTracking,
		long basePlayCardRuntimeInstanceId,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot capture authoritative cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new CaptureCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			captureStartIndex,
			ToSortedRuleIndexes(extraTableCardIndexes),
			preserveCardEffectsWhenEnteringDeck,
			ToSortedRuleIndexes(preservedTableCardIndexes),
			redirectedTableCardIndex,
			redirectedTableCardIndex >= 0 ? redirectedPlayerIndex : -1,
			scoreMutations,
			restartQuestionRefreshTracking,
			basePlayCardRuntimeInstanceId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Capture rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitScoreMutationsRuleCommand(
		ImmutableArray<RuleScoreMutation> mutations,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot change authoritative scores.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ApplyScoreMutationsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			mutations));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Score rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitDrawCardsRuleCommand(
		int playerIndex,
		int drawCount,
		int maximumHandSize,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot draw authoritative cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new DrawCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			playerIndex,
			drawCount,
			maximumHandSize));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Draw rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitReturnCardsToHandRuleCommand(
		int playerIndex,
		ImmutableArray<long> cardRuntimeInstanceIds,
		int maximumHandSize,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot return authoritative cards to a hand.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ReturnCardsToHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			current.TurnGeneration,
			playerIndex,
			cardRuntimeInstanceIds,
			maximumHandSize));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Return-to-hand rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

}
