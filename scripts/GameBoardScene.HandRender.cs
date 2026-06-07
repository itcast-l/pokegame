using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private const string HandRenderPoolableSlotMeta = "ocean_hand_render_poolable_slot";
	private const string HandCardVisualNodeName = "HandCardVisual";
	private const int MaxPooledHandCardSlots = MaxPlayers * MaxTotalHandCards;
	private static readonly StyleBoxFlat InputSelectionFrameStyle = CreateInputSelectionFrameStyle();
	private readonly int[] _playerHandRenderSignatures = new int[MaxPlayers];
	private readonly bool[] _playerHandRenderSignaturesInitialized = new bool[MaxPlayers];
	private readonly List<Control> _pooledHandCardSlots = new();

	private void RenderPlayerHands()
	{
		RefreshMainPlayerHandSelection();
		ulong sampleStart = BeginPerformanceSample();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control handArea = _playerHandAreas[playerIndex];
			int renderSignature = BuildPlayerHandRenderSignature(playerIndex, handArea);
			if (_playerHandRenderSignaturesInitialized[playerIndex]
				&& _playerHandRenderSignatures[playerIndex] == renderSignature)
			{
				continue;
			}

			_playerHandRenderSignaturesInitialized[playerIndex] = true;
			_playerHandRenderSignatures[playerIndex] = renderSignature;
			for (int childIndex = handArea.GetChildCount() - 1; childIndex >= 0; childIndex--)
			{
				Node child = handArea.GetChild(childIndex);
				if (child.HasMeta(HandCardSlotMeta))
				{
					ReleaseHandRenderChild(handArea, child);
				}
			}

			handArea.Visible = playerIndex < player_count;
			if (!handArea.Visible)
			{
				continue;
			}

			List<PlayingCard> hand = _playerHands[playerIndex];
			List<ItemCard> itemHand = _playerItemHands[playerIndex];
			int visibleHandCount = GetVisibleHandCardCountForRender(playerIndex, hand.Count);
			int displayCardCount = hand.Count + itemHand.Count;
			Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
			Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
			float cardRotation = GetCardRotationDegreesForPlayer(playerIndex);
			bool canMainPlayerAct = CanMainPlayerAct();
			bool isMagnetSelectionActive = IsMainPlayerMagnetSelectionActive();
			bool isHandLimitDiscardSelectionActive = IsMainPlayerHandLimitDiscardSelectionActive();

			void AddHandCard(
				Texture2D? texture,
				int displayIndex,
				bool isPlayable,
				int handIndex,
				int itemHandIndex,
				bool animateCardBack,
				float visualScale,
				Texture2D? attachmentOverlayTexture = null,
				bool showMagnetSelectionHighlight = false,
				bool showAdventureTutorialHighlight = false,
				bool isAdventureTutorialBlocked = false,
				bool isJellyfishDisabled = false,
				bool showInputSelection = false,
				string tooltipText = "")
			{
				if (texture == null)
				{
					return;
				}

				Vector2 basePosition = firstCardPosition + displayIndex * cardOffset;
				float displayVisualScale = GetHandCardVisualScaleForPlayer(playerIndex) * visualScale;
				Vector2 visualSize = CardVisualSize * displayVisualScale;
				Vector2 visualBasePosition = (CardVisualSize - visualSize) / 2.0f;
				// Main-player slots bind per-render input delegates, so they must not be pooled.
				bool canPoolSlot = playerIndex != MainPlayerIndex && CanPoolHandCardSlot(
					isPlayable,
					attachmentOverlayTexture,
					showMagnetSelectionHighlight,
					showAdventureTutorialHighlight,
					isAdventureTutorialBlocked,
					isJellyfishDisabled,
					showInputSelection,
					tooltipText);
				Control cardSlot = canPoolSlot ? RentHandCardSlot() : CreateHandCardSlot(canPoolSlot);
				TextureRect cardVisual = GetOrCreateHandCardVisual(cardSlot);

				cardSlot.Name = $"HandCard_{displayIndex}";
				cardSlot.MouseFilter = isPlayable || isAdventureTutorialBlocked || isJellyfishDisabled || !string.IsNullOrEmpty(tooltipText)
					? MouseFilterEnum.Stop
					: MouseFilterEnum.Ignore;
				cardSlot.Position = basePosition;
				cardSlot.Size = CardVisualSize;
				cardSlot.Scale = Vector2.One;
				cardSlot.RotationDegrees = 0.0f;
				cardSlot.ZIndex = displayIndex;
				cardSlot.ZAsRelative = true;
				cardSlot.Modulate = Colors.White;
				cardSlot.Visible = !IsHandDisplaySlotHidden(playerIndex, displayIndex);

				cardVisual.Texture = texture;
				cardVisual.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
				cardVisual.MouseFilter = MouseFilterEnum.Ignore;
				cardVisual.Position = visualBasePosition;
				cardVisual.Size = visualSize;
				cardVisual.PivotOffset = visualSize / 2.0f;
				cardVisual.Scale = Vector2.One;
				cardVisual.RotationDegrees = cardRotation;
				cardVisual.ZIndex = 0;
				cardVisual.Modulate = isJellyfishDisabled || isAdventureTutorialBlocked
					? new Color(1.0f, 1.0f, 1.0f, 0.48f)
					: Colors.White;

				if (showInputSelection)
				{
					cardSlot.ZIndex = displayIndex + HoveredHandCardZIndexBoost;
					cardVisual.ZIndex = HoveredHandCardZIndexBoost;
					cardVisual.Position = visualBasePosition + new Vector2(0.0f, -HoveredHandCardLift * 0.55f);
					cardVisual.Scale = Vector2.One * 1.035f;
					AddInputSelectionFrame(cardSlot, cardVisual.Position, visualSize, cardRotation);
				}

				if (showMagnetSelectionHighlight)
				{
					AddMagnetSelectionPulse(cardSlot, visualBasePosition, visualSize, cardRotation);
					StartMagnetSelectionBounce(cardSlot, basePosition);
				}
				else if (showAdventureTutorialHighlight)
				{
					AddMagnetSelectionPulse(cardSlot, visualBasePosition, visualSize, cardRotation);
					StartMagnetSelectionBounce(cardSlot, basePosition);
				}

				if (animateCardBack)
				{
					MarkAnimatedCardBack(cardVisual);
				}
				else
				{
					SetAnimatedCardBackPlayback(cardVisual, false);
				}

				if (playerIndex == MainPlayerIndex)
				{
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

							Vector2 selectedOffset = showInputSelection ? new Vector2(0.0f, -HoveredHandCardLift * 0.55f) : Vector2.Zero;
							Vector2 targetPosition = visualBasePosition + (hovered ? new Vector2(0.0f, -HoveredHandCardLift) : selectedOffset);
							Vector2 targetScale = hovered
								? Vector2.One * HoveredHandCardScale
								: showInputSelection
								? Vector2.One * 1.035f
								: Vector2.One;
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
									cardSlot.ZIndex = showInputSelection ? displayIndex + HoveredHandCardZIndexBoost : displayIndex;
									cardVisual.ZIndex = showInputSelection ? HoveredHandCardZIndexBoost : 0;
								};
							}
						}

						cardSlot.MouseEntered += () =>
						{
							PlayGameSfx(GameSfx.CardHover, -10.0f, 0.96f, 1.04f, 0.05);
							AnimateHover(true);
						};
						cardSlot.MouseExited += () => AnimateHover(false);
					}

					cardSlot.GuiInput += inputEvent =>
					{
						ClearMainPlayerHandSelectionInputOnMouseClick(inputEvent);

						if (showMagnetSelectionHighlight && handIndex >= 0)
						{
							TryCompleteMainPlayerMagnetAttachment(inputEvent, handIndex);
							return;
						}

						if (isHandLimitDiscardSelectionActive)
						{
							TryCompleteMainPlayerHandLimitDiscard(inputEvent, displayIndex);
							return;
						}

						if (itemHandIndex >= 0)
						{
							TryPlayMainPlayerItemCard(inputEvent, itemHandIndex);
							return;
						}

						if (isAdventureTutorialBlocked)
						{
							TryRejectAdventureTutorialCard(inputEvent);
							return;
						}

						if (isPlayable)
						{
							TryPlayMainPlayerCard(inputEvent, handIndex);
						}
					};
				}

				string effectiveTooltipText = isJellyfishDisabled
					? Localize("GAME_CARD_DISABLED_THIS_TURN")
					: isAdventureTutorialBlocked
					? Localize("GAME_TUTORIAL_WRONG_CARD")
					: tooltipText;
				if (!string.IsNullOrEmpty(effectiveTooltipText))
				{
					Label descriptionLabel = CreateCardDescriptionLabel(
						effectiveTooltipText,
						GetHandCardDescriptionPosition(playerIndex),
						new Vector2(204.0f, 42.0f));
					cardSlot.AddChild(descriptionLabel);
					cardSlot.MouseEntered += () =>
					{
						descriptionLabel.Visible = true;
						if (!isPlayable)
						{
							cardSlot.ZIndex = displayIndex + HoveredHandCardZIndexBoost;
						}
					};
					cardSlot.MouseExited += () =>
					{
						descriptionLabel.Visible = showInputSelection;
						if (!isPlayable)
						{
							cardSlot.ZIndex = displayIndex;
						}
					};
					descriptionLabel.Visible = showInputSelection;
				}

				if (attachmentOverlayTexture != null)
				{
					AddCardAttachmentOverlay(cardVisual, attachmentOverlayTexture, Vector2.Zero, visualSize, 0.0f, 1);
				}

				if (isJellyfishDisabled)
				{
					AddDisabledCardProhibitedMark(cardSlot, visualBasePosition, visualSize, cardRotation, 3);
				}

				handArea.AddChild(cardSlot);
			}

			for (int cardIndex = 0; cardIndex < visibleHandCount; cardIndex++)
			{
				PlayingCard card = hand[cardIndex];
				bool showCardBack = playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;
				Texture2D? texture = showCardBack ? _cardBackTexture : card.Texture;
				bool showMagnetSelectionHighlight = isMagnetSelectionActive
					&& playerIndex == MainPlayerIndex
					&& IsAttachableMagnetCard(card, _pendingItemCardId);
				bool isJellyfishDisabled = !isHandLimitDiscardSelectionActive
					&& IsJellyfishDisabledHandDisplaySlot(playerIndex, cardIndex);
				bool isMagnetSelectionCard = showMagnetSelectionHighlight && !isJellyfishDisabled;
				bool isHandLimitDiscardSelectionCard = isHandLimitDiscardSelectionActive
					&& playerIndex == MainPlayerIndex;
				bool showAdventureTutorialHighlight = IsAdventureTutorialHandCardHighlighted(playerIndex, cardIndex);
				bool isAdventureTutorialBlocked = IsAdventureTutorialHandCardBlocked(playerIndex, cardIndex);
				bool isPlayable = isMagnetSelectionCard
					|| isHandLimitDiscardSelectionCard
					|| (!isJellyfishDisabled && playerIndex == MainPlayerIndex && canMainPlayerAct);
				if (playerIndex == MainPlayerIndex && IsAdventureTutorialPlayerActionStepActive())
				{
					isPlayable = isPlayable && showAdventureTutorialHighlight;
				}
				AddHandCard(
					texture,
					cardIndex,
					isPlayable,
					cardIndex,
					-1,
					showCardBack,
					1.0f,
					showCardBack ? null : GetCardAttachmentOverlayTexture(card),
					isMagnetSelectionCard,
					showAdventureTutorialHighlight,
					isAdventureTutorialBlocked,
					isJellyfishDisabled,
					playerIndex == MainPlayerIndex && IsMainPlayerHandDisplayIndexSelected(cardIndex),
					GetHandCardDescription(card, showCardBack));
			}

			for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
			{
				ItemCard itemCard = itemHand[itemIndex];
				bool showCardBack = playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;
				Texture2D? texture = showCardBack ? _cardBackTexture : itemCard.Texture;
				float visualScale = showCardBack ? 1.0f : GetItemCardVisualScale(itemCard.Id);
				int displayIndex = hand.Count + itemIndex;
				bool isJellyfishDisabled = !isHandLimitDiscardSelectionActive
					&& IsJellyfishDisabledHandDisplaySlot(playerIndex, displayIndex);
				bool showAdventureTutorialHighlight = IsAdventureTutorialItemCardHighlighted(playerIndex, itemIndex);
				bool isAdventureTutorialBlocked = IsAdventureTutorialItemCardBlocked(playerIndex, itemIndex);
				bool isPlayable = playerIndex == MainPlayerIndex
					&& (isHandLimitDiscardSelectionActive || (!isJellyfishDisabled && canMainPlayerAct && CanPlayItemCard(playerIndex, itemCard.Id)));
				if (playerIndex == MainPlayerIndex && IsAdventureTutorialPlayerActionStepActive())
				{
					isPlayable = isPlayable && showAdventureTutorialHighlight;
				}
				string tooltipText = !showCardBack && _itemCardDescriptions.TryGetValue(itemCard.Id, out string? description)
					? description
					: string.Empty;
				AddHandCard(
					texture,
					displayIndex,
					isPlayable,
					-1,
					itemIndex,
					showCardBack,
					visualScale,
					showAdventureTutorialHighlight: showAdventureTutorialHighlight,
					isAdventureTutorialBlocked: isAdventureTutorialBlocked,
					isJellyfishDisabled: isJellyfishDisabled,
					showInputSelection: playerIndex == MainPlayerIndex && IsMainPlayerHandDisplayIndexSelected(displayIndex),
					tooltipText: tooltipText);
			}
		}

		RefreshAdventureTutorialOverlayLayout();
		EndPerformanceSample("RenderPlayerHands", sampleStart);
	}

	private static bool CanPoolHandCardSlot(
		bool isPlayable,
		Texture2D? attachmentOverlayTexture,
		bool showMagnetSelectionHighlight,
		bool showAdventureTutorialHighlight,
		bool isAdventureTutorialBlocked,
		bool isJellyfishDisabled,
		bool showInputSelection,
		string tooltipText)
	{
		return !isPlayable
			&& attachmentOverlayTexture == null
			&& !showMagnetSelectionHighlight
			&& !showAdventureTutorialHighlight
			&& !isAdventureTutorialBlocked
			&& !isJellyfishDisabled
			&& !showInputSelection
			&& string.IsNullOrEmpty(tooltipText);
	}

	private static void AddInputSelectionFrame(Control cardSlot, Vector2 visualPosition, Vector2 visualSize, float rotationDegrees)
	{
		Vector2 padding = new(7.0f, 8.0f);
		var frame = new Panel
		{
			Name = "InputSelectionFrame",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = visualPosition - padding,
			Size = visualSize + padding * 2.0f,
			PivotOffset = (visualSize + padding * 2.0f) / 2.0f,
			RotationDegrees = rotationDegrees,
			ZIndex = -1,
			ZAsRelative = true,
		};
		frame.AddThemeStyleboxOverride("panel", InputSelectionFrameStyle);
		cardSlot.AddChild(frame);
		cardSlot.MoveChild(frame, 0);
	}

	private static StyleBoxFlat CreateInputSelectionFrameStyle()
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.78f, 0.92f, 0.10f),
			BorderColor = new Color(0.88f, 1.0f, 0.62f, 0.96f),
			BorderWidthLeft = 2,
			BorderWidthTop = 2,
			BorderWidthRight = 2,
			BorderWidthBottom = 2,
			CornerRadiusTopLeft = 7,
			CornerRadiusTopRight = 7,
			CornerRadiusBottomRight = 7,
			CornerRadiusBottomLeft = 7,
			ShadowColor = new Color(0.20f, 0.90f, 1.0f, 0.32f),
			ShadowSize = 14,
		};
	}

	private Control CreateHandCardSlot(bool poolable)
	{
		var cardSlot = new Control();
		cardSlot.SetMeta(HandCardSlotMeta, true);
		if (poolable)
		{
			cardSlot.SetMeta(HandRenderPoolableSlotMeta, true);
			cardSlot.AddChild(CreateHandCardVisual());
		}

		return cardSlot;
	}

	private Control RentHandCardSlot()
	{
		int lastIndex = _pooledHandCardSlots.Count - 1;
		if (lastIndex < 0)
		{
			return CreateHandCardSlot(poolable: true);
		}

		Control cardSlot = _pooledHandCardSlots[lastIndex];
		_pooledHandCardSlots.RemoveAt(lastIndex);
		return cardSlot;
	}

	private void ReleaseHandRenderChild(Node parent, Node child)
	{
		if (child is Control cardSlot && cardSlot.HasMeta(HandRenderPoolableSlotMeta))
		{
			parent.RemoveChild(cardSlot);
			ResetPooledHandCardSlot(cardSlot);
			if (_pooledHandCardSlots.Count < MaxPooledHandCardSlots)
			{
				_pooledHandCardSlots.Add(cardSlot);
			}
			else
			{
				cardSlot.QueueFree();
			}

			return;
		}

		RemoveAndQueueFreeChild(parent, child);
	}

	private TextureRect GetOrCreateHandCardVisual(Control cardSlot)
	{
		if (cardSlot.GetNodeOrNull<TextureRect>(HandCardVisualNodeName) is TextureRect existingVisual)
		{
			return existingVisual;
		}

		TextureRect visual = CreateHandCardVisual();
		cardSlot.AddChild(visual);
		return visual;
	}

	private static TextureRect CreateHandCardVisual()
	{
		return new TextureRect
		{
			Name = HandCardVisualNodeName,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
		};
	}

	private void ResetPooledHandCardSlot(Control cardSlot)
	{
		cardSlot.Name = "PooledHandCard";
		cardSlot.Visible = false;
		cardSlot.MouseFilter = MouseFilterEnum.Ignore;
		cardSlot.Position = Vector2.Zero;
		cardSlot.Size = CardVisualSize;
		cardSlot.PivotOffset = Vector2.Zero;
		cardSlot.Scale = Vector2.One;
		cardSlot.RotationDegrees = 0.0f;
		cardSlot.ZIndex = 0;
		cardSlot.ZAsRelative = true;
		cardSlot.Modulate = Colors.White;

		for (int childIndex = cardSlot.GetChildCount() - 1; childIndex >= 0; childIndex--)
		{
			Node child = cardSlot.GetChild(childIndex);
			if (child is TextureRect textureRect && child.Name.ToString() == HandCardVisualNodeName)
			{
				ResetPooledHandCardVisual(textureRect);
				continue;
			}

			RemoveAndQueueFreeChild(cardSlot, child);
		}
	}

	private static void ResetPooledHandCardVisual(TextureRect textureRect)
	{
		SetAnimatedCardBackPlayback(textureRect, false);
		textureRect.Texture = null;
		textureRect.Visible = true;
		textureRect.MouseFilter = MouseFilterEnum.Ignore;
		textureRect.Position = Vector2.Zero;
		textureRect.Size = Vector2.Zero;
		textureRect.PivotOffset = Vector2.Zero;
		textureRect.Scale = Vector2.One;
		textureRect.RotationDegrees = 0.0f;
		textureRect.ZIndex = 0;
		textureRect.ZAsRelative = true;
		textureRect.Modulate = Colors.White;
	}

	private int BuildPlayerHandRenderSignature(int playerIndex, Control handArea)
	{
		var signature = new HashCode();
		bool playerVisible = playerIndex < player_count;
		signature.Add(playerVisible);
		if (!playerVisible)
		{
			return signature.ToHashCode();
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		int visibleHandCount = GetVisibleHandCardCountForRender(playerIndex, hand.Count);
		bool canMainPlayerAct = CanMainPlayerAct();
		bool isMagnetSelectionActive = IsMainPlayerMagnetSelectionActive();
		bool isHandLimitDiscardSelectionActive = IsMainPlayerHandLimitDiscardSelectionActive();
		bool showCardBack = playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;

		signature.Add(handArea.Size.X);
		signature.Add(handArea.Size.Y);
		signature.Add(player_count);
		signature.Add(visibleHandCount);
		signature.Add(hand.Count);
		signature.Add(itemHand.Count);
		signature.Add(canMainPlayerAct);
		signature.Add(isMagnetSelectionActive);
		signature.Add(isHandLimitDiscardSelectionActive);
		signature.Add(_revealedPlayerHandIndex);
		signature.Add(_pendingItemSelectionMode);
		signature.Add(_pendingItemOwnerIndex);
		signature.Add(_pendingItemCardId);
		signature.Add(_isOpeningDealAnimationPlaying);
		if (playerIndex == MainPlayerIndex)
		{
			signature.Add(_selectedMainPlayerHandDisplayIndex);
		}
		signature.Add(TranslationServer.GetLocale());
		AddAdventureTutorialHandRenderSignature(ref signature, playerIndex);

		for (int cardIndex = 0; cardIndex < visibleHandCount; cardIndex++)
		{
			PlayingCard card = hand[cardIndex];
			bool showMagnetSelectionHighlight = isMagnetSelectionActive
				&& playerIndex == MainPlayerIndex
				&& IsAttachableMagnetCard(card, _pendingItemCardId);
			bool isJellyfishDisabled = !isHandLimitDiscardSelectionActive
				&& IsJellyfishDisabledHandDisplaySlot(playerIndex, cardIndex);
			bool isMagnetSelectionCard = showMagnetSelectionHighlight && !isJellyfishDisabled;
			bool isHandLimitDiscardSelectionCard = isHandLimitDiscardSelectionActive
				&& playerIndex == MainPlayerIndex;
			bool showAdventureTutorialHighlight = IsAdventureTutorialHandCardHighlighted(playerIndex, cardIndex);
			bool isAdventureTutorialBlocked = IsAdventureTutorialHandCardBlocked(playerIndex, cardIndex);
			bool isPlayable = isMagnetSelectionCard
				|| isHandLimitDiscardSelectionCard
				|| (!isJellyfishDisabled && playerIndex == MainPlayerIndex && canMainPlayerAct);
			if (playerIndex == MainPlayerIndex && IsAdventureTutorialPlayerActionStepActive())
			{
				isPlayable = isPlayable && showAdventureTutorialHighlight;
			}

			signature.Add(cardIndex);
			signature.Add(card.Suit);
			signature.Add(card.Rank);
			signature.Add(card.AttachedItemCardIdsCsv);
			signature.Add(showCardBack);
			signature.Add(isPlayable);
			signature.Add(isMagnetSelectionCard);
			signature.Add(showAdventureTutorialHighlight);
			signature.Add(isAdventureTutorialBlocked);
			signature.Add(isJellyfishDisabled);
			signature.Add(IsHandDisplaySlotHidden(playerIndex, cardIndex));
			signature.Add(!showCardBack && ShouldPlayedCardCollectTable(card));
			AddTextureSignature(ref signature, showCardBack ? _cardBackTexture : card.Texture);
			AddTextureSignature(ref signature, showCardBack ? null : GetCardAttachmentOverlayTexture(card));
		}

		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			ItemCard itemCard = itemHand[itemIndex];
			int displayIndex = hand.Count + itemIndex;
			bool isJellyfishDisabled = !isHandLimitDiscardSelectionActive
				&& IsJellyfishDisabledHandDisplaySlot(playerIndex, displayIndex);
			bool showAdventureTutorialHighlight = IsAdventureTutorialItemCardHighlighted(playerIndex, itemIndex);
			bool isAdventureTutorialBlocked = IsAdventureTutorialItemCardBlocked(playerIndex, itemIndex);
			bool isPlayable = playerIndex == MainPlayerIndex
				&& (isHandLimitDiscardSelectionActive || (!isJellyfishDisabled && canMainPlayerAct && CanPlayItemCard(playerIndex, itemCard.Id)));
			if (playerIndex == MainPlayerIndex && IsAdventureTutorialPlayerActionStepActive())
			{
				isPlayable = isPlayable && showAdventureTutorialHighlight;
			}

			signature.Add(itemIndex);
			signature.Add(itemCard.Id);
			signature.Add(displayIndex);
			signature.Add(showCardBack);
			signature.Add(showCardBack ? 1.0f : GetItemCardVisualScale(itemCard.Id));
			signature.Add(isPlayable);
			signature.Add(showAdventureTutorialHighlight);
			signature.Add(isAdventureTutorialBlocked);
			signature.Add(isJellyfishDisabled);
			signature.Add(IsHandDisplaySlotHidden(playerIndex, displayIndex));
			signature.Add(!showCardBack && _itemCardDescriptions.ContainsKey(itemCard.Id));
			AddTextureSignature(ref signature, showCardBack ? _cardBackTexture : itemCard.Texture);
		}

		return signature.ToHashCode();
	}

	private static void AddTextureSignature(ref HashCode signature, Texture2D? texture)
	{
		if (texture == null)
		{
			signature.Add(0UL);
			return;
		}

		signature.Add(texture.GetInstanceId());
	}
}
