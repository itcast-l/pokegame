using System.Collections.Generic;
using Godot;

public partial class GameBoardScene : Control
{
	private const int MinPlayers = 2;
	private const int MaxPlayers = 4;
	private const string PokerCardsScenePath = "res://scenes/poker_cards_scene.tscn";
	private const string StandardCardsRootPath = "StandardPokerCards";
	private const string CardBackPath = "res://art/cards/ocean_animated_deck/back/processed/frames/frame-001.png";
	private const string CardBackFramePattern = "res://art/cards/ocean_animated_deck/back/processed/frames/frame-{0:000}.png";
	private const int CardBackFrameCount = 4;
	private const float CardBackAnimationFps = 6.0f;
	private const string ScoreFillEffectFramePattern = "res://art/fx/score_fill_orbs/run-001/processed/fx-{0}.png";
	private const int ScoreFillEffectFrameCount = 4;
	private const string ItemCardRootPath = "res://art/cards/item_cards";
	private const string QuestionMarkItemCardId = "question_mark";
	private const string JackItemCardId = "J";
	private const string QueenItemCardId = "Q";
	private const string KingItemCardId = "K";
	private const string CardNamePrefix = "Card_";
	private const string CardBackAnimatedMeta = "ocean_card_back_animated";
	private const string HandCardSlotMeta = "ocean_hand_card_slot";
	private const int VisiblePileCards = 4;
	private const int StartingHandCards = 5;
	private const int MaxTotalHandCards = 8;
	private const int MainPlayerIndex = 0;
	private static readonly HashSet<string> ExcludedDeckRanks = new() { JackItemCardId, QueenItemCardId, KingItemCardId };
	private static readonly HashSet<string> PlayableSpecialItemCardIds = new() { JackItemCardId, QueenItemCardId, KingItemCardId };
	private static readonly Dictionary<string, string> FaceCardItemTexturePaths = new()
	{
		[JackItemCardId] = "res://art/cards/ocean_animated_deck/court/J/processed/frames/frame-001.png",
		[QueenItemCardId] = "res://art/cards/ocean_animated_deck/court/Q/processed/frames/frame-001.png",
		[KingItemCardId] = "res://art/cards/ocean_animated_deck/court/K/processed/frames/frame-001.png",
	};
	private static readonly string[] AllItemCardIds =
	{
		QuestionMarkItemCardId,
		"bomb",
		"magnet",
		"greed",
		"lens",
		"double",
		"overtime",
		"draw_card",
		"shuffle",
		JackItemCardId,
		QueenItemCardId,
		KingItemCardId,
	};
	private static readonly string[] QuestionRewardItemCardIds =
	{
		"bomb",
		"magnet",
		"greed",
		"lens",
		"double",
		"overtime",
		"draw_card",
		"shuffle",
		JackItemCardId,
		QueenItemCardId,
		KingItemCardId,
	};
	private static readonly int[] ClockwiseTurnOrder = { 0, 2, 1, 3 };
	private const float HoveredHandCardLift = 14.0f;
	private const int HoveredHandCardZIndexBoost = 100;
	private const float HandCardHoverTweenTime = 0.12f;
	private const float AiTurnDelaySeconds = 0.35f;
	private const float TableCaptureDelaySeconds = 0.45f;
	private const float TableCardRevealHalfFlipSeconds = 0.16f;
	private const float TableCardRevealPauseSeconds = 0.07f;
	private const float TableCardRevealMinScaleX = 0.08f;
	private const float TableCaptureFlightDuration = 0.48f;
	private const float TableCaptureFlightStaggerSeconds = 0.07f;
	private const float TableCaptureFlightArcHeight = 42.0f;
	private const float TableCaptureFlightEndScale = 0.82f;
	private const int QuestionCardRefreshCooldownRounds = 2;

	private static readonly Vector2 CardPileSize = new(124, 128);
	private static readonly Vector2 CardVisualSize = new(72, 96);
	private static readonly Vector2 CardPileOffset = new(5, -4);
	private static readonly Vector2 TableCardStartPosition = new(96, 16);
	private const float FirstTableCardLift = 20.0f;
	private static readonly float TableCardVerticalStep = CardVisualSize.Y / 2.0f;
	private static readonly float TableCardColumnGap = CardVisualSize.X + 18.0f;
	private const float TableCardBottomInset = 28.0f;
	private const float TableCardTurnDepth = 24.0f;
	private const int TableCardTurnSlots = 3;
	private static readonly Vector2 HorizontalHandCardOffset = new(62, 0);
	private static readonly Vector2 VerticalHandCardOffset = new(0, 50);
	private int _playerCount = 2;
	private readonly RandomNumberGenerator _rng = new();
	private readonly List<List<PlayingCard>> _playerDecks = new();
	private readonly List<List<PlayingCard>> _playerHands = new();
	private readonly List<List<ItemCard>> _playerItemHands = new();
	private readonly List<TableCard> _tableCards = new();
	private readonly List<Control> _playerCardPiles = new();
	private readonly List<Control> _playerHandAreas = new();
	private readonly Dictionary<string, Texture2D> _itemCardTextures = new();
	private readonly List<Texture2D> _cardBackFrames = new();
	private readonly List<Texture2D> _scoreFillEffectFrames = new();
	private Control? _selectionOverlay;
	private Texture2D? _cardBackTexture;
	private float _cardBackAnimationTime;
	private Label _winnerLabel = default!;
	private int _currentTurnPlayerIndex = MainPlayerIndex;
	private int _turnSequenceGeneration;
	private int _completedTurnsSinceQuestionCardRefresh;
	private int _revealedPlayerHandIndex = -1;
	private PendingItemSelectionMode _pendingItemSelectionMode = PendingItemSelectionMode.None;
	private int _pendingItemOwnerIndex = -1;
	private int _pendingItemHandIndex = -1;
	private int _pendingItemTurnSequenceGeneration = -1;
	private string _pendingItemCardId = string.Empty;
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
	public bool debug_opponent_has_one_card = false;

	private Control _centerArena = default!;
	private Control _topPlayerArea = default!;
	private Control _bottomPlayerArea = default!;
	private Control _leftPlayerArea = default!;
	private Control _rightPlayerArea = default!;

	public override void _Ready()
	{
		_rng.Randomize();
		LoadCardBackTextures();
		LoadScoreFillEffectTextures();
		LoadItemCardTextures();

		_centerArena = GetNode<Control>("%CenterArena");
		_topPlayerArea = GetNode<Control>("%TopPlayerArea");
		_bottomPlayerArea = GetNode<Control>("%BottomPlayerArea");
		_leftPlayerArea = GetNode<Control>("%LeftPlayerArea");
		_rightPlayerArea = GetNode<Control>("%RightPlayerArea");

		CreatePlayerCardPiles();
		CreatePlayerHandAreas();
		BindPlayerScoreMeters();
		CreateWinnerLabel();
		UpdatePlayerVisibility();
		DealCardsToPlayers();
	}

	public override void _Process(double delta)
	{
		if (_cardBackFrames.Count <= 1)
		{
			return;
		}

		_cardBackAnimationTime += (float)delta;
		int frameIndex = Mathf.FloorToInt(_cardBackAnimationTime * CardBackAnimationFps) % _cardBackFrames.Count;
		UpdateAnimatedCardBacks(this, _cardBackFrames[frameIndex]);
	}

	private void LoadCardBackTextures()
	{
		_cardBackTexture = GD.Load<Texture2D>(CardBackPath);
		_cardBackFrames.Clear();
		for (int frameIndex = 1; frameIndex <= CardBackFrameCount; frameIndex++)
		{
			string path = string.Format(CardBackFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing card back animation frame: {path}");
				continue;
			}

			_cardBackFrames.Add(texture);
		}

		if (_cardBackTexture == null && _cardBackFrames.Count > 0)
		{
			_cardBackTexture = _cardBackFrames[0];
		}
	}

	private void LoadScoreFillEffectTextures()
	{
		_scoreFillEffectFrames.Clear();
		for (int frameIndex = 1; frameIndex <= ScoreFillEffectFrameCount; frameIndex++)
		{
			string path = string.Format(ScoreFillEffectFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing score fill effect frame: {path}");
				continue;
			}

			_scoreFillEffectFrames.Add(texture);
		}
	}

	private void MarkAnimatedCardBack(TextureRect cardBack)
	{
		cardBack.SetMeta(CardBackAnimatedMeta, true);
	}

	private void UpdateAnimatedCardBacks(Node root, Texture2D frame)
	{
		foreach (Node child in root.GetChildren())
		{
			if (child is TextureRect { } textureRect && textureRect.HasMeta(CardBackAnimatedMeta))
			{
				textureRect.Texture = frame;
			}

			UpdateAnimatedCardBacks(child, frame);
		}
	}

	private void LoadItemCardTextures()
	{
		_itemCardTextures.Clear();
		foreach (string itemCardId in AllItemCardIds)
		{
			string path = FaceCardItemTexturePaths.TryGetValue(itemCardId, out string? faceCardPath)
				? faceCardPath
				: $"{ItemCardRootPath}/{itemCardId}/run-004/processed/frames/frame-001.png";
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing item card texture: {path}");
				continue;
			}

			_itemCardTextures[itemCardId] = texture;
		}
	}

	private ItemCard? CreateItemCard(string itemCardId)
	{
		if (!_itemCardTextures.TryGetValue(itemCardId, out Texture2D? texture))
		{
			GD.PushWarning($"Missing loaded item card texture for {itemCardId}.");
			return null;
		}

		return new ItemCard(itemCardId, texture);
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
}
