using Godot;

public partial class GameBoardScene
{
	private int _selectedMainPlayerHandDisplayIndex = -1;

	private bool TryHandleGameBoardControlInput(InputEvent inputEvent)
	{
		if (_isPauseMenuOpen || IsGameOver())
		{
			return false;
		}

		if (_selectionOverlay != null && GodotObject.IsInstanceValid(_selectionOverlay) && _selectionOverlay.IsVisibleInTree())
		{
			return false;
		}

		if (TryHandleAdventureTutorialContinueInput(inputEvent))
		{
			return true;
		}

		if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			DisableMainPlayerHandSelectionInput();
		}

		if (inputEvent.IsActionPressed(GameInputActions.GameCancel) && TryCancelMainPlayerPendingSelection())
		{
			return true;
		}

		if (inputEvent.IsActionPressed(GameInputActions.HandPrevious))
		{
			EnableMainPlayerHandSelectionInput();
			MoveMainPlayerHandSelection(-1);
			return true;
		}

		if (inputEvent.IsActionPressed(GameInputActions.HandNext))
		{
			EnableMainPlayerHandSelectionInput();
			MoveMainPlayerHandSelection(1);
			return true;
		}

		if (inputEvent.IsActionPressed(GameInputActions.GameConfirm))
		{
			EnableMainPlayerHandSelectionInput();
			ConfirmSelectedMainPlayerHandEntry();
			return true;
		}

		return false;
	}

	private void EnableMainPlayerHandSelectionInput()
	{
		if (_mainPlayerHandSelectionInputEnabled)
		{
			return;
		}

		_mainPlayerHandSelectionInputEnabled = true;
	}

	private void DisableMainPlayerHandSelectionInput()
	{
		if (!_mainPlayerHandSelectionInputEnabled && _selectedMainPlayerHandDisplayIndex < 0)
		{
			return;
		}

		_mainPlayerHandSelectionInputEnabled = false;
		_selectedMainPlayerHandDisplayIndex = -1;
		RenderPlayerHands();
	}

	private void ClearMainPlayerHandSelectionInputOnMouseClick(InputEvent inputEvent)
	{
		if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			DisableMainPlayerHandSelectionInput();
		}
	}

	private bool TryCancelMainPlayerPendingSelection()
	{
		if (!HasPendingItemSelection())
		{
			return false;
		}

		if (_pendingItemSelectionMode == PendingItemSelectionMode.HandLimitDiscard)
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return true;
		}

		PlayGameSfx(GameSfx.UiCancel, -2.0f, 0.98f, 1.02f, 0.08);
		CancelPendingItemSelection();
		return true;
	}

	private void MoveMainPlayerHandSelection(int direction)
	{
		if (direction == 0 || !TryFindSelectableMainPlayerHandDisplayIndex(direction, out int displayIndex))
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		if (_selectedMainPlayerHandDisplayIndex != displayIndex)
		{
			_selectedMainPlayerHandDisplayIndex = displayIndex;
			PlayGameSfx(GameSfx.CardHover, -10.0f, 0.96f, 1.04f, 0.05);
			RenderPlayerHands();
		}
	}

	private void ConfirmSelectedMainPlayerHandEntry()
	{
		RefreshMainPlayerHandSelection();
		if (!IsMainPlayerHandDisplayIndexSelectable(_selectedMainPlayerHandDisplayIndex))
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		int handCardCount = _playerHands[MainPlayerIndex].Count;
		if (IsMainPlayerHandLimitDiscardSelectionActive())
		{
			ConfirmMainPlayerHandLimitDiscard(_selectedMainPlayerHandDisplayIndex);
			return;
		}

		if (_selectedMainPlayerHandDisplayIndex < handCardCount)
		{
			if (IsMainPlayerMagnetSelectionActive())
			{
				ConfirmMainPlayerMagnetAttachment(_selectedMainPlayerHandDisplayIndex);
				return;
			}

			ConfirmMainPlayerHandCard(_selectedMainPlayerHandDisplayIndex);
			return;
		}

		ConfirmMainPlayerItemCard(_selectedMainPlayerHandDisplayIndex - handCardCount);
	}

	private void RefreshMainPlayerHandSelection()
	{
		if (!_mainPlayerHandSelectionInputEnabled)
		{
			_selectedMainPlayerHandDisplayIndex = -1;
			return;
		}

		if (IsMainPlayerHandDisplayIndexSelectable(_selectedMainPlayerHandDisplayIndex))
		{
			return;
		}

		if (TryFindSelectableMainPlayerHandDisplayIndex(1, out int displayIndex))
		{
			_selectedMainPlayerHandDisplayIndex = displayIndex;
			return;
		}

		_selectedMainPlayerHandDisplayIndex = -1;
	}

	private bool TryFindSelectableMainPlayerHandDisplayIndex(int direction, out int displayIndex)
	{
		displayIndex = -1;
		if (MainPlayerIndex >= _playerHands.Count || MainPlayerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		int totalHandCardCount = GetTotalHandCardCount(MainPlayerIndex);
		if (totalHandCardCount <= 0)
		{
			return false;
		}

		int startIndex = _selectedMainPlayerHandDisplayIndex;
		if (startIndex < 0 || startIndex >= totalHandCardCount)
		{
			startIndex = direction > 0 ? -1 : totalHandCardCount;
		}

		for (int offset = 1; offset <= totalHandCardCount; offset++)
		{
			int candidateIndex = PositiveModulo(startIndex + direction * offset, totalHandCardCount);
			if (IsMainPlayerHandDisplayIndexSelectable(candidateIndex))
			{
				displayIndex = candidateIndex;
				return true;
			}
		}

		return false;
	}

	private bool IsMainPlayerHandDisplayIndexSelected(int displayIndex)
	{
		return _mainPlayerHandSelectionInputEnabled
			&& _selectedMainPlayerHandDisplayIndex == displayIndex
			&& IsMainPlayerHandDisplayIndexSelectable(displayIndex);
	}

	private bool IsMainPlayerHandDisplayIndexSelectable(int displayIndex)
	{
		if (MainPlayerIndex >= _playerHands.Count
			|| MainPlayerIndex >= _playerItemHands.Count
			|| displayIndex < 0
			|| displayIndex >= GetTotalHandCardCount(MainPlayerIndex)
			|| IsHandDisplaySlotHidden(MainPlayerIndex, displayIndex))
		{
			return false;
		}

		bool isHandLimitDiscardSelectionActive = IsMainPlayerHandLimitDiscardSelectionActive();
		if (isHandLimitDiscardSelectionActive)
		{
			return true;
		}

		int handCardCount = _playerHands[MainPlayerIndex].Count;
		bool isJellyfishDisabled = IsJellyfishDisabledHandDisplaySlot(MainPlayerIndex, displayIndex);
		if (displayIndex < handCardCount)
		{
			if (displayIndex >= GetVisibleHandCardCountForRender(MainPlayerIndex, handCardCount))
			{
				return false;
			}

			if (isJellyfishDisabled)
			{
				return false;
			}

			PlayingCard card = _playerHands[MainPlayerIndex][displayIndex];
			if (IsMainPlayerMagnetSelectionActive())
			{
				return IsAttachableMagnetCard(card, _pendingItemCardId);
			}

			return CanMainPlayerAct() && CanPlayAdventureTutorialHandCard(displayIndex);
		}

		if (IsMainPlayerMagnetSelectionActive() || isJellyfishDisabled || !CanMainPlayerAct())
		{
			return false;
		}

		int itemHandIndex = displayIndex - handCardCount;
		if (itemHandIndex < 0 || itemHandIndex >= _playerItemHands[MainPlayerIndex].Count)
		{
			return false;
		}

		return CanPlayItemCard(MainPlayerIndex, _playerItemHands[MainPlayerIndex][itemHandIndex].Id)
			&& CanPlayAdventureTutorialItemCard(itemHandIndex);
	}

	private static int PositiveModulo(int value, int modulo)
	{
		return ((value % modulo) + modulo) % modulo;
	}
}
