using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayBombCardFromHandAsync(int playerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| playerIndex >= _playerHandAreas.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (_tableCards.Count == 0)
		{
			return new PlayCardResult(false, false);
		}

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard bombCard = itemHand[itemHandIndex];
		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Vector2 startGlobalPosition;
		if (_networkAuthorityRuleTransactionActive)
		{
			startGlobalPosition = GetNetworkAuthorityRuleVisualStartGlobalPosition(Vector2.Zero);
		}
		else
		{
			int displayCardCount = _playerHands[playerIndex].Count + itemHand.Count;
			Control handArea = _playerHandAreas[playerIndex];
			Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
			Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
			Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
			TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
			startGlobalPosition = sourceCardVisual?.GlobalPosition
				?? handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
			if (sourceCardSlot != null)
			{
				sourceCardSlot.Visible = false;
			}
		}

		itemHand.RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(playerIndex, displayIndex);
		MarkNetworkAuthorityPrimaryMutationApplied();
		int tableCardIndex = AddBombCardToTable(bombCard, playerIndex);
		PlaySpecialItemCardPlaySfx();
		await PlayItemCardToTableAnimationAsync(playerIndex, bombCard, startGlobalPosition, tableCardIndex, false);
		PlayGameSfx(GameSfx.BombPlace, -1.5f, 0.97f, 1.03f, 0.06);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		RenderTableCards();
		await ResolveBombPairAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderTableCards();
		CheckForWinner();
		return new PlayCardResult(
			true,
			false,
			ItemPlayVisualSync: new NetworkItemPlayVisualSync(
				bombCard.Id,
				CardPlayPresentationTypes.TableOverlay,
				itemHandIndex,
				tableCardIndex));
	}

	private async Task ResolveBombPairAsync(int turnSequenceGeneration)
	{
		if (!TryGetLastTableBombPair(out int firstBombIndex, out int secondBombIndex))
		{
			return;
		}

		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastBombPairVisualEventFromHost(
				_currentTurnPlayerIndex,
				turnSequenceGeneration,
				snapshotId);
		}

		PlayGameSfx(GameSfx.BombPairLock, -1.0f, 0.97f, 1.03f, 0.08);
		await RevealBombPairAsync(firstBombIndex, secondBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombPairTriggerEffectsAsync(firstBombIndex, secondBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombPairDestroySequenceAsync(firstBombIndex, secondBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		RemoveBombPairAndCardsBetween(firstBombIndex, secondBombIndex);
	}

	private bool TryGetLastTableBombPair(out int firstBombIndex, out int secondBombIndex)
	{
		firstBombIndex = -1;
		secondBombIndex = -1;
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			if (!IsBombTableCard(_tableCards[tableCardIndex]))
			{
				continue;
			}

			if (secondBombIndex < 0)
			{
				secondBombIndex = tableCardIndex;
				continue;
			}

			firstBombIndex = tableCardIndex;
			return true;
		}

		return false;
	}

	private async Task RevealBombPairAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		if (firstBombIndex < 0
			|| secondBombIndex < 0
			|| firstBombIndex >= _tableCards.Count
			|| secondBombIndex >= _tableCards.Count)
		{
			return;
		}

		TableCard firstBomb = _tableCards[firstBombIndex];
		TableCard secondBomb = _tableCards[secondBombIndex];
		if (_networkAuthorityRuleTransactionActive)
		{
			firstBomb.IsFaceUp = true;
			secondBomb.IsFaceUp = true;
			return;
		}

		Texture2D? firstTexture = firstBomb.ItemOverlay?.Texture;
		Texture2D? secondTexture = secondBomb.ItemOverlay?.Texture;
		Task? firstRevealTask = null;
		Task? secondRevealTask = null;
		if (!firstBomb.IsFaceUp && firstTexture != null)
		{
			firstRevealTask = PlayTableCardRevealAnimationAsync(firstBombIndex, firstTexture);
		}

		if (!secondBomb.IsFaceUp && secondTexture != null)
		{
			secondRevealTask = PlayTableCardRevealAnimationAsync(secondBombIndex, secondTexture);
		}

		if (firstRevealTask != null && secondRevealTask != null)
		{
			await Task.WhenAll(firstRevealTask, secondRevealTask);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
		else if (firstRevealTask != null)
		{
			await firstRevealTask;
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}
		else if (secondRevealTask != null)
		{
			await secondRevealTask;
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		firstBomb.IsFaceUp = true;
		secondBomb.IsFaceUp = true;
		RenderTableCards();
		await WaitForBoardDelayAsync(TableCardRevealPauseSeconds);
	}

	private async Task PlayBombPairTriggerEffectsAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		PlayGameSfx(GameSfx.BombWarning, -1.0f, 0.98f, 1.02f, 0.12);
		await PlayBombTableCardTriggerEffectAsync(firstBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombTableCardTriggerEffectAsync(secondBombIndex, turnSequenceGeneration);
	}

	private async Task PlayBombTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		TableCard tableCard = _tableCards[tableCardIndex];
		if (!tableCard.ItemOverlay.HasValue || !IsBombTableEffect(tableCard))
		{
			return;
		}

		ItemCard bombCard = tableCard.ItemOverlay.Value;
		await PlaySpecialCardTriggerEffectAsync(
			bombCard.Texture,
			_centerArena.GlobalPosition + cardPosition + GetItemCardVisualPosition(bombCard.Id),
			tableCardIndex * 2 + 1);
	}

	private void RemoveBombPairAndCardsBetween(int firstBombIndex, int secondBombIndex)
	{
		if (firstBombIndex < 0 || secondBombIndex >= _tableCards.Count || firstBombIndex > secondBombIndex)
		{
			return;
		}

		for (int tableCardIndex = firstBombIndex; tableCardIndex <= secondBombIndex; tableCardIndex++)
		{
			ClearAdventureTraceHuntMarkerIfTableCardRemoved(_tableCards[tableCardIndex]);
		}

		_tableCards.RemoveRange(firstBombIndex, secondBombIndex - firstBombIndex + 1);
		RefreshGardenEelBuriedCardIndex();
	}

	private async Task PlayBombPairDestroySequenceAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		if (firstBombIndex < 0 || secondBombIndex >= _tableCards.Count || firstBombIndex >= secondBombIndex)
		{
			return;
		}

		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		PlayGameSfx(GameSfx.BombExplosion, -0.5f, 0.97f, 1.02f, 0.12);

		for (int tableCardIndex = secondBombIndex; tableCardIndex >= firstBombIndex; tableCardIndex--)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			Texture2D? startTexture = GetVisibleTableCardTexture(tableCard);
			if (startTexture == null)
			{
				continue;
			}

			tableCard.IsHiddenDuringCapture = true;
			RenderTableCards();
			await PlayBombPairDestroyCardAsync(tableCardIndex, GetTableCardPosition(tableCardIndex, tableLayoutRect));
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			await WaitForBoardDelayAsync(BombDestroyCardStaggerSeconds);
		}
	}

	private Texture2D? GetVisibleTableCardTexture(TableCard tableCard)
	{
		if (tableCard.Card.HasValue)
		{
			if (ShouldShowMainPlayerTablePreview(tableCard))
			{
				return tableCard.Card.Value.Texture;
			}

			return tableCard.IsFaceUp || _cardBackTexture == null
				? tableCard.Card.Value.Texture
				: _cardBackTexture;
		}

		if (tableCard.ItemOverlay.HasValue)
		{
			return tableCard.IsFaceUp || _cardBackTexture == null
				? tableCard.ItemOverlay.Value.Texture
				: _cardBackTexture;
		}

		return null;
	}

	private async Task PlayBombPairDestroyCardAsync(int tableCardIndex, Vector2 centerArenaPosition)
	{
		EnsureItemModeOptionalFxResourcesLoaded();

		TextureRect? fx = CreateBombPairDestroyFx(centerArenaPosition + CardVisualSize / 2.0f, tableCardIndex);
		if (fx == null)
		{
			return;
		}

		PlayGameSfx(GameSfx.BombDestroyCard, -2.0f, 0.95f, 1.06f, 0.03);
		Tween tween = CreateBoardTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(fx, "scale", Vector2.One * 1.08f, BombPairDestroyFxFrameSeconds * _bombPairDestroyEffectFrames.Count);
		tween.TweenProperty(fx, "modulate:a", 0.0f, BombPairDestroyFxFrameSeconds * _bombPairDestroyEffectFrames.Count);
		FrameAnimationHelpers.TweenTextureFrames(tween, fx, _bombPairDestroyEffectFrames, BombPairDestroyFxFrameSeconds);
		tween.Finished += () =>
		{
			if (GodotObject.IsInstanceValid(fx))
			{
				_animationPresenter.Release(fx);
			}
		};

		await WaitForBoardTweenAsync(tween);
	}

	private TextureRect? CreateBombPairDestroyFx(Vector2 localCenter, int tableCardIndex)
	{
		if (_bombPairDestroyEffectFrames.Count == 0)
		{
			return null;
		}

		Vector2 fxSize = new(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize);
		TextureRect fx = _animationPresenter.RentTextureRect(
			_centerArena,
			"BombPairDestroyFx",
			_bombPairDestroyEffectFrames[0],
			fxSize,
			PlayedCardFlightZIndex + tableCardIndex * 2,
			CanvasItem.TextureFilterEnum.Linear);
		fx.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		fx.Position = localCenter - fxSize / 2.0f;
		return fx;
	}

	private Vector2? GetTableCardGlobalCenter(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return null;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return null;
		}

		return _centerArena.GlobalPosition + cardPosition + CardVisualSize / 2.0f;
	}

	private bool IsBombTableCard(TableCard tableCard)
	{
		return IsBombTableEffect(tableCard);
	}
}
