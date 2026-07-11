using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private List<CapturedTableCardVisual> BuildCapturedTableCardVisuals(
	int captureStartIndex,
	bool includeQuestionCard,
	bool scoringCardsOnly = false,
	HashSet<int>? extraTableCardIndexes = null,
	PairCaptureEdgeFloatHold? flightStartOverrides = null,
	bool includeCaptureStartScoring = false,
	bool includeCaptureEndScoring = false,
	int scorePlayerIndex = -1)
{
	var visuals = new List<CapturedTableCardVisual>();
	if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
	{
		return visuals;
	}

	int captureEndScoringIndex = includeCaptureEndScoring
		? GetCaptureTailTableCardIndex(captureStartIndex)
		: -1;
	Rect2 tableLayoutRect = GetTableCardLayoutRect();
	for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
	{
		bool isBaseCapturedIndex = tableCardIndex >= captureStartIndex;
		bool isExtraCapturedIndex = extraTableCardIndexes != null
			&& extraTableCardIndexes.Contains(tableCardIndex);
		if (!isBaseCapturedIndex && !isExtraCapturedIndex)
		{
			continue;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (tableCard.IsBuriedInSand || tableCard.RemoveCardOnCapture)
		{
			continue;
		}

		bool isScoringCardIndex = IsCapturedTableCardScoringIndex(
				tableCardIndex,
				captureStartIndex)
			|| isExtraCapturedIndex
			|| (includeCaptureStartScoring && tableCardIndex == captureStartIndex)
			|| (includeCaptureEndScoring && tableCardIndex == captureEndScoringIndex);
		if (scoringCardsOnly
			&& (!isScoringCardIndex
				|| !tableCard.Card.HasValue
				|| IsSeaSlugSlimeScoreSuppressed(tableCard, scorePlayerIndex)))
		{
			continue;
		}

		bool isQuestionCard = IsQuestionTableCard(tableCard);
		if (!tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue)
		{
			continue;
		}

		if (isQuestionCard && !includeQuestionCard && !tableCard.Card.HasValue)
		{
			continue;
		}

		Texture2D? startTexture = null;
		if (tableCard.Card.HasValue)
		{
			startTexture = tableCard.IsFaceUp || _cardBackTexture == null
				? tableCard.Card.Value.Texture
				: _cardBackTexture;
		}
		else if (tableCard.ItemOverlay.HasValue)
		{
			startTexture = !tableCard.IsFaceUp && _cardBackTexture != null
				? _cardBackTexture
				: tableCard.ItemOverlay.Value.Texture;
		}

		if (startTexture == null)
		{
			continue;
		}

		Texture2D? itemOverlayTexture = null;
		if (tableCard.Card.HasValue && tableCard.IsSlimeCoated)
		{
			itemOverlayTexture = _slimeCardOverlayTexture;
		}
		else if (tableCard.Card.HasValue
			&& tableCard.ItemOverlay.HasValue
			&& (includeQuestionCard || !isQuestionCard))
		{
			itemOverlayTexture = tableCard.ItemOverlay.Value.Texture;
		}
		else if (tableCard.Card.HasValue && tableCard.IsFaceUp)
		{
			itemOverlayTexture = GetCardAttachmentOverlayTexture(tableCard.Card.Value);
		}

		visuals.Add(new CapturedTableCardVisual(
			tableCardIndex,
			startTexture,
			itemOverlayTexture,
			flightStartOverrides != null
				&& flightStartOverrides.TryGetFlightStartPositionOverride(
					tableCardIndex,
					out Vector2 overrideGlobalPosition)
					? overrideGlobalPosition
					: _centerArena.GlobalPosition
						+ GetTableCardPosition(tableCardIndex, tableLayoutRect),
			tableCardIndex * 2));
	}

	return visuals;
}
}
