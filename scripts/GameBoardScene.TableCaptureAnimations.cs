using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayTableCardRevealAnimationAsync(int tableCardIndex, Texture2D faceTexture)
	{
		if (_networkAuthorityRuleTransactionActive
			|| _cardBackTexture == null
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		TextureRect revealCard = _animationPresenter.RentTextureRect(
			_centerArena,
			"TableCardReveal",
			_cardBackTexture,
			CardVisualSize,
			tableCardIndex * 2 + 100,
			CanvasItem.TextureFilterEnum.Nearest);
		revealCard.Position = cardPosition;

		try
		{
			Tween revealTween = CreateBoardTween();
			revealTween.SetParallel(true);
			revealTween.SetTrans(Tween.TransitionType.Cubic);
			revealTween.SetEase(Tween.EaseType.Out);
			revealTween.TweenProperty(revealCard, "scale", new Vector2(TableCardRevealMinScaleX, 1.0f), TableCardRevealHalfFlipSeconds);
			revealTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(revealCard))
				{
					PlayGameSfx(GameSfx.CardFlip, -1.5f, 0.96f, 1.05f, 0.04);
					revealCard.Texture = faceTexture;
				}
			})).SetDelay(TableCardRevealHalfFlipSeconds);
			revealTween.TweenProperty(revealCard, "scale", Vector2.One, TableCardRevealHalfFlipSeconds)
				.SetDelay(TableCardRevealHalfFlipSeconds);
			revealTween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(revealCard))
				{
					_animationPresenter.Release(revealCard);
				}
			};
			await WaitForBoardTweenAsync(revealTween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(revealCard))
			{
				_animationPresenter.Release(revealCard);
			}
		}
	}

	private async Task PlayTableCardPointPopupAsync(int tableCardIndex, int points, bool isDoubleScore = false, bool isDamage = false)
	{
		if (_networkAuthorityRuleTransactionActive
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return;
		}

		Label pointLabel = _animationPresenter.RentLabel(
			this,
			"TableCardPointPopup",
			new Vector2(CardVisualSize.X, isDoubleScore ? 34.0f : 28.0f),
			TableCardPointPopupZIndex);
		pointLabel.Text = points == 0 ? "0" : $"{(isDamage ? "-" : "+")}{points}";
		pointLabel.AddThemeFontSizeOverride("font_size", isDoubleScore ? TableCardPointPopupDoubleFontSize : TableCardPointPopupFontSize);
		pointLabel.AddThemeColorOverride("font_color", isDamage ? new Color(1.0f, 0.42f, 0.32f) : new Color(1.0f, 0.94f, 0.56f));
		pointLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.82f));
		pointLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		pointLabel.AddThemeConstantOverride("shadow_offset_y", 2);
		PlayGameSfx(GameSfx.CardCollectPointPopup, -5.5f, 0.98f, 1.02f, 0.0);

		pointLabel.GlobalPosition = _centerArena.GlobalPosition
			+ cardPosition
			+ new Vector2(0.0f, -TableCardPointPopupTopOffset);
		Vector2 endPosition = pointLabel.Position + new Vector2(0.0f, -TableCardPointPopupRise);

		try
		{
			Tween tween = CreateBoardTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(pointLabel, "position", endPosition, TableCardPointPopupSeconds);
			tween.TweenProperty(pointLabel, "modulate:a", 0.0f, TableCardPointPopupSeconds);
			await WaitForBoardTweenAsync(tween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(pointLabel))
			{
				_animationPresenter.Release(pointLabel);
			}
		}
	}

	private sealed class PairCaptureEdgeFloatHold
	{
		public PairCaptureEdgeFloatHold(
			int firstTableCardIndex,
			long firstTableCardRuntimeInstanceId,
			TextureRect firstOverlay)
			: this(
				firstTableCardIndex,
				firstTableCardRuntimeInstanceId,
				firstOverlay,
				-1,
				0,
				null)
		{
		}

		public PairCaptureEdgeFloatHold(
			int firstTableCardIndex,
			long firstTableCardRuntimeInstanceId,
			TextureRect firstOverlay,
			int secondTableCardIndex,
			long secondTableCardRuntimeInstanceId,
			TextureRect? secondOverlay)
		{
			_firstTableCardIndex = firstTableCardIndex;
			_firstTableCardRuntimeInstanceId = firstTableCardRuntimeInstanceId;
			_firstOverlay = firstOverlay;
			_secondTableCardIndex = secondTableCardIndex;
			_secondTableCardRuntimeInstanceId = secondTableCardRuntimeInstanceId;
			_secondOverlay = secondOverlay;
		}

		private readonly int _firstTableCardIndex;
		private readonly long _firstTableCardRuntimeInstanceId;
		private readonly TextureRect _firstOverlay;
		private readonly int _secondTableCardIndex;
		private readonly long _secondTableCardRuntimeInstanceId;
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

		public bool TryGetOverlay(int tableCardIndex, out TextureRect? overlay)
		{
			if (tableCardIndex == _firstTableCardIndex && GodotObject.IsInstanceValid(_firstOverlay))
			{
				overlay = _firstOverlay;
				return true;
			}

			if (tableCardIndex == _secondTableCardIndex
				&& _secondOverlay != null
				&& GodotObject.IsInstanceValid(_secondOverlay))
			{
				overlay = _secondOverlay;
				return true;
			}

			overlay = null;
			return false;
		}

		public void RestorePresentationHiddenTableCards(HashSet<long> hiddenRuntimeInstanceIds)
		{
			hiddenRuntimeInstanceIds.Remove(_firstTableCardRuntimeInstanceId);
			if (_secondTableCardRuntimeInstanceId > 0)
			{
				hiddenRuntimeInstanceIds.Remove(_secondTableCardRuntimeInstanceId);
			}
		}

		public void QueueFreeOverlays(IGameBoardAnimationPresenter animationPresenter)
		{
			if (GodotObject.IsInstanceValid(_firstOverlay))
			{
				animationPresenter.Release(_firstOverlay);
			}

			if (_secondOverlay != null && GodotObject.IsInstanceValid(_secondOverlay))
			{
				animationPresenter.Release(_secondOverlay);
			}
		}

	}

	private async Task<PairCaptureEdgeFloatHold?> PlayPairCaptureEdgeFloatHoldAnimationAsync(int captureStartIndex, int playedTableCardIndex)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return null;
		}

		Rect2 tableLayoutRect = GetTableCardLayoutRect();
		int firstTableCardIndex = -1;
		int secondTableCardIndex = -1;
		long firstTableCardRuntimeInstanceId = 0;
		long secondTableCardRuntimeInstanceId = 0;
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

			TextureRect overlay = _animationPresenter.RentTextureRect(
				this,
				"PairCaptureEdgeFloat",
				texture,
				CardVisualSize,
				PlayedCardFlightZIndex + tableCardIndex * 2 + 220,
				CanvasItem.TextureFilterEnum.Nearest,
				zAsRelative: false);
			overlay.GlobalPosition = _centerArena.GlobalPosition + GetTableCardPosition(tableCardIndex, tableLayoutRect);
			TableCard tableCard = _tableCards[tableCardIndex];
			_presentationHiddenTableCardRuntimeInstanceIds.Add(tableCard.InstanceId);
			if (firstOverlay == null)
			{
				firstTableCardIndex = tableCardIndex;
				firstTableCardRuntimeInstanceId = tableCard.InstanceId;
				firstOverlay = overlay;
			}
			else
			{
				secondTableCardIndex = tableCardIndex;
				secondTableCardRuntimeInstanceId = tableCard.InstanceId;
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

		var hold = new PairCaptureEdgeFloatHold(
			firstTableCardIndex,
			firstTableCardRuntimeInstanceId,
			firstOverlay,
			secondTableCardIndex,
			secondTableCardRuntimeInstanceId,
			secondOverlay);
		bool holdReturned = false;
		try
		{
			PlayGameSfx(GameSfx.CaptureLift, -2.0f, 0.98f, 1.02f);
			RenderTableCards();
			Tween tween = CreateBoardTween();
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

			await WaitForBoardTweenAsync(tween);
			holdReturned = true;
			return hold;
		}
		finally
		{
			if (!holdReturned)
			{
				RestorePairCaptureEdgeFloatHold(hold);
			}
		}
	}

	private async Task<bool> PlayPairCaptureEdgeFloatRevealAnimationAsync(
		PairCaptureEdgeFloatHold hold,
		int tableCardIndex,
		Texture2D faceTexture)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return false;
		}

		if (!hold.TryGetOverlay(tableCardIndex, out TextureRect? overlay) || overlay == null)
		{
			return false;
		}

		Vector2 settledScale = overlay.Scale;
		Vector2 flippedScale = new(TableCardRevealMinScaleX * settledScale.X, settledScale.Y);
		Tween revealTween = CreateBoardTween();
		revealTween.SetParallel(true);
		revealTween.SetTrans(Tween.TransitionType.Cubic);
		revealTween.SetEase(Tween.EaseType.Out);
		revealTween.TweenProperty(overlay, "scale", flippedScale, TableCardRevealHalfFlipSeconds);
		revealTween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(overlay))
			{
				PlayGameSfx(GameSfx.CardFlip, -1.5f, 0.96f, 1.05f, 0.04);
				overlay.Texture = faceTexture;
			}
		})).SetDelay(TableCardRevealHalfFlipSeconds);
		revealTween.TweenProperty(overlay, "scale", settledScale, TableCardRevealHalfFlipSeconds)
			.SetDelay(TableCardRevealHalfFlipSeconds);
		await WaitForBoardTweenAsync(revealTween);
		return true;
	}

	private void RestorePairCaptureEdgeFloatHold(PairCaptureEdgeFloatHold hold)
	{
		hold.RestorePresentationHiddenTableCards(
			_presentationHiddenTableCardRuntimeInstanceIds);
		hold.QueueFreeOverlays(_animationPresenter);
		RenderTableCards();
	}

}
