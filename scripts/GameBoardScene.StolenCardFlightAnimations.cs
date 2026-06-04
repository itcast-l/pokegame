using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayStolenHandCardFlightAsync(
		Texture2D cardTexture,
		Texture2D? attachmentOverlayTexture,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float visualScale,
		float targetVisualScale)
	{
		if (!IsInsideTree())
		{
			return;
		}

		Vector2 visualSize = CardVisualSize * visualScale;
		Control flightCard = _animationPresenter.RentControl(
			this,
			"StolenHandCardFlight",
			CardVisualSize,
			PlayedCardFlightZIndex + 840);
		flightCard.RotationDegrees = startRotationDegrees;
		flightCard.GlobalPosition = startGlobalPosition;

		TextureRect cardVisual = _animationPresenter.RentTextureRect(
			flightCard,
			"StolenHandCardFlightVisual",
			cardTexture,
			visualSize,
			0,
			CanvasItem.TextureFilterEnum.Nearest);
		cardVisual.Position = (CardVisualSize - visualSize) / 2.0f;

		if (attachmentOverlayTexture != null)
		{
			_ = _animationPresenter.RentTextureRect(
				flightCard,
				"StolenHandCardAttachmentOverlay",
				attachmentOverlayTexture,
				CardVisualSize,
				1,
				CanvasItem.TextureFilterEnum.Linear);
		}

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateStolenHandCardFlight(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					targetRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				StolenCardFlightSeconds);
			TweenStolenHandCardScale(tween, flightCard, StolenCardFlightSeconds, targetVisualScale);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					_animationPresenter.Release(flightCard);
				}
			};
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				_animationPresenter.Release(flightCard);
			}
		}
	}

	private async Task PlayStolenDeckCardFlightAsync(
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees)
	{
		Texture2D? cardTexture = _cardBackTexture;
		if (cardTexture == null || !IsInsideTree())
		{
			return;
		}

		await PlayStolenHandCardFlightAsync(
			cardTexture,
			null,
			startGlobalPosition,
			targetGlobalPosition,
			startRotationDegrees,
			targetRotationDegrees,
			1.0f,
			1.0f);
	}
}
