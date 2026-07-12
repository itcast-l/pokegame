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
		List<int> tableIndexes,
		bool includeCaptureEndpointsInScoring = false)
	{
		rawPoints.Clear();
		resolvedPoints.Clear();
		tableIndexes.Clear();
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
			tableIndexes.Add(tableCardIndex);
			return resolvedPoint;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < tableCards.Count; tableCardIndex++)
		{
			TryAddCardPointValues(tableCardIndex, allowAnyVisibleCapturedCard: false);
		}

		if (extraTableCardIndexes != null)
		{
			var sortedExtraTableCardIndexes = new List<int>(
				extraTableCardIndexes);
			sortedExtraTableCardIndexes.Sort();
			foreach (int tableCardIndex in sortedExtraTableCardIndexes)
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

	private int ResolveBlackLedgerRedirectTableCardIndex(
		int playerIndex,
		int captureStartIndex)
	{
		var scoreValues = new int[_tableCards.Count];
		for (int tableCardIndex = 0;
			tableCardIndex < _tableCards.Count;
			tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			scoreValues[tableCardIndex] = !tableCard.IsBuriedInSand
				&& !tableCard.RemoveCardOnCapture
				&& tableCard.Card.HasValue
				? Math.Max(0, GetCardScoreValue(tableCard.Card.Value))
				: 0;
		}

		return AdventureUpgradeEffectService
			.ResolveBlackLedgerRedirectTableCardIndex(
				AdventureUpgradeEffectService.HasBlackLedger(
					GetAdventureUpgradeRunState()
						?.GetAcquiredUpgradeIds()
						?? Array.Empty<string>()),
				playerIndex == MainPlayerIndex,
				scoreValues,
				captureStartIndex);
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
