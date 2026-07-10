using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene : Control
{
	private const int MinPlayers = 2;
	private const int MaxPlayers = 4;
	private const string PokerCardsScenePath = "res://scenes/poker_cards_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const string PauseMenuScenePath = "res://scenes/pause_menu.tscn";
	private const string ResultBannerScenePath = "res://scenes/game_board_result_banner.tscn";
	private const string DefeatFinisherOverlayScenePath = "res://scenes/game_board_defeat_finisher_overlay.tscn";
	private const string ItemSelectionOverlayScenePath = "res://scenes/item_selection_overlay.tscn";
	private const string AdventureUpgradeSelectionOverlayScenePath = "res://scenes/adventure_upgrade_selection_overlay.tscn";
	private const string AdventureQuestionRejudgeOverlayScenePath = "res://scenes/adventure_question_rejudge_overlay.tscn";
	private const string StandardCardsRootPath = "StandardPokerCards";
	private const string SuitedJqkBackupCardsNodeName = "SuitedJqkBackupCards";
	private const float CardBackAnimationFps = 12.0f;
	private const string ScoreFillEffectFramePattern = "res://art/fx/score_fill_orbs/run-001/processed/fx-{0}.png";
	private const int ScoreFillEffectFrameCount = 4;
	private const string EnemySpecialAbilityAvatarFxFramePattern = "res://art/fx/enemy_special_ability_avatar/run-001/processed/fx-{0}.png";
	private const int EnemySpecialAbilityAvatarFxFrameCount = 6;
	private const string MantaRayTideShiftCycloneFxFramePattern = "res://art/fx/player_manta_ray_tide_shift_cyclone/run-003/processed/fx-{0}.png";
	private const int MantaRayTideShiftCycloneFxFrameCount = 8;
	private const string AdventureDamageEffectFramePattern = "res://art/fx/attack_bubble_burst/run-001/processed/fx-{0}.png";
	private const string CatAdventureDamageEffectFramePattern = "res://art/fx/player_cat_claw_attack/run-001/processed/fx-{0}.png";
	private const string LeafySeadragonBladeFlightFramePattern = "res://art/fx/player_leafy_seadragon_blade_projectile/run-001/flight/processed/fx-{0}.png";
	private const string LeafySeadragonBladeImpactFramePattern = "res://art/fx/player_leafy_seadragon_blade_projectile/run-001/impact/processed/fx-{0}.png";
	private const string BossKRoyalSeizureLanceFramePattern = "res://art/fx/boss_k_royal_seizure_lance/run-001/processed/fx-{0}.png";
	private const int AdventureDamageEffectFrameCount = 4;
	private const int LeafySeadragonBladeFlightFrameCount = 4;
	private const int LeafySeadragonBladeImpactFrameCount = 4;
	private const int BossKRoyalSeizureLanceFrameCount = 8;
	private const string CardBackFramePattern = "res://art/cards/ocean_animated_deck/back/processed/frames/frame-{0:000}.png";
	private const int CardBackFrameCount = 8;
	private static readonly Texture2D[] CardBackFrames =
		FrameAnimationHelpers.LoadTextureFrames(CardBackFramePattern, CardBackFrameCount, "card back");
	private const string QuestionCardTransformFramePattern = "res://art/fx/question_card_transform/run-001/processed/fx-{0}.png";
	private const int QuestionCardTransformFrameCount = 6;
	private const string SpecialCardTriggerEffectFramePattern = "res://art/fx/special_card_trigger/ocean_chrysanthemum_preview5/sea_anemone_rays/processed/fx-{0}.png";
	private const int SpecialCardTriggerEffectFrameCount = 4;
	private const string SpecialCardTriggerActivationFramePattern = "res://art/fx/special_card_trigger_activation/run-001/processed/fx-{0}.png";
	private const int SpecialCardTriggerActivationFrameCount = 6;
	private const string BombPairDestroyEffectFramePattern = "res://art/fx/bomb_pair_destroy/card_rip_burst/run-001/processed/fx-{0}.png";
	private const int BombPairDestroyEffectFrameCount = 4;
	private const string LensRevealWispFramePattern = "res://art/fx/lens_reveal_sprites/run-001/processed/fx-{0}.png";
	private const int LensRevealWispFrameCount = 4;
	private const string SlimeCardOverlayPath = "res://art/fx/slime_card_overlay/run-001/processed/card-aspect.png";
	private const string MagnetCountdownFramePattern = "res://art/fx/magnet_selection_rework/run-002/countdown_ring_normal/processed/fx-{0}.png";
	private const int MagnetCountdownFrameCount = 8;
	private const string TableShuffleVortexFramePattern = "res://art/fx/table_shuffle_vortex/run-001/processed/fx-{0}.png";
	private const int TableShuffleVortexFrameCount = 8;
	private const string MagnetAttachmentApplyRingFramePattern = "res://art/fx/magnet_attach_apply_ring/run-001/processed/fx-{0}.png";
	private const int MagnetAttachmentApplyRingFrameCount = 6;
	private const string AdventureTraceHuntMarkerFramePattern = "res://art/fx/adventure_trace_hunt_marker/run-004/processed/fx-{0}.png";
	private const int AdventureTraceHuntMarkerFrameCount = 4;
	private const string VictoryBannerTexturePath = "res://art/ui/result_text_prompt/run-001/processed/victory-text-prompt-effect.png";
	private const string DefeatBannerTexturePath = "res://art/ui/result_text_prompt/run-001/processed/defeat-text-prompt-effect.png";
		private const string QuestionMarkItemCardId = ItemCardCatalog.QuestionMarkItemCardId;
		private const string BombItemCardId = ItemCardCatalog.BombItemCardId;
		private const string MagnetItemCardId = ItemCardCatalog.MagnetItemCardId;
		private const string DoubleItemCardId = ItemCardCatalog.DoubleItemCardId;
		private const string CardNamePrefix = "Card_";
	private const string CardBackAnimatedMeta = "ocean_card_back_animated";
	private const string HandCardSlotMeta = "ocean_hand_card_slot";
	private const string TableRenderPooledTextureMeta = "ocean_table_render_texture";
	private const string TableRenderPooledPanelMeta = "ocean_table_render_panel";
	private const string CardCountLabelNodeName = "CardCountLabel";
	private const int VisiblePileCards = 4;
	private const int MaxAnimatedTableCardBacks = 2;
	private static readonly string[] PileCardNodeNames = { "PileCard_0", "PileCard_1", "PileCard_2", "PileCard_3" };
	private const int StartingHandCards = 5;
	private const int MaxTotalHandCards = 8;
	private const int MainPlayerIndex = 0;
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
	private const int MaxNautilusBombInsertionsPerMatch = 2;
	private const int MaxBossKBombInsertionsPerMatch = 1;
	private const int MaxBossKStealTriggersPerMatch = 3;
	private const int MaxBossKDoubleCollectTriggersPerMatch = 1;
	private const float TableCardPointPopupSeconds = 0.42f;
	private const float TableCardPointPopupRise = 24.0f;
	private const float TableCardPointPopupTopOffset = 26.0f;
	private const int TableCardPointPopupFontSize = 22;
	private const int TableCardPointPopupDoubleFontSize = 26;
	private const int TableCardPointPopupZIndex = 2800;
	private const float AvatarDamagePopupSeconds = 0.58f;
	private const float AvatarDamagePopupRise = 34.0f;
	private const float AvatarDamagePopupTopOffset = 16.0f;
	private const int AvatarDamagePopupFontSize = 28;
	private const int AvatarDamagePopupZIndex = 2900;
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
	private readonly GameBoardState _state = new();
	private readonly CardPlayStateService _cardPlayStateService = new();
	private readonly CaptureStateService _captureStateService = new();
	private readonly TurnAdvancementService _turnAdvancementService = new();
	private IPlayerInputHandler _playerInputHandler = default!;
	private int _playerCount
	{
		get => _state.PlayerCount;
		set => _state.PlayerCount = value;
	}
	private readonly RandomNumberGenerator _rng = new();
	private List<List<PlayingCard>> _playerDecks => _state.PlayerDecks;
	private List<List<PlayingCard>> _playerHands => _state.PlayerHands;
	private List<List<ItemCard>> _playerItemHands => _state.PlayerItemHands;
	private List<TableCard> _tableCards => _state.TableCards;
	private readonly List<Control> _playerCardPiles = new();
	private readonly List<Control> _playerHandAreas = new();
	private readonly Dictionary<string, Texture2D> _itemCardTextures = new();
	private readonly List<Texture2D> _scoreFillEffectFrames = new();
	private readonly List<Texture2D> _enemySpecialAbilityAvatarFxFrames = new();
	private readonly List<Texture2D> _mantaRayTideShiftCycloneFxFrames = new();
	private readonly List<Texture2D> _leafySeadragonBladeFlightFrames = new();
	private readonly List<Texture2D> _leafySeadragonBladeImpactFrames = new();
	private readonly List<Texture2D> _bossKRoyalSeizureLanceFrames = new();
	private readonly List<Texture2D> _adventureDamageEffectFrames = new();
	private readonly List<Texture2D> _adventureEnemyDamageEffectFrames = new();
	private readonly List<Texture2D> _dragonPrinceDamageHitFlightFrames = new();
	private readonly List<Texture2D> _dragonPrinceDamageHitImpactFrames = new();
	private readonly List<Texture2D> _questionCardTransformFrames = new();
	private readonly List<Texture2D> _specialCardTriggerEffectFrames = new();
	private readonly List<Texture2D> _specialCardTriggerActivationFrames = new();
	private readonly List<Texture2D> _bombPairDestroyEffectFrames = new();
	private readonly List<Texture2D> _lensRevealWispFrames = new();
	private readonly List<Texture2D> _magnetCountdownFrameTextures = new();
	private readonly List<Texture2D> _tableShuffleVortexFrames = new();
	private readonly List<Texture2D> _magnetAttachmentApplyRingFrames = new();
	private readonly List<Texture2D> _adventureTraceHuntMarkerFrames = new();
	private bool _commonGameplayFxResourcesLoaded;
	private bool _adventureModeResourcesLoaded;
	private bool _itemModeResourcesLoaded;
	private bool _itemModeOptionalFxResourcesLoaded;
	private readonly HashSet<string> _enabledSceneItemCardIds = new();
	private readonly Dictionary<string, string> _itemCardDescriptions = new();
	private readonly Dictionary<string, ItemAttachmentOverlay> _itemAttachmentOverlaysById = new();
	private readonly HashSet<string> _hiddenHandDisplaySlots = new();
	private readonly HashSet<string> _shiftedHiddenHandDisplaySlots = new();
	private readonly CardEventBus _cardEventBus = new();
	private readonly List<TextureRect> _tableRenderTexturePool = new();
	private readonly List<Panel> _tableRenderPanelPool = new();
	private IGameBoardAnimationPresenter _animationPresenter = default!;
	private SpecialCardEffectResolver _effectResolver = default!;
	private ItemCardCatalog _itemCardCatalog = default!;
	private Dictionary<string, AdventureAttackEffectHandler> _adventureAttackEffectHandlers = default!;
	private readonly Queue<PendingItemAttachmentReward> _pendingItemAttachmentRewards = new();
	private readonly List<SeatControlState> _networkSeatControlStates = new();
	private readonly Panel?[] _playerSeatControlIndicatorPanels = new Panel?[MaxPlayers];
	private readonly Label?[] _playerSeatControlIndicatorLabels = new Label?[MaxPlayers];
	private readonly SeatControlMode[] _playerSeatControlIndicatorModes = new SeatControlMode[MaxPlayers];
	private bool _seatControlIndicatorsDirty;
	private bool _seatControlIndicatorLayoutDirty;
	private bool _activePlayerTooltipContentDirty;
	private bool _activePlayerTooltipLayoutDirty;
	private NetworkAiBlockedItemState _networkAiBlockedItem = NetworkAiBlockedItemState.None;
	private AdventureRunSessionStore? _adventureRunSessionStore;
	private SelectionOverlaySession? _activeSelectionOverlaySession;
	private Panel? _networkReconnectOverlayPanel;
	private Label? _networkReconnectOverlayLabel;
	private Panel? _networkReconnectNoticePanel;
	private Label? _networkReconnectNoticeLabel;
	private Godot.Timer? _networkReconnectNoticeHideTimer;
	private bool _networkManagerSignalsConnected;
	private bool _networkReconnectCatchupPendingCompletion;
	private Texture2D? _cardBackTexture;
	private Texture2D? _slimeCardOverlayTexture;
	private Texture2D? _dragonPrinceSpecialJackTexture;
	private string _dragonPrinceSpecialJackRank = string.Empty;
	private GameBoardResultBanner? _winnerBannerRoot;
	private Control? _winnerBannerPanel;
	private TextureRect? _winnerBannerArt;
	private Button? _winnerReplayButton;
	private Label _winnerLabel = default!;
	private CharacterAvatar? _resultAvatar;
	private GameBoardDefeatFinisherOverlay? _defeatFinisherRoot;
	private TextureRect? _defeatFinisherTexture;
	private bool _showingDefeatResult;
	private bool _mainPlayerHandSelectionInputEnabled;
	private int _currentTurnPlayerIndex
	{
		get => _state.CurrentTurnPlayerIndex;
		set => _state.CurrentTurnPlayerIndex = value;
	}
	private int _turnSequenceGeneration
	{
		get => _state.TurnSequenceGeneration;
		set => _state.TurnSequenceGeneration = value;
	}
	private readonly TurnStateMachine _turnStateMachine = new();
	private int _completedTurnsSinceQuestionCardRefresh
	{
		get => _state.CompletedTurnsSinceQuestionCardRefresh;
		set => _state.CompletedTurnsSinceQuestionCardRefresh = value;
	}
	private int _questionCardRefreshAnchorPlayerIndex
	{
		get => _state.QuestionCardRefreshAnchorPlayerIndex;
		set => _state.QuestionCardRefreshAnchorPlayerIndex = value;
	}
	private int _adventureMainPlayerSuccessfulCapturesThisTurn
	{
		get => _state.AdventureMainPlayerSuccessfulCapturesThisTurn;
		set => _state.AdventureMainPlayerSuccessfulCapturesThisTurn = value;
	}
	private int _adventureMainPlayerCaptureCountThisBattle
	{
		get => _state.AdventureMainPlayerCaptureCountThisBattle;
		set => _state.AdventureMainPlayerCaptureCountThisBattle = value;
	}
	private bool _adventureMainPlayerQuestionRewardEchoActive
	{
		get => _state.AdventureMainPlayerQuestionRewardEchoActive;
		set => _state.AdventureMainPlayerQuestionRewardEchoActive = value;
	}
	private bool _adventureMainPlayerPendingExtraBaseCardAction
	{
		get => _state.AdventureMainPlayerPendingExtraBaseCardAction;
		set => _state.AdventureMainPlayerPendingExtraBaseCardAction = value;
	}
	private bool _adventureMainPlayerExtraBaseCardActionKeepsTurn
	{
		get => _state.AdventureMainPlayerExtraBaseCardActionKeepsTurn;
		set => _state.AdventureMainPlayerExtraBaseCardActionKeepsTurn = value;
	}
	private bool _adventureQuestionRejudgeUsedThisBattle
	{
		get => _state.AdventureQuestionRejudgeUsedThisBattle;
		set => _state.AdventureQuestionRejudgeUsedThisBattle = value;
	}
	private bool _adventureMainPlayerHalfHealthRescueUsed
	{
		get => _state.AdventureMainPlayerHalfHealthRescueUsed;
		set => _state.AdventureMainPlayerHalfHealthRescueUsed = value;
	}
	private bool _adventureMainPlayerQuarterHealthRescueUsed
	{
		get => _state.AdventureMainPlayerQuarterHealthRescueUsed;
		set => _state.AdventureMainPlayerQuarterHealthRescueUsed = value;
	}
	private bool _adventureEnemyNextBaseCardFacesUp
	{
		get => _state.AdventureEnemyNextBaseCardFacesUp;
		set => _state.AdventureEnemyNextBaseCardFacesUp = value;
	}
	private AdventureEnemyNextCardInsertMode _adventureEnemyNextCardInsertMode
	{
		get => _state.AdventureEnemyNextCardInsertMode;
		set => _state.AdventureEnemyNextCardInsertMode = value;
	}
	private int _adventureEnemyNextCaptureDamageDelta
	{
		get => _state.AdventureEnemyNextCaptureDamageDelta;
		set => _state.AdventureEnemyNextCaptureDamageDelta = value;
	}
	private int _revealedPlayerHandIndex
	{
		get => _state.RevealedPlayerHandIndex;
		set => _state.RevealedPlayerHandIndex = value;
	}
	private PendingItemSelectionMode _pendingItemSelectionMode
	{
		get => _state.PendingItemSelectionMode;
		set => _state.PendingItemSelectionMode = value;
	}
	private int _pendingItemOwnerIndex
	{
		get => _state.PendingItemOwnerIndex;
		set => _state.PendingItemOwnerIndex = value;
	}
	private int _pendingItemHandIndex
	{
		get => _state.PendingItemHandIndex;
		set => _state.PendingItemHandIndex = value;
	}
	private int _pendingItemTurnSequenceGeneration
	{
		get => _state.PendingItemTurnSequenceGeneration;
		set => _state.PendingItemTurnSequenceGeneration = value;
	}
	private string _pendingItemCardId
	{
		get => _state.PendingItemCardId;
		set => _state.PendingItemCardId = value;
	}
	private TaskCompletionSource<bool>? _pendingMagnetAttachmentCompletion;
	private TaskCompletionSource<int>? _pendingHandLimitDiscardCompletion;
	private Control? _magnetAttachmentCountdownRoot;
	private Label? _magnetAttachmentCountdownLabel;
	private Label? _magnetAttachmentCountdownHintLabel;
	private Tween? _magnetAttachmentCountdownHintTween;
	private double _magnetAttachmentCountdownRemainingSeconds;
	private bool _magnetAttachmentCountdownHintIsUrgent;
	private bool _isResolvingMagnetAttachmentSelection
	{
		get => _state.IsResolvingMagnetAttachmentSelection;
		set => _state.IsResolvingMagnetAttachmentSelection = value;
	}
	private bool _isAnimatingMagnetAttachment
	{
		get => _state.IsAnimatingMagnetAttachment;
		set => _state.IsAnimatingMagnetAttachment = value;
	}
	private bool _isResolvingTurnSequence
	{
		get => _state.IsResolvingTurnSequence;
		set => _state.IsResolvingTurnSequence = value;
	}
	private bool _isProcessingCardPlay
	{
		get => _state.IsProcessingCardPlay;
		set => _state.IsProcessingCardPlay = value;
	}
	private bool _isOpeningDealAnimationPlaying
	{
		get => _state.IsOpeningDealAnimationPlaying;
		set => _state.IsOpeningDealAnimationPlaying = value;
	}
	private bool _isLeavingGame
	{
		get => _state.IsLeavingGame;
		set => _state.IsLeavingGame = value;
	}
	private bool _itemMode = true;
	private bool _soloMode;
	private int _soloAiDifficulty = (int)SoloAiRules.Difficulty.Medium;
	private int _localPlayerIndex;
	private string _networkSessionId = string.Empty;
	private long _matchSeed;
	private string _playerNamesCsv = string.Empty;
	private string _playerAvatarIdsCsv = string.Empty;
	private bool _networkWaitForInitialize;
	private bool _networkBoardInitialized;
	private string _networkMatchId = string.Empty;
	private bool _adventureMode;
	private bool _adventureTutorialMode;
	private string _adventureTutorialFlow = "basic_capture";
	private string _adventureEnemyAvatarId = "fish";
	private bool _mantaRayTideShiftCheckedThisTurn;
	private bool _jellyfishBubbleTriggeredThisTurn;
	private int _jellyfishDisabledMainPlayerHandSlotIndex = -1;
	private int _gardenEelCaptureCountUntilSandBite;
	private bool _gardenEelSandBiteReady;
	private int _gardenEelBuriedTableCardIndex = -1;
	private int _nautilusMainPlayerCaptureCountUntilBomb;
	private int _nautilusBombInsertionsThisMatch;
	private int _narwhalCaptureCountUntilDouble;
	private bool _narwhalDoubleReady;
	private int _bossKDoubleCollectTriggersThisMatch;
	private int _leafySeadragonBladeBreakCooldownTurns;
	private int _seaSlugMissCountUntilSlime;
	private bool _dragonPrinceJackUsed;
	private int _dragonQueenCaptureCountUntilSteal;
	private int _bossKCaptureCountUntilSteal;
	private int _bossKStealTriggersThisMatch;
	private bool _bossKActiveControlUsedThisTurn;
	private readonly List<int> _openingVisibleHandCardCounts = new();
	private readonly List<int> _openingVisiblePileCardCounts = new();
	private readonly HashSet<int> _cardPlayPendingFaceUpMagnetTriggerIndexes = new();
	private readonly HashSet<int> _cardPlayResolvedMagnetTriggerIndexes = new();
	private readonly HashSet<int> _cardPlayMagnetExtraCaptureIndexes = new();
	private readonly HashSet<int> _cardPlayResolvedDoubleTriggerIndexes = new();
	private readonly HashSet<int> _cardPlayResolvedOvertimeTriggerIndexes = new();
		private readonly HashSet<CardTriggerResolutionKey> _resolvedCardTriggerKeys = new();
		private readonly List<TableSpecialCardTrigger> _capturedSpecialCardTriggers = new();
		private readonly List<int> _tableShuffleIndexes = new();
		private readonly List<TableCard> _tableShuffleMovableCards = new();
		private int _winnerPlayerIndex
		{
			get => _state.WinnerPlayerIndex;
			set => _state.WinnerPlayerIndex = value;
		}

	private Control _centerArena = default!;
	private Control _tableCardLayoutArea = default!;
	private Control _topPlayerArea = default!;
	private Control _bottomPlayerArea = default!;
	private Control _leftPlayerArea = default!;
	private Control _rightPlayerArea = default!;
	private Control _questionCardTransformArea = default!;
	private TextureRect _ultraWideBackdrop = default!;
	private TextureRect _verticalBackdrop = default!;
	private TextureRect _boardArt = default!;
	private Control _layoutAreas = default!;

	public override void _Ready()
	{
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		ApplySavedLanguagePreference();
		_rng.Randomize();
		_animationPresenter = new GameBoardAnimationPresenter();
		_itemCardCatalog = ItemCardCatalog.Load(this);
		_effectResolver = new SpecialCardEffectResolver(this);
		_itemPlayPresentationHandlers = CreateItemPlayPresentationHandlers();
		_itemPlayPresentationValidators = CreateItemPlayPresentationValidators();
		LoadAdventureUpgradeCatalog();
		_adventureAttackEffectHandlers = CreateAdventureAttackEffectHandlers();
		LoadCardBackTextures();
		LoadItemCardEnabledStates();
		if (item_mode)
		{
			EnsureItemModeResourcesLoaded();
		}

		_ultraWideBackdrop = GetNode<TextureRect>("UltraWideBackdrop");
		_verticalBackdrop = GetNode<TextureRect>("VerticalBackdrop");
		_boardArt = GetNode<TextureRect>("BoardArt");
		_layoutAreas = GetNode<Control>("LayoutAreas");
		RefreshResponsiveBoardLayout();
		_centerArena = GetNode<Control>("%CenterArena");
		_tableCardLayoutArea = GetNode<Control>("%TableCardLayoutArea");
		_topPlayerArea = GetNode<Control>("%TopPlayerArea");
		_bottomPlayerArea = GetNode<Control>("%BottomPlayerArea");
		_leftPlayerArea = GetNode<Control>("%LeftPlayerArea");
		_rightPlayerArea = GetNode<Control>("%RightPlayerArea");

		CreatePlayerStatusTooltip();
		CreatePlayerCardPiles();
		CreatePlayerHandAreas();
		_playerInputHandler = CreatePlayerInputHandler();
		BindPlayerAvatarAreas();
		CreatePlayerAvatars();
		BindQuestionCardTransformArea();
		BindPlayerScoreMeters();
		CreatePlayerSeatControlIndicators();
		CreateWinnerLabel();
		CreateDefeatFinisherOverlay();
		CreateNetworkReconnectOverlay();
		CreateDiceRollOverlay();
		ConnectNetworkManagerSignalsForBoard();
		RefreshNetworkReconnectOverlayIfNeeded();
		UpdatePlayerVisibility();
		if (_networkWaitForInitialize)
		{
			GetNetworkManager()?.NotifyLocalBoardReady();
		}
		else
		{
			InitializeBoard(playOpeningIntroAnimation: true);
		}
		PlayBoardMusic();
		_ = WarmupGameplayResourcesAsync(GetCurrentTurnContext());
	}

	public void ApplyNetworkBoardInitialize(Godot.Collections.Dictionary payload)
	{
		player_count = ReadNetworkInt(payload, "player_count", player_count);
		item_mode = ReadNetworkBool(payload, "item_mode", item_mode);
		winning_score = ReadNetworkInt(payload, "winning_score", winning_score);
		network_session_id = ReadNetworkString(payload, "session_id", network_session_id);
		match_seed = ReadNetworkLong(payload, "match_seed", match_seed);
		player_names_csv = ReadNetworkString(payload, "player_names_csv", player_names_csv);
		player_avatar_ids_csv = ReadNetworkString(payload, "player_avatar_ids_csv", player_avatar_ids_csv);
		network_match_id = ReadNetworkString(payload, "match_id", network_match_id);
		network_wait_for_initialize = false;
		RefreshNetworkReconnectOverlayIfNeeded();
		if (IsNetworkMultiplayerMode() && !IsNetworkHost())
		{
			InitializeNetworkClientBoardShell();
			return;
		}

		InitializeBoard(playOpeningIntroAnimation: true);
		if (IsNetworkHost())
		{
			GetNetworkManager()?.SendInitialBoardSnapshotsFromHost(this);
		}
	}

	private void InitializeBoard(bool playOpeningIntroAnimation)
	{
		if (_networkBoardInitialized)
		{
			return;
		}

		_networkBoardInitialized = true;
		ApplyMatchSeed();
		InitializeAdventureUpgradeRunForCurrentBoard();
		UpdatePlayerVisibility();
		DealCardsToPlayers(playOpeningDealAnimation: true, playOpeningIntroAnimation: playOpeningIntroAnimation);
	}

	private void ApplyMatchSeed()
	{
		if (_matchSeed == 0)
		{
			return;
		}

		_rng.Seed = unchecked((ulong)_matchSeed);
	}

	public override void _ExitTree()
	{
		DisconnectNetworkManagerSignalsForBoard();
		DisposeActiveSelectionOverlay();
		ClearDetachedVisualPools();
		_animationPresenter?.ClearPools();
	}

	private AdventureRunSessionStore? GetAdventureRunSessionStore()
	{
		if (_adventureRunSessionStore != null && GodotObject.IsInstanceValid(_adventureRunSessionStore))
		{
			return _adventureRunSessionStore;
		}

		_adventureRunSessionStore = GetNodeOrNull<AdventureRunSessionStore>("/root/AdventureRunSessionStore");
		return _adventureRunSessionStore;
	}

	private void ClearDetachedVisualPools()
	{
		FreeDetachedNodes(_pooledHandCardSlots);
		FreeDetachedNodes(_tableRenderTexturePool);
		FreeDetachedNodes(_tableRenderPanelPool);
	}

	private static void FreeDetachedNodes<T>(ICollection<T> nodes) where T : Node
	{
		foreach (T node in new List<T>(nodes))
		{
			if (!GodotObject.IsInstanceValid(node))
			{
				continue;
			}

			node.GetParent()?.RemoveChild(node);
			node.Free();
		}

		nodes.Clear();
	}

	public override void _Process(double delta)
	{
		UpdateMagnetAttachmentCountdown(delta);
		UpdateAdventureUpgradeOverlayCardMotion(delta);
		FlushUiDirtyFlagsIfNeeded();
	}

	private void MarkSeatControlIndicatorsDirty()
	{
		_seatControlIndicatorsDirty = true;
	}

	private void MarkSeatControlIndicatorLayoutDirty()
	{
		_seatControlIndicatorLayoutDirty = true;
	}

	private void MarkActivePlayerStatusTooltipContentDirty()
	{
		_activePlayerTooltipContentDirty = true;
	}

	private void MarkActivePlayerStatusTooltipLayoutDirty()
	{
		_activePlayerTooltipLayoutDirty = true;
	}

	private void FlushUiDirtyFlagsIfNeeded()
	{
		if (_seatControlIndicatorsDirty)
		{
			RefreshAllPlayerSeatControlIndicators();
			_seatControlIndicatorsDirty = false;
		}

		if (_seatControlIndicatorLayoutDirty)
		{
			UpdateAllPlayerSeatControlIndicatorLayouts();
			_seatControlIndicatorLayoutDirty = false;
		}

		if (_activeStatusTooltipPlayerIndex < 0)
		{
			return;
		}

		if (_activePlayerTooltipContentDirty)
		{
			RefreshPlayerStatusTooltip(_activeStatusTooltipPlayerIndex);
			_activePlayerTooltipContentDirty = false;
		}

		if (_activePlayerTooltipLayoutDirty)
		{
			UpdatePlayerStatusTooltipLayout();
			_activePlayerTooltipLayoutDirty = false;
		}
	}

}
