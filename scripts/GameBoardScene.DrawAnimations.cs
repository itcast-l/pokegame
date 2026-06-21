using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayReplacementDrawAnimationAsync(int playerIndex, DrawnHandCard? drawnCard, int turnSequenceGeneration)
	{
		if (!IsInsideTree())
		{
			return;
		}

		if (!drawnCard.HasValue)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
			return;
		}

		if (playerIndex < 0
			|| playerIndex >= _playerCardPiles.Count
			|| playerIndex >= _playerHandAreas.Count
			|| playerIndex >= _playerHands.Count
			|| drawnCard.Value.HandIndex < 0
			|| drawnCard.Value.HandIndex >= _playerHands[playerIndex].Count)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
			return;
		}

		int displayIndex = drawnCard.Value.HandIndex;
		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastDrawCardVisualEventFromHost(
				playerIndex,
				displayIndex,
				ToNetworkCardCode(drawnCard.Value.Card),
				turnSequenceGeneration,
				snapshotId);
		}

		HideHandDisplaySlot(playerIndex, displayIndex);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		PlayGameSfx(GameSfx.CardDraw, -2.0f, 0.96f, 1.05f, 0.08);

		try
		{
			await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			Vector2 startGlobalPosition = GetDrawCardStartGlobalPosition(playerIndex);
			Vector2 targetGlobalPosition = GetHandDisplayCardSlotGlobalPosition(playerIndex, displayIndex);
			await PlayDrawnCardFlightAsync(
				playerIndex,
				drawnCard.Value.Card,
				startGlobalPosition,
				targetGlobalPosition,
				displayIndex);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			ShowHandDisplaySlot(playerIndex, displayIndex);
			RenderPlayerHands();
			await PlayDrawnCardSettleAsync(playerIndex, displayIndex);
		}
		finally
		{
			ShowHandDisplaySlot(playerIndex, displayIndex);
		}
	}

	private async Task PlayDrawnCardFlightAsync(
		int playerIndex,
		PlayingCard drawnCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		int displayIndex)
	{
		Texture2D? startTexture = _cardBackTexture ?? drawnCard.Texture;
		if (startTexture == null || !IsInsideTree())
		{
			return;
		}

		bool showFaceOnLanding = !IsPlayerHandHidden(playerIndex);
		Texture2D targetTexture = showFaceOnLanding ? drawnCard.Texture : startTexture;
		Texture2D? attachmentOverlayTexture = showFaceOnLanding ? GetCardAttachmentOverlayTexture(drawnCard) : null;
		float startRotationDegrees = GetCardPileRotationDegreesForPlayer(playerIndex);
		float targetRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		float targetVisualScale = GetHandDisplayCardVisualScale(playerIndex, displayIndex);
		Control flightCard = _animationPresenter.RentControl(
			this,
			"ReplacementDrawCardFlight",
			CardVisualSize,
			PlayedCardFlightZIndex + 120);
		flightCard.RotationDegrees = startRotationDegrees;
		flightCard.Scale = Vector2.One * ReplacementDrawStartScale;
		TextureRect cardVisual = _animationPresenter.RentTextureRect(
			flightCard,
			"ReplacementDrawCardVisual",
			startTexture,
			CardVisualSize,
			0,
			CanvasItem.TextureFilterEnum.Nearest);
		TextureRect? attachmentOverlay = null;
		if (attachmentOverlayTexture != null)
		{
			attachmentOverlay = _animationPresenter.RentTextureRect(
				flightCard,
				"ReplacementDrawAttachmentOverlay",
				attachmentOverlayTexture,
				CardVisualSize,
				1,
				CanvasItem.TextureFilterEnum.Linear);
			attachmentOverlay.Visible = false;
		}
		flightCard.GlobalPosition = startGlobalPosition;

		try
		{
			UpdateDrawnCardFlightMotion(
				flightCard,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				targetRotationDegrees,
				0.0f);

			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateDrawnCardFlightMotion(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					targetRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				ReplacementDrawFlightSeconds);
			TweenDrawnCardFlightVisuals(
				tween,
				flightCard,
				cardVisual,
				attachmentOverlay,
				startTexture,
				targetTexture,
				showFaceOnLanding,
				targetVisualScale);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					_animationPresenter.Release(flightCard);
				}
			};
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				_animationPresenter.Release(flightCard);
			}
		}
	}

	private async Task PlayDrawnCardSettleAsync(int playerIndex, int displayIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count)
		{
			return;
		}

		Control? cardSlot = _playerHandAreas[playerIndex].GetNodeOrNull<Control>($"HandCard_{displayIndex}");
		if (cardSlot == null)
		{
			return;
		}

		TextureRect? cardVisual = FindHandCardVisual(cardSlot);
		if (cardVisual == null)
		{
			return;
		}

		cardVisual.Scale = Vector2.One * ReplacementDrawPeakScale;
		cardVisual.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.92f);
		Tween tween = cardVisual.CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Back);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(cardVisual, "scale", Vector2.One, ReplacementDrawSettleSeconds);
		tween.TweenProperty(cardVisual, "modulate:a", 1.0f, ReplacementDrawSettleSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private static void UpdateDrawnCardFlightMotion(
		Control flightCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * ReplacementDrawArcHeight;
		float lift = Mathf.Sin(clampedProgress * Mathf.Pi);
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress) + lift * 7.0f;
	}

	private static void TweenDrawnCardFlightVisuals(
		Tween tween,
		Control flightCard,
		TextureRect cardVisual,
		TextureRect? attachmentOverlay,
		Texture2D startTexture,
		Texture2D targetTexture,
		bool flipToFace,
		float targetVisualScale)
	{
		float halfDuration = ReplacementDrawFlightSeconds * 0.5f;
		float peakScale = targetVisualScale * ReplacementDrawPeakScale;
		tween.TweenProperty(flightCard, "scale", Vector2.One * peakScale, halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(flightCard, "scale", Vector2.One * targetVisualScale, ReplacementDrawFlightSeconds - halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		if (!flipToFace)
		{
			cardVisual.Texture = startTexture;
			return;
		}

		cardVisual.Texture = startTexture;
		float flipDelay = ReplacementDrawFlightSeconds * 0.58f;
		tween.TweenProperty(cardVisual, "scale", new Vector2(PlayedCardFlipMinScaleX, 1.0f), flipDelay)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(cardVisual))
			{
				return;
			}

			cardVisual.Texture = targetTexture;
			if (attachmentOverlay != null && GodotObject.IsInstanceValid(attachmentOverlay))
			{
				attachmentOverlay.Visible = true;
			}
		})).SetDelay(flipDelay);
		tween.TweenProperty(cardVisual, "scale", Vector2.One, ReplacementDrawFlightSeconds - flipDelay)
			.SetDelay(flipDelay)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private Vector2 GetDrawCardStartGlobalPosition(int playerIndex)
	{
		return GetVisibleCardPileTopGlobalPosition(playerIndex);
	}

	private Vector2 GetHandDisplayCardGlobalPosition(int playerIndex, int displayIndex)
	{
		return GetHandDisplayCardSlotGlobalPosition(playerIndex, displayIndex)
			+ GetHandDisplayCardVisualBasePosition(playerIndex, displayIndex);
	}

	private Vector2 GetHandDisplayCardSlotGlobalPosition(int playerIndex, int displayIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHandAreas.Count
			|| playerIndex >= _playerHands.Count
			|| playerIndex >= _playerItemHands.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		int displayCardCount = Math.Max(1, _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count);
		return GetHandDisplayCardSlotGlobalPosition(playerIndex, displayIndex, displayCardCount);
	}

	private Vector2 GetHandDisplayCardGlobalPosition(int playerIndex, int displayIndex, int displayCardCount)
	{
		return GetHandDisplayCardSlotGlobalPosition(playerIndex, displayIndex, displayCardCount)
			+ GetHandDisplayCardVisualBasePosition(playerIndex, displayIndex);
	}

	private Vector2 GetHandDisplayCardSlotGlobalPosition(int playerIndex, int displayIndex, int displayCardCount)
	{
		if (playerIndex < 0 || playerIndex >= _playerHandAreas.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		Control handArea = _playerHandAreas[playerIndex];
		displayCardCount = Math.Max(1, displayCardCount);
		Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
		return handArea.GlobalPosition
			+ firstCardPosition
			+ displayIndex * cardOffset;
	}

	private bool IsHandDisplaySlotHidden(int playerIndex, int displayIndex)
	{
		return _hiddenHandDisplaySlots.Contains(BuildHandDisplaySlotKey(playerIndex, displayIndex));
	}

	private void HideHandDisplaySlot(int playerIndex, int displayIndex)
	{
		_hiddenHandDisplaySlots.Add(BuildHandDisplaySlotKey(playerIndex, displayIndex));
	}

	private void ShowHandDisplaySlot(int playerIndex, int displayIndex)
	{
		ForceShowHandDisplaySlot(playerIndex, displayIndex);
	}

	private void ForceShowHandDisplaySlot(int playerIndex, int displayIndex)
	{
		_hiddenHandDisplaySlots.Remove(BuildHandDisplaySlotKey(playerIndex, displayIndex));
	}

	private void ClearHiddenHandDisplaySlots(int playerIndex)
	{
		if (_hiddenHandDisplaySlots.Count == 0)
		{
			return;
		}

		_shiftedHiddenHandDisplaySlots.Clear();
		foreach (string key in _hiddenHandDisplaySlots)
		{
			if (TryParseHandDisplaySlotKey(key, out int keyPlayerIndex, out _) && keyPlayerIndex == playerIndex)
			{
				_shiftedHiddenHandDisplaySlots.Add(key);
			}
		}

		foreach (string key in _shiftedHiddenHandDisplaySlots)
		{
			_hiddenHandDisplaySlots.Remove(key);
		}
		_shiftedHiddenHandDisplaySlots.Clear();
	}

	private static string BuildHandDisplaySlotKey(int playerIndex, int displayIndex)
	{
		return $"{playerIndex}:{displayIndex}";
	}
}
