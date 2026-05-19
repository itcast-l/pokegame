using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
		TaskCompletionSource<bool>? magnetAttachmentCompletion = _pendingMagnetAttachmentCompletion;
		ClearMagnetAttachmentCountdown();
		_isAnimatingMagnetAttachment = false;
		_pendingItemSelectionMode = PendingItemSelectionMode.None;
		_pendingItemOwnerIndex = -1;
		_pendingItemHandIndex = -1;
		_pendingItemTurnSequenceGeneration = -1;
		_pendingItemCardId = string.Empty;
		_revealedPlayerHandIndex = -1;
		magnetAttachmentCompletion?.TrySetResult(true);
	}

	private async Task ResolvePendingMagnetAttachmentsAsync(int turnSequenceGeneration)
	{
		while (_pendingMagnetAttachmentRewardPlayers.Count > 0)
		{
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			int playerIndex = _pendingMagnetAttachmentRewardPlayers.Dequeue();
			if (!PlayerHasAttachableMagnetCard(playerIndex))
			{
				continue;
			}

			if (playerIndex == MainPlayerIndex)
			{
				await ResolveMainPlayerMagnetAttachmentAsync(turnSequenceGeneration);
			}
			else
			{
				AttachMagnetToRandomHandCard(playerIndex);
			}

			RenderPlayerHands();
		}
	}

	private async Task ResolveMainPlayerMagnetAttachmentAsync(int turnSequenceGeneration)
	{
		if (_isResolvingMagnetAttachmentSelection || !PlayerHasAttachableMagnetCard(MainPlayerIndex))
		{
			return;
		}

		bool wasProcessingCardPlay = _isProcessingCardPlay;
		_isResolvingMagnetAttachmentSelection = true;
		_isProcessingCardPlay = true;
		_pendingItemCardId = MagnetItemCardId;
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
				await CompleteMagnetAttachmentSelectionAsync(PickRandomAttachableMagnetHandIndex(MainPlayerIndex));
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
			Text = "选择基础牌",
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
		if (!PlayerHasAttachableMagnetCard(MainPlayerIndex))
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
		int attachHandIndex = IsValidMagnetAttachmentHandIndex(ownerPlayerIndex, handIndex)
			? handIndex
			: PickRandomAttachableMagnetHandIndex(ownerPlayerIndex);
		if (attachHandIndex < 0)
		{
			ClearSelectionOverlay();
			ClearPendingItemSelectionState();
			RenderPlayerHands();
			return;
		}

		_isAnimatingMagnetAttachment = true;
		ClearMagnetAttachmentCountdown();
		await PlayMagnetAttachmentToHandCardAnimationAsync(ownerPlayerIndex, attachHandIndex);
		bool attached = AttachMagnetToHandCard(ownerPlayerIndex, attachHandIndex);
		if (!attached)
		{
			AttachMagnetToRandomHandCard(ownerPlayerIndex);
		}

		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();
	}

	private bool PlayerHasAttachableMagnetCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return false;
		}

		foreach (PlayingCard card in _playerHands[playerIndex])
		{
			if (IsAttachableMagnetCard(card))
			{
				return true;
			}
		}

		return false;
	}

	private bool AttachMagnetToHandCard(int playerIndex, int handIndex)
	{
		if (!IsValidMagnetAttachmentHandIndex(playerIndex, handIndex))
		{
			return false;
		}

		PlayingCard card = _playerHands[playerIndex][handIndex];
		_playerHands[playerIndex][handIndex] = card.WithMagnetEffect();
		return true;
	}

	private bool AttachMagnetToRandomHandCard(int playerIndex)
	{
		int handIndex = PickRandomAttachableMagnetHandIndex(playerIndex);
		return AttachMagnetToHandCard(playerIndex, handIndex);
	}

	private int PickRandomAttachableMagnetHandIndex(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return -1;
		}

		var candidates = new List<int>();
		for (int handIndex = 0; handIndex < _playerHands[playerIndex].Count; handIndex++)
		{
			if (IsAttachableMagnetCard(_playerHands[playerIndex][handIndex]))
			{
				candidates.Add(handIndex);
			}
		}

		return candidates.Count == 0 ? -1 : candidates[_rng.RandiRange(0, candidates.Count - 1)];
	}

	private bool IsValidMagnetAttachmentHandIndex(int playerIndex, int handIndex)
	{
		return playerIndex >= 0
			&& playerIndex < _playerHands.Count
			&& handIndex >= 0
			&& handIndex < _playerHands[playerIndex].Count
			&& IsAttachableMagnetCard(_playerHands[playerIndex][handIndex]);
	}

	private async Task PlayMagnetAttachmentToHandCardAnimationAsync(int playerIndex, int handIndex)
	{
		if (!IsInsideTree() || !IsValidMagnetAttachmentHandIndex(playerIndex, handIndex))
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
		if (_magnetCardOverlayTexture != null)
		{
			magnetOverlay = new TextureRect
			{
				Name = "MagnetAttachmentOverlay",
				Texture = _magnetCardOverlayTexture,
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

	private static bool IsAttachableMagnetCard(PlayingCard card)
	{
		return !card.HasMagnetEffect && !IsExcludedDeckRank(card.Rank);
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
		int collectedCardCount = CaptureTableCards(
			ownerPlayerIndex,
			0,
			out QuestionCardRewardAnimation? questionRewardAnimation);
		if (!IsGameOver())
		{
			Shuffle(_playerDecks[ownerPlayerIndex]);
			Cut(_playerDecks[ownerPlayerIndex]);
		}

		return new PlayCardResult(true, captured, collectedCardCount > 0, questionRewardAnimation);
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
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
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
		PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
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
			AddCardToPlayerDeck(ownerPlayerIndex, topCard);
		}

		Shuffle(_playerDecks[ownerPlayerIndex]);
		Shuffle(_playerDecks[targetPlayerIndex]);
		if (cardsToMove > 0)
		{
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}

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

		ItemCard itemCard = _playerItemHands[ownerPlayerIndex][itemHandIndex];
		Vector2? itemGlobalPosition = GetHandItemCardGlobalPosition(ownerPlayerIndex, itemHandIndex);
		_playerItemHands[ownerPlayerIndex].RemoveAt(itemHandIndex);
		ClearSelectionOverlay();
		ClearPendingItemSelectionState();
		RenderPlayerHands();

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

		PlayCardResult result = ResolveItemCardEffect(itemCardId, ownerPlayerIndex, targetPlayerIndex, targetHandIndex) with { CompletesTurn = false };
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
