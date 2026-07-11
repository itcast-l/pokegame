using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlaySpecialCardTriggerEffectAsync(Texture2D cardTexture, Vector2 globalPosition, int zIndex, float rotationDegrees = 0.0f)
	{
		EnsureCommonGameplayFxResourcesLoaded();

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
		IReadOnlyList<Texture2D> activationFrames = _specialCardTriggerActivationFrames;
		Control effectRoot = _animationPresenter.RentControl(
			this,
			"SpecialCardTriggerEffect",
			CardVisualSize,
			PlayedCardFlightZIndex + zIndex + 760);
		effectRoot.RotationDegrees = rotationDegrees;
		effectRoot.GlobalPosition = globalPosition;

		TextureRect activationOverlay = _animationPresenter.RentTextureRect(
			effectRoot,
			"SpecialCardTriggerActivation",
			activationFrames.Count > 0 ? activationFrames[0] : null,
			new Vector2(320.0f, 320.0f),
			0,
			CanvasItem.TextureFilterEnum.Linear);
		activationOverlay.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		activationOverlay.Position = (CardVisualSize - activationOverlay.Size) / 2.0f;
		activationOverlay.Visible = activationFrames.Count > 0;
		activationOverlay.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

		TextureRect card = _animationPresenter.RentTextureRect(
			effectRoot,
			"SpecialCardTriggerCard",
			cardTexture,
			CardVisualSize,
			2,
			CanvasItem.TextureFilterEnum.Nearest);

		TextureRect triggerFx = _animationPresenter.RentTextureRect(
			card,
			"SpecialCardTriggerFx",
			triggerFxFrames.Count > 0 ? triggerFxFrames[0] : null,
			SpecialCardTriggerFxVisualSize,
			20,
			CanvasItem.TextureFilterEnum.Linear);
		triggerFx.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
		triggerFx.Position = (CardVisualSize - SpecialCardTriggerFxVisualSize) / 2.0f;
		triggerFx.Visible = triggerFxFrames.Count > 0;
		triggerFx.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);

		try
		{
			Tween tween = CreateTween();
			if (_debugSpecialCardProbeRunning)
			{
				_debugSpecialCardProbeTween = tween;
				_debugSpecialCardProbeEffectRoot = effectRoot;
			}
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			float firstPhaseSeconds = SpecialCardTriggerEffectSeconds * 0.42f;
			float secondPhaseSeconds = SpecialCardTriggerEffectSeconds * 0.58f;
			tween.TweenProperty(effectRoot, "scale", Vector2.One * 1.82f, firstPhaseSeconds);
			tween.TweenProperty(card, "scale", Vector2.One * 1.10f, firstPhaseSeconds);
			tween.TweenProperty(activationOverlay, "modulate:a", 1.0f, SpecialCardTriggerEffectSeconds * 0.18f);
			tween.TweenProperty(activationOverlay, "scale", Vector2.One * 1.18f, firstPhaseSeconds);
			tween.TweenProperty(triggerFx, "modulate:a", triggerFxPeakAlpha, SpecialCardTriggerEffectSeconds * 0.12f);
			tween.TweenProperty(triggerFx, "scale", Vector2.One * 1.18f, firstPhaseSeconds);

			tween.TweenProperty(effectRoot, "scale", Vector2.One * 1.16f, secondPhaseSeconds)
				.SetDelay(firstPhaseSeconds);
			tween.TweenProperty(card, "scale", Vector2.One, secondPhaseSeconds)
				.SetDelay(firstPhaseSeconds);
			tween.TweenProperty(activationOverlay, "modulate:a", 0.0f, secondPhaseSeconds)
				.SetDelay(firstPhaseSeconds);
			tween.TweenProperty(activationOverlay, "scale", Vector2.One * 1.32f, secondPhaseSeconds)
				.SetDelay(firstPhaseSeconds);
			tween.TweenProperty(triggerFx, "modulate:a", 0.0f, secondPhaseSeconds)
				.SetDelay(firstPhaseSeconds);
			tween.TweenProperty(triggerFx, "scale", Vector2.One * 1.34f, secondPhaseSeconds)
				.SetDelay(firstPhaseSeconds);
			FrameAnimationHelpers.TweenTextureFrames(tween, activationOverlay, activationFrames, SpecialCardTriggerEffectSeconds / Mathf.Max(1, activationFrames.Count));
			FrameAnimationHelpers.TweenTextureFrames(tween, triggerFx, triggerFxFrames, triggerFxFrameSeconds);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(effectRoot))
				{
					_animationPresenter.Release(effectRoot);
				}
			};
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(effectRoot))
			{
				_animationPresenter.Release(effectRoot);
			}
		}
	}
}
