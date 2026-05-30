using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayOpeningDealCardFlightAsync(
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		int playerIndex,
		int dealNumber,
		float targetVisualScale)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		PlayGameSfx(GameSfx.CardDeal, -4.0f, 0.94f, 1.08f, 0.03);
		float clampedTargetVisualScale = Mathf.Max(0.01f, targetVisualScale);
		Vector2 targetVisualSize = CardVisualSize * clampedTargetVisualScale;
		Vector2 targetVisualBasePosition = GetCardVisualBasePosition(clampedTargetVisualScale);
		float startRotationDegrees = -8.0f + dealNumber % 5 * 4.0f;
		var cardRoot = new Control
		{
			Name = "OpeningDealCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = OpeningDealZIndex + dealNumber,
			ZAsRelative = false,
		};
		var cardVisual = new TextureRect
		{
			Name = "OpeningDealCardVisual",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = targetVisualBasePosition,
			Size = targetVisualSize,
			PivotOffset = targetVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
		};
		MarkAnimatedCardBack(cardVisual);
		cardRoot.AddChild(cardVisual);
		AddChild(cardRoot);
		cardRoot.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealSlotCardFlight(
					cardRoot,
					cardVisual,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					GetCardRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				OpeningDealCardFlightSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
			await ToSignal(GetTree().CreateTimer(OpeningDealCardStaggerSeconds), SceneTreeTimer.SignalName.Timeout);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(cardRoot))
			{
				cardRoot.QueueFree();
			}
		}
	}

	private async Task PlayOpeningDealPileCardFlightAsync(Vector2 startGlobalPosition, Vector2 targetGlobalPosition, int playerIndex, int dealIndex, float flightSeconds)
	{
		if (_cardBackTexture == null || !IsInsideTree())
		{
			return;
		}

		PlayGameSfx(GameSfx.CardDeal, -5.0f, 0.92f, 1.06f, 0.03);
		float startRotationDegrees = -6.0f + dealIndex % 4 * 4.0f;
		var cardRoot = new Control
		{
			Name = "OpeningPileCardFlight",
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = OpeningDealZIndex + 200 + dealIndex,
			ZAsRelative = false,
		};
		var cardVisual = new TextureRect
		{
			Name = "OpeningPileCardVisual",
			Texture = _cardBackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			RotationDegrees = startRotationDegrees,
		};
		MarkAnimatedCardBack(cardVisual);
		cardRoot.AddChild(cardVisual);
		AddChild(cardRoot);
		cardRoot.GlobalPosition = startGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateOpeningDealSlotCardFlight(
					cardRoot,
					cardVisual,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					GetCardPileRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				flightSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(cardRoot))
			{
				cardRoot.QueueFree();
			}
		}
	}

	private static void UpdateOpeningDealSlotCardFlight(
		Control cardRoot,
		TextureRect cardVisual,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float startRotationDegrees,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(cardRoot) || !GodotObject.IsInstanceValid(cardVisual))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float easedProgress = Mathf.SmoothStep(0.0f, 1.0f, clampedProgress);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * 34.0f;
		float pulse = Mathf.Sin(clampedProgress * Mathf.Pi);
		cardRoot.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, easedProgress) + new Vector2(0.0f, arcY);
		cardVisual.RotationDegrees = Mathf.Lerp(startRotationDegrees, targetRotationDegrees, easedProgress) + pulse * 8.0f;
	}
}
