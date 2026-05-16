using System;
using Godot;

public partial class GameBoardScene
{
	private void CreatePlayerCardPiles()
	{
		_playerCardPiles.Clear();

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control playerArea = GetPlayerArea(playerIndex);
			var pile = new Control
			{
				Name = $"Player{playerIndex + 1}CardPile",
				MouseFilter = MouseFilterEnum.Stop,
				AnchorLeft = CardPileCenters[playerIndex].X,
				AnchorTop = CardPileCenters[playerIndex].Y,
				AnchorRight = CardPileCenters[playerIndex].X,
				AnchorBottom = CardPileCenters[playerIndex].Y,
				OffsetLeft = -CardPileSize.X / 2.0f,
				OffsetTop = -CardPileSize.Y / 2.0f,
				OffsetRight = CardPileSize.X / 2.0f,
				OffsetBottom = CardPileSize.Y / 2.0f,
			};
			pile.MouseEntered += () => SetCardPileCountVisible(pile, true);
			pile.MouseExited += () => SetCardPileCountVisible(pile, false);

			playerArea.AddChild(pile);
			_playerCardPiles.Add(pile);
		}
	}

	private void CreatePlayerHandAreas()
	{
		_playerHandAreas.Clear();

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control playerArea = GetPlayerArea(playerIndex);
			Vector2 handSize = GetHandAreaSize(playerIndex);
			var handArea = new Control
			{
				Name = $"Player{playerIndex + 1}Hand",
				MouseFilter = MouseFilterEnum.Ignore,
				AnchorLeft = HandCenters[playerIndex].X,
				AnchorTop = HandCenters[playerIndex].Y,
				AnchorRight = HandCenters[playerIndex].X,
				AnchorBottom = HandCenters[playerIndex].Y,
				OffsetLeft = -handSize.X / 2.0f,
				OffsetTop = -handSize.Y / 2.0f,
				OffsetRight = handSize.X / 2.0f,
				OffsetBottom = handSize.Y / 2.0f,
			};

			playerArea.AddChild(handArea);
			_playerHandAreas.Add(handArea);
		}
	}

	private void CreateWinnerLabel()
	{
		_winnerLabel = new Label
		{
			Name = "WinnerLabel",
			Visible = false,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorLeft = 0.35f,
			AnchorTop = 0.42f,
			AnchorRight = 0.65f,
			AnchorBottom = 0.5f,
			ZIndex = 1000,
		};
		_winnerLabel.AddThemeColorOverride("font_color", Colors.White);
		_winnerLabel.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.85f));
		_winnerLabel.AddThemeConstantOverride("shadow_offset_x", 2);
		_winnerLabel.AddThemeConstantOverride("shadow_offset_y", 2);

		AddChild(_winnerLabel);
	}

	private static Vector2 GetHandAreaSize(int playerIndex)
	{
		Vector2 cardOffset = GetHandCardOffset(playerIndex);
		return new Vector2(
			CardVisualSize.X + Math.Abs(cardOffset.X) * (StartingHandCards - 1),
			CardVisualSize.Y + Math.Abs(cardOffset.Y) * (StartingHandCards - 1));
	}

	private static Vector2 GetHandCardOffset(int playerIndex)
	{
		return playerIndex < 2 ? HorizontalHandCardOffset : VerticalHandCardOffset;
	}

	private void SetCardPileCountVisible(Control pile, bool visible)
	{
		if (pile.GetNodeOrNull<Label>("CardCountLabel") is Label countLabel)
		{
			countLabel.Visible = visible;
		}
	}

	private void UpdatePlayerVisibility()
	{
		_bottomPlayerArea.Visible = player_count >= 1;
		_topPlayerArea.Visible = player_count >= 2;
		_leftPlayerArea.Visible = player_count >= 3;
		_rightPlayerArea.Visible = player_count >= 4;
	}
}
