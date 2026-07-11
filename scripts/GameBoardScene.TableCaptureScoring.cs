using System.Collections.Generic;

public partial class GameBoardScene
{
	private int GetCapturedTablePoints(int captureStartIndex, int scorePlayerIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return ScoreService.GetCapturedScore(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			scorePlayerIndex,
			BuildCaptureScoringContext(scorePlayerIndex),
			extraTableCardIndexes).Points;
	}

	private int GetCapturedTableCardScoreValueForPlayer(TableCard tableCard, int scorePlayerIndex, bool hasDoubleEffect)
	{
		return CaptureScoringRules.GetCapturedTableCardScoreValue(
			ToCaptureScoringTableCard(tableCard),
			BuildCaptureScoringContext(scorePlayerIndex),
			hasDoubleEffect);
	}

	private CaptureScoringRules.Context BuildCaptureScoringContext(int scorePlayerIndex)
	{
		bool applyScoreReduction = ShouldApplyCrabScoreReduction(scorePlayerIndex);
		return new CaptureScoringRules.Context(
			SuppressSlimeCoatedScore: ShouldSuppressSlimeCoatedCapturedScore(scorePlayerIndex),
			ApplyScoreReductionPerCard: applyScoreReduction,
			ScoreReductionPerCard: applyScoreReduction
				? GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.ScoreReductionPerCard, 2)
				: 0);
	}

	private bool ShouldSuppressSlimeCoatedCapturedScore(int scorePlayerIndex)
	{
		return adventure_mode
			&& scorePlayerIndex == MainPlayerIndex
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.ColorCurrent);
	}

	private bool IsSeaSlugSlimeScoreSuppressed(TableCard tableCard, int scorePlayerIndex)
	{
		return CaptureScoringRules.IsSlimeScoreSuppressed(
			tableCard.IsSlimeCoated,
			BuildCaptureScoringContext(scorePlayerIndex));
	}

	private bool HasCapturedDoubleScoreEffect(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return CaptureScoringRules.HasCapturedDoubleScoreEffect(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			extraTableCardIndexes);
	}

	private int GetCapturedTableScorePlayerIndex(int captureStartIndex, int fallbackPlayerIndex)
	{
		return ScoreService.GetCapturedScorePlayerIndex(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			fallbackPlayerIndex,
			player_count);
	}

	private bool IsCapturedTableCardScoringIndex(int tableCardIndex, int captureStartIndex)
	{
		return CaptureScoringRules.IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex, _tableCards.Count);
	}

	private bool HasCapturedScoringTableCards(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return CaptureScoringRules.HasCapturedScoringTableCards(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			extraTableCardIndexes);
	}

	private int BuildAdventureCaptureDamageCardPointLists(
	IReadOnlyList<CaptureScoringRules.TableCard> tableCards,
	int captureStartIndex,
	int scorePlayerIndex,
	IReadOnlySet<int>? extraTableCardIndexes,
	List<int> rawPoints,
	List<int> resolvedPoints,
	bool includeCaptureEndpointsInScoring = false)
{
	rawPoints.Clear();
	resolvedPoints.Clear();
	if (captureStartIndex < 0 || captureStartIndex >= tableCards.Count)
	{
		return 0;
	}

	bool hasDoubleEffect = CaptureScoringRules.HasCapturedDoubleScoreEffect(
		tableCards,
		captureStartIndex,
		extraTableCardIndexes);
	CaptureScoringRules.Context context = BuildCaptureScoringContext(scorePlayerIndex);
	var addedIndexes = new HashSet<int>();

	int TryAddCardPointValues(int tableCardIndex, bool allowAnyVisibleCapturedCard)
	{
		if (tableCardIndex < 0 || tableCardIndex >= tableCards.Count)
		{
			return 0;
		}

		CaptureScoringRules.TableCard tableCard = tableCards[tableCardIndex];
		if (tableCard.IsBuriedInSand || !tableCard.Card.HasValue)
		{
			return 0;
		}

		if (!allowAnyVisibleCapturedCard
			&& !CaptureScoringRules.IsCapturedTableCardScoringIndex(
				tableCardIndex,
				captureStartIndex,
				tableCards.Count))
		{
			return 0;
		}

		int rawPoint = Math.Max(0, tableCard.Card.Value.ScoreValue);
		if (rawPoint <= 0 || !addedIndexes.Add(tableCardIndex))
		{
			return 0;
		}

		int resolvedPoint = CaptureScoringRules.GetCapturedTableCardScoreValue(
			tableCard,
			context,
			hasDoubleEffect);
		rawPoints.Add(rawPoint);
		resolvedPoints.Add(resolvedPoint);
		return resolvedPoint;
	}

	for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
	{
		TryAddCardPointValues(tableCardIndex, allowAnyVisibleCapturedCard: false);
	}

	if (extraTableCardIndexes != null)
	{
		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			TryAddCardPointValues(tableCardIndex, allowAnyVisibleCapturedCard: true);
		}
	}

	if (!includeCaptureEndpointsInScoring)
	{
		return 0;
	}

	int captureTailTableCardIndex = -1;
	for (int tableCardIndex = tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
	{
		if (!tableCards[tableCardIndex].IsBuriedInSand)
		{
			captureTailTableCardIndex = tableCardIndex;
			break;
		}
	}

	int endpointResolvedPoints = TryAddCardPointValues(
		captureStartIndex,
		allowAnyVisibleCapturedCard: true);
	endpointResolvedPoints += TryAddCardPointValues(
		captureTailTableCardIndex,
		allowAnyVisibleCapturedCard: true);
	return endpointResolvedPoints;
}

	private int CountCapturedOldScoringTableCards(
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes = null)
	{
		if (prePlayTableCards.Count == 0)
		{
			return 0;
		}

		var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
		int count = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!oldTableCards.Contains(_tableCards[tableCardIndex])
				|| !IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex))
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue && GetCardScoreValue(tableCard.Card.Value) > 0)
			{
				count++;
			}
		}

		if (extraTableCardIndexes == null)
		{
			return count;
		}

		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			if (tableCardIndex < 0
				|| tableCardIndex >= _tableCards.Count
				|| !oldTableCards.Contains(_tableCards[tableCardIndex]))
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue && GetCardScoreValue(tableCard.Card.Value) > 0)
			{
				count++;
			}
		}

		return count;
	}

	private void GetCapturedOldTableResolvedPointTotals(
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		HashSet<int>? extraTableCardIndexes,
		out int totalResolvedOldTablePoints,
		out int currentResolvedOldTableScoringPoints)
	{
		int totalPoints = 0;
		int currentScoringPoints = 0;
		if (prePlayTableCards.Count == 0)
		{
			totalResolvedOldTablePoints = 0;
			currentResolvedOldTableScoringPoints = 0;
			return;
		}

		CaptureScoringRules.TableCard[] captureScoringTableCards = ToCaptureScoringTableCards();
		bool hasDoubleEffect = CaptureScoringRules.HasCapturedDoubleScoreEffect(
			captureScoringTableCards,
			captureStartIndex,
			extraTableCardIndexes);
		CaptureScoringRules.Context context = BuildCaptureScoringContext(scorePlayerIndex);
		var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
		var countedIndexes = new HashSet<int>();

		void AccumulateIfOld(int tableCardIndex, bool currentlyScoring)
		{
			if (!countedIndexes.Add(tableCardIndex)
				|| tableCardIndex < 0
				|| tableCardIndex >= _tableCards.Count)
			{
				return;
			}

			TableCard liveTableCard = _tableCards[tableCardIndex];
			if (!oldTableCards.Contains(liveTableCard) || liveTableCard.IsBuriedInSand || !liveTableCard.Card.HasValue)
			{
				return;
			}

			int resolvedPoints = CaptureScoringRules.GetCapturedTableCardScoreValue(
				ToCaptureScoringTableCard(liveTableCard),
				context,
				hasDoubleEffect);
			if (resolvedPoints <= 0)
			{
				return;
			}

			totalPoints += resolvedPoints;
			if (currentlyScoring)
			{
				currentScoringPoints += resolvedPoints;
			}
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			AccumulateIfOld(
				tableCardIndex,
				IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex));
		}

		if (extraTableCardIndexes == null)
		{
			totalResolvedOldTablePoints = totalPoints;
			currentResolvedOldTableScoringPoints = currentScoringPoints;
			return;
		}

		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			AccumulateIfOld(tableCardIndex, currentlyScoring: true);
		}

		totalResolvedOldTablePoints = totalPoints;
		currentResolvedOldTableScoringPoints = currentScoringPoints;
	}

	private bool IsCapturedTableCardReference(
		TableCard? target,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes = null)
	{
		if (target == null)
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (ReferenceEquals(_tableCards[tableCardIndex], target))
			{
				return true;
			}
		}

		if (extraTableCardIndexes == null)
		{
			return false;
		}

		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			if (tableCardIndex >= 0
				&& tableCardIndex < _tableCards.Count
				&& ReferenceEquals(_tableCards[tableCardIndex], target))
			{
				return true;
			}
		}

		return false;
	}

	private TableCard? FindRightmostRemainingOldBaseTableCard(IReadOnlyCollection<TableCard> prePlayTableCards)
	{
		if (prePlayTableCards.Count == 0)
		{
			return null;
		}

		var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (oldTableCards.Contains(tableCard) && !tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				return tableCard;
			}
		}

		return null;
	}

	private TableCard? FindLeftmostCurrentBaseTableCard()
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				return tableCard;
			}
		}

		return null;
	}

	private bool CapturedOnlyOuterVisibleCards(
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes = null)
	{
		if (prePlayTableCards.Count == 0)
		{
			return false;
		}

		var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
		int visibleCapturedCount = 0;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!oldTableCards.Contains(tableCard) || tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (tableCard.IsFaceUp)
			{
				visibleCapturedCount++;
			}
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
				{
					continue;
				}

				TableCard tableCard = _tableCards[tableCardIndex];
				if (oldTableCards.Contains(tableCard) && !tableCard.IsBuriedInSand && tableCard.IsFaceUp)
				{
					visibleCapturedCount++;
				}
			}
		}

		return visibleCapturedCount == 2;
	}

	private CaptureScoringRules.TableCard[] ToCaptureScoringTableCards()
	{
		return ToCaptureScoringTableCards(_tableCards);
	}

	private CaptureScoringRules.TableCard[] ToCaptureScoringTableCards(IReadOnlyList<TableCard> sourceTableCards)
	{
		var tableCards = new CaptureScoringRules.TableCard[sourceTableCards.Count];
		for (int index = 0; index < sourceTableCards.Count; index++)
		{
			tableCards[index] = ToCaptureScoringTableCard(sourceTableCards[index]);
		}

		return tableCards;
	}

	private CaptureScoringRules.TableCard ToCaptureScoringTableCard(TableCard tableCard)
	{
		CaptureScoringRules.Card? card = tableCard.Card.HasValue
			? ToCaptureScoringCard(tableCard.Card.Value)
			: null;

		return new CaptureScoringRules.TableCard(
			card,
			tableCard.IsBuriedInSand,
			tableCard.IsSlimeCoated,
			IsItemEffectType(tableCard, CardEffectTypes.DoubleCollect),
			IsItemEffectType(tableCard, CardEffectTypes.PlaceTablePairDestroy),
			tableCard.ItemOwnerPlayerIndex);
	}

	private CaptureScoringRules.Card ToCaptureScoringCard(PlayingCard card)
	{
		return new CaptureScoringRules.Card(
			card.Rank,
			GetCardScoreValue(card),
			IsAttachedEffectType(card, CardEffectTypes.DoubleCollect),
			card.Suit);
	}
}
