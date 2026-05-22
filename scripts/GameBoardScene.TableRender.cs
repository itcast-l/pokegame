using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
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
			bool showMainPlayerPreview = ShouldShowMainPlayerTablePreview(tableCardData);
			if (tableCardData.Card.HasValue)
			{
				animateCardBack = !showMainPlayerPreview && !tableCardData.IsFaceUp && _cardBackTexture != null;
				tableCardTexture = showMainPlayerPreview
					? tableCardData.Card.Value.Texture
					: tableCardData.IsFaceUp ? tableCardData.Card.Value.Texture : _cardBackTexture;
			}
			else if (tableCardData.ItemOverlay.HasValue && !tableCardData.IsFaceUp && _cardBackTexture != null)
			{
				animateCardBack = true;
				hideItemOverlay = true;
				tableCardTexture = _cardBackTexture;
			}

			if (tableCardTexture != null)
			{
				Label? descriptionLabel = null;
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
					Modulate = showMainPlayerPreview
						? new Color(1.0f, 1.0f, 1.0f, MainPlayerTablePreviewAlpha)
						: Colors.White,
				};
				if (tableCardData.Card.HasValue)
				{
					string tooltipText = GetAttachmentCardDescription(tableCardData.Card.Value, !tableCardData.IsFaceUp);
					if (!string.IsNullOrEmpty(tooltipText))
					{
						tableCard.MouseFilter = MouseFilterEnum.Stop;
						var labelSize = new Vector2(218.0f, 44.0f);
						descriptionLabel = CreateCardDescriptionLabel(
							tooltipText,
							GetTableCardDescriptionPosition(cardPositions[cardIndex], labelSize),
							labelSize);
						tableCard.MouseEntered += () => descriptionLabel.Visible = true;
						tableCard.MouseExited += () => descriptionLabel.Visible = false;
					}
				}

				if (animateCardBack)
				{
					MarkAnimatedCardBack(tableCard);
				}

				_centerArena.AddChild(tableCard);
				if (descriptionLabel != null)
				{
					_centerArena.AddChild(descriptionLabel);
				}
				Texture2D? attachmentOverlayTexture = tableCardData.Card.HasValue && (tableCardData.IsFaceUp || showMainPlayerPreview)
					? GetCardAttachmentOverlayTexture(tableCardData.Card.Value)
					: null;
				if (attachmentOverlayTexture != null)
				{
					AddCardAttachmentOverlay(
						_centerArena,
						attachmentOverlayTexture,
						cardPositions[cardIndex],
						CardVisualSize,
						0.0f,
						baseZIndex + 1,
						showMainPlayerPreview ? MainPlayerTablePreviewAlpha : 1.0f);
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

	private static bool ShouldShowMainPlayerTablePreview(TableCard tableCard)
	{
		return tableCard.Card.HasValue
			&& !tableCard.IsFaceUp
			&& tableCard.CardOwnerPlayerIndex == MainPlayerIndex;
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
}
