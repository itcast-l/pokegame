using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private void RenderPlayerCardPiles()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control pile = _playerCardPiles[playerIndex];
			foreach (Node child in pile.GetChildren())
			{
				RemoveAndQueueFreeChild(pile, child);
			}

			pile.Visible = playerIndex < player_count;
			if (!pile.Visible)
			{
				continue;
			}

			int cardCount = _playerDecks[playerIndex].Count;
			int visibleCards = Math.Min(cardCount, VisiblePileCards);
			float cardRotation = GetCardRotationDegreesForPlayer(playerIndex);
			float groupWidth = CardVisualSize.X + (visibleCards - 1) * CardPileOffset.X;
			float groupHeight = CardVisualSize.Y - (visibleCards - 1) * CardPileOffset.Y;
			var firstCardPosition = new Vector2(
				(CardPileSize.X - groupWidth) / 2.0f,
				(CardPileSize.Y - groupHeight) / 2.0f - (visibleCards - 1) * CardPileOffset.Y);

			for (int cardIndex = 0; cardIndex < visibleCards; cardIndex++)
			{
				var cardBack = new TextureRect
				{
					Texture = _cardBackTexture,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore,
					Position = firstCardPosition + cardIndex * CardPileOffset,
					Size = CardVisualSize,
					PivotOffset = CardVisualSize / 2.0f,
					RotationDegrees = cardRotation,
					ZIndex = cardIndex,
				};

				MarkAnimatedCardBack(cardBack);
				pile.AddChild(cardBack);
			}

			var countLabel = new Label
			{
				Name = "CardCountLabel",
				Text = cardCount.ToString(),
				HorizontalAlignment = HorizontalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(0.0f, CardPileSize.Y - 22.0f),
				Size = new Vector2(CardPileSize.X, 22.0f),
				Visible = false,
				ZIndex = VisiblePileCards + 1,
			};
			countLabel.AddThemeColorOverride("font_color", Colors.White);
			countLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
			countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
			countLabel.AddThemeConstantOverride("shadow_offset_y", 1);

			pile.AddChild(countLabel);
		}
	}

	private void RenderPlayerHands()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control handArea = _playerHandAreas[playerIndex];
			foreach (Node child in handArea.GetChildren())
			{
				if (child.HasMeta(HandCardSlotMeta))
				{
					RemoveAndQueueFreeChild(handArea, child);
				}
			}

			handArea.Visible = playerIndex < player_count;
			if (!handArea.Visible)
			{
				continue;
			}

			List<PlayingCard> hand = _playerHands[playerIndex];
			List<ItemCard> itemHand = _playerItemHands[playerIndex];
			int displayCardCount = hand.Count + itemHand.Count;
			Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
			Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
			float cardRotation = GetCardRotationDegreesForPlayer(playerIndex);
			bool canMainPlayerAct = CanMainPlayerAct();

			void AddHandCard(
				Texture2D? texture,
				int displayIndex,
				bool isPlayable,
				int handIndex,
				int itemHandIndex,
				bool animateCardBack,
				float visualScale,
				bool showMagnetOverlay = false)
			{
				if (texture == null)
				{
					return;
				}

				Vector2 basePosition = firstCardPosition + displayIndex * cardOffset;
				Vector2 visualSize = CardVisualSize * visualScale;
				Vector2 visualBasePosition = (CardVisualSize - visualSize) / 2.0f;
				var cardSlot = new Control
				{
					Name = $"HandCard_{displayIndex}",
					MouseFilter = isPlayable ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore,
					Position = basePosition,
					Size = CardVisualSize,
					ZIndex = displayIndex,
				};
				cardSlot.SetMeta(HandCardSlotMeta, true);
				var cardVisual = new TextureRect
				{
					Texture = texture,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore,
					Position = visualBasePosition,
					Size = visualSize,
					PivotOffset = visualSize / 2.0f,
					RotationDegrees = cardRotation,
				};

				if (animateCardBack)
				{
					MarkAnimatedCardBack(cardVisual);
				}

				if (isPlayable)
				{
					Tween? hoverTween = null;
					void AnimateHover(bool hovered)
					{
						hoverTween?.Kill();
						if (hovered)
						{
							cardSlot.ZIndex = displayIndex + HoveredHandCardZIndexBoost;
							cardVisual.ZIndex = HoveredHandCardZIndexBoost;
						}

						Vector2 targetPosition = visualBasePosition + (hovered ? new Vector2(0.0f, -HoveredHandCardLift) : Vector2.Zero);
						Vector2 targetScale = hovered ? Vector2.One * HoveredHandCardScale : Vector2.One;
						float duration = hovered ? HandCardHoverInTweenTime : HandCardHoverOutTweenTime;
						hoverTween = cardVisual.CreateTween();
						hoverTween.SetParallel(true);
						hoverTween.SetTrans(hovered ? Tween.TransitionType.Cubic : Tween.TransitionType.Sine);
						hoverTween.SetEase(hovered ? Tween.EaseType.Out : Tween.EaseType.InOut);
						hoverTween.TweenProperty(cardVisual, "position", targetPosition, duration);
						hoverTween.TweenProperty(cardVisual, "scale", targetScale, duration);
						if (!hovered)
						{
							hoverTween.Finished += () =>
							{
								cardSlot.ZIndex = displayIndex;
								cardVisual.ZIndex = 0;
							};
						}
					}

					cardSlot.MouseEntered += () => AnimateHover(true);
					cardSlot.MouseExited += () => AnimateHover(false);

					cardSlot.GuiInput += inputEvent =>
					{
						if (itemHandIndex >= 0)
						{
							TryPlayMainPlayerItemCard(inputEvent, itemHandIndex);
							return;
						}

						TryPlayMainPlayerCard(inputEvent, handIndex);
					};
				}

				if (showMagnetOverlay)
				{
					AddMagnetOverlay(cardVisual, Vector2.Zero, visualSize, 0.0f, 1);
				}

				cardSlot.AddChild(cardVisual);
				handArea.AddChild(cardSlot);
			}

			for (int cardIndex = 0; cardIndex < hand.Count; cardIndex++)
			{
				PlayingCard card = hand[cardIndex];
				bool showCardBack = playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;
				Texture2D? texture = showCardBack ? _cardBackTexture : card.Texture;
				bool isPlayable = playerIndex == MainPlayerIndex && canMainPlayerAct;
				AddHandCard(texture, cardIndex, isPlayable, cardIndex, -1, showCardBack, 1.0f, card.HasMagnetEffect && !showCardBack);
			}

			for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
			{
				ItemCard itemCard = itemHand[itemIndex];
				bool showCardBack = playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;
				Texture2D? texture = showCardBack ? _cardBackTexture : itemCard.Texture;
				float visualScale = showCardBack ? 1.0f : GetItemCardVisualScale(itemCard.Id);
				bool isPlayable = playerIndex == MainPlayerIndex
					&& canMainPlayerAct
					&& PlayableSpecialItemCardIds.Contains(itemCard.Id)
					&& (!RequiresPlayerTarget(itemCard.Id) || HasPlayableTargetForItemCard(itemCard.Id, playerIndex));
				AddHandCard(texture, hand.Count + itemIndex, isPlayable, -1, itemIndex, showCardBack, visualScale);
			}
		}
	}

	private void AddMagnetOverlay(Node parent, Vector2 position, Vector2 size, float rotationDegrees = 0.0f, int zIndex = 1)
	{
		if (_magnetCardOverlayTexture == null)
		{
			return;
		}

		var overlay = new TextureRect
		{
			Name = "MagnetCardOverlay",
			Texture = _magnetCardOverlayTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = position,
			Size = size,
			PivotOffset = size / 2.0f,
			RotationDegrees = rotationDegrees,
			ZIndex = zIndex,
		};
		parent.AddChild(overlay);
	}

	private static float GetItemCardVisualScale(string itemCardId)
	{
		return 1.0f;
	}

	private static Vector2 GetItemCardVisualSize(string itemCardId)
	{
		return CardVisualSize * GetItemCardVisualScale(itemCardId);
	}

	private static Vector2 GetItemCardVisualPosition(string itemCardId)
	{
		return (CardVisualSize - GetItemCardVisualSize(itemCardId)) / 2.0f;
	}

	private static float GetCardRotationDegreesForPlayer(int playerIndex)
	{
		return playerIndex switch
		{
			2 => 90.0f,
			3 => -90.0f,
			_ => 0.0f,
		};
	}

	private static TextureRect? FindHandCardVisual(Control? handCardSlot)
	{
		if (handCardSlot == null)
		{
			return null;
		}

		foreach (Node child in handCardSlot.GetChildren())
		{
			if (child is TextureRect textureRect)
			{
				return textureRect;
			}
		}

		return null;
	}

	private void TryPlayMainPlayerCard(InputEvent inputEvent, int handIndex)
	{
		if (!CanMainPlayerAct())
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		Vector2? startGlobalPosition = TryGetHandCardStartGlobalPosition(MainPlayerIndex, handIndex);
		_ = PlayMainPlayerCardAndAdvanceAsync(handIndex, _turnSequenceGeneration, startGlobalPosition);
	}

	private void TryPlayMainPlayerItemCard(InputEvent inputEvent, int itemHandIndex)
	{
		if (!CanMainPlayerAct())
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		_ = PlayMainPlayerItemCardAndAdvanceAsync(itemHandIndex, _turnSequenceGeneration);
	}

	private Vector2? TryGetHandCardStartGlobalPosition(int playerIndex, int handIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count)
		{
			return null;
		}

		Control handArea = _playerHandAreas[playerIndex];
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		return FindHandCardVisual(sourceCardSlot)?.GlobalPosition;
	}

	private Vector2? GetHandItemCardGlobalPosition(int playerIndex, int itemHandIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHandAreas.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[playerIndex].Count)
		{
			return null;
		}

		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Control handArea = _playerHandAreas[playerIndex];
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		if (sourceCardVisual != null)
		{
			return sourceCardVisual.GlobalPosition;
		}

		int displayCardCount = _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count;
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		return handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
	}

	private void RenderTableCards()
	{
		foreach (Node child in _centerArena.GetChildren())
		{
			RemoveAndQueueFreeChild(_centerArena, child);
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int cardIndex = 0; cardIndex < _tableCards.Count; cardIndex++)
		{
			TableCard tableCardData = _tableCards[cardIndex];
			if (tableCardData.IsHiddenDuringCapture)
			{
				continue;
			}

			int baseZIndex = cardIndex * 2;
			Texture2D? tableCardTexture = null;
			bool animateCardBack = false;
			bool hideItemOverlay = false;
			if (tableCardData.Card.HasValue)
			{
				animateCardBack = !tableCardData.IsFaceUp && _cardBackTexture != null;
				tableCardTexture = tableCardData.IsFaceUp ? tableCardData.Card.Value.Texture : _cardBackTexture;
			}
			else if (tableCardData.ItemOverlay.HasValue && !tableCardData.IsFaceUp && _cardBackTexture != null)
			{
				animateCardBack = true;
				hideItemOverlay = true;
				tableCardTexture = _cardBackTexture;
			}

			if (tableCardTexture != null)
			{
				var tableCard = new TextureRect
				{
					Texture = tableCardTexture,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore,
					Position = cardPositions[cardIndex],
					Size = CardVisualSize,
					ZIndex = baseZIndex,
				};

				if (animateCardBack)
				{
					MarkAnimatedCardBack(tableCard);
				}

				_centerArena.AddChild(tableCard);
				if (tableCardData.Card.HasValue && tableCardData.Card.Value.HasMagnetEffect && tableCardData.IsFaceUp)
				{
					AddMagnetOverlay(_centerArena, cardPositions[cardIndex], CardVisualSize, 0.0f, baseZIndex + 1);
				}
			}

			if (!tableCardData.ItemOverlay.HasValue || hideItemOverlay)
			{
				continue;
			}

			string itemCardId = tableCardData.ItemOverlay.Value.Id;
			Vector2 itemVisualSize = GetItemCardVisualSize(itemCardId);
			var itemOverlay = new TextureRect
			{
				Texture = tableCardData.ItemOverlay.Value.Texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = cardPositions[cardIndex] + GetItemCardVisualPosition(itemCardId),
				Size = itemVisualSize,
				PivotOffset = itemVisualSize / 2.0f,
				ZIndex = baseZIndex + 1,
			};

			_centerArena.AddChild(itemOverlay);
		}
	}

	private List<Vector2> BuildTableCardPositions(int cardCount)
	{
		var positions = new List<Vector2>(cardCount);
		if (cardCount <= 0)
		{
			return positions;
		}

		Rect2 layoutRect = GetTableCardLayoutRect();
		float rowGap = Math.Min(TableCardRowGap, Math.Max(0.0f, layoutRect.Size.Y - CardVisualSize.Y));

		for (int cardIndex = 0; cardIndex < cardCount; cardIndex++)
		{
			int rowIndex = cardIndex / TableCardsPerRow;
			int columnIndex = cardIndex % TableCardsPerRow;
			positions.Add(layoutRect.Position + new Vector2(columnIndex * TableCardHorizontalStep, rowIndex * rowGap));
		}

		return positions;
	}

	private Rect2 GetTableCardLayoutRect()
	{
		if (_tableCardLayoutArea == null || !GodotObject.IsInstanceValid(_tableCardLayoutArea))
		{
			return new Rect2(Vector2.Zero, _centerArena.Size);
		}

		Vector2 position = _tableCardLayoutArea.GlobalPosition - _centerArena.GlobalPosition;
		return new Rect2(position, _tableCardLayoutArea.Size);
	}

	private static void RemoveAndQueueFreeChild(Node parent, Node child)
	{
		parent.RemoveChild(child);
		child.QueueFree();
	}
}
