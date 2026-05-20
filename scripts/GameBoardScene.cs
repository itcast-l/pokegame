using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene : Control
{
	private const int MinPlayers = 2;
	private const int MaxPlayers = 4;
	private const string PokerCardsScenePath = "res://scenes/poker_cards_scene.tscn";
	private const string ItemCardsScenePath = "res://scenes/item_cards_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const string StandardCardsRootPath = "StandardPokerCards";
	private const string SuitedJqkBackupCardsNodeName = "SuitedJqkBackupCards";
	private const string ItemCardsRootPath = "ItemCards";
	private const string CardBackPath = "res://art/cards/ocean_animated_deck/back/processed/frames/frame-001.png";
	private const string CardBackFramePattern = "res://art/cards/ocean_animated_deck/back/processed/frames/frame-{0:000}.png";
	private const int CardBackFrameCount = 4;
	private const float CardBackAnimationFps = 6.0f;
	private const string ScoreFillEffectFramePattern = "res://art/fx/score_fill_orbs/run-001/processed/fx-{0}.png";
	private const int ScoreFillEffectFrameCount = 4;
	private const string QuestionCardTransformFramePattern = "res://art/fx/question_card_transform/run-001/processed/fx-{0}.png";
	private const int QuestionCardTransformFrameCount = 6;
	private const string SpecialCardTriggerEffectFramePattern = "res://art/fx/special_card_trigger/ocean_chrysanthemum_preview5/sea_anemone_rays/processed/fx-{0}.png";
	private const int SpecialCardTriggerEffectFrameCount = 4;
	private const string BombPairDestroyEffectFramePattern = "res://art/fx/bomb_pair_destroy/card_rip_burst/run-001/processed/fx-{0}.png";
	private const int BombPairDestroyEffectFrameCount = 4;
	private const string LensRevealWispFramePattern = "res://art/fx/lens_reveal_sprites/run-001/processed/fx-{0}.png";
	private const int LensRevealWispFrameCount = 4;
	private const string MagnetCardOverlayPath = "res://art/fx/magnet_card_overlay/run-001/processed/card-aspect.png";
	private const string DoubleCardOverlayPath = "res://art/fx/double_card_overlay/run-002/processed/card-aspect.png";
	private const string OvertimeCardOverlayPath = "res://art/fx/overtime_card_overlay/run-001/processed/card-aspect.png";
	private const string MagnetCountdownFramePattern = "res://art/fx/magnet_selection_rework/run-001/countdown_orb/processed/fx-{0}.png";
	private const int MagnetCountdownFrameCount = 4;
	private const string VictoryBannerTexturePath = "res://art/ui/victory_banner/run-001/processed/banner-transparent.png";
	private const string ItemCardRootPath = "res://art/cards/item_cards";
	private const string QuestionMarkItemCardId = "question_mark";
	private const string BombItemCardId = "bomb";
	private const string MagnetItemCardId = "magnet";
	private const string GreedItemCardId = "greed";
	private const string LensItemCardId = "lens";
	private const string DoubleItemCardId = "double";
	private const string OvertimeItemCardId = "overtime";
	private const string DrawCardItemCardId = "draw_card";
	private const string ShuffleItemCardId = "shuffle";
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
	private static readonly HashSet<string> PlayableSpecialItemCardIds = new() { BombItemCardId, LensItemCardId, DoubleItemCardId, OvertimeItemCardId, DrawCardItemCardId, ShuffleItemCardId };
	private static readonly Dictionary<string, string> ItemCardArtRunsById = new()
	{
		[DoubleItemCardId] = "run-005",
	};
	private static readonly string[] AllItemCardIds =
	{
		QuestionMarkItemCardId,
		BombItemCardId,
		MagnetItemCardId,
		GreedItemCardId,
		LensItemCardId,
		DoubleItemCardId,
		OvertimeItemCardId,
		DrawCardItemCardId,
		ShuffleItemCardId,
	};
	private static readonly string[] QuestionRewardItemCardIds =
	{
		BombItemCardId,
		MagnetItemCardId,
		GreedItemCardId,
		LensItemCardId,
		DoubleItemCardId,
		OvertimeItemCardId,
		DrawCardItemCardId,
		ShuffleItemCardId,
	};
	private static readonly int[] ClockwiseTurnOrder = { 0, 2, 1, 3 };
	private const float HoveredHandCardLift = 18.0f;
	private const float HoveredHandCardScale = 1.06f;
	private const int HoveredHandCardZIndexBoost = 100;
	private const float HandCardHoverInTweenTime = 0.16f;
	private const float HandCardHoverOutTweenTime = 0.22f;
	private const float MainPlayerTablePreviewAlpha = 0.72f;
	private const float AiTurnDelaySeconds = 0.35f;
	private const float TableCaptureDelaySeconds = 0.45f;
	private const float TableCardRevealHalfFlipSeconds = 0.16f;
	private const float TableCardRevealPauseSeconds = 0.07f;
	private const float TableCardRevealMinScaleX = 0.08f;
	private const float TableCaptureFlightDuration = 0.48f;
	private const float TableCaptureFlightStaggerSeconds = 0.07f;
	private const float TableCaptureFlightArcHeight = 42.0f;
	private const float TableCaptureFlightEndScale = 0.82f;
	private const float BombDestroyCardStaggerSeconds = 0.08f;
	private const float BombPairDestroyFxFrameSeconds = 0.055f;
	private const float BombPairDestroyFxVisualSize = 132.0f;
	private const float TableCardPointPopupSeconds = 0.42f;
	private const float TableCardPointPopupRise = 24.0f;
	private const float TableCardPointPopupTopOffset = 26.0f;
	private const int TableCardPointPopupFontSize = 22;
	private const int TableCardPointPopupDoubleFontSize = 26;
	private const int TableCardPointPopupZIndex = 2800;
	private const int QuestionCardRefreshCyclesAfterCapture = 2;
	private const double MagnetAttachmentSelectionSeconds = 5.0;
	private const int LensRevealMaxCards = 2;

	private static readonly Vector2 CardPileSize = new(124, 128);
	private static readonly Vector2 CardVisualSize = new(72, 96);
	private static readonly Vector2 CardPileOffset = new(3, -2);
	private const int TableCardsPerRow = 7;
	private static readonly float TableCardHorizontalStep = CardVisualSize.X + 18.0f;
	private static readonly float TableCardRowGap = CardVisualSize.Y * 0.62f;
	private static readonly Vector2 HorizontalHandCardOffset = new(62, 0);
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
	private readonly List<Texture2D> _questionCardTransformFrames = new();
	private readonly List<Texture2D> _specialCardTriggerEffectFrames = new();
	private readonly List<Texture2D> _bombPairDestroyEffectFrames = new();
	private readonly List<Texture2D> _lensRevealWispFrames = new();
	private readonly List<Texture2D> _magnetCountdownFrameTextures = new();
	private readonly HashSet<string> _enabledSceneItemCardIds = new();
	private readonly Dictionary<string, string> _itemCardDescriptions = new();
	private readonly Queue<PendingItemAttachmentReward> _pendingItemAttachmentRewards = new();
	private Control? _selectionOverlay;
	private Texture2D? _cardBackTexture;
	private Texture2D? _magnetCardOverlayTexture;
	private Texture2D? _doubleCardOverlayTexture;
	private Texture2D? _overtimeCardOverlayTexture;
	private float _cardBackAnimationTime;
	private Control? _winnerBannerRoot;
	private Control? _winnerBannerPanel;
	private Button? _winnerReplayButton;
	private Label _winnerLabel = default!;
	private int _currentTurnPlayerIndex = MainPlayerIndex;
	private int _turnSequenceGeneration;
	private int _completedTurnsSinceQuestionCardRefresh;
	private int _questionCardRefreshAnchorPlayerIndex = -1;
	private int _revealedPlayerHandIndex = -1;
	private PendingItemSelectionMode _pendingItemSelectionMode = PendingItemSelectionMode.None;
	private int _pendingItemOwnerIndex = -1;
	private int _pendingItemHandIndex = -1;
	private int _pendingItemTurnSequenceGeneration = -1;
	private string _pendingItemCardId = string.Empty;
	private TaskCompletionSource<bool>? _pendingMagnetAttachmentCompletion;
	private Control? _magnetAttachmentCountdownRoot;
	private Label? _magnetAttachmentCountdownLabel;
	private Label? _magnetAttachmentCountdownHintLabel;
	private MagnetAttachmentCountdownRing? _magnetAttachmentCountdownRing;
	private double _magnetAttachmentCountdownRemainingSeconds;
	private double _magnetAttachmentCountdownPulseTime;
	private bool _isResolvingMagnetAttachmentSelection;
	private bool _isAnimatingMagnetAttachment;
	private bool _isResolvingTurnSequence;
	private bool _isProcessingCardPlay;
	private bool _isOpeningDealAnimationPlaying;
	private readonly List<int> _openingVisibleHandCardCounts = new();
	private readonly List<int> _openingVisiblePileCardCounts = new();
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
				DealCardsToPlayers(playOpeningDealAnimation: true);
			}
		}
	}

	[Export]
	public bool debug_opponent_has_one_card = false;

	[Export]
	public bool debug_main_player_has_one_card = false;

	private Control _centerArena = default!;
	private Control _tableCardLayoutArea = default!;
	private Control _topPlayerArea = default!;
	private Control _bottomPlayerArea = default!;
	private Control _leftPlayerArea = default!;
	private Control _rightPlayerArea = default!;
	private Control _questionCardTransformArea = default!;

	public override void _Ready()
	{
		Localization.EnsureLoaded();
		ApplySavedLanguagePreference();
		_rng.Randomize();
		LoadCardBackTextures();
		LoadScoreFillEffectTextures();
		LoadItemCardTextures();
		LoadItemCardEnabledStates();
		LoadAttachmentCardOverlayTextures();
		LoadQuestionCardTransformTextures();
		LoadSpecialCardTriggerEffectTextures();
		LoadBombPairDestroyEffectTextures();
		LoadLensRevealWispTextures();
		LoadMagnetCountdownFxTextures();

		_centerArena = GetNode<Control>("%CenterArena");
		_tableCardLayoutArea = GetNode<Control>("%TableCardLayoutArea");
		_topPlayerArea = GetNode<Control>("%TopPlayerArea");
		_bottomPlayerArea = GetNode<Control>("%BottomPlayerArea");
		_leftPlayerArea = GetNode<Control>("%LeftPlayerArea");
		_rightPlayerArea = GetNode<Control>("%RightPlayerArea");

		CreatePlayerCardPiles();
		CreatePlayerHandAreas();
		BindPlayerAvatarAreas();
		CreatePlayerAvatars();
		BindQuestionCardTransformArea();
		BindPlayerScoreMeters();
		CreateWinnerLabel();
		UpdatePlayerVisibility();
		DealCardsToPlayers(playOpeningDealAnimation: true);
	}

	public override void _Process(double delta)
	{
		UpdateMagnetAttachmentCountdown(delta);

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

	private void LoadQuestionCardTransformTextures()
	{
		_questionCardTransformFrames.Clear();
		for (int frameIndex = 1; frameIndex <= QuestionCardTransformFrameCount; frameIndex++)
		{
			string path = string.Format(QuestionCardTransformFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing question card transform frame: {path}");
				continue;
			}

			_questionCardTransformFrames.Add(texture);
		}
	}

	private void LoadSpecialCardTriggerEffectTextures()
	{
		_specialCardTriggerEffectFrames.Clear();
		for (int frameIndex = 1; frameIndex <= SpecialCardTriggerEffectFrameCount; frameIndex++)
		{
			string path = string.Format(SpecialCardTriggerEffectFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing special card trigger effect frame: {path}");
				continue;
			}

			_specialCardTriggerEffectFrames.Add(texture);
		}
	}

	private void LoadBombPairDestroyEffectTextures()
	{
		_bombPairDestroyEffectFrames.Clear();
		for (int frameIndex = 1; frameIndex <= BombPairDestroyEffectFrameCount; frameIndex++)
		{
			string path = string.Format(BombPairDestroyEffectFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing bomb pair destroy effect frame: {path}");
				continue;
			}

			_bombPairDestroyEffectFrames.Add(texture);
		}
	}

	private void LoadLensRevealWispTextures()
	{
		_lensRevealWispFrames.Clear();
		for (int frameIndex = 1; frameIndex <= LensRevealWispFrameCount; frameIndex++)
		{
			string path = string.Format(LensRevealWispFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing lens reveal wisp frame: {path}");
				continue;
			}

			_lensRevealWispFrames.Add(texture);
		}
	}

	private void LoadAttachmentCardOverlayTextures()
	{
		_magnetCardOverlayTexture = GD.Load<Texture2D>(MagnetCardOverlayPath);
		if (_magnetCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing magnet card overlay texture: {MagnetCardOverlayPath}");
		}

		_doubleCardOverlayTexture = GD.Load<Texture2D>(DoubleCardOverlayPath);
		if (_doubleCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing double card overlay texture: {DoubleCardOverlayPath}");
		}

		_overtimeCardOverlayTexture = GD.Load<Texture2D>(OvertimeCardOverlayPath);
		if (_overtimeCardOverlayTexture == null)
		{
			GD.PushWarning($"Missing overtime card overlay texture: {OvertimeCardOverlayPath}");
		}
	}

	private void LoadMagnetCountdownFxTextures()
	{
		_magnetCountdownFrameTextures.Clear();
		for (int frameIndex = 1; frameIndex <= MagnetCountdownFrameCount; frameIndex++)
		{
			string path = string.Format(MagnetCountdownFramePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing magnet countdown frame: {path}");
				continue;
			}

			_magnetCountdownFrameTextures.Add(texture);
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
			string path = $"{ItemCardRootPath}/{itemCardId}/{GetItemCardArtRunName(itemCardId)}/processed/frames/frame-001.png";
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing item card texture: {path}");
				continue;
			}

			_itemCardTextures[itemCardId] = texture;
		}
	}

	private static string GetItemCardArtRunName(string itemCardId)
	{
		return ItemCardArtRunsById.TryGetValue(itemCardId, out string? runName) ? runName : "run-004";
	}

	private ItemCard? CreateItemCard(string itemCardId)
	{
		if (!IsSpecialItemCardEnabled(itemCardId))
		{
			return null;
		}

		if (!_itemCardTextures.TryGetValue(itemCardId, out Texture2D? texture))
		{
			GD.PushWarning($"Missing loaded item card texture for {itemCardId}.");
			return null;
		}

		return new ItemCard(itemCardId, texture);
	}

	private bool IsSpecialItemCardEnabled(string itemCardId)
	{
		return itemCardId switch
		{
			JackItemCardId or QueenItemCardId or KingItemCardId => false,
			_ => _enabledSceneItemCardIds.Contains(itemCardId),
		};
	}

	private static bool IsExcludedDeckRank(string rank)
	{
		if (ExcludedDeckRanks.Contains(rank))
		{
			return true;
		}

		int suffixStartIndex = rank.LastIndexOf('_') + 1;
		return suffixStartIndex > 0
			&& suffixStartIndex < rank.Length
			&& ExcludedDeckRanks.Contains(rank[suffixStartIndex..]);
	}

	private void LoadItemCardEnabledStates()
	{
		_enabledSceneItemCardIds.Clear();
		_itemCardDescriptions.Clear();
		PackedScene? itemCardsScene = GD.Load<PackedScene>(ItemCardsScenePath);
		if (itemCardsScene == null)
		{
			GD.PushError($"Missing item cards scene: {ItemCardsScenePath}");
			return;
		}

		Node itemCards = itemCardsScene.Instantiate();
		Node? itemCardsRoot = itemCards.GetNodeOrNull<Node>(ItemCardsRootPath);
		if (itemCardsRoot == null)
		{
			GD.PushError($"Missing {ItemCardsRootPath} in {ItemCardsScenePath}.");
			itemCards.Free();
			return;
		}

		foreach (Node itemCardNode in itemCardsRoot.GetChildren())
		{
			if (itemCardNode is not ItemCardData itemCardData || !itemCardData.enabled_in_game)
			{
				continue;
			}

			if (itemCardData.TryGetItemCardId(out string itemCardId))
			{
				_enabledSceneItemCardIds.Add(itemCardId);
				_itemCardDescriptions[itemCardId] = GetItemDescription(itemCardId, itemCardData.item_description);
			}
		}

		itemCards.Free();
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
