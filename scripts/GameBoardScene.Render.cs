using System;
using Godot;

public partial class GameBoardScene
{
	private sealed record PlayerCardPileVisualBinding(
		TextureRect[] CardBacks,
		Label CountLabel);

	private readonly int[] _playerCardPileRenderSignatures = new int[MaxPlayers];
	private readonly bool[] _playerCardPileRenderSignaturesInitialized = new bool[MaxPlayers];
	private readonly PlayerCardPileVisualBinding?[] _playerCardPileVisualBindings =
		new PlayerCardPileVisualBinding?[MaxPlayers];

	private void RenderPlayerCardPiles()
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		ulong sampleStart = BeginPerformanceSample();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control pile = _playerCardPiles[playerIndex];
			int renderSignature = BuildPlayerCardPileRenderSignature(playerIndex, pile);
			if (_playerCardPileRenderSignaturesInitialized[playerIndex]
				&& _playerCardPileRenderSignatures[playerIndex] == renderSignature)
			{
				continue;
			}

			_playerCardPileRenderSignaturesInitialized[playerIndex] = true;
			_playerCardPileRenderSignatures[playerIndex] = renderSignature;

			pile.Visible = playerIndex < player_count;
			if (!pile.Visible)
			{
				SetPlayerCardPileVisualsInactive(playerIndex);
				continue;
			}

			PlayerCardPileVisualBinding visuals =
				GetOrCreatePlayerCardPileVisuals(playerIndex, pile);

			int cardCount = GetVisiblePileCardCountForRender(playerIndex, _playerDecks[playerIndex].Count);
			int visibleCards = Math.Min(cardCount, VisiblePileCards);
			float cardRotation = GetCardPileRotationDegreesForPlayer(playerIndex);

			for (int cardIndex = 0; cardIndex < VisiblePileCards; cardIndex++)
			{
				TextureRect cardBack = visuals.CardBacks[cardIndex];
				cardBack.Visible = cardIndex < visibleCards;
				cardBack.Texture = _cardBackTexture;
				cardBack.Position = GetCardPileCardLocalPosition(visibleCards, cardIndex);
				cardBack.RotationDegrees = cardRotation;
				cardBack.ZIndex = cardIndex;
				SetAnimatedCardBackPlayback(cardBack, cardBack.Visible);
			}

			visuals.CountLabel.Text = cardCount.ToString();
		}

		EndPerformanceSample("RenderPlayerCardPiles", sampleStart);
	}

	private int BuildPlayerCardPileRenderSignature(int playerIndex, Control pile)
	{
		var signature = new HashCode();
		bool playerVisible = playerIndex < player_count;
		signature.Add(playerVisible);
		if (!playerVisible)
		{
			return signature.ToHashCode();
		}

		int cardCount = GetVisiblePileCardCountForRender(playerIndex, _playerDecks[playerIndex].Count);
		signature.Add(player_count);
		signature.Add(cardCount);
		signature.Add(Math.Min(cardCount, VisiblePileCards));
		signature.Add(GetCardPileRotationDegreesForPlayer(playerIndex));
		signature.Add(_isOpeningDealAnimationPlaying);
		signature.Add(pile.Size.X);
		signature.Add(pile.Size.Y);
		AddTextureSignature(ref signature, _cardBackTexture);
		return signature.ToHashCode();
	}

	private void InvalidateRenderCaches()
	{
		Array.Fill(_playerHandRenderSignaturesInitialized, false);
		Array.Fill(_playerCardPileRenderSignaturesInitialized, false);
		_tableRenderSignatureInitialized = false;
	}

	private PlayerCardPileVisualBinding GetOrCreatePlayerCardPileVisuals(
		int playerIndex,
		Control pile)
	{
		PlayerCardPileVisualBinding? existing = _playerCardPileVisualBindings[playerIndex];
		if (existing != null
			&& GodotObject.IsInstanceValid(existing.CountLabel)
			&& Array.TrueForAll(existing.CardBacks, GodotObject.IsInstanceValid))
		{
			return existing;
		}

		var cardBacks = new TextureRect[VisiblePileCards];
		for (int cardIndex = 0; cardIndex < VisiblePileCards; cardIndex++)
		{
			string cardNodeName = PileCardNodeNames[cardIndex];
			var cardBack = new TextureRect
			{
				Name = cardNodeName,
				Texture = _cardBackTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				Size = CardVisualSize,
				PivotOffset = CardVisualSize / 2.0f,
				Visible = false,
			};
			MarkAnimatedCardBack(cardBack);
			SetAnimatedCardBackPlayback(cardBack, false);
			pile.AddChild(cardBack);
			cardBacks[cardIndex] = cardBack;
		}

		var countLabel = new Label
		{
			Name = CardCountLabelNodeName,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			Position = new Vector2(0.0f, CardPileSize.Y - 22.0f),
			Size = new Vector2(CardPileSize.X, 22.0f),
			Visible = false,
			ZIndex = VisiblePileCards + 1,
		};
		countLabel.AddThemeColorOverride("font_color", Colors.White);
		countLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
		countLabel.AddThemeConstantOverride("shadow_offset_x", 1);
		countLabel.AddThemeConstantOverride("shadow_offset_y", 1);
		pile.AddChild(countLabel);

		var binding = new PlayerCardPileVisualBinding(cardBacks, countLabel);
		_playerCardPileVisualBindings[playerIndex] = binding;
		return binding;
	}

	private void SetPlayerCardPileVisualsInactive(int playerIndex)
	{
		PlayerCardPileVisualBinding? visuals = GetPlayerCardPileVisuals(playerIndex);
		if (visuals == null)
		{
			return;
		}

		foreach (TextureRect cardBack in visuals.CardBacks)
		{
			cardBack.Visible = false;
			SetAnimatedCardBackPlayback(cardBack, false);
		}
		visuals.CountLabel.Visible = false;
	}

	private PlayerCardPileVisualBinding? GetPlayerCardPileVisuals(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerCardPileVisualBindings.Length)
		{
			return null;
		}

		PlayerCardPileVisualBinding? visuals = _playerCardPileVisualBindings[playerIndex];
		return visuals != null
			&& GodotObject.IsInstanceValid(visuals.CountLabel)
			&& Array.TrueForAll(visuals.CardBacks, GodotObject.IsInstanceValid)
			? visuals
			: null;
	}

	private TextureRect? GetPlayerCardPileBack(int playerIndex, int cardIndex)
	{
		PlayerCardPileVisualBinding? visuals = GetPlayerCardPileVisuals(playerIndex);
		return visuals != null && cardIndex >= 0 && cardIndex < visuals.CardBacks.Length
			? visuals.CardBacks[cardIndex]
			: null;
	}

	private Label? GetPlayerCardPileCountLabel(int playerIndex)
	{
		return GetPlayerCardPileVisuals(playerIndex)?.CountLabel;
	}

	private static void RemoveAndQueueFreeChild(Node parent, Node child)
	{
		parent.RemoveChild(child);
		child.Free();
	}

	private static Vector2 GetCardPileCardLocalPosition(int visibleCards, int cardIndex)
	{
		int clampedVisibleCards = Mathf.Clamp(visibleCards, 1, VisiblePileCards);
		int clampedCardIndex = Mathf.Clamp(cardIndex, 0, clampedVisibleCards - 1);
		float groupWidth = CardVisualSize.X + (clampedVisibleCards - 1) * CardPileOffset.X;
		float groupHeight = CardVisualSize.Y - (clampedVisibleCards - 1) * CardPileOffset.Y;
		Vector2 firstCardPosition = new(
			(CardPileSize.X - groupWidth) / 2.0f,
			(CardPileSize.Y - groupHeight) / 2.0f - (clampedVisibleCards - 1) * CardPileOffset.Y);
		return firstCardPosition + clampedCardIndex * CardPileOffset;
	}

	private Vector2 GetCardPileCardGlobalPosition(int playerIndex, int visibleCards, int cardIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerCardPiles.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		return _playerCardPiles[playerIndex].GlobalPosition + GetCardPileCardLocalPosition(visibleCards, cardIndex);
	}

	private Vector2 GetVisibleCardPileTopGlobalPosition(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerCardPiles.Count || playerIndex >= _playerDecks.Count)
		{
			return GetOpeningDealStartGlobalPosition();
		}

		int cardCount = GetVisiblePileCardCountForRender(playerIndex, _playerDecks[playerIndex].Count);
		int visibleCards = Math.Min(cardCount, VisiblePileCards);
		if (visibleCards <= 0)
		{
			return GetCardPileCardGlobalPosition(playerIndex, 1, 0);
		}

		return GetCardPileCardGlobalPosition(playerIndex, visibleCards, visibleCards - 1);
	}
}
