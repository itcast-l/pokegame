using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void ShowTargetPlayerSelection()
	{
		ClearSelectionOverlay();
		List<int> candidates = BuildItemTargetPlayerCandidates(_pendingItemCardId, _pendingItemOwnerIndex);
		if (candidates.Count == 0)
		{
			CancelPendingItemSelection();
			return;
		}

		var options = new List<(string Text, Action Action)>();
		foreach (int playerIndex in candidates)
		{
			int targetPlayerIndex = playerIndex;
			options.Add((GetPlayerLabel(playerIndex), () => SelectPendingItemTargetPlayer(targetPlayerIndex)));
		}

		ShowSelectionOverlay(Localize("GAME_SELECT_PLAYER"), options, showCancel: true);
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

		var options = new List<(string Text, Action Action)>();
		if (targetPlayerIndex >= 0 && targetPlayerIndex < _playerHands.Count)
		{
			for (int handIndex = 0; handIndex < _playerHands[targetPlayerIndex].Count; handIndex++)
			{
				PlayingCard card = _playerHands[targetPlayerIndex][handIndex];
				int selectedHandIndex = handIndex;
				options.Add(($"{card.Suit} {card.Rank}", () => CompletePendingItemSelection(targetPlayerIndex, selectedHandIndex)));
			}

			for (int itemIndex = 0; itemIndex < _playerItemHands[targetPlayerIndex].Count; itemIndex++)
			{
				ItemCard itemCard = _playerItemHands[targetPlayerIndex][itemIndex];
				int selectedHandIndex = _playerHands[targetPlayerIndex].Count + itemIndex;
				options.Add((LocalizeFormat("GAME_SPECIAL_CARD", itemCard.Id), () => CompletePendingItemSelection(targetPlayerIndex, selectedHandIndex)));
			}
		}

		ShowSelectionOverlay(LocalizeFormat("GAME_PLAYER_HAND", targetPlayerIndex + 1), options, showCancel: true);
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

		PlaySpecialItemCardPlaySfx();
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

	private void ShowSelectionOverlay(string title, IReadOnlyList<(string Text, Action Action)> options, bool showCancel)
	{
		ClearSelectionOverlay();
		PackedScene? selectionOverlayScene = GD.Load<PackedScene>(ItemSelectionOverlayScenePath);
		if (selectionOverlayScene?.Instantiate() is not ItemSelectionOverlay selectionOverlay)
		{
			GD.PushError($"Missing item selection overlay scene: {ItemSelectionOverlayScenePath}");
			return;
		}

		var optionLabels = new List<string>(options.Count);
		foreach ((string text, Action action) in options)
		{
			optionLabels.Add(text);
			_selectionOverlayActions.Add(action);
		}

		AddChild(selectionOverlay);
		_selectionOverlay = selectionOverlay;
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		selectionOverlay.Configure(title, optionLabels, Localize("GAME_CANCEL"), showCancel);
		selectionOverlay.OptionSelected += SelectSelectionOverlayOption;
		selectionOverlay.CancelRequested += CancelPendingItemSelection;
	}

	private void SelectSelectionOverlayOption(int optionIndex)
	{
		if (optionIndex < 0 || optionIndex >= _selectionOverlayActions.Count)
		{
			PlayGameSfx(GameSfx.UiInvalid, -3.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		Action action = _selectionOverlayActions[optionIndex];
		action.Invoke();
	}

	private void ClearSelectionOverlay()
	{
		_selectionOverlayActions.Clear();
		if (_selectionOverlay == null)
		{
			return;
		}

		RemoveChild(_selectionOverlay);
		_selectionOverlay.QueueFree();
		_selectionOverlay = null;
	}
}
