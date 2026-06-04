using System;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayCardToTableAnimationAsync(
		int playerIndex,
		PlayingCard playedCard,
		Vector2 startGlobalPosition,
		int tableCardIndex,
		bool tableCardIsFaceUp,
		Texture2D? visibleTextureOverride = null)
	{
		if (!IsInsideTree() || tableCardIndex < 0)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 tableCardPosition))
		{
			return;
		}

		Texture2D? startTexture = visibleTextureOverride ?? GetVisibleHandCardTexture(playerIndex, playedCard);
		if (startTexture == null)
		{
			return;
		}

		Vector2 targetGlobalPosition = _centerArena.GlobalPosition + tableCardPosition;
		bool showMainPlayerPreview = playerIndex == MainPlayerIndex && !tableCardIsFaceUp;
		bool flipToCardBack = !showMainPlayerPreview && playerIndex == MainPlayerIndex && !tableCardIsFaceUp && _cardBackTexture != null;
		Texture2D targetTexture = flipToCardBack ? _cardBackTexture! : startTexture;
		float targetAlpha = showMainPlayerPreview ? MainPlayerTablePreviewAlpha : 1.0f;
		float startRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		Control flightCard = _animationPresenter.RentControl(this, "PlayedCardFlight", CardVisualSize, PlayedCardFlightZIndex);
		flightCard.RotationDegrees = startRotationDegrees;
		flightCard.GlobalPosition = startGlobalPosition;
		TextureRect cardVisual = _animationPresenter.RentTextureRect(
			flightCard,
			"PlayedCardFlightVisual",
			startTexture,
			CardVisualSize,
			0,
			CanvasItem.TextureFilterEnum.Nearest);
		TextureRect? attachmentOverlay = null;
		Texture2D? attachmentOverlayTexture = GetCardAttachmentOverlayTexture(playedCard);
		bool startShowsAttachmentOverlay = attachmentOverlayTexture != null
			&& (playerIndex == MainPlayerIndex || playerIndex == _revealedPlayerHandIndex || _cardBackTexture == null);
		if (startShowsAttachmentOverlay)
		{
			attachmentOverlay = _animationPresenter.RentTextureRect(
				flightCard,
				"PlayedCardAttachmentOverlay",
				attachmentOverlayTexture,
				CardVisualSize,
				1,
				CanvasItem.TextureFilterEnum.Linear);
		}

		try
		{
			PlayGameSfx(GameSfx.CardFlight, -2.0f, 0.96f, 1.05f, 0.04);
			UpdateCardToTableMotion(
				flightCard,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				0.0f);

			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateCardToTableMotion(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				PlayedCardFlightDuration);
			TweenPlayedCardVisuals(
				tween,
				flightCard,
				cardVisual,
				attachmentOverlay,
				startTexture,
				targetTexture,
				flipToCardBack,
				targetAlpha);
			tween.Finished += () =>
			{
				PlayGameSfx(GameSfx.CardLand, -2.0f, 0.96f, 1.05f, 0.04);
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

	private async Task PlayItemCardToTableAnimationAsync(
		int playerIndex,
		ItemCard itemCard,
		Vector2 startGlobalPosition,
		int tableCardIndex,
		bool tableCardIsFaceUp)
	{
		if (!IsInsideTree() || tableCardIndex < 0)
		{
			return;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 tableCardPosition))
		{
			return;
		}

		bool flipToCardBack = playerIndex == MainPlayerIndex && !tableCardIsFaceUp && _cardBackTexture != null;
		Texture2D? startTexture = flipToCardBack ? _cardBackTexture : GetVisibleHandItemCardTexture(playerIndex, itemCard);
		if (startTexture == null)
		{
			return;
		}

		Vector2 targetGlobalPosition = _centerArena.GlobalPosition + tableCardPosition;
		Texture2D targetTexture = flipToCardBack ? _cardBackTexture! : startTexture;
		float startRotationDegrees = GetCardRotationDegreesForPlayer(playerIndex);
		TextureRect flightCard = _animationPresenter.RentTextureRect(
			this,
			"PlayedItemCardFlight",
			startTexture,
			CardVisualSize,
			PlayedCardFlightZIndex,
			CanvasItem.TextureFilterEnum.Nearest,
			zAsRelative: false);
		flightCard.RotationDegrees = startRotationDegrees;
		flightCard.GlobalPosition = startGlobalPosition;

		try
		{
			PlayGameSfx(GameSfx.CardFlight, -2.0f, 0.95f, 1.04f, 0.04);
			UpdateItemCardToTableMotion(
				flightCard,
				startGlobalPosition,
				targetGlobalPosition,
				startRotationDegrees,
				0.0f);

			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.InOut);
			tween.TweenMethod(
				Callable.From<float>(progress => UpdateItemCardToTableMotion(
					flightCard,
					startGlobalPosition,
					targetGlobalPosition,
					startRotationDegrees,
					progress)),
				0.0f,
				1.0f,
				PlayedCardFlightDuration);
			TweenPlayedItemCardVisuals(
				tween,
				flightCard,
				startTexture,
				targetTexture,
				flipToCardBack);
			tween.Finished += () =>
			{
				PlayGameSfx(GameSfx.CardLand, -2.0f, 0.96f, 1.05f, 0.04);
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

	private Texture2D? GetVisibleHandCardTexture(int playerIndex, PlayingCard playedCard)
	{
		if (playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null)
		{
			return _cardBackTexture;
		}

		return playedCard.Texture;
	}

	private Texture2D? GetVisibleHandItemCardTexture(int playerIndex, ItemCard itemCard)
	{
		if (playerIndex != MainPlayerIndex && playerIndex != _revealedPlayerHandIndex && _cardBackTexture != null)
		{
			return _cardBackTexture;
		}

		return itemCard.Texture;
	}

}
