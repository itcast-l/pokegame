using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayReplacementDrawAnimationAsync(
		int playerIndex,
		DrawnHandCard? drawnCard,
		int turnSequenceGeneration,
		bool broadcastNetworkVisualEvent = true)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			if (broadcastNetworkVisualEvent
				&& drawnCard.HasValue
				&& playerIndex >= 0
				&& playerIndex < _playerHands.Count
				&& drawnCard.Value.HandIndex >= 0
				&& drawnCard.Value.HandIndex < _playerHands[playerIndex].Count
				&& TryGetCurrentAuthoritativeVisualSnapshotId(out int transactionSnapshotId))
			{
				GetNetworkManager()?.BroadcastDrawCardVisualEventFromHost(
					playerIndex,
					drawnCard.Value.HandIndex,
					ToNetworkCardCode(drawnCard.Value.Card),
					turnSequenceGeneration,
					transactionSnapshotId);
			}
			return;
		}

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
		if (broadcastNetworkVisualEvent
			&& IsNetworkMultiplayerMode()
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
			await WaitForNextBoardFrameAsync();
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			Vector2 startGlobalPosition = GetRenderedDrawCardStartGlobalPosition(playerIndex);
			Vector2 targetGlobalPosition = GetRenderedHandDisplayCardGlobalPosition(playerIndex, displayIndex);
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
		Texture2D? startTexture = _cardBackTexture ?? GetPlayingCardTexture(drawnCard);
		if (startTexture == null || !IsInsideTree())
		{
			return;
		}

		bool showFaceOnLanding = !IsPlayerHandHidden(playerIndex);
		Texture2D targetTexture = showFaceOnLanding ? GetPlayingCardTexture(drawnCard) : startTexture;
		Texture2D? attachmentOverlayTexture = showFaceOnLanding ? GetCardAttachmentOverlayTexture(drawnCard) : null;
		float startRotationDegrees = GetCardPileRotationDegreesForPlayer(playerIndex);
		float targetRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		float targetVisualScale = GetHandDisplayCardVisualScale(playerIndex, displayIndex);
		float startVisualScale = showFaceOnLanding ? ReplacementDrawStartScale : 1.0f;
		Control flightCard = _animationPresenter.RentControl(
			this,
			"ReplacementDrawCardFlight",
			CardVisualSize,
			PlayedCardFlightZIndex + 120);
		flightCard.RotationDegrees = startRotationDegrees;
		flightCard.Scale = Vector2.One * startVisualScale;
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
				startVisualScale,
				targetVisualScale,
				showFaceOnLanding,
				0.0f);

			Tween tween = CreateBoardTween();
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
					startVisualScale,
					targetVisualScale,
					showFaceOnLanding,
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
				showFaceOnLanding);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					_animationPresenter.Release(flightCard);
				}
			};
			await WaitForBoardTweenAsync(tween);
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

		if (IsPlayerHandHidden(playerIndex))
		{
			cardVisual.Scale = Vector2.One;
			cardVisual.Modulate = Colors.White;
			return;
		}

		cardVisual.Scale = Vector2.One * ReplacementDrawPeakScale;
		cardVisual.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.92f);
		// Bind the visual tween to the board, not the rendered hand node. A
		// network snapshot or prediction may replace that node while the settle
		// effect is running; the playback queue still needs a bounded completion.
		Tween tween = CreateBoardTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Back);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(cardVisual, "scale", Vector2.One, ReplacementDrawSettleSeconds);
		tween.TweenProperty(cardVisual, "modulate:a", 1.0f, ReplacementDrawSettleSeconds);
		await WaitForBoardDelayAsync(ReplacementDrawSettleSeconds);
		if (GodotObject.IsInstanceValid(tween))
		{
			tween.Kill();
		}
		if (GodotObject.IsInstanceValid(cardVisual))
		{
			cardVisual.Scale = Vector2.One;
			cardVisual.Modulate = Colors.White;
		}
	}

	private static void UpdateDrawnCardFlightMotion(
		Control flightCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float startVisualScale,
		float targetVisualScale,
		bool pulseScale,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = pulseScale
			? -Mathf.Sin(clampedProgress * Mathf.Pi) * ReplacementDrawArcHeight
			: 0.0f;
		float currentVisualScale = GetDrawnCardFlightScale(startVisualScale, targetVisualScale, pulseScale, clampedProgress);
		float rotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress);
		if (!Mathf.IsEqualApprox(startRotationDegrees, targetRotationDegrees))
		{
			rotationDegrees += Mathf.Sin(clampedProgress * Mathf.Pi) * 7.0f;
		}

		flightCard.Scale = Vector2.One * currentVisualScale;
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = rotationDegrees;
	}

	private static float GetDrawnCardFlightScale(
		float startVisualScale,
		float targetVisualScale,
		bool pulseScale,
		float progress)
	{
		if (!pulseScale)
		{
			return 1.0f;
		}

		float peakScale = targetVisualScale * ReplacementDrawPeakScale;
		if (progress <= 0.5f)
		{
			return Mathf.Lerp(startVisualScale, peakScale, progress / 0.5f);
		}

		return Mathf.Lerp(peakScale, targetVisualScale, (progress - 0.5f) / 0.5f);
	}

	private static void TweenDrawnCardFlightVisuals(
		Tween tween,
		Control flightCard,
		TextureRect cardVisual,
		TextureRect? attachmentOverlay,
		Texture2D startTexture,
		Texture2D targetTexture,
		bool flipToFace)
	{
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

	private Vector2 GetRenderedDrawCardStartGlobalPosition(int playerIndex)
	{
		Control? visiblePileCard = GetVisiblePileCardControl(playerIndex);
		if (visiblePileCard != null)
		{
			return visiblePileCard.GetGlobalRect().Position;
		}

		return GetDrawCardStartGlobalPosition(playerIndex);
	}

	private Vector2 GetRenderedHandDisplayCardGlobalPosition(int playerIndex, int displayIndex)
	{
		if (playerIndex >= 0 && playerIndex < _playerHandAreas.Count)
		{
			Control? cardSlot = _playerHandAreas[playerIndex].GetNodeOrNull<Control>($"HandCard_{displayIndex}");
			TextureRect? cardVisual = FindHandCardVisual(cardSlot);
			if (cardVisual != null)
			{
				return cardVisual.GetGlobalRect().Position;
			}
		}

		return GetHandDisplayCardGlobalPosition(playerIndex, displayIndex);
	}

	private Control? GetVisiblePileCardControl(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerCardPiles.Count)
		{
			return null;
		}

		for (int cardIndex = VisiblePileCards - 1; cardIndex >= 0; cardIndex--)
		{
			if (GetPlayerCardPileBack(playerIndex, cardIndex) is Control visibleCard
				&& visibleCard.Visible)
			{
				return visibleCard;
			}
		}

		return null;
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
