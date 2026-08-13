using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene : Control
{
	private const int MinPlayers = 2;
	private const int MaxPlayers = 4;
	private const string PokerCardsScenePath = "res://scenes/poker_cards_scene.tscn";
	private const string CharacterAvatarScenePath = "res://scenes/character_avatar.tscn";
	private const string ResultBannerScenePath = "res://scenes/game_board_result_banner.tscn";
	private const string DefeatFinisherOverlayScenePath = "res://scenes/game_board_defeat_finisher_overlay.tscn";
	private static PackedScene? _cachedCharacterAvatarScene;
	private static PackedScene? _cachedResultBannerScene;
	private static PackedScene? _cachedDefeatFinisherOverlayScene;
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
	private const int QuestionCardUncollectedLifetimeTurns = 2;
	private const double MagnetAttachmentSelectionSeconds = 5.0;
	private const int LensRevealMaxCards = 2;

	private static readonly Vector2 CardPileSize = new(124, 128);
	private static readonly Vector2 CardVisualSize = new(84, 112);
	private static readonly Vector2 CardPileOffset = new(3, -2);
	private const int TableCardsPerRow = 7;
	private static readonly float TableCardHorizontalStep = CardVisualSize.X + 18.0f;
	private static readonly float TableCardRowGap = CardVisualSize.Y * 0.62f;
	private static readonly Vector2 HorizontalHandCardOffset = new(62, 0);
			private AuthorityRuleSession? _authorityRuleSession;
			private ReplicaRuleSession? _replicaRuleSession;
	private readonly GameBoardRuleReadProjection _ruleReadProjection = new();
		private readonly GodotRuleRandomSource _ruleRandomSource;
	private IPlayerInputHandler _playerInputHandler = default!;
		private int _playerCount = MinPlayers;
			private readonly RandomNumberGenerator _rng = new();
		private readonly RandomNumberGenerator _presentationRng = new();
	private NetworkReplicaPresentationState? _networkReplicaPresentationState;
	private IReadOnlyList<IReadOnlyList<PlayingCard>> _playerDecks =>
		_networkReplicaPresentationState is { } presentation
			? presentation.PlayerDecks
			: CurrentRuleProjection.PlayerDecks;
	private IReadOnlyList<IReadOnlyList<PlayingCard>> _playerHands =>
		_networkReplicaPresentationState is { } presentation
			? presentation.PlayerHands
			: CurrentRuleProjection.PlayerHands;
	private IReadOnlyList<IReadOnlyList<ItemCard>> _playerItemHands =>
		_networkReplicaPresentationState is { } presentation
			? presentation.PlayerItemHands
			: CurrentRuleProjection.PlayerItemHands;
	private IReadOnlyList<TableCard> _tableCards =>
		_networkReplicaPresentationState is { } presentation
			? presentation.TableCards
			: CurrentRuleProjection.TableCards;
	private readonly List<Control> _playerCardPiles = new();
	private readonly List<Control> _playerHandAreas = new();
	private readonly Dictionary<string, Texture2D> _itemCardTextures = new();
	private readonly Dictionary<string, Texture2D> _playingCardTexturesByCode = new(StringComparer.Ordinal);
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
	private readonly HashSet<string> _pendingQuestionRewardItemHandSlots = new();
	private readonly List<TextureRect> _tableRenderTexturePool = new();
	private readonly List<Panel> _tableRenderPanelPool = new();
	private readonly HashSet<long> _presentationHiddenTableCardRuntimeInstanceIds = new();
	private IGameBoardAnimationPresenter _animationPresenter = default!;
	private SpecialCardEffectResolver _effectResolver = default!;
	private ItemCardCatalog _itemCardCatalog = default!;
	private Dictionary<string, AdventureAttackEffectHandler> _adventureAttackEffectHandlers = default!;
	private readonly List<SeatControlState> _networkSeatControlStates = new();
	private readonly Panel?[] _playerSeatControlIndicatorPanels = new Panel?[MaxPlayers];
	private readonly Label?[] _playerSeatControlIndicatorLabels = new Label?[MaxPlayers];
	private readonly SeatControlMode[] _playerSeatControlIndicatorModes = new SeatControlMode[MaxPlayers];
	private bool _seatControlIndicatorsDirty;
	private bool _seatControlIndicatorLayoutDirty;
	private bool _activePlayerTooltipContentDirty;
	private bool _activePlayerTooltipLayoutDirty;
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
		get => CurrentRuleReadModel?.CurrentTurnSeat ?? MainPlayerIndex;
	}
	private int _turnSequenceGeneration
	{
		get => _replicaTurnSequenceGenerationOverride >= 0
			? _replicaTurnSequenceGenerationOverride
			: CurrentRuleReadModel?.TurnGeneration ?? 0;
	}
	private int _replicaTurnSequenceGenerationOverride = -1;
	private int _completedTurnsSinceQuestionCardRefresh
	{
		get => CurrentRuleReadModel?.CompletedTurnsSinceQuestionRefresh ?? 0;
	}
	private int _questionCardRefreshAnchorPlayerIndex
	{
		get => CurrentRuleReadModel?.QuestionRefreshAnchorSeat ?? -1;
	}
		private long _adventureNextBaseCardActionId = 1;
	private MandatorySelectionState CurrentMandatorySelection =>
		CurrentRuleReadModel?.NormalizedMandatorySelection ?? MandatorySelectionState.None;
	private PendingItemSelectionMode _pendingItemSelectionMode =>
		CurrentMandatorySelection.Kind switch
		{
			MandatorySelectionKind.HandLimitDiscard => PendingItemSelectionMode.HandLimitDiscard,
			MandatorySelectionKind.AttachmentHandCard => PendingItemSelectionMode.AttachmentRewardHandCard,
			_ => PendingItemSelectionMode.None,
		};
	private int _pendingItemOwnerIndex =>
		CurrentMandatorySelection.Kind == MandatorySelectionKind.None
			? -1
			: CurrentMandatorySelection.Seat;
	private int _pendingItemHandIndex =>
		CurrentMandatorySelection.Kind == MandatorySelectionKind.None
			? -1
			: CurrentMandatorySelection.SourceHandIndex;
	private int _pendingItemTurnSequenceGeneration =>
		CurrentMandatorySelection.Kind == MandatorySelectionKind.None
			? -1
			: CurrentMandatorySelection.TurnGeneration;
	private string _pendingItemCardId =>
		CurrentMandatorySelection.Kind == MandatorySelectionKind.None
			? string.Empty
			: CurrentMandatorySelection.ItemId;
	private TaskCompletionSource<bool>? _pendingMagnetAttachmentCompletion;
	private TaskCompletionSource<int>? _pendingHandLimitDiscardCompletion;
	private readonly GameBoardFrameScheduler _frameScheduler = new();
	private readonly GameBoardLifecycleCoordinator _lifecycleCoordinator = new();
	private MandatoryActionCountdownController? _mandatoryActionCountdownController;
	private IDisposable? _mandatoryActionCountdownFrameRegistration;
	private bool _debugMandatoryActionCountdownProbeActive;
	private bool _isResolvingMagnetAttachmentSelection;
	private bool _isAnimatingMagnetAttachment;
	private bool _isResolvingTurnSequence;
	private bool _isProcessingCardPlay;
	private bool _isOpeningDealAnimationPlaying;
	private bool _isLeavingGame;
	private bool _itemMode = true;
	private bool _soloMode;
	private GameModePolicy _gameModePolicy = GameModePolicy.LocalStandard;
	private int _soloAiDifficulty = (int)SoloAiDifficulty.Medium;
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
	private readonly List<int> _openingVisibleHandCardCounts = new();
	private readonly List<int> _openingVisiblePileCardCounts = new();
	private int _winnerPlayerIndex => CurrentRuleReadModel?.WinnerSeat ?? -1;
	private MatchOutcomeKind _matchOutcomeKind => CurrentRuleReadModel?.Outcome switch
	{
		Deckland.Rules.MatchOutcome.Won => MatchOutcomeKind.Winner,
		Deckland.Rules.MatchOutcome.Draw => MatchOutcomeKind.Draw,
		_ => MatchOutcomeKind.InProgress,
	};

	private Control _centerArena = default!;
	private Control _tableCardLayoutArea = default!;
	private Control _topPlayerArea = default!;
	private Control _bottomPlayerArea = default!;
	private Control _leftPlayerArea = default!;
	private Control _rightPlayerArea = default!;
	private Control _questionCardTransformArea = default!;
	private TextureRect _backdropFill = default!;
	private Control _boardCanvas = default!;
	private TextureRect _verticalBackdrop = default!;
	private TextureRect _boardArt = default!;
	private Control _layoutAreas = default!;
	private GameBoardNodeBindings _nodeBindings = default!;

	public GameModePolicy ModePolicy => _gameModePolicy;

	public GameBoardScene()
	{
		_ruleRandomSource = new GodotRuleRandomSource(_rng);
	}

	private static PackedScene? LoadCachedCoreScene(ref PackedScene? cachedScene, string path)
	{
		if (cachedScene != null && GodotObject.IsInstanceValid(cachedScene))
		{
			return cachedScene;
		}

		cachedScene = GD.Load<PackedScene>(path);
		return cachedScene;
	}

	public override void _Ready()
	{
		_lifecycleCoordinator.Start(
			CreateRuleSessionAndFoundation,
			BindBoardNodes,
			CreateBoardControllers,
			RegisterBoardNetworkEndpoint,
			StartBoardMatch);
	}

	private void CreateRuleSessionAndFoundation()
	{
		_boardAsyncLifetime = new BoardAsyncLifetime(this);
		_gameModePolicy = GameModePolicy.Derive(
			!string.IsNullOrWhiteSpace(_networkSessionId),
			_adventureMode,
			_adventureTutorialMode,
			_soloMode);
		InitializeRuleSessionForCurrentMode();
		GetAdventureRunSessionStore()?.ConfigureForPolicy(_gameModePolicy);
		GameInputActions.EnsureRegistered();
		Localization.EnsureLoaded();
		ApplySavedLanguagePreference();
		LocalizationTypography.Apply(this, useLocaleLayoutDirection: false);
		_rng.Randomize();
		_presentationRng.Randomize();
		_animationPresenter = new GameBoardAnimationPresenter();
		_itemCardCatalog = ItemCardCatalog.LoadRequired(this);
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
	}

	private void BindBoardNodes()
	{
		_nodeBindings = GameBoardNodeBindings.BindRequired(this);
		_backdropFill = _nodeBindings.BackdropFill;
		_boardCanvas = _nodeBindings.BoardCanvas;
		_verticalBackdrop = _nodeBindings.VerticalBackdrop;
		_boardArt = _nodeBindings.BoardArt;
		_layoutAreas = _nodeBindings.LayoutAreas;
		RefreshResponsiveBoardLayout();
		_centerArena = _nodeBindings.CenterArena;
		_tableCardLayoutArea = _nodeBindings.TableCardLayoutArea;
		_topPlayerArea = _nodeBindings.TopPlayerArea;
		_bottomPlayerArea = _nodeBindings.BottomPlayerArea;
		_leftPlayerArea = _nodeBindings.LeftPlayerArea;
		_rightPlayerArea = _nodeBindings.RightPlayerArea;
	}

	private void CreateBoardControllers()
	{
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
	}

	private void RegisterBoardNetworkEndpoint()
	{
		RegisterNetworkMatchEndpoint();
	}

	private void StartBoardMatch()
	{
		if (_networkWaitForInitialize)
		{
			GetNetworkManager()?.NotifyLocalBoardReady();
		}
		else
		{
			InitializeBoard(playOpeningIntroAnimation: true);
		}
		PlayBoardMusic();
		TrackBoardTask(WarmupGameplayResourcesAsync(GetCurrentTurnContext()), nameof(WarmupGameplayResourcesAsync));
		RefreshBoardProcessState();
	}

	internal bool TryApplyNetworkBoardInitialize(NetworkBoardInitialization initialization)
	{
		NetworkSessionConfig config = initialization.Config;
		if (config.PlayerCount is < NetworkSessionDefaults.MinPlayers or > NetworkSessionDefaults.MaxPlayers
			|| config.WinningScore is not (50 or 100 or 200)
			|| string.IsNullOrWhiteSpace(config.SessionId)
			|| string.IsNullOrWhiteSpace(config.MatchId)
			|| initialization.PlayerNames.Length != config.PlayerCount
			|| initialization.PlayerAvatarIds.Length != config.PlayerCount
			|| initialization.LocalPlayerIndex < 0
			|| initialization.LocalPlayerIndex >= config.PlayerCount)
		{
			return false;
		}

		player_count = config.PlayerCount;
		item_mode = config.ItemMode;
		winning_score = config.WinningScore;
		network_session_id = config.SessionId;
		network_match_id = config.MatchId;
		match_seed = config.MatchSeed;
		local_player_index = initialization.LocalPlayerIndex;
		player_names_csv = string.Join(',', initialization.PlayerNames);
		player_avatar_ids_csv = string.Join(',', initialization.PlayerAvatarIds);
		network_wait_for_initialize = false;
		RefreshNetworkReconnectOverlayIfNeeded();
		if (IsNetworkMultiplayerMode() && !IsNetworkHost())
		{
			InitializeNetworkClientBoardShell();
			return true;
		}

		InitializeBoard(playOpeningIntroAnimation: true);
		if (IsNetworkHost())
		{
			GetNetworkManager()?.SendInitialBoardSnapshotsFromHost();
		}
		return true;
	}

	private void InitializeBoard(bool playOpeningIntroAnimation)
	{
		TrackBoardTask(InitializeBoardAsync(playOpeningIntroAnimation), nameof(InitializeBoardAsync));
	}

	private async Task InitializeBoardAsync(bool playOpeningIntroAnimation)
	{
		if (_networkBoardInitialized)
		{
			return;
		}

		_networkBoardInitialized = true;
		ApplyMatchSeed();
		if (!await InitializeAdventureUpgradeRunForCurrentBoardAsync()
			|| !await PrepareAdventureBattleForDealAsync())
		{
			return;
		}
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
		_lifecycleCoordinator.Stop(
			StopBoardInputAndNetworkRequests,
			StopAndUnregisterNetworkMatchEndpoint,
			CancelBoardAsyncLifetime,
			ReleaseBoardControllers,
			ClearBoardPresentationState);
	}

	private void StopBoardInputAndNetworkRequests()
	{
		_isLeavingGame = true;
		ClearMagnetAttachmentCountdown();
		_networkVisualPlaybackController?.Shutdown();
	}

	private void CancelBoardAsyncLifetime()
	{
		_boardAsyncLifetime?.BeginStopping();
		_boardAsyncLifetime?.Shutdown();
	}

	private void ReleaseBoardControllers()
	{
		DisconnectNetworkManagerSignalsForBoard();
		DisposeActiveSelectionOverlay();
		_frameScheduler.Dispose();
	}

	private void ClearBoardPresentationState()
	{
		if (_authorityRuleSession != null)
		{
			GetAdventureRunSessionStore()?.UnbindAuthorityRuleSession(_authorityRuleSession);
		}
		DiscardNetworkReplicaPresentationState();
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
		_frameScheduler.Tick(delta);
		FlushResponsiveBoardVisualsIfNeeded();
		FlushUiDirtyFlagsIfNeeded();
		RefreshBoardProcessState();
	}

	private void RequestBoardFrameUpdate()
	{
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		if (!IsProcessing())
		{
			SetProcess(true);
		}
	}

	private void RefreshBoardProcessState()
	{
		if (!GodotObject.IsInstanceValid(this))
		{
			return;
		}

		bool hasTooltipWork = _activeStatusTooltipPlayerIndex >= 0
			&& (_activePlayerTooltipContentDirty || _activePlayerTooltipLayoutDirty);
		bool shouldProcess = _frameScheduler.HasActiveRegistrations
			|| _responsiveBoardVisualsDirty
			|| _seatControlIndicatorsDirty
			|| _seatControlIndicatorLayoutDirty
			|| hasTooltipWork;
		if (IsProcessing() != shouldProcess)
		{
			SetProcess(shouldProcess);
		}
	}

	public bool DebugIsBoardFrameProcessing()
	{
		return IsProcessing();
	}

	private void MarkSeatControlIndicatorsDirty()
	{
		_seatControlIndicatorsDirty = true;
		RequestBoardFrameUpdate();
	}

	private void MarkSeatControlIndicatorLayoutDirty()
	{
		_seatControlIndicatorLayoutDirty = true;
		RequestBoardFrameUpdate();
	}

	private void MarkActivePlayerStatusTooltipContentDirty()
	{
		_activePlayerTooltipContentDirty = true;
		RequestBoardFrameUpdate();
	}

	private void MarkActivePlayerStatusTooltipLayoutDirty()
	{
		_activePlayerTooltipLayoutDirty = true;
		RequestBoardFrameUpdate();
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
