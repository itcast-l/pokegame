using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayTableCardRevealAnimationAsync(int tableCardIndex, Texture2D faceTexture)
	{
		if (_cardBackTexture == null || tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		var revealCard = new TextureRect
		{
			Name = "TableCardReveal",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = cardPosition,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = tableCardIndex * 2 + 100,
		};
		_centerArena.AddChild(revealCard);

		try
		{
			Tween revealTween = CreateTween();
			revealTween.SetParallel(true);
			revealTween.SetTrans(Tween.TransitionType.Cubic);
			revealTween.SetEase(Tween.EaseType.Out);
			revealTween.TweenProperty(revealCard, "scale", new Vector2(TableCardRevealMinScaleX, 1.0f), TableCardRevealHalfFlipSeconds);
			revealTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(revealCard))
				{
					revealCard.Texture = faceTexture;
				}
			})).SetDelay(TableCardRevealHalfFlipSeconds);
			revealTween.TweenProperty(revealCard, "scale", Vector2.One, TableCardRevealHalfFlipSeconds)
				.SetDelay(TableCardRevealHalfFlipSeconds);
			revealTween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(revealCard))
				{
					revealCard.QueueFree();
				}
			};
			await ToSignal(revealTween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(revealCard))
			{
				revealCard.QueueFree();
			}
		}
	}

	private async Task PlayTableCardPointPopupAsync(int tableCardIndex, int points, bool isDoubleScore = false, bool isDamage = false)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		var pointLabel = new Label
		{
			Name = "TableCardPointPopup",
			Text = points == 0 ? "0" : $"{(isDamage ? "-" : "+")}{points}",
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = new Vector2(CardVisualSize.X, isDoubleScore ? 34.0f : 28.0f),
			ZIndex = TableCardPointPopupZIndex,
		};
		pointLabel.AddThemeFontSizeOverride("font_size", isDoubleScore ? TableCardPointPopupDoubleFontSize : TableCardPointPopupFontSize);
		pointLabel.AddThemeColorOverride("font_color", isDamage ? new Color(1.0f, 0.42f, 0.32f) : new Color(1.0f, 0.94f, 0.56f));
		pointLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.82f));
		pointLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		pointLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		AddChild(pointLabel);

		pointLabel.GlobalPosition = _centerArena.GlobalPosition
			+ cardPosition
			+ new Vector2(0.0f, -TableCardPointPopupTopOffset);
		Vector2 endPosition = pointLabel.Position + new Vector2(0.0f, -TableCardPointPopupRise);

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pointLabel, "position", endPosition, TableCardPointPopupSeconds);
			tween.TweenProperty(pointLabel, "modulate:a", 0.0f, TableCardPointPopupSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(pointLabel))
			{
				pointLabel.QueueFree();
			}
		}
	}

	private sealed class PairCaptureEdgeFloatHold
	{
		public PairCaptureEdgeFloatHold(int firstTableCardIndex, TextureRect firstOverlay)
			: this(firstTableCardIndex, firstOverlay, -1, null)
		{
		}

		public PairCaptureEdgeFloatHold(
			int firstTableCardIndex,
			TextureRect firstOverlay,
			int secondTableCardIndex,
			TextureRect? secondOverlay)
		{
			_firstTableCardIndex = firstTableCardIndex;
			_firstOverlay = firstOverlay;
			_secondTableCardIndex = secondTableCardIndex;
			_secondOverlay = secondOverlay;
		}

		private readonly int _firstTableCardIndex;
		private readonly TextureRect _firstOverlay;
		private readonly int _secondTableCardIndex;
		private readonly TextureRect? _secondOverlay;

		public bool TryGetFlightStartPositionOverride(int tableCardIndex, out Vector2 globalPosition)
		{
			if (tableCardIndex == _firstTableCardIndex && GodotObject.IsInstanceValid(_firstOverlay))
			{
				globalPosition = _firstOverlay.GlobalPosition;
				return true;
			}

			if (tableCardIndex == _secondTableCardIndex
				&& _secondOverlay != null
				&& GodotObject.IsInstanceValid(_secondOverlay))
			{
				globalPosition = _secondOverlay.GlobalPosition;
				return true;
			}

			globalPosition = default;
			return false;
		}

		public void RestoreHiddenTableCards(IReadOnlyList<TableCard> tableCards)
		{
			RestoreHiddenTableCard(tableCards, _firstTableCardIndex);
			RestoreHiddenTableCard(tableCards, _secondTableCardIndex);
		}

		public void QueueFreeOverlays()
		{
			if (GodotObject.IsInstanceValid(_firstOverlay))
			{
				_firstOverlay.QueueFree();
			}

			if (_secondOverlay != null && GodotObject.IsInstanceValid(_secondOverlay))
			{
				_secondOverlay.QueueFree();
			}
		}

		private static void RestoreHiddenTableCard(IReadOnlyList<TableCard> tableCards, int tableCardIndex)
		{
			if (tableCardIndex >= 0 && tableCardIndex < tableCards.Count)
			{
				tableCards[tableCardIndex].IsHiddenDuringCapture = false;
			}
		}
	}

	private async Task<PairCaptureEdgeFloatHold?> PlayPairCaptureEdgeFloatHoldAnimationAsync(int captureStartIndex, int playedTableCardIndex)
	{
		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		int firstTableCardIndex = -1;
		int secondTableCardIndex = -1;
		TextureRect? firstOverlay = null;
		TextureRect? secondOverlay = null;

		void TryAddEdgeFloatOverlay(int tableCardIndex)
		{
			if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
			{
				return;
			}

			Texture2D? texture = GetVisibleTableCardTexture(_tableCards[tableCardIndex]);
			if (texture == null)
			{
				return;
			}

			var overlay = new TextureRect
			{
				Name = "PairCaptureEdgeFloat",
				Texture = texture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2 + 220,
				ZAsRelative = false,
			};
			AddChild(overlay);
			overlay.GlobalPosition = _centerArena.GlobalPosition + GetTableCardPosition(tableCardIndex, tableLayoutRect);
			_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
			if (firstOverlay == null)
			{
				firstTableCardIndex = tableCardIndex;
				firstOverlay = overlay;
			}
			else
			{
				secondTableCardIndex = tableCardIndex;
				secondOverlay = overlay;
			}
		}

		TryAddEdgeFloatOverlay(captureStartIndex);
		if (playedTableCardIndex != captureStartIndex)
		{
			TryAddEdgeFloatOverlay(playedTableCardIndex);
		}

		if (firstOverlay == null)
		{
			return null;
		}

		RenderTableCards();
		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);

		void TweenOverlay(TextureRect overlay)
		{
			Vector2 startGlobalPosition = overlay.GlobalPosition;
			tween.TweenProperty(overlay, "global_position", startGlobalPosition + new Vector2(0.0f, -PairCaptureEdgeFloatRise), PairCaptureEdgeFloatSeconds);
			tween.TweenProperty(overlay, "scale", Vector2.One * PairCaptureEdgeFloatScale, PairCaptureEdgeFloatSeconds);
		}

		TweenOverlay(firstOverlay);
		if (secondOverlay != null)
		{
			TweenOverlay(secondOverlay);
		}

		await ToSignal(tween, Tween.SignalName.Finished);
		return new PairCaptureEdgeFloatHold(firstTableCardIndex, firstOverlay, secondTableCardIndex, secondOverlay);
	}

	private void RestorePairCaptureEdgeFloatHold(PairCaptureEdgeFloatHold hold)
	{
		hold.RestoreHiddenTableCards(_tableCards);
		hold.QueueFreeOverlays();
		RenderTableCards();
	}

}
