using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private readonly record struct AdventureEnemyNextBaseCardModifiers(
		bool FacesUp,
		int TableInsertIndex,
		int CaptureDamageDelta);
	private const int FirstMainAdventureStageIndex = 1;

	private AdventureSuitUpgradeCatalog _adventureUpgradeCatalog = default!;
	private TableCard? GetPendingAdventureEnemyInsertAnchor() =>
		FindCurrentTableCard(CurrentMatchFlowState.PendingPlacementAnchorRuntimeId);
	private TableCard? GetAdventureTraceHuntMarkedTableCard() =>
		FindCurrentTableCard(CurrentMatchFlowState.CaptureMarkRuntimeId);
	private TableCard? GetAdventureHuntOrderMarkedTableCard() =>
		FindCurrentTableCard(CurrentMatchFlowState.TurnCaptureMarkRuntimeId);
	private TableCard? GetAdventureCanopyRevealAnchorTableCard() =>
		FindCurrentTableCard(CurrentMatchFlowState.SequentialRevealAnchorRuntimeId);
	private AdventureEnemyNextCardInsertMode GetPendingAdventureEnemyCardInsertMode() =>
		(AdventureEnemyNextCardInsertMode)(int)CurrentMatchFlowState.PendingCardPlacement;

	private TableCard? FindCurrentTableCard(long runtimeInstanceId)
	{
		if (runtimeInstanceId <= 0)
		{
			return null;
		}

		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.InstanceId == runtimeInstanceId)
			{
				return tableCard;
			}
		}

		return null;
	}

	private void LoadAdventureUpgradeCatalog()
	{
		_adventureUpgradeCatalog = AdventureSuitUpgradeCatalog.Load(this);
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (_gameModePolicy.CanReadAdventureRun
			&& _authorityRuleSession != null
			&& store != null
			&& !store.BindAuthorityRuleSession(_authorityRuleSession))
		{
			GD.PushWarning("Failed to bind the adventure run to the board authority session.");
		}
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
		_isResolvingAdventureUpgradeSelection = false;
		ClearAdventureTraceHuntMarkerIntro();
		_adventureUpgradeResolutionTask = Task.CompletedTask;
		ResetAdventureUpgradeOverlayCardMotionStates();
		CancelAdventureUpgradeSelection();
		DisposeActiveSelectionOverlay();
	}

	private void WarnMissingAdventureRunSessionStore(string action)
	{
		GD.PushWarning($"AdventureRunSessionStore autoload is missing; cannot {action}.");
	}

	private AdventureRunReadModel? GetAdventureUpgradeRunState()
	{
		return GetAdventureRunSessionStore()?.GetReadModel();
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
		TryUpdateMatchFlowState(state => state with
		{
			SuccessfulCapturesThisTurn = 0,
			MainPlayerCapturesThisBattle = 0,
			QuestionRejudgeUsedThisBattle = false,
			HalfHealthRescueUsed = false,
			QuarterHealthRescueUsed = false,
			MainPlayerRealTurnActive = prepareNewBoard,
			MainPlayerRealTurnStartPending = false,
			PendingBaseCardFacesUp = false,
			PendingCardPlacement = PendingCardPlacementMode.None,
			PendingCaptureDamageDelta = 0,
			PendingPlacementAnchorRuntimeId = 0,
			CaptureMarkRuntimeId = 0,
			TurnCaptureMarkRuntimeId = 0,
			SequentialRevealAnchorRuntimeId = 0,
			PendingProgressionChoices = default,
			PendingWinnerSeat = -1,
		});
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
		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = false,
			PendingCardPlacement = PendingCardPlacementMode.None,
			PendingCaptureDamageDelta = 0,
			PendingPlacementAnchorRuntimeId = 0,
		});
	}

	private bool HasAdventureUpgrade(string upgradeId)
	{
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true && runState.HasAcquiredUpgrade(upgradeId);
	}

	private void MarkAdventureQuestionRejudgeUsed()
	{
		TryUpdateMatchFlowState(state => state with
		{
			QuestionRejudgeUsedThisBattle = true,
		});
	}

	private void RefreshAdventureHuntOrderMarkForMainPlayerTurn()
	{
		if (!ShouldUseAdventureUpgradeSystem() || !HasAdventureUpgrade("spades_hunt_order"))
		{
			TryUpdateMatchFlowState(state => state with
			{
				TurnCaptureMarkRuntimeId = 0,
			});
			return;
		}

		TableCard? target = FindLeftmostCurrentBaseTableCard();
		TryUpdateMatchFlowState(state => state with
		{
			TurnCaptureMarkRuntimeId = target?.InstanceId ?? 0,
		});
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
			if (shuffledTableCards.Count != _tableCards.Count)
			{
				return false;
			}

			bool animationCompleted = await PlayTableCardsShuffleAnimationAsync(
				shuffledTableCards,
				turnSequenceGeneration);
			if (!animationCompleted
				|| turnSequenceGeneration != _turnSequenceGeneration)
			{
				return false;
			}

			bool appliedShuffle = ApplyShuffledTableCards(shuffledTableCards);
			if (!appliedShuffle || IsGameOver())
			{
				return false;
			}
		}

		if (effectResult.InsertPlayedCardAtLeftmost)
		{
			int currentPlayedTableCardIndex =
				_tableCards.IndexOf(playedTableCard);
			if (currentPlayedTableCardIndex > 0)
			{
				if (!TrySubmitMoveTableCardRuleCommand(
					_currentTurnPlayerIndex,
					currentPlayedTableCardIndex,
					targetIndex: 0,
					playedTableCard.InstanceId,
					turnSequenceGeneration,
					out _))
				{
					return false;
				}
			}
		}
		RenderTableCards();

		if (effectResult.SetCanopyRevealAnchor)
		{
			TryUpdateMatchFlowState(state => state with
			{
				SequentialRevealAnchorRuntimeId = playedTableCard.InstanceId,
			});
		}

		return true;
	}

	private void ApplyAdventureCanopyRevealToNewTableCard(int playedTableCardIndex)
	{
		TableCard? revealAnchor = GetAdventureCanopyRevealAnchorTableCard();
		if (revealAnchor == null)
		{
			return;
		}

		int anchorIndex = _tableCards.IndexOf(revealAnchor);
		if (anchorIndex < 0)
		{
			TryUpdateMatchFlowState(state => state with
			{
				SequentialRevealAnchorRuntimeId = 0,
			});
			return;
		}

		if (playedTableCardIndex > anchorIndex
			&& playedTableCardIndex >= 0
			&& playedTableCardIndex < _tableCards.Count)
		{
			TableCard playedTableCard = _tableCards[playedTableCardIndex];
			TrySubmitSetTableCardsFaceUpRuleCommand(
				_currentTurnPlayerIndex,
				ImmutableArray.Create(new RuleTableCardReference(
					playedTableCardIndex,
					playedTableCard.InstanceId)),
				playedTableCard.IsFaceUp,
				faceUp: true,
				_turnSequenceGeneration,
				out _);
		}
	}

	private void RefreshAdventureCanopyAnchorAfterTableMutation()
	{
		long anchorRuntimeId = CurrentMatchFlowState.SequentialRevealAnchorRuntimeId;
		if (anchorRuntimeId > 0 && FindCurrentTableCard(anchorRuntimeId) == null)
		{
			TryUpdateMatchFlowState(state => state with
			{
				SequentialRevealAnchorRuntimeId = 0,
			});
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return;
		}

		bool hadOldTableCardsBeforeMiss = prePlayTableCards.Count > 0;
		AdventureEnemyMissEffectResult effectResult = runState.ResolveEnemyMissEffects(
			_tableCards.Count,
			hadOldTableCardsBeforeMiss);
		TableCard? insertionAnchor = ResolveAdventureEnemyNextInsertAnchorAfterMiss(
			effectResult.InsertMode,
			playedTableCardIndex);
		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = effectResult.NextCardFacesUp,
			PendingCardPlacement = (PendingCardPlacementMode)(int)effectResult.InsertMode,
			PendingCaptureDamageDelta = effectResult.NextCaptureDamageDelta,
			PendingPlacementAnchorRuntimeId = insertionAnchor?.InstanceId ?? 0,
		});
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return;
		}

		AdventureEnemyCaptureEffectResult effectResult = runState.ResolveEnemyCaptureEffects();
		if (!effectResult.NextCardFacesUp && effectResult.InsertMode == AdventureEnemyNextCardInsertMode.None)
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = effectResult.NextCardFacesUp,
			PendingCardPlacement = (PendingCardPlacementMode)(int)effectResult.InsertMode,
			PendingPlacementAnchorRuntimeId = 0,
		});
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
		switch (GetPendingAdventureEnemyCardInsertMode())
		{
			case AdventureEnemyNextCardInsertMode.Leftmost:
				tableInsertIndex = 0;
				break;
			case AdventureEnemyNextCardInsertMode.BeforeTrackedOldest:
				TableCard? insertionAnchor = GetPendingAdventureEnemyInsertAnchor();
				tableInsertIndex = insertionAnchor != null
					? _tableCards.IndexOf(insertionAnchor)
					: -1;
				if (tableInsertIndex < 0 && _tableCards.Count > 0)
				{
					tableInsertIndex = 0;
				}
				break;
		}

		AdventureEnemyNextBaseCardModifiers modifiers = new(
			CurrentMatchFlowState.PendingBaseCardFacesUp,
			tableInsertIndex,
			CurrentMatchFlowState.PendingCaptureDamageDelta);
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
			(GetAdventureUpgradeRunState() ?? AdventureRunReadModel.Empty)
			.BuildCaptureRewardApplicationPlan(
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
			TrySynchronizeCommittedAdventurePlayerHealth(
				commitResult.HealingResult.Value.CurrentHealth);
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return new AdventureCaptureDamageResult(
				Math.Max(0, normalizedInput.BaseDamage + normalizedInput.FlatDamageDelta));
		}

		return runState.PreviewCaptureDamage(normalizedInput);
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
			: targetPlayerIndex >= 0 && targetPlayerIndex < _playerScores.Count
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true
			? normalizedInput with
			{
				FrozenModifiers = runState.FreezeCaptureDamageModifiers(),
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
			TrySynchronizeCommittedAdventurePlayerHealth(
				commitResult.HealingResult.Value.CurrentHealth);
		}
		if (commitResult.CommitBandageAtRealTurnEnd)
		{
			if (!TrySubmitRegisterRealTurnEndMarkerRuleCommand(
				MainPlayerIndex,
				AdventureMissBandageRealTurnEndMarkerId,
				_turnSequenceGeneration))
			{
				return new AdventureMissSceneResolution(
					false,
					default,
					default);
			}
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
			TrySynchronizeCommittedAdventurePlayerHealth(
				commitResult.HealingResult.Value.CurrentHealth);
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

			Texture2D? revealTexture = tableCard.Card.HasValue
				? GetPlayingCardTexture(tableCard.Card.Value)
				: tableCard.ItemOverlay.HasValue ? GetItemCardTexture(tableCard.ItemOverlay.Value) : null;
			if (revealTexture == null)
			{
				continue;
			}

			await PlayTableCardRevealAnimationAsync(tableCardIndex, revealTexture);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			if (!TrySubmitSetTableCardsFaceUpRuleCommand(
				_currentTurnPlayerIndex,
				ImmutableArray.Create(new RuleTableCardReference(
					tableCardIndex,
					tableCard.InstanceId)),
				tableCard.IsFaceUp,
				faceUp: true,
				turnSequenceGeneration,
				out _))
			{
				return;
			}
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

		int availableHandSlots = Math.Max(
			0,
			MaxTotalHandCards - GetTotalHandCardCount(playerIndex));
		var returnedCards = new List<PlayingCard>(Math.Min(cardsToReturn.Count, availableHandSlots));
		var returnedCardIds = ImmutableArray.CreateBuilder<long>(
			Math.Min(cardsToReturn.Count, availableHandSlots));
		foreach (PlayingCard card in cardsToReturn)
		{
			int deckIndex = FindLastPlayerDeckCardIndex(playerIndex, card);
			if (deckIndex < 0 || returnedCards.Count >= availableHandSlots)
			{
				continue;
			}

			returnedCards.Add(card);
			returnedCardIds.Add(card.InstanceId);
		}

		if (returnedCards.Count == 0
			|| !TrySubmitReturnCardsToHandRuleCommand(
				playerIndex,
				returnedCardIds.ToImmutable(),
				MaxTotalHandCards,
				out RuleCommandResult returnResult))
		{
			return;
		}

		int firstHandIndex = returnResult.Mutation.TargetIndex;
		for (int returnedIndex = 0; returnedIndex < returnedCards.Count; returnedIndex++)
		{
			await PlayReplacementDrawAnimationAsync(
				playerIndex,
				new DrawnHandCard(returnedCards[returnedIndex], firstHandIndex + returnedIndex),
				turnSequenceGeneration);
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return playedCard.Rank;
		}

		return runState.ResolveClubPlayAdjustments(playedCard.Rank).PrimaryRank;
	}

	private List<string>? BuildAdventureMainPlayerAlternateRanks(PlayingCard playedCard)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| _currentTurnPlayerIndex != MainPlayerIndex
			|| !TryGetNumericRankValue(playedCard.Rank, out _))
		{
			return null;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return null;
		}

		IReadOnlyList<string> alternateRanks =
			runState.ResolveClubPlayAdjustments(playedCard.Rank).AlternateRanks;
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		return runState?.IsActive == true
			&& runState.HasAcquiredUpgrade("clubs_borrow_suit_set")
			&& runState.Effects.ClubBorrowSuitSetArmed;
	}

	private int FindLastPlayerDeckCardIndex(int playerIndex, PlayingCard card)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return -1;
		}

		IReadOnlyList<PlayingCard> deck = _playerDecks[playerIndex];
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return 0;
		}

		return runState.GetBattleStartDrawCount();
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
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
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
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (!ShouldUseAdventureUpgradeSystem()
			|| winnerPlayerIndex != MainPlayerIndex
			|| runState?.ShouldGrantVictoryUpgrade != true)
		{
			return false;
		}

		if (CurrentMatchFlowState.PendingWinnerSeat == winnerPlayerIndex)
		{
			return true;
		}

		return TryUpdateMatchFlowState(state => state with
		{
			PendingWinnerSeat = winnerPlayerIndex,
		});
	}

	private bool ShouldQueueAdventureBigCaptureUpgrade(int playerIndex, int totalCapturedCardCount)
	{
		if (!ShouldUseAdventureUpgradeSystem()
			|| playerIndex != MainPlayerIndex
			|| GetAdventureUpgradeRunState()
				?.ShouldGrantBigCaptureUpgrade(totalCapturedCardCount) != true)
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
