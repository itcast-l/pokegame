using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private bool HasCapturedEndpointCrownEffect(int captureStartIndex)
	{
		return HasCapturedAttachedEffectType(captureStartIndex, CardEffectTypes.EndpointCrown);
	}

	private bool HasCapturedMidTideUpgradeEffect(int captureStartIndex)
	{
		return HasCapturedAttachedEffectType(captureStartIndex, CardEffectTypes.MidTideUpgrade);
	}

	private bool HasCapturedAttachedEffectType(int captureStartIndex, string effectType)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.Card.HasValue || tableCard.IsBuriedInSand)
			{
				continue;
			}

			if (IsAttachedEffectType(tableCard.Card.Value, effectType))
			{
				return true;
			}
		}

		return false;
	}

	private int GetCaptureStartScoreBonusPoints(int captureStartIndex, int scorePlayerIndex, bool hasDoubleEffect)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		if (!HasCapturedEndpointCrownEffect(captureStartIndex))
		{
			return 0;
		}

		TableCard captureStartCard = _tableCards[captureStartIndex];
		if (!captureStartCard.Card.HasValue)
		{
			return 0;
		}

		return GetCapturedCardScoreValueForPlayer(captureStartCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private int GetCaptureEndScoreBonusPoints(int captureStartIndex, int scorePlayerIndex, bool hasDoubleEffect)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return 0;
		}

		if (!HasCapturedMidTideUpgradeEffect(captureStartIndex))
		{
			return 0;
		}

		int tailTableCardIndex = GetCaptureTailTableCardIndex(captureStartIndex);
		if (tailTableCardIndex < 0 || tailTableCardIndex >= _tableCards.Count)
		{
			return 0;
		}

		if (!CaptureScoringRules.HasCapturedMiddleTableCards(ToCaptureScoringTableCards(), captureStartIndex))
		{
			return 0;
		}

		TableCard tailCard = _tableCards[tailTableCardIndex];
		if (!tailCard.Card.HasValue)
		{
			return 0;
		}

		return GetCapturedCardScoreValueForPlayer(tailCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private int GetCaptureTailTableCardIndex(int captureStartIndex)
	{
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private async Task<int> TryResolveReverseAnchorCaptureStartAsync(
		int captureStartIndex,
		int scorePlayerIndex,
		int turnSequenceGeneration)
	{
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return captureStartIndex;
		}

		int tailTableCardIndex = GetCaptureTailTableCardIndex(captureStartIndex);
		if (tailTableCardIndex < 0 || tailTableCardIndex >= _tableCards.Count)
		{
			return captureStartIndex;
		}

		TableCard tailTableCard = _tableCards[tailTableCardIndex];
		if (!tailTableCard.Card.HasValue
			|| !IsAttachedEffectType(tailTableCard.Card.Value, CardEffectTypes.ReverseAnchor)
			|| tailTableCardIndex != _tableCards.Count - 1)
		{
			return captureStartIndex;
		}

		if (captureStartIndex <= 0)
		{
			return captureStartIndex;
		}

		int newCaptureStartIndex = captureStartIndex - 1;
		if (!_tableCards[newCaptureStartIndex].Card.HasValue)
		{
			return captureStartIndex;
		}

		PlayGameSfx(GameSfx.EnchantTargetSelect, -1.5f, 0.97f, 1.04f, 0.08);
		await PlaySpecialCardTriggerEffectAsync(
			GetAttachedTriggerTexture(tailTableCard.Card.Value, CardEffectTypes.ReverseAnchor),
			_centerArena.GlobalPosition + GetTableCardPosition(tailTableCardIndex, GetTableCardLayoutRect()),
			tailTableCardIndex * 2 + 1);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return captureStartIndex;
		}

		return newCaptureStartIndex;
	}

	private async Task<bool> TryResolveEmptyTideContinuationAfterMissAsync(int playedTableCardIndex, int turnSequenceGeneration)
	{
		if (playedTableCardIndex < 0 || playedTableCardIndex >= _tableCards.Count)
		{
			return false;
		}

		TableCard playedTableCard = _tableCards[playedTableCardIndex];
		if (!playedTableCard.Card.HasValue
			|| !IsAttachedEffectType(playedTableCard.Card.Value, CardEffectTypes.EmptyTideContinuation)
			|| _tableCards.Count < 4)
		{
			return false;
		}

		int revealTableCardIndex = -1;
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (!_tableCards[tableCardIndex].IsFaceUp)
			{
				revealTableCardIndex = tableCardIndex;
				break;
			}
		}

		if (revealTableCardIndex < 0)
		{
			revealTableCardIndex = 0;
		}

		TableCard revealTableCard = _tableCards[revealTableCardIndex];
		Texture2D? revealTexture = revealTableCard.Card?.Texture ?? revealTableCard.ItemOverlay?.Texture;
		if (revealTexture == null)
		{
			return false;
		}

		PlayGameSfx(GameSfx.EnchantTargetSelect, -1.5f, 0.97f, 1.04f, 0.08);
		await PlayTableCardRevealAnimationAsync(revealTableCardIndex, revealTexture);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return false;
		}

		revealTableCard.IsFaceUp = true;
		RenderTableCards();
		return true;
	}
}
