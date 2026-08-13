using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial class AuthorityRuleSession
{
	public MatchStateSnapshot CaptureSnapshot()
	{
		if (!_state.RuleSessionInitialized)
		{
			throw new InvalidOperationException("The match has not been initialized.");
		}

		return CreateSnapshot(
			_state,
			_randomSource.CaptureState(),
			_appliedActionIds.Order().ToImmutableArray());
	}

	public static AuthorityRuleSession Restore(
		MatchStateSnapshot snapshot,
		IRuleRandomSource randomSource,
		MatchRuleProfile profile)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentNullException.ThrowIfNull(randomSource);
		ArgumentNullException.ThrowIfNull(profile);
		SnapshotValidation.ValidateOrThrow(snapshot, nameof(snapshot));
		if (snapshot.RandomState.State == 0)
		{
			throw new ArgumentException(
				"The authority snapshot contains an invalid random state.",
				nameof(snapshot));
		}

		var session = new AuthorityRuleSession(randomSource, profile)
		{
			_nextPresentationBarrierId = Math.Max(1, snapshot.PendingPresentationBarrierId + 1),
		};
		RestoreState(session._state, snapshot);
		session._readModel = CreateReadModel(session._state) with
		{
			Adventure = session._adventureReadModel,
		};
		if (!snapshot.AppliedActionIds.IsDefaultOrEmpty)
		{
			session._appliedActionIds.UnionWith(snapshot.AppliedActionIds);
		}

		randomSource.RestoreState(snapshot.RandomState);
		return session;
	}


	internal static void RestoreState(GameBoardState state, MatchStateSnapshot snapshot)
	{
		state.PlayerCount = snapshot.PlayerCount;
		state.WinningScore = snapshot.WinningScore;
		state.CurrentTurnPlayerIndex = snapshot.CurrentTurnSeat;
		state.TurnSequenceGeneration = snapshot.TurnGeneration;
		state.CompletedTurnsSinceQuestionCardRefresh = snapshot.CompletedTurnsSinceQuestionRefresh;
		state.QuestionCardRefreshAnchorPlayerIndex = snapshot.QuestionRefreshAnchorSeat;
		state.MatchOutcomeKind = snapshot.Outcome switch
		{
			MatchOutcome.Won => global::MatchOutcomeKind.Winner,
			MatchOutcome.Draw => global::MatchOutcomeKind.Draw,
			_ => global::MatchOutcomeKind.InProgress,
		};
		state.WinnerPlayerIndex = snapshot.WinnerSeat;
		state.RuleSessionInitialized = true;
		state.RuleSessionEnded = snapshot.Outcome == MatchOutcome.Ended;
		state.RuleRevision = snapshot.Revision;
		state.RuleLifecycleGeneration = snapshot.LifecycleGeneration;
		state.RulePendingPresentationBarrierId = snapshot.PendingPresentationBarrierId;
		state.RulePendingTableOrderRuntimeInstanceIds.Clear();
		state.RulePendingTableOrderRuntimeInstanceIds.AddRange(
			snapshot.PendingTableOrderRuntimeInstanceIds.IsDefault
				? ImmutableArray<long>.Empty
				: snapshot.PendingTableOrderRuntimeInstanceIds);
		MandatorySelectionState snapshotSelection = snapshot.MandatorySelection
			?? MandatorySelectionState.None;
		state.MandatorySelection = snapshotSelection with
		{
			LegalSelectionIndexes = snapshotSelection.NormalizedLegalSelectionIndexes,
		};
		state.PendingAttachmentRewards.Clear();
		state.PendingAttachmentRewards.AddRange(
			snapshot.NormalizedPendingAttachmentRewards);
		state.ExtraActions.RestoreSnapshot(
			snapshot.NormalizedExtraActions);
		state.MatchFlow = snapshot.NormalizedMatchFlow;

		state.PlayerDecks.Clear();
		state.PlayerHands.Clear();
		state.PlayerItemHands.Clear();
		state.TableCards.Clear();
		Array.Clear(state.PlayerScores);
		for (int seat = 0; seat < CompatibilityPlayerCapacity; seat++)
		{
			state.PlayerDecks.Add(new List<PlayingCard>());
			state.PlayerHands.Add(new List<PlayingCard>());
			state.PlayerItemHands.Add(new List<ItemCard>());
			if (seat >= snapshot.PlayerCount)
			{
				continue;
			}

			PlayerStateSnapshot player = snapshot.Players[seat];
			state.PlayerDecks[seat].AddRange(player.Deck.Select(FromLogicalCard));
			state.PlayerHands[seat].AddRange(player.Hand.Select(FromLogicalCard));
			state.PlayerItemHands[seat].AddRange(player.ItemHand.Select(item => new ItemCard(item.Id)));
			state.PlayerScores[seat] = player.Score;
		}

		foreach (TableCardState tableCard in snapshot.TableCards)
		{
			TableCard restored = TableCard.Restore(
				tableCard.RuntimeInstanceId,
				tableCard.Card.HasValue ? FromLogicalCard(tableCard.Card.Value) : null,
				tableCard.IsFaceUp,
				tableCard.ItemOverlay.HasValue ? new ItemCard(tableCard.ItemOverlay.Value.Id) : null,
				tableCard.ItemOwnerSeat,
				tableCard.CardOwnerSeat,
				tableCard.RemoveOnCapture);
			restored.IsHiddenDuringCapture = tableCard.IsHiddenDuringCapture;
			restored.IsBuriedInSand = tableCard.IsBuried;
			restored.IsSlimeCoated = tableCard.IsScoreSuppressed;
			state.TableCards.Add(restored);
		}
	}

	private static MatchReadModel CreateReadModel(GameBoardState state)
	{
		return new MatchReadModel(
			state.RuleRevision,
			state.RuleLifecycleGeneration,
			state.TurnSequenceGeneration,
			state.PlayerCount,
			state.WinningScore,
			state.CurrentTurnPlayerIndex,
			GetOutcome(state),
			state.WinnerPlayerIndex,
			CreatePlayerSnapshots(state),
			CreateTableCardSnapshots(state),
			state.RulePendingPresentationBarrierId,
			state.RulePendingTableOrderRuntimeInstanceIds.ToImmutableArray(),
			state.MandatorySelection,
			state.PendingAttachmentRewards.ToImmutableArray(),
			state.ExtraActions.CaptureSnapshot(),
			state.CompletedTurnsSinceQuestionCardRefresh,
			state.QuestionCardRefreshAnchorPlayerIndex,
			state.MatchFlow);
	}

	private static MatchStateSnapshot CreateSnapshot(
		GameBoardState state,
		RuleRandomState randomState,
		ImmutableArray<long> appliedActionIds)
	{
		return new MatchStateSnapshot(
			MatchStateSnapshot.CurrentSchemaVersion,
			state.RuleRevision,
			state.RuleLifecycleGeneration,
			state.TurnSequenceGeneration,
			state.PlayerCount,
			state.WinningScore,
			state.CurrentTurnPlayerIndex,
			GetOutcome(state),
			state.WinnerPlayerIndex,
			CreatePlayerSnapshots(state),
			CreateTableCardSnapshots(state),
			randomState,
			state.RulePendingPresentationBarrierId,
			appliedActionIds,
			state.RulePendingTableOrderRuntimeInstanceIds.ToImmutableArray(),
			state.MandatorySelection,
			state.PendingAttachmentRewards.ToImmutableArray(),
			state.ExtraActions.CaptureSnapshot(),
			state.CompletedTurnsSinceQuestionCardRefresh,
			state.QuestionCardRefreshAnchorPlayerIndex,
			state.MatchFlow);
	}

	private static ImmutableArray<PlayerStateSnapshot> CreatePlayerSnapshots(GameBoardState state)
	{
		var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(state.PlayerCount);
		for (int seat = 0; seat < state.PlayerCount; seat++)
		{
			players.Add(new PlayerStateSnapshot(
				seat,
				state.PlayerDecks[seat].Select(ToLogicalCard).ToImmutableArray(),
				state.PlayerHands[seat].Select(ToLogicalCard).ToImmutableArray(),
				state.PlayerItemHands[seat].Select(item => new LogicalItemState(item.Id)).ToImmutableArray(),
				state.PlayerScores[seat]));
		}

		return players.MoveToImmutable();
	}

	private static ImmutableArray<TableCardState> CreateTableCardSnapshots(GameBoardState state)
	{
		var tableCards = ImmutableArray.CreateBuilder<TableCardState>(state.TableCards.Count);
		foreach (TableCard tableCard in state.TableCards)
		{
			tableCards.Add(new TableCardState(
				tableCard.InstanceId,
				tableCard.Card.HasValue ? ToLogicalCard(tableCard.Card.Value) : null,
				tableCard.IsFaceUp,
				tableCard.IsHiddenDuringCapture,
				tableCard.IsBuriedInSand,
				tableCard.IsSlimeCoated,
				tableCard.ItemOverlay.HasValue ? new LogicalItemState(tableCard.ItemOverlay.Value.Id) : null,
				tableCard.ItemOwnerPlayerIndex,
				tableCard.CardOwnerPlayerIndex,
				tableCard.RemoveCardOnCapture));
		}

		return tableCards.MoveToImmutable();
	}

	private static MatchOutcome GetOutcome(GameBoardState state)
	{
		if (state.RuleSessionEnded)
		{
			return MatchOutcome.Ended;
		}

		return state.MatchOutcomeKind switch
		{
			global::MatchOutcomeKind.Winner => MatchOutcome.Won,
			global::MatchOutcomeKind.Draw => MatchOutcome.Draw,
			_ => MatchOutcome.InProgress,
		};
	}

	private static LogicalCardState ToLogicalCard(PlayingCard card)
	{
		return new LogicalCardState(
			card.InstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.GetAttachedItemCardIds().ToImmutableArray());
	}

	private static PlayingCard FromLogicalCard(LogicalCardState card)
	{
		return PlayingCard.Restore(
			card.RuntimeInstanceId,
			card.Code,
			card.Suit,
			card.Rank,
			card.NormalizedAttachedEffectIds);
	}

}
