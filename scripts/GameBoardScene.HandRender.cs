using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private readonly int[] _playerHandRenderSignatures = new int[MaxPlayers];
	private readonly bool[] _playerHandRenderSignaturesInitialized = new bool[MaxPlayers];

	private void RenderPlayerHands()
	{
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
		signature.Add(TranslationServer.GetLocale());

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

			signature.Add(cardIndex);
			signature.Add(card.Suit);
			signature.Add(card.Rank);
			signature.Add(card.AttachedItemCardIdsCsv);
			signature.Add(showCardBack);
			signature.Add(isPlayable);
			signature.Add(isMagnetSelectionCard);
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
			bool isPlayable = playerIndex == MainPlayerIndex
				&& (isHandLimitDiscardSelectionActive || (!isJellyfishDisabled && canMainPlayerAct && CanPlayItemCard(playerIndex, itemCard.Id)));

			signature.Add(itemIndex);
			signature.Add(itemCard.Id);
			signature.Add(displayIndex);
			signature.Add(showCardBack);
			signature.Add(showCardBack ? 1.0f : GetItemCardVisualScale(itemCard.Id));
			signature.Add(isPlayable);
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
