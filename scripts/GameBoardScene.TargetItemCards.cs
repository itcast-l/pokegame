using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> ResolveQueenEffectAsync(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int targetHandIndex,
		int turnSequenceGeneration)
	{
		if (ownerPlayerIndex < 0
			|| ownerPlayerIndex >= _playerHands.Count
			|| ownerPlayerIndex >= _playerItemHands.Count
			|| targetPlayerIndex < 0
			|| targetPlayerIndex >= _playerHands.Count
			|| targetPlayerIndex >= _playerItemHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (targetPlayerIndex == ownerPlayerIndex || targetHandIndex < 0 || targetHandIndex >= GetTotalHandCardCount(targetPlayerIndex))
		{
			return new PlayCardResult(false, false);
		}

		if (targetHandIndex < _playerHands[targetPlayerIndex].Count)
		{
			if (!HasRoomForNewHandCard(ownerPlayerIndex)
				&& !await DiscardHandCardForNewItemCardAsync(ownerPlayerIndex, turnSequenceGeneration))
			{
				return new PlayCardResult(false, false);
			}

			PlayingCard stolenCard = _playerHands[targetPlayerIndex][targetHandIndex];
			await PlayStealHandCardAnimationAsync(
				ownerPlayerIndex,
				targetPlayerIndex,
				targetHandIndex,
				GetVisibleStolenPlayingCardTexture(targetPlayerIndex, stolenCard),
				GetVisibleStolenPlayingCardAttachmentOverlayTexture(targetPlayerIndex, stolenCard),
				1.0f,
				GetStolenPlayingCardTargetDisplayIndex(ownerPlayerIndex));
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				RenderPlayerHands();
				return new PlayCardResult(false, false);
			}

			_playerHands[targetPlayerIndex].RemoveAt(targetHandIndex);
			ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
			_playerHands[ownerPlayerIndex].Add(stolenCard);
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			RenderPlayerHands();
			return new PlayCardResult(true, false);
		}

		int itemHandIndex = targetHandIndex - _playerHands[targetPlayerIndex].Count;
		ItemCard stolenItemCard = _playerItemHands[targetPlayerIndex][itemHandIndex];
		if (!HasRoomForNewHandCard(ownerPlayerIndex)
			&& !await DiscardHandCardForNewItemCardAsync(ownerPlayerIndex, turnSequenceGeneration))
		{
			return new PlayCardResult(false, false);
		}

		await PlayStealHandCardAnimationAsync(
			ownerPlayerIndex,
			targetPlayerIndex,
			targetHandIndex,
			GetVisibleStolenItemCardTexture(targetPlayerIndex, stolenItemCard),
			null,
			GetVisibleStolenItemCardScale(targetPlayerIndex, stolenItemCard),
			GetStolenItemCardTargetDisplayIndex(ownerPlayerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			RenderPlayerHands();
			return new PlayCardResult(false, false);
		}

		_playerItemHands[targetPlayerIndex].RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(targetPlayerIndex, targetHandIndex);
		_playerItemHands[ownerPlayerIndex].Add(stolenItemCard);
		PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		RenderPlayerHands();
		return new PlayCardResult(true, false);
	}

	private async Task PlayStealHandCardAnimationAsync(
		int ownerPlayerIndex,
		int targetPlayerIndex,
		int sourceDisplayIndex,
		Texture2D? cardTexture,
		Texture2D? attachmentOverlayTexture,
		float visualScale,
		int targetHandDisplayIndex)
	{
		if (cardTexture == null || !IsInsideTree())
		{
			return;
		}

		Vector2 startGlobalPosition = GetHandDisplayCardGlobalPosition(targetPlayerIndex, sourceDisplayIndex);
		if (targetPlayerIndex >= 0 && targetPlayerIndex < _playerHandAreas.Count)
		{
			Control? sourceCardSlot = _playerHandAreas[targetPlayerIndex].GetNodeOrNull<Control>($"HandCard_{sourceDisplayIndex}");
			startGlobalPosition = sourceCardSlot?.GlobalPosition ?? startGlobalPosition;
			if (sourceCardSlot != null)
			{
				sourceCardSlot.Visible = false;
			}
		}

		int ownerDisplayCardCountAfterSteal = GetTotalHandCardCount(ownerPlayerIndex) + 1;
		Vector2 targetGlobalPosition = GetHandDisplayCardGlobalPosition(
			ownerPlayerIndex,
			targetHandDisplayIndex,
			ownerDisplayCardCountAfterSteal);
		await PlayStolenHandCardFlightAsync(
			cardTexture,
			attachmentOverlayTexture,
			startGlobalPosition,
			targetGlobalPosition,
			GetCardRotationDegreesForPlayer(targetPlayerIndex),
			GetCardRotationDegreesForPlayer(ownerPlayerIndex),
			visualScale);
	}

	private Texture2D? GetVisibleStolenPlayingCardTexture(int sourcePlayerIndex, PlayingCard card)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null ? _cardBackTexture : card.Texture;
	}

	private Texture2D? GetVisibleStolenPlayingCardAttachmentOverlayTexture(int sourcePlayerIndex, PlayingCard card)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) ? null : GetCardAttachmentOverlayTexture(card);
	}

	private Texture2D? GetVisibleStolenItemCardTexture(int sourcePlayerIndex, ItemCard itemCard)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null ? _cardBackTexture : itemCard.Texture;
	}

	private float GetVisibleStolenItemCardScale(int sourcePlayerIndex, ItemCard itemCard)
	{
		return IsPlayerHandHidden(sourcePlayerIndex) && _cardBackTexture != null ? 1.0f : GetItemCardVisualScale(itemCard.Id);
	}

	private int GetStolenPlayingCardTargetDisplayIndex(int ownerPlayerIndex)
	{
		return ownerPlayerIndex >= 0 && ownerPlayerIndex < _playerHands.Count ? _playerHands[ownerPlayerIndex].Count : 0;
	}

	private int GetStolenItemCardTargetDisplayIndex(int ownerPlayerIndex)
	{
		return GetTotalHandCardCount(ownerPlayerIndex);
	}

	private bool IsPlayerHandHidden(int playerIndex)
	{
		return playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null;
	}

	private PlayCardResult ResolveKingEffect(int ownerPlayerIndex, int targetPlayerIndex)
	{
		return ResolveKingEffect(ownerPlayerIndex, targetPlayerIndex, 3);
	}

	private PlayCardResult ResolveKingEffect(int ownerPlayerIndex, int targetPlayerIndex, int cardsToMoveLimit)
	{
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= _playerDecks.Count || targetPlayerIndex < 0 || targetPlayerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (targetPlayerIndex == ownerPlayerIndex)
		{
			return new PlayCardResult(false, false);
		}

		int cardsToMove = Math.Min(cardsToMoveLimit, _playerDecks[targetPlayerIndex].Count);
		for (int cardIndex = 0; cardIndex < cardsToMove; cardIndex++)
		{
			PlayingCard topCard = _playerDecks[targetPlayerIndex][0];
			_playerDecks[targetPlayerIndex].RemoveAt(0);
			AddCardToPlayerDeck(ownerPlayerIndex, topCard);
		}

		Shuffle(_playerDecks[ownerPlayerIndex]);
		Shuffle(_playerDecks[targetPlayerIndex]);
		if (cardsToMove > 0)
		{
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

		return new PlayCardResult(true, false, cardsToMove > 0);
	}

	private List<int> BuildItemTargetPlayerCandidates(string itemCardId, int ownerPlayerIndex)
	{
		var candidates = new List<int>();
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return candidates;
		}

		string target = definition.EffectValues.GetString(CardEffectValueKeys.Target);
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (IsResolvedItemTargetPlayerCandidate(target, ownerPlayerIndex, playerIndex))
			{
				candidates.Add(playerIndex);
			}
		}

		return candidates;
	}

	private int PickItemTargetPlayer(string itemCardId, int ownerPlayerIndex)
	{
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return -1;
		}

		string target = definition.EffectValues.GetString(CardEffectValueKeys.Target);
		int selectedPlayerIndex = -1;
		int candidateCount = 0;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (!IsResolvedItemTargetPlayerCandidate(target, ownerPlayerIndex, playerIndex))
			{
				continue;
			}

			candidateCount++;
			if (_rng.RandiRange(1, candidateCount) == 1)
			{
				selectedPlayerIndex = playerIndex;
			}
		}

		return selectedPlayerIndex;
	}

	private bool IsItemTargetPlayerCandidate(string itemCardId, int ownerPlayerIndex, int targetPlayerIndex)
	{
		return _itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition)
			&& IsResolvedItemTargetPlayerCandidate(
				definition.EffectValues.GetString(CardEffectValueKeys.Target),
				ownerPlayerIndex,
				targetPlayerIndex);
	}

	private bool IsResolvedItemTargetPlayerCandidate(string target, int ownerPlayerIndex, int targetPlayerIndex)
	{
		if (targetPlayerIndex < 0 || targetPlayerIndex >= player_count || targetPlayerIndex == ownerPlayerIndex)
		{
			return false;
		}

		if (target == CardEffectTargets.PlayerHand)
		{
			return targetPlayerIndex < _playerHands.Count
				&& targetPlayerIndex < _playerItemHands.Count
				&& GetTotalHandCardCount(targetPlayerIndex) > 0;
		}

		if (target == CardEffectTargets.PlayerDeck)
		{
			return targetPlayerIndex < _playerDecks.Count && _playerDecks[targetPlayerIndex].Count > 0;
		}

		return true;
	}

	private int PickTargetHandIndex(int targetPlayerIndex)
	{
		if (targetPlayerIndex < 0 || targetPlayerIndex >= _playerHands.Count || targetPlayerIndex >= _playerItemHands.Count)
		{
			return -1;
		}

		int totalHandCards = GetTotalHandCardCount(targetPlayerIndex);
		if (totalHandCards == 0)
		{
			return -1;
		}

		return _rng.RandiRange(0, totalHandCards - 1);
	}

	private void ShowTargetPlayerSelection()
	{
		ClearSelectionOverlay();
		List<int> candidates = BuildItemTargetPlayerCandidates(_pendingItemCardId, _pendingItemOwnerIndex);
		if (candidates.Count == 0)
		{
			CancelPendingItemSelection();
			return;
		}

		var overlay = CreateSelectionOverlay();
		var panel = CreateSelectionPanel(Localize("GAME_SELECT_PLAYER"));

		foreach (int playerIndex in candidates)
		{
			int targetPlayerIndex = playerIndex;
			Button button = CreateSelectionButton(GetPlayerLabel(playerIndex));
			button.Pressed += () => SelectPendingItemTargetPlayer(targetPlayerIndex);
			panel.AddChild(button);
		}

		panel.AddChild(CreateCancelButton());
		overlay.AddChild(panel);
		AddChild(overlay);
		_selectionOverlay = overlay;
	}

	private void SelectPendingItemTargetPlayer(int targetPlayerIndex)
	{
		if (!IsPendingItemSelectionValid())
		{
			CancelPendingItemSelection();
			return;
		}

		if (!IsItemTargetPlayerCandidate(_pendingItemCardId, _pendingItemOwnerIndex, targetPlayerIndex))
		{
			CancelPendingItemSelection();
			return;
		}

		if (IsPlayerDeckTargetItemCard(_pendingItemCardId))
		{
			CompletePendingItemSelection(targetPlayerIndex, -1);
			return;
		}

		_pendingItemSelectionMode = PendingItemSelectionMode.TargetHandCard;
		_revealedPlayerHandIndex = targetPlayerIndex;
		RenderPlayerHands();
		ShowTargetHandSelection(targetPlayerIndex);
	}

	private void ShowTargetHandSelection(int targetPlayerIndex)
	{
		ClearSelectionOverlay();
		if (targetPlayerIndex < 0 || targetPlayerIndex >= _playerHands.Count || targetPlayerIndex >= _playerItemHands.Count || GetTotalHandCardCount(targetPlayerIndex) == 0)
		{
			CancelPendingItemSelection();
			return;
		}

		var overlay = CreateSelectionOverlay();
		var panel = CreateSelectionPanel(LocalizeFormat("GAME_PLAYER_HAND", targetPlayerIndex + 1));

		if (targetPlayerIndex >= 0 && targetPlayerIndex < _playerHands.Count)
		{
			for (int handIndex = 0; handIndex < _playerHands[targetPlayerIndex].Count; handIndex++)
			{
				PlayingCard card = _playerHands[targetPlayerIndex][handIndex];
				int selectedHandIndex = handIndex;
				Button button = CreateSelectionButton($"{card.Suit} {card.Rank}");
				button.Pressed += () => CompletePendingItemSelection(targetPlayerIndex, selectedHandIndex);
				panel.AddChild(button);
			}

			for (int itemIndex = 0; itemIndex < _playerItemHands[targetPlayerIndex].Count; itemIndex++)
			{
				ItemCard itemCard = _playerItemHands[targetPlayerIndex][itemIndex];
				int selectedHandIndex = _playerHands[targetPlayerIndex].Count + itemIndex;
				Button button = CreateSelectionButton(LocalizeFormat("GAME_SPECIAL_CARD", itemCard.Id));
				button.Pressed += () => CompletePendingItemSelection(targetPlayerIndex, selectedHandIndex);
				panel.AddChild(button);
			}
		}

		panel.AddChild(CreateCancelButton());
		overlay.AddChild(panel);
		AddChild(overlay);
		_selectionOverlay = overlay;
	}

	private async void CompletePendingItemSelection(int targetPlayerIndex, int targetHandIndex)
	{
		if (!IsPendingItemSelectionValid())
		{
			CancelPendingItemSelection();
			return;
		}

		string itemCardId = _pendingItemCardId;
		int ownerPlayerIndex = _pendingItemOwnerIndex;
		int itemHandIndex = _pendingItemHandIndex;
		int turnSequenceGeneration = _pendingItemTurnSequenceGeneration;
		bool targetIsValid = IsPendingItemTargetValid(itemCardId, ownerPlayerIndex, targetPlayerIndex, targetHandIndex);
		if (!targetIsValid || itemHandIndex < 0 || itemHandIndex >= _playerItemHands[ownerPlayerIndex].Count)
		{
			CancelPendingItemSelection();
			return;
		}

		ItemCard itemCard = _playerItemHands[ownerPlayerIndex][itemHandIndex];
		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(ownerPlayerIndex, itemHandIndex);
		_playerItemHands[ownerPlayerIndex].RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(ownerPlayerIndex, _playerHands[ownerPlayerIndex].Count + itemHandIndex);
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();

		await PlaySpecialCardTriggerEffectAsync(
			itemCard.Texture,
			itemGlobalPosition ?? Vector2.Zero,
			0,
			GetCardRotationDegreesForPlayer(ownerPlayerIndex));
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			return;
		}

		PlayCardResult result = (await ResolveItemCardOnPlayAsync(
			itemCard,
			ownerPlayerIndex,
			itemHandIndex,
			targetPlayerIndex,
			targetHandIndex,
			itemGlobalPosition ?? Vector2.Zero,
			turnSequenceGeneration)) with { CompletesTurn = false };
		RenderPlayerCardPiles();
		RenderTableCards();
		await PlayQuestionCardRewardAnimationAsync(ownerPlayerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			return;
		}

		await ResolvePendingMagnetAttachmentsAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			return;
		}

		RenderPlayerHands();
		if (result.DeckCollected)
		{
			await PlayPhysicalDeckShuffleAsync(ownerPlayerIndex);
		}

		CheckForWinner();
		_ = FinishPendingMainPlayerItemCardAsync(result, turnSequenceGeneration);
	}

	private async System.Threading.Tasks.Task FinishPendingMainPlayerItemCardAsync(PlayCardResult result, int turnSequenceGeneration)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			return;
		}

		if (result.CompletesTurn)
		{
			await AdvanceTurnAfterPlayedResultAsync(MainPlayerIndex, result);
		}

		_isProcessingCardPlay = false;
		RenderPlayerHands();
		if (result.CompletesTurn && !result.Captured)
		{
			await ResolveOpponentTurnsAsync(turnSequenceGeneration);
		}
	}

	private bool IsPendingItemSelectionValid()
	{
		return _pendingItemSelectionMode != PendingItemSelectionMode.None
			&& _pendingItemOwnerIndex == MainPlayerIndex
			&& _pendingItemTurnSequenceGeneration == _turnSequenceGeneration
			&& _pendingItemOwnerIndex >= 0
			&& _pendingItemOwnerIndex < _playerItemHands.Count;
	}

	private bool IsPendingItemTargetValid(string itemCardId, int ownerPlayerIndex, int targetPlayerIndex, int targetHandIndex)
	{
		if (!_itemCardCatalog.TryGetDefinition(itemCardId, out ItemCardDefinition definition))
		{
			return false;
		}

		return definition.EffectValues.GetString(CardEffectValueKeys.Target) switch
		{
			CardEffectTargets.PlayerHand => targetPlayerIndex >= 0
				&& targetPlayerIndex < _playerHands.Count
				&& targetPlayerIndex != ownerPlayerIndex
				&& targetHandIndex >= 0
				&& targetHandIndex < GetTotalHandCardCount(targetPlayerIndex),
			CardEffectTargets.PlayerDeck => targetPlayerIndex >= 0
				&& targetPlayerIndex < _playerDecks.Count
				&& targetPlayerIndex != ownerPlayerIndex
				&& _playerDecks[targetPlayerIndex].Count > 0,
			_ => true,
		};
	}

	private Control CreateSelectionOverlay()
	{
		return new Control
		{
			Name = "ItemSelectionOverlay",
			MouseFilter = MouseFilterEnum.Stop,
			AnchorLeft = 0.0f,
			AnchorTop = 0.0f,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			ZIndex = 2000,
		};
	}

	private VBoxContainer CreateSelectionPanel(string title)
	{
		var panel = new VBoxContainer
		{
			AnchorLeft = 0.39f,
			AnchorTop = 0.32f,
			AnchorRight = 0.61f,
			AnchorBottom = 0.68f,
			MouseFilter = MouseFilterEnum.Stop,
		};
		panel.AddThemeConstantOverride("separation", 8);

		var titleLabel = new Label
		{
			Text = title,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			CustomMinimumSize = new Vector2(0.0f, 32.0f),
		};
		titleLabel.AddThemeColorOverride("font_color", Colors.White);
		titleLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
		titleLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		titleLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		panel.AddChild(titleLabel);

		return panel;
	}

	private Button CreateSelectionButton(string text)
	{
		return new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(0.0f, 36.0f),
			FocusMode = FocusModeEnum.None,
		};
	}

	private Button CreateCancelButton()
	{
		Button button = CreateSelectionButton(Localize("GAME_CANCEL"));
		button.Pressed += CancelPendingItemSelection;
		return button;
	}

	private void ClearSelectionOverlay()
	{
		if (_selectionOverlay == null)
		{
			return;
		}

		RemoveChild(_selectionOverlay);
		_selectionOverlay.QueueFree();
		_selectionOverlay = null;
	}
}
