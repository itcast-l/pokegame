using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private bool TrySubmitPlayItemFromHandRuleCommand(
		int playerIndex,
		int itemHandIndex,
		string expectedItemId,
		int turnSequenceGeneration,
		RuleItemPlayDestination destination,
		int tableIndex,
		bool tableItemFacesUp,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot play an authoritative item card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlayItemFromHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			itemHandIndex,
			expectedItemId,
			destination,
			tableIndex,
			tableItemFacesUp));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item play rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitDiscardHandCardRuleCommand(
		int playerIndex,
		int displayIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot discard an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new DiscardHandCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			displayIndex,
			expectedCardRuntimeInstanceId,
			expectedItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Hand discard rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitGrantItemToHandRuleCommand(
		int playerIndex,
		string itemId,
		int maximumHandSize,
		int discardDisplayIndex,
		long expectedDiscardCardRuntimeInstanceId,
		string expectedDiscardItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot grant an authoritative item card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new GrantItemToHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			itemId,
			maximumHandSize,
			discardDisplayIndex,
			expectedDiscardCardRuntimeInstanceId,
			expectedDiscardItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item reward rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitReplaceItemInHandRuleCommand(
		int playerIndex,
		int itemHandIndex,
		string expectedItemId,
		string replacementItemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot replace an authoritative item card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new ReplaceItemInHandCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			itemHandIndex,
			expectedItemId,
			replacementItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item replacement rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitAttachItemToHandCardRuleCommand(
		int playerIndex,
		int handIndex,
		string itemId,
		ImmutableArray<int> randomCandidateHandIndexes,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot attach an authoritative item effect.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new AttachItemToHandCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			playerIndex,
			handIndex,
			itemId,
			randomCandidateHandIndexes));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Item attachment rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitChooseRandomHandDisplayIndexRuleCommand(
		int actingPlayerIndex,
		int handPlayerIndex,
		ImmutableArray<int> excludedDisplayIndexes,
		int turnSequenceGeneration,
		out int displayIndex)
	{
		displayIndex = -1;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot choose an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomHandDisplayIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				handPlayerIndex,
				excludedDisplayIndexes));
		if (result.Accepted)
		{
			displayIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random hand selection rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomHandCardIndexRuleCommand(
		int playerIndex,
		ImmutableArray<int> candidateHandIndexes,
		int turnSequenceGeneration,
		out int handIndex)
	{
		handIndex = -1;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot choose an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomHandCardIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				playerIndex,
				candidateHandIndexes));
		if (result.Accepted)
		{
			handIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random attachment target rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomTargetSeatRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<int> candidatePlayerIndexes,
		int turnSequenceGeneration,
		out int targetPlayerIndex)
	{
		targetPlayerIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomTargetSeatCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				candidatePlayerIndexes));
		if (result.Accepted)
		{
			targetPlayerIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random target-seat rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseWeightedOptionRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleWeightedOption> options,
		int turnSequenceGeneration,
		out string selectedId)
	{
		selectedId = string.Empty;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseWeightedOptionCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				options));
		if (result.Accepted)
		{
			selectedId = result.Mutation.LogicalItemId;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Weighted-option rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomTableCardIndexRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> candidates,
		int turnSequenceGeneration,
		out int tableCardIndex)
	{
		tableCardIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomTableCardIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				candidates));
		if (result.Accepted)
		{
			tableCardIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Random table-card rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitChooseRandomIndexRuleCommand(
		int actingPlayerIndex,
		int minimumInclusive,
		int maximumInclusive,
		int turnSequenceGeneration,
		out int selectedIndex)
	{
		selectedIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseRandomIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				minimumInclusive,
				maximumInclusive));
		if (result.Accepted)
		{
			selectedIndex = result.Mutation.SourceIndex;
			return true;
		}

		GD.PushWarning(
			$"Random-index rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitChooseReservoirHandDisplayIndexRuleCommand(
		int actingPlayerIndex,
		int handPlayerIndex,
		ImmutableArray<int> candidateDisplayIndexes,
		int turnSequenceGeneration,
		out int selectedIndex)
	{
		selectedIndex = -1;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(
			new ChooseReservoirHandDisplayIndexCommand(
				NextRuleActionId(),
				current.Revision,
				current.LifecycleGeneration,
				turnSequenceGeneration,
				actingPlayerIndex,
				handPlayerIndex,
				candidateDisplayIndexes));
		if (result.Accepted)
		{
			selectedIndex = result.Mutation.SourceIndex;
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Reservoir hand-index rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitTransferHandCardRuleCommand(
		int actingPlayerIndex,
		int sourcePlayerIndex,
		int sourceDisplayIndex,
		long expectedCardRuntimeInstanceId,
		string expectedItemId,
		int maximumHandSize,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot transfer an authoritative hand card.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new TransferHandCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourcePlayerIndex,
			sourceDisplayIndex,
			maximumHandSize,
			expectedCardRuntimeInstanceId,
			expectedItemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Hand transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitTransferDeckCardsRuleCommand(
		int actingPlayerIndex,
		int sourcePlayerIndex,
		int maximumCount,
		bool preserveCardEffectsWhenEnteringDeck,
		ImmutableArray<long> expectedSourceTopCardRuntimeInstanceIds,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot transfer authoritative deck cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new TransferDeckCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourcePlayerIndex,
			maximumCount,
			preserveCardEffectsWhenEnteringDeck,
			expectedSourceTopCardRuntimeInstanceIds));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Deck transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitTransferTopDeckCardRuleCommand(
		int actingPlayerIndex,
		int sourcePlayerIndex,
		long expectedRuntimeInstanceId,
		bool preserveCardEffectsWhenEnteringDeck,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new TransferTopDeckCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourcePlayerIndex,
			expectedRuntimeInstanceId,
			preserveCardEffectsWhenEnteringDeck));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Top-deck transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitShuffleDeckRuleCommand(
		int actingPlayerIndex,
		int deckPlayerIndex,
		bool cutAfterShuffle,
		int turnSequenceGeneration)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new ShuffleDeckCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			deckPlayerIndex,
			cutAfterShuffle));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Deck-shuffle rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

}
