using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlaySpecialCardTriggerEffectAsync(Texture2D cardTexture, Vector2 globalPosition, int zIndex, float rotationDegrees = 0.0f)
	{
		if (!IsInsideTree())
		{
			return;
		}

		PlayGameSfx(GameSfx.SpecialTrigger, -1.0f, 0.97f, 1.03f, 0.08);
		bool useEnemyAbilityFlashFx = _enemySpecialAbilityAvatarFxFrames.Count > 0;
		IReadOnlyList<Texture2D> triggerFxFrames = useEnemyAbilityFlashFx
			? _enemySpecialAbilityAvatarFxFrames
			: _specialCardTriggerEffectFrames;
		float triggerFxFrameSeconds = useEnemyAbilityFlashFx
			? EnemySpecialAbilityAvatarFxFrameSeconds
			: SpecialCardTriggerFxFrameSeconds;
		float triggerFxPeakAlpha = useEnemyAbilityFlashFx ? 0.88f : 1.0f;
		var effectRoot = new Control
		{
			Name = "SpecialCardTriggerEffect",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + zIndex + 760,
			ZAsRelative = false,
			RotationDegrees = rotationDegrees,
			Scale = Vector2.One,
		};
		AddChild(effectRoot);
		effectRoot.GlobalPosition = globalPosition;

		var ring = new SpecialCardTriggerRing
		{
			Name = "SpecialCardTriggerRing",
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - SpecialCardTriggerRingSize) / 2.0f,
			Size = SpecialCardTriggerRingSize,
			PivotOffset = SpecialCardTriggerRingSize / 2.0f,
			ZIndex = 0,
		};
		effectRoot.AddChild(ring);

		var card = new TextureRect
		{
			Name = "SpecialCardTriggerCard",
			Texture = cardTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = 2,
		};
		effectRoot.AddChild(card);

		var triggerFx = new TextureRect
		{
			Name = "SpecialCardTriggerFx",
			Texture = triggerFxFrames.Count > 0 ? triggerFxFrames[0] : null,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - SpecialCardTriggerFxVisualSize) / 2.0f,
			Size = SpecialCardTriggerFxVisualSize,
			PivotOffset = SpecialCardTriggerFxVisualSize / 2.0f,
			ZIndex = 20,
			Visible = triggerFxFrames.Count > 0,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		card.AddChild(triggerFx);

		Vector2[] sparkPositions =
		{
			new(-36.0f, -34.0f),
			new(CardVisualSize.X + 17.0f, -28.0f),
			new(-30.0f, CardVisualSize.Y + 16.0f),
			new(CardVisualSize.X + 20.0f, CardVisualSize.Y + 15.0f),
		};
		foreach (Vector2 sparkPosition in sparkPositions)
		{
			var spark = new ColorRect
			{
				Name = "SpecialCardTriggerSpark",
				Color = new Color(1.0f, 0.96f, 0.56f, 0.95f),
				MouseFilter = MouseFilterEnum.Ignore,
				Position = sparkPosition,
				Size = SpecialCardTriggerSparkSize,
				PivotOffset = SpecialCardTriggerSparkSize / 2.0f,
				RotationDegrees = 45.0f,
				ZIndex = 4,
				Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			};
			effectRoot.AddChild(spark);
		}

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(effectRoot, "scale", Vector2.One * 1.82f, SpecialCardTriggerEffectSeconds * 0.42f);
			tween.TweenProperty(card, "scale", Vector2.One * 1.10f, SpecialCardTriggerEffectSeconds * 0.42f);
			tween.TweenProperty(ring, "ring_progress", 0.58f, SpecialCardTriggerEffectSeconds * 0.42f);
			tween.TweenProperty(ring, "ring_alpha", 1.0f, SpecialCardTriggerEffectSeconds * 0.18f);
			tween.TweenProperty(triggerFx, "modulate:a", triggerFxPeakAlpha, SpecialCardTriggerEffectSeconds * 0.12f);
			tween.TweenProperty(triggerFx, "scale", Vector2.One * 1.18f, SpecialCardTriggerEffectSeconds * 0.42f);
			for (int sparkIndex = 0; sparkIndex < effectRoot.GetChildCount(); sparkIndex++)
			{
				if (effectRoot.GetChild(sparkIndex) is ColorRect spark)
				{
					tween.TweenProperty(spark, "modulate:a", 1.0f, SpecialCardTriggerEffectSeconds * 0.24f);
					tween.TweenProperty(spark, "scale", Vector2.One * 2.35f, SpecialCardTriggerEffectSeconds * 0.42f);
				}
			}

			tween.Chain().SetParallel(true);
			tween.TweenProperty(effectRoot, "scale", Vector2.One * 1.16f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(card, "scale", Vector2.One, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(ring, "ring_progress", 1.0f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(ring, "ring_alpha", 0.0f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(triggerFx, "modulate:a", 0.0f, SpecialCardTriggerEffectSeconds * 0.58f);
			tween.TweenProperty(triggerFx, "scale", Vector2.One * 1.34f, SpecialCardTriggerEffectSeconds * 0.58f);
			for (int sparkIndex = 0; sparkIndex < effectRoot.GetChildCount(); sparkIndex++)
			{
				if (effectRoot.GetChild(sparkIndex) is ColorRect spark)
				{
					tween.TweenProperty(spark, "modulate:a", 0.0f, SpecialCardTriggerEffectSeconds * 0.58f);
					tween.TweenProperty(spark, "scale", Vector2.One * 0.68f, SpecialCardTriggerEffectSeconds * 0.58f);
				}
			}

			FrameAnimationHelpers.TweenTextureFrames(tween, triggerFx, triggerFxFrames, triggerFxFrameSeconds);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(effectRoot))
				{
					effectRoot.QueueFree();
				}
			};
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effectRoot))
			{
				effectRoot.QueueFree();
			}
		}
	}
}
