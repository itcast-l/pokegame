using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolvePendingMagnetAttachmentsAsync(int turnSequenceGeneration)
	{
		while (_pendingItemAttachmentRewards.Count > 0)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			PendingItemAttachmentReward reward = _pendingItemAttachmentRewards.Dequeue();
			if (!PlayerHasAttachableMagnetCard(reward.PlayerIndex, reward.ItemCardId))
			{
				continue;
			}

			if (reward.PlayerIndex == MainPlayerIndex)
			{
				await ResolveMainPlayerMagnetAttachmentAsync(reward.ItemCardId, turnSequenceGeneration);
			}
			else
			{
				AttachMagnetToRandomHandCard(reward.PlayerIndex, reward.ItemCardId);
			}

			RenderPlayerHands();
		}
	}

	private async Task ResolveMainPlayerMagnetAttachmentAsync(string itemCardId, int turnSequenceGeneration)
	{
		if (_isResolvingMagnetAttachmentSelection || !PlayerHasAttachableMagnetCard(MainPlayerIndex, itemCardId))
		{
			return;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		_isResolvingMagnetAttachmentSelection = true;
		_isProcessingCardPlay = true;
		_pendingItemCardId = itemCardId;
		_pendingItemOwnerIndex = MainPlayerIndex;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = turnSequenceGeneration;
		_pendingItemSelectionMode = PendingItemSelectionMode.MagnetHandCard;
		_pendingMagnetAttachmentCompletion = new TaskCompletionSource<bool>();
		RenderPlayerHands();
		ShowMagnetHandSelection();

		try
		{
			System.Threading.Tasks.Task timeoutTask = WaitForMagnetAttachmentTimeoutAsync();
			await Task.WhenAny(_pendingMagnetAttachmentCompletion.Task, timeoutTask);
			if (turnSequenceGeneration == _turnSequenceGeneration
				&& _pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
				&& _pendingItemOwnerIndex == MainPlayerIndex)
			{
				await CompleteMagnetAttachmentSelectionAsync(PickRandomAttachableMagnetHandIndex(MainPlayerIndex, itemCardId));
			}

			while (_pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard
				&& turnSequenceGeneration == _turnSequenceGeneration
				&& !IsGameOver())
			{
				await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
			}
		}
		finally
		{
			if (_pendingItemSelectionMode == PendingItemSelectionMode.MagnetHandCard)
			{
				ClearSelectionOverlay();
				ClearPendingItemSelectionState();
			}

			_isResolvingMagnetAttachmentSelection = false;
			_pendingMagnetAttachmentCompletion = null;
			_isProcessingCardPlay = wasProcessingCardPlay;
			RenderPlayerHands();
		}
	}

	private async System.Threading.Tasks.Task WaitForMagnetAttachmentTimeoutAsync()
	{
		await ToSignal(GetTree().CreateTimer(MagnetAttachmentSelectionSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	private void ShowMagnetAttachmentCountdown()
	{
		ClearMagnetAttachmentCountdown();

		const float countdownWidth = 176.0f;
		const float countdownHeight = 172.0f;
		_magnetAttachmentCountdownRemainingSeconds = MagnetAttachmentSelectionSeconds;
		_magnetAttachmentCountdownPulseTime = 0.0;

		var root = new Control
		{
			Name = "MagnetAttachmentCountdown",
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0.5f,
			AnchorTop = 0.5f,
			AnchorRight = 0.5f,
			AnchorBottom = 0.5f,
			OffsetLeft = -countdownWidth / 2.0f,
			OffsetTop = -countdownHeight / 2.0f,
			OffsetRight = countdownWidth / 2.0f,
			OffsetBottom = countdownHeight / 2.0f,
			PivotOffset = new Vector2(countdownWidth, countdownHeight) / 2.0f,
			ZIndex = 1900,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};

		if (_magnetCountdownFrameTextures.Count > 0)
		{
			var countdownFrame = new TextureRect
			{
				Name = "MagnetAttachmentCountdownFrame",
				Texture = _magnetCountdownFrameTextures[0],
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = new Vector2(10.0f, -2.0f),
				Size = new Vector2(156.0f, 156.0f),
				PivotOffset = new Vector2(78.0f, 78.0f),
				ZIndex = 0,
			};
			root.AddChild(countdownFrame);
		}

		var ring = new MagnetAttachmentCountdownRing
		{
			Name = "MagnetAttachmentCountdownRing",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(24.0f, 10.0f),
			Size = new Vector2(128.0f, 128.0f),
			ZIndex = 1,
			countdown_progress = 1.0f,
		};
		root.AddChild(ring);

		var countdownLabel = new Label
		{
			Name = "MagnetAttachmentCountdownLabel",
			Text = Mathf.CeilToInt((float)MagnetAttachmentSelectionSeconds).ToString(),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = ring.Position,
			Size = ring.Size,
			ZIndex = 2,
		};
		countdownLabel.AddThemeFontSizeOverride("font_size", 46);
		countdownLabel.AddThemeColorOverride("font_color", Colors.White);
		countdownLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.08f, 0.12f, 0.72f));
		countdownLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		countdownLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		root.AddChild(countdownLabel);

		var hintLabel = new Label
		{
			Name = "MagnetAttachmentCountdownHint",
			Text = Localize("GAME_SELECT_BASE_CARD"),
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(0.0f, 146.0f),
			Size = new Vector2(countdownWidth, 22.0f),
			ZIndex = 2,
		};
		hintLabel.AddThemeFontSizeOverride("font_size", 16);
		hintLabel.AddThemeColorOverride("font_color", new Color(0.82f, 1.0f, 0.98f, 0.95f));
		hintLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.08f, 0.12f, 0.72f));
		hintLabel.AddThemeConstantOverride("shadow_offset_x", 0);
		hintLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		root.AddChild(hintLabel);

		AddChild(root);
		_magnetAttachmentCountdownRoot = root;
		_magnetAttachmentCountdownRing = ring;
		_magnetAttachmentCountdownLabel = countdownLabel;
		_magnetAttachmentCountdownHintLabel = hintLabel;

		Tween introTween = CreateTween();
		introTween.SetTrans(Tween.TransitionType.Cubic);
		introTween.SetEase(Tween.EaseType.Out);
		introTween.TweenProperty(root, "modulate", Colors.White, 0.16f);
	}

	private void UpdateMagnetAttachmentCountdown(double delta)
	{
		if (_magnetAttachmentCountdownRoot == null || !GodotObject.IsInstanceValid(_magnetAttachmentCountdownRoot))
		{
			return;
		}

		if (!IsMainPlayerMagnetSelectionActive())
		{
			ClearMagnetAttachmentCountdown();
			return;
		}

		_magnetAttachmentCountdownRemainingSeconds = Mathf.Max(
			0.0f,
			(float)(_magnetAttachmentCountdownRemainingSeconds - delta));
		_magnetAttachmentCountdownPulseTime += delta;
		float progress = Mathf.Clamp(
			(float)(_magnetAttachmentCountdownRemainingSeconds / MagnetAttachmentSelectionSeconds),
			0.0f,
			1.0f);
		int displaySeconds = progress <= 0.0f
			? 0
			: Mathf.CeilToInt((float)_magnetAttachmentCountdownRemainingSeconds);

		if (_magnetAttachmentCountdownLabel != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownLabel))
		{
			_magnetAttachmentCountdownLabel.Text = displaySeconds.ToString();
			_magnetAttachmentCountdownLabel.AddThemeColorOverride(
				"font_color",
				progress <= 0.35f ? new Color(1.0f, 0.86f, 0.42f) : Colors.White);
		}

		if (_magnetAttachmentCountdownHintLabel != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownHintLabel))
		{
			_magnetAttachmentCountdownHintLabel.Modulate = new Color(
				1.0f,
				1.0f,
				1.0f,
				progress <= 0.35f ? 0.78f + 0.22f * Mathf.Sin((float)_magnetAttachmentCountdownPulseTime * 9.0f) : 1.0f);
		}

		if (_magnetAttachmentCountdownRing != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownRing))
		{
			_magnetAttachmentCountdownRing.countdown_progress = progress;
		}
	}

	private void ClearMagnetAttachmentCountdown()
	{
		if (_magnetAttachmentCountdownRoot != null && GodotObject.IsInstanceValid(_magnetAttachmentCountdownRoot))
		{
			RemoveChild(_magnetAttachmentCountdownRoot);
			_magnetAttachmentCountdownRoot.QueueFree();
		}

		_magnetAttachmentCountdownRoot = null;
		_magnetAttachmentCountdownRing = null;
		_magnetAttachmentCountdownLabel = null;
		_magnetAttachmentCountdownHintLabel = null;
		_magnetAttachmentCountdownRemainingSeconds = 0.0;
		_magnetAttachmentCountdownPulseTime = 0.0;
	}

	private void ShowMagnetHandSelection()
	{
		ClearSelectionOverlay();
		if (!PlayerHasAttachableMagnetCard(MainPlayerIndex, _pendingItemCardId))
		{
			_ = CompleteMagnetAttachmentSelectionAsync(-1);
			return;
		}

		RenderPlayerHands();
		ShowMagnetAttachmentCountdown();
	}

	private async Task CompleteMagnetAttachmentSelectionAsync(int handIndex)
	{
		if (_pendingItemSelectionMode != PendingItemSelectionMode.MagnetHandCard)
		{
			return;
		}

		if (_isAnimatingMagnetAttachment)
		{
			return;
		}

		int ownerPlayerIndex = _pendingItemOwnerIndex;
		string itemCardId = _pendingItemCardId;
		int attachHandIndex = IsValidMagnetAttachmentHandIndex(ownerPlayerIndex, handIndex, itemCardId)
			? handIndex
			: PickRandomAttachableMagnetHandIndex(ownerPlayerIndex, itemCardId);
		if (attachHandIndex < 0)
		{
			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			return;
		}

		_isAnimatingMagnetAttachment = true;
		ClearMagnetAttachmentCountdown();
		await PlayMagnetAttachmentToHandCardAnimationAsync(ownerPlayerIndex, attachHandIndex, itemCardId);
		bool attached = AttachMagnetToHandCard(ownerPlayerIndex, attachHandIndex, itemCardId);
		if (!attached)
		{
			AttachMagnetToRandomHandCard(ownerPlayerIndex, itemCardId);
		}

		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();
	}

	private bool PlayerHasAttachableMagnetCard(int playerIndex, string itemCardId)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		foreach (PlayingCard card in _playerHands[playerIndex])
		{
			if (IsAttachableMagnetCard(card, itemCardId))
			{
				return true;
			}
		}

		return false;
	}

	private bool AttachMagnetToHandCard(int playerIndex, int handIndex, string itemCardId)
	{
		if (!IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return false;
		}

		PlayingCard card = _playerHands[playerIndex][handIndex];
		_playerHands[playerIndex][handIndex] = card.WithAttachedItemEffect(itemCardId);
		return true;
	}

	private bool AttachMagnetToRandomHandCard(int playerIndex, string itemCardId)
	{
		int handIndex = PickRandomAttachableMagnetHandIndex(playerIndex, itemCardId);
		return AttachMagnetToHandCard(playerIndex, handIndex, itemCardId);
	}

	private int PickRandomAttachableMagnetHandIndex(int playerIndex, string itemCardId)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return -1;
		}

		var candidates = new List<int>();
		for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
		{
			if (IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId))
			{
				candidates.Add(handIndex);
			}
		}

		return candidates.Count == 0 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
	}

	private bool IsValidMagnetAttachmentHandIndex(int playerIndex, int handIndex, string itemCardId)
	{
		return playerIndex >= 0
			&& playerIndex < _playerHands.Count
			&& handIndex >= 0
			&& handIndex < _playerHands[playerIndex].Count
			&& IsAttachableMagnetCard(_playerHands[playerIndex][handIndex], itemCardId);
	}

	private async Task PlayMagnetAttachmentToHandCardAnimationAsync(int playerIndex, int handIndex, string itemCardId)
	{
		if (!IsInsideTree() || !IsValidMagnetAttachmentHandIndex(playerIndex, handIndex, itemCardId))
		{
			return;
		}

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

		var ring = new MagnetAttachmentApplyRing
		{
			Name = "MagnetAttachmentApplyRing",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(-34.0f, -22.0f),
			Size = new Vector2(140.0f, 140.0f),
			PivotOffset = new Vector2(70.0f, 70.0f),
			ZIndex = 1,
			ring_progress = 0.0f,
			ring_alpha = 0.0f,
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
		MagnetAttachmentApplyRing ring,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees)
	{
		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Cubic);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(animationRoot, "global_position", targetGlobalPosition, 0.24f);
		tween.TweenProperty(animationRoot, "scale", Vector2.One * 1.28f, 0.24f);
		tween.TweenProperty(animationRoot, "rotation_degrees", startRotationDegrees + 9.0f, 0.24f);
		tween.TweenProperty(cardVisual, "modulate", Colors.White, 0.24f);
		tween.TweenProperty(ring, "ring_alpha", 0.75f, 0.18f);
		tween.TweenProperty(ring, "ring_progress", 0.32f, 0.24f);
		await ToSignal(tween, Tween.SignalName.Finished);
	}

	private async Task TweenMagnetAttachmentApplyAsync(
		Control animationRoot,
		TextureRect cardVisual,
		TextureRect? magnetOverlay,
		MagnetAttachmentApplyRing ring)
	{
		Tween closeTween = CreateTween();
		closeTween.SetParallel(true);
		closeTween.SetTrans(Tween.TransitionType.Cubic);
		closeTween.SetEase(Tween.EaseType.In);
		closeTween.TweenProperty(animationRoot, "scale", new Vector2(0.08f, 1.42f), 0.18f);
		closeTween.TweenProperty(cardVisual, "modulate", new Color(0.82f, 1.0f, 0.98f, 1.0f), 0.18f);
		closeTween.TweenProperty(ring, "ring_progress", 0.62f, 0.18f);
		closeTween.TweenProperty(ring, "ring_alpha", 1.0f, 0.12f);
		await ToSignal(closeTween, Tween.SignalName.Finished);

		if (magnetOverlay != null)
		{
			magnetOverlay.Modulate = Colors.White;
			magnetOverlay.Scale = Vector2.One;
		}

		Tween openTween = CreateTween();
		openTween.SetParallel(true);
		openTween.SetTrans(Tween.TransitionType.Cubic);
		openTween.SetEase(Tween.EaseType.Out);
		openTween.TweenProperty(animationRoot, "scale", Vector2.One * 1.25f, 0.28f);
		openTween.TweenProperty(cardVisual, "modulate", Colors.White, 0.28f);
		openTween.TweenProperty(ring, "ring_progress", 1.0f, 0.28f);
		openTween.TweenProperty(ring, "ring_alpha", 0.0f, 0.28f);
		await ToSignal(openTween, Tween.SignalName.Finished);
	}

	private async Task TweenMagnetAttachmentCardBackAsync(
		Control animationRoot,
		TextureRect? magnetOverlay,
		Vector2 targetGlobalPosition,
		Vector2 startGlobalPosition,
		float startRotationDegrees)
	{
		Tween tween = CreateTween();
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

		await ToSignal(tween, Tween.SignalName.Finished);
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
		string modifier = GetAttachmentModifier(itemCardId);
		return modifier switch
		{
			CardAttachmentModifiers.Double => _doubleCardOverlayTexture,
			CardAttachmentModifiers.Overtime => _overtimeCardOverlayTexture,
			_ => _magnetCardOverlayTexture,
		};
	}

	private bool IsAttachableMagnetCard(PlayingCard card, string itemCardId)
	{
		if (IsExcludedDeckRank(card.Rank))
		{
			return false;
		}

		return !card.HasAttachedItemEffect(itemCardId);
	}

	private partial class MagnetAttachmentCountdownRing : Control
	{
		private float _countdownProgress = 1.0f;
		public float countdown_progress
		{
			get => _countdownProgress;
			set
			{
				_countdownProgress = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			Vector2 center = Size / 2.0f;
			float radius = Mathf.Min(Size.X, Size.Y) * 0.36f;
			bool urgent = _countdownProgress <= 0.35f;
			Color accentColor = urgent
				? new Color(1.0f, 0.76f, 0.24f, 0.95f)
				: new Color(0.58f, 1.0f, 0.96f, 0.95f);

			DrawArc(center, radius + 7.0f, 0.0f, FullCircleRadians, 96, new Color(0.76f, 1.0f, 0.96f, 0.24f), 5.0f, true);

			if (_countdownProgress > 0.0f)
			{
				float startAngle = -Mathf.Pi / 2.0f;
				float endAngle = startAngle + FullCircleRadians * _countdownProgress;
				int pointCount = Mathf.Max(12, Mathf.CeilToInt(96.0f * _countdownProgress));
				DrawArc(center, radius + 7.0f, startAngle, endAngle, pointCount, accentColor, urgent ? 7.0f : 6.0f, true);
			}

			DrawArc(center, radius - 8.0f, 0.0f, FullCircleRadians, 96, new Color(1.0f, 1.0f, 1.0f, 0.30f), 2.0f, true);
		}
	}

	private partial class MagnetAttachmentApplyRing : Control
	{
		private float _ringProgress;
		private float _ringAlpha;

		public float ring_progress
		{
			get => _ringProgress;
			set
			{
				_ringProgress = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public float ring_alpha
		{
			get => _ringAlpha;
			set
			{
				_ringAlpha = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			if (_ringAlpha <= 0.0f)
			{
				return;
			}

			Vector2 center = Size / 2.0f;
			float radius = Mathf.Lerp(30.0f, 58.0f, _ringProgress);
			DrawCircle(center, radius + 13.0f, new Color(0.08f, 0.78f, 0.92f, 0.14f * _ringAlpha));
			DrawArc(center, radius + 7.0f, -Mathf.Pi / 2.0f, -Mathf.Pi / 2.0f + FullCircleRadians * _ringProgress, 72, new Color(0.42f, 1.0f, 0.95f, 0.92f * _ringAlpha), 6.0f, true);
			DrawArc(center, radius - 7.0f, 0.0f, FullCircleRadians, 72, new Color(1.0f, 0.94f, 0.38f, 0.44f * _ringAlpha), 2.5f, true);
			DrawArc(center, radius + 15.0f, 0.0f, FullCircleRadians, 72, new Color(1.0f, 1.0f, 1.0f, 0.22f * _ringAlpha), 2.0f, true);
		}
	}
}
