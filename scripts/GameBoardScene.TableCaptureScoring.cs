using System.Collections.Generic;

public partial class GameBoardScene
{
	private int GetCapturedTablePoints(int captureStartIndex, int scorePlayerIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		return CaptureScoringRules.GetCapturedTablePoints(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			BuildCaptureScoringContext(scorePlayerIndex),
			extraTableCardIndexes);
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
		return CaptureScoringRules.GetCapturedTableScorePlayerIndex(
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

	private CaptureScoringRules.TableCard[] ToCaptureScoringTableCards()
	{
		var tableCards = new CaptureScoringRules.TableCard[_tableCards.Count];
		for (int index = 0; index < _tableCards.Count; index++)
		{
			tableCards[index] = ToCaptureScoringTableCard(_tableCards[index]);
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
			GetCardScoreValue(card),
			IsAttachedEffectType(card, CardEffectTypes.DoubleCollect));
	}
}
