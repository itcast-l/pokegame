using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene : Control
{
	private const int MinPlayers = 2;
	private const int MaxPlayers = 4;
	private const string PokerCardsScenePath = "res://scenes/poker_cards_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const string StandardCardsRootPath = "StandardPokerCards";
	private const string SuitedJqkBackupCardsNodeName = "SuitedJqkBackupCards";
	private const float CardBackAnimationFps = 12.0f;
	private const string ScoreFillEffectFramePattern = "res://art/fx/score_fill_orbs/run-001/processed/fx-{0}.png";
	private const int ScoreFillEffectFrameCount = 4;
	private const string EnemySpecialAbilityAvatarFxFramePattern = "res://art/fx/enemy_special_ability_avatar/run-001/processed/fx-{0}.png";
	private const int EnemySpecialAbilityAvatarFxFrameCount = 6;
	private static readonly Texture2D[] ScoreFillEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(ScoreFillEffectFramePattern, ScoreFillEffectFrameCount, "score fill effect");
	private static readonly Texture2D[] EnemySpecialAbilityAvatarFxFrames =
		FrameAnimationHelpers.LoadTextureFrames(
			EnemySpecialAbilityAvatarFxFramePattern,
			EnemySpecialAbilityAvatarFxFrameCount,
			"enemy special ability avatar effect",
			preferImageFile: true);
	private static readonly Texture2D[] MantaRayTideShiftCycloneFxFrames =
	{
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-1.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-2.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-3.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-4.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-5.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-6.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-7.png"),
		GD.Load<Texture2D>("res://art/fx/player_manta_ray_tide_shift_cyclone/run-002/processed/fx-8.png"),
	};
	private const string AdventureDamageEffectFramePattern = "res://art/fx/attack_bubble_burst/run-001/processed/fx-{0}.png";
	private const string CatAdventureDamageEffectFramePattern = "res://art/fx/player_cat_claw_attack/run-001/processed/fx-{0}.png";
	private const string FishAdventureDamageEffectFramePattern = "res://art/fx/fish_attack_burst/run-001/processed/fx-{0}.png";
	private const string LanternfishAdventureDamageEffectFramePattern = "res://art/fx/lanternfish_lure_flash/run-001/processed/fx-{0}.png";
	private const string ShrimpAdventureDamageEffectFramePattern = "res://art/fx/player_shrimp_snap_attack/run-001/processed/fx-{0}.png";
	private const string CrabAdventureDamageEffectFramePattern = "res://art/fx/player_crab_tidal_pincer_attack/run-001/processed/fx-{0}.png";
	private const string JellyfishAdventureDamageEffectFramePattern = "res://art/fx/player_jellyfish_sting_attack/run-002/processed/fx-{0}.png";
	private const string GardenEelAdventureDamageEffectFramePattern = "res://art/fx/player_garden_eel_burrow_ambush/run-001/processed/fx-{0}.png";
	private const string SeaSlugAdventureDamageEffectFramePattern = "res://art/fx/player_sea_slug_hit_marker/run-001/processed/fx-{0}.png";
	private const string NautilusAdventureDamageEffectFramePattern = "res://art/fx/player_nautilus_spiral_pressure_slash/run-001/processed/fx-{0}.png";
	private const string NarwhalAdventureDamageEffectFramePattern = "res://art/fx/player_narwhal_tusk_break/run-001/processed/fx-{0}.png";
	private const string DragonPrinceAdventureDamageEffectFramePattern = "res://art/fx/player_dragon_prince_damage_hit/run-001/processed/fx-{0}.png";
	private const string DragonPrinceSpecialJackTexturePath = "res://art/cards/ocean_animated_deck/court/J/processed/frames/frame-001.png";
	private const string LeafySeadragonCollectAttackEffectFramePattern = "res://art/fx/player_leafy_seadragon_collect_attack/run-002/processed/fx-{0}.png";
	private const string LeafySeadragonBladeProjectileFramePattern = "res://art/fx/player_leafy_seadragon_leafy_mirage_slash/concept-001/processed/fx-{0}.png";
	private const int AdventureDamageEffectFrameCount = 4;
	private const int GardenEelAdventureDamageEffectFrameCount = 6;
	private const int DragonPrinceAdventureDamageEffectFrameCount = 12;
	private const int LeafySeadragonBladeProjectileFrameCount = 4;
	private const string FishDefeatFinisherFramePattern = "res://art/characters/player_fish_avatar/run-006/fp_fx_asset_preview/abu-fp-fx-asset-{0:00}.png";
	private const int FishDefeatFinisherFrameCount = 20;
	private const float FishDefeatFinisherSeconds = 1.12f;
	private static readonly Texture2D[] FishDefeatFinisherFrames =
		FrameAnimationHelpers.LoadTextureFrames(FishDefeatFinisherFramePattern, FishDefeatFinisherFrameCount, "fish defeat finisher");
	private static readonly Texture2D[] CardBackFrames =
	{
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-001.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-002.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-003.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-004.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-005.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-006.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-007.png"),
		GD.Load<Texture2D>("res://art/cards/ocean_animated_deck/back/processed/frames/frame-008.png"),
	};
	private static readonly Texture2D[] DefaultAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(AdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "adventure damage effect");
	private static readonly Texture2D[] CatAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(CatAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "cat adventure damage effect");
	private static readonly Texture2D[] FishAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(FishAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "fish adventure damage effect");
	private static readonly Texture2D[] LanternfishAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(LanternfishAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "lanternfish adventure damage effect");
	private static readonly Texture2D[] ShrimpAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(ShrimpAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "shrimp adventure damage effect");
	private static readonly Texture2D[] CrabAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(CrabAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "crab adventure damage effect");
	private static readonly Texture2D[] JellyfishAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(JellyfishAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "jellyfish adventure damage effect", preferImageFile: true);
	private static readonly Texture2D[] GardenEelAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(GardenEelAdventureDamageEffectFramePattern, GardenEelAdventureDamageEffectFrameCount, "garden eel adventure damage effect", preferImageFile: true);
	private static readonly Texture2D[] SeaSlugAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(SeaSlugAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "sea slug adventure damage effect", preferImageFile: true);
	private static readonly Texture2D[] NautilusAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(NautilusAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "nautilus adventure damage effect", preferImageFile: true);
	private static readonly Texture2D[] NarwhalAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(NarwhalAdventureDamageEffectFramePattern, AdventureDamageEffectFrameCount, "narwhal adventure damage effect", preferImageFile: true);
	private static readonly Texture2D[] DragonPrinceAdventureDamageEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(DragonPrinceAdventureDamageEffectFramePattern, DragonPrinceAdventureDamageEffectFrameCount, "dragon prince adventure damage effect", preferImageFile: true);
	private static readonly Texture2D[] LeafySeadragonCollectAttackEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(LeafySeadragonCollectAttackEffectFramePattern, AdventureDamageEffectFrameCount, "leafy seadragon collect attack effect", preferImageFile: true);
	private static readonly Texture2D[] LeafySeadragonBladeProjectileFrames =
		FrameAnimationHelpers.LoadTextureFrames(LeafySeadragonBladeProjectileFramePattern, LeafySeadragonBladeProjectileFrameCount, "leafy seadragon blade projectile", preferImageFile: true);
	private const string QuestionCardTransformFramePattern = "res://art/fx/question_card_transform/run-001/processed/fx-{0}.png";
	private const int QuestionCardTransformFrameCount = 6;
	private static readonly Texture2D[] QuestionCardTransformFrames =
		FrameAnimationHelpers.LoadTextureFrames(QuestionCardTransformFramePattern, QuestionCardTransformFrameCount, "question card transform");
	private const string SpecialCardTriggerEffectFramePattern = "res://art/fx/special_card_trigger/ocean_chrysanthemum_preview5/sea_anemone_rays/processed/fx-{0}.png";
	private const int SpecialCardTriggerEffectFrameCount = 4;
	private static readonly Texture2D[] SpecialCardTriggerEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(SpecialCardTriggerEffectFramePattern, SpecialCardTriggerEffectFrameCount, "special card trigger effect");
	private const string BombPairDestroyEffectFramePattern = "res://art/fx/bomb_pair_destroy/card_rip_burst/run-001/processed/fx-{0}.png";
	private const int BombPairDestroyEffectFrameCount = 4;
	private static readonly Texture2D[] BombPairDestroyEffectFrames =
		FrameAnimationHelpers.LoadTextureFrames(BombPairDestroyEffectFramePattern, BombPairDestroyEffectFrameCount, "bomb pair destroy effect");
	private const string LensRevealWispFramePattern = "res://art/fx/lens_reveal_sprites/run-001/processed/fx-{0}.png";
	private const int LensRevealWispFrameCount = 4;
	private static readonly Texture2D[] LensRevealWispFrames =
		FrameAnimationHelpers.LoadTextureFrames(LensRevealWispFramePattern, LensRevealWispFrameCount, "lens reveal wisp");
	private const string MagnetCardOverlayPath = "res://art/fx/magnet_card_overlay/run-001/processed/card-aspect.png";
	private const string DoubleCardOverlayPath = "res://art/fx/double_card_overlay/run-002/processed/card-aspect.png";
	private const string OvertimeCardOverlayPath = "res://art/fx/overtime_card_overlay/run-001/processed/card-aspect.png";
	private const string SlimeCardOverlayPath = "res://art/fx/slime_card_overlay/run-001/processed/card-aspect.png";
	private const string MagnetCountdownFramePattern = "res://art/fx/magnet_selection_rework/run-001/countdown_orb/processed/fx-{0}.png";
	private const int MagnetCountdownFrameCount = 4;
	private static readonly Texture2D[] MagnetCountdownFrames =
		FrameAnimationHelpers.LoadTextureFrames(MagnetCountdownFramePattern, MagnetCountdownFrameCount, "magnet countdown");
	private const string VictoryBannerTexturePath = "res://art/ui/victory_banner/run-001/processed/banner-transparent.png";
	private const string DefeatBannerTexturePath = "res://art/ui/defeat_banner/run-001/processed/banner-transparent.png";
	private const string QuestionMarkItemCardId = ItemCardCatalog.QuestionMarkItemCardId;
	private const string BombItemCardId = ItemCardCatalog.BombItemCardId;
	private const string MagnetItemCardId = ItemCardCatalog.MagnetItemCardId;
	private const string GreedItemCardId = ItemCardCatalog.GreedItemCardId;
	private const string LensItemCardId = ItemCardCatalog.LensItemCardId;
	private const string DoubleItemCardId = ItemCardCatalog.DoubleItemCardId;
	private const string OvertimeItemCardId = ItemCardCatalog.OvertimeItemCardId;
	private const string DrawCardItemCardId = ItemCardCatalog.DrawCardItemCardId;
	private const string ShuffleItemCardId = ItemCardCatalog.ShuffleItemCardId;
	private const string JackItemCardId = ItemCardCatalog.JackItemCardId;
	private const string QueenItemCardId = ItemCardCatalog.QueenItemCardId;
	private const string KingItemCardId = ItemCardCatalog.KingItemCardId;
	private const string FishAdventureEnemyAvatarId = "fish";
	private const string LanternfishAdventureEnemyAvatarId = "lanternfish";
	private const string ShrimpAdventureEnemyAvatarId = "shrimp";
	private const string CrabAdventureEnemyAvatarId = "crab";
	private const string JellyfishAdventureEnemyAvatarId = "jellyfish";
	private const string MantaRayAdventureEnemyAvatarId = "manta_ray";
	private const string GardenEelAdventureEnemyAvatarId = "garden_eel";
	private const string SeaSlugAdventureEnemyAvatarId = "sea_slug";
	private const string NautilusAdventureEnemyAvatarId = "nautilus";
	private const string NarwhalAdventureEnemyAvatarId = "narwhal";
	private const string LeafySeadragonAdventureEnemyAvatarId = "leafy_seadragon";
	private const string DragonPrinceAdventureEnemyAvatarId = "dragon_prince_j";
	private const string DragonQueenAdventureEnemyAvatarId = "dragon_queen_q";
	private const string BossKAdventureEnemyAvatarId = "boss_k";
	private const int CrabScoreReductionPerCard = 2;
	private const int GardenEelCapturesBeforeSandBite = 2;
	private const int NautilusCapturesBeforeBomb = 3;
	private const int NarwhalCapturesBeforeDouble = 2;
	private const int LeafySeadragonBladeBreakTableCardThreshold = 5;
	private const int LeafySeadragonBladeBreakCooldownTurns = 1;
	private const int SeaSlugMissesBeforeSlimeCoat = 2;
	private const int DragonRoyalCaptureThreshold = 5;
	private const string CardNamePrefix = "Card_";
	private const string CardBackAnimatedMeta = "ocean_card_back_animated";
	private const string HandCardSlotMeta = "ocean_hand_card_slot";
	private const int VisiblePileCards = 4;
	private const int StartingHandCards = 5;
	private const int MaxTotalHandCards = 8;
	private const int MainPlayerIndex = 0;
	private static readonly HashSet<string> ExcludedDeckRanks = new() { JackItemCardId, QueenItemCardId, KingItemCardId };
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
	private const float PairCaptureEdgeFloatSeconds = 0.18f;
	private const float PairCaptureEdgeFloatScale = 1.08f;
	private const float PairCaptureEdgeFloatRise = 8.0f;
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
	private static readonly Vector2 CardVisualSize = new(84, 112);
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
	private readonly List<Texture2D> _scoreFillEffectFrames = new();
	private readonly List<Texture2D> _enemySpecialAbilityAvatarFxFrames = new();
	private readonly List<Texture2D> _mantaRayTideShiftCycloneFxFrames = new();
	private readonly List<Texture2D> _leafySeadragonBladeProjectileFrames = new();
	private readonly List<Texture2D> _adventureDamageEffectFrames = new();
	private readonly List<Texture2D> _adventureEnemyDamageEffectFrames = new();
	private readonly List<Texture2D> _questionCardTransformFrames = new();
	private readonly List<Texture2D> _specialCardTriggerEffectFrames = new();
	private readonly List<Texture2D> _bombPairDestroyEffectFrames = new();
	private readonly List<Texture2D> _lensRevealWispFrames = new();
	private readonly List<Texture2D> _magnetCountdownFrameTextures = new();
	private readonly HashSet<string> _enabledSceneItemCardIds = new();
	private readonly Dictionary<string, string> _itemCardDescriptions = new();
	private readonly HashSet<string> _hiddenHandDisplaySlots = new();
	private readonly CardEventBus _cardEventBus = new();
	private SpecialCardEffectResolver _effectResolver = default!;
	private ItemCardCatalog _itemCardCatalog = default!;
	private readonly Queue<PendingItemAttachmentReward> _pendingItemAttachmentRewards = new();
	private Control? _selectionOverlay;
	private Texture2D? _cardBackTexture;
	private Texture2D? _magnetCardOverlayTexture;
	private Texture2D? _doubleCardOverlayTexture;
	private Texture2D? _overtimeCardOverlayTexture;
	private Texture2D? _slimeCardOverlayTexture;
	private Texture2D? _dragonPrinceSpecialJackTexture;
	private Control? _winnerBannerRoot;
	private Control? _winnerBannerPanel;
	private TextureRect? _winnerBannerArt;
	private Button? _winnerReplayButton;
	private Label _winnerLabel = default!;
	private CharacterAvatar? _resultAvatar;
	private Control? _defeatFinisherRoot;
	private TextureRect? _defeatFinisherTexture;
	private bool _showingDefeatResult;
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
	private TaskCompletionSource<int>? _pendingHandLimitDiscardCompletion;
	private Control? _magnetAttachmentCountdownRoot;
	private Label? _magnetAttachmentCountdownLabel;
	private Label? _magnetAttachmentCountdownHintLabel;
	private MagnetAttachmentCountdownRing? _magnetAttachmentCountdownRing;
	private Tween? _magnetAttachmentCountdownHintTween;
	private double _magnetAttachmentCountdownRemainingSeconds;
	private bool _magnetAttachmentCountdownHintIsUrgent;
	private bool _isResolvingMagnetAttachmentSelection;
	private bool _isAnimatingMagnetAttachment;
	private bool _isResolvingTurnSequence;
	private bool _isProcessingCardPlay;
	private bool _isOpeningDealAnimationPlaying;
	private bool _itemMode = true;
	private bool _soloMode;
	private bool _adventureMode;
	private string _adventureEnemyAvatarId = "fish";
	private bool _mantaRayTideShiftCheckedThisTurn;
	private bool _jellyfishBubbleTriggeredThisTurn;
	private int _jellyfishDisabledMainPlayerHandSlotIndex = -1;
	private int _gardenEelCaptureCountUntilSandBite;
	private bool _gardenEelSandBiteReady;
	private int _gardenEelBuriedTableCardIndex = -1;
	private int _nautilusMainPlayerCaptureCountUntilBomb;
	private int _narwhalCaptureCountUntilDouble;
	private bool _narwhalDoubleReady;
	private int _leafySeadragonBladeBreakCooldownTurns;
	private int _seaSlugMissCountUntilSlime;
	private bool _dragonPrinceJackUsed;
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
	public bool item_mode
	{
		get => _itemMode;
		set
		{
			_itemMode = value;
			if (IsInsideTree())
			{
				LoadItemCardEnabledStates();
				DealCardsToPlayers(playOpeningDealAnimation: true);
			}
		}
	}

	[Export(PropertyHint.Range, "50,200,50")]
	public int winning_score
	{
		get => _winningScore;
		set
		{
			_winningScore = value switch
			{
				50 or 100 or 200 => value,
				_ => DefaultWinningScore,
			};

			if (IsInsideTree())
			{
				RefreshScoreTooltips();
				CheckForWinner();
			}
		}
	}

	[Export]
	public bool adventure_mode
	{
		get => _adventureMode;
		set => _adventureMode = value;
	}

	[Export]
	public bool solo_mode
	{
		get => _soloMode;
		set => _soloMode = value;
	}

	[Export]
	public string adventure_enemy_avatar_id
	{
		get => _adventureEnemyAvatarId;
		set
		{
			string normalized = NormalizeAdventureEnemyAvatarId(value);
			if (_adventureEnemyAvatarId == normalized)
			{
				return;
			}

			_adventureEnemyAvatarId = normalized;
			if (IsInsideTree())
			{
				LoadAdventureEnemyDamageEffectTextures();
			}
		}
	}

	[Export]
	public bool debug_opponent_has_one_card = false;

	[Export]
	public bool debug_main_player_has_one_card = false;

	[Export]
	public bool debug_random_item_cards_are_magnet = false;

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
		_itemCardCatalog = ItemCardCatalog.Load(this);
		_effectResolver = new SpecialCardEffectResolver(this);
		LoadCardBackTextures();
		LoadScoreFillEffectTextures();
		LoadEnemySpecialAbilityAvatarFxTextures();
		LoadMantaRayTideShiftCycloneFxTextures();
		LoadLeafySeadragonBladeProjectileTextures();
		LoadAdventureDamageEffectTextures();
		LoadAdventureEnemyDamageEffectTextures();
		LoadItemCardTextures();
		LoadItemCardEnabledStates();
		LoadAttachmentCardOverlayTextures();
		LoadQuestionCardTransformTextures();
		LoadSpecialCardTriggerEffectTextures();
		LoadDragonPrinceSpecialJackTexture();
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
		CreateDefeatFinisherOverlay();
		UpdatePlayerVisibility();
		DealCardsToPlayers(playOpeningDealAnimation: true, playOpeningIntroAnimation: true);
	}

	public override void _Process(double delta)
	{
		UpdateMagnetAttachmentCountdown(delta);
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
