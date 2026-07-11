using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<bool> PlayTableCardsShuffleAnimationAsync(IReadOnlyList<TableCard> shuffledTableCards, int turnSequenceGeneration)
	{
		EnsureItemModeOptionalFxResourcesLoaded();

		if (!IsInsideTree()
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| shuffledTableCards.Count <= 1
			|| shuffledTableCards.Count != _tableCards.Count)
		{
			return false;
		}

		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastTableShuffleVisualEventFromHost(
				_currentTurnPlayerIndex,
				BuildShuffleTargetIndexes(shuffledTableCards),
				turnSequenceGeneration,
				snapshotId);
		}

		List<ShuffledTableCardVisual> shuffledVisuals = BuildShuffledTableCardVisuals(shuffledTableCards);
		if (shuffledVisuals.Count == 0)
		{
			return false;
		}

		var ownedTableCards = new TableCard[_tableCards.Count];
		var originalHiddenStates = new bool[_tableCards.Count];
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			ownedTableCards[tableCardIndex] = tableCard;
			originalHiddenStates[tableCardIndex] = tableCard.IsHiddenDuringCapture;
		}

		PlayGameSfx(GameSfx.TableShuffle, -1.5f, 0.97f, 1.03f, 0.12);
		Control?[] flightCards = new Control?[shuffledVisuals.Count];
		TextureRect? vortex = null;
		Tween? gatherTween = null;
		Tween? scatterTween = null;
		Tween? vortexFrameTween = null;
		try
		{
			foreach (TableCard tableCard in ownedTableCards)
			{
				if (!IsFixedDuringTableShuffle(tableCard))
				{
					tableCard.IsHiddenDuringCapture = true;
				}
			}
			RenderTableCards();

			vortex = CreateTableShuffleVortex(GetQuestionCardTransformAreaCardGlobalPosition(Vector2.Zero) + CardVisualSize / 2.0f);
			gatherTween = CreateTween();
			gatherTween.SetParallel(true);
			gatherTween.SetTrans(Tween.TransitionType.Sine);
			gatherTween.SetEase(Tween.EaseType.InOut);
			for (int visualIndex = 0; visualIndex < shuffledVisuals.Count; visualIndex++)
			{
				ShuffledTableCardVisual visual = shuffledVisuals[visualIndex];
				Control flightCard = CreateShuffledTableCardFlight(visual, visualIndex);
				flightCards[visualIndex] = flightCard;
				gatherTween.TweenMethod(
					Callable.From<float>(progress => UpdateShuffledTableCardGatherFlight(flightCard, visual, visualIndex, progress)),
					0.0f,
					1.0f,
					TableShuffleGatherSeconds);
				gatherTween.TweenProperty(flightCard, "scale", Vector2.One * 0.86f, TableShuffleGatherSeconds)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			}
			gatherTween.TweenProperty(vortex, "modulate:a", 1.0f, TableShuffleGatherSeconds * 0.55f);
			gatherTween.TweenProperty(vortex, "scale", Vector2.One * 1.08f, TableShuffleGatherSeconds);
			vortexFrameTween = CreateTween();
			vortexFrameTween.SetParallel(true);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				vortexFrameTween,
				vortex,
				_tableShuffleVortexFrames,
				(TableShuffleGatherSeconds + TableShuffleScatterSeconds) / Mathf.Max(1, _tableShuffleVortexFrames.Count),
				TableShuffleGatherSeconds + TableShuffleScatterSeconds);

			await ToSignal(gatherTween, Tween.SignalName.Finished);
			if (!OwnsTableShuffleState(turnSequenceGeneration, ownedTableCards))
			{
				return false;
			}

			scatterTween = CreateTween();
			scatterTween.SetParallel(true);
			scatterTween.SetTrans(Tween.TransitionType.Sine);
			scatterTween.SetEase(Tween.EaseType.InOut);
			for (int visualIndex = 0; visualIndex < shuffledVisuals.Count; visualIndex++)
			{
				ShuffledTableCardVisual visual = shuffledVisuals[visualIndex];
				Control? flightCard = flightCards[visualIndex];
				if (flightCard == null)
				{
					continue;
				}

				scatterTween.TweenMethod(
					Callable.From<float>(progress => UpdateShuffledTableCardScatterFlight(flightCard, visual, visualIndex, progress)),
					0.0f,
					1.0f,
					TableShuffleScatterSeconds);
				scatterTween.TweenProperty(flightCard, "scale", Vector2.One, TableShuffleScatterSeconds)
					.SetTrans(Tween.TransitionType.Cubic)
					.SetEase(Tween.EaseType.Out);
			}
			scatterTween.TweenProperty(vortex, "modulate:a", 0.0f, TableShuffleScatterSeconds);
			scatterTween.TweenProperty(vortex, "scale", Vector2.One * 1.22f, TableShuffleScatterSeconds);

			await ToSignal(scatterTween, Tween.SignalName.Finished);
			return OwnsTableShuffleState(turnSequenceGeneration, ownedTableCards);
		}
		finally
		{
			if (gatherTween != null && gatherTween.IsValid())
			{
				gatherTween.Kill();
			}
			if (scatterTween != null && scatterTween.IsValid())
			{
				scatterTween.Kill();
			}
			if (vortexFrameTween != null && vortexFrameTween.IsValid())
			{
				vortexFrameTween.Kill();
			}

			foreach (Control? flightCard in flightCards)
			{
				if (flightCard != null && GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			}

			if (GodotObject.IsInstanceValid(vortex))
			{
				_animationPresenter.Release(vortex);
			}

			if (OwnsTableShuffleState(turnSequenceGeneration, ownedTableCards))
			{
				for (int tableCardIndex = 0; tableCardIndex < ownedTableCards.Length; tableCardIndex++)
				{
					ownedTableCards[tableCardIndex].IsHiddenDuringCapture = originalHiddenStates[tableCardIndex];
				}
				RenderTableCards();
			}
		}
	}

	private bool OwnsTableShuffleState(int turnSequenceGeneration, IReadOnlyList<TableCard> ownedTableCards)
	{
		if (turnSequenceGeneration != _turnSequenceGeneration || ownedTableCards.Count != _tableCards.Count)
		{
			return false;
		}

		for (int tableCardIndex = 0; tableCardIndex < ownedTableCards.Count; tableCardIndex++)
		{
			if (!ReferenceEquals(ownedTableCards[tableCardIndex], _tableCards[tableCardIndex]))
			{
				return false;
			}
		}

		return true;
	}

	private async Task PlayMantaRayTideShiftTableMoveAnimationAsync(int turnSequenceGeneration)
	{
		if (!IsInsideTree() || _tableCards.Count <= 1)
		{
			return;
		}

		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastMantaTideShiftVisualEventFromHost(
				_currentTurnPlayerIndex,
				turnSequenceGeneration,
				snapshotId);
		}

		List<ShuffledTableCardVisual> tideShiftVisuals = BuildMantaRayTideShiftTableMoveVisuals();
		if (tideShiftVisuals.Count == 0)
		{
			return;
		}

		bool[] hiddenStates = new bool[_tableCards.Count];
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			hiddenStates[tableCardIndex] = _tableCards[tableCardIndex].IsHiddenDuringCapture;
			_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
		}
		RenderTableCards();

		Control?[] flightCards = new Control?[tideShiftVisuals.Count];
		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			for (int visualIndex = 0; visualIndex < tideShiftVisuals.Count; visualIndex++)
			{
				ShuffledTableCardVisual visual = tideShiftVisuals[visualIndex];
				bool wrapsToEnd = visualIndex == 0;
				Control flightCard = CreateShuffledTableCardFlight(visual, visualIndex);
				if (wrapsToEnd)
				{
					flightCard.ZIndex += _tableCards.Count * 4;
				}

				flightCards[visualIndex] = flightCard;

				float delay = wrapsToEnd
					? 0.0f
					: Mathf.Min(0.08f, visualIndex * MantaRayTideShiftTableSlideStaggerSeconds);
				float duration = wrapsToEnd ? MantaRayTideShiftTableMoveSeconds : MantaRayTideShiftTableSlideSeconds;
				tween.TweenMethod(
						Callable.From<float>(progress => UpdateMantaRayTideShiftTableCardFlight(
							flightCard,
							visual,
							wrapsToEnd,
							progress)),
						0.0f,
						1.0f,
						duration)
					.SetDelay(delay);
			}

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			int restoreCount = Math.Min(hiddenStates.Length, _tableCards.Count);
			for (int index = 0; index < restoreCount; index++)
			{
				_tableCards[index].IsHiddenDuringCapture = hiddenStates[index];
			}

			foreach (Control? flightCard in flightCards)
			{
				if (flightCard != null && GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			}

			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				RenderTableCards();
			}
		}
	}

	private List<ShuffledTableCardVisual> BuildMantaRayTideShiftTableMoveVisuals()
	{
		var visuals = new List<ShuffledTableCardVisual>(_tableCards.Count);
		Rect2 tableLayoutRect = GetTableCardLayoutRect();

		int lastIndex = _tableCards.Count - 1;
		for (int sourceIndex = 0; sourceIndex < _tableCards.Count; sourceIndex++)
		{
			int targetIndex = sourceIndex == 0 ? lastIndex : sourceIndex - 1;
			int zIndex = sourceIndex == 0 ? _tableCards.Count * 2 + 4 : sourceIndex * 2;
			ShuffledTableCardVisual? visual = BuildShuffledTableCardVisual(
				_tableCards[sourceIndex],
				_centerArena.GlobalPosition + GetTableCardPosition(sourceIndex, tableLayoutRect),
				_centerArena.GlobalPosition + GetTableCardPosition(targetIndex, tableLayoutRect),
				Vector2.Zero,
				zIndex);
			if (visual.HasValue)
			{
				visuals.Add(visual.Value);
			}
		}

		return visuals;
	}

	private int[] BuildShuffleTargetIndexes(IReadOnlyList<TableCard> shuffledTableCards)
	{
		var result = new int[_tableCards.Count];
		for (int sourceIndex = 0; sourceIndex < _tableCards.Count; sourceIndex++)
		{
			TableCard sourceTableCard = _tableCards[sourceIndex];
			result[sourceIndex] = FindTableCardReferenceIndex(shuffledTableCards, sourceTableCard);
		}

		return result;
	}

	private List<ShuffledTableCardVisual> BuildShuffledTableCardVisuals(IReadOnlyList<TableCard> shuffledTableCards)
	{
		var visuals = new List<ShuffledTableCardVisual>(_tableCards.Count);
		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		Vector2 shuffleCenterGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(Vector2.Zero) + CardVisualSize / 2.0f;

		for (int sourceIndex = 0; sourceIndex < _tableCards.Count; sourceIndex++)
		{
			TableCard tableCard = _tableCards[sourceIndex];
			if (IsFixedDuringTableShuffle(tableCard))
			{
				continue;
			}

			int targetIndex = FindTableCardReferenceIndex(shuffledTableCards, tableCard);
			if (targetIndex < 0)
			{
				continue;
			}

			ShuffledTableCardVisual? visual = BuildShuffledTableCardVisual(
				tableCard,
				_centerArena.GlobalPosition + GetTableCardPosition(sourceIndex, tableLayoutRect),
				_centerArena.GlobalPosition + GetTableCardPosition(targetIndex, tableLayoutRect),
				shuffleCenterGlobalPosition,
				sourceIndex * 2);
			if (visual.HasValue)
			{
				visuals.Add(visual.Value);
			}
		}

		return visuals;
	}

	private static int FindTableCardReferenceIndex(IReadOnlyList<TableCard> tableCards, TableCard target)
	{
		for (int index = 0; index < tableCards.Count; index++)
		{
			if (ReferenceEquals(tableCards[index], target))
			{
				return index;
			}
		}

		return -1;
	}

	private ShuffledTableCardVisual? BuildShuffledTableCardVisual(
		TableCard tableCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		Vector2 shuffleCenterGlobalPosition,
		int zIndex)
	{
		Texture2D? mainTexture = null;
		bool animateCardBack = false;
		bool hideItemOverlay = false;
		float alpha = 1.0f;
		bool showMainPlayerPreview = ShouldShowMainPlayerTablePreview(tableCard);

		if (tableCard.Card.HasValue)
		{
			animateCardBack = !showMainPlayerPreview && !tableCard.IsFaceUp && _cardBackTexture != null;
			mainTexture = showMainPlayerPreview
				? tableCard.Card.Value.Texture
				: tableCard.IsFaceUp ? tableCard.Card.Value.Texture : _cardBackTexture;
			alpha = showMainPlayerPreview ? MainPlayerTablePreviewAlpha : 1.0f;
		}
		else if (tableCard.ItemOverlay.HasValue && !tableCard.IsFaceUp && _cardBackTexture != null)
		{
			animateCardBack = true;
			hideItemOverlay = true;
			mainTexture = _cardBackTexture;
		}
		else if (tableCard.ItemOverlay.HasValue)
		{
			mainTexture = tableCard.ItemOverlay.Value.Texture;
		}

		if (mainTexture == null)
		{
			return null;
		}

		Texture2D? itemOverlayTexture = null;
		Vector2 itemOverlayPosition = Vector2.Zero;
		Vector2 itemOverlaySize = CardVisualSize;
		if (tableCard.Card.HasValue && tableCard.IsSlimeCoated)
		{
			itemOverlayTexture = _slimeCardOverlayTexture;
		}

		if (itemOverlayTexture == null && tableCard.Card.HasValue && (tableCard.IsFaceUp || showMainPlayerPreview))
		{
			itemOverlayTexture = GetCardAttachmentOverlayTexture(tableCard.Card.Value);
		}

		if (itemOverlayTexture == null && tableCard.Card.HasValue && tableCard.ItemOverlay.HasValue && !hideItemOverlay)
		{
			ItemCard itemOverlay = tableCard.ItemOverlay.Value;
			itemOverlayTexture = itemOverlay.Texture;
			itemOverlayPosition = GetItemCardVisualPosition(itemOverlay.Id);
			itemOverlaySize = GetItemCardVisualSize(itemOverlay.Id);
		}

		return new ShuffledTableCardVisual(
			mainTexture,
			itemOverlayTexture,
			itemOverlayPosition,
			itemOverlaySize,
			animateCardBack,
			alpha,
			startGlobalPosition,
			targetGlobalPosition,
			shuffleCenterGlobalPosition,
			zIndex);
	}

	private Control CreateShuffledTableCardFlight(ShuffledTableCardVisual visual, int visualIndex)
	{
		var flightCard = new Control
		{
			Name = "ShuffledTableCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + visual.ZIndex + 520 + visualIndex,
			ZAsRelative = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, visual.Alpha),
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = visual.StartGlobalPosition;

		var mainVisual = new TextureRect
		{
			Name = "ShuffledTableCardMain",
			Texture = visual.MainTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = 0,
		};
		if (visual.AnimateCardBack)
		{
			MarkAnimatedCardBack(mainVisual);
		}
		flightCard.AddChild(mainVisual);

		if (visual.ItemOverlayTexture != null)
		{
			var itemOverlay = new TextureRect
			{
				Name = "ShuffledTableCardOverlay",
				Texture = visual.ItemOverlayTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = visual.ItemOverlayPosition,
				Size = visual.ItemOverlaySize,
				PivotOffset = visual.ItemOverlaySize / 2.0f,
				ZIndex = 1,
			};
			flightCard.AddChild(itemOverlay);
		}

		return flightCard;
	}

	private TextureRect CreateTableShuffleVortex(Vector2 globalCenter)
	{
		TextureRect vortex = _animationPresenter.RentTextureRect(
			this,
			"TableShuffleVortex",
			_tableShuffleVortexFrames.Count > 0 ? _tableShuffleVortexFrames[0] : null,
			new Vector2(180.0f, 180.0f),
			PlayedCardFlightZIndex + 500,
			CanvasItem.TextureFilterEnum.Linear,
			zAsRelative: false);
		vortex.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		vortex.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		vortex.GlobalPosition = globalCenter - vortex.Size / 2.0f;
		return vortex;
	}

	private static void UpdateShuffledTableCardGatherFlight(Control flightCard, ShuffledTableCardVisual visual, int visualIndex, float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float direction = visualIndex % 2 == 0 ? 1.0f : -1.0f;
		Vector2 centerOffset = GetTableShuffleCenterOffset(visualIndex);
		Vector2 gatherGlobalPosition = visual.ShuffleCenterGlobalPosition + centerOffset - CardVisualSize / 2.0f;
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * TableShuffleArcHeight;

		flightCard.GlobalPosition = visual.StartGlobalPosition.Lerp(gatherGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = direction * Mathf.Lerp(0.0f, TableShuffleRotationDegrees, easedProgress);
	}

	private static void UpdateShuffledTableCardScatterFlight(Control flightCard, ShuffledTableCardVisual visual, int visualIndex, float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float direction = visualIndex % 2 == 0 ? 1.0f : -1.0f;
		Vector2 centerOffset = GetTableShuffleCenterOffset(visualIndex);
		Vector2 gatherGlobalPosition = visual.ShuffleCenterGlobalPosition + centerOffset - CardVisualSize / 2.0f;
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * TableShuffleArcHeight;

		flightCard.GlobalPosition = gatherGlobalPosition.Lerp(visual.TargetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = direction * Mathf.Lerp(TableShuffleRotationDegrees, 0.0f, easedProgress);
	}

	private static void UpdateMantaRayTideShiftTableCardFlight(Control flightCard, ShuffledTableCardVisual visual, bool wrapsToEnd, float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		Vector2 position = visual.StartGlobalPosition.Lerp(visual.TargetGlobalPosition, easedProgress);
		if (!wrapsToEnd)
		{
			flightCard.GlobalPosition = position;
			flightCard.RotationDegrees = 0.0f;
			return;
		}

		float pulse = Mathf.Sin(clampedProgress * Mathf.Pi);
		flightCard.GlobalPosition = position + new Vector2(0.0f, -pulse * MantaRayTideShiftMovedCardArcHeight);
		flightCard.RotationDegrees = Mathf.Lerp(
			-MantaRayTideShiftMovedCardRotationDegrees,
			MantaRayTideShiftMovedCardRotationDegrees,
			easedProgress);
		flightCard.Scale = Vector2.One * (1.0f + pulse * 0.08f);
	}

	private static Vector2 GetTableShuffleCenterOffset(int visualIndex)
	{
		float angle = visualIndex * 2.3999631f;
		float radius = TableShuffleCenterSpread * (0.35f + 0.65f * ((visualIndex % 3) / 2.0f));
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	}
}
