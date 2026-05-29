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
		var flightCard = new Control
		{
			Name = "StolenHandCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
			ZIndex = PlayedCardFlightZIndex + 840,
			ZAsRelative = false,
		};
		AddChild(flightCard);
		flightCard.GlobalPosition = startGlobalPosition;

		var cardVisual = new TextureRect
		{
			Name = "StolenHandCardFlightVisual",
			Texture = cardTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = (CardVisualSize - visualSize) / 2.0f,
			Size = visualSize,
			PivotOffset = visualSize / 2.0f,
		};
		flightCard.AddChild(cardVisual);

		if (attachmentOverlayTexture != null)
		{
			var attachmentOverlay = new TextureRect
			{
				Name = "StolenHandCardAttachmentOverlay",
				Texture = attachmentOverlayTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Linear,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				ZIndex = 1,
			};
			flightCard.AddChild(attachmentOverlay);
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
					flightCard.QueueFree();
				}
			};
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				flightCard.QueueFree();
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
