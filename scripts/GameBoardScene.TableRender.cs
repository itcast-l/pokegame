using System;
using Godot;

public partial class GameBoardScene
{
	private static readonly StyleBoxFlat SandBurialOverlayStyle = CreateSandBurialOverlayStyle();
	private int _tableRenderSignature;
	private bool _tableRenderSignatureInitialized;

	private void RenderTableCards()
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		ulong sampleStart = BeginPerformanceSample();
		int renderSignature = BuildTableRenderSignature();
		if (_tableRenderSignatureInitialized
			&& _tableRenderSignature == renderSignature
			&& CanSkipTableRender())
		{
			EndPerformanceSample("RenderTableCards(skip)", sampleStart);
			return;
		}

		_tableRenderSignatureInitialized = true;
		_tableRenderSignature = renderSignature;
		for (int childIndex = _centerArena.GetChildCount() - 1; childIndex >= 0; childIndex--)
		{
			Node child = _centerArena.GetChild(childIndex);
			ReleaseTableRenderChild(_centerArena, child);
		}

		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		int oldestAnimatedTableCardBackIndex = GetOldestAnimatedTableCardBackIndex();
		for (int cardIndex = 0; cardIndex < _tableCards.Count; cardIndex++)
		{
			TableCard tableCardData = _tableCards[cardIndex];
			if (IsTableCardHiddenForPresentation(tableCardData))
			{
				continue;
			}

			Vector2 cardPosition = GetTableCardPosition(cardIndex, tableLayoutRect);
			int baseZIndex = cardIndex * 2;
			Texture2D? tableCardTexture = null;
			bool animateCardBack = false;
			bool hideItemOverlay = false;
			bool showMainPlayerPreview = ShouldShowMainPlayerTablePreview(tableCardData);
			if (tableCardData.Card.HasValue)
			{
				animateCardBack = cardIndex >= oldestAnimatedTableCardBackIndex
					&& CanAnimateTableCardBack(tableCardData);
				tableCardTexture = showMainPlayerPreview
					? GetPlayingCardTexture(tableCardData.Card.Value)
					: tableCardData.IsFaceUp ? GetPlayingCardTexture(tableCardData.Card.Value) : _cardBackTexture;
			}
			else if (tableCardData.ItemOverlay.HasValue && !tableCardData.IsFaceUp && _cardBackTexture != null)
			{
				animateCardBack = cardIndex >= oldestAnimatedTableCardBackIndex
					&& CanAnimateTableCardBack(tableCardData);
				hideItemOverlay = true;
				tableCardTexture = _cardBackTexture;
			}

			if (tableCardTexture != null)
			{
				Label? descriptionLabel = null;
				TextureRect tableCard = RentTableRenderTextureRect("TableCard");
				tableCard.Texture = tableCardTexture;
				tableCard.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
				tableCard.Position = cardPosition;
				tableCard.Size = CardVisualSize;
				tableCard.PivotOffset = CardVisualSize / 2.0f;
				tableCard.ZIndex = baseZIndex;
				tableCard.Modulate = tableCardData.IsBuriedInSand
					? new Color(0.78f, 0.66f, 0.44f, 0.82f)
					: showMainPlayerPreview
					? new Color(1.0f, 1.0f, 1.0f, MainPlayerTablePreviewAlpha)
					: Colors.White;
				if (tableCardData.Card.HasValue)
				{
					string tooltipText = GetAttachmentCardDescription(tableCardData.Card.Value, !tableCardData.IsFaceUp);
					if (!string.IsNullOrEmpty(tooltipText))
					{
						tableCard.MouseFilter = MouseFilterEnum.Stop;
						var labelSize = TableCardDescriptionLabelSize;
						descriptionLabel = CreateCardDescriptionLabel(
							tooltipText,
							GetTableCardDescriptionPosition(cardPosition, labelSize),
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
					AddSandBurialOverlay(cardPosition, baseZIndex + 1);
				}

				Texture2D? slimeOverlayTexture = tableCardData.IsSlimeCoated ? _slimeCardOverlayTexture : null;
				if (slimeOverlayTexture != null)
				{
					AddTableCardAttachmentOverlay(
						slimeOverlayTexture,
						cardPosition,
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
					AddTableCardAttachmentOverlay(
						attachmentOverlayTexture,
						cardPosition,
						CardVisualSize,
						0.0f,
						baseZIndex + 1,
						showMainPlayerPreview ? MainPlayerTablePreviewAlpha : 1.0f);
				}

				if (ReferenceEquals(tableCardData, GetAdventureTraceHuntMarkedTableCard()))
				{
					AddAdventureTraceHuntMarkerOverlay(cardPosition, baseZIndex + 1);
				}
			}

			if (!tableCardData.ItemOverlay.HasValue || hideItemOverlay)
			{
				continue;
			}

			string itemCardId = tableCardData.ItemOverlay.Value.Id;
			Vector2 itemVisualSize = GetItemCardVisualSize(itemCardId);
			TextureRect itemOverlay = RentTableRenderTextureRect("TableItemOverlay");
			itemOverlay.Texture = GetItemCardTexture(tableCardData.ItemOverlay.Value);
			itemOverlay.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
			itemOverlay.Position = cardPosition + GetItemCardVisualPosition(itemCardId);
			itemOverlay.Size = itemVisualSize;
			itemOverlay.PivotOffset = itemVisualSize / 2.0f;
			itemOverlay.ZIndex = baseZIndex + 1;

			_centerArena.AddChild(itemOverlay);
		}
		AddAdventureTutorialTableHighlights(tableLayoutRect);
		EndPerformanceSample("RenderTableCards", sampleStart);
	}

	private int BuildTableRenderSignature()
	{
		var signature = new HashCode();
		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		signature.Add(_tableCards.Count);
		signature.Add(tableLayoutRect.Position.X);
		signature.Add(tableLayoutRect.Position.Y);
		signature.Add(tableLayoutRect.Size.X);
		signature.Add(tableLayoutRect.Size.Y);
		signature.Add(_centerArena.Size.X);
		signature.Add(_centerArena.Size.Y);
		signature.Add(TranslationServer.GetLocale());
		AddAdventureTutorialTableRenderSignature(ref signature);
		AddTextureSignature(ref signature, _cardBackTexture);
		AddTextureSignature(ref signature, _slimeCardOverlayTexture);

		for (int cardIndex = 0; cardIndex < _tableCards.Count; cardIndex++)
		{
			TableCard tableCard = _tableCards[cardIndex];
			signature.Add(cardIndex);
			signature.Add(ReferenceEquals(tableCard, GetAdventureTraceHuntMarkedTableCard()));
			signature.Add(tableCard.IsFaceUp);
			signature.Add(tableCard.IsHiddenDuringCapture);
			signature.Add(_presentationHiddenTableCardRuntimeInstanceIds.Contains(tableCard.InstanceId));
			signature.Add(tableCard.IsBuriedInSand);
			signature.Add(tableCard.IsSlimeCoated);
			signature.Add(tableCard.ItemOwnerPlayerIndex);
			signature.Add(tableCard.CardOwnerPlayerIndex);
			signature.Add(tableCard.RemoveCardOnCapture);
			if (tableCard.Card.HasValue)
			{
				PlayingCard card = tableCard.Card.Value;
				signature.Add(card.Suit);
				signature.Add(card.Rank);
				signature.Add(card.AttachedItemCardIdsCsv);
				AddTextureSignature(ref signature, GetPlayingCardTexture(card));
				AddTextureSignature(ref signature, GetCardAttachmentOverlayTexture(card));
			}
			else
			{
				signature.Add(string.Empty);
				signature.Add(string.Empty);
				signature.Add(string.Empty);
				AddTextureSignature(ref signature, null);
				AddTextureSignature(ref signature, null);
			}

			if (tableCard.ItemOverlay.HasValue)
			{
				ItemCard itemOverlay = tableCard.ItemOverlay.Value;
				signature.Add(itemOverlay.Id);
				AddTextureSignature(ref signature, GetItemCardTexture(itemOverlay));
			}
			else
			{
				signature.Add(string.Empty);
				AddTextureSignature(ref signature, null);
			}
		}

		return signature.ToHashCode();
	}

	private bool CanSkipTableRender()
	{
		for (int childIndex = 0; childIndex < _centerArena.GetChildCount(); childIndex++)
		{
			if (!IsTableRenderManagedChild(_centerArena.GetChild(childIndex)))
			{
				return false;
			}
		}

		return true;
	}

	private static bool IsTableRenderManagedChild(Node child)
	{
		return child is TextureRect textureRect && textureRect.HasMeta(TableRenderPooledTextureMeta)
			|| child is Panel panel && panel.HasMeta(TableRenderPooledPanelMeta)
			|| IsAdventureTutorialTableHighlight(child)
			|| child is Label label && label.Name.ToString() == "CardDescriptionLabel";
	}

	private int GetOldestAnimatedTableCardBackIndex()
	{
		int animatedCardBackCount = 0;
		for (int cardIndex = _tableCards.Count - 1; cardIndex >= 0; cardIndex--)
		{
			TableCard tableCard = _tableCards[cardIndex];
				if (IsTableCardHiddenForPresentation(tableCard) || !CanAnimateTableCardBack(tableCard))
			{
				continue;
			}

			animatedCardBackCount++;
			if (animatedCardBackCount >= MaxAnimatedTableCardBacks)
			{
				return cardIndex;
			}
		}

		return animatedCardBackCount > 0 ? 0 : int.MaxValue;
	}

	private bool CanAnimateTableCardBack(TableCard tableCard)
	{
		return _cardBackTexture != null
			&& !tableCard.IsFaceUp
			&& !ShouldShowMainPlayerTablePreview(tableCard)
			&& (tableCard.Card.HasValue || tableCard.ItemOverlay.HasValue);
	}

	private bool IsTableCardHiddenForPresentation(TableCard tableCard)
	{
		return tableCard.IsHiddenDuringCapture
			|| _presentationHiddenTableCardRuntimeInstanceIds.Contains(tableCard.InstanceId);
	}

	private void AddSandBurialOverlay(Vector2 cardPosition, int zIndex)
	{
		Panel sandOverlay = RentTableRenderPanel("SandBurialOverlay");
		sandOverlay.Position = cardPosition;
		sandOverlay.Size = CardVisualSize;
		sandOverlay.ZIndex = zIndex;
		_centerArena.AddChild(sandOverlay);
	}

	private void AddTableCardAttachmentOverlay(
		Texture2D overlayTexture,
		Vector2 position,
		Vector2 size,
		float rotationDegrees = 0.0f,
		int zIndex = 1,
		float alpha = 1.0f)
	{
		TextureRect overlay = RentTableRenderTextureRect("TableCardAttachmentOverlay");
		overlay.Texture = overlayTexture;
		overlay.TextureFilter = CanvasItem.TextureFilterEnum.Linear;
		overlay.Position = position;
		overlay.Size = size;
		overlay.PivotOffset = size / 2.0f;
		overlay.RotationDegrees = rotationDegrees;
		overlay.ZIndex = zIndex;
		overlay.Modulate = new Color(1.0f, 1.0f, 1.0f, alpha);
		_centerArena.AddChild(overlay);
	}

	private TextureRect RentTableRenderTextureRect(string nodeName)
	{
		TextureRect textureRect;
		int lastIndex = _tableRenderTexturePool.Count - 1;
		if (lastIndex >= 0)
		{
			textureRect = _tableRenderTexturePool[lastIndex];
			_tableRenderTexturePool.RemoveAt(lastIndex);
		}
		else
		{
			textureRect = new TextureRect
			{
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				MouseFilter = MouseFilterEnum.Ignore,
			};
			textureRect.SetMeta(TableRenderPooledTextureMeta, true);
		}

		textureRect.Name = nodeName;
		textureRect.Visible = true;
		textureRect.Scale = Vector2.One;
		textureRect.RotationDegrees = 0.0f;
		textureRect.ZAsRelative = true;
		textureRect.MouseFilter = MouseFilterEnum.Ignore;
		textureRect.Modulate = Colors.White;
		return textureRect;
	}

	private Panel RentTableRenderPanel(string nodeName)
	{
		Panel panel;
		int lastIndex = _tableRenderPanelPool.Count - 1;
		if (lastIndex >= 0)
		{
			panel = _tableRenderPanelPool[lastIndex];
			_tableRenderPanelPool.RemoveAt(lastIndex);
		}
		else
		{
			panel = new Panel
			{
				MouseFilter = MouseFilterEnum.Ignore,
			};
			panel.SetMeta(TableRenderPooledPanelMeta, true);
			panel.AddThemeStyleboxOverride("panel", SandBurialOverlayStyle);
		}

		panel.Name = nodeName;
		panel.Visible = true;
		panel.Scale = Vector2.One;
		panel.RotationDegrees = 0.0f;
		panel.ZAsRelative = true;
		panel.MouseFilter = MouseFilterEnum.Ignore;
		panel.Modulate = Colors.White;
		return panel;
	}

	private void ReleaseTableRenderChild(Node parent, Node child)
	{
		if (child is TextureRect textureRect && CanPoolTableRenderTexture(textureRect))
		{
			parent.RemoveChild(textureRect);
			ResetTableRenderTextureRect(textureRect);
			_tableRenderTexturePool.Add(textureRect);
			return;
		}

		if (child is Panel panel && panel.HasMeta(TableRenderPooledPanelMeta))
		{
			parent.RemoveChild(panel);
			ResetTableRenderPanel(panel);
			_tableRenderPanelPool.Add(panel);
			return;
		}

		RemoveAndQueueFreeChild(parent, child);
	}

	private static bool CanPoolTableRenderTexture(TextureRect textureRect)
	{
		if (!textureRect.HasMeta(TableRenderPooledTextureMeta)
			|| textureRect.MouseFilter != MouseFilterEnum.Ignore)
		{
			return false;
		}

		if (textureRect.GetChildCount() == 0)
		{
			return true;
		}

		return textureRect.HasMeta(CardBackAnimatedMeta)
			&& textureRect.GetChildCount() == 1
			&& textureRect.GetChild(0) is AnimationPlayer;
	}

	private static void ResetTableRenderTextureRect(TextureRect textureRect)
	{
		SetAnimatedCardBackPlayback(textureRect, false);
		textureRect.Texture = null;
		textureRect.Visible = false;
		textureRect.Position = Vector2.Zero;
		textureRect.Size = Vector2.Zero;
		textureRect.PivotOffset = Vector2.Zero;
		textureRect.Scale = Vector2.One;
		textureRect.RotationDegrees = 0.0f;
		textureRect.ZIndex = 0;
		textureRect.ZAsRelative = true;
		textureRect.Modulate = Colors.White;
		textureRect.MouseFilter = MouseFilterEnum.Ignore;
	}

	private static void ResetTableRenderPanel(Panel panel)
	{
		panel.Visible = false;
		panel.Position = Vector2.Zero;
		panel.Size = Vector2.Zero;
		panel.PivotOffset = Vector2.Zero;
		panel.Scale = Vector2.One;
		panel.RotationDegrees = 0.0f;
		panel.ZIndex = 0;
		panel.ZAsRelative = true;
		panel.Modulate = Colors.White;
		panel.MouseFilter = MouseFilterEnum.Ignore;
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

	private bool ShouldShowMainPlayerTablePreview(TableCard tableCard)
	{
		return tableCard.Card.HasValue
			&& !tableCard.IsFaceUp
			&& IsLocalHandPlayer(tableCard.CardOwnerPlayerIndex);
	}

	private bool TryGetTableCardPosition(int tableCardIndex, out Vector2 position)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			position = default;
			return false;
		}

		position = GetTableCardPosition(tableCardIndex, GetTableCardLayoutRect());
		return true;
	}

	private static Vector2 GetTableCardPosition(int cardIndex, Rect2 layoutRect)
	{
		float rowGap = Math.Min(TableCardRowGap, Math.Max(0.0f, layoutRect.Size.Y - CardVisualSize.Y));
		int rowIndex = cardIndex / TableCardsPerRow;
		int columnIndex = cardIndex % TableCardsPerRow;
		return layoutRect.Position + new Vector2(columnIndex * TableCardHorizontalStep, rowIndex * rowGap);
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
