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

		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		if (itemHandIndex < 0 || itemHandIndex >= itemHand.Count)
		{
			return new PlayCardResult(false, false);
		}

		ItemCard bombCard = itemHand[itemHandIndex];
		int displayCardCount = _playerHands[playerIndex].Count + itemHand.Count;
		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Control handArea = _playerHandAreas[playerIndex];
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		Vector2 startGlobalPosition = sourceCardVisual?.GlobalPosition
			?? handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		itemHand.RemoveAt(itemHandIndex);
		int tableCardIndex = AddBombCardToTable(bombCard, playerIndex);
		await PlayItemCardToTableAnimationAsync(playerIndex, bombCard, startGlobalPosition, tableCardIndex, false);
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
		return new PlayCardResult(true, false, CompletesTurn: false);
	}

	private async Task ResolveBombPairAsync(int turnSequenceGeneration)
	{
		if (!TryGetLastTableBombPair(out int firstBombIndex, out int secondBombIndex))
		{
			return;
		}

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
		Texture2D? firstTexture = firstBomb.ItemOverlay?.Texture;
		Texture2D? secondTexture = secondBomb.ItemOverlay?.Texture;
		var revealTasks = new List<Task>();
		if (!firstBomb.IsFaceUp && firstTexture != null)
		{
			revealTasks.Add(PlayTableCardRevealAnimationAsync(firstBombIndex, firstTexture));
		}

		if (!secondBomb.IsFaceUp && secondTexture != null)
		{
			revealTasks.Add(PlayTableCardRevealAnimationAsync(secondBombIndex, secondTexture));
		}

		if (revealTasks.Count > 0)
		{
			await Task.WhenAll(revealTasks);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}
		}

		firstBomb.IsFaceUp = true;
		secondBomb.IsFaceUp = true;
		RenderTableCards();
		await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	private async Task PlayBombPairTriggerEffectsAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		await PlayBombTableCardTriggerEffectAsync(firstBombIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return;
		}

		await PlayBombTableCardTriggerEffectAsync(secondBombIndex, turnSequenceGeneration);
	}

	private async Task PlayBombTableCardTriggerEffectAsync(int tableCardIndex, int turnSequenceGeneration)
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
		if (!tableCard.ItemOverlay.HasValue || !IsBombTableEffect(tableCard))
		{
			return;
		}

		ItemCard bombCard = tableCard.ItemOverlay.Value;
		await PlaySpecialCardTriggerEffectAsync(
			bombCard.Texture,
			_centerArena.GlobalPosition + cardPositions[tableCardIndex] + GetItemCardVisualPosition(bombCard.Id),
			tableCardIndex * 2 + 1);
	}

	private void RemoveBombPairAndCardsBetween(int firstBombIndex, int secondBombIndex)
	{
		if (firstBombIndex < 0 || secondBombIndex >= _tableCards.Count || firstBombIndex > secondBombIndex)
		{
			return;
		}

		_tableCards.RemoveRange(firstBombIndex, secondBombIndex - firstBombIndex + 1);
	}

	private async Task PlayBombPairDestroySequenceAsync(int firstBombIndex, int secondBombIndex, int turnSequenceGeneration)
	{
		if (firstBombIndex < 0 || secondBombIndex >= _tableCards.Count || firstBombIndex >= secondBombIndex)
		{
			return;
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (secondBombIndex >= cardPositions.Count)
		{
			return;
		}

		for (int tableCardIndex = secondBombIndex; tableCardIndex >= firstBombIndex; tableCardIndex--)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			if (tableCardIndex >= cardPositions.Count)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			Texture2D? startTexture = GetVisibleTableCardTexture(tableCard);
			if (startTexture == null)
			{
				continue;
			}

			tableCard.IsHiddenDuringCapture = true;
			RenderTableCards();
			await PlayBombPairDestroyCardAsync(tableCardIndex, cardPositions[tableCardIndex]);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			await ToSignal(GetTree().CreateTimer(BombDestroyCardStaggerSeconds), SceneTreeTimer.SignalName.Timeout);
		}
	}

	private Texture2D? GetVisibleTableCardTexture(TableCard tableCard)
	{
		if (tableCard.Card.HasValue)
		{
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
		TextureRect? fx = CreateBombPairDestroyFx(centerArenaPosition + CardVisualSize / 2.0f, tableCardIndex);
		if (fx == null)
		{
			return;
		}

		try
		{
			for (int frameIndex = 0; frameIndex < _bombPairDestroyEffectFrames.Count; frameIndex++)
			{
				if (!GodotObject.IsInstanceValid(fx))
				{
					return;
				}

				fx.Texture = _bombPairDestroyEffectFrames[frameIndex];
				await ToSignal(GetTree().CreateTimer(BombPairDestroyFxFrameSeconds), SceneTreeTimer.SignalName.Timeout);
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(fx))
			{
				fx.QueueFree();
			}
		}
	}

	private TextureRect? CreateBombPairDestroyFx(Vector2 localCenter, int tableCardIndex)
	{
		if (_bombPairDestroyEffectFrames.Count == 0)
		{
			return null;
		}

		var fx = new TextureRect
		{
			Name = "BombPairDestroyFx",
			Texture = _bombPairDestroyEffectFrames[0],
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = localCenter - new Vector2(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize) / 2.0f,
			Size = new Vector2(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize),
			PivotOffset = new Vector2(BombPairDestroyFxVisualSize, BombPairDestroyFxVisualSize) / 2.0f,
			ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2,
		};
		_centerArena.AddChild(fx);
		return fx;
	}

	private Vector2? GetTableCardGlobalCenter(int tableCardIndex)
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

		return _centerArena.GlobalPosition + cardPositions[tableCardIndex] + CardVisualSize / 2.0f;
	}

	private bool IsBombTableCard(TableCard tableCard)
	{
		return IsBombTableEffect(tableCard);
	}
}
