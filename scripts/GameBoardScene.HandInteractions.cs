using Godot;

public partial class GameBoardScene
{
	private void TryPlayMainPlayerCard(InputEvent inputEvent, int handIndex)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		ConfirmMainPlayerHandCard(handIndex);
	}

	private void ConfirmMainPlayerHandCard(int handIndex)
	{
		if (!CanMainPlayerAct())
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		if (!CanPlayAdventureTutorialHandCard(handIndex))
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			PulseAdventureTutorialCallout();
			return;
		}

		PlayGameSfx(GameSfx.CardPickup, -2.0f, 0.97f, 1.04f, 0.04);
		Vector2? startGlobalPosition = TryGetHandCardStartGlobalPosition(MainPlayerIndex, handIndex);
		_ = PlayMainPlayerCardAndAdvanceAsync(handIndex, _turnSequenceGeneration, startGlobalPosition);
	}

	private void TryCompleteMainPlayerMagnetAttachment(InputEvent inputEvent, int handIndex)
	{
		if (!IsMainPlayerMagnetSelectionActive() || _isAnimatingMagnetAttachment)
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		ConfirmMainPlayerMagnetAttachment(handIndex);
	}

	private void ConfirmMainPlayerMagnetAttachment(int handIndex)
	{
		if (!IsMainPlayerMagnetSelectionActive() || _isAnimatingMagnetAttachment)
		{
			return;
		}

		PlayGameSfx(GameSfx.EnchantTargetSelect, -1.5f, 0.97f, 1.04f, 0.08);
		_ = CompleteMagnetAttachmentSelectionAsync(handIndex);
	}

	private void TryCompleteMainPlayerHandLimitDiscard(InputEvent inputEvent, int displayIndex)
	{
		if (!IsMainPlayerHandLimitDiscardSelectionActive())
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		ConfirmMainPlayerHandLimitDiscard(displayIndex);
	}

	private void ConfirmMainPlayerHandLimitDiscard(int displayIndex)
	{
		if (!IsMainPlayerHandLimitDiscardSelectionActive())
		{
			return;
		}

		CompleteHandLimitDiscardSelection(displayIndex);
	}

	private void TryPlayMainPlayerItemCard(InputEvent inputEvent, int itemHandIndex)
	{
		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		ConfirmMainPlayerItemCard(itemHandIndex);
	}

	private void ConfirmMainPlayerItemCard(int itemHandIndex)
	{
		if (!CanMainPlayerAct())
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			return;
		}

		if (!CanPlayAdventureTutorialItemCard(itemHandIndex))
		{
			PlayGameSfx(GameSfx.UiInvalid, -4.0f, 0.98f, 1.02f, 0.08);
			PulseAdventureTutorialCallout();
			return;
		}

		PlayGameSfx(GameSfx.CardPickup, -2.0f, 0.97f, 1.04f, 0.04);
		_ = PlayMainPlayerItemCardAndAdvanceAsync(itemHandIndex, _turnSequenceGeneration);
	}

	private Vector2? TryGetHandCardStartGlobalPosition(int playerIndex, int handIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count)
		{
			return null;
		}

		Control handArea = _playerHandAreas[playerIndex];
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		return FindHandCardVisual(sourceCardSlot)?.GlobalPosition;
	}

	private Vector2? GetHandItemCardGlobalPosition(int playerIndex, int itemHandIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHandAreas.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count
			|| itemHandIndex < 0
			|| itemHandIndex >= _playerItemHands[playerIndex].Count)
		{
			return null;
		}

		int displayIndex = _playerHands[playerIndex].Count + itemHandIndex;
		Control handArea = _playerHandAreas[playerIndex];
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{displayIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		if (sourceCardVisual != null)
		{
			return sourceCardVisual.GlobalPosition;
		}

		int displayCardCount = _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count;
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		return handArea.GlobalPosition
			+ firstCardPosition
			+ displayIndex * cardOffset
			+ GetHandDisplayCardVisualBasePosition(playerIndex, displayIndex);
	}
}
