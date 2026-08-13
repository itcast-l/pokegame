using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolveMagnetTableCardTriggerAsync(
		int tableCardIndex,
		int captureStartIndex,
		int capturingPlayerIndex,
		int scorePlayerIndex,
		int turnSequenceGeneration,
		HashSet<int> resolvedMagnetTriggerIndexes,
		HashSet<int> magnetExtraCaptureIndexes,
		HashSet<int> resolvedDoubleTriggerIndexes,
		HashSet<int> resolvedOvertimeTriggerIndexes)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
		{
			return;
		}

		if (!resolvedMagnetTriggerIndexes.Add(tableCardIndex))
		{
			return;
		}

		await PlayMagnetTableCardTriggerEffectAsync(tableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await RevealRandomMagnetExtraTableCardAsync(
			captureStartIndex,
			capturingPlayerIndex,
			scorePlayerIndex,
			turnSequenceGeneration,
			resolvedMagnetTriggerIndexes,
			magnetExtraCaptureIndexes,
			resolvedDoubleTriggerIndexes,
			resolvedOvertimeTriggerIndexes);
	}

	private async Task PlayMagnetTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (_networkAuthorityRuleTransactionActive
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
		{
			return;
		}

		Texture2D triggerTexture = GetAttachedTriggerTexture(tableCard.Card.Value, CardEffectTypes.MagnetExtraCapture);
		PlayGameSfx(GameSfx.MagnetExtraCapture, -1.0f, 0.97f, 1.03f, 0.08);
		await PlaySpecialCardTriggerEffectAsync(
			triggerTexture,
			_centerArena.GlobalPosition + cardPosition,
			tableCardIndex * 2 + 1);
	}

	private async Task ResolveDoubleTableCardTriggerAsync(
		int tableCardIndex,
		int turnSequenceGeneration,
		HashSet<int>? resolvedDoubleTriggerIndexes)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
		{
			return;
		}

		if (resolvedDoubleTriggerIndexes != null && !resolvedDoubleTriggerIndexes.Add(tableCardIndex))
		{
			return;
		}

		await PlayDoubleTableCardTriggerEffectAsync(tableCardIndex, turnSequenceGeneration);
	}

	private async Task PlayDoubleTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (_networkAuthorityRuleTransactionActive
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
		{
			return;
		}

		Texture2D triggerTexture = GetAttachedTriggerTexture(tableCard.Card.Value, CardEffectTypes.DoubleCollect);
		PlayGameSfx(GameSfx.DoubleCollect, -1.0f, 0.97f, 1.04f, 0.08);
		await PlaySpecialCardTriggerEffectAsync(
			triggerTexture,
			_centerArena.GlobalPosition + cardPosition,
			tableCardIndex * 2 + 1);
	}

	private async Task ResolveOvertimeTableCardTriggerAsync(
		int tableCardIndex,
		int turnSequenceGeneration,
		HashSet<int>? resolvedOvertimeTriggerIndexes)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.ExtraTurn))
		{
			return;
		}

		if (resolvedOvertimeTriggerIndexes != null && !resolvedOvertimeTriggerIndexes.Add(tableCardIndex))
		{
			return;
		}

		await PlayOvertimeTableCardTriggerEffectAsync(tableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		if (tableCard.Card.HasValue
			&& TryGetAttachedItemIdByEffectType(tableCard.Card.Value, CardEffectTypes.ExtraTurn, out string itemCardId))
		{
			if (!TrySubmitRemoveTableCardAttachmentRuleCommand(
				_currentTurnPlayerIndex,
				tableCardIndex,
				tableCard.InstanceId,
				itemCardId,
				turnSequenceGeneration,
				out _))
			{
				return;
			}
		}
		RenderTableCards();
	}

	private async Task PlayOvertimeTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (_networkAuthorityRuleTransactionActive
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.ExtraTurn))
		{
			return;
		}

		Texture2D triggerTexture = GetAttachedTriggerTexture(tableCard.Card.Value, CardEffectTypes.ExtraTurn);
		PlayGameSfx(GameSfx.ExtraTurn, -1.0f, 0.98f, 1.02f, 0.08);
		await PlaySpecialCardTriggerEffectAsync(
			triggerTexture,
			_centerArena.GlobalPosition + cardPosition,
			tableCardIndex * 2 + 1);
	}

	private Texture2D GetAttachedTriggerTexture(PlayingCard card, string effectType)
	{
		return TryGetAttachedItemIdByEffectType(card, effectType, out string itemCardId)
			&& _itemCardTextures.TryGetValue(itemCardId, out Texture2D? itemTexture)
				? itemTexture
				: GetPlayingCardTexture(card);
	}

	private async Task RevealRandomMagnetExtraTableCardAsync(
		int captureStartIndex,
		int capturingPlayerIndex,
		int scorePlayerIndex,
		int turnSequenceGeneration,
		HashSet<int> resolvedMagnetTriggerIndexes,
		HashSet<int> magnetExtraCaptureIndexes,
		HashSet<int> resolvedDoubleTriggerIndexes,
		HashSet<int> resolvedOvertimeTriggerIndexes)
	{
		int tableCardIndex = PickRandomMagnetExtraRevealIndex(
			capturingPlayerIndex,
			captureStartIndex,
			magnetExtraCaptureIndexes,
			turnSequenceGeneration);
		if (tableCardIndex < 0)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || tableCard.IsFaceUp)
		{
			return;
		}

		Texture2D revealTexture = GetPlayingCardTexture(tableCard.Card.Value);
		await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		if (!TrySubmitSetTableCardsFaceUpRuleCommand(
			capturingPlayerIndex,
			ImmutableArray.Create(new RuleTableCardReference(tableCardIndex, tableCard.InstanceId)),
			expectedFaceUp: false,
			faceUp: true,
			turnSequenceGeneration,
			out _))
		{
			return;
		}
		magnetExtraCaptureIndexes.Add(tableCardIndex);
		RenderTableCards();
		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
		int popupPoints = GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
		TrackBoardTask(PlayTableCardPointPopupAsync(tableCardIndex, popupPoints, hasDoubleEffect), nameof(PlayTableCardPointPopupAsync));
		if (IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
		{
			await ResolveMagnetTableCardTriggerAsync(
				tableCardIndex,
				captureStartIndex,
				capturingPlayerIndex,
				scorePlayerIndex,
				turnSequenceGeneration,
				resolvedMagnetTriggerIndexes,
				magnetExtraCaptureIndexes,
				resolvedDoubleTriggerIndexes,
				resolvedOvertimeTriggerIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		if (IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.DoubleCollect))
		{
			await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		if (IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.ExtraTurn))
		{
			await ResolveOvertimeTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		await WaitForBoardDelayAsync(TableCardRevealPauseSeconds);
	}

	private int PickRandomMagnetExtraRevealIndex(
		int actingPlayerIndex,
		int captureStartIndex,
		HashSet<int> magnetExtraCaptureIndexes,
		int turnSequenceGeneration)
	{
		var candidates = ImmutableArray.CreateBuilder<RuleTableCardReference>();
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (tableCardIndex >= captureStartIndex)
			{
				continue;
			}

			if (magnetExtraCaptureIndexes.Contains(tableCardIndex))
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (!tableCard.IsBuriedInSand && !tableCard.IsFaceUp && tableCard.Card.HasValue)
			{
				candidates.Add(new RuleTableCardReference(tableCardIndex, tableCard.InstanceId));
			}
		}

		return TrySubmitChooseRandomTableCardIndexRuleCommand(
			actingPlayerIndex,
			candidates.ToImmutable(),
			turnSequenceGeneration,
			out int selectedTableCardIndex)
				? selectedTableCardIndex
				: -1;
	}

	private void BuildCapturedSpecialCardTriggers(int captureStartIndex, HashSet<int>? extraTableCardIndexes, List<TableSpecialCardTrigger> triggers)
	{
		triggers.Clear();
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		Rect2 tableLayoutRect = GetAuthorityRuleSafeTableLayoutRect();
		Vector2 centerArenaGlobalPosition = GetAuthorityRuleSafeCenterArenaGlobalPosition();
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			AddCapturedSpecialCardTrigger(
				tableCardIndex,
				tableLayoutRect,
				centerArenaGlobalPosition,
				triggers);
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				AddCapturedSpecialCardTrigger(
					tableCardIndex,
					tableLayoutRect,
					centerArenaGlobalPosition,
					triggers);
			}
		}

		triggers.Sort(TableSpecialCardTriggerComparer.Instance);
	}

	private void AddCapturedSpecialCardTrigger(
		int tableCardIndex,
		Rect2 tableLayoutRect,
		Vector2 centerArenaGlobalPosition,
		List<TableSpecialCardTrigger> triggers)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (tableCard.IsBuriedInSand || !tableCard.ItemOverlay.HasValue)
		{
			return;
		}

		if (IsQuestionItemCard(tableCard.ItemOverlay.Value.Id))
		{
			return;
		}

		triggers.Add(new TableSpecialCardTrigger(
			tableCardIndex,
			GetItemCardTexture(tableCard.ItemOverlay.Value),
			centerArenaGlobalPosition + GetTableCardPosition(tableCardIndex, tableLayoutRect) + GetItemCardVisualPosition(tableCard.ItemOverlay.Value.Id),
			tableCardIndex * 2 + 1,
			GetTableTriggerPriority(tableCardIndex)));
	}

	private TableSpecialCardTrigger? GetTableSpecialCardTrigger(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return null;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return null;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.ItemOverlay.HasValue)
		{
			return null;
		}

		ItemCard itemOverlay = tableCard.ItemOverlay.Value;
		if (IsQuestionItemCard(itemOverlay.Id))
		{
			return null;
		}

		return new TableSpecialCardTrigger(
			tableCardIndex,
			GetItemCardTexture(itemOverlay),
			_centerArena.GlobalPosition + cardPosition + GetItemCardVisualPosition(itemOverlay.Id),
			tableCardIndex * 2 + 1,
			GetTableTriggerPriority(tableCardIndex));
	}

	private async Task PlayTableSpecialCardTriggerEffectAsync(TableSpecialCardTrigger trigger)
	{
		if (_networkAuthorityRuleTransactionActive
			|| trigger.TableCardIndex < 0
			|| trigger.TableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[trigger.TableCardIndex];
		bool presentationHideAdded =
			_presentationHiddenTableCardRuntimeInstanceIds.Add(tableCard.InstanceId);
		RenderTableCards();
		try
		{
			await PlaySpecialCardTriggerEffectAsync(trigger.Texture, trigger.GlobalPosition, trigger.ZIndex);
		}
		finally
		{
			if (presentationHideAdded)
			{
				_presentationHiddenTableCardRuntimeInstanceIds.Remove(tableCard.InstanceId);
				RenderTableCards();
			}
		}
	}

	private int GetTableTriggerPriority(int tableCardIndex)
	{
		return TryGetTableCardTriggerDefinition(tableCardIndex, out ItemCardDefinition definition) ? definition.Priority : 0;
	}

	private sealed class TableSpecialCardTriggerComparer : IComparer<TableSpecialCardTrigger>
	{
		public static readonly TableSpecialCardTriggerComparer Instance = new();

		public int Compare(TableSpecialCardTrigger a, TableSpecialCardTrigger b)
		{
			int priorityComparison = b.Priority.CompareTo(a.Priority);
			return priorityComparison != 0 ? priorityComparison : b.TableCardIndex.CompareTo(a.TableCardIndex);
		}
	}

}
