using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private enum AdventureUpgradeSelectionOutcome
	{
		Confirmed = 0,
		Canceled = 1,
		DisplayFailed = 2,
	}

	private readonly record struct AdventureUpgradeSelectionResult(
		AdventureUpgradeSelectionOutcome Outcome,
		int SelectedIndex);

	private readonly record struct AdventureEnemyNextBaseCardModifiers(
		bool FacesUp,
		int TableInsertIndex,
		int CaptureDamageDelta);

	private readonly record struct AdventureUpgradeOverlayOptionModel(
		string Suit,
		string Rarity,
		string Description);

	private sealed class AdventureUpgradeOverlayCardMotionState
	{
		internal AdventureUpgradeOverlayCardMotionState(
			Button button,
			Control cardFrame,
			AdventureUpgradeChoiceArt choiceArt,
			Color baseModulate)
		{
			Button = button;
			CardFrame = cardFrame;
			ChoiceArt = choiceArt;
			BaseModulate = baseModulate;
			BasePosition = Vector2.Zero;
			BaseScale = Vector2.One * AdventureUpgradeOverlayRestScale;
			BaseRotationDegrees = 0.0f;
			StartPosition = BasePosition;
			StartScale = BaseScale;
			StartRotationDegrees = BaseRotationDegrees;
			TargetPosition = BasePosition;
			TargetScale = BaseScale;
			TargetRotationDegrees = BaseRotationDegrees;
		}

		internal Button Button { get; }
		internal Control CardFrame { get; }
		internal AdventureUpgradeChoiceArt ChoiceArt { get; }
		internal Color BaseModulate { get; }
		internal bool PointerHovered { get; set; }
		internal Vector2 BasePosition { get; set; }
		internal Vector2 BaseScale { get; set; }
		internal float BaseRotationDegrees { get; set; }
		internal Vector2 StartPosition { get; set; }
		internal Vector2 StartScale { get; set; }
		internal float StartRotationDegrees { get; set; }
		internal float RevealDelaySeconds { get; set; }
		internal float RevealElapsedSeconds { get; set; }
		internal bool RevealCompleted { get; set; }
		internal bool InteractionUnlocked { get; set; }
		internal Vector2 TargetPosition { get; set; }
		internal Vector2 TargetScale { get; set; }
		internal float TargetRotationDegrees { get; set; }
		internal float TargetHighlightAmount { get; set; }
	}

	private sealed class AdventureUpgradeOverlayRevealState
	{
		internal AdventureUpgradeOverlayRevealState(
			Control root,
			ColorRect backdrop,
			Panel selectionPanel,
			TextureRect selectionHalo,
			int visibleCardCount)
		{
			Root = root;
			Backdrop = backdrop;
			SelectionPanel = selectionPanel;
			SelectionHalo = selectionHalo;
			VisibleCardCount = visibleCardCount;
			BackdropBaseModulate = backdrop.Modulate;
			SelectionPanelBaseModulate = selectionPanel.Modulate;
			SelectionHaloBaseModulate = selectionHalo.Modulate;
		}

		internal Control Root { get; }
		internal ColorRect Backdrop { get; }
		internal Panel SelectionPanel { get; }
		internal TextureRect SelectionHalo { get; }
		internal int VisibleCardCount { get; }
		internal Color BackdropBaseModulate { get; }
		internal Color SelectionPanelBaseModulate { get; }
		internal Color SelectionHaloBaseModulate { get; }
		internal float ElapsedSeconds { get; set; }
		internal bool InteractionUnlocked { get; set; }
	}

	private static readonly Color DefaultAdventureUpgradeTitleColor = new(0.93f, 0.96f, 1.0f, 0.99f);
	private static readonly Color DefaultAdventureUpgradeGlowColor = new(0.48f, 0.92f, 1.0f, 0.06f);
	private static readonly Color DefaultAdventureUpgradeRuleColor = new(0.98f, 0.9f, 0.7f, 0.08f);
	private static readonly int[] AdventureUpgradeOverlayPreferredOrder = { 2, 1, 0 };
	private const int FirstMainAdventureAvatarStageNumber = 3;
	private const float AdventureUpgradeOverlayRestScale = 0.94f;
	private const float AdventureUpgradeOverlayHoverScale = 0.99f;
	private const float AdventureUpgradeOverlayRevealStartScale = 0.82f;
	private const float AdventureUpgradeOverlayHoverLift = 8.0f;
	private const float AdventureUpgradeOverlayRevealStartYOffset = 28.0f;
	private const float AdventureUpgradeOverlayRevealStaggerSeconds = 0.07f;
	private const float AdventureUpgradeOverlayRevealDurationSeconds = 0.55f;
	private const float AdventureUpgradeOverlayBackdropFadeDurationSeconds = 0.14f;
	private const float AdventureUpgradeOverlayPanelFadeDurationSeconds = 0.18f;
	private const float AdventureUpgradeOverlayMotionSmoothSpeed = 30.0f;
	private const float AdventureUpgradeOverlayHighlightSmoothSpeed = 10.0f;
	private const float AdventureUpgradeOverlayFocusHighlightAmount = 0.26f;
	private const float AdventureUpgradeOverlayHoverHighlightAmount = 1.0f;
	private const float AdventureUpgradeOverlayMotionSnapDistance = 0.001f;

	private AdventureSuitUpgradeCatalog _adventureUpgradeCatalog = default!;
	private readonly Queue<AdventureUpgradeTriggerSource> _pendingAdventureUpgradeTriggers = new();
	private Task _adventureUpgradeResolutionTask = Task.CompletedTask;
	private TaskCompletionSource<AdventureUpgradeSelectionResult>? _adventureUpgradeSelectionCompletionSource;
	private int _pendingAdventureWinnerPlayerIndex = -1;
	private bool _isResolvingAdventureUpgradeSelection;
	private TableCard? _adventureEnemyNextInsertAnchorTableCard;
	private TableCard? _adventureTraceHuntMarkedTableCard;
	private TableCard? _adventureHuntOrderMarkedTableCard;
	private TableCard? _adventureCanopyRevealAnchorTableCard;
	private AdventureUpgradeOverlayRevealState? _adventureUpgradeOverlayRevealState;
	private readonly Dictionary<ulong, AdventureUpgradeOverlayCardMotionState> _adventureUpgradeOverlayCardMotionStates = new();

	private void LoadAdventureUpgradeCatalog()
	{
		_adventureUpgradeCatalog = AdventureSuitUpgradeCatalog.Load(this);
	}

	private void EnsureAdventureUpgradeCatalogLoaded()
	{
		if (_adventureUpgradeCatalog != null && _adventureUpgradeCatalog.All.Count > 0)
		{
			return;
		}

		LoadAdventureUpgradeCatalog();
	}

	private void ResetAdventureUpgradeBoardState()
	{
		_pendingAdventureUpgradeTriggers.Clear();
		_pendingAdventureWinnerPlayerIndex = -1;
		_isResolvingAdventureUpgradeSelection = false;
		ResetAdventureEnemyMissPendingState();
		_adventureTraceHuntMarkedTableCard = null;
		_adventureHuntOrderMarkedTableCard = null;
		_adventureCanopyRevealAnchorTableCard = null;
		_adventureUpgradeResolutionTask = Task.CompletedTask;
		ResetAdventureUpgradeOverlayCardMotionStates();
		CancelAdventureUpgradeSelection();
		DisposeActiveSelectionOverlay();
	}

	private void WarnMissingAdventureRunSessionStore(string action)
	{
		GD.PushWarning($"AdventureRunSessionStore autoload is missing; cannot {action}.");
	}

	private AdventureUpgradeRunState? GetAdventureUpgradeRunState()
	{
		return GetAdventureRunSessionStore()?.GetRunState();
	}

	private void BeginAdventureUpgradeRun(int avatarStageNumber)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("begin the adventure upgrade run");
			return;
		}

		store.BeginNewRun(avatarStageNumber);
	}

	private void AdvanceAdventureUpgradeRunToStage(int avatarStageNumber)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("advance the adventure upgrade run");
			return;
		}

		store.AdvanceToStage(avatarStageNumber);
	}

	private void MarkAdventureRunStartUpgradeGranted()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("mark the run-start upgrade as granted");
			return;
		}

		store.MarkRunStartUpgradeGranted();
	}

	private void MarkAdventureVictoryUpgradeGranted()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("mark the victory upgrade as granted");
			return;
		}

		store.MarkVictoryUpgradeGranted();
	}

	private void RecordAdventureAcquiredUpgrade(string upgradeId)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("record an acquired adventure upgrade");
			return;
		}

		store.AcquireUpgrade(upgradeId);
	}

	private void PersistAdventureEffectState(AdventureUpgradeEffectState effectState)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("persist adventure upgrade effect state");
			return;
		}

		store.ReplaceEffectState(effectState);
	}

	private int DrawCardsFromDeckToHandForAdventureEffect(int playerIndex, int drawCount)
	{
		int normalizedDrawCount = Math.Max(0, drawCount);
		int drawnCardCount = 0;
		for (int drawIndex = 0; drawIndex < normalizedDrawCount; drawIndex++)
		{
			if (!DrawCardFromDeckToHand(playerIndex).HasValue)
			{
				break;
			}

			drawnCardCount++;
		}

		if (drawnCardCount > 0)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
		}

		return drawnCardCount;
	}

	private bool TryGrantAdventureMainPlayerExtraBaseCardAction(bool keepsTurnAfterAction = false)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		if (_playerHands.Count <= MainPlayerIndex || _playerHands[MainPlayerIndex].Count == 0)
		{
			return false;
		}

		_adventureMainPlayerPendingExtraBaseCardAction = true;
		_adventureMainPlayerExtraBaseCardActionKeepsTurn = keepsTurnAfterAction;
		return true;
	}

	private bool ConsumeAdventureMainPlayerExtraBaseCardActionIfNeeded(int playerIndex)
	{
		if (playerIndex != MainPlayerIndex || !_adventureMainPlayerPendingExtraBaseCardAction)
		{
			return false;
		}

		_adventureMainPlayerPendingExtraBaseCardAction = false;
		return true;
	}

	private async Task TryResolveAdventurePendingExtraBaseCardActionForAiAsync(int turnSequenceGeneration)
	{
		if (!_adventureMainPlayerPendingExtraBaseCardAction
			|| _currentTurnPlayerIndex != MainPlayerIndex
			|| IsLocalSeatHumanConnected())
		{
			return;
		}

		if (_playerHands.Count <= MainPlayerIndex || _playerHands[MainPlayerIndex].Count == 0)
		{
			_adventureMainPlayerPendingExtraBaseCardAction = false;
			return;
		}

		await PlayMainPlayerCardAndAdvanceAsync(0, turnSequenceGeneration);
	}

	private void ApplyAdventureEnemyHookDredgeIfNeeded(
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int playerIndex)
	{
		if (!ShouldUseAdventureUpgradeSystem() || !IsAdventureEnemyPlayer(playerIndex) || prePlayTableCards.Count == 0)
		{
			return;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return;
		}

		TableCard? oldestOldTableCard = null;
		foreach (TableCard tableCard in prePlayTableCards)
		{
			if (!tableCard.IsBuriedInSand && tableCard.Card.HasValue)
			{
				oldestOldTableCard = tableCard;
				break;
			}
		}

		if (oldestOldTableCard == null || !_tableCards.Contains(oldestOldTableCard))
		{
			return;
		}

		AdventureEnemyMissHookDredgeResult result = AdventureUpgradeEffectService.ResolveEnemyMissHookDredge(
			runState.GetAcquiredUpgradeIds(),
			_tableCards.Count,
			GetCardScoreValue(oldestOldTableCard.Card!.Value));
		if (!result.MoveOldestOldTableCardToPlayerDeckTop)
		{
			return;
		}

		_tableCards.Remove(oldestOldTableCard);
		_playerDecks[MainPlayerIndex].Insert(0, PrepareCardForPlayerDeck(oldestOldTableCard.Card.Value));
		if (ReferenceEquals(_adventureTraceHuntMarkedTableCard, oldestOldTableCard))
		{
			_adventureTraceHuntMarkedTableCard = null;
		}

		if (ReferenceEquals(_adventureHuntOrderMarkedTableCard, oldestOldTableCard))
		{
			_adventureHuntOrderMarkedTableCard = null;
		}

		RefreshAdventureCanopyAnchorAfterTableMutation();
		RenderPlayerCardPiles();
		RenderTableCards();
		if (result.DirectDamageToEnemy > 0)
		{
			AddPlayerScore(MainPlayerIndex, result.DirectDamageToEnemy);
		}
	}

	private void ResetAdventureEnemyMissPendingState()
	{
		_adventureEnemyNextBaseCardFacesUp = false;
		_adventureEnemyNextCardInsertMode = AdventureEnemyNextCardInsertMode.None;
		_adventureEnemyNextCaptureDamageDelta = 0;
		_adventureEnemyNextInsertAnchorTableCard = null;
	}

	private bool HasAdventureUpgrade(string upgradeId)
	{
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true && runState.HasAcquiredUpgrade(upgradeId);
	}

	private void RefreshAdventureHuntOrderMarkForMainPlayerTurn()
	{
		if (!ShouldUseAdventureUpgradeSystem() || !HasAdventureUpgrade("spades_hunt_order"))
		{
			_adventureHuntOrderMarkedTableCard = null;
			return;
		}

		_adventureHuntOrderMarkedTableCard = FindLeftmostCurrentBaseTableCard();
	}

	private void RefreshAdventureTraceHuntMarkAfterMainPlayerCapture(IReadOnlyCollection<TableCard> prePlayTableCards)
	{
		if (!ShouldUseAdventureUpgradeSystem() || !HasAdventureUpgrade("spades_trace_hunt"))
		{
			_adventureTraceHuntMarkedTableCard = null;
			return;
		}

		_adventureTraceHuntMarkedTableCard = FindRightmostRemainingOldBaseTableCard(prePlayTableCards);
	}

	private async Task<bool> ApplyAdventureMainPlayerMissTableEffectsAsync(
		AdventureMainPlayerMissEffectResult effectResult,
		int playedTableCardIndex,
		int turnSequenceGeneration)
	{
		if (playedTableCardIndex < 0 || playedTableCardIndex >= _tableCards.Count)
		{
			return false;
		}

		TableCard playedTableCard = _tableCards[playedTableCardIndex];
		if (effectResult.InsertPlayedCardAtLeftmost && playedTableCardIndex > 0)
		{
			_tableCards.RemoveAt(playedTableCardIndex);
			_tableCards.Insert(0, playedTableCard);
			playedTableCardIndex = 0;
		}

		if (effectResult.ShuffleTableAfterMiss && GetMovableTableShuffleCardCount() >= 2)
		{
			List<TableCard> shuffledTableCards = BuildShuffledTableCards();
			await PlayTableCardsShuffleAnimationAsync(shuffledTableCards, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return false;
			}

			ApplyShuffledTableCards(shuffledTableCards);
			RenderTableCards();
		}

		if (effectResult.SetCanopyRevealAnchor)
		{
			_adventureCanopyRevealAnchorTableCard = playedTableCard;
		}

		return true;
	}

	private void ApplyAdventureCanopyRevealToNewTableCard(int playedTableCardIndex)
	{
		if (_adventureCanopyRevealAnchorTableCard == null)
		{
			return;
		}

		int anchorIndex = _tableCards.IndexOf(_adventureCanopyRevealAnchorTableCard);
		if (anchorIndex < 0)
		{
			_adventureCanopyRevealAnchorTableCard = null;
			return;
		}

		if (playedTableCardIndex > anchorIndex
			&& playedTableCardIndex >= 0
			&& playedTableCardIndex < _tableCards.Count)
		{
			_tableCards[playedTableCardIndex].IsFaceUp = true;
		}
	}

	private void RefreshAdventureCanopyAnchorAfterTableMutation()
	{
		if (_adventureCanopyRevealAnchorTableCard != null
			&& _tableCards.IndexOf(_adventureCanopyRevealAnchorTableCard) < 0)
		{
			_adventureCanopyRevealAnchorTableCard = null;
		}
	}

	private void ApplyAdventureEnemyMissEffects(
		int playerIndex,
		int playedTableCardIndex,
		IReadOnlyCollection<TableCard> prePlayTableCards)
	{
		if (!ShouldUseAdventureUpgradeSystem() || !IsAdventureEnemyPlayer(playerIndex))
		{
			return;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return;
		}

		bool hadOldTableCardsBeforeMiss = prePlayTableCards.Count > 0;
		AdventureEnemyMissEffectResult effectResult = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			runState.GetAcquiredUpgradeIds(),
			_tableCards.Count,
			hadOldTableCardsBeforeMiss);
		_adventureEnemyNextBaseCardFacesUp = effectResult.NextCardFacesUp;
		_adventureEnemyNextCardInsertMode = effectResult.InsertMode;
		_adventureEnemyNextCaptureDamageDelta = effectResult.NextCaptureDamageDelta;
		_adventureEnemyNextInsertAnchorTableCard = ResolveAdventureEnemyNextInsertAnchorAfterMiss(
			effectResult.InsertMode,
			playedTableCardIndex);
		if (effectResult.DirectDamageToEnemy > 0)
		{
			AddPlayerScore(MainPlayerIndex, effectResult.DirectDamageToEnemy);
		}

		if (effectResult.GrantMainPlayerExtraBaseAction)
		{
			TryGrantAdventureMainPlayerExtraBaseCardAction(keepsTurnAfterAction: true);
		}
	}

	private TableCard? ResolveAdventureEnemyNextInsertAnchorAfterMiss(
		AdventureEnemyNextCardInsertMode insertMode,
		int playedTableCardIndex)
	{
		if (insertMode != AdventureEnemyNextCardInsertMode.BeforeTrackedOldest
			|| _tableCards.Count <= 1)
		{
			return null;
		}

		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			if (tableCardIndex != playedTableCardIndex)
			{
				return _tableCards[tableCardIndex];
			}
		}

		return null;
	}

	private AdventureEnemyNextBaseCardModifiers ConsumeAdventureEnemyNextBaseCardModifiers(int playerIndex)
	{
		if (!IsAdventureEnemyPlayer(playerIndex))
		{
			return new AdventureEnemyNextBaseCardModifiers(false, -1, 0);
		}

		int tableInsertIndex = -1;
		switch (_adventureEnemyNextCardInsertMode)
		{
			case AdventureEnemyNextCardInsertMode.Leftmost:
				tableInsertIndex = 0;
				break;
			case AdventureEnemyNextCardInsertMode.BeforeTrackedOldest:
				tableInsertIndex = _adventureEnemyNextInsertAnchorTableCard != null
					? _tableCards.IndexOf(_adventureEnemyNextInsertAnchorTableCard)
					: -1;
				if (tableInsertIndex < 0 && _tableCards.Count > 0)
				{
					tableInsertIndex = 0;
				}
				break;
		}

		AdventureEnemyNextBaseCardModifiers modifiers = new(
			_adventureEnemyNextBaseCardFacesUp,
			tableInsertIndex,
			_adventureEnemyNextCaptureDamageDelta);
		ResetAdventureEnemyMissPendingState();
		return modifiers;
	}

	private void ApplyAdventureQuestionRewardEffects(int playerIndex, AdventureQuestionRewardEffectResult? effectResult)
	{
		if (!effectResult.HasValue || playerIndex != MainPlayerIndex)
		{
			return;
		}

		AdventureQuestionRewardEffectResult resolved = effectResult.Value;
		if (resolved.HealAmount > 0)
		{
			ApplyAdventureMainPlayerHealing(resolved.HealAmount);
		}

		if (resolved.DrawCount > 0)
		{
			DrawCardsFromDeckToHandForAdventureEffect(playerIndex, resolved.DrawCount);
		}

		if (resolved.BonusDirectDamage > 0)
		{
			AddPlayerScore(playerIndex, resolved.BonusDirectDamage);
		}
	}

	private void ApplyAdventureUpgradeAcquisitionEffects(
		string upgradeId,
		AdventureUpgradeTriggerSource triggerSource)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return;
		}

		bool triggerBattleStartBonusImmediately = triggerSource == AdventureUpgradeTriggerSource.RunStart;
		AdventureUpgradeAcquisitionEffectResult effectResult = AdventureUpgradeEffectService.OnUpgradeAcquired(
			runState.EffectState,
			upgradeId,
			triggerBattleStartBonusImmediately);
		store.ReplaceEffectState(runState.EffectState.Clone());

		if (effectResult.MaxHealthDelta > 0 || effectResult.HealAmount > 0)
		{
			ApplyAdventureMainPlayerHealing(effectResult.HealAmount, effectResult.MaxHealthDelta, animate: true);
		}

		if (effectResult.ImmediateDrawCount > 0)
		{
			DrawCardsFromDeckToHandForAdventureEffect(MainPlayerIndex, effectResult.ImmediateDrawCount);
		}
	}

	private int ResolveAdventureCaptureDamage(
		int playerIndex,
		AdventureCaptureDamageInput damageInput)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return Math.Max(0, damageInput.BaseDamage + damageInput.FlatDamageDelta);
		}

		if (playerIndex != MainPlayerIndex)
		{
			return Math.Max(0, damageInput.BaseDamage + damageInput.FlatDamageDelta);
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return Math.Max(0, damageInput.BaseDamage);
		}

		AdventureCaptureDamageResult result = AdventureUpgradeEffectService.ApplyCaptureDamage(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			damageInput);
		store.ReplaceEffectState(runState.EffectState.Clone());
		return result.FinalDamage;
	}

	private AdventurePostCaptureEffectResult? ResolveAdventurePostCaptureEffects(
		int playerIndex,
		int finalDamage,
		int successfulCapturesThisTurn,
		bool attachedEffectTriggered = false,
		bool capturedOnlyOuterVisibleCards = false)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return null;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return null;
		}

		return AdventureUpgradeEffectService.OnMainPlayerCaptureResolved(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			finalDamage,
			successfulCapturesThisTurn,
			attachedEffectTriggered,
			capturedOnlyOuterVisibleCards);
	}

	private AdventureMainPlayerMissEffectResult ApplyAdventureMainPlayerMissEffects(int playerIndex)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return new AdventureMainPlayerMissEffectResult(false);
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return new AdventureMainPlayerMissEffectResult(false);
		}

		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			_tableCards.Count);
		store.ReplaceEffectState(runState.EffectState.Clone());
		return result;
	}

	private async Task ApplyAdventureMainPlayerTurnStartEffectsAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return;
		}

		AdventureTurnStartEffectResult effectResult = AdventureUpgradeEffectService.ConsumeMainPlayerTurnStartEffects(runState.EffectState);
		store.ReplaceEffectState(runState.EffectState.Clone());
		if (effectResult.HealAmount > 0)
		{
			ApplyAdventureMainPlayerHealing(effectResult.HealAmount);
		}

		bool drewAny = false;
		for (int drawIndex = 0; drawIndex < effectResult.DrawCount; drawIndex++)
		{
			DrawnHandCard? drawnCard = DrawCardFromDeckToHand(playerIndex);
			if (!drawnCard.HasValue)
			{
				break;
			}

			drewAny = true;
			await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}

		if (drewAny)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
		}
	}

	private async Task ApplyAdventurePostCaptureEffectsAsync(
		int playerIndex,
		AdventurePostCaptureEffectResult? effectResult,
		int turnSequenceGeneration)
	{
		if (!effectResult.HasValue || playerIndex != MainPlayerIndex)
		{
			return;
		}

		AdventurePostCaptureEffectResult resolved = effectResult.Value;
		if (resolved.RevealRemainingHiddenCards)
		{
			await RevealRemainingHiddenTableCardsAsync(turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}

		if (resolved.HealAmount > 0)
		{
			ApplyAdventureMainPlayerHealing(resolved.HealAmount);
		}

		bool drewAny = false;
		for (int drawIndex = 0; drawIndex < resolved.DrawCount; drawIndex++)
		{
			DrawnHandCard? drawnCard = DrawCardFromDeckToHand(playerIndex);
			if (!drawnCard.HasValue)
			{
				break;
			}

			drewAny = true;
			await PlayReplacementDrawAnimationAsync(playerIndex, drawnCard, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}

		if (drewAny)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
		}
	}

	private async Task RevealRemainingHiddenTableCardsAsync(int turnSequenceGeneration)
	{
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsFaceUp || tableCard.IsBuriedInSand)
			{
				continue;
			}

			Texture2D? revealTexture = tableCard.Card?.Texture ?? tableCard.ItemOverlay?.Texture;
			if (revealTexture == null)
			{
				continue;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			tableCard.IsFaceUp = true;
			RenderTableCards();
			await ToSignal(GetTree().CreateTimer(TableCardRevealPauseSeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}
	}

	private async Task ApplyAdventureClubCaptureReturnsAsync(
		int playerIndex,
		IReadOnlyList<PlayingCard> capturedBaseCards,
		PlayingCard? playedCardForAdventureReturns,
		bool playedCardUsedAttachedEffect,
		int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return;
		}

		AdventureClubCaptureEffectResult effectResult = AdventureUpgradeEffectService.ResolveClubCaptureEffects(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			capturedBaseCards.Count,
			GetTotalHandCardCount(playerIndex),
			MaxTotalHandCards,
			playUsedAttachedEffect: playedCardUsedAttachedEffect,
			playedCardWasBaseCard: playedCardForAdventureReturns.HasValue);
		store.ReplaceEffectState(runState.EffectState.Clone());
		if (!effectResult.ReturnPlayedCardToHand && !effectResult.ReturnTwoCapturedCardsToHand)
		{
			return;
		}

		var cardsToReturn = new List<PlayingCard>();
		if (effectResult.ReturnTwoCapturedCardsToHand)
		{
			for (int index = 0; index < Math.Min(2, capturedBaseCards.Count); index++)
			{
				cardsToReturn.Add(capturedBaseCards[index]);
			}
		}

		if (effectResult.ReturnPlayedCardToHand && playedCardForAdventureReturns.HasValue)
		{
			bool alreadyQueued = false;
			foreach (PlayingCard card in cardsToReturn)
			{
				if (card.Equals(playedCardForAdventureReturns.Value))
				{
					alreadyQueued = true;
					break;
				}
			}

			if (!alreadyQueued)
			{
				cardsToReturn.Add(playedCardForAdventureReturns.Value);
			}
		}

		foreach (PlayingCard card in cardsToReturn)
		{
			int deckIndex = FindLastPlayerDeckCardIndex(playerIndex, card);
			if (deckIndex < 0 || GetTotalHandCardCount(playerIndex) >= MaxTotalHandCards)
			{
				continue;
			}

			_playerDecks[playerIndex].RemoveAt(deckIndex);
			int handIndex = _playerHands[playerIndex].Count;
			_playerHands[playerIndex].Add(card);
			await PlayReplacementDrawAnimationAsync(playerIndex, new DrawnHandCard(card, handIndex), turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}

		RenderPlayerCardPiles();
		RenderPlayerHands();
	}

	private string ResolveAdventureMainPlayerPrimaryMatchRank(PlayingCard playedCard)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| _currentTurnPlayerIndex != MainPlayerIndex
			|| !TryGetNumericRankValue(playedCard.Rank, out _))
		{
			return playedCard.Rank;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return playedCard.Rank;
		}

		return AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			playedCard.Rank).PrimaryRank;
	}

	private List<string>? BuildAdventureMainPlayerAlternateRanks(PlayingCard playedCard)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| _currentTurnPlayerIndex != MainPlayerIndex
			|| !TryGetNumericRankValue(playedCard.Rank, out _))
		{
			return null;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return null;
		}

		IReadOnlyList<string> alternateRanks = AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			playedCard.Rank).AlternateRanks;
		if (alternateRanks.Count == 0)
		{
			return null;
		}

		var results = new List<string>(alternateRanks.Count);
		foreach (string alternateRank in alternateRanks)
		{
			results.Add(alternateRank);
		}

		return results;
	}

	private bool ShouldAdventureBorrowSuitSetAffectPlayedCard(PlayingCard playedCard)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| _currentTurnPlayerIndex != MainPlayerIndex
			|| string.IsNullOrWhiteSpace(playedCard.Suit)
			|| !TryGetNumericRankValue(playedCard.Rank, out _))
		{
			return false;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true
			&& runState.HasAcquiredUpgrade("clubs_borrow_suit_set")
			&& runState.EffectState.ClubBorrowSuitSetArmed;
	}

	private void ConsumeAdventureBorrowSuitSetAfterPlayIfNeeded(PlayingCard playedCard)
	{
		if (!ShouldAdventureBorrowSuitSetAffectPlayedCard(playedCard))
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return;
		}

		AdventureUpgradeEffectService.ConsumeBorrowSuitSetAfterNumericBaseCardPlay(runState.EffectState, playedCard.Rank);
		store.ReplaceEffectState(runState.EffectState.Clone());
	}

	private async Task ApplyAdventureLinkedStemsQuestionRewardIfNeededAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (store == null || runState?.IsActive != true)
		{
			return;
		}

		if (!AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
			runState.GetAcquiredUpgradeIds(),
			_adventureMainPlayerSuccessfulCapturesThisTurn))
		{
			return;
		}

		QuestionCardRewardGrant? rewardGrant = await GrantQuestionCardRewardAsync(
			playerIndex,
			reserveReplacementCard: false,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !rewardGrant.HasValue)
		{
			return;
		}

		ApplyAdventureQuestionRewardEffects(playerIndex, ResolveAdventureQuestionRewardEffects(playerIndex));
		RenderPlayerCardPiles();
		RenderPlayerHands();
	}

	private int FindLastPlayerDeckCardIndex(int playerIndex, PlayingCard card)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return -1;
		}

		List<PlayingCard> deck = _playerDecks[playerIndex];
		for (int index = deck.Count - 1; index >= 0; index--)
		{
			if (deck[index].Equals(card))
			{
				return index;
			}
		}

		return -1;
	}

	private int GetAdventureBattleStartDrawCount()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return 0;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return 0;
		}

		return AdventureUpgradeEffectService.GetBattleStartDrawCount(runState.GetAcquiredUpgradeIds());
	}

	private void ClearAdventureUpgradeRun()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("clear the adventure upgrade run");
			return;
		}

		store.ClearRun();
	}

	private void InitializeAdventureUpgradeRunForCurrentBoard()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return;
		}

		int avatarStageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			BeginAdventureUpgradeRun(avatarStageNumber);
			return;
		}

		if (runState.CurrentAvatarStageNumber == avatarStageNumber)
		{
			return;
		}

		AdvanceAdventureUpgradeRunToStage(avatarStageNumber);
	}

	private bool ShouldUseAdventureUpgradeSystem()
	{
		if (!adventure_mode || adventure_tutorial_mode)
		{
			return false;
		}

		int avatarStageNumber = CharacterAvatarCatalog.GetAdventureStageNumber(adventure_enemy_avatar_id);
		return AdventureUpgradeChoiceRules.IsMainAdventureAvatarStageNumber(avatarStageNumber);
	}

	private void QueueAdventureRunStartUpgradeIfNeeded()
	{
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem() || runState?.ShouldGrantRunStartUpgrade != true)
		{
			return;
		}

		EnqueueAdventureUpgradeTriggers(
			AdventureUpgradeTriggerRules.BuildTriggerOrder(
				runStartTriggered: true,
				bigCaptureTriggered: false,
				victoryTriggered: false));
	}

	private void EnqueueAdventureUpgradeTriggers(IReadOnlyList<AdventureUpgradeTriggerSource> triggerSources)
	{
		if (!ShouldUseAdventureUpgradeSystem() || triggerSources.Count == 0)
		{
			return;
		}

		foreach (AdventureUpgradeTriggerSource triggerSource in triggerSources)
		{
			if (triggerSource == AdventureUpgradeTriggerSource.None || HasPendingAdventureUpgradeTrigger(triggerSource))
			{
				continue;
			}

			_pendingAdventureUpgradeTriggers.Enqueue(triggerSource);
		}
	}

	private bool HasPendingAdventureUpgradeTrigger(AdventureUpgradeTriggerSource triggerSource)
	{
		foreach (AdventureUpgradeTriggerSource pendingTrigger in _pendingAdventureUpgradeTriggers)
		{
			if (pendingTrigger == triggerSource)
			{
				return true;
			}
		}

		return false;
	}

	private IReadOnlyList<AdventureUpgradeTriggerSource> BuildAdventureUpgradeTriggerOrder(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered)
	{
		return AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered,
			bigCaptureTriggered,
			victoryTriggered);
	}

	private async Task QueueAndResolveAdventureUpgradeTriggersAsync(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered,
		int turnSequenceGeneration)
	{
		IReadOnlyList<AdventureUpgradeTriggerSource> triggerOrder = BuildAdventureUpgradeTriggerOrder(
			runStartTriggered,
			bigCaptureTriggered,
			victoryTriggered);
		EnqueueAdventureUpgradeTriggers(triggerOrder);
		if (_pendingAdventureUpgradeTriggers.Count == 0)
		{
			return;
		}

		await ResolvePendingAdventureUpgradeTriggersAsync(turnSequenceGeneration);
	}

	private Task ResolvePendingAdventureUpgradeTriggersAsync(int turnSequenceGeneration)
	{
		if (_isResolvingAdventureUpgradeSelection)
		{
			return _adventureUpgradeResolutionTask;
		}

		_adventureUpgradeResolutionTask = ResolvePendingAdventureUpgradeTriggersInternalAsync(turnSequenceGeneration);
		return _adventureUpgradeResolutionTask;
	}

	private async Task ResolvePendingAdventureUpgradeTriggersInternalAsync(int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return;
		}

		_isResolvingAdventureUpgradeSelection = true;
		try
		{
			while (_pendingAdventureUpgradeTriggers.Count > 0
				&& turnSequenceGeneration == _turnSequenceGeneration
				&& !_isLeavingGame
				&& !IsGameOver())
			{
				AdventureUpgradeTriggerSource triggerSource = _pendingAdventureUpgradeTriggers.Peek();
				IReadOnlyList<AdventureSuitUpgradeDefinition> offer = BuildAdventureUpgradeOffer();
				if (offer.Count == 0)
				{
					_pendingAdventureUpgradeTriggers.Dequeue();
					continue;
				}

				AdventureUpgradeSelectionResult selectionResult = await ShowAdventureUpgradeSelectionAsync(triggerSource, offer);
				if (turnSequenceGeneration != _turnSequenceGeneration || _isLeavingGame)
				{
					return;
				}

					if (selectionResult.Outcome is AdventureUpgradeSelectionOutcome.Canceled
						&& triggerSource != AdventureUpgradeTriggerSource.None)
					{
						return;
					}

					int selectedIndex = selectionResult.SelectedIndex;
					if (selectionResult.Outcome == AdventureUpgradeSelectionOutcome.DisplayFailed)
					{
						selectedIndex = 0;
					}

					if (selectedIndex < 0 || selectedIndex >= offer.Count)
					{
						if (selectionResult.Outcome == AdventureUpgradeSelectionOutcome.DisplayFailed)
						{
							return;
						}

						continue;
					}

					AdventureSuitUpgradeDefinition selectedDefinition = offer[selectedIndex];
					MarkAdventureUpgradeTriggerAsGranted(triggerSource);
					RecordAdventureAcquiredUpgrade(selectedDefinition.Id);
					ApplyAdventureUpgradeAcquisitionEffects(selectedDefinition.Id, triggerSource);
					GD.Print($"Adventure upgrade acquired: {selectedDefinition.Id}");
					_pendingAdventureUpgradeTriggers.Dequeue();
			}
		}
		finally
		{
			_isResolvingAdventureUpgradeSelection = false;
		}

		if (_pendingAdventureWinnerPlayerIndex >= 0
			&& _pendingAdventureUpgradeTriggers.Count == 0
			&& turnSequenceGeneration == _turnSequenceGeneration
			&& !IsGameOver())
		{
			int winnerPlayerIndex = _pendingAdventureWinnerPlayerIndex;
			_pendingAdventureWinnerPlayerIndex = -1;
			DeclareWinner(winnerPlayerIndex);
		}
	}

	private void MarkAdventureUpgradeTriggerAsGranted(AdventureUpgradeTriggerSource triggerSource)
	{
		switch (triggerSource)
		{
			case AdventureUpgradeTriggerSource.RunStart:
				MarkAdventureRunStartUpgradeGranted();
				break;
			case AdventureUpgradeTriggerSource.Victory:
				MarkAdventureVictoryUpgradeGranted();
				break;
		}
	}

	private IReadOnlyList<AdventureSuitUpgradeDefinition> BuildAdventureUpgradeOffer()
	{
		EnsureAdventureUpgradeCatalogLoaded();
		if (_adventureUpgradeCatalog == null || _adventureUpgradeCatalog.All.Count == 0)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		var definitions = new List<AdventureUpgradeDefinitionModel>(_adventureUpgradeCatalog.All.Count);
		foreach (AdventureSuitUpgradeDefinition definition in _adventureUpgradeCatalog.All)
		{
			definitions.Add(new AdventureUpgradeDefinitionModel(
				definition.Id,
				definition.DisplayName,
				definition.Suit,
				definition.Rarity,
				definition.TriggerText,
				definition.EffectText,
				definition.Enabled));
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		IReadOnlyList<AdventureUpgradeDefinitionModel> offerModels = AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			runState.GetAcquiredUpgradeIds(),
			Math.Max(1, runState.CurrentRunStageIndex),
			maxExclusive => _rng.RandiRange(0, maxExclusive - 1));

		if (offerModels.Count == 0)
		{
			return Array.Empty<AdventureSuitUpgradeDefinition>();
		}

		var offer = new List<AdventureSuitUpgradeDefinition>(offerModels.Count);
		foreach (AdventureUpgradeDefinitionModel model in offerModels)
		{
			if (_adventureUpgradeCatalog.TryGetDefinition(model.Id, out AdventureSuitUpgradeDefinition definition))
			{
				offer.Add(definition);
			}
		}

		return offer;
	}

		private async Task<AdventureUpgradeSelectionResult> ShowAdventureUpgradeSelectionAsync(
			AdventureUpgradeTriggerSource triggerSource,
			IReadOnlyList<AdventureSuitUpgradeDefinition> offer)
		{
			DisposeActiveSelectionOverlay();
		var overlayOptions = new List<AdventureUpgradeOverlayOptionModel>(offer.Count);
			for (int index = 0; index < offer.Count; index++)
			{
				AdventureSuitUpgradeDefinition definition = offer[index];
				overlayOptions.Add(new AdventureUpgradeOverlayOptionModel(
					definition.Suit,
					definition.Rarity,
					FormatAdventureUpgradeDescription(definition)));
			}
			var completionSource = new TaskCompletionSource<AdventureUpgradeSelectionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
			_adventureUpgradeSelectionCompletionSource = completionSource;
			if (!ShowAdventureUpgradeSelectionOverlay(overlayOptions))
			{
				_adventureUpgradeSelectionCompletionSource = null;
				return new AdventureUpgradeSelectionResult(
					AdventureUpgradeSelectionOutcome.DisplayFailed,
					-1);
			}

			AdventureUpgradeSelectionResult selectionResult = await completionSource.Task;
		if (ReferenceEquals(_adventureUpgradeSelectionCompletionSource, completionSource))
		{
			_adventureUpgradeSelectionCompletionSource = null;
		}

		return selectionResult;
	}

	private void CancelAdventureUpgradeSelection()
	{
		ResolveAdventureUpgradeSelection(
			new AdventureUpgradeSelectionResult(
				AdventureUpgradeSelectionOutcome.Canceled,
				-1),
			clearOverlay: false);
	}

	private void ResolveAdventureUpgradeSelection(
		AdventureUpgradeSelectionResult result,
		bool clearOverlay)
	{
		TaskCompletionSource<AdventureUpgradeSelectionResult>? completionSource = _adventureUpgradeSelectionCompletionSource;
		_adventureUpgradeSelectionCompletionSource = null;
		completionSource?.TrySetResult(result);
		if (clearOverlay)
		{
			DisposeActiveSelectionOverlay();
		}
	}

	private bool ShowAdventureUpgradeSelectionOverlay(IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		if (ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure())
		{
			return false;
		}

		PackedScene? overlayScene = GD.Load<PackedScene>(AdventureUpgradeSelectionOverlayScenePath);
		if (overlayScene?.Instantiate() is not Control selectionOverlay)
		{
			GD.PushError($"Missing adventure upgrade selection overlay scene: {AdventureUpgradeSelectionOverlayScenePath}");
			return false;
		}

		AddChild(selectionOverlay);
		PlayGameSfx(GameSfx.UiPanelOpen, -2.0f, 0.98f, 1.02f, 0.08);
		ResetAdventureUpgradeOverlayCardMotionStates();
		ConfigureAdventureUpgradeSelectionOverlay(selectionOverlay, options);
		RegisterSelectionOverlaySession(new SelectionOverlaySession(
			SelectionOverlayKind.AdventureUpgrade,
			selectionOverlay,
			options.Count,
			optionIndex =>
			{
				if (optionIndex < 0 || optionIndex >= options.Count)
				{
					return false;
				}

				ResolveAdventureUpgradeSelection(
					new AdventureUpgradeSelectionResult(
						AdventureUpgradeSelectionOutcome.Confirmed,
						optionIndex),
					clearOverlay: true);
				return true;
			},
			cancel: CancelAdventureUpgradeSelection,
			dispose: () =>
			{
				ResetAdventureUpgradeOverlayCardMotionStates();
				if (IsInstanceValid(selectionOverlay))
				{
					RemoveChild(selectionOverlay);
					selectionOverlay.Free();
				}
			}));
		return true;
	}

	private string FormatAdventureUpgradeDescription(AdventureSuitUpgradeDefinition definition)
	{
		return GetAdventureUpgradeDescription(definition.DescriptionKey, definition.EffectText);
	}

	private void ConfigureAdventureUpgradeSelectionOverlay(
		Control selectionOverlay,
		IReadOnlyList<AdventureUpgradeOverlayOptionModel> options)
	{
		Control? header = selectionOverlay.GetNodeOrNull<Control>("Margin/Layout/Header");
		if (header != null)
		{
			header.Visible = false;
		}

		ColorRect backdrop = selectionOverlay.GetNode<ColorRect>("Backdrop");
		Panel selectionPanel = selectionOverlay.GetNode<Panel>("Margin/SelectionPanel");
		TextureRect selectionHalo = selectionOverlay.GetNode<TextureRect>("Margin/SelectionHalo");
		int visibleOptionCount = Mathf.Min(options.Count, 3);
		InitializeAdventureUpgradeSelectionOverlayReveal(
			selectionOverlay,
			backdrop,
			selectionPanel,
			selectionHalo,
			visibleOptionCount);

		for (int index = 0; index < 3; index++)
		{
			int capturedIndex = index;
			Button button = selectionOverlay.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
			AdventureUpgradeChoiceArt choiceArt = button.GetNode<AdventureUpgradeChoiceArt>("CardFrame/ChoiceArt");
			Control cardFrame = button.GetNode<Control>("CardFrame");

			bool visible = index < options.Count;
			Color baseModulate = cardFrame.Modulate;
			button.Visible = visible;
			button.Text = string.Empty;
			button.Disabled = !visible;
			button.FocusMode = visible ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
			cardFrame.PivotOffset = button.Size * 0.5f;
			cardFrame.Position = GetAdventureUpgradeCardFrameOffset(index);
			cardFrame.RotationDegrees = GetAdventureUpgradeCardFrameRotation(index);
			cardFrame.Scale = Vector2.One * AdventureUpgradeOverlayRestScale;
			cardFrame.Modulate = baseModulate;

			button.Pressed += () => TrySelectActiveSelectionOverlayOption(SelectionOverlayKind.AdventureUpgrade, capturedIndex);
			button.FocusEntered += () => RefreshAdventureUpgradeOverlayHighlight(button);
			button.FocusExited += () => RefreshAdventureUpgradeOverlayHighlight(button);
			button.MouseEntered += () => SetAdventureUpgradeOverlayPointerHover(button, true);
			button.MouseExited += () => SetAdventureUpgradeOverlayPointerHover(button, false);
			button.Connect("mouse_entered", Callable.From(() => SetAdventureUpgradeOverlayPointerHover(button, true)));
			button.Connect("mouse_exited", Callable.From(() => SetAdventureUpgradeOverlayPointerHover(button, false)));
			button.Connect(Control.SignalName.MouseEntered, Callable.From(() => SetAdventureUpgradeOverlayPointerHover(button, true)));
			button.Connect(Control.SignalName.MouseExited, Callable.From(() => SetAdventureUpgradeOverlayPointerHover(button, false)));
			button.GuiInput += @event =>
			{
				if (@event is InputEventMouseMotion)
				{
					SetAdventureUpgradeOverlayPointerHover(button, true);
				}
			};

			if (!visible)
			{
				button.ReleaseFocus();
				choiceArt.ConfigureChoice(null, string.Empty, string.Empty, false);
				choiceArt.SetHighlightAmount(0.0f);
				continue;
			}

			RegisterAdventureUpgradeOverlayCardMotionState(button, cardFrame, choiceArt, baseModulate);
			InitializeAdventureUpgradeOverlayCardReveal(button, index, visibleOptionCount);

			AdventureUpgradeOverlayOptionModel option = options[index];
			AdventureUpgradeCardVisualEntryResource? visualEntry = AdventureUpgradeCardVisualResolver.ResolveEntry(option.Suit, option.Rarity);
			Texture2D? cardTexture = AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity(option.Suit, option.Rarity);
			choiceArt.ConfigureChoice(cardTexture, string.Empty, option.Description, false);
			choiceArt.SetPalette(
				visualEntry?.TitleColor ?? DefaultAdventureUpgradeTitleColor,
				visualEntry?.GlowColor ?? DefaultAdventureUpgradeGlowColor,
				visualEntry?.RuleColor ?? DefaultAdventureUpgradeRuleColor);
			RefreshAdventureUpgradeOverlayHighlight(button);
		}

		if (visibleOptionCount == 0)
		{
			CallDeferred(nameof(FocusAdventureUpgradeSelectionOverlayDefaultButton));
		}
	}

	private void FocusAdventureUpgradeSelectionOverlayDefaultButton()
	{
		if (_adventureUpgradeOverlayRevealState != null
			&& !_adventureUpgradeOverlayRevealState.InteractionUnlocked)
		{
			return;
		}

		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null
			|| !IsSelectionOverlaySessionVisible(session)
			|| !session.Root.IsInsideTree())
		{
			return;
		}

		foreach (int index in AdventureUpgradeOverlayPreferredOrder)
		{
			Button button = session.Root.GetNode<Button>($"Margin/Layout/CardsRow/Option{index + 1}Button");
			if (!button.Visible || button.Disabled || !IsInstanceValid(button))
			{
				continue;
			}

			button.GrabFocus();
			RefreshAdventureUpgradeOverlayHighlight(button);
			return;
		}
	}

	private void RefreshAdventureUpgradeOverlayHighlight(Button button)
	{
		if (!TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return;
		}

		bool canInteract = state.InteractionUnlocked && button.Visible && !button.Disabled;
		bool pointerHovered = button.Visible && state.PointerHovered;
		bool focused = canInteract && button.HasFocus();

		state.TargetPosition = state.BasePosition + (pointerHovered ? new Vector2(0.0f, -AdventureUpgradeOverlayHoverLift) : Vector2.Zero);
		state.TargetScale = pointerHovered
			? Vector2.One * AdventureUpgradeOverlayHoverScale
			: state.BaseScale;
		state.TargetRotationDegrees = state.BaseRotationDegrees;
		state.TargetHighlightAmount = pointerHovered
			? AdventureUpgradeOverlayHoverHighlightAmount
			: (focused ? AdventureUpgradeOverlayFocusHighlightAmount : 0.0f);

		state.CardFrame.PivotOffset = button.Size * 0.5f;
		state.CardFrame.ZIndex = pointerHovered ? 3 : 0;
		button.ZIndex = pointerHovered ? 2 : 0;
	}

	private static Vector2 GetAdventureUpgradeCardFrameOffset(int optionIndex)
	{
		return Vector2.Zero;
	}

	private static float GetAdventureUpgradeCardFrameRotation(int optionIndex)
	{
		return 0.0f;
	}

	private void RegisterAdventureUpgradeOverlayCardMotionState(
		Button button,
		Control cardFrame,
		AdventureUpgradeChoiceArt choiceArt,
		Color baseModulate)
	{
		_adventureUpgradeOverlayCardMotionStates[button.GetInstanceId()] =
			new AdventureUpgradeOverlayCardMotionState(button, cardFrame, choiceArt, baseModulate);
	}

	private bool TryGetAdventureUpgradeOverlayCardMotionState(
		Button button,
		out AdventureUpgradeOverlayCardMotionState state)
	{
		if (_adventureUpgradeOverlayCardMotionStates.TryGetValue(button.GetInstanceId(), out AdventureUpgradeOverlayCardMotionState? existingState))
		{
			state = existingState;
			return true;
		}

		state = null!;
		return false;
	}

	private void SetAdventureUpgradeOverlayPointerHover(Button button, bool hovered)
	{
		if (!TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return;
		}

		state.PointerHovered = hovered;
		if (hovered && !state.RevealCompleted)
		{
			state.RevealCompleted = true;
			state.InteractionUnlocked = true;
			state.CardFrame.Position = state.BasePosition;
			state.CardFrame.Scale = state.BaseScale;
			state.CardFrame.RotationDegrees = state.BaseRotationDegrees;
		}

		RefreshAdventureUpgradeOverlayHighlight(button);
		if (!IsInstanceValid(state.CardFrame))
		{
			return;
		}

		Vector2 immediatePositionTarget = hovered ? state.TargetPosition : state.BasePosition;
		Vector2 immediateScaleTarget = hovered ? state.TargetScale : state.BaseScale;
		state.CardFrame.Position = state.CardFrame.Position.Lerp(immediatePositionTarget, 0.28f);
		state.CardFrame.Scale = state.CardFrame.Scale.Lerp(immediateScaleTarget, 0.28f);
	}

	private void InitializeAdventureUpgradeSelectionOverlayReveal(
		Control selectionOverlay,
		ColorRect backdrop,
		Panel selectionPanel,
		TextureRect selectionHalo,
		int visibleOptionCount)
	{
		_adventureUpgradeOverlayRevealState = new AdventureUpgradeOverlayRevealState(
			selectionOverlay,
			backdrop,
			selectionPanel,
			selectionHalo,
			visibleOptionCount);
		SetAdventureUpgradeOverlayAlpha(backdrop, _adventureUpgradeOverlayRevealState.BackdropBaseModulate, 0.0f);
		SetAdventureUpgradeOverlayAlpha(selectionPanel, _adventureUpgradeOverlayRevealState.SelectionPanelBaseModulate, 0.0f);
		SetAdventureUpgradeOverlayAlpha(selectionHalo, _adventureUpgradeOverlayRevealState.SelectionHaloBaseModulate, 0.0f);
	}

	private void InitializeAdventureUpgradeOverlayCardReveal(Button button, int optionIndex, int visibleOptionCount)
	{
		if (!TryGetAdventureUpgradeOverlayCardMotionState(button, out AdventureUpgradeOverlayCardMotionState state))
		{
			return;
		}

		state.PointerHovered = false;
		state.BasePosition = GetAdventureUpgradeCardFrameOffset(optionIndex);
		state.BaseScale = Vector2.One * AdventureUpgradeOverlayRestScale;
		state.BaseRotationDegrees = GetAdventureUpgradeCardFrameRotation(optionIndex);
		state.StartPosition = state.BasePosition + new Vector2(0.0f, AdventureUpgradeOverlayRevealStartYOffset);
		state.StartScale = Vector2.One * AdventureUpgradeOverlayRevealStartScale;
		state.StartRotationDegrees = state.BaseRotationDegrees;
		state.RevealDelaySeconds = GetAdventureUpgradeOverlayRevealDelay(optionIndex, visibleOptionCount);
		state.RevealElapsedSeconds = 0.02f;
		state.RevealCompleted = false;
		state.InteractionUnlocked = false;
		state.TargetPosition = state.BasePosition;
		state.TargetScale = state.BaseScale;
		state.TargetRotationDegrees = state.BaseRotationDegrees;
		state.TargetHighlightAmount = 0.0f;
		state.CardFrame.Position = state.StartPosition;
		state.CardFrame.Scale = state.StartScale;
		state.CardFrame.RotationDegrees = state.StartRotationDegrees;
		state.CardFrame.Modulate = CreateAdventureUpgradeOverlayAlphaColor(state.BaseModulate, 0.0f);
		UpdateAdventureUpgradeOverlayCardReveal(state, 0.0);
		state.ChoiceArt.SetHighlightAmount(0.0f);
		button.ReleaseFocus();
		button.Disabled = true;
		button.FocusMode = Control.FocusModeEnum.None;
	}

	private void UnlockAdventureUpgradeSelectionOverlayInteraction()
	{
		if (_adventureUpgradeOverlayRevealState == null
			|| _adventureUpgradeOverlayRevealState.InteractionUnlocked)
		{
			return;
		}

		_adventureUpgradeOverlayRevealState.InteractionUnlocked = true;
		foreach (AdventureUpgradeOverlayCardMotionState state in _adventureUpgradeOverlayCardMotionStates.Values)
		{
			if (!IsInstanceValid(state.Button) || !state.Button.Visible)
			{
				continue;
			}

			state.InteractionUnlocked = true;
			state.Button.Disabled = false;
			state.Button.FocusMode = Control.FocusModeEnum.All;
			RefreshAdventureUpgradeOverlayHighlight(state.Button);
		}

		CallDeferred(nameof(FocusAdventureUpgradeSelectionOverlayDefaultButton));
	}

	private void ResetAdventureUpgradeOverlayCardMotionStates()
	{
		_adventureUpgradeOverlayRevealState = null;
		_adventureUpgradeOverlayCardMotionStates.Clear();
	}

	private void UpdateAdventureUpgradeOverlayCardMotion(double delta)
	{
		UpdateAdventureUpgradeSelectionOverlayReveal(delta);

		if (_adventureUpgradeOverlayCardMotionStates.Count == 0)
		{
			return;
		}

		float motionWeight = ComputeAdventureUpgradeOverlaySmoothingWeight(delta, AdventureUpgradeOverlayMotionSmoothSpeed);
		float highlightWeight = ComputeAdventureUpgradeOverlaySmoothingWeight(delta, AdventureUpgradeOverlayHighlightSmoothSpeed);
		List<ulong> staleStateIds = new();

		foreach (KeyValuePair<ulong, AdventureUpgradeOverlayCardMotionState> pair in _adventureUpgradeOverlayCardMotionStates)
		{
			AdventureUpgradeOverlayCardMotionState state = pair.Value;
			if (!IsInstanceValid(state.Button) || !IsInstanceValid(state.CardFrame) || !IsInstanceValid(state.ChoiceArt))
			{
				staleStateIds.Add(pair.Key);
				continue;
			}

			state.CardFrame.PivotOffset = state.Button.Size * 0.5f;
			UpdateAdventureUpgradeOverlayCardReveal(state, delta);

			if (!state.RevealCompleted)
			{
				continue;
			}

			state.CardFrame.Position = state.CardFrame.Position.Lerp(state.TargetPosition, motionWeight);
			state.CardFrame.Scale = state.CardFrame.Scale.Lerp(state.TargetScale, motionWeight);
			state.CardFrame.RotationDegrees = Mathf.Lerp(state.CardFrame.RotationDegrees, state.TargetRotationDegrees, motionWeight);
			state.CardFrame.Modulate = state.BaseModulate;
			state.ChoiceArt.SetHighlightAmount(Mathf.Lerp(state.ChoiceArt.HighlightAmount, state.TargetHighlightAmount, highlightWeight));

			if (state.CardFrame.Position.DistanceTo(state.TargetPosition) <= AdventureUpgradeOverlayMotionSnapDistance)
			{
				state.CardFrame.Position = state.TargetPosition;
			}

			if (state.CardFrame.Scale.DistanceTo(state.TargetScale) <= AdventureUpgradeOverlayMotionSnapDistance)
			{
				state.CardFrame.Scale = state.TargetScale;
			}

			if (Mathf.Abs(state.CardFrame.RotationDegrees - state.TargetRotationDegrees) <= 0.02f)
			{
				state.CardFrame.RotationDegrees = state.TargetRotationDegrees;
			}
		}

		foreach (ulong staleStateId in staleStateIds)
		{
			_adventureUpgradeOverlayCardMotionStates.Remove(staleStateId);
		}

		if (AreAdventureUpgradeOverlayCardsFullyRevealed())
		{
			UnlockAdventureUpgradeSelectionOverlayInteraction();
		}
	}

	private void UpdateAdventureUpgradeSelectionOverlayReveal(double delta)
	{
		if (_adventureUpgradeOverlayRevealState == null)
		{
			return;
		}

		if (!IsInstanceValid(_adventureUpgradeOverlayRevealState.Root)
			|| !IsInstanceValid(_adventureUpgradeOverlayRevealState.Backdrop)
			|| !IsInstanceValid(_adventureUpgradeOverlayRevealState.SelectionPanel)
			|| !IsInstanceValid(_adventureUpgradeOverlayRevealState.SelectionHalo))
		{
			_adventureUpgradeOverlayRevealState = null;
			return;
		}

		_adventureUpgradeOverlayRevealState.ElapsedSeconds += (float)delta;
		float backdropAlpha = Mathf.Clamp(_adventureUpgradeOverlayRevealState.ElapsedSeconds / AdventureUpgradeOverlayBackdropFadeDurationSeconds, 0.0f, 1.0f);
		float panelAlpha = Mathf.Clamp(_adventureUpgradeOverlayRevealState.ElapsedSeconds / AdventureUpgradeOverlayPanelFadeDurationSeconds, 0.0f, 1.0f);

		SetAdventureUpgradeOverlayAlpha(
			_adventureUpgradeOverlayRevealState.Backdrop,
			_adventureUpgradeOverlayRevealState.BackdropBaseModulate,
			backdropAlpha);
		SetAdventureUpgradeOverlayAlpha(
			_adventureUpgradeOverlayRevealState.SelectionPanel,
			_adventureUpgradeOverlayRevealState.SelectionPanelBaseModulate,
			panelAlpha);
		SetAdventureUpgradeOverlayAlpha(
			_adventureUpgradeOverlayRevealState.SelectionHalo,
			_adventureUpgradeOverlayRevealState.SelectionHaloBaseModulate,
			panelAlpha);
	}

	private void UpdateAdventureUpgradeOverlayCardReveal(
		AdventureUpgradeOverlayCardMotionState state,
		double delta)
	{
		if (state.RevealCompleted)
		{
			return;
		}

		state.RevealElapsedSeconds += (float)delta;
		float revealTime = state.RevealElapsedSeconds - state.RevealDelaySeconds;
		if (revealTime <= 0.0f)
		{
			state.CardFrame.Position = state.StartPosition;
			state.CardFrame.Scale = state.StartScale;
			state.CardFrame.RotationDegrees = state.StartRotationDegrees;
			state.CardFrame.Modulate = CreateAdventureUpgradeOverlayAlphaColor(state.BaseModulate, 0.0f);
			state.ChoiceArt.SetHighlightAmount(0.0f);
			return;
		}

		float rawRevealT = Mathf.Clamp(revealTime / AdventureUpgradeOverlayRevealDurationSeconds, 0.0f, 1.0f);
		float easedRevealT = EaseOutAdventureUpgradeOverlayReveal(rawRevealT);
		float overshootWeight = ComputeAdventureUpgradeOverlayRevealOvershoot(rawRevealT);
		Vector2 revealPosition = state.StartPosition.Lerp(state.BasePosition, easedRevealT)
			+ new Vector2(0.0f, -AdventureUpgradeOverlayHoverLift * 0.9f * overshootWeight);
		Vector2 revealScale = state.StartScale.Lerp(state.BaseScale, easedRevealT)
			+ Vector2.One * ((AdventureUpgradeOverlayHoverScale - AdventureUpgradeOverlayRestScale) * overshootWeight);
		if (state.PointerHovered)
		{
			revealPosition = revealPosition.Lerp(state.TargetPosition, rawRevealT);
			revealScale = revealScale.Lerp(state.TargetScale, rawRevealT);
		}

		state.CardFrame.Position = revealPosition;
		state.CardFrame.Scale = revealScale;
		state.CardFrame.RotationDegrees = Mathf.Lerp(state.StartRotationDegrees, state.BaseRotationDegrees, easedRevealT);
		state.CardFrame.Modulate = CreateAdventureUpgradeOverlayAlphaColor(state.BaseModulate, rawRevealT);
		state.ChoiceArt.SetHighlightAmount(Mathf.Lerp(0.0f, state.TargetHighlightAmount, easedRevealT));

		if (rawRevealT < 1.0f)
		{
			return;
		}

		state.RevealCompleted = true;
		state.CardFrame.Position = state.BasePosition;
		state.CardFrame.Scale = state.BaseScale;
		state.CardFrame.RotationDegrees = state.BaseRotationDegrees;
		state.CardFrame.Modulate = state.BaseModulate;
	}

	private bool AreAdventureUpgradeOverlayCardsFullyRevealed()
	{
		if (_adventureUpgradeOverlayRevealState == null
			|| _adventureUpgradeOverlayRevealState.InteractionUnlocked
			|| _adventureUpgradeOverlayRevealState.VisibleCardCount == 0)
		{
			return false;
		}

		int revealedCount = 0;
		foreach (AdventureUpgradeOverlayCardMotionState state in _adventureUpgradeOverlayCardMotionStates.Values)
		{
			if (!IsInstanceValid(state.Button) || !state.Button.Visible)
			{
				continue;
			}

			if (!state.RevealCompleted)
			{
				return false;
			}

			revealedCount++;
		}

		return revealedCount >= _adventureUpgradeOverlayRevealState.VisibleCardCount;
	}

	private static float GetAdventureUpgradeOverlayRevealDelay(int optionIndex, int visibleOptionCount)
	{
		int revealOrder = 0;
		foreach (int candidateIndex in AdventureUpgradeOverlayPreferredOrder)
		{
			if (candidateIndex >= visibleOptionCount)
			{
				continue;
			}

			if (candidateIndex == optionIndex)
			{
				return revealOrder * AdventureUpgradeOverlayRevealStaggerSeconds;
			}

			revealOrder++;
		}

		return 0.0f;
	}

	private static void SetAdventureUpgradeOverlayAlpha(CanvasItem item, Color baseModulate, float alphaMultiplier)
	{
		item.Modulate = CreateAdventureUpgradeOverlayAlphaColor(baseModulate, alphaMultiplier);
	}

	private static Color CreateAdventureUpgradeOverlayAlphaColor(Color baseModulate, float alphaMultiplier)
	{
		Color color = baseModulate;
		color.A = baseModulate.A * Mathf.Clamp(alphaMultiplier, 0.0f, 1.0f);
		return color;
	}

	private static float EaseOutAdventureUpgradeOverlayReveal(float value)
	{
		float inverse = 1.0f - Mathf.Clamp(value, 0.0f, 1.0f);
		return 1.0f - inverse * inverse * inverse;
	}

	private static float ComputeAdventureUpgradeOverlayRevealOvershoot(float value)
	{
		float clamped = Mathf.Clamp(value, 0.0f, 1.0f);
		return Mathf.Sin(clamped * Mathf.Pi);
	}

	private static float ComputeAdventureUpgradeOverlaySmoothingWeight(double delta, float speed)
	{
		return 1.0f - Mathf.Exp(-speed * (float)delta);
	}

	private static int ExtractAdventureUpgradeOptionIndex(Button button)
	{
		string name = button.Name.ToString();
		if (!name.StartsWith("Option") || !name.EndsWith("Button"))
		{
			return 0;
		}

		string digits = name["Option".Length..^"Button".Length];
		return int.TryParse(digits, out int optionNumber)
			? Mathf.Max(0, optionNumber - 1)
			: 0;
	}

	private bool ShouldDelayWinnerForAdventureVictoryUpgrade(int winnerPlayerIndex)
	{
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem()
			|| winnerPlayerIndex != MainPlayerIndex
			|| runState?.ShouldGrantVictoryUpgrade != true)
		{
			return false;
		}

		if (_pendingAdventureWinnerPlayerIndex == winnerPlayerIndex)
		{
			return true;
		}

		_pendingAdventureWinnerPlayerIndex = winnerPlayerIndex;
		return true;
	}

	private bool ShouldQueueAdventureBigCaptureUpgrade(int playerIndex, int totalCapturedCardCount)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| !AdventureUpgradeChoiceRules.ShouldGrantBigCapture(totalCapturedCardCount))
		{
			return false;
		}

		return true;
	}

	private static string GetFirstMainAdventureAvatarId()
	{
		return CharacterAvatarCatalog.GetAdventureAvatarIdForStage(FirstMainAdventureAvatarStageNumber);
	}
}
