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
	private const int FirstMainAdventureStageIndex = 1;

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

	private AdventureUpgradeGrantCommitResult GrantAdventureUpgrade(
		string upgradeId,
		AdventureUpgradeTriggerSource triggerSource)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			WarnMissingAdventureRunSessionStore("record an acquired adventure upgrade");
			return new AdventureUpgradeGrantCommitResult(
				AdventureRunWriteResult.SaveFailed,
				AdventureUpgradeAcquireResult.DeferredManifestUnavailable,
				default,
				null);
		}

		return store.GrantUpgrade(upgradeId, triggerSource);
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

	private void ResetAdventureBattleScopedStateForNewBattle()
	{
		ClearAdventureBattleScopedState(prepareNewBoard: true);
	}

	private void ClearAdventureBattleScopedStateForBattleEnd()
	{
		ClearAdventureBattleScopedState(prepareNewBoard: false);
	}

	private void ClearAdventureBattleScopedState(bool prepareNewBoard)
	{
		_adventureMainPlayerSuccessfulCapturesThisTurn = 0;
		_adventureMainPlayerCaptureCountThisBattle = 0;
		_adventureQuestionRejudgeUsedThisBattle = false;
		_adventureMainPlayerHalfHealthRescueUsed = false;
		_adventureMainPlayerQuarterHealthRescueUsed = false;
		ResetAdventureUpgradeBoardState();
		if (prepareNewBoard)
		{
			ResetAdventureInteractionStateForNewBoard();
		}
		else
		{
			ClearAdventureInteractionStateForBattleEnd();
		}

		MarkActivePlayerStatusTooltipForRefresh();
	}

	private async Task<bool> PrepareAdventureBattleForDealAsync()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return true;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store?.HasActiveRun != true)
		{
			return false;
		}
		AdventureSaveGateResult<AdventureRunWriteResult> gate =
			await ExecuteAdventureSaveGateAsync(
				store.ResetBattleScopedEffects,
				result => result == AdventureRunWriteResult.Succeeded,
				result => result == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to reset adventure battle effects: {gate.Result}");
			return false;
		}
		return true;
	}

	private async Task<bool> DealCardsToPlayersWithAdventureGateAsync(
		bool playOpeningDealAnimation = false,
		bool playOpeningIntroAnimation = false)
	{
		if (!await PrepareAdventureBattleForDealAsync())
		{
			return false;
		}
		DealCardsToPlayers(playOpeningDealAnimation, playOpeningIntroAnimation);
		return true;
	}

	private async Task TryResolveAdventurePendingExtraBaseCardActionForAiAsync(int turnSequenceGeneration)
	{
		if (!HasPendingAdventureExtraBaseCardAction()
			|| _currentTurnPlayerIndex != MainPlayerIndex
			|| IsLocalSeatHumanConnected())
		{
			return;
		}

		if (_playerHands.Count <= MainPlayerIndex || _playerHands[MainPlayerIndex].Count == 0)
		{
			bool resumeNormalTurn = AbortAdventureExtraActionRootChain("no_base_card_available");
			await SettleAdventureExtraActionChainAbortAsync(
				resumeNormalTurn,
				turnSequenceGeneration);
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
		if (effectResult.ShuffleTableAfterMiss && GetMovableTableShuffleCardCount() >= 2)
		{
			List<TableCard> shuffledTableCards = BuildShuffledTableCards();
			bool shuffleCompleted = await PlayTableCardsShuffleAnimationAsync(shuffledTableCards, turnSequenceGeneration);
			if (!shuffleCompleted || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return false;
			}

			ApplyShuffledTableCards(shuffledTableCards);
		}

		if (effectResult.InsertPlayedCardAtLeftmost)
		{
			int currentPlayedTableCardIndex =
				_tableCards.IndexOf(playedTableCard);
			if (currentPlayedTableCardIndex > 0)
			{
				_tableCards.RemoveAt(currentPlayedTableCardIndex);
				_tableCards.Insert(0, playedTableCard);
			}
		}
		RenderTableCards();

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
			TryGrantAdventureMainPlayerExtraBaseCardAction(
				AdventureInterceptBeatUpgradeId,
				resumeNormalTurnAfterChain: true);
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

	private async Task ApplyAdventureQuestionRewardEffects(
		int playerIndex,
		AdventureQuestionRewardEffectResult? effectResult,
		int turnSequenceGeneration)
	{
		await ApplyAdventureCaptureRewardEffectsAsync(
			playerIndex,
			effectResult,
			postCaptureEffects: null,
			turnSequenceGeneration);
	}

	private async Task ApplyAdventureCaptureRewardEffectsAsync(
		int playerIndex,
		AdventureQuestionRewardEffectResult? questionRewardEffects,
		AdventurePostCaptureEffectResult? postCaptureEffects,
		int turnSequenceGeneration)
	{
		if (playerIndex != MainPlayerIndex
			|| (!questionRewardEffects.HasValue
				&& !postCaptureEffects.HasValue))
		{
			return;
		}

		AdventureCaptureRewardApplicationPlan plan =
			AdventureUpgradeEffectService.BuildCaptureRewardApplicationPlan(
				questionRewardEffects,
				postCaptureEffects,
				GetTotalHandCardCount(playerIndex),
				MaxTotalHandCards,
				_playerDecks[playerIndex].Count);
		if (plan.HealAmount > 0)
		{
			if (!await ApplyAdventureMainPlayerHealingAsync(plan.HealAmount))
			{
				return;
			}
		}

		bool drewAny = false;
		for (int drawIndex = 0; drawIndex < plan.DrawCount; drawIndex++)
		{
			DrawnHandCard? drawnCard = DrawCardFromDeckToHand(playerIndex);
			if (!drawnCard.HasValue)
			{
				break;
			}

			drewAny = true;
			await PlayReplacementDrawAnimationAsync(
				playerIndex,
				drawnCard,
				turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration
				|| IsGameOver())
			{
				return;
			}
		}
		if (drewAny)
		{
			RenderPlayerCardPiles();
			RenderPlayerHands();
		}

		if (plan.BonusDirectDamage > 0)
		{
			await PlayAdventureAvatarAttackAsync(
				playerIndex,
				GetAdventureDamageTargetPlayerIndex(playerIndex),
				turnSequenceGeneration,
				() => AddPlayerScore(
					playerIndex,
					plan.BonusDirectDamage));
			if (turnSequenceGeneration != _turnSequenceGeneration
				|| IsGameOver())
			{
				return;
			}
		}

		if (plan.RevealRemainingHiddenCards)
		{
			await RevealRemainingHiddenTableCardsAsync(
				turnSequenceGeneration);
		}
	}

	private Task ApplyAdventureUpgradeAcquisitionEffectsAsync(
		AdventureUpgradeGrantCommitResult commitResult,
		int turnSequenceGeneration)
	{
		if (commitResult.WriteResult != AdventureRunWriteResult.Succeeded)
		{
			return Task.CompletedTask;
		}

		if (commitResult.HealingResult.HasValue)
		{
			_playerScores[MainPlayerIndex] = commitResult.HealingResult.Value.CurrentHealth;
			UpdatePlayerScoreMeter(MainPlayerIndex, animate: true);
			RefreshGameplayTensionMusic();
		}

		if (commitResult.EffectResult.ImmediateDrawCount > 0)
		{
			DrawCardsFromDeckToHandForAdventureEffect(
				MainPlayerIndex,
				commitResult.EffectResult.ImmediateDrawCount);
		}

		return Task.CompletedTask;
	}

	private AdventureCaptureDamageResult PreviewAdventureCaptureDamage(
		int playerIndex,
		AdventureCaptureDamageInput damageInput)
	{
		AdventureCaptureDamageInput normalizedInput =
			FreezeAdventureCaptureDamageInput(playerIndex, damageInput);
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return new AdventureCaptureDamageResult(
				Math.Max(0, normalizedInput.BaseDamage + normalizedInput.FlatDamageDelta));
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return new AdventureCaptureDamageResult(
				Math.Max(0, normalizedInput.BaseDamage + normalizedInput.FlatDamageDelta));
		}

		return AdventureUpgradeEffectService.ApplyCaptureDamage(
			runState.EffectState.Clone(),
			runState.GetAcquiredUpgradeIds(),
			normalizedInput);
	}

	private AdventureCaptureDamageInput FreezeAdventureCaptureDamageInput(
		int playerIndex,
		AdventureCaptureDamageInput damageInput)
	{
		int targetPlayerIndex = adventure_mode
			? GetAdventureDamageTargetPlayerIndex(playerIndex)
			: playerIndex;
		int targetMaxHealth = damageInput.TargetMaxHealth > 0
			? damageInput.TargetMaxHealth
			: targetPlayerIndex >= 0 && targetPlayerIndex < _playerScores.Length
				? Math.Max(1, GetScoreLimit(targetPlayerIndex))
				: 1;
		AdventureCaptureDamageInput normalizedInput = damageInput with
		{
			CapturerIsMainPlayer = playerIndex == MainPlayerIndex,
			TargetMaxHealth = targetMaxHealth,
		};
		if (normalizedInput.FrozenModifiers.HasValue)
		{
			return normalizedInput;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true
			? normalizedInput with
			{
				FrozenModifiers = AdventureUpgradeEffectService
					.FreezeCaptureDamageModifiers(
						runState.EffectState,
						runState.GetAcquiredUpgradeIds()),
			}
			: normalizedInput;
	}

	private readonly record struct AdventureMissSceneResolution(
		bool Succeeded,
		AdventureMainPlayerMissEffectResult EffectResult,
		AdventurePostCaptureEffectResult RecoveryEffectResult);

	private async Task<AdventureMissSceneResolution> ResolveAdventureMissActionAsync(
		int playerIndex,
		string playedCardRank,
		IReadOnlyCollection<string> triggeredAttachedItemCardIds)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return new AdventureMissSceneResolution(true, default, default);
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store?.HasActiveRun != true)
		{
			return new AdventureMissSceneResolution(false, default, default);
		}

		AdventureSaveGateResult<AdventureMissCommitResult> gate =
			await ExecuteAdventureSaveGateAsync(
				() => store.ResolveMissAction(new AdventureMissCommitInput(
					playedCardRank,
					_tableCards.Count,
					triggeredAttachedItemCardIds)),
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to commit adventure miss action: {gate.Result.WriteResult}");
			return new AdventureMissSceneResolution(false, default, default);
		}

		AdventureMissCommitResult commitResult = gate.Result;
		if (commitResult.HealingResult.HasValue)
		{
			_playerScores[MainPlayerIndex] = commitResult.HealingResult.Value.CurrentHealth;
			UpdatePlayerScoreMeter(MainPlayerIndex, animate: true);
			RefreshGameplayTensionMusic();
		}
		if (commitResult.CommitBandageAtRealTurnEnd)
		{
			_state.AdventureExtraActionQueue.RegisterMissBandageForRealTurnEnd();
		}
		RenderPlayerHands();
		return new AdventureMissSceneResolution(
			true,
			commitResult.EffectResult,
			commitResult.RecoveryEffectResult);
	}

	private async Task ApplyAdventureMainPlayerTurnStartEffectsAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store?.HasActiveRun != true)
		{
			return;
		}

		AdventureSaveGateResult<AdventureTurnStartCommitResult> gate =
			await ExecuteAdventureSaveGateAsync(
				store.ResolveTurnStartEffects,
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to commit adventure turn-start effects: {gate.Result.WriteResult}");
			return;
		}
		AdventureTurnStartCommitResult commitResult = gate.Result;
		if (commitResult.HealingResult.HasValue)
		{
			_playerScores[MainPlayerIndex] = commitResult.HealingResult.Value.CurrentHealth;
			UpdatePlayerScoreMeter(MainPlayerIndex, animate: true);
			RefreshGameplayTensionMusic();
		}

		bool drewAny = false;
		for (int drawIndex = 0; drawIndex < commitResult.EffectResult.DrawCount; drawIndex++)
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
		await ApplyAdventureCaptureRewardEffectsAsync(
			playerIndex,
			questionRewardEffects: null,
			effectResult,
			turnSequenceGeneration);
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
			await WaitForBoardDelayAsync(TableCardRevealPauseSeconds);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}
		}
	}

	private AdventureClubCaptureCommitInput BuildAdventureClubCaptureCommitInput(
		int playerIndex,
		CaptureResolutionContext captureContext)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return default;
		}

		var shortReturnCandidateIds = new HashSet<long>();
		foreach (AdventureCapturedCardEntry entry in captureContext.CapturedCards)
		{
			if (entry.BaseCardInstanceId > 0)
			{
				shortReturnCandidateIds.Add(entry.BaseCardInstanceId);
			}
			if (shortReturnCandidateIds.Count == 2)
			{
				break;
			}
		}
		bool playedCardWasBaseCard = captureContext.Action.PlayedCardInstanceId > 0;
		var combinedReturnCandidateIds = new HashSet<long>(shortReturnCandidateIds);
		if (playedCardWasBaseCard)
		{
			combinedReturnCandidateIds.Add(captureContext.Action.PlayedCardInstanceId);
		}

		return new AdventureClubCaptureCommitInput(
			captureContext.CompleteBaseCardCount,
			shortReturnCandidateIds.Count,
			combinedReturnCandidateIds.Count,
			Math.Max(0, MaxTotalHandCards - GetTotalHandCardCount(playerIndex)),
			playedCardWasBaseCard);
	}

	private async Task ApplyAdventureClubCaptureReturnsAsync(
		int playerIndex,
		CaptureResolutionContext captureContext,
		AdventureClubCaptureEffectResult effectResult,
		int turnSequenceGeneration)
	{
		if (!ShouldUseAdventureUpgradeSystem() || playerIndex != MainPlayerIndex)
		{
			return;
		}

		var shortReturnCandidates = new List<PlayingCard>(2);
		var shortReturnCandidateIds = new HashSet<long>();
		foreach (AdventureCapturedCardEntry entry in captureContext.CapturedCards)
		{
			if (entry.BaseCardInstanceId <= 0
				|| !entry.TableCard.Card.HasValue
				|| !shortReturnCandidateIds.Add(entry.BaseCardInstanceId))
			{
				continue;
			}

			shortReturnCandidates.Add(entry.TableCard.Card.Value);
			if (shortReturnCandidates.Count == 2)
			{
				break;
			}
		}

		PlayingCard? playedCardForReturn = captureContext.Action.PlayedCardInstanceId > 0
			? captureContext.Action.PlayedCard
			: null;
		var combinedReturnCandidateIds = new HashSet<long>(shortReturnCandidateIds);
		if (playedCardForReturn.HasValue)
		{
			combinedReturnCandidateIds.Add(playedCardForReturn.Value.InstanceId);
		}

		if (!effectResult.ReturnPlayedCardToHand && !effectResult.ReturnTwoCapturedCardsToHand)
		{
			return;
		}

		var cardsToReturn = new List<PlayingCard>();
		var queuedCardIds = new HashSet<long>();
		if (effectResult.ReturnTwoCapturedCardsToHand)
		{
			foreach (PlayingCard card in shortReturnCandidates)
			{
				if (queuedCardIds.Add(card.InstanceId))
				{
					cardsToReturn.Add(card);
				}
			}
		}

		if (effectResult.ReturnPlayedCardToHand
			&& playedCardForReturn.HasValue
			&& queuedCardIds.Add(playedCardForReturn.Value.InstanceId))
		{
			cardsToReturn.Add(playedCardForReturn.Value);
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

	private int FindLastPlayerDeckCardIndex(int playerIndex, PlayingCard card)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return -1;
		}

		List<PlayingCard> deck = _playerDecks[playerIndex];
		for (int index = deck.Count - 1; index >= 0; index--)
		{
			if (deck[index].InstanceId == card.InstanceId)
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

		AdventureRunWriteResult result = store.ClearRun();
		if (result != AdventureRunWriteResult.Succeeded)
		{
			GD.PushWarning($"Failed to clear adventure upgrade run: {result}");
		}
	}

	private async Task<bool> InitializeAdventureUpgradeRunForCurrentBoardAsync()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return true;
		}

		if (!AdventureCampaignCatalog.TryGetMainStageIndex(adventure_enemy_avatar_id, out int mainStageIndex))
		{
			return false;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null)
		{
			return false;
		}
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			AdventureSaveGateResult<AdventureRunWriteResult> gate =
				await ExecuteAdventureSaveGateAsync(
					() => store.BeginNewMainRun(mainStageIndex, adventure_enemy_avatar_id),
					result => result == AdventureRunWriteResult.Succeeded,
					result => result == AdventureRunWriteResult.SaveFailed);
			if (!gate.Succeeded)
			{
				GD.PushWarning($"Failed to initialize adventure run for current board: {gate.Result}");
				return false;
			}
			return true;
		}

		if (runState.MainStageIndex == mainStageIndex)
		{
			return true;
		}

		AdventureSaveGateResult<AdventureRunWriteResult> advanceGate =
			await ExecuteAdventureSaveGateAsync(
				() => store.AdvanceToMainStage(mainStageIndex, adventure_enemy_avatar_id),
				result => result == AdventureRunWriteResult.Succeeded,
				result => result == AdventureRunWriteResult.SaveFailed);
		if (!advanceGate.Succeeded)
		{
			GD.PushWarning($"Failed to align adventure run with current board: {advanceGate.Result}");
			return false;
		}
		return true;
	}

	private bool ShouldUseAdventureUpgradeSystem()
	{
		if (!_gameModePolicy.IsAdventureMain)
		{
			return false;
		}

		return AdventureCampaignCatalog.TryGetMainStageIndex(adventure_enemy_avatar_id, out _);
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
		return AdventureCampaignCatalog.GetMainAvatarId(FirstMainAdventureStageIndex);
	}
}
