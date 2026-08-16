using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Deckland.Rules;

internal static class NetworkAuthorityCheckpointRuleAdapter
{
	internal static NetworkAuthorityCheckpoint ApplyRuleSnapshot(
		NetworkAuthorityCheckpoint checkpoint,
		MatchStateSnapshot snapshot,
		bool recordTelemetry = true)
	{
		ArgumentNullException.ThrowIfNull(checkpoint);
		ArgumentNullException.ThrowIfNull(snapshot);
		if (!recordTelemetry)
		{
			return ApplyRuleSnapshotCore(checkpoint, snapshot, out _);
		}

		long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
		long startedAt = Stopwatch.GetTimestamp();
		int projectedEntryCount = 0;
		try
		{
			return ApplyRuleSnapshotCore(
				checkpoint,
				snapshot,
				out projectedEntryCount);
		}
		finally
		{
			long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
			long allocatedBytes =
				GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
			AuthorityCheckpointTelemetry.RecordProjection(
				projectedEntryCount,
				allocatedBytes,
				elapsedTicks);
		}
	}

	private static NetworkAuthorityCheckpoint ApplyRuleSnapshotCore(
		NetworkAuthorityCheckpoint checkpoint,
		MatchStateSnapshot snapshot,
		out int projectedEntryCount)
	{
		List<List<NetworkCheckpointCard>> decks =
			ExportCardLists(snapshot.Players, exportDecks: true);
		List<List<NetworkCheckpointCard>> hands =
			ExportCardLists(snapshot.Players, exportDecks: false);
		List<List<string>> itemHands = ExportItemHands(snapshot.Players);
		List<NetworkCheckpointTableCard> tableCards =
			ExportTableCards(snapshot.TableCards);
		projectedEntryCount = CountProjectedEntries(
			decks,
			hands,
			itemHands,
			tableCards.Count);
		return checkpoint with
		{
			PlayerCount = snapshot.PlayerCount,
			WinningScore = snapshot.WinningScore,
			Decks = decks,
			Hands = hands,
			ItemHands = itemHands,
			TableCards = tableCards,
			Scores = ExportScores(snapshot.Players),
			BasePlayCaptureThreshold = snapshot.BasePlayCaptureThreshold,
			MatchOutcomeKind = ToNetworkMatchOutcome(snapshot.Outcome),
			WinnerPlayerIndex = snapshot.WinnerSeat,
			CurrentTurnPlayerIndex = snapshot.CurrentTurnSeat,
			TurnSequenceGeneration = snapshot.TurnGeneration,
			CompletedTurnsSinceQuestionRefresh =
				snapshot.CompletedTurnsSinceQuestionRefresh,
			QuestionRefreshAnchorPlayerIndex = snapshot.QuestionRefreshAnchorSeat,
			RngSeed = unchecked((long)snapshot.RandomState.Seed),
			RngState = snapshot.RandomState.State.ToString(CultureInfo.InvariantCulture),
		};
	}

	internal static int CountProjectedEntries(NetworkAuthorityCheckpoint checkpoint)
	{
		ArgumentNullException.ThrowIfNull(checkpoint);
		return CountProjectedEntries(
			checkpoint.Decks,
			checkpoint.Hands,
			checkpoint.ItemHands,
			checkpoint.TableCards.Count);
	}

	private static int CountProjectedEntries(
		IReadOnlyList<List<NetworkCheckpointCard>> decks,
		IReadOnlyList<List<NetworkCheckpointCard>> hands,
		IReadOnlyList<List<string>> itemHands,
		int tableCardCount)
	{
		int count = tableCardCount;
		for (int seat = 0; seat < decks.Count; seat++)
		{
			count = checked(
				count
				+ decks[seat].Count
				+ hands[seat].Count
				+ itemHands[seat].Count);
		}
		return count;
	}

	internal static bool TryCreateRuleSnapshot(
		NetworkAuthorityCheckpoint checkpoint,
		int lifecycleGeneration,
		int turnGeneration,
		ImmutableArray<PlayerStateSnapshot> players,
		ImmutableArray<TableCardState> tableCards,
		RuleRandomState fallbackRandomState,
		ImmutableArray<PendingAttachmentRewardState> pendingAttachmentRewards,
		out MatchStateSnapshot snapshot,
		out string error)
	{
		snapshot = null!;
		error = string.Empty;
		if (checkpoint is null
			|| lifecycleGeneration <= 0
			|| turnGeneration <= 0
			|| players.IsDefault
			|| players.Length != checkpoint.PlayerCount
			|| tableCards.IsDefault
			|| pendingAttachmentRewards.IsDefault)
		{
			error = "checkpoint rule state is invalid";
			return false;
		}

		RuleRandomState randomState = fallbackRandomState;
		if (checkpoint.RngSeed != 0)
		{
			if (!ulong.TryParse(
					checkpoint.RngState,
					NumberStyles.None,
					CultureInfo.InvariantCulture,
					out ulong rngState)
				|| rngState == 0)
			{
				error = "checkpoint RNG state is invalid";
				return false;
			}

			randomState = new RuleRandomState(
				unchecked((ulong)checkpoint.RngSeed),
				rngState);
		}

		if (randomState.State == 0)
		{
			error = "checkpoint RNG state is invalid";
			return false;
		}

		snapshot = new MatchStateSnapshot(
			MatchStateSnapshot.CurrentSchemaVersion,
			checkpoint.CheckpointId,
			lifecycleGeneration,
			turnGeneration,
			checkpoint.PlayerCount,
			checkpoint.WinningScore,
			checkpoint.CurrentTurnPlayerIndex,
			ToRuleMatchOutcome(checkpoint.MatchOutcomeKind),
			checkpoint.WinnerPlayerIndex,
			players,
			tableCards,
			randomState,
			PendingPresentationBarrierId: 0,
			AppliedActionIds: ImmutableArray<long>.Empty,
			PendingAttachmentRewards: pendingAttachmentRewards,
			CompletedTurnsSinceQuestionRefresh:
				checkpoint.CompletedTurnsSinceQuestionRefresh,
			QuestionRefreshAnchorSeat:
				checkpoint.QuestionRefreshAnchorPlayerIndex,
			PendingBasePlayCapture: PendingBasePlayCaptureState.None,
			BasePlayCaptureThreshold: checkpoint.BasePlayCaptureThreshold);
		return true;
	}

	internal static MatchOutcomeKind ToNetworkMatchOutcome(
		Deckland.Rules.MatchOutcome outcome)
	{
		return outcome switch
		{
			Deckland.Rules.MatchOutcome.Won => MatchOutcomeKind.Winner,
			Deckland.Rules.MatchOutcome.Draw => MatchOutcomeKind.Draw,
			_ => MatchOutcomeKind.InProgress,
		};
	}

	private static Deckland.Rules.MatchOutcome ToRuleMatchOutcome(
		MatchOutcomeKind outcome)
	{
		return outcome switch
		{
			MatchOutcomeKind.Winner => Deckland.Rules.MatchOutcome.Won,
			MatchOutcomeKind.Draw => Deckland.Rules.MatchOutcome.Draw,
			_ => Deckland.Rules.MatchOutcome.InProgress,
		};
	}

	private static List<List<NetworkCheckpointCard>> ExportCardLists(
		ImmutableArray<PlayerStateSnapshot> players,
		bool exportDecks)
	{
		var result = new List<List<NetworkCheckpointCard>>(players.Length);
		foreach (PlayerStateSnapshot player in players)
		{
			ImmutableArray<LogicalCardState> source = exportDecks
				? player.Deck
				: player.Hand;
			var cards = new List<NetworkCheckpointCard>(source.Length);
			foreach (LogicalCardState card in source)
			{
				cards.Add(ToCheckpointCard(card));
			}
			result.Add(cards);
		}

		return result;
	}

	private static List<List<string>> ExportItemHands(
		ImmutableArray<PlayerStateSnapshot> players)
	{
		var result = new List<List<string>>(players.Length);
		foreach (PlayerStateSnapshot player in players)
		{
			var items = new List<string>(player.ItemHand.Length);
			foreach (LogicalItemState item in player.ItemHand)
			{
				items.Add(item.Id);
			}
			result.Add(items);
		}

		return result;
	}

	private static int[] ExportScores(ImmutableArray<PlayerStateSnapshot> players)
	{
		var scores = new int[players.Length];
		for (int seat = 0; seat < players.Length; seat++)
		{
			scores[seat] = players[seat].Score;
		}
		return scores;
	}

	private static NetworkCheckpointCard ToCheckpointCard(LogicalCardState card)
	{
		return new NetworkCheckpointCard
		{
			Code = card.Code,
			AttachedItemCardIds = card.NormalizedAttachedEffectIds,
		};
	}

	private static List<NetworkCheckpointTableCard> ExportTableCards(
		ImmutableArray<TableCardState> tableCards)
	{
		var result = new List<NetworkCheckpointTableCard>(tableCards.Length);
		foreach (TableCardState tableCard in tableCards)
		{
			result.Add(new NetworkCheckpointTableCard
			{
				Card = tableCard.Card.HasValue
					? ToCheckpointCard(tableCard.Card.Value)
					: null,
				IsFaceUp = tableCard.IsFaceUp,
				IsBuriedInSand = tableCard.IsBuried,
				IsSlimeCoated = tableCard.IsScoreSuppressed,
				CardOwnerPlayerIndex = tableCard.CardOwnerSeat,
				RemoveCardOnCapture = tableCard.RemoveOnCapture,
				ItemOverlayId = tableCard.ItemOverlay?.Id ?? string.Empty,
				ItemOwnerPlayerIndex = tableCard.ItemOwnerSeat,
			});
		}

		return result;
	}
}
