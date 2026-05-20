using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayTableCardsShuffleAnimationAsync(IReadOnlyList<TableCard> shuffledTableCards, int turnSequenceGeneration)
	{
		if (!IsInsideTree() || shuffledTableCards.Count <= 1 || shuffledTableCards.Count != _tableCards.Count)
		{
			return;
		}

		List<ShuffledTableCardVisual> shuffledVisuals = BuildShuffledTableCardVisuals(shuffledTableCards);
		if (shuffledVisuals.Count == 0)
		{
			return;
		}

		foreach (TableCard tableCard in _tableCards)
		{
			if (!IsFixedDuringTableShuffle(tableCard))
			{
				tableCard.IsHiddenDuringCapture = true;
			}
		}
		RenderTableCards();

		var flightCards = new List<Control>(shuffledVisuals.Count);
		TableShuffleVortex? vortex = null;
		try
		{
			vortex = CreateTableShuffleVortex(GetQuestionCardTransformAreaCardGlobalPosition(Vector2.Zero) + CardVisualSize / 2.0f);
			Tween gatherTween = CreateTween();
			gatherTween.SetParallel(true);
			gatherTween.SetTrans(Tween.TransitionType.Sine);
			gatherTween.SetEase(Tween.EaseType.InOut);
			for (int visualIndex = 0; visualIndex < shuffledVisuals.Count; visualIndex++)
			{
				ShuffledTableCardVisual visual = shuffledVisuals[visualIndex];
				Control flightCard = CreateShuffledTableCardFlight(visual, visualIndex);
				flightCards.Add(flightCard);
				gatherTween.TweenMethod(
					Callable.From<float>(progress => UpdateShuffledTableCardGatherFlight(flightCard, visual, visualIndex, progress)),
					0.0f,
					1.0f,
					TableShuffleGatherSeconds);
			}
			gatherTween.TweenProperty(vortex, "vortex_alpha", 1.0f, TableShuffleGatherSeconds * 0.55f);

			await ToSignal(gatherTween, Tween.SignalName.Finished);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return;
			}

			Tween scatterTween = CreateTween();
			scatterTween.SetParallel(true);
			scatterTween.SetTrans(Tween.TransitionType.Sine);
			scatterTween.SetEase(Tween.EaseType.InOut);
			for (int visualIndex = 0; visualIndex < shuffledVisuals.Count; visualIndex++)
			{
				ShuffledTableCardVisual visual = shuffledVisuals[visualIndex];
				Control flightCard = flightCards[visualIndex];
				scatterTween.TweenMethod(
					Callable.From<float>(progress => UpdateShuffledTableCardScatterFlight(flightCard, visual, visualIndex, progress)),
					0.0f,
					1.0f,
					TableShuffleScatterSeconds);
			}
			scatterTween.TweenProperty(vortex, "vortex_alpha", 0.0f, TableShuffleScatterSeconds);
			scatterTween.TweenProperty(vortex, "vortex_progress", 1.0f, TableShuffleScatterSeconds);

			await ToSignal(scatterTween, Tween.SignalName.Finished);
		}
		finally
		{
			foreach (TableCard tableCard in _tableCards)
			{
				tableCard.IsHiddenDuringCapture = false;
			}

			foreach (Control flightCard in flightCards)
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					flightCard.QueueFree();
				}
			}

			if (GodotObject.IsInstanceValid(vortex))
			{
				vortex.QueueFree();
			}

			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				RenderTableCards();
			}
		}
	}

	private List<ShuffledTableCardVisual> BuildShuffledTableCardVisuals(IReadOnlyList<TableCard> shuffledTableCards)
	{
		var visuals = new List<ShuffledTableCardVisual>();
		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		if (cardPositions.Count != _tableCards.Count)
		{
			return visuals;
		}

		var targetIndexesByCard = new Dictionary<TableCard, int>();
		for (int targetIndex = 0; targetIndex < shuffledTableCards.Count; targetIndex++)
		{
			targetIndexesByCard[shuffledTableCards[targetIndex]] = targetIndex;
		}
		Vector2 shuffleCenterGlobalPosition = GetQuestionCardTransformAreaCardGlobalPosition(Vector2.Zero) + CardVisualSize / 2.0f;

		for (int sourceIndex = 0; sourceIndex < _tableCards.Count; sourceIndex++)
		{
			TableCard tableCard = _tableCards[sourceIndex];
			if (IsFixedDuringTableShuffle(tableCard))
			{
				continue;
			}

			if (!targetIndexesByCard.TryGetValue(tableCard, out int targetIndex))
			{
				continue;
			}

			ShuffledTableCardVisual? visual = BuildShuffledTableCardVisual(
				tableCard,
				_centerArena.GlobalPosition + cardPositions[sourceIndex],
				_centerArena.GlobalPosition + cardPositions[targetIndex],
				shuffleCenterGlobalPosition,
				sourceIndex * 2);
			if (visual.HasValue)
			{
				visuals.Add(visual.Value);
			}
		}

		return visuals;
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
		if (tableCard.Card.HasValue && (tableCard.IsFaceUp || showMainPlayerPreview))
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

	private TableShuffleVortex CreateTableShuffleVortex(Vector2 globalCenter)
	{
		var vortex = new TableShuffleVortex
		{
			Name = "TableShuffleVortex",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(180.0f, 180.0f),
			PivotOffset = new Vector2(90.0f, 90.0f),
			ZIndex = PlayedCardFlightZIndex + 500,
			ZAsRelative = false,
			vortex_alpha = 0.0f,
			vortex_progress = 0.0f,
		};
		AddChild(vortex);
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
		flightCard.Scale = Vector2.One * Mathf.Lerp(1.0f, 0.86f, easedProgress);
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
		flightCard.Scale = Vector2.One * Mathf.Lerp(0.86f, 1.0f, easedProgress);
	}

	private static Vector2 GetTableShuffleCenterOffset(int visualIndex)
	{
		float angle = visualIndex * 2.3999631f;
		float radius = TableShuffleCenterSpread * (0.35f + 0.65f * ((visualIndex % 3) / 2.0f));
		return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
	}
}
