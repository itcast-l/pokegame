using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;

public partial class GameBoardScene
{
	private List<TableCard> BuildShuffledTableCards()
	{
		var tableCardReferences = ImmutableArray.CreateBuilder<RuleTableCardReference>(_tableCards.Count);
		var fixedTableIndexes = ImmutableArray.CreateBuilder<int>();
		for (int index = 0; index < _tableCards.Count; index++)
		{
			tableCardReferences.Add(new RuleTableCardReference(index, _tableCards[index].InstanceId));
			if (IsFixedDuringTableShuffle(_tableCards[index]))
			{
				fixedTableIndexes.Add(index);
			}
		}

		if (!TrySubmitPlanTableShuffleRuleCommand(
			_currentTurnPlayerIndex,
			tableCardReferences.MoveToImmutable(),
			fixedTableIndexes.ToImmutable(),
			_turnSequenceGeneration,
			out RuleCommandResult result))
		{
			return new List<TableCard>();
		}

		ImmutableArray<long> plannedOrder = result.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds;
		if (plannedOrder.Length != _tableCards.Count)
		{
			TryCompleteRulePresentationBarrier(
				_turnSequenceGeneration,
				"Invalid table-shuffle plan recovery");
			return new List<TableCard>();
		}

		var tableCardsById = new Dictionary<long, TableCard>(_tableCards.Count);
		foreach (TableCard tableCard in _tableCards)
		{
			if (!tableCardsById.TryAdd(tableCard.InstanceId, tableCard))
			{
				TryCompleteRulePresentationBarrier(
					_turnSequenceGeneration,
					"Duplicate table-card shuffle identity recovery");
				return new List<TableCard>();
			}
		}

		var shuffledTableCards = new List<TableCard>(plannedOrder.Length);
		foreach (long runtimeInstanceId in plannedOrder)
		{
			if (!tableCardsById.TryGetValue(runtimeInstanceId, out TableCard? tableCard))
			{
				TryCompleteRulePresentationBarrier(
					_turnSequenceGeneration,
					"Missing table-card shuffle identity recovery");
				return new List<TableCard>();
			}
			shuffledTableCards.Add(tableCard);
		}

		return shuffledTableCards;
	}

	private bool ApplyShuffledTableCards(IReadOnlyList<TableCard> shuffledTableCards)
	{
		if (_authorityRuleSession == null)
		{
			return false;
		}

		ImmutableArray<long> pendingOrder = _authorityRuleSession.ReadModel
			.NormalizedPendingTableOrderRuntimeInstanceIds;
		bool matchesPlan = pendingOrder.Length == shuffledTableCards.Count;
		for (int index = 0; matchesPlan && index < pendingOrder.Length; index++)
		{
			matchesPlan = shuffledTableCards[index].InstanceId == pendingOrder[index];
		}

		bool completed = TryCompleteRulePresentationBarrier(
			_turnSequenceGeneration,
			"Table-shuffle presentation barrier completion");
		return matchesPlan && completed;
	}

	private bool IsFixedDuringTableShuffle(TableCard tableCard)
	{
		return IsFixedDuringTableShuffleByData(tableCard);
	}
}
