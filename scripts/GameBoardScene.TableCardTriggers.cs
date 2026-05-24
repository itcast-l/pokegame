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
		if (!tableCard.Card.HasValue || !IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Magnet))
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

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Magnet))
		{
			return;
		}

		Texture2D triggerTexture = _itemCardTextures.TryGetValue(MagnetItemCardId, out Texture2D? magnetTexture)
			? magnetTexture
			: tableCard.Card.Value.Texture;
		await PlaySpecialCardTriggerEffectAsync(
			triggerTexture,
			_centerArena.GlobalPosition + cardPositions[tableCardIndex],
			tableCardIndex * 2 + 1);
	}

	private async Task ResolveDoubleTableCardTriggerAsync(
		int tableCardIndex,
		int turnSequenceGeneration,
		HashSet<int> resolvedDoubleTriggerIndexes)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Double))
		{
			return;
		}

		if (!resolvedDoubleTriggerIndexes.Add(tableCardIndex))
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

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Double))
		{
			return;
		}

		Texture2D triggerTexture = _itemCardTextures.TryGetValue(DoubleItemCardId, out Texture2D? doubleTexture)
			? doubleTexture
			: tableCard.Card.Value.Texture;
		await PlaySpecialCardTriggerEffectAsync(
			triggerTexture,
			_centerArena.GlobalPosition + cardPositions[tableCardIndex],
			tableCardIndex * 2 + 1);
	}

	private async Task ResolveOvertimeTableCardTriggerAsync(
		int tableCardIndex,
		int turnSequenceGeneration,
		HashSet<int> resolvedOvertimeTriggerIndexes)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Overtime))
		{
			return;
		}

		if (!resolvedOvertimeTriggerIndexes.Add(tableCardIndex))
		{
			return;
		}

		await PlayOvertimeTableCardTriggerEffectAsync(tableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		if (tableCard.Card.HasValue
			&& TryGetAttachedItemIdByModifier(tableCard.Card.Value, CardAttachmentModifiers.Overtime, out string itemCardId))
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

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.Card.HasValue || !IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Overtime))
		{
			return;
		}

		Texture2D triggerTexture = _itemCardTextures.TryGetValue(OvertimeItemCardId, out Texture2D? overtimeTexture)
			? overtimeTexture
			: tableCard.Card.Value.Texture;
		await PlaySpecialCardTriggerEffectAsync(
			triggerTexture,
			_centerArena.GlobalPosition + cardPositions[tableCardIndex],
			tableCardIndex * 2 + 1);
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
		if (IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Magnet))
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

		if (IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Double))
		{
			await ResolveDoubleTableCardTriggerAsync(tableCardIndex, turnSequenceGeneration, resolvedDoubleTriggerIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		if (IsAttachedModifier(tableCard.Card.Value, CardAttachmentModifiers.Overtime))
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
		var candidates = new List<int>();
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
				candidates.Add(tableCardIndex);
			}
		}

		return candidates.Count == 0 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
	}

	private List<TableSpecialCardTrigger> GetCapturedSpecialCardTriggers(int captureStartIndex, HashSet<int>? extraTableCardIndexes = null)
	{
		var triggers = new List<TableSpecialCardTrigger>();
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return triggers;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= captureStartIndex; tableCardIndex--)
		{
			AddCapturedSpecialCardTrigger(tableCardIndex, cardPositions, triggers);
		}

		if (extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				AddCapturedSpecialCardTrigger(tableCardIndex, cardPositions, triggers);
			}
		}

		triggers.Sort((a, b) =>
		{
			int priorityComparison = GetTableTriggerPriority(b.TableCardIndex).CompareTo(GetTableTriggerPriority(a.TableCardIndex));
			return priorityComparison != 0 ? priorityComparison : b.TableCardIndex.CompareTo(a.TableCardIndex);
		});
		return triggers;
	}

	private void AddCapturedSpecialCardTrigger(int tableCardIndex, IReadOnlyList<Vector2> cardPositions, List<TableSpecialCardTrigger> triggers)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count || tableCardIndex >= cardPositions.Count)
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
			_centerArena.GlobalPosition + cardPositions[tableCardIndex] + GetItemCardVisualPosition(tableCard.ItemOverlay.Value.Id),
			tableCardIndex * 2 + 1));
	}

	private int GetTableTriggerPriority(int tableCardIndex)
	{
		return TryGetTableCardTriggerDefinition(tableCardIndex, out ItemCardDefinition definition) ? definition.Priority : 0;
	}

	private TableSpecialCardTrigger? GetTableSpecialCardTrigger(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return null;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (tableCardIndex >= cardPositions.Count)
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
			_centerArena.GlobalPosition + cardPositions[tableCardIndex] + GetItemCardVisualPosition(itemOverlay.Id),
			tableCardIndex * 2 + 1);
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

	private void ApplyJellyfishBubbleAfterCapture(int playerIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != JellyfishAdventureEnemyAvatarId
			|| playerIndex != 1
			|| _jellyfishBubbleTriggeredThisTurn
			|| _jellyfishHiddenMainPlayerHandSlotIndex >= 0)
		{
			return;
		}

		int handSlotIndex = PickVisibleMainPlayerHandSlotIndex();
		if (handSlotIndex < 0)
		{
			return;
		}

		_jellyfishBubbleTriggeredThisTurn = true;
		_jellyfishHiddenMainPlayerHandSlotIndex = handSlotIndex;
		HideHandDisplaySlot(MainPlayerIndex, handSlotIndex);
		RenderPlayerHands();
	}

	private bool ShouldGardenEelBuryNextPlayedCard(int playerIndex)
	{
		return adventure_mode
			&& adventure_enemy_avatar_id == GardenEelAdventureEnemyAvatarId
			&& playerIndex == 1
			&& _gardenEelSandAmbushReady;
	}

	private void MarkGardenEelBuriedCard(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		_gardenEelSandAmbushReady = false;
		_gardenEelBuriedTableCardIndex = tableCardIndex;
		_tableCards[tableCardIndex].IsBuriedInSand = true;
	}

	private void ClearGardenEelBuriedCardIfNeeded(int playerIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != GardenEelAdventureEnemyAvatarId
			|| playerIndex != 1
			|| _gardenEelBuriedTableCardIndex < 0)
		{
			return;
		}

		if (_gardenEelBuriedTableCardIndex < _tableCards.Count)
		{
			_tableCards[_gardenEelBuriedTableCardIndex].IsBuriedInSand = false;
		}

		_gardenEelBuriedTableCardIndex = -1;
		RenderTableCards();
	}

	private void RecordGardenEelCaptureForSandAmbush(int playerIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != GardenEelAdventureEnemyAvatarId
			|| playerIndex != 1
			|| _gardenEelSandAmbushReady)
		{
			return;
		}

		_gardenEelCaptureCountUntilSandAmbush++;
		if (_gardenEelCaptureCountUntilSandAmbush >= GardenEelCapturesBeforeSandAmbush)
		{
			_gardenEelCaptureCountUntilSandAmbush = 0;
			_gardenEelSandAmbushReady = true;
		}
	}

	private void ApplySeaSlugColorCurrentAfterMiss(int playerIndex, int playedTableCardIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != SeaSlugAdventureEnemyAvatarId
			|| playerIndex != 1
			|| playedTableCardIndex < 0
			|| playedTableCardIndex >= _tableCards.Count
			|| _tableCards.Count < 2)
		{
			return;
		}

		_tableCards[playedTableCardIndex].IsFaceUp = false;
		int swapIndex = PickSeaSlugColorCurrentSwapIndex(playedTableCardIndex);
		if (swapIndex < 0)
		{
			return;
		}

		(_tableCards[playedTableCardIndex], _tableCards[swapIndex]) = (_tableCards[swapIndex], _tableCards[playedTableCardIndex]);
	}

	private async Task ApplyNautilusBombAfterMainPlayerCaptureAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != NautilusAdventureEnemyAvatarId
			|| playerIndex != MainPlayerIndex)
		{
			return;
		}

		_nautilusMainPlayerCaptureCountUntilBomb++;
		if (_nautilusMainPlayerCaptureCountUntilBomb < NautilusCapturesBeforeBomb)
		{
			return;
		}

		if (_tableCards.Count < 2)
		{
			return;
		}

		ItemCard? bombCard = CreateItemCard(BombItemCardId);
		if (!bombCard.HasValue)
		{
			return;
		}

		int insertionIndex = PickNautilusBombInsertionIndex();
		if (insertionIndex < 0)
		{
			return;
		}

		InsertBombCardIntoTable(bombCard.Value, 1, insertionIndex);
		_nautilusMainPlayerCaptureCountUntilBomb = 0;
		PlayPlayerAvatarAction(1, CharacterAvatar.AvatarAction.Score);
		RenderTableCards();
		await ResolveBombPairAsync(turnSequenceGeneration);
		RenderTableCards();
	}

	private void RecordNarwhalCaptureForDouble(int playerIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != NarwhalAdventureEnemyAvatarId
			|| playerIndex != 1
			|| _narwhalDoubleReady)
		{
			return;
		}

		_narwhalCaptureCountUntilDouble++;
		if (_narwhalCaptureCountUntilDouble >= NarwhalCapturesBeforeDouble)
		{
			_narwhalCaptureCountUntilDouble = 0;
			_narwhalDoubleReady = true;
		}
	}

	private async Task ResolveAdventureEnemyTurnEndAbilityAsync(int playerIndex, PlayCardResult result)
	{
		await ResolveLeafySeadragonBladeBreakAsync(playerIndex, result);
	}

	private async Task<PlayCardResult> ResolveDragonPrinceJackTurnStartAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!ShouldResolveDragonPrinceJack(playerIndex))
		{
			return new PlayCardResult(false, false);
		}

		_dragonPrinceJackUsed = true;
		PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
		if (_tableCards.Count > 0)
		{
			Texture2D? triggerTexture = GetVisibleTableCardTexture(_tableCards[^1]);
			if (triggerTexture != null)
			{
				List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
				await PlaySpecialCardTriggerEffectAsync(triggerTexture, _centerArena.GlobalPosition + cardPositions[^1], _tableCards.Count * 2 + 1);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new PlayCardResult(false, false);
				}
			}
		}

		PlayCardResult result = await ResolveJackEffectAsync(playerIndex, turnSequenceGeneration);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return result with { CompletesTurn = true };
	}

	private bool ShouldResolveDragonPrinceJack(int playerIndex)
	{
		return adventure_mode
			&& adventure_enemy_avatar_id == DragonPrinceAdventureEnemyAvatarId
			&& playerIndex == 1
			&& !_dragonPrinceJackUsed
			&& _tableCards.Count >= DragonRoyalCaptureThreshold;
	}

	private void ApplyDragonQueenAndKingAfterEnemyCapture(int playerIndex, int removedTableCardCount)
	{
		if (!adventure_mode || playerIndex != 1 || removedTableCardCount < DragonRoyalCaptureThreshold)
		{
			return;
		}

		if (adventure_enemy_avatar_id == DragonQueenAdventureEnemyAvatarId)
		{
			StealRandomMainPlayerHandCardForEnemy(playerIndex);
		}
		else if (adventure_enemy_avatar_id == BossKAdventureEnemyAvatarId)
		{
			StealMainPlayerTopDeckCardForEnemy(playerIndex);
		}
	}

	private void StealRandomMainPlayerHandCardForEnemy(int enemyPlayerIndex)
	{
		if (enemyPlayerIndex < 0
			|| enemyPlayerIndex >= _playerHands.Count
			|| enemyPlayerIndex >= _playerItemHands.Count
			|| MainPlayerIndex >= _playerHands.Count
			|| MainPlayerIndex >= _playerItemHands.Count)
		{
			return;
		}

		List<int> targetHandIndexes = BuildDragonQueenStealTargetHandIndexes(enemyPlayerIndex);
		if (targetHandIndexes.Count == 0)
		{
			return;
		}

		int targetHandIndex = targetHandIndexes[_rng.RandiRange(0, targetHandIndexes.Count - 1)];
		PlayCardResult result = ResolveQueenEffect(enemyPlayerIndex, MainPlayerIndex, targetHandIndex);
		if (result.Played)
		{
			ShiftJellyfishHiddenHandSlotAfterRemovedCard(MainPlayerIndex, targetHandIndex);
			PlayPlayerAvatarAction(enemyPlayerIndex, CharacterAvatar.AvatarAction.Score);
			RenderPlayerHands();
		}
	}

	private List<int> BuildDragonQueenStealTargetHandIndexes(int enemyPlayerIndex)
	{
		var targetHandIndexes = new List<int>();
		for (int handIndex = 0; handIndex < _playerHands[MainPlayerIndex].Count; handIndex++)
		{
			targetHandIndexes.Add(handIndex);
		}

		if (!HasRoomForItemCard(enemyPlayerIndex))
		{
			return targetHandIndexes;
		}

		for (int itemIndex = 0; itemIndex < _playerItemHands[MainPlayerIndex].Count; itemIndex++)
		{
			targetHandIndexes.Add(_playerHands[MainPlayerIndex].Count + itemIndex);
		}

		return targetHandIndexes;
	}

	private void StealMainPlayerTopDeckCardForEnemy(int enemyPlayerIndex)
	{
		if (enemyPlayerIndex < 0
			|| enemyPlayerIndex >= _playerDecks.Count
			|| MainPlayerIndex >= _playerDecks.Count
			|| _playerDecks[MainPlayerIndex].Count == 0)
		{
			return;
		}

		PlayingCard stolenCard = _playerDecks[MainPlayerIndex][0];
		_playerDecks[MainPlayerIndex].RemoveAt(0);
		AddCardToPlayerDeck(enemyPlayerIndex, stolenCard);
		PlayPlayerAvatarAction(enemyPlayerIndex, CharacterAvatar.AvatarAction.Score);
		PlayPlayerAvatarAction(MainPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerCardPiles();
	}

	private async Task ResolveLeafySeadragonBladeBreakAsync(int playerIndex, PlayCardResult result)
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

		PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
		_tableCards[targetTableCardIndex].IsHiddenDuringCapture = true;
		RenderTableCards();
		await PlayBombPairDestroyCardAsync(targetTableCardIndex, BuildTableCardPositions(_tableCards.Count)[targetTableCardIndex]);
		if (IsGameOver() || targetTableCardIndex >= _tableCards.Count)
		{
			return;
		}

		_tableCards.RemoveAt(targetTableCardIndex);
		RefreshGardenEelBuriedCardIndex();
		_leafySeadragonBladeBreakCooldownTurns = LeafySeadragonBladeBreakCooldownTurns;
		RenderTableCards();
	}

	private bool ShouldResolveLeafySeadragonBladeBreak(int playerIndex, PlayCardResult result)
	{
		return adventure_mode
			&& adventure_enemy_avatar_id == LeafySeadragonAdventureEnemyAvatarId
			&& playerIndex == 1
			&& !result.Captured
			&& _leafySeadragonBladeBreakCooldownTurns <= 0
			&& _tableCards.Count >= LeafySeadragonBladeBreakTableCardThreshold
			&& PickLeafySeadragonBladeBreakTargetIndex() >= 0;
	}

	private void UpdateLeafySeadragonBladeBreakCooldown(int playerIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != LeafySeadragonAdventureEnemyAvatarId
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

		_narwhalDoubleReady = false;
		return card.WithAttachedItemEffect(DoubleItemCardId);
	}

	private bool ShouldApplyNarwhalDoubleToPlayedCard(int playerIndex, PlayingCard card)
	{
		return adventure_mode
			&& adventure_enemy_avatar_id == NarwhalAdventureEnemyAvatarId
			&& playerIndex == 1
			&& _narwhalDoubleReady
			&& !card.HasItemEffect
			&& !IsExcludedDeckRank(card.Rank);
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

	private int PickSeaSlugColorCurrentSwapIndex(int playedTableCardIndex)
	{
		var candidates = new List<int>();
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (tableCardIndex == playedTableCardIndex || IsFixedDuringTableShuffle(_tableCards[tableCardIndex]))
			{
				continue;
			}

			candidates.Add(tableCardIndex);
		}

		return candidates.Count == 0 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
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
		var candidates = new List<int>();
		for (int displayIndex = 0; displayIndex < totalHandCardCount; displayIndex++)
		{
			if (!IsHandDisplaySlotHidden(MainPlayerIndex, displayIndex))
			{
				candidates.Add(displayIndex);
			}
		}

		return candidates.Count <= 1 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
	}

	private void ClearJellyfishBubbleIfMainPlayerTurnCompleted(int playerIndex, PlayCardResult result)
	{
		if (playerIndex != MainPlayerIndex
			|| result.Captured
			|| _jellyfishHiddenMainPlayerHandSlotIndex < 0)
		{
			return;
		}

		ForceShowHandDisplaySlot(MainPlayerIndex, _jellyfishHiddenMainPlayerHandSlotIndex);
		_jellyfishHiddenMainPlayerHandSlotIndex = -1;
		RenderPlayerHands();
	}

	private void ResetJellyfishBubbleTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| adventure_enemy_avatar_id != JellyfishAdventureEnemyAvatarId
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		_jellyfishBubbleTriggeredThisTurn = false;
	}

	private void ShiftJellyfishHiddenHandSlotAfterRemovedCard(int playerIndex, int removedDisplayIndex)
	{
		if (playerIndex != MainPlayerIndex
			|| _jellyfishHiddenMainPlayerHandSlotIndex < 0
			|| removedDisplayIndex > _jellyfishHiddenMainPlayerHandSlotIndex)
		{
			return;
		}

		ForceShowHandDisplaySlot(MainPlayerIndex, _jellyfishHiddenMainPlayerHandSlotIndex);
		if (removedDisplayIndex == _jellyfishHiddenMainPlayerHandSlotIndex)
		{
			_jellyfishHiddenMainPlayerHandSlotIndex = -1;
			return;
		}

		_jellyfishHiddenMainPlayerHandSlotIndex--;
		if (_jellyfishHiddenMainPlayerHandSlotIndex >= 0)
		{
			HideHandDisplaySlot(MainPlayerIndex, _jellyfishHiddenMainPlayerHandSlotIndex);
		}
	}

}
