using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;

public partial class GameBoardScene
{
	private sealed class DebugRuleFixture
	{
		internal DebugRuleFixture(int playerCount)
		{
			for (int seat = 0; seat < playerCount; seat++)
			{
				PlayerDecks.Add(new List<PlayingCard>());
				PlayerHands.Add(new List<PlayingCard>());
				PlayerItemHands.Add(new List<ItemCard>());
				PlayerScores.Add(0);
			}
		}

		internal List<List<PlayingCard>> PlayerDecks { get; } = new();
		internal List<List<PlayingCard>> PlayerHands { get; } = new();
		internal List<List<ItemCard>> PlayerItemHands { get; } = new();
		internal List<TableCard> TableCards { get; } = new();
		internal List<int> PlayerScores { get; } = new();
		internal int BasePlayCaptureThreshold { get; set; } =
			ContinuousCaptureRules.MinimumThreshold;
		internal int CurrentTurnSeat { get; set; }
	}

	private DebugRuleFixture CreateEmptyDebugRuleFixture()
	{
		return new DebugRuleFixture(player_count);
	}

	private DebugRuleFixture CreateCurrentDebugRuleFixture()
	{
		var fixture = new DebugRuleFixture(player_count)
		{
			CurrentTurnSeat = _currentTurnPlayerIndex,
		};
		for (int seat = 0; seat < player_count; seat++)
		{
			fixture.PlayerDecks[seat].AddRange(_playerDecks[seat]);
			fixture.PlayerHands[seat].AddRange(_playerHands[seat]);
			fixture.PlayerItemHands[seat].AddRange(_playerItemHands[seat]);
			fixture.PlayerScores[seat] = _playerScores[seat];
		}
		fixture.BasePlayCaptureThreshold = GetBasePlayCaptureThreshold();
		fixture.TableCards.AddRange(_tableCards);
		return fixture;
	}

	private bool CommitDebugRuleFixture(DebugRuleFixture fixture)
	{
		if (fixture.PlayerDecks.Count != player_count
			|| fixture.PlayerHands.Count != player_count
			|| fixture.PlayerItemHands.Count != player_count
			|| fixture.PlayerScores.Count != player_count
			|| !ContinuousCaptureRules.IsValidThreshold(
				fixture.BasePlayCaptureThreshold))
		{
			return false;
		}

		var runtimeInstanceIds = new HashSet<long>();
		var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(player_count);
		for (int seat = 0; seat < player_count; seat++)
		{
			players.Add(new PlayerStateSnapshot(
				seat,
				SnapshotDebugCards(fixture.PlayerDecks[seat], runtimeInstanceIds),
				SnapshotDebugCards(fixture.PlayerHands[seat], runtimeInstanceIds),
				fixture.PlayerItemHands[seat]
					.ConvertAll(item => new LogicalItemState(item.Id))
					.ToImmutableArray(),
				fixture.PlayerScores[seat]));
		}

		var tableCards = ImmutableArray.CreateBuilder<TableCardState>(fixture.TableCards.Count);
		foreach (TableCard tableCard in fixture.TableCards)
		{
			TableCardState state = ToRuleTableCardState(tableCard);
			if (state.Card.HasValue && !runtimeInstanceIds.Add(state.Card.Value.RuntimeInstanceId))
			{
				PlayingCard cloned = tableCard.Card!.Value.CloneWithNewRuntimeInstanceId();
				state = state with { Card = ToRuleCardState(cloned) };
				runtimeInstanceIds.Add(state.Card.Value.RuntimeInstanceId);
			}
			tableCards.Add(state);
		}

		return TryInitializeScriptedRuleMatch(
			players.MoveToImmutable(),
			tableCards.MoveToImmutable(),
			fixture.CurrentTurnSeat,
			fixture.BasePlayCaptureThreshold);
	}

	private static ImmutableArray<LogicalCardState> SnapshotDebugCards(
		IReadOnlyList<PlayingCard> cards,
		HashSet<long> runtimeInstanceIds)
	{
		var result = ImmutableArray.CreateBuilder<LogicalCardState>(cards.Count);
		foreach (PlayingCard card in cards)
		{
			PlayingCard uniqueCard = runtimeInstanceIds.Add(card.InstanceId)
				? card
				: card.CloneWithNewRuntimeInstanceId();
			runtimeInstanceIds.Add(uniqueCard.InstanceId);
			result.Add(ToRuleCardState(uniqueCard));
		}
		return result.MoveToImmutable();
	}

	private bool TryReplaceDebugTableCardPresentationReference(
		int tableCardIndex,
		TableCard replacement)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return false;
		}

		if (_networkReplicaPresentationState != null)
		{
			_networkReplicaPresentationState.TableCards[tableCardIndex] = replacement;
			return true;
		}

		return _ruleReadProjection.TryReplaceTableCardPresentationReference(
			tableCardIndex,
			replacement);
	}
}
