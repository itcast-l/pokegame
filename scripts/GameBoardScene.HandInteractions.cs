using Godot;

public partial class GameBoardScene
{
	private void TryPlayMainPlayerCard(InputEvent inputEvent, int handIndex)
	{
		if (!CanMainPlayerAct())
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

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

		_ = CompleteMagnetAttachmentSelectionAsync(handIndex);
	}

	private void TryPlayMainPlayerItemCard(InputEvent inputEvent, int itemHandIndex)
	{
		if (!CanMainPlayerAct())
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

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
		return handArea.GlobalPosition + firstCardPosition + displayIndex * cardOffset;
	}
}
