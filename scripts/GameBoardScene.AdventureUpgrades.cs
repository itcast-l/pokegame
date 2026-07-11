using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct AdventureEnemyNextBaseCardModifiers(
		bool FacesUp,
		int TableInsertIndex,
		int CaptureDamageDelta);
	private const int FirstMainAdventureAvatarStageNumber = 3;

	private AdventureSuitUpgradeCatalog _adventureUpgradeCatalog = default!;
	private TableCard? _adventureEnemyNextInsertAnchorTableCard;
	private TableCard? _adventureTraceHuntMarkedTableCard;
	private TableCard? _adventureHuntOrderMarkedTableCard;
	private TableCard? _adventureCanopyRevealAnchorTableCard;

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
		ClearAdventureTraceHuntMarkerIntro();
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
			SetAdventureTraceHuntMarkedTableCard(null, playIntro: false);
			return;
		}

		SetAdventureTraceHuntMarkedTableCard(
			FindRightmostRemainingOldBaseTableCard(prePlayTableCards),
			playIntro: true);
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
			bool shuffleCompleted = await PlayTableCardsShuffleAnimationAsync(shuffledTableCards, turnSequenceGeneration);
			if (!shuffleCompleted || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
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

	private void ApplyAdventureEnemyCaptureEffects(int playerIndex)
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

		AdventureEnemyCaptureEffectResult effectResult = AdventureUpgradeEffectService.ResolveEnemyCaptureEffects(
			runState.GetAcquiredUpgradeIds());
		if (!effectResult.NextCardFacesUp && effectResult.InsertMode == AdventureEnemyNextCardInsertMode.None)
		{
			return;
		}

		_adventureEnemyNextBaseCardFacesUp = effectResult.NextCardFacesUp;
		_adventureEnemyNextCardInsertMode = effectResult.InsertMode;
		_adventureEnemyNextInsertAnchorTableCard = null;
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

	private async Task ApplyAdventureQuestionRewardEffects(int playerIndex, AdventureQuestionRewardEffectResult? effectResult, int turnSequenceGeneration)
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
			await PlayAdventureAvatarAttackAsync(
				playerIndex,
				GetAdventureDamageTargetPlayerIndex(playerIndex),
				turnSequenceGeneration,
				() => AddPlayerScore(playerIndex, resolved.BonusDirectDamage));
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

		await ApplyAdventureQuestionRewardEffects(playerIndex, ResolveAdventureQuestionRewardEffects(playerIndex), turnSequenceGeneration);
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
		SetAdventureTraceHuntMarkedTableCard(null, playIntro: false);
		ClearAdventureTraceHuntMarkerIntro();
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
