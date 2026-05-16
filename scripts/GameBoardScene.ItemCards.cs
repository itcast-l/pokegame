using System;
using System.Collections.Generic;
using Godot;

public partial class GameBoardScene
{
	private bool RequiresPlayerTarget(string itemCardId)
	{
		return itemCardId == QueenItemCardId || itemCardId == KingItemCardId;
	}

	private bool HasPendingItemSelection()
	{
		return _pendingItemSelectionMode != PendingItemSelectionMode.None;
	}

	private bool HasPlayableTargetForItemCard(string itemCardId, int ownerPlayerIndex)
	{
		return BuildItemTargetPlayerCandidates(itemCardId, ownerPlayerIndex).Count > 0;
	}

	private void StartPendingItemSelection(string itemCardId, int ownerPlayerIndex, int itemHandIndex, int turnSequenceGeneration)
	{
		_pendingItemCardId = itemCardId;
		_pendingItemOwnerIndex = ownerPlayerIndex;
		_pendingItemHandIndex = itemHandIndex;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemSelectionMode = PendingItemSelectionMode.TargetPlayer;
		_revealedPlayerHandIndex = -1;
		RenderPlayerHands();
		ShowTargetPlayerSelection();
	}

	private void CancelPendingItemSelection()
	{
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		_isProcessingCardPlay = false;
		RenderPlayerHands();
	}

	private void ClearPendingItemSelectionState()
	{
		_pendingItemSelectionMode = PendingItemSelectionMode.None;
		_pendingItemOwnerIndex = -1;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = -1;
		_pendingItemCardId = string.Empty;
		_revealedPlayerHandIndex = -1;
	}

	private PlayCardResult ResolveItemCardEffect(string itemCardId, int ownerPlayerIndex, int targetPlayerIndex, int targetHandIndex)
	{
		if (itemCardId == JackItemCardId)
		{
			return ResolveJackEffect(ownerPlayerIndex);
		}

		if (itemCardId == QueenItemCardId)
		{
			return ResolveQueenEffect(ownerPlayerIndex, targetPlayerIndex, targetHandIndex);
		}

		if (itemCardId == KingItemCardId)
		{
			return ResolveKingEffect(ownerPlayerIndex, targetPlayerIndex);
		}

		return new PlayCardResult(false, false);
	}

	private PlayCardResult ResolveJackEffect(int ownerPlayerIndex)
	{
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		bool captured = _tableCards.Count > 0;
		int collectedCardCount = CaptureTableCards(ownerPlayerIndex, 0);
		if (!IsGameOver())
		{
			Shuffle(_playerDecks[ownerPlayerIndex]);
			Cut(_playerDecks[ownerPlayerIndex]);
		}

		return new PlayCardResult(true, captured, collectedCardCount > 0);
	}

	private PlayCardResult ResolveQueenEffect(int ownerPlayerIndex, int targetPlayerIndex, int targetHandIndex)
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
			PlayingCard stolenCard = _playerHands[targetPlayerIndex][targetHandIndex];
			_playerHands[targetPlayerIndex].RemoveAt(targetHandIndex);
			_playerHands[ownerPlayerIndex].Add(stolenCard);
			return new PlayCardResult(true, false);
		}

		int itemHandIndex = targetHandIndex - _playerHands[targetPlayerIndex].Count;
		ItemCard stolenItemCard = _playerItemHands[targetPlayerIndex][itemHandIndex];
		if (!HasRoomForItemCard(ownerPlayerIndex))
		{
			return new PlayCardResult(false, false);
		}

		_playerItemHands[targetPlayerIndex].RemoveAt(itemHandIndex);
		_playerItemHands[ownerPlayerIndex].Add(stolenItemCard);
		return new PlayCardResult(true, false);
	}

	private PlayCardResult ResolveKingEffect(int ownerPlayerIndex, int targetPlayerIndex)
	{
		if (ownerPlayerIndex < 0 || ownerPlayerIndex >= _playerDecks.Count || targetPlayerIndex < 0 || targetPlayerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		if (targetPlayerIndex == ownerPlayerIndex)
		{
			return new PlayCardResult(false, false);
		}

		int cardsToMove = Math.Min(3, _playerDecks[targetPlayerIndex].Count);
		for (int cardIndex = 0; cardIndex < cardsToMove; cardIndex++)
		{
			PlayingCard topCard = _playerDecks[targetPlayerIndex][0];
			_playerDecks[targetPlayerIndex].RemoveAt(0);
			_playerDecks[ownerPlayerIndex].Add(topCard);
		}

		Shuffle(_playerDecks[ownerPlayerIndex]);
		Shuffle(_playerDecks[targetPlayerIndex]);
		return new PlayCardResult(true, false, cardsToMove > 0);
	}

	private List<int> BuildItemTargetPlayerCandidates(string itemCardId, int ownerPlayerIndex)
	{
		var candidates = new List<int>();
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (playerIndex == ownerPlayerIndex)
			{
				continue;
			}

			if (itemCardId == QueenItemCardId && GetTotalHandCardCount(playerIndex) == 0)
			{
				continue;
			}

			if (itemCardId == KingItemCardId && _playerDecks[playerIndex].Count == 0)
			{
				continue;
			}

			candidates.Add(playerIndex);
		}

		return candidates;
	}

	private int PickItemTargetPlayer(string itemCardId, int ownerPlayerIndex)
	{
		List<int> candidates = BuildItemTargetPlayerCandidates(itemCardId, ownerPlayerIndex);
		if (candidates.Count == 0)
		{
			return -1;
		}

		return candidates[_rng.RandiRange(0, candidates.Count - 1)];
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
		var panel = CreateSelectionPanel("选择玩家");

		foreach (int playerIndex in candidates)
		{
			int targetPlayerIndex = playerIndex;
			Button button = CreateSelectionButton($"玩家 {playerIndex + 1}");
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

		if (!BuildItemTargetPlayerCandidates(_pendingItemCardId, _pendingItemOwnerIndex).Contains(targetPlayerIndex))
		{
			CancelPendingItemSelection();
			return;
		}

		if (_pendingItemCardId == KingItemCardId)
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
		var panel = CreateSelectionPanel($"玩家 {targetPlayerIndex + 1} 手牌");

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
				Button button = CreateSelectionButton($"特殊牌 {itemCard.Id}");
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
		bool targetIsValid = itemCardId switch
		{
			QueenItemCardId => targetPlayerIndex >= 0
				&& targetPlayerIndex < _playerHands.Count
				&& targetPlayerIndex != ownerPlayerIndex
				&& targetHandIndex >= 0
				&& targetHandIndex < GetTotalHandCardCount(targetPlayerIndex),
			KingItemCardId => targetPlayerIndex >= 0
				&& targetPlayerIndex < _playerDecks.Count
				&& targetPlayerIndex != ownerPlayerIndex
				&& _playerDecks[targetPlayerIndex].Count > 0,
			_ => true,
		};
		if (!targetIsValid || itemHandIndex < 0 || itemHandIndex >= _playerItemHands[ownerPlayerIndex].Count)
		{
			CancelPendingItemSelection();
			return;
		}

		_playerItemHands[ownerPlayerIndex].RemoveAt(itemHandIndex);
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();

		PlayCardResult result = ResolveItemCardEffect(itemCardId, ownerPlayerIndex, targetPlayerIndex, targetHandIndex);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
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

		AdvanceTurnAfterPlayedResult(MainPlayerIndex, result);
		_isProcessingCardPlay = false;
		RenderPlayerHands();
		if (!result.Captured)
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
		Button button = CreateSelectionButton("取消");
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

	private int GetTotalHandCardCount(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return 0;
		}

		return _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count;
	}

	private bool HasRoomForItemCard(int playerIndex, int reservedHandCards = 0)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count || playerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		return GetTotalHandCardCount(playerIndex) + reservedHandCards < MaxTotalHandCards;
	}
}
