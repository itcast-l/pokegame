using System.Collections.Generic;
using System.Collections.Immutable;
using System;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private bool TrySubmitPlaceTableItemRuleCommand(
		int actingPlayerIndex,
		string itemId,
		int tableIndex,
		bool isFaceUp,
		int itemOwnerPlayerIndex,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot place an authoritative table item.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlaceTableItemCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			itemId,
			tableIndex,
			isFaceUp,
			itemOwnerPlayerIndex));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table item placement rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitPlaceTableCardRuleCommand(
		int actingPlayerIndex,
		PlayingCard card,
		int tableIndex,
		bool isFaceUp,
		int cardOwnerPlayerIndex,
		bool removeOnCapture,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlaceTableCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			ToRuleCardState(card),
			tableIndex,
			isFaceUp,
			cardOwnerPlayerIndex,
			removeOnCapture));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card placement rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitChooseTableCardsRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> candidates,
		int maximumCount,
		int turnSequenceGeneration,
		out ImmutableArray<int> selectedIndexes)
	{
		selectedIndexes = ImmutableArray<int>.Empty;
		if (_authorityRuleSession == null)
		{
			GD.PushError("A replica rule session cannot choose authoritative table cards.");
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		RuleCommandResult result = _authorityRuleSession.Submit(new ChooseTableCardsCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			candidates,
			maximumCount));
		if (result.Accepted)
		{
			var indexes = ImmutableArray.CreateBuilder<int>(result.PresentationIntents.Length);
			foreach (PresentationIntent intent in result.PresentationIntents)
			{
				if (intent.Kind == PresentationIntentKind.TableCardsSelected)
				{
					indexes.Add(intent.TargetIndex);
				}
			}
			selectedIndexes = indexes.ToImmutable();
			return selectedIndexes.Length == result.Mutation.CollectedCardCount;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Table-card selection rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

	private bool TrySubmitSetTableCardsFaceUpRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		bool expectedFaceUp,
		bool faceUp,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new SetTableCardsFaceUpCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			expectedFaceUp,
			faceUp));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card face-state rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitSetTableCardsHiddenRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		bool expectedHidden,
		bool hidden,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new SetTableCardsHiddenCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			expectedHidden,
			hidden));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card visibility rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitSetTableCardsScoreSuppressedRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		bool expectedScoreSuppressed,
		bool scoreSuppressed,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new SetTableCardsScoreSuppressedCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			expectedScoreSuppressed,
			scoreSuppressed));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card score-suppression rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitRemoveTableCardAttachmentRuleCommand(
		int actingPlayerIndex,
		int tableCardIndex,
		long expectedTableCardRuntimeInstanceId,
		string itemId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new RemoveTableCardAttachmentCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCardIndex,
			expectedTableCardRuntimeInstanceId,
			itemId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card attachment removal rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitRemoveTableCardRangeRuleCommand(
		int actingPlayerIndex,
		int startIndex,
		ImmutableArray<long> expectedRuntimeInstanceIds,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new RemoveTableCardRangeCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			startIndex,
			expectedRuntimeInstanceIds));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card removal rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitMoveTableCardRuleCommand(
		int actingPlayerIndex,
		int sourceIndex,
		int targetIndex,
		long expectedRuntimeInstanceId,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new MoveTableCardCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			sourceIndex,
			targetIndex,
			expectedRuntimeInstanceId));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card move rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitMoveTableCardToDeckRuleCommand(
		int actingPlayerIndex,
		int tableCardIndex,
		long expectedTableCardRuntimeInstanceId,
		int deckPlayerIndex,
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
		result = _authorityRuleSession.Submit(new MoveTableCardToDeckCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCardIndex,
			expectedTableCardRuntimeInstanceId,
			deckPlayerIndex,
			preserveCardEffectsWhenEnteringDeck));
		if (result.Accepted)
		{
			return true;
		}

		GD.PushWarning(
			$"Table-card deck transfer rule command was rejected: "
			+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		return false;
	}

	private bool TrySubmitPlanTableShuffleRuleCommand(
		int actingPlayerIndex,
		ImmutableArray<RuleTableCardReference> tableCards,
		ImmutableArray<int> fixedTableIndexes,
		int turnSequenceGeneration,
		out RuleCommandResult result)
	{
		result = default!;
		if (_authorityRuleSession == null)
		{
			return false;
		}

		MatchReadModel current = _authorityRuleSession.ReadModel;
		result = _authorityRuleSession.Submit(new PlanTableShuffleCommand(
			NextRuleActionId(),
			current.Revision,
			current.LifecycleGeneration,
			turnSequenceGeneration,
			actingPlayerIndex,
			tableCards,
			fixedTableIndexes));
		if (result.Accepted)
		{
			return true;
		}

		if (result.Rejection.Code != RuleRejectionCode.NoLegalAction)
		{
			GD.PushWarning(
				$"Table-shuffle planning rule command was rejected: "
				+ $"{result.Rejection.Code} {result.Rejection.Detail}");
		}
		return false;
	}

}
