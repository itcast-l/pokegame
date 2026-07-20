using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayItemCardToTransformAreaAnimationAsync(
		Texture2D itemTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees)
	{
		if (!IsInsideTree())
		{
			return;
		}

		TextureRect flightCard = _animationPresenter.RentTextureRect(
			this,
			"ItemCardTransformAreaFlight",
			itemTexture,
			CardVisualSize,
			PlayedCardFlightZIndex + 820,
			CanvasItem.TextureFilterEnum.Nearest,
			zAsRelative: false);
		flightCard.RotationDegrees = startRotationDegrees;
		flightCard.GlobalPosition = startGlobalPosition;

		try
		{
			Tween flightTween = CreateBoardTween();
			flightTween.SetParallel(true);
			flightTween.SetTrans(Tween.TransitionType.Sine);
			flightTween.SetEase(Tween.EaseType.InOut);
			flightTween.TweenMethod(
				Callable.From<float>(progress => UpdateItemCardToTransformAreaFlight(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				LensCardFlightSeconds);
			TweenFlightPulseScale(flightTween, flightCard, LensCardFlightSeconds, 1.12f);
			flightTween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					_animationPresenter.Release(flightCard);
				}
			};
			await WaitForBoardTweenAsync(flightTween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				_animationPresenter.Release(flightCard);
			}
		}
	}
}
