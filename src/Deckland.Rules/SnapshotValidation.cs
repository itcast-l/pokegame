using System.Collections.Immutable;

namespace Deckland.Rules;

internal static class SnapshotValidation
{
	internal static void ValidateOrThrow(MatchStateSnapshot snapshot, string parameterName)
	{
		if (snapshot.SchemaVersion != MatchStateSnapshot.CurrentSchemaVersion
			|| snapshot.Revision < 0
			|| snapshot.LifecycleGeneration <= 0
			|| snapshot.TurnGeneration <= 0
			|| snapshot.PlayerCount is < 2 or > 4
			|| snapshot.WinningScore <= 0
			|| snapshot.CurrentTurnSeat < 0
			|| snapshot.CurrentTurnSeat >= snapshot.PlayerCount
			|| snapshot.WinnerSeat < -1
			|| snapshot.WinnerSeat >= snapshot.PlayerCount
			|| snapshot.Players.IsDefault
			|| snapshot.Players.Length != snapshot.PlayerCount
			|| snapshot.TableCards.IsDefault
			|| snapshot.PendingPresentationBarrierId < 0
			|| snapshot.AppliedActionIds.IsDefault
			|| snapshot.CompletedTurnsSinceQuestionRefresh < 0
			|| snapshot.QuestionRefreshAnchorSeat < -1
			|| snapshot.QuestionRefreshAnchorSeat >= snapshot.PlayerCount)
		{
			throw new ArgumentException("The match snapshot is structurally invalid.", parameterName);
		}

		for (int seat = 0; seat < snapshot.Players.Length; seat++)
		{
			PlayerStateSnapshot player = snapshot.Players[seat];
			if (player.Seat != seat
				|| player.Score < 0
				|| player.Deck.IsDefault
				|| player.Hand.IsDefault
				|| player.ItemHand.IsDefault)
			{
				throw new ArgumentException("The match snapshot contains an invalid player.", parameterName);
			}
		}

		if (snapshot.AppliedActionIds.Any(actionId => actionId <= 0)
			|| snapshot.AppliedActionIds.Distinct().Count() != snapshot.AppliedActionIds.Length)
		{
			throw new ArgumentException("The match snapshot contains invalid action IDs.", parameterName);
		}

		ImmutableArray<long> pendingTableOrder = snapshot.PendingTableOrderRuntimeInstanceIds.IsDefault
			? ImmutableArray<long>.Empty
			: snapshot.PendingTableOrderRuntimeInstanceIds;
		if (pendingTableOrder.Length > 0
			&& (snapshot.PendingPresentationBarrierId <= 0
				|| pendingTableOrder.Length != snapshot.TableCards.Length
				|| pendingTableOrder.Any(id => id <= 0)
				|| pendingTableOrder.Distinct().Count() != pendingTableOrder.Length
				|| !pendingTableOrder.Order().SequenceEqual(
					snapshot.TableCards.Select(card => card.RuntimeInstanceId).Order())))
		{
			throw new ArgumentException("The match snapshot contains an invalid pending table order.", parameterName);
		}

		ValidateMandatorySelection(snapshot, parameterName);
		ValidatePendingAttachmentRewards(snapshot, parameterName);
		ValidateExtraActions(snapshot.NormalizedExtraActions, parameterName);
		if (!MatchFlowStateValidation.IsValid(snapshot.NormalizedMatchFlow)
			|| snapshot.NormalizedMatchFlow.PendingWinnerSeat >= snapshot.PlayerCount
			|| snapshot.NormalizedMatchFlow.BlockedAutomatedItemSeat >= snapshot.PlayerCount)
		{
			throw new ArgumentException(
				"The match snapshot contains invalid flow state.",
				parameterName);
		}
	}

	private static void ValidateExtraActions(
		ExtraActionQueueSnapshot extraActions,
		string parameterName)
	{
		if (extraActions.NextRootChainId <= 0
			|| extraActions.ActiveRootChainId < 0
			|| extraActions.RootEventCount < 0
			|| extraActions.RootMaxDepth < 0
			|| extraActions.ExtraActionsExecutedThisRealTurn < 0
			|| extraActions.QueuedGrants.IsDefault
			|| extraActions.SourcesInRootChain.IsDefault
			|| extraActions.PendingRealTurnEndMarkerIds.IsDefault
			|| extraActions.SourcesInRootChain.Any(string.IsNullOrWhiteSpace)
			|| extraActions.SourcesInRootChain.Distinct(StringComparer.Ordinal).Count()
				!= extraActions.SourcesInRootChain.Length
			|| extraActions.PendingRealTurnEndMarkerIds.Any(string.IsNullOrWhiteSpace)
			|| extraActions.PendingRealTurnEndMarkerIds.Distinct(StringComparer.Ordinal).Count()
				!= extraActions.PendingRealTurnEndMarkerIds.Length)
		{
			throw new ArgumentException(
				"The match snapshot contains invalid extra-action state.",
				parameterName);
		}

		IEnumerable<ExtraActionGrantState> grants = extraActions.QueuedGrants;
		if (extraActions.CurrentGrant.HasValue)
		{
			grants = grants.Prepend(extraActions.CurrentGrant.Value);
		}
		if (grants.Any(grant =>
			string.IsNullOrWhiteSpace(grant.SourceId)
			|| grant.RootChainId <= 0
			|| grant.ChainDepth < 0
			|| !Enum.IsDefined(grant.ReentryPolicy)
			|| grant.RootChainId != extraActions.ActiveRootChainId))
		{
			throw new ArgumentException(
				"The match snapshot contains an invalid extra-action grant.",
				parameterName);
		}

		if (extraActions.ActiveRootChainId == 0
			&& (extraActions.CurrentGrant.HasValue
				|| extraActions.QueuedGrants.Length > 0
				|| extraActions.SourcesInRootChain.Length > 0
				|| extraActions.RootResumesNormalTurn
				|| extraActions.RootDerivationStopped
				|| extraActions.RootEventCount != 0
				|| extraActions.RootMaxDepth != 0))
		{
			throw new ArgumentException(
				"The match snapshot contains an orphaned extra-action root.",
				parameterName);
		}
	}

	private static void ValidatePendingAttachmentRewards(
		MatchStateSnapshot snapshot,
		string parameterName)
	{
		foreach (PendingAttachmentRewardState reward in
			snapshot.NormalizedPendingAttachmentRewards)
		{
			if (reward.Seat < 0
				|| reward.Seat >= snapshot.PlayerCount
				|| string.IsNullOrWhiteSpace(reward.ItemId))
			{
				throw new ArgumentException(
					"The match snapshot contains an invalid pending attachment reward.",
					parameterName);
			}
		}
	}

	private static void ValidateMandatorySelection(
		MatchStateSnapshot snapshot,
		string parameterName)
	{
		MandatorySelectionState selection = snapshot.MandatorySelection
			?? MandatorySelectionState.None;
		ImmutableArray<int> legalIndexes = selection.NormalizedLegalSelectionIndexes;
		if (!Enum.IsDefined(selection.Kind))
		{
			throw new ArgumentException("The match snapshot contains an invalid mandatory selection kind.", parameterName);
		}

		if (selection.Kind == MandatorySelectionKind.None)
		{
			if (selection.Seat != -1
				|| selection.SourceHandIndex != -1
				|| selection.TurnGeneration != -1
				|| !string.IsNullOrEmpty(selection.ItemId)
				|| legalIndexes.Length != 0)
			{
				throw new ArgumentException("The match snapshot contains a non-empty mandatory selection sentinel.", parameterName);
			}

			return;
		}

		if (selection.Seat < 0 || selection.Seat >= snapshot.PlayerCount
			|| selection.TurnGeneration != snapshot.TurnGeneration
			|| selection.SourceHandIndex < -1
			|| legalIndexes.Length == 0
			|| legalIndexes.Distinct().Count() != legalIndexes.Length)
		{
			throw new ArgumentException("The match snapshot contains an invalid mandatory selection header.", parameterName);
		}

		PlayerStateSnapshot player = snapshot.Players[selection.Seat];
		int legalIndexLimit = selection.Kind == MandatorySelectionKind.HandLimitDiscard
			? player.Hand.Length + player.ItemHand.Length
			: player.Hand.Length;
		if (selection.Kind == MandatorySelectionKind.HandLimitDiscard
			&& (!string.IsNullOrEmpty(selection.ItemId) || selection.SourceHandIndex != -1))
		{
			throw new ArgumentException("The hand-limit mandatory selection contains an invalid source.", parameterName);
		}

		if (selection.Kind == MandatorySelectionKind.AttachmentHandCard
			&& (string.IsNullOrWhiteSpace(selection.ItemId)
				|| selection.SourceHandIndex >= player.ItemHand.Length))
		{
			throw new ArgumentException("The attachment mandatory selection contains an invalid item source.", parameterName);
		}

		if (legalIndexes.Any(index => index < 0 || index >= legalIndexLimit))
		{
			throw new ArgumentException("The match snapshot contains an invalid mandatory selection index.", parameterName);
		}

		if (selection.Kind == MandatorySelectionKind.AttachmentHandCard
			&& legalIndexes.Any(index => player.Hand[index].NormalizedAttachedEffectIds.Contains(
				selection.ItemId,
				StringComparer.Ordinal)))
		{
			throw new ArgumentException("The attachment mandatory selection contains an occupied target.", parameterName);
		}

		if (selection.SourceHandIndex >= 0
			&& !string.Equals(
				player.ItemHand[selection.SourceHandIndex].Id,
				selection.ItemId,
				StringComparison.Ordinal))
		{
			throw new ArgumentException("The mandatory selection source item does not match.", parameterName);
		}
	}
}
