using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene : Control
{
	private const int MinPlayers = 2;
	private const int MaxPlayers = 4;
	private const string PokerCardsScenePath = "res://scenes/poker_cards_scene.tscn";
	private const string StandardCardsRootPath = "StandardPokerCards";
	private const string CardBackPath = "res://art/cards/standard_deck/back.png";
	private const string CardNamePrefix = "Card_";
	private const string JackRank = "J";
	private const int VisiblePileCards = 4;
	private const int StartingHandCards = 5;
	private const int MainPlayerIndex = 0;
	private static readonly int[] ClockwiseTurnOrder = { 0, 2, 1, 3 };
	private const float HoveredHandCardLift = 10.0f;
	private const int HoveredHandCardZIndexBoost = 100;
	private const float HandCardHoverTweenTime = 0.12f;
	private const float AiTurnDelaySeconds = 0.35f;
	private const float TableCaptureDelaySeconds = 0.45f;

	private static readonly Vector2 CardPileSize = new(82, 78);
	private static readonly Vector2 CardVisualSize = new(48, 64);
	private static readonly Vector2 CardPileOffset = new(3, -2);
	private static readonly Vector2 TableCardStartPosition = new(96, 20);
	private static readonly float TableCardVerticalStep = CardVisualSize.Y / 2.0f;
	private static readonly float TableCardColumnGap = CardVisualSize.X + 14.0f;
	private const float TableCardBottomInset = 28.0f;
	private const float TableCardTurnDepth = 24.0f;
	private const int TableCardTurnSlots = 3;
	private static readonly Vector2 HorizontalHandCardOffset = new(40, 0);
	private static readonly Vector2 VerticalHandCardOffset = new(0, 34);
	private static readonly Vector2[] CardPileCenters =
	{
		new(0.875f, 0.38f),
		new(0.875f, 0.66f),
		new(0.88f, 0.86f),
		new(0.14f, 0.86f),
	};
	private static readonly Vector2[] HandCenters =
	{
		new(0.46f, 0.46f),
		new(0.46f, 0.54f),
		new(0.52f, 0.42f),
		new(0.48f, 0.42f),
	};

	private int _playerCount = 2;
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<List<PlayingCard>> _playerDecks = new();
	private readonly List<List<PlayingCard>> _playerHands = new();
	private readonly List<TableCard> _tableCards = new();
	private readonly List<Control> _playerCardPiles = new();
	private readonly List<Control> _playerHandAreas = new();
	private Texture2D? _cardBackTexture;
	private Label _winnerLabel = default!;
	private int _currentTurnPlayerIndex = MainPlayerIndex;
	private int _turnSequenceGeneration;
	private bool _isResolvingTurnSequence;
	private bool _isProcessingCardPlay;
	private int _winnerPlayerIndex = -1;

	[Export(PropertyHint.Range, "2,4,1")]
	public int player_count
	{
		get => _playerCount;
		set
		{
			_playerCount = Mathf.Clamp(value, MinPlayers, MaxPlayers);
			if (IsInsideTree())
			{
				UpdatePlayerVisibility();
				DealCardsToPlayers();
			}
		}
	}

	[Export]
	public bool debug_opponent_has_one_card = true;

	private Control _centerArena = default!;
	private Control _topPlayerArea = default!;
	private Control _bottomPlayerArea = default!;
	private Control _leftPlayerArea = default!;
	private Control _rightPlayerArea = default!;

	public override void _Ready()
	{
		_rng.Randomize();
		_cardBackTexture = GD.Load<Texture2D>(CardBackPath);

		_centerArena = GetNode<Control>("%CenterArena");
		_topPlayerArea = GetNode<Control>("%TopPlayerArea");
		_bottomPlayerArea = GetNode<Control>("%BottomPlayerArea");
		_leftPlayerArea = GetNode<Control>("%LeftPlayerArea");
		_rightPlayerArea = GetNode<Control>("%RightPlayerArea");

		CreatePlayerCardPiles();
		CreatePlayerHandAreas();
		CreateWinnerLabel();
		UpdatePlayerVisibility();
		DealCardsToPlayers();
	}

	public Control GetPlayerArea(int playerIndex)
	{
		return playerIndex switch
		{
			0 => _bottomPlayerArea,
			1 => _topPlayerArea,
			2 => _leftPlayerArea,
			3 => _rightPlayerArea,
			_ => HandleInvalidPlayerIndex(),
		};
	}

	public Control get_player_area(int playerIndex)
	{
		return GetPlayerArea(playerIndex);
	}

	private Control HandleInvalidPlayerIndex()
	{
		GD.PushError("Player index must be between 0 and 3.");
		return _bottomPlayerArea;
	}

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

	private void DealCardsToPlayers()
	{
		if (_playerCardPiles.Count != MaxPlayers || _playerHandAreas.Count != MaxPlayers)
		{
			return;
		}

		List<PlayingCard> deck = LoadDeckWithoutJacks();
		Shuffle(deck);
		Cut(deck);

		_playerDecks.Clear();
		_playerHands.Clear();
		_tableCards.Clear();
		_turnSequenceGeneration++;
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_winnerPlayerIndex = -1;
		if (_winnerLabel != null)
		{
			_winnerLabel.Visible = false;
		}
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
		}

		int cardsPerPlayer = deck.Count / player_count;
		int cardsToDeal = cardsPerPlayer * player_count;

		if (deck.Count % player_count != 0)
		{
			GD.PushWarning($"Deck has {deck.Count} cards, which cannot be split evenly between {player_count} players.");
		}

		for (int cardIndex = 0; cardIndex < cardsToDeal; cardIndex++)
		{
			int playerIndex = cardIndex % player_count;
			_playerDecks[playerIndex].Add(deck[cardIndex]);
		}

		DrawStartingHands();
		ApplyDebugOpponentOneCardHand();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
	}

	private void DrawStartingHands()
	{
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			int cardsToDraw = Math.Min(StartingHandCards, _playerDecks[playerIndex].Count);
			for (int cardIndex = 0; cardIndex < cardsToDraw; cardIndex++)
			{
				PlayingCard topCard = _playerDecks[playerIndex][0];
				_playerDecks[playerIndex].RemoveAt(0);
				_playerHands[playerIndex].Add(topCard);
			}
		}
	}

	private void ApplyDebugOpponentOneCardHand()
	{
		const int opponentPlayerIndex = 1;
		if (!debug_opponent_has_one_card || player_count != 2 || _playerHands[opponentPlayerIndex].Count == 0)
		{
			return;
		}

		PlayingCard onlyCard = _playerHands[opponentPlayerIndex][0];
		_playerHands[opponentPlayerIndex].Clear();
		_playerHands[opponentPlayerIndex].Add(onlyCard);
		_playerDecks[opponentPlayerIndex].Clear();
	}

	private List<PlayingCard> LoadDeckWithoutJacks()
	{
		var deck = new List<PlayingCard>();
		PackedScene? pokerCardsScene = GD.Load<PackedScene>(PokerCardsScenePath);
		if (pokerCardsScene == null)
		{
			GD.PushError($"Missing poker cards scene: {PokerCardsScenePath}");
			return deck;
		}

		Node pokerCards = pokerCardsScene.Instantiate();
		Node? standardCards = pokerCards.GetNodeOrNull<Node>(StandardCardsRootPath);
		if (standardCards == null)
		{
			GD.PushError($"Missing {StandardCardsRootPath} in {PokerCardsScenePath}.");
			pokerCards.Free();
			return deck;
		}

		foreach (Node suitNode in standardCards.GetChildren())
		{
			string suit = suitNode.Name.ToString();
			if (suit == "Back")
			{
				continue;
			}

			foreach (Node cardNode in suitNode.GetChildren())
			{
				if (cardNode is not Sprite2D cardSprite || cardSprite.Texture == null)
				{
					continue;
				}

				string cardName = cardSprite.Name.ToString();
				string rank = cardName.StartsWith(CardNamePrefix, StringComparison.Ordinal)
					? cardName[CardNamePrefix.Length..]
					: cardName;

				if (rank == JackRank)
				{
					continue;
				}

				deck.Add(new PlayingCard(suit, rank, cardSprite.Texture));
			}
		}

		pokerCards.Free();
		return deck;
	}

	private void Shuffle(List<PlayingCard> deck)
	{
		for (int index = deck.Count - 1; index > 0; index--)
		{
			int swapIndex = _rng.RandiRange(0, index);
			(deck[index], deck[swapIndex]) = (deck[swapIndex], deck[index]);
		}
	}

	private void Cut(List<PlayingCard> deck)
	{
		if (deck.Count < 2)
		{
			return;
		}

		int cutIndex = _rng.RandiRange(1, deck.Count - 1);
		List<PlayingCard> topCards = deck.GetRange(0, cutIndex);
		deck.RemoveRange(0, cutIndex);
		deck.AddRange(topCards);
	}

	private void RenderPlayerCardPiles()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control pile = _playerCardPiles[playerIndex];
			foreach (Node child in pile.GetChildren())
			{
				child.QueueFree();
			}

			pile.Visible = playerIndex < player_count;
			if (!pile.Visible)
			{
				continue;
			}

			int cardCount = _playerDecks[playerIndex].Count;
			int visibleCards = Math.Min(cardCount, VisiblePileCards);
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
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore,
					Position = firstCardPosition + cardIndex * CardPileOffset,
					Size = CardVisualSize,
					ZIndex = cardIndex,
				};

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

	private void RenderPlayerHands()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			Control handArea = _playerHandAreas[playerIndex];
			foreach (Node child in handArea.GetChildren())
			{
				child.QueueFree();
			}

			handArea.Visible = playerIndex < player_count;
			if (!handArea.Visible)
			{
				continue;
			}

			List<PlayingCard> hand = _playerHands[playerIndex];
			Vector2 cardOffset = GetHandCardOffset(playerIndex);
			bool canMainPlayerAct = CanMainPlayerAct();
			for (int cardIndex = 0; cardIndex < hand.Count; cardIndex++)
			{
				PlayingCard card = hand[cardIndex];
				Vector2 basePosition = cardIndex * cardOffset;
				int baseZIndex = cardIndex;
				var cardSlot = new Control
				{
					MouseFilter = playerIndex == MainPlayerIndex && canMainPlayerAct ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore,
					Position = basePosition,
					Size = CardVisualSize,
					ZIndex = baseZIndex,
				};
				var cardVisual = new TextureRect
				{
					Texture = playerIndex == 0 || _cardBackTexture == null ? card.Texture : _cardBackTexture,
					ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
					StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
					TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
					MouseFilter = MouseFilterEnum.Ignore,
					Position = Vector2.Zero,
					Size = CardVisualSize,
				};

				if (playerIndex == MainPlayerIndex && canMainPlayerAct)
				{
					Tween? hoverTween = null;
					void AnimateHover(bool hovered)
					{
						hoverTween?.Kill();
						cardVisual.ZIndex = hovered ? HoveredHandCardZIndexBoost : 0;
						Vector2 targetPosition = hovered ? new Vector2(0.0f, -HoveredHandCardLift) : Vector2.Zero;
						hoverTween = cardVisual.CreateTween();
						hoverTween.SetTrans(Tween.TransitionType.Sine);
						hoverTween.SetEase(hovered ? Tween.EaseType.Out : Tween.EaseType.InOut);
						hoverTween.TweenProperty(cardVisual, "position", targetPosition, HandCardHoverTweenTime);
					}

					cardSlot.MouseEntered += () => AnimateHover(true);
					cardSlot.MouseExited += () => AnimateHover(false);

					int handIndex = cardIndex;
					cardSlot.GuiInput += inputEvent => TryPlayMainPlayerCard(inputEvent, handIndex);
				}

				cardSlot.AddChild(cardVisual);
				handArea.AddChild(cardSlot);
			}
		}
	}

	private void TryPlayMainPlayerCard(InputEvent inputEvent, int handIndex)
	{
		if (!CanMainPlayerAct())
		{
			return;
		}

		if (inputEvent is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
		{
			return;
		}

		_ = PlayMainPlayerCardAndAdvanceAsync(handIndex, _turnSequenceGeneration);
	}

	private void RenderTableCards()
	{
		foreach (Node child in _centerArena.GetChildren())
		{
			child.QueueFree();
		}

		List<Vector2> cardPositions = BuildTableCardPositions(_tableCards.Count);
		for (int cardIndex = 0; cardIndex < _tableCards.Count; cardIndex++)
		{
			var tableCard = new TextureRect
			{
				Texture = _tableCards[cardIndex].IsFaceUp ? _tableCards[cardIndex].Card.Texture : _cardBackTexture,
				ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
				MouseFilter = MouseFilterEnum.Ignore,
				Position = cardPositions[cardIndex],
				Size = CardVisualSize,
				ZIndex = cardIndex,
			};

			_centerArena.AddChild(tableCard);
		}
	}

	private List<Vector2> BuildTableCardPositions(int cardCount)
	{
		var positions = new List<Vector2>(cardCount);
		if (cardCount <= 0)
		{
			return positions;
		}

		float topY = TableCardStartPosition.Y + TableCardTurnDepth;
		float safeBottomY = Math.Max(topY, _centerArena.Size.Y - CardVisualSize.Y - TableCardBottomInset);
		float maxBottomTurnAnchorY = Math.Max(topY, safeBottomY - TableCardTurnDepth);
		int verticalSlots = Math.Max(1, Mathf.FloorToInt((maxBottomTurnAnchorY - topY) / TableCardVerticalStep) + 1);
		float bottomY = topY + (verticalSlots - 1) * TableCardVerticalStep;

		int columnIndex = 0;
		bool goingDown = true;
		while (positions.Count < cardCount)
		{
			float x = TableCardStartPosition.X + columnIndex * TableCardColumnGap;
			if (goingDown)
			{
				for (int slot = 0; slot < verticalSlots && positions.Count < cardCount; slot++)
				{
					positions.Add(new Vector2(x, topY + slot * TableCardVerticalStep));
				}

				AddTableTurnPositions(positions, cardCount, x, x + TableCardColumnGap, bottomY, TableCardTurnDepth);
			}
			else
			{
				for (int slot = 0; slot < verticalSlots && positions.Count < cardCount; slot++)
				{
					positions.Add(new Vector2(x, bottomY - slot * TableCardVerticalStep));
				}

				AddTableTurnPositions(positions, cardCount, x, x + TableCardColumnGap, topY, -TableCardTurnDepth);
			}

			columnIndex++;
			goingDown = !goingDown;
		}

		return positions;
	}

	private static void AddTableTurnPositions(List<Vector2> positions, int cardCount, float fromX, float toX, float anchorY, float turnDepth)
	{
		for (int turnSlot = 1; turnSlot <= TableCardTurnSlots && positions.Count < cardCount; turnSlot++)
		{
			float t = (float)turnSlot / (TableCardTurnSlots + 1);
			float x = Mathf.Lerp(fromX, toX, t);
			float y = anchorY + Mathf.Sin(Mathf.Pi * t) * turnDepth;
			positions.Add(new Vector2(x, y));
		}
	}

	private async Task ResolveOpponentTurnsAsync(int turnSequenceGeneration)
	{
		if (_isResolvingTurnSequence || IsGameOver())
		{
			return;
		}

		_isResolvingTurnSequence = true;
		try
		{
			while (turnSequenceGeneration == _turnSequenceGeneration && _currentTurnPlayerIndex != MainPlayerIndex && !IsGameOver())
			{
				int playerIndex = _currentTurnPlayerIndex;
				if (playerIndex >= player_count || _playerHands[playerIndex].Count == 0)
				{
					_currentTurnPlayerIndex = GetNextTurnPlayerIndex(playerIndex);
					continue;
				}

				await ToSignal(GetTree().CreateTimer(AiTurnDelaySeconds), SceneTreeTimer.SignalName.Timeout);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return;
				}

				PlayCardResult result = await PlayRandomCardForPlayerAsync(playerIndex, turnSequenceGeneration);
				if (IsGameOver())
				{
					return;
				}

				_currentTurnPlayerIndex = result.Captured ? playerIndex : GetNextTurnPlayerIndex(playerIndex);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				_isResolvingTurnSequence = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task PlayMainPlayerCardAndAdvanceAsync(int handIndex, int turnSequenceGeneration)
	{
		if (_isProcessingCardPlay || IsGameOver())
		{
			return;
		}

		_isProcessingCardPlay = true;
		RenderPlayerHands();
		try
		{
			PlayCardResult result = await PlayCardFromHandAsync(MainPlayerIndex, handIndex, turnSequenceGeneration);
			if (!result.Played)
			{
				return;
			}

			if (IsGameOver())
			{
				return;
			}

			_currentTurnPlayerIndex = result.Captured ? MainPlayerIndex : GetNextTurnPlayerIndex(MainPlayerIndex);
			_isProcessingCardPlay = false;
			if (!result.Captured)
			{
				await ResolveOpponentTurnsAsync(turnSequenceGeneration);
			}
		}
		finally
		{
			if (turnSequenceGeneration == _turnSequenceGeneration && !IsGameOver())
			{
				_isProcessingCardPlay = false;
				RenderPlayerHands();
			}
		}
	}

	private async Task<PlayCardResult> PlayRandomCardForPlayerAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		if (hand.Count == 0)
		{
			return new PlayCardResult(false, false);
		}

		int handIndex = _rng.RandiRange(0, hand.Count - 1);
		return await PlayCardFromHandAsync(playerIndex, handIndex, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> PlayCardFromHandAsync(int playerIndex, int handIndex, int turnSequenceGeneration)
	{
		if (playerIndex < 0 || playerIndex >= _playerHands.Count)
		{
			return new PlayCardResult(false, false);
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return new PlayCardResult(false, false);
		}

		PlayingCard playedCard = hand[handIndex];
		hand.RemoveAt(handIndex);
		int captureStartIndex = AddPlayedCardToTable(playedCard);

		RenderPlayerHands();
		RenderTableCards();
		if (captureStartIndex >= 0)
		{
			await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new PlayCardResult(false, false);
			}

			CaptureTableCards(playerIndex, captureStartIndex);
			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
		}
		else
		{
			DrawReplacementCard(playerIndex);
			RenderPlayerCardPiles();
			RenderPlayerHands();
		}

		CheckForWinner();
		return new PlayCardResult(true, captureStartIndex >= 0);
	}

	private int AddPlayedCardToTable(PlayingCard playedCard)
	{
		int matchingIndex = FindMatchingTableCardIndex(playedCard);
		bool isFaceUp = matchingIndex >= 0;
		if (isFaceUp)
		{
			_tableCards[matchingIndex].IsFaceUp = true;
		}

		_tableCards.Add(new TableCard(playedCard, isFaceUp));
		return matchingIndex;
	}

	private int FindMatchingTableCardIndex(PlayingCard playedCard)
	{
		for (int tableCardIndex = _tableCards.Count - 1; tableCardIndex >= 0; tableCardIndex--)
		{
			if (_tableCards[tableCardIndex].Card.Rank == playedCard.Rank)
			{
				return tableCardIndex;
			}
		}

		return -1;
	}

	private void CaptureTableCards(int playerIndex, int captureStartIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return;
		}

		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return;
		}

		int captureCount = _tableCards.Count - captureStartIndex;
		for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			_playerDecks[playerIndex].Add(_tableCards[tableCardIndex].Card);
		}

		_tableCards.RemoveRange(captureStartIndex, captureCount);
	}

	private void DrawReplacementCard(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return;
		}

		if (_playerDecks[playerIndex].Count == 0)
		{
			return;
		}

		_playerHands[playerIndex].Add(_playerDecks[playerIndex][0]);
		_playerDecks[playerIndex].RemoveAt(0);
	}

	private void CheckForWinner()
	{
		if (IsGameOver())
		{
			return;
		}

		int remainingPlayerIndex = -1;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (!PlayerHasCards(playerIndex))
			{
				continue;
			}

			if (remainingPlayerIndex >= 0)
			{
				return;
			}

			remainingPlayerIndex = playerIndex;
		}

		if (remainingPlayerIndex >= 0)
		{
			DeclareWinner(remainingPlayerIndex);
		}
	}

	private bool PlayerHasCards(int playerIndex)
	{
		return _playerHands[playerIndex].Count > 0 || _playerDecks[playerIndex].Count > 0;
	}

	private void DeclareWinner(int playerIndex)
	{
		_winnerPlayerIndex = playerIndex;
		_turnSequenceGeneration++;
		_isResolvingTurnSequence = false;
		_isProcessingCardPlay = false;
		_currentTurnPlayerIndex = playerIndex;

		_winnerLabel.Text = $"玩家 {playerIndex + 1} 获胜";
		_winnerLabel.Visible = true;
		RenderPlayerHands();
	}

	private bool IsGameOver()
	{
		return _winnerPlayerIndex >= 0;
	}

	private bool CanMainPlayerAct()
	{
		return !IsGameOver() && !_isResolvingTurnSequence && !_isProcessingCardPlay && _currentTurnPlayerIndex == MainPlayerIndex;
	}

	private int GetNextTurnPlayerIndex(int currentPlayerIndex)
	{
		int currentOrderIndex = Array.IndexOf(ClockwiseTurnOrder, currentPlayerIndex);
		if (currentOrderIndex < 0)
		{
			return MainPlayerIndex;
		}

		for (int offset = 1; offset <= ClockwiseTurnOrder.Length; offset++)
		{
			int nextPlayerIndex = ClockwiseTurnOrder[(currentOrderIndex + offset) % ClockwiseTurnOrder.Length];
			if (nextPlayerIndex < player_count)
			{
				return nextPlayerIndex;
			}
		}

		return MainPlayerIndex;
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

	private readonly struct PlayingCard
	{
		public PlayingCard(string suit, string rank, Texture2D texture)
		{
			Suit = suit;
			Rank = rank;
			Texture = texture;
		}

		public string Suit { get; }
		public string Rank { get; }
		public Texture2D Texture { get; }
	}

	private readonly record struct PlayCardResult(bool Played, bool Captured);

	private sealed class TableCard
	{
		public TableCard(PlayingCard card, bool isFaceUp)
		{
			Card = card;
			IsFaceUp = isFaceUp;
		}

		public PlayingCard Card { get; }
		public bool IsFaceUp { get; set; }
	}
}
