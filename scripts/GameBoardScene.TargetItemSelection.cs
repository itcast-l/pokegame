using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private void RefreshNetworkPendingSelectionOverlay()
	{
		if (!IsNetworkMultiplayerMode() || IsNetworkHost())
		{
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.TargetPlayer)
		{
			if (!ShouldShowLocalPendingItemSelectionUi())
			{
				DismissActiveSelectionOverlay(invokeCancel: false);
				return;
			}

			ShowTargetPlayerSelection();
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.TargetHandCard)
		{
			if (!ShouldShowLocalPendingItemSelectionUi())
			{
				DismissActiveSelectionOverlay(invokeCancel: false);
				return;
			}

			ShowTargetHandSelection(_revealedPlayerHandIndex);
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			ShowHandLimitDiscardSelection();
			return;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard)
		{
			ShowMagnetHandSelection();
			return;
		}

		DismissActiveSelectionOverlay(invokeCancel: false);
	}

	private void ShowTargetPlayerSelection()
	{
		if (!ShouldShowLocalPendingItemSelectionUi())
		{
			DismissActiveSelectionOverlay(invokeCancel: false);
			return;
		}

		DismissActiveSelectionOverlay(invokeCancel: false);
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

		ShowSelectionOverlay(Localize("GAME_SELECT_PLAYER"), options, showCancel: !ShouldSendNetworkPendingItemSelectionRequest());
	}

	private void SelectPendingItemTargetPlayer(int targetPlayerIndex)
	{
		if (!IsLocalPendingItemSelectionValid())
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
			if (ShouldSendNetworkPendingItemSelectionRequest())
			{
				_networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.TargetPlayer;
				_networkLocallyCommittedPendingSelectionHandIndex = _pendingItemHandIndex;
				_networkLocallyCommittedPendingSelectionItemCardId = _pendingItemCardId;
				DismissActiveSelectionOverlay(invokeCancel: false);
				_ = BeginPredictedNetworkTargetedItemCommitAsync();
				RequestNetworkPendingItemSelection(targetPlayerIndex, -1, _pendingItemTurnSequenceGeneration);
				return;
			}

			_ = CompletePendingItemSelectionAuthorityAsync(
				_pendingItemCardId,
				_pendingItemOwnerIndex,
				_pendingItemHandIndex,
				targetPlayerIndex,
				-1,
				_pendingItemTurnSequenceGeneration);
			return;
		}

		if (ShouldSendNetworkPendingItemSelectionRequest())
		{
			_pendingItemSelectionMode = PendingItemSelectionMode.TargetHandCard;
			_revealedPlayerHandIndex = targetPlayerIndex;
			_networkLocallyAdvancedPendingTargetSelection = true;
			_networkPendingSelectionRequestInFlight = false;
			RenderPlayerHands();
			ShowTargetHandSelection(targetPlayerIndex);
			RequestNetworkPendingItemSelection(targetPlayerIndex, -1, _pendingItemTurnSequenceGeneration);
			return;
		}

		_pendingItemSelectionMode = PendingItemSelectionMode.TargetHandCard;
		_revealedPlayerHandIndex = targetPlayerIndex;
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		ShowTargetHandSelection(targetPlayerIndex);
	}

	private void ShowTargetHandSelection(int targetPlayerIndex)
	{
		if (!ShouldShowLocalPendingItemSelectionUi())
		{
			DismissActiveSelectionOverlay(invokeCancel: false);
			return;
		}

		DismissActiveSelectionOverlay(invokeCancel: false);
		if (targetPlayerIndex < 0 || targetPlayerIndex >= _playerHands.Count || targetPlayerIndex >= _playerItemHands.Count || GetTotalHandCardCount(targetPlayerIndex) == 0)
		{
			CancelPendingItemSelection();
			return;
		}

		var options = new List<(string Text, Action Action)>();
		bool useHiddenLabels = ShouldUseHiddenTargetHandLabels(targetPlayerIndex);
		if (targetPlayerIndex >= 0 && targetPlayerIndex < _playerHands.Count)
		{
			for (int handIndex = 0; handIndex < _playerHands[targetPlayerIndex].Count; handIndex++)
			{
				PlayingCard card = _playerHands[targetPlayerIndex][handIndex];
				int selectedHandIndex = handIndex;
				string optionLabel = useHiddenLabels
					? LocalizeFormat("GAME_SPECIAL_CARD", handIndex + 1)
					: $"{card.Suit} {card.Rank}";
				options.Add((optionLabel, () => CompletePendingItemSelection(targetPlayerIndex, selectedHandIndex)));
			}

			for (int itemIndex = 0; itemIndex < _playerItemHands[targetPlayerIndex].Count; itemIndex++)
			{
				ItemCard itemCard = _playerItemHands[targetPlayerIndex][itemIndex];
				int selectedHandIndex = _playerHands[targetPlayerIndex].Count + itemIndex;
				string optionLabel = useHiddenLabels
					? LocalizeFormat("GAME_SPECIAL_CARD", selectedHandIndex + 1)
					: LocalizeFormat("GAME_SPECIAL_CARD", itemCard.Id);
				options.Add((optionLabel, () => CompletePendingItemSelection(targetPlayerIndex, selectedHandIndex)));
			}
		}

		ShowSelectionOverlay(LocalizeFormat("GAME_PLAYER_HAND", targetPlayerIndex + 1), options, showCancel: !ShouldSendNetworkPendingItemSelectionRequest());
	}

	private void CompletePendingItemSelection(int targetPlayerIndex, int targetHandIndex)
	{
		_ = CompletePendingItemSelectionAsync(targetPlayerIndex, targetHandIndex);
	}

	private async Task CompletePendingItemSelectionAsync(int targetPlayerIndex, int targetHandIndex)
	{
		try
		{
			if (!IsLocalPendingItemSelectionValid())
			{
				CancelPendingItemSelection();
				return;
			}

			if (ShouldSendNetworkPendingItemSelectionRequest())
			{
				_networkLocallyCommittedPendingSelectionMode = PendingItemSelectionMode.TargetHandCard;
				_networkLocallyCommittedPendingSelectionDisplayIndex = targetHandIndex;
				_networkLocallyCommittedPendingSelectionHandIndex = _pendingItemHandIndex;
				_networkLocallyCommittedPendingSelectionItemCardId = _pendingItemCardId;
				DismissActiveSelectionOverlay(invokeCancel: false);
				_ = BeginPredictedNetworkTargetedItemCommitAsync();
				RequestNetworkPendingItemSelection(targetPlayerIndex, targetHandIndex, _pendingItemTurnSequenceGeneration);
				return;
			}

			await CompletePendingItemSelectionAuthorityAsync(
				_pendingItemCardId,
				_pendingItemOwnerIndex,
				_pendingItemHandIndex,
				targetPlayerIndex,
				targetHandIndex,
				_pendingItemTurnSequenceGeneration);
		}
		catch (Exception error)
		{
			if (IsInsideTree())
			{
				CancelPendingItemSelection();
			}
			else
			{
				if (HasPendingItemSelection())
				{
					ClearPendingItemSelectionState();
				}

				_isProcessingCardPlay = false;
				ResetNetworkPredictedItemVisualState();
			}

			ResetNetworkLocallyCommittedPendingSelectionState();
			ClearNetworkPendingActionState();
			GD.PushWarning($"Failed to complete pending item selection: {error.Message}");
		}
	}

	private async Task CompletePendingItemSelectionAuthorityAsync(
		string itemCardId,
		int ownerPlayerIndex,
		int itemHandIndex,
		int targetPlayerIndex,
		int targetHandIndex,
		int turnSequenceGeneration)
	{
		if (!IsAuthorityPendingItemSelectionValid(ownerPlayerIndex, turnSequenceGeneration))
		{
			CancelPendingItemSelectionForAuthorityFailure(ownerPlayerIndex);
			return;
		}

		bool targetIsValid = IsPendingItemTargetValid(itemCardId, ownerPlayerIndex, targetPlayerIndex, targetHandIndex);
		if (!targetIsValid || itemHandIndex < 0 || itemHandIndex >= _playerItemHands[ownerPlayerIndex].Count)
		{
			CancelPendingItemSelectionForAuthorityFailure(ownerPlayerIndex);
			return;
		}

		ItemCard itemCard = _playerItemHands[ownerPlayerIndex][itemHandIndex];
		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(ownerPlayerIndex, itemHandIndex);
		_playerItemHands[ownerPlayerIndex].RemoveAt(itemHandIndex);
		ShiftHandDisplayStateAfterRemovedCard(ownerPlayerIndex, _playerHands[ownerPlayerIndex].Count + itemHandIndex);
		DismissActiveSelectionOverlay(invokeCancel: false);
		ClearPendingItemSelectionState();
		RenderPlayerHands();
		int snapshotId = IsNetworkMultiplayerMode() && IsNetworkHost()
			? CreateNextNetworkSnapshotId()
			: -1;
		if (snapshotId >= 0)
		{
			_networkAuthoritativeVisualSnapshotId = snapshotId;
		}

		try
		{
			if (snapshotId >= 0)
			{
				GetNetworkManager()?.BroadcastItemCardPlayedVisualEventFromHost(
					ownerPlayerIndex,
					new NetworkItemPlayVisualSync(itemCard.Id, CardPlayPresentationTypes.TargetedTrigger, itemHandIndex),
					turnSequenceGeneration,
					snapshotId);
			}

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
			SendQuestionRewardVisualEventIfNeeded(ownerPlayerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
			await PlayQuestionCardRewardAnimationAsync(ownerPlayerIndex, result.QuestionRewardAnimation, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
				return;
			}
			ApplyAdventureQuestionRewardEffects(ownerPlayerIndex, result.AdventureQuestionRewardEffects);
			await ApplyAdventurePostCaptureEffectsAsync(ownerPlayerIndex, result.AdventurePostCaptureEffects, turnSequenceGeneration);
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
			if (snapshotId >= 0)
			{
				GetNetworkManager()?.SendBoardSnapshotsFromHost(this, snapshotId);
			}
			await FinishPendingItemCardAsync(result, ownerPlayerIndex, turnSequenceGeneration);
		}
		finally
		{
			if (snapshotId >= 0)
			{
				_networkAuthoritativeVisualSnapshotId = -1;
			}
		}
	}

	private async System.Threading.Tasks.Task FinishPendingItemCardAsync(PlayCardResult result, int ownerPlayerIndex, int turnSequenceGeneration)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			return;
		}

		if (result.CompletesTurn)
		{
			await AdvanceTurnAfterPlayedResultAsync(ownerPlayerIndex, result);
		}

		_isProcessingCardPlay = false;
		RenderPlayerHands();
		SendBoardSnapshotIfHost();
		if (IsNetworkMultiplayerMode())
		{
			ResumeNetworkAiProxyTurnAfterAuthorityActionIfNeeded();
			return;
		}

		if (result.CompletesTurn && !result.Captured)
		{
			await ResolveOpponentTurnsAsync(turnSequenceGeneration);
		}
	}

	private bool IsLocalPendingItemSelectionValid()
	{
		return _pendingItemSelectionMode != PendingItemSelectionMode.None
			&& _pendingItemOwnerIndex == GetPendingSelectionLocalOwnerIndex()
			&& _pendingItemTurnSequenceGeneration == _turnSequenceGeneration
			&& _pendingItemOwnerIndex >= 0
			&& _pendingItemOwnerIndex < _playerItemHands.Count;
	}

	private bool IsAuthorityPendingItemSelectionValid(int ownerPlayerIndex, int turnSequenceGeneration)
	{
		return _pendingItemSelectionMode != PendingItemSelectionMode.None
			&& _pendingItemOwnerIndex == ownerPlayerIndex
			&& _pendingItemTurnSequenceGeneration == turnSequenceGeneration
			&& turnSequenceGeneration == _turnSequenceGeneration
			&& ownerPlayerIndex >= 0
			&& ownerPlayerIndex < _playerItemHands.Count;
	}

	private bool ShouldShowLocalPendingItemSelectionUi()
	{
		return _pendingItemSelectionMode is PendingItemSelectionMode.TargetPlayer or PendingItemSelectionMode.TargetHandCard
			&& _pendingItemOwnerIndex == GetPendingSelectionLocalOwnerIndex()
			&& IsLocalSeatHumanConnected();
	}

	private bool ShouldSendNetworkPendingItemSelectionRequest()
	{
		return IsNetworkMultiplayerMode() && !IsNetworkHost();
	}

	private bool ShouldUseHiddenTargetHandLabels(int targetPlayerIndex)
	{
		return ShouldSendNetworkPendingItemSelectionRequest()
			&& _networkLocallyAdvancedPendingTargetSelection
			&& targetPlayerIndex != GetLocalHandPlayerIndex();
	}

	private int GetPendingSelectionLocalOwnerIndex()
	{
		return IsNetworkMultiplayerMode() ? GetLocalHandPlayerIndex() : MainPlayerIndex;
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
		DisposeActiveSelectionOverlay();
		PackedScene? selectionOverlayScene = GD.Load<PackedScene>(ItemSelectionOverlayScenePath);
		if (selectionOverlayScene?.Instantiate() is not ItemSelectionOverlay selectionOverlay)
		{
			GD.PushError($"Missing item selection overlay scene: {ItemSelectionOverlayScenePath}");
			return;
		}

		var optionLabels = new List<string>(options.Count);
		var optionActions = new List<Action>(options.Count);
		foreach ((string text, Action action) in options)
		{
			optionLabels.Add(text);
			optionActions.Add(action);
		}

		AddChild(selectionOverlay);
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		selectionOverlay.Configure(title, optionLabels, Localize("GAME_CANCEL"), showCancel);
		selectionOverlay.OptionSelected += optionIndex =>
		{
			if (optionIndex < 0 || optionIndex >= optionActions.Count)
			{
				PlayGameSfx(GameSfx.UiInvalid, -3.0f, 0.98f, 1.02f, 0.08);
				return;
			}

			optionActions[optionIndex].Invoke();
		};
		selectionOverlay.CancelRequested += () =>
		{
			if (_pendingItemSelectionMode is PendingItemSelectionMode.HandLimitDiscard or PendingItemSelectionMode.MagnetHandCard)
			{
				PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
				return;
			}

			CancelPendingItemSelection();
		};

		RegisterSelectionOverlaySession(new SelectionOverlaySession(
			SelectionOverlayKind.ItemSelection,
			selectionOverlay,
			optionActions.Count,
			optionIndex =>
			{
				if (optionIndex < 0 || optionIndex >= optionActions.Count)
				{
					PlayGameSfx(GameSfx.UiInvalid, -3.0f, 0.98f, 1.02f, 0.08);
					return false;
				}

				optionActions[optionIndex].Invoke();
				return true;
			},
			cancel: () =>
			{
				if (_pendingItemSelectionMode is PendingItemSelectionMode.HandLimitDiscard or PendingItemSelectionMode.MagnetHandCard)
				{
					PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
					return;
				}

				CancelPendingItemSelection();
			},
			dispose: () =>
			{
				if (IsInstanceValid(selectionOverlay))
				{
					RemoveChild(selectionOverlay);
					selectionOverlay.CallDeferred(Node.MethodName.QueueFree);
				}
			}));
	}
}
