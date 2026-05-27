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
					Modulate = tableCardData.IsBuriedInSand
						? new Color(0.78f, 0.66f, 0.44f, 0.82f)
						: showMainPlayerPreview
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

				if (tableCardData.IsBuriedInSand)
				{
					AddSandBurialOverlay(cardPositions[cardIndex], baseZIndex + 1);
				}

				Texture2D? slimeOverlayTexture = tableCardData.IsSlimeCoated ? _slimeCardOverlayTexture : null;
				if (slimeOverlayTexture != null)
				{
					AddCardAttachmentOverlay(
						_centerArena,
						slimeOverlayTexture,
						cardPositions[cardIndex],
						CardVisualSize,
						0.0f,
						baseZIndex + 1,
						showMainPlayerPreview ? MainPlayerTablePreviewAlpha : 1.0f);
				}

				Texture2D? attachmentOverlayTexture = slimeOverlayTexture == null && tableCardData.Card.HasValue && (tableCardData.IsFaceUp || showMainPlayerPreview)
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

	private void AddSandBurialOverlay(Vector2 cardPosition, int zIndex)
	{
		var sandOverlay = new Panel
		{
			Name = "SandBurialOverlay",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = cardPosition,
			Size = CardVisualSize,
			ZIndex = zIndex,
		};
		sandOverlay.AddThemeStyleboxOverride("panel", CreateSandBurialOverlayStyle());
		_centerArena.AddChild(sandOverlay);
	}

	private static StyleBoxFlat CreateSandBurialOverlayStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.66f, 0.50f, 0.24f, 0.30f),
			BorderColor = new Color(0.96f, 0.78f, 0.38f, 0.62f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ShadowColor = new Color(0.78f, 0.54f, 0.18f, 0.32f),
			ShadowSize = 12,
		};
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
