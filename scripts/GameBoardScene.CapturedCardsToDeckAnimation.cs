using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayCapturedCardsToDeckAnimationAsync(
		int playerIndex,
		int captureStartIndex,
		List<CapturedTableCardVisual> capturedCardVisuals,
		int turnSequenceGeneration,
		HashSet<int>? extraTableCardIndexes = null,
		float flightStaggerSeconds = TableCaptureFlightStaggerSeconds,
		int redirectedTableCardIndex = -1)
	{
		if (_networkAuthorityRuleTransactionActive
			|| _cardBackTexture == null
			|| playerIndex < 0
			|| playerIndex >= _playerCardPiles.Count
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count
			|| capturedCardVisuals.Count == 0)
		{
			return;
		}

		Control targetPile = _playerCardPiles[playerIndex];
		if (!GodotObject.IsInstanceValid(targetPile))
		{
			return;
		}

		var ownedPresentationHiddenRuntimeIds = new List<long>();
		try
		{
			for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
			{
				TableCard tableCard = _tableCards[tableCardIndex];
				if (tableCard.IsBuriedInSand || IsQuestionOnlyTableCard(tableCard))
				{
					continue;
				}

				if (_presentationHiddenTableCardRuntimeInstanceIds.Add(tableCard.InstanceId))
				{
					ownedPresentationHiddenRuntimeIds.Add(tableCard.InstanceId);
				}
			}
			if (extraTableCardIndexes != null)
			{
				foreach (int tableCardIndex in extraTableCardIndexes)
				{
					if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
					{
						continue;
					}

					TableCard tableCard = _tableCards[tableCardIndex];
					if (tableCard.IsBuriedInSand || IsQuestionOnlyTableCard(tableCard))
					{
						continue;
					}

					if (_presentationHiddenTableCardRuntimeInstanceIds.Add(tableCard.InstanceId))
					{
						ownedPresentationHiddenRuntimeIds.Add(tableCard.InstanceId);
					}
				}
			}
			RenderTableCards();

			PlayGameSfx(GameSfx.CaptureCollect, -1.0f, 0.96f, 1.04f, 0.08);
			Task[] flightTasks = new Task[capturedCardVisuals.Count];
			for (int visualIndex = 0; visualIndex < capturedCardVisuals.Count; visualIndex++)
			{
				int targetPlayerIndex = capturedCardVisuals[visualIndex].TableCardIndex
					== redirectedTableCardIndex
					? MainPlayerIndex
					: playerIndex;
				flightTasks[visualIndex] = PlayCapturedCardFlightAsync(
					capturedCardVisuals[visualIndex],
					GetCardPileCaptureTargetGlobalPosition(targetPlayerIndex),
					targetPlayerIndex,
					visualIndex * flightStaggerSeconds);
			}

			await Task.WhenAll(flightTasks);
		}
		finally
		{
			foreach (long runtimeInstanceId in ownedPresentationHiddenRuntimeIds)
			{
				_presentationHiddenTableCardRuntimeInstanceIds.Remove(runtimeInstanceId);
			}
		}
	}

	private bool IsQuestionOnlyTableCard(TableCard tableCard)
	{
		return !tableCard.Card.HasValue
			&& IsQuestionTableCard(tableCard);
	}

	private Vector2 GetCardPileCaptureTargetGlobalPosition(int playerIndex)
	{
		Control? visiblePileCard = GetVisiblePileCardControl(playerIndex);
		if (visiblePileCard != null)
		{
			return visiblePileCard.GetGlobalRect().Position;
		}

		return GetVisibleCardPileTopGlobalPosition(playerIndex);
	}

	private async Task PlayCapturedCardFlightAsync(
		CapturedTableCardVisual capturedCardVisual,
		Vector2 targetGlobalPosition,
		int playerIndex,
		float startDelaySeconds)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		TextureRect flightCard = _animationPresenter.RentTextureRect(
			this,
			"CapturedCardFlight",
			capturedCardVisual.StartTexture,
			CardVisualSize,
			PlayedCardFlightZIndex + capturedCardVisual.ZIndex,
			CanvasItem.TextureFilterEnum.Nearest,
			zAsRelative: false);
		flightCard.GlobalPosition = capturedCardVisual.GlobalPosition;

		TextureRect? itemOverlay = null;
		if (capturedCardVisual.ItemOverlayTexture != null)
		{
			itemOverlay = _animationPresenter.RentTextureRect(
				flightCard,
				"CapturedCardItemOverlayFlight",
				capturedCardVisual.ItemOverlayTexture,
				CardVisualSize,
				1,
				CanvasItem.TextureFilterEnum.Nearest);
		}

		try
		{
			if (startDelaySeconds > 0.0f)
			{
				await WaitForBoardDelayAsync(startDelaySeconds);
				if (!GodotObject.IsInstanceValid(flightCard))
				{
					return;
				}
			}

			Tween tween = CreateBoardTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCapturedCardFlightMotion(
					flightCard,
					capturedCardVisual.GlobalPosition,
					targetGlobalPosition,
					GetCardRotationDegreesForPlayer(playerIndex),
					progress)),
				0.0f,
				1.0f,
				TableCaptureFlightDuration);
			TweenCapturedCardFlightVisuals(
				tween,
				flightCard,
				itemOverlay,
				capturedCardVisual.StartTexture,
				_cardBackTexture!);
			tween.Finished += () =>
			{
				if (GodotObject.IsInstanceValid(flightCard))
				{
					_animationPresenter.Release(flightCard);
				}
			};

			await WaitForBoardTweenAsync(tween);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(flightCard))
			{
				_animationPresenter.Release(flightCard);
			}
		}
	}

	private static void UpdateCapturedCardFlightMotion(
		TextureRect flightCard,
		Vector2 startGlobalPosition,
		Vector2 targetGlobalPosition,
		float targetRotationDegrees,
		float progress)
	{
		if (!GodotObject.IsInstanceValid(flightCard))
		{
			return;
		}

		float clampedProgress = Mathf.Clamp(progress, 0.0f, 1.0f);
		float arcY = -Mathf.Sin(clampedProgress * Mathf.Pi) * TableCaptureFlightArcHeight;
		flightCard.GlobalPosition = startGlobalPosition.Lerp(targetGlobalPosition, clampedProgress) + new Vector2(0.0f, arcY);
		flightCard.RotationDegrees = Mathf.Lerp(0.0f, targetRotationDegrees, clampedProgress);
	}

	private static void TweenCapturedCardFlightVisuals(
		Tween tween,
		TextureRect flightCard,
		TextureRect? itemOverlay,
		Texture2D startTexture,
		Texture2D cardBackTexture)
	{
		flightCard.Texture = startTexture;
		float halfDuration = TableCaptureFlightDuration * 0.5f;
		float midScale = Mathf.Lerp(1.0f, TableCaptureFlightEndScale, 0.5f);
		tween.TweenProperty(flightCard, "scale", new Vector2(PlayedCardFlipMinScaleX * midScale, midScale), halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
		tween.TweenCallback(Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(flightCard))
			{
				return;
			}

			flightCard.Texture = cardBackTexture;
			if (itemOverlay != null && GodotObject.IsInstanceValid(itemOverlay))
			{
				itemOverlay.Visible = false;
			}
		})).SetDelay(halfDuration);
		tween.TweenProperty(flightCard, "scale", Vector2.One * TableCaptureFlightEndScale, halfDuration)
			.SetDelay(halfDuration)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}
}
