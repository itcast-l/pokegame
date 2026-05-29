using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private int GetCapturedTablePoints(int captureStartIndex, int scorePlayerIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count || !HasCapturedMiddleTableCards(captureStartIndex))
		{
			return 0;
		}

		int collectedPoints = 0;
		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, extraTableCardIndexes);
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (!IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex))
			{
				continue;
			}

			if (tableCard.Card.HasValue)
			{
				collectedPoints += GetCapturedTableCardScoreValueForPlayer(tableCard, scorePlayerIndex, hasDoubleEffect);
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
				if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
				{
					collectedPoints += GetCapturedTableCardScoreValueForPlayer(tableCard, scorePlayerIndex, hasDoubleEffect);
				}
			}
		}

		return collectedPoints;
	}

	private int GetCapturedTableCardScoreValueForPlayer(TableCard tableCard, int scorePlayerIndex, bool hasDoubleEffect)
	{
		if (!tableCard.Card.HasValue || IsSeaSlugSlimeScoreSuppressed(tableCard, scorePlayerIndex))
		{
			return 0;
		}

		return GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private bool IsSeaSlugSlimeScoreSuppressed(TableCard tableCard, int scorePlayerIndex)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.ColorCurrent)
			&& scorePlayerIndex == MainPlayerIndex
			&& tableCard.IsSlimeCoated;
	}

	private bool HasCapturedDoubleScoreEffect(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand && HasDoubleScoreEffect(_tableCards[tableCardIndex]))
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
				&& !_tableCards[tableCardIndex].IsBuriedInSand
				&& HasDoubleScoreEffect(_tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	private bool HasDoubleScoreEffect(TableCard tableCard)
	{
		return (tableCard.Card.HasValue && IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
			|| IsItemEffectType(tableCard, CardEffectTypes.DoubleCollect);
	}

	private int GetCapturedTableScorePlayerIndex(int captureStartIndex, int fallbackPlayerIndex)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return fallbackPlayerIndex;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!IsItemEffectType(tableCard, CardEffectTypes.PlaceTablePairDestroy))
			{
				continue;
			}

			if (tableCard.ItemOwnerPlayerIndex >= 0 && tableCard.ItemOwnerPlayerIndex < player_count)
			{
				return tableCard.ItemOwnerPlayerIndex;
			}
		}

		return fallbackPlayerIndex;
	}

	private bool IsCapturedTableCardScoringIndex(int tableCardIndex, int captureStartIndex)
	{
		return tableCardIndex > captureStartIndex && tableCardIndex < _tableCards.Count - 1;
	}

	private bool HasCapturedScoringTableCards(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count || !HasCapturedMiddleTableCards(captureStartIndex))
		{
			return false;
		}

		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex)
				&& IsScoringTableCard(_tableCards[tableCardIndex]))
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
				&& IsScoringTableCard(_tableCards[tableCardIndex]))
			{
				return true;
			}
		}

		return false;
	}

	private static bool IsScoringTableCard(TableCard tableCard)
	{
		return !tableCard.IsBuriedInSand
			&& tableCard.Card.HasValue
			&& GetCardScoreValue(tableCard.Card.Value) > 0;
	}
}
