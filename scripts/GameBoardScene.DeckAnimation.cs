using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const string QuestionCardRefreshFramePattern = "res://art/fx/question_card_refresh/run-001/processed/fx-{0}.png";
	private const int QuestionCardRefreshFrameCount = 6;
	private const float QuestionCardRefreshSeconds = 0.48f;
	private const float OpeningIntroFlyInSeconds = 0.42f;
	private const float OpeningIntroHoldSeconds = 0.20f;
	private const float OpeningIntroFadeOutSeconds = 0.14f;
	private const float OpeningDealCardFlightSeconds = 0.18f;
	private const float OpeningDealCardStaggerSeconds = 0.035f;
	private const float OpeningDealPilesTotalSeconds = 2.0f;
	private const float OpeningDealPileCardFlightSeconds = 0.18f;
	private const float ReplacementDrawFlightSeconds = 0.30f;
	private const float ReplacementDrawSettleSeconds = 0.12f;
	private const float ReplacementDrawArcHeight = 38.0f;
	private const float ReplacementDrawStartScale = 0.94f;
	private const float ReplacementDrawPeakScale = 1.08f;
	private const string DeckShuffleFramePattern = "res://art/cards/deck_shuffle/overhand_drop/game_frames/frame-{0:000}.png";
	private const int DeckShuffleFrameCount = 6;
	private const int DeckShuffleRepeatCount = 3;
	private const float DeckShuffleFrameSeconds = 0.065f;
	private const float PlayedCardFlightDuration = 0.34f;
	private const float PlayedCardFlipMinScaleX = 0.06f;
	private const float QuestionCardToTransformAreaFlightSeconds = 0.36f;
	private const float QuestionCardTransformSeconds = 0.58f;
	private const float QuestionCardRewardInsertSeconds = 0.22f;
	private const float SpecialCardTriggerEffectSeconds = 0.72f;
	private const float SpecialCardTriggerFxFrameSeconds = 0.14f;
	private const float LensCardFlightSeconds = 0.34f;
	private const float LensCardSplitSeconds = 0.18f;
	private const float LensRevealWispFlightSeconds = 0.42f;
	private const float LensRevealWispFrameSeconds = 0.07f;
	private const float LensRevealWispStaggerSeconds = 0.09f;
	private const float LensRevealWispArcHeight = 46.0f;
	private const float TableShuffleGatherSeconds = 0.28f;
	private const float TableShuffleScatterSeconds = 0.38f;
	private const float TableShuffleCenterSpread = 22.0f;
	private const float TableShuffleArcHeight = 54.0f;
	private const float TableShuffleRotationDegrees = 42.0f;
	private const float FullCircleRadians = 6.2831855f;
	private const int PlayedCardFlightZIndex = 2000;
	private const int OpeningDealZIndex = 2400;
	private static readonly Vector2 DeckShuffleVisualSize = new(96.0f, 192.0f);
	private static readonly Vector2 QuestionCardRefreshFxVisualSize = new(160.0f, 160.0f);
	private static readonly Vector2 QuestionCardTransformFxVisualSize = new(128.0f, 128.0f);
	private static readonly Vector2 SpecialCardTriggerSparkSize = new(20.0f, 20.0f);
	private static readonly Vector2 SpecialCardTriggerRingSize = new(188.0f, 188.0f);
	private static readonly Vector2 SpecialCardTriggerFxVisualSize = new(256.0f, 256.0f);
	private static readonly Vector2 LensRevealWispVisualSize = new(72.0f, 72.0f);

	private partial class SpecialCardTriggerRing : Control
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
			float radius = Mathf.Lerp(42.0f, 78.0f, _ringProgress);
			float outerAlpha = 0.38f * _ringAlpha;
			float innerAlpha = 0.95f * _ringAlpha;
			DrawArc(center, radius + 8.0f, 0.0f, FullCircleRadians, 96, new Color(0.48f, 1.0f, 0.92f, outerAlpha), 14.0f, true);
			DrawArc(center, radius, 0.0f, FullCircleRadians, 96, new Color(1.0f, 0.96f, 0.42f, innerAlpha), 6.0f, true);
			DrawArc(center, radius - 7.0f, 0.0f, FullCircleRadians, 96, new Color(1.0f, 1.0f, 1.0f, 0.55f * _ringAlpha), 2.5f, true);
		}
	}

	private partial class TableShuffleVortex : Control
	{
		private float _vortexAlpha;
		private float _vortexProgress;

		public float vortex_alpha
		{
			get => _vortexAlpha;
			set
			{
				_vortexAlpha = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public float vortex_progress
		{
			get => _vortexProgress;
			set
			{
				_vortexProgress = Mathf.Clamp(value, 0.0f, 1.0f);
				QueueRedraw();
			}
		}

		public override void _Draw()
		{
			if (_vortexAlpha <= 0.0f)
			{
				return;
			}

			Vector2 center = Size / 2.0f;
			float rotation = _vortexProgress * FullCircleRadians * 1.35f;
			for (int ringIndex = 0; ringIndex < 3; ringIndex++)
			{
				float radius = 34.0f + ringIndex * 18.0f;
				float startAngle = rotation + ringIndex * 0.74f;
				float endAngle = startAngle + Mathf.Pi * 1.28f;
				float alpha = _vortexAlpha * (0.72f - ringIndex * 0.16f);
				DrawArc(center, radius, startAngle, endAngle, 80, new Color(0.58f, 1.0f, 0.94f, alpha), 5.0f - ringIndex, true);
			}

			for (int rayIndex = 0; rayIndex < 8; rayIndex++)
			{
				float angle = rotation + rayIndex * Mathf.Pi / 4.0f;
				Vector2 inner = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 18.0f;
				Vector2 outer = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 74.0f;
				DrawLine(inner, outer, new Color(1.0f, 0.94f, 0.46f, _vortexAlpha * 0.34f), 2.0f, true);
			}

			DrawCircle(center, 18.0f, new Color(0.38f, 1.0f, 0.95f, _vortexAlpha * 0.18f));
			DrawArc(center, 82.0f, rotation, rotation + FullCircleRadians, 96, new Color(1.0f, 1.0f, 1.0f, _vortexAlpha * 0.22f), 2.0f, true);
		}
	}

}
