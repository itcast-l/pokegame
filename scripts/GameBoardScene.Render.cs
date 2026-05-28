using System;
using Godot;

public partial class GameBoardScene
{
	private void RenderPlayerCardPiles()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control pile = _playerCardPiles[playerIndex];

			pile.Visible = playerIndex < player_count;
			if (!pile.Visible)
			{
				SetPlayerCardPileVisualsInactive(pile);
				continue;
			}

			EnsurePlayerCardPileVisuals(pile);

			int cardCount = GetVisiblePileCardCountForRender(playerIndex, _playerDecks[playerIndex].Count);
			int visibleCards = Math.Min(cardCount, VisiblePileCards);
			float cardRotation = GetCardPileRotationDegreesForPlayer(playerIndex);
			Vector2 firstCardPosition = Vector2.Zero;
			if (visibleCards > 0)
			{
				float groupWidth = CardVisualSize.X + (visibleCards - 1) * CardPileOffset.X;
				float groupHeight = CardVisualSize.Y - (visibleCards - 1) * CardPileOffset.Y;
				firstCardPosition = new Vector2(
					(CardPileSize.X - groupWidth) / 2.0f,
					(CardPileSize.Y - groupHeight) / 2.0f - (visibleCards - 1) * CardPileOffset.Y);
			}

			for (int cardIndex = 0; cardIndex < VisiblePileCards; cardIndex++)
			{
				if (pile.GetNodeOrNull<TextureRect>(PileCardNodeNames[cardIndex]) is not TextureRect cardBack)
				{
					continue;
				}

				cardBack.Visible = cardIndex < visibleCards;
				cardBack.Texture = _cardBackTexture;
				cardBack.Position = firstCardPosition + cardIndex * CardPileOffset;
				cardBack.RotationDegrees = cardRotation;
				cardBack.ZIndex = cardIndex;
				SetAnimatedCardBackPlayback(cardBack, cardBack.Visible);
			}

			if (pile.GetNodeOrNull<Label>(CardCountLabelNodeName) is Label countLabel)
			{
				countLabel.Text = cardCount.ToString();
			}
		}
	}

	private void EnsurePlayerCardPileVisuals(Control pile)
	{
		for (int cardIndex = 0; cardIndex < VisiblePileCards; cardIndex++)
		{
			string cardNodeName = PileCardNodeNames[cardIndex];
			if (pile.GetNodeOrNull<TextureRect>(cardNodeName) != null)
			{
				continue;
			}

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
		}

		if (pile.GetNodeOrNull<Label>(CardCountLabelNodeName) != null)
		{
			return;
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
	}

	private void SetPlayerCardPileVisualsInactive(Control pile)
	{
		for (int cardIndex = 0; cardIndex < VisiblePileCards; cardIndex++)
		{
			if (pile.GetNodeOrNull<TextureRect>(PileCardNodeNames[cardIndex]) is not TextureRect cardBack)
			{
				continue;
			}

			cardBack.Visible = false;
			SetAnimatedCardBackPlayback(cardBack, false);
		}

		if (pile.GetNodeOrNull<Label>(CardCountLabelNodeName) is Label countLabel)
		{
			countLabel.Visible = false;
		}
	}

	private static void RemoveAndQueueFreeChild(Node parent, Node child)
	{
		parent.RemoveChild(child);
		child.QueueFree();
	}
}
