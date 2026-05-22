using System;
using Godot;

public partial class GameBoardScene
{
	private void RenderPlayerCardPiles()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control pile = _playerCardPiles[playerIndex];
			foreach (Node child in pile.GetChildren())
			{
				RemoveAndQueueFreeChild(pile, child);
			}

			pile.Visible = playerIndex < player_count;
			if (!pile.Visible)
			{
				continue;
			}

			int cardCount = GetVisiblePileCardCountForRender(playerIndex, _playerDecks[playerIndex].Count);
			int visibleCards = Math.Min(cardCount, VisiblePileCards);
			float cardRotation = GetCardPileRotationDegreesForPlayer(playerIndex);
			float groupWidth = CardVisualSize.X + (visibleCards - 1) * CardPileOffset.X;
			float groupHeight = CardVisualSize.Y - (visibleCards - 1) * CardPileOffset.Y;
			var firstCardPosition = new Vector2(
				(CardPileSize.X - groupWidth) / 2.0f,
				(CardPileSize.Y - groupHeight) / 2.0f - (visibleCards - 1) * CardPileOffset.Y);

			for (int cardIndex = 0; cardIndex < visibleCards; cardIndex++)
			{
				var cardBack = new TextureRect
				{
					Texture = _cardBackTexture,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore,
					Position = firstCardPosition + cardIndex * CardPileOffset,
					Size = CardVisualSize,
					PivotOffset = CardVisualSize / 2.0f,
					RotationDegrees = cardRotation,
					ZIndex = cardIndex,
				};

				MarkAnimatedCardBack(cardBack);
				pile.AddChild(cardBack);
			}

			var countLabel = new Label
			{
				Name = "CardCountLabel",
				Text = cardCount.ToString(),
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
	}

	private static void RemoveAndQueueFreeChild(Node parent, Node child)
	{
		parent.RemoveChild(child);
		child.QueueFree();
	}
}
