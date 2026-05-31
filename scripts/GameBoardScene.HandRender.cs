using System.Collections.Generic;
using System.Text;
using Godot;

public partial class GameBoardScene
{
	private readonly string?[] _playerHandRenderSignatures = new string?[MaxPlayers];

	private void RenderPlayerHands()
	{
				for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
				{
					Control handArea = _playerHandAreas[playerIndex];
					string renderSignature = BuildPlayerHandRenderSignature(playerIndex, handArea);
					if (_playerHandRenderSignatures[playerIndex] == renderSignature)
					{
						continue;
					}

					_playerHandRenderSignatures[playerIndex] = renderSignature;
					for (int childIndex = handArea.GetChildCount() - 1; childIndex >= 0; childIndex--)
					{
						Node child = handArea.GetChild(childIndex);
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
				bool isJellyfishDisabled = false,
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
					var cardSlot = new Control
					{
						Name = $"HandCard_{displayIndex}",
						MouseFilter = isPlayable || isJellyfishDisabled || !string.IsNullOrEmpty(tooltipText) ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore,
						Position = basePosition,
						Size = CardVisualSize,
						ZIndex = displayIndex,
					Visible = !IsHandDisplaySlotHidden(playerIndex, displayIndex),
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
					Modulate = isJellyfishDisabled ? new Color(1.0f, 1.0f, 1.0f, 0.48f) : Colors.White,
				};

				if (showMagnetSelectionHighlight)
				{
					AddMagnetSelectionPulse(cardSlot, visualBasePosition, visualSize, cardRotation);
					StartMagnetSelectionBounce(cardSlot, basePosition);
				}

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

						cardSlot.MouseEntered += () =>
						{
							PlayGameSfx(GameSfx.CardHover, -10.0f, 0.96f, 1.04f, 0.05);
							AnimateHover(true);
						};
						cardSlot.MouseExited += () => AnimateHover(false);

						cardSlot.GuiInput += inputEvent =>
						{
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

						TryPlayMainPlayerCard(inputEvent, handIndex);
					};
				}

				string effectiveTooltipText = isJellyfishDisabled ? Localize("GAME_CARD_DISABLED_THIS_TURN") : tooltipText;
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
							descriptionLabel.Visible = false;
							if (!isPlayable)
							{
								cardSlot.ZIndex = displayIndex;
							}
					};
				}

				if (attachmentOverlayTexture != null)
				{
					AddCardAttachmentOverlay(cardVisual, attachmentOverlayTexture, Vector2.Zero, visualSize, 0.0f, 1);
				}

				cardSlot.AddChild(cardVisual);
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
						bool isPlayable = isMagnetSelectionCard
							|| isHandLimitDiscardSelectionCard
							|| (!isJellyfishDisabled && playerIndex == MainPlayerIndex && canMainPlayerAct);
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
					isJellyfishDisabled,
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
					bool isPlayable = playerIndex == MainPlayerIndex
						&& (isHandLimitDiscardSelectionActive || (!isJellyfishDisabled && canMainPlayerAct && CanPlayItemCard(playerIndex, itemCard.Id)));
					string tooltipText = !showCardBack && _itemCardDescriptions.TryGetValue(itemCard.Id, out string? description)
						? description
						: string.Empty;
					AddHandCard(texture, displayIndex, isPlayable, -1, itemIndex, showCardBack, visualScale, isJellyfishDisabled: isJellyfishDisabled, tooltipText: tooltipText);
				}
			}
		}

	private string BuildPlayerHandRenderSignature(int playerIndex, Control handArea)
	{
		var signature = new StringBuilder(512);
		bool playerVisible = playerIndex < player_count;
		signature.Append("visible=").Append(playerVisible);
		if (!playerVisible)
		{
			return signature.ToString();
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		List<ItemCard> itemHand = _playerItemHands[playerIndex];
		int visibleHandCount = GetVisibleHandCardCountForRender(playerIndex, hand.Count);
		bool canMainPlayerAct = CanMainPlayerAct();
		bool isMagnetSelectionActive = IsMainPlayerMagnetSelectionActive();
		bool isHandLimitDiscardSelectionActive = IsMainPlayerHandLimitDiscardSelectionActive();
		bool showCardBack = playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;

		signature.Append("|size=").Append(handArea.Size.X).Append(',').Append(handArea.Size.Y);
		signature.Append("|playerCount=").Append(player_count);
		signature.Append("|visibleHandCount=").Append(visibleHandCount);
		signature.Append("|handCount=").Append(hand.Count);
		signature.Append("|itemHandCount=").Append(itemHand.Count);
		signature.Append("|canAct=").Append(canMainPlayerAct);
		signature.Append("|magnet=").Append(isMagnetSelectionActive);
		signature.Append("|discard=").Append(isHandLimitDiscardSelectionActive);
		signature.Append("|revealed=").Append(_revealedPlayerHandIndex);
		signature.Append("|pendingMode=").Append(_pendingItemSelectionMode);
		signature.Append("|pendingOwner=").Append(_pendingItemOwnerIndex);
		signature.Append("|pendingItem=").Append(_pendingItemCardId);
		signature.Append("|opening=").Append(_isOpeningDealAnimationPlaying);

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
			bool isPlayable = isMagnetSelectionCard
				|| isHandLimitDiscardSelectionCard
				|| (!isJellyfishDisabled && playerIndex == MainPlayerIndex && canMainPlayerAct);
			string tooltipText = isJellyfishDisabled
				? Localize("GAME_CARD_DISABLED_THIS_TURN")
				: GetHandCardDescription(card, showCardBack);

			signature.Append("|card=").Append(cardIndex);
			signature.Append(':').Append(card.Suit).Append('/').Append(card.Rank);
			signature.Append(':').Append(card.AttachedItemCardIdsCsv);
			signature.Append(":back=").Append(showCardBack);
			signature.Append(":playable=").Append(isPlayable);
			signature.Append(":magnet=").Append(isMagnetSelectionCard);
			signature.Append(":disabled=").Append(isJellyfishDisabled);
			signature.Append(":hidden=").Append(IsHandDisplaySlotHidden(playerIndex, cardIndex));
			signature.Append(":tooltip=").Append(tooltipText);
			signature.Append(":texture=");
			AppendTextureSignature(signature, showCardBack ? _cardBackTexture : card.Texture);
			signature.Append(":attachment=");
			AppendTextureSignature(signature, showCardBack ? null : GetCardAttachmentOverlayTexture(card));
		}

		for (int itemIndex = 0; itemIndex < itemHand.Count; itemIndex++)
		{
			ItemCard itemCard = itemHand[itemIndex];
			int displayIndex = hand.Count + itemIndex;
			bool isJellyfishDisabled = !isHandLimitDiscardSelectionActive
				&& IsJellyfishDisabledHandDisplaySlot(playerIndex, displayIndex);
			bool isPlayable = playerIndex == MainPlayerIndex
				&& (isHandLimitDiscardSelectionActive || (!isJellyfishDisabled && canMainPlayerAct && CanPlayItemCard(playerIndex, itemCard.Id)));
			string tooltipText = isJellyfishDisabled
				? Localize("GAME_CARD_DISABLED_THIS_TURN")
				: !showCardBack && _itemCardDescriptions.TryGetValue(itemCard.Id, out string? description)
					? description
					: string.Empty;

			signature.Append("|item=").Append(itemIndex);
			signature.Append(':').Append(itemCard.Id);
			signature.Append(":display=").Append(displayIndex);
			signature.Append(":back=").Append(showCardBack);
			signature.Append(":scale=").Append(showCardBack ? 1.0f : GetItemCardVisualScale(itemCard.Id));
			signature.Append(":playable=").Append(isPlayable);
			signature.Append(":disabled=").Append(isJellyfishDisabled);
			signature.Append(":hidden=").Append(IsHandDisplaySlotHidden(playerIndex, displayIndex));
			signature.Append(":tooltip=").Append(tooltipText);
			signature.Append(":texture=");
			AppendTextureSignature(signature, showCardBack ? _cardBackTexture : itemCard.Texture);
		}

		return signature.ToString();
	}

	private static void AppendTextureSignature(StringBuilder signature, Texture2D? texture)
	{
		if (texture == null)
		{
			signature.Append("<null>");
			return;
		}

		signature.Append(texture.ResourcePath);
		signature.Append('#').Append(texture.GetInstanceId());
	}
	}
