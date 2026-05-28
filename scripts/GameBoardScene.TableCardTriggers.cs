using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolveMagnetTableCardTriggerAsync(
		int tableCardIndex,
		int captureStartIndex,
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
			scorePlayerIndex,
			turnSequenceGeneration,
			resolvedMagnetTriggerIndexes,
			magnetExtraCaptureIndexes,
			resolvedDoubleTriggerIndexes,
			resolvedOvertimeTriggerIndexes);
	}

	private async Task PlayMagnetTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
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
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
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
			tableCard.ClearAttachedItemEffect(itemCardId);
		}
		RenderTableCards();
	}

	private async Task PlayOvertimeTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
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
				: card.Texture;
	}

	private async Task RevealRandomMagnetExtraTableCardAsync(
		int captureStartIndex,
		int scorePlayerIndex,
		int turnSequenceGeneration,
		HashSet<int> resolvedMagnetTriggerIndexes,
		HashSet<int> magnetExtraCaptureIndexes,
		HashSet<int> resolvedDoubleTriggerIndexes,
		HashSet<int> resolvedOvertimeTriggerIndexes)
	{
		int tableCardIndex = PickRandomMagnetExtraRevealIndex(captureStartIndex, magnetExtraCaptureIndexes);
		if (tableCardIndex < 0)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || tableCard.IsFaceUp)
		{
			return;
		}

		Texture2D revealTexture = tableCard.Card.Value.Texture;
		await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		tableCard.IsFaceUp = true;
		magnetExtraCaptureIndexes.Add(tableCardIndex);
		RenderTableCards();
		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
		int popupPoints = GetCapturedCardScoreValueForPlayer(tableCard.Card.Value, scorePlayerIndex, hasDoubleEffect);
		_ = PlayTableCardPointPopupAsync(tableCardIndex, popupPoints, hasDoubleEffect);
		if (IsAttachedEffectType(tableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
		{
			await ResolveMagnetTableCardTriggerAsync(
				tableCardIndex,
				captureStartIndex,
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

		await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	private int PickRandomMagnetExtraRevealIndex(int captureStartIndex, HashSet<int> magnetExtraCaptureIndexes)
	{
		int selectedTableCardIndex = -1;
		int candidateCount = 0;
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
				candidateCount++;
				if (_rng.RandiRange(1, candidateCount) == 1)
				{
					selectedTableCardIndex = tableCardIndex;
				}
			}
		}

		return selectedTableCardIndex;
	}

	private void BuildCapturedSpecialCardTriggers(int captureStartIndex, HashSet<int>? extraTableCardIndexes, List<TableSpecialCardTrigger> triggers)
	{
		triggers.Clear();
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			AddCapturedSpecialCardTrigger(tableCardIndex, tableLayoutRect, triggers);
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				AddCapturedSpecialCardTrigger(tableCardIndex, tableLayoutRect, triggers);
			}
		}

		triggers.Sort(TableSpecialCardTriggerComparer.Instance);
	}

	private void AddCapturedSpecialCardTrigger(int tableCardIndex, Rect2 tableLayoutRect, List<TableSpecialCardTrigger> triggers)
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
			tableCard.ItemOverlay.Value.Texture,
			_centerArena.GlobalPosition + GetTableCardPosition(tableCardIndex, tableLayoutRect) + GetItemCardVisualPosition(tableCard.ItemOverlay.Value.Id),
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
			itemOverlay.Texture,
			_centerArena.GlobalPosition + cardPosition + GetItemCardVisualPosition(itemOverlay.Id),
			tableCardIndex * 2 + 1,
			GetTableTriggerPriority(tableCardIndex));
	}

	private async Task PlayTableSpecialCardTriggerEffectAsync(TableSpecialCardTrigger trigger)
	{
		if (trigger.TableCardIndex < 0 || trigger.TableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[trigger.TableCardIndex];
		bool wasHidden = tableCard.IsHiddenDuringCapture;
		tableCard.IsHiddenDuringCapture = true;
		RenderTableCards();
		try
		{
			await PlaySpecialCardTriggerEffectAsync(trigger.Texture, trigger.GlobalPosition, trigger.ZIndex);
		}
		finally
		{
			if (trigger.TableCardIndex >= 0 && trigger.TableCardIndex < _tableCards.Count)
			{
				_tableCards[trigger.TableCardIndex].IsHiddenDuringCapture = wasHidden;
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

	private async Task ApplyJellyfishBubbleAfterCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.BubbleCover)
			|| playerIndex != 1
			|| _jellyfishBubbleTriggeredThisTurn
			|| _jellyfishDisabledMainPlayerHandSlotIndex >= 0)
		{
			return;
		}

		int handSlotIndex = PickVisibleMainPlayerHandSlotIndex();
		if (handSlotIndex < 0)
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.BubbleCover);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		_jellyfishBubbleTriggeredThisTurn = true;
		_jellyfishDisabledMainPlayerHandSlotIndex = handSlotIndex;
		RenderPlayerHands();
	}

	private bool ShouldApplyGardenEelSandBite(int playerIndex, int captureStartIndex, bool playedRankCollectsTable)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.SandAmbush)
			&& playerIndex == 1
			&& _gardenEelSandBiteReady
			&& !playedRankCollectsTable
			&& captureStartIndex >= 0
			&& captureStartIndex < _tableCards.Count
			&& !_tableCards[captureStartIndex].IsBuriedInSand
			&& _tableCards[captureStartIndex].Card.HasValue;
	}

	private int ConsumeGardenEelSandBiteBonusPoints(
		int playerIndex,
		int captureStartIndex,
		int scorePlayerIndex,
		bool hasDoubleEffect,
		bool playedRankCollectsTable)
	{
		if (!ShouldApplyGardenEelSandBite(playerIndex, captureStartIndex, playedRankCollectsTable))
		{
			return 0;
		}

		_gardenEelSandBiteReady = false;
		_gardenEelCaptureCountUntilSandBite = 0;
		return GetHalfCapturedCardScoreValueForPlayer(_tableCards[captureStartIndex].Card!.Value, scorePlayerIndex, hasDoubleEffect);
	}

	private void RecordGardenEelCaptureForSandBite(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.SandAmbush)
			|| playerIndex != 1
			|| _gardenEelSandBiteReady)
		{
			return;
		}

		_gardenEelCaptureCountUntilSandBite++;
		if (_gardenEelCaptureCountUntilSandBite >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeSandBite, 2))
		{
			_gardenEelCaptureCountUntilSandBite = 0;
			_gardenEelSandBiteReady = true;
		}
	}

	private async Task ApplySeaSlugSlimeCoatAfterMissAsync(int playerIndex, int playedTableCardIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.ColorCurrent)
			|| playerIndex != 1
			|| playedTableCardIndex < 0
			|| playedTableCardIndex >= _tableCards.Count
			|| !_tableCards[playedTableCardIndex].Card.HasValue
			|| HasSeaSlugSlimeCoatedTableCard())
		{
			return;
		}

		_seaSlugMissCountUntilSlime++;
		if (_seaSlugMissCountUntilSlime < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.MissesBeforeSlimeCoat, 2))
		{
			return;
		}

		_seaSlugMissCountUntilSlime = 0;
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.ColorCurrent);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		if (playedTableCardIndex < 0
			|| playedTableCardIndex >= _tableCards.Count
			|| !_tableCards[playedTableCardIndex].Card.HasValue
			|| HasSeaSlugSlimeCoatedTableCard())
		{
			return;
		}

		_tableCards[playedTableCardIndex].IsSlimeCoated = true;
		RenderTableCards();
	}

	private bool HasSeaSlugSlimeCoatedTableCard()
	{
		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.IsSlimeCoated)
			{
				return true;
			}
		}

		return false;
	}

	private async Task ApplyNautilusBombAfterMainPlayerCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.SpiralShell)
			|| playerIndex != MainPlayerIndex)
		{
			return;
		}

		_nautilusMainPlayerCaptureCountUntilBomb++;
		if (_nautilusMainPlayerCaptureCountUntilBomb < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeBomb, 3))
		{
			return;
		}

		if (_tableCards.Count < 2)
		{
			return;
		}

			string bombItemCardId = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BombItemCardId, BombItemCardId);
			if (!IsItemEffectType(bombItemCardId, CardEffectTypes.PlaceTablePairDestroy))
			{
				return;
			}

			ItemCard? bombCard = CreateItemCard(bombItemCardId);
			if (!bombCard.HasValue)
			{
				return;
			}

		int insertionIndex = PickNautilusBombInsertionIndex();
		if (insertionIndex < 0)
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(1, turnSequenceGeneration, AdventureEnemyAbilities.SpiralShell);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		InsertBombCardIntoTable(bombCard.Value, 1, insertionIndex);
		_nautilusMainPlayerCaptureCountUntilBomb = 0;
		RenderTableCards();
		await ResolveBombPairAsync(turnSequenceGeneration);
		RenderTableCards();
	}

	private void RecordNarwhalCaptureForDouble(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.TuskBreak)
			|| playerIndex != 1
			|| _narwhalDoubleReady)
		{
			return;
		}

		_narwhalCaptureCountUntilDouble++;
		if (_narwhalCaptureCountUntilDouble >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeDouble, 2))
		{
			_narwhalCaptureCountUntilDouble = 0;
			_narwhalDoubleReady = true;
		}
	}

	private async Task ResolveAdventureEnemyTurnEndAbilityAsync(int playerIndex, PlayCardResult result, int turnSequenceGeneration)
	{
		await ResolveLeafySeadragonBladeBreakAsync(playerIndex, result, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> ResolveDragonPrinceJackTurnStartAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!ShouldResolveDragonPrinceJack(playerIndex))
		{
			return new PlayCardResult(false, false);
		}

		_dragonPrinceJackUsed = true;
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.DragonPrince);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new PlayCardResult(false, false);
		}

		int specialJackTableIndex = await PlayDragonPrinceSpecialJackToTableAsync(playerIndex, turnSequenceGeneration);
		if (specialJackTableIndex < 0)
		{
			return new PlayCardResult(false, false);
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = await ResolveDragonPrinceJackCollectTableAsync(playerIndex, turnSequenceGeneration, specialJackTableIndex);
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return result with { CompletesTurn = true };
	}

	private async Task<int> PlayDragonPrinceSpecialJackToTableAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (_dragonPrinceSpecialJackTexture == null || string.IsNullOrWhiteSpace(_dragonPrinceSpecialJackRank))
		{
			return -1;
		}

		Vector2 skillGlobalPosition = GetDragonPrinceSpecialJackSkillGlobalPosition();
		await PlayDragonPrinceSpecialJackSkillSpawnAsync(skillGlobalPosition);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return -1;
		}

		PlayingCard specialJack = new(string.Empty, _dragonPrinceSpecialJackRank, _dragonPrinceSpecialJackTexture);
		_tableCards.Add(new TableCard(specialJack, true, cardOwnerPlayerIndex: playerIndex, removeCardOnCapture: true));
		int tableCardIndex = _tableCards.Count - 1;
		await PlayCardToTableAnimationAsync(
			playerIndex,
			specialJack,
			skillGlobalPosition,
			tableCardIndex,
			true,
			_dragonPrinceSpecialJackTexture);
		RenderTableCards();
		return tableCardIndex;
	}

	private Vector2 GetDragonPrinceSpecialJackSkillGlobalPosition()
	{
		return GetQuestionCardTransformAreaCardGlobalPosition(
			_centerArena.GlobalPosition + (_centerArena.Size - CardVisualSize) / 2.0f);
	}

	private async Task PlayDragonPrinceSpecialJackSkillSpawnAsync(Vector2 skillGlobalPosition)
	{
		if (!IsInsideTree() || _dragonPrinceSpecialJackTexture == null)
		{
			return;
		}

		var spawnCard = new TextureRect
		{
			Name = "DragonPrinceSpecialJackSkillSpawn",
			Texture = _dragonPrinceSpecialJackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + 860,
			ZAsRelative = false,
			Scale = Vector2.One * 0.78f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(spawnCard);
		spawnCard.GlobalPosition = skillGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(spawnCard, "modulate:a", 1.0f, DragonPrinceSpecialJackSpawnSeconds * 0.55f);
			tween.TweenProperty(spawnCard, "scale", Vector2.One * 1.10f, DragonPrinceSpecialJackSpawnSeconds);
			tween.Chain().SetParallel(true);
			tween.TweenProperty(spawnCard, "scale", Vector2.One, DragonPrinceSpecialJackSpawnSettleSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(spawnCard))
			{
				spawnCard.QueueFree();
			}
		}
	}

	private async Task<PlayCardResult> ResolveDragonPrinceJackCollectTableAsync(
		int playerIndex,
		int turnSequenceGeneration,
		int specialJackTableIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		bool captured = _tableCards.Count > 0;
		PairCaptureEdgeFloatHold? specialJackFloat = null;
		bool specialJackFloatHandedToDeck = false;
		if (captured)
		{
			try
			{
				specialJackFloat = await PlayDragonPrinceSpecialJackTableFloatHoldAsync(specialJackTableIndex);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new PlayCardResult(false, false);
				}

				List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
					0,
					false,
					flightStartOverrides: specialJackFloat);
				if (capturedCardVisuals.Count > 0)
				{
					specialJackFloat?.QueueFreeOverlays();
					specialJackFloatHandedToDeck = specialJackFloat != null;
					await PlayCapturedCardsToDeckAnimationAsync(
						playerIndex,
						0,
						capturedCardVisuals,
						turnSequenceGeneration);
				}
			}
			finally
			{
				if (!specialJackFloatHandedToDeck && specialJackFloat != null)
				{
					RestorePairCaptureEdgeFloatHold(specialJackFloat);
				}
			}
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new PlayCardResult(false, false);
		}

		CaptureTableCardsResult captureResult = await CaptureTableCardsAsync(
			playerIndex,
			0,
			turnSequenceGeneration: turnSequenceGeneration);
		if (!IsGameOver())
		{
			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
		}

		return new PlayCardResult(true, captured, captureResult.CollectedCardCount > 0, captureResult.QuestionRewardAnimation);
	}

	private async Task<PairCaptureEdgeFloatHold?> PlayDragonPrinceSpecialJackTableFloatHoldAsync(int tableCardIndex)
	{
		if (!IsInsideTree()
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count
			|| _dragonPrinceSpecialJackTexture == null)
		{
			return null;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return null;
		}

		var overlay = new TextureRect
		{
			Name = "DragonPrinceSpecialJackTableFloat",
			Texture = _dragonPrinceSpecialJackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2 + 260,
			ZAsRelative = false,
		};
		AddChild(overlay);
		overlay.GlobalPosition = _centerArena.GlobalPosition + cardPosition;
		_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
		RenderTableCards();

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(
			overlay,
			"global_position",
			overlay.GlobalPosition + new Vector2(0.0f, -DragonPrinceSpecialJackFloatRise),
			DragonPrinceSpecialJackFloatSeconds);
		tween.TweenProperty(
			overlay,
			"scale",
			Vector2.One * DragonPrinceSpecialJackFloatScale,
			DragonPrinceSpecialJackFloatSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
		return new PairCaptureEdgeFloatHold(tableCardIndex, overlay);
	}

	private bool ShouldResolveDragonPrinceJack(int playerIndex)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonPrince)
			&& playerIndex == 1
			&& !_dragonPrinceJackUsed
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			&& (!IsBossKEnemy(playerIndex) || ShouldResolveBossKDragonPrinceJack(playerIndex));
	}

	private bool ShouldResolveBossKDragonPrinceJack(int playerIndex)
	{
		return BossKCanUseActiveControl(playerIndex)
			&& GetBestPotentialNormalCaptureCount(playerIndex, _tableCards) < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5);
	}

	private async Task ApplyDragonQueenAndKingAfterEnemyCaptureAsync(
		int playerIndex,
		int removedTableCardCount,
		int turnSequenceGeneration)
	{
		if (!adventure_mode || playerIndex != 1)
		{
			return;
		}

		if (!IsBossKEnemy(playerIndex)
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonQueen)
			&& removedTableCardCount >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.DragonQueenCaptureThreshold, 1))
		{
			_dragonQueenCaptureCountUntilSteal++;
			if (_dragonQueenCaptureCountUntilSteal < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeSteal, 3))
			{
				return;
			}

			_dragonQueenCaptureCountUntilSteal = 0;
			await StealRandomMainPlayerHandCardForEnemyAsync(playerIndex, turnSequenceGeneration);
		}
		else if (IsBossKEnemy(playerIndex) && removedTableCardCount > 0)
		{
			_bossKCaptureCountUntilSteal++;
			if (_bossKCaptureCountUntilSteal < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.CapturesBeforeSteal, 3))
			{
				return;
			}

			_bossKCaptureCountUntilSteal = 0;
			await ApplyBossKRandomStealAfterCaptureAsync(playerIndex, turnSequenceGeneration);
		}
	}

	private async Task ApplyBossKRandomStealAfterCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		bool canStealDeck = CanStealMainPlayerTopDeckCard(playerIndex);
		bool canStealHand = CanStealRandomMainPlayerHandCard(playerIndex);
		if (!canStealDeck && !canStealHand)
		{
			return;
		}

		if ((canStealDeck && canStealHand && _rng.RandiRange(0, 1) == 0)
			|| (canStealDeck && !canStealHand))
		{
			await StealMainPlayerTopDeckCardForEnemyAsync(playerIndex, turnSequenceGeneration);
			return;
		}

		await StealRandomMainPlayerHandCardForEnemyAsync(playerIndex, turnSequenceGeneration);
	}

	private bool CanStealRandomMainPlayerHandCard(int enemyPlayerIndex)
	{
		return enemyPlayerIndex >= 0
			&& enemyPlayerIndex < _playerHands.Count
			&& enemyPlayerIndex < _playerItemHands.Count
			&& MainPlayerIndex < _playerHands.Count
			&& MainPlayerIndex < _playerItemHands.Count
			&& GetTotalHandCardCount(MainPlayerIndex) > 0;
	}

	private bool CanStealMainPlayerTopDeckCard(int enemyPlayerIndex)
	{
		return enemyPlayerIndex >= 0
			&& enemyPlayerIndex < _playerDecks.Count
			&& MainPlayerIndex < _playerDecks.Count
			&& _playerDecks[MainPlayerIndex].Count > 0;
	}

	private async Task StealRandomMainPlayerHandCardForEnemyAsync(int enemyPlayerIndex, int turnSequenceGeneration)
	{
		if (enemyPlayerIndex < 0
			|| enemyPlayerIndex >= _playerHands.Count
			|| enemyPlayerIndex >= _playerItemHands.Count
			|| MainPlayerIndex >= _playerHands.Count
			|| MainPlayerIndex >= _playerItemHands.Count)
		{
			return;
		}

		int targetHandCount = GetTotalHandCardCount(MainPlayerIndex);
		if (targetHandCount == 0)
		{
			return;
		}

		int targetHandIndex = _rng.RandiRange(0, targetHandCount - 1);
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(enemyPlayerIndex, turnSequenceGeneration, AdventureEnemyAbilities.DragonQueen);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		PlayCardResult result = await ResolveQueenEffectAsync(enemyPlayerIndex, MainPlayerIndex, targetHandIndex, turnSequenceGeneration);
		if (result.Played)
		{
			RenderPlayerHands();
		}
	}

	private async Task StealMainPlayerTopDeckCardForEnemyAsync(int enemyPlayerIndex, int turnSequenceGeneration)
	{
		if (enemyPlayerIndex < 0
			|| enemyPlayerIndex >= _playerDecks.Count
			|| MainPlayerIndex >= _playerDecks.Count
			|| _playerDecks[MainPlayerIndex].Count == 0)
		{
			return;
		}

		Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(MainPlayerIndex);
		Vector2 targetGlobalPosition = GetDrawCardStartGlobalPosition(enemyPlayerIndex);
		await PlayBossKRoyalSeizureLanceAttackAsync(
			enemyPlayerIndex,
			GetPlayerCardPileGlobalCenter(MainPlayerIndex),
			turnSequenceGeneration,
			null);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		await PlayStolenDeckCardFlightAsync(
			startGlobalPosition,
			targetGlobalPosition,
			GetCardPileRotationDegreesForPlayer(MainPlayerIndex),
			GetCardPileRotationDegreesForPlayer(enemyPlayerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return;
		}

		PlayingCard stolenCard = _playerDecks[MainPlayerIndex][0];
		_playerDecks[MainPlayerIndex].RemoveAt(0);
		AddCardToPlayerDeck(enemyPlayerIndex, stolenCard);
		PlayPlayerAvatarAction(MainPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerCardPiles();
	}

	private async Task ResolveLeafySeadragonBladeBreakAsync(
		int playerIndex,
		PlayCardResult result,
		int turnSequenceGeneration)
	{
		if (!ShouldResolveLeafySeadragonBladeBreak(playerIndex, result))
		{
			UpdateLeafySeadragonBladeBreakCooldown(playerIndex);
			return;
		}

		int targetTableCardIndex = PickLeafySeadragonBladeBreakTargetIndex();
		if (targetTableCardIndex < 0)
		{
			UpdateLeafySeadragonBladeBreakCooldown(playerIndex);
			return;
		}

		if (IsBossKEnemy(playerIndex))
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.LeafyMirage);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}

		await PlayLeafySeadragonBladeProjectilesAsync(playerIndex, targetTableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || targetTableCardIndex >= _tableCards.Count)
		{
			return;
		}

		_tableCards[targetTableCardIndex].IsHiddenDuringCapture = true;
		RenderTableCards();
		if (!TryGetTableCardPosition(targetTableCardIndex, out Vector2 targetCardPosition))
		{
			return;
		}

		await PlayBombPairDestroyCardAsync(targetTableCardIndex, targetCardPosition);
		if (IsGameOver() || targetTableCardIndex >= _tableCards.Count)
		{
			return;
		}

		PlayingCard destroyedCard = _tableCards[targetTableCardIndex].Card!.Value;
		int damagePoints = Mathf.CeilToInt(GetCardScoreValue(destroyedCard) / 2.0f);
		_tableCards.RemoveAt(targetTableCardIndex);
		RefreshGardenEelBuriedCardIndex();
		_leafySeadragonBladeBreakCooldownTurns = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BladeBreakCooldownTurns, 1);
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderTableCards();

		if (damagePoints <= 0)
		{
			return;
		}

		int damageTargetPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
		await PlayAdventureAvatarAttackAsync(
			playerIndex,
			damageTargetPlayerIndex,
			turnSequenceGeneration,
			() => AddPlayerScore(playerIndex, damagePoints));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		CheckForWinner();
	}

	private bool ShouldResolveLeafySeadragonBladeBreak(int playerIndex, PlayCardResult result)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.LeafyMirage)
			&& playerIndex == 1
			&& !result.Captured
			&& _leafySeadragonBladeBreakCooldownTurns <= 0
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.BladeBreakTableCardThreshold, 5)
			&& PickLeafySeadragonBladeBreakTargetIndex() >= 0
			&& (!IsBossKEnemy(playerIndex) || BossKCanUseActiveControl(playerIndex));
	}

	private void UpdateLeafySeadragonBladeBreakCooldown(int playerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.LeafyMirage)
			|| playerIndex != 1
			|| _leafySeadragonBladeBreakCooldownTurns <= 0)
		{
			return;
		}

		_leafySeadragonBladeBreakCooldownTurns--;
	}

	private int PickLeafySeadragonBladeBreakTargetIndex()
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.Card.HasValue && !IsFixedDuringTableShuffle(tableCard))
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private PlayingCard PrepareAdventureEnemyPlayedCard(int playerIndex, PlayingCard card)
	{
		if (!ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card))
		{
			return card;
		}

			string doubleItemCardId = GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.DoubleItemCardId, DoubleItemCardId);
			if (!IsItemAttachedEffectType(doubleItemCardId, CardEffectTypes.DoubleCollect))
			{
				return card;
			}

			_narwhalDoubleReady = false;
			return card.WithAttachedItemEffect(doubleItemCardId);
		}

	private bool ShouldApplyNarwhalDoubleToPlayedCard(int playerIndex, PlayingCard card)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TuskBreak)
			&& playerIndex == 1
			&& _narwhalDoubleReady
			&& !card.HasItemEffect
			&& !IsExcludedDeckRank(card.Rank)
			&& (!IsBossKEnemy(playerIndex) || FindMatchingTableCardIndex(card) >= 0);
	}

	private bool IsItemAttachedEffectType(string itemCardId, string effectType)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& string.Equals(definition.AttachedEffectType, effectType, StringComparison.Ordinal);
	}

	private int PickNautilusBombInsertionIndex()
	{
		if (_tableCards.Count < 2)
		{
			return -1;
		}

		if (_tableCards.Count == 2)
		{
			return 1;
		}

		return _rng.RandiRange(1, _tableCards.Count - 1);
	}

	private bool IsBossKEnemy(int playerIndex)
	{
		return adventure_mode
			&& playerIndex == 1
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonKing);
	}

	private bool BossKCanUseActiveControl(int playerIndex)
	{
		return IsBossKEnemy(playerIndex) && !_bossKActiveControlUsedThisTurn;
	}

	private void MarkBossKActiveControlUsedIfNeeded(int playerIndex)
	{
		if (IsBossKEnemy(playerIndex))
		{
			_bossKActiveControlUsedThisTurn = true;
		}
	}

	private void RefreshGardenEelBuriedCardIndex()
	{
		_gardenEelBuriedTableCardIndex = -1;
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (_tableCards[tableCardIndex].IsBuriedInSand)
			{
				_gardenEelBuriedTableCardIndex = tableCardIndex;
				return;
			}
		}
	}

	private int PickVisibleMainPlayerHandSlotIndex()
	{
		int totalHandCardCount = GetTotalHandCardCount(MainPlayerIndex);
		int selectedDisplayIndex = -1;
		int candidateCount = 0;
		for (int displayIndex = 0; displayIndex < totalHandCardCount; displayIndex++)
		{
			if (!IsHandDisplaySlotHidden(MainPlayerIndex, displayIndex))
			{
				candidateCount++;
				if (_rng.RandiRange(1, candidateCount) == 1)
				{
					selectedDisplayIndex = displayIndex;
				}
			}
		}

		return candidateCount <= 1 ? -1 : selectedDisplayIndex;
	}

	private void ClearJellyfishBubbleIfMainPlayerTurnCompleted(int playerIndex, PlayCardResult result)
	{
		if (playerIndex != MainPlayerIndex
			|| result.Captured
			|| _jellyfishDisabledMainPlayerHandSlotIndex < 0)
		{
			return;
		}

		_jellyfishDisabledMainPlayerHandSlotIndex = -1;
		RenderPlayerHands();
	}

	private void ResetJellyfishBubbleTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.BubbleCover)
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		_jellyfishBubbleTriggeredThisTurn = false;
	}

	private void ShiftHandDisplayStateAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (playerIndex < 0 || removedDisplayIndex < 0)
		{
			return;
		}

		ShiftHiddenHandDisplaySlotsAfterRemovedCard(playerIndex, removedDisplayIndex);
		ShiftJellyfishDisabledHandSlotAfterRemovedCard(playerIndex, removedDisplayIndex);
	}

	private void ShiftHiddenHandDisplaySlotsAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (_hiddenHandDisplaySlots.Count == 0)
		{
			return;
		}

		_shiftedHiddenHandDisplaySlots.Clear();
		bool changed = false;
		foreach (string key in _hiddenHandDisplaySlots)
		{
			if (!TryParseHandDisplaySlotKey(key, out int keyPlayerIndex, out int displayIndex)
				|| keyPlayerIndex != playerIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(key);
				continue;
			}

			changed = true;
			if (displayIndex < removedDisplayIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(key);
			}
			else if (displayIndex > removedDisplayIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(BuildHandDisplaySlotKey(playerIndex, displayIndex - 1));
			}
		}

		if (!changed)
		{
			_shiftedHiddenHandDisplaySlots.Clear();
			return;
		}

		_hiddenHandDisplaySlots.Clear();
		foreach (string key in _shiftedHiddenHandDisplaySlots)
		{
			_hiddenHandDisplaySlots.Add(key);
		}
		_shiftedHiddenHandDisplaySlots.Clear();
	}

	private static bool TryParseHandDisplaySlotKey(string key, out int playerIndex, out int displayIndex)
	{
		playerIndex = -1;
		displayIndex = -1;
		int separatorIndex = key.IndexOf(':');
		return separatorIndex > 0
			&& separatorIndex < key.Length - 1
			&& int.TryParse(key[..separatorIndex], out playerIndex)
			&& int.TryParse(key[(separatorIndex + 1)..], out displayIndex);
	}

	private void ShiftJellyfishDisabledHandSlotAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (playerIndex != MainPlayerIndex
			|| _jellyfishDisabledMainPlayerHandSlotIndex < 0
			|| removedDisplayIndex > _jellyfishDisabledMainPlayerHandSlotIndex)
		{
			return;
		}

		if (removedDisplayIndex == _jellyfishDisabledMainPlayerHandSlotIndex)
		{
			_jellyfishDisabledMainPlayerHandSlotIndex = -1;
			return;
		}

		_jellyfishDisabledMainPlayerHandSlotIndex--;
	}

	private bool IsJellyfishDisabledHandDisplaySlot(int playerIndex, int displayIndex)
	{
		return playerIndex == MainPlayerIndex
			&& displayIndex == _jellyfishDisabledMainPlayerHandSlotIndex;
	}

}
