using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayMagnetAttachmentToHandCardAnimationAsync(int playerIndex, int handIndex, string itemCardId)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		EnsureItemModeOptionalFxResourcesLoaded();

		if (!IsInsideTree() || !IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return;
		}

		PlayGameSfx(GameSfx.MagnetAttach, -1.0f, 0.97f, 1.03f, 0.08);
		PlayingCard card = _playerHands[playerIndex][handIndex];
		Vector2 startGlobalPosition = GetHandCardGlobalPosition(playerIndex, handIndex);
		Vector2 targetGlobalPosition = GetMagnetAttachmentAnimationTargetGlobalPosition();
		float startRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);

		Control? handArea = playerIndex >= 0 && playerIndex < _playerHandAreas.Count ? _playerHandAreas[playerIndex] : null;
		Control? sourceCardSlot = handArea?.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		var animationRoot = new Control
		{
			Name = "MagnetAttachmentCardAnimation",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
			ZIndex = PlayedCardFlightZIndex + 900,
			ZAsRelative = false,
		};
		AddChild(animationRoot);
		animationRoot.GlobalPosition = startGlobalPosition;

		var cardVisual = new TextureRect
		{
			Name = "MagnetAttachmentCardVisual",
			Texture = card.Texture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = 0,
		};
		animationRoot.AddChild(cardVisual);

		TextureRect? magnetOverlay = null;
		Texture2D? overlayTexture = GetItemAttachmentOverlayTexture(itemCardId);
		if (overlayTexture != null)
		{
			magnetOverlay = new TextureRect
			{
				Name = "MagnetAttachmentOverlay",
				Texture = overlayTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = 2,
				Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
				Scale = Vector2.One * 1.24f,
			};
			animationRoot.AddChild(magnetOverlay);
		}

		var ring = new TextureRect
		{
			Name = "MagnetAttachmentApplyRing",
			Texture = _magnetAttachmentApplyRingFrames.Count > 0 ? _magnetAttachmentApplyRingFrames[0] : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(-34.0f, -22.0f),
			Size = new Vector2(140.0f, 140.0f),
			PivotOffset = new Vector2(70.0f, 70.0f),
			ZIndex = 1,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		animationRoot.AddChild(ring);

		try
		{
			await TweenMagnetAttachmentCardOutAsync(
				animationRoot,
				cardVisual,
				ring,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees);
			await TweenMagnetAttachmentApplyAsync(animationRoot, cardVisual, magnetOverlay, ring);
			await TweenMagnetAttachmentCardBackAsync(animationRoot, magnetOverlay, targetGlobalPosition, startGlobalPosition, startRotationDegrees);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(animationRoot))
			{
				animationRoot.QueueFree();
			}

			if (sourceCardSlot != null && GodotObject.IsInstanceValid(sourceCardSlot))
			{
				sourceCardSlot.Visible = true;
			}
		}
	}

	private async Task TweenMagnetAttachmentCardOutAsync(
		Control animationRoot,
		TextureRect cardVisual,
		TextureRect ring,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees)
	{
		Tween tween = CreateBoardTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(animationRoot, "global_position", targetGlobalPosition, 0.24f);
		tween.TweenProperty(animationRoot, "scale", Vector2.One * 1.28f, 0.24f);
		tween.TweenProperty(animationRoot, "rotation_degrees", startRotationDegrees + 9.0f, 0.24f);
		tween.TweenProperty(cardVisual, "modulate", Colors.White, 0.24f);
		tween.TweenProperty(ring, "modulate:a", 0.75f, 0.18f);
		FrameAnimationHelpers.TweenTextureFramesForDuration(tween, ring, _magnetAttachmentApplyRingFrames, 0.04f, 0.24f);
		await WaitForBoardTweenAsync(tween);
	}

	private async Task TweenMagnetAttachmentApplyAsync(
		Control animationRoot,
		TextureRect cardVisual,
		TextureRect? magnetOverlay,
		TextureRect ring)
	{
		Tween closeTween = CreateBoardTween();
		closeTween.SetParallel(true);
		closeTween.SetTrans(Tween.TransitionType.Cubic);
		closeTween.SetEase(Tween.EaseType.In);
		closeTween.TweenProperty(animationRoot, "scale", new Vector2(0.08f, 1.42f), 0.18f);
		closeTween.TweenProperty(cardVisual, "modulate", new Color(0.82f, 1.0f, 0.98f, 1.0f), 0.18f);
		closeTween.TweenProperty(ring, "modulate:a", 1.0f, 0.12f);
		FrameAnimationHelpers.TweenTextureFramesForDuration(closeTween, ring, _magnetAttachmentApplyRingFrames, 0.03f, 0.18f);
		await WaitForBoardTweenAsync(closeTween);

		if (magnetOverlay != null)
		{
			magnetOverlay.Modulate = Colors.White;
			magnetOverlay.Scale = Vector2.One;
		}

		Tween openTween = CreateBoardTween();
		openTween.SetParallel(true);
		openTween.SetTrans(Tween.TransitionType.Cubic);
		openTween.SetEase(Tween.EaseType.Out);
		openTween.TweenProperty(animationRoot, "scale", Vector2.One * 1.25f, 0.28f);
		openTween.TweenProperty(cardVisual, "modulate", Colors.White, 0.28f);
		openTween.TweenProperty(ring, "scale", Vector2.One * 1.18f, 0.28f);
		openTween.TweenProperty(ring, "modulate:a", 0.0f, 0.28f);
		FrameAnimationHelpers.TweenTextureFramesForDuration(openTween, ring, _magnetAttachmentApplyRingFrames, 0.045f, 0.28f);
		await WaitForBoardTweenAsync(openTween);
	}

	private async Task TweenMagnetAttachmentCardBackAsync(
		Control animationRoot,
		TextureRect? magnetOverlay,
		Vector2 targetGlobalPosition,
		Vector2 startGlobalPosition,
		float startRotationDegrees)
	{
		Tween tween = CreateBoardTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.InOut);
		tween.TweenProperty(animationRoot, "global_position", startGlobalPosition, 0.24f);
		tween.TweenProperty(animationRoot, "scale", Vector2.One, 0.24f);
		tween.TweenProperty(animationRoot, "rotation_degrees", startRotationDegrees, 0.24f);
		if (magnetOverlay != null)
		{
			tween.TweenProperty(magnetOverlay, "modulate", Colors.White, 0.16f);
		}

		await WaitForBoardTweenAsync(tween);
	}

	private Vector2 GetHandCardGlobalPosition(int playerIndex, int handIndex)
	{
		if (playerIndex >= 0 && playerIndex < _playerHandAreas.Count)
		{
			Control handArea = _playerHandAreas[playerIndex];
			Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
			TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
			if (sourceCardVisual != null)
			{
				return sourceCardVisual.GlobalPosition;
			}

			int displayCardCount = playerIndex < _playerHands.Count && playerIndex < _playerItemHands.Count
				? _playerHands[playerIndex].Count + _playerItemHands[playerIndex].Count
				: 0;
			Vector2 cardOffset = GetHandCardOffset(playerIndex, displayCardCount, handArea.Size);
			Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, displayCardCount, handArea.Size, cardOffset);
			return handArea.GlobalPosition + firstCardPosition + handIndex * cardOffset;
		}

		return GlobalPosition + (Size - CardVisualSize) / 2.0f;
	}

	private Vector2 GetMagnetAttachmentAnimationTargetGlobalPosition()
	{
		return GlobalPosition + (Size - CardVisualSize) / 2.0f + new Vector2(0.0f, -12.0f);
	}

	private Texture2D? GetItemAttachmentOverlayTexture(string itemCardId)
	{
		return _itemAttachmentOverlaysById.TryGetValue(itemCardId, out ItemAttachmentOverlay overlay)
			? overlay.Texture
			: null;
	}
}
