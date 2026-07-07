using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayCardFromHandAsync(int playerIndex, int handIndex, int turnSequenceGeneration, Vector2? startGlobalPositionOverride = null)
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
		var prePlayTableCards = new List<TableCard>(_tableCards);
		bool narwhalDoubleAbilityTriggered = ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, playedCard);
		if (narwhalDoubleAbilityTriggered)
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.TuskBreak);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new PlayCardResult(false, false);
			}
		}

		playedCard = PrepareAdventureEnemyPlayedCard(playerIndex, playedCard);
		AdventureEnemyNextBaseCardModifiers adventureEnemyNextBaseCardModifiers = ConsumeAdventureEnemyNextBaseCardModifiers(playerIndex);
		CardPlayPlan cardPlayPlan = ResolveCardPlayPlan(
			playedCard,
			adventureEnemyNextBaseCardModifiers.TableInsertIndex,
			adventureEnemyNextBaseCardModifiers.FacesUp);
		bool playedCardUsedAttachedEffectOnPlan = DidPlayedCardUseAttachedEffectOnPlan(playedCard, cardPlayPlan);
		ConsumeBorrowTideIfUsed(cardPlayPlan);
		bool playedRankCollectsTable = cardPlayPlan.PlayedCardCollectsTable;
		Control handArea = _playerHandAreas[playerIndex];
		Vector2 cardOffset = GetHandCardOffset(playerIndex, hand.Count, handArea.Size);
		Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, hand.Count, handArea.Size, cardOffset);
		Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
		Vector2 startGlobalPosition = startGlobalPositionOverride
			?? sourceCardVisual?.GlobalPosition
			?? handArea.GlobalPosition + firstCardPosition + handIndex * cardOffset;
		if (sourceCardSlot != null)
		{
			sourceCardSlot.Visible = false;
		}

		CardPlayStartResult startResult = _cardPlayStateService.TryStartPlayFromHand(
			_state,
			playerIndex,
			handIndex,
			playedCard,
			cardPlayPlan);
		if (!startResult.Played)
		{
			return new PlayCardResult(false, false);
		}

		ConsumeAdventureBorrowSuitSetAfterPlayIfNeeded(playedCard);
		ApplyAdventureCanopyRevealToNewTableCard(startResult.PlayedTableCardIndex);

		ShiftHandDisplayStateAfterRemovedCard(playerIndex, handIndex);
		int captureStartIndex = startResult.CaptureStartIndex;
		int playedTableCardIndex = startResult.PlayedTableCardIndex;
		bool matchedTableCardFlipped = startResult.MatchedTableCardFlipped;
		bool playedTableCardIsFaceUp = _tableCards[playedTableCardIndex].IsFaceUp;
		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
		{
			GetNetworkManager()?.BroadcastCardPlayedEventFromHost(
				playerIndex,
				handIndex,
				ToNetworkCardCode(playedCard),
				playedTableCardIsFaceUp,
				new NetworkHandPlayVisualSync(
					playedTableCardIndex,
					cardPlayPlan.MatchingTableCardIndex,
					cardPlayPlan.MatchedTableCardFlipped,
					cardPlayPlan.EffectiveRank),
				turnSequenceGeneration,
				snapshotId);
		}

		await PlayCardToTableAnimationAsync(
			playerIndex,
			playedCard,
			startGlobalPosition,
			playedTableCardIndex,
			playedTableCardIsFaceUp);

		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new PlayCardResult(false, false);
		}

		RenderPlayerHands();
		RenderTableCards();
		bool consumedAdventureExtraBaseCardAction = ConsumeAdventureMainPlayerExtraBaseCardActionIfNeeded(playerIndex);

		CardPlayBranchResult branchResult;
		if (captureStartIndex >= 0)
		{
			branchResult = await ResolvePlayedCardCaptureAsync(
				playerIndex,
				playedCard,
				prePlayTableCards,
				captureStartIndex,
				playedTableCardIndex,
				matchedTableCardFlipped,
				playedRankCollectsTable,
				adventureEnemyNextBaseCardModifiers.CaptureDamageDelta,
				playedCardUsedAttachedEffectOnPlan,
				turnSequenceGeneration);
		}
		else
		{
			branchResult = await ResolveMissedPlayedCardAsync(playerIndex, playedCard, prePlayTableCards, playedTableCardIndex, turnSequenceGeneration);
		}

		if (branchResult.ShouldReturn)
		{
			return branchResult.Result;
		}

		CheckForWinner();
		bool captured = captureStartIndex >= 0;
		return new PlayCardResult(
			true,
			captured,
			captured,
			CompletesTurn: !branchResult.OvertimeTriggered,
			CaptureVisualSync: branchResult.CaptureVisualSync,
			HandPlayVisualSync: new NetworkHandPlayVisualSync(
				playedTableCardIndex,
				cardPlayPlan.MatchingTableCardIndex,
				cardPlayPlan.MatchedTableCardFlipped,
				cardPlayPlan.EffectiveRank),
			AdventureConsumedExtraBaseCardAction: consumedAdventureExtraBaseCardAction);
	}

	private async Task<CardPlayBranchResult> ResolvePlayedCardCaptureAsync(
		int playerIndex,
		PlayingCard playedCard,
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int captureStartIndex,
		int playedTableCardIndex,
		bool matchedTableCardFlipped,
		bool playedRankCollectsTable,
		int adventureEnemyCurrentPlayCaptureDamageDelta,
		bool playedCardUsedAttachedEffectOnPlan,
		int turnSequenceGeneration)
	{
		PlayGameSfx(GameSfx.CaptureBite, -1.0f, 0.97f, 1.03f);
		ClearCardPlayTriggerScratchSets();
		HashSet<int> pendingFaceUpMagnetTriggerIndexes = _cardPlayPendingFaceUpMagnetTriggerIndexes;
		HashSet<int> resolvedMagnetTriggerIndexes = _cardPlayResolvedMagnetTriggerIndexes;
		HashSet<int> magnetExtraCaptureIndexes = _cardPlayMagnetExtraCaptureIndexes;
		HashSet<int> resolvedDoubleTriggerIndexes = _cardPlayResolvedDoubleTriggerIndexes;
		HashSet<int> resolvedOvertimeTriggerIndexes = _cardPlayResolvedOvertimeTriggerIndexes;
		TableCard matchedTableCard = _tableCards[captureStartIndex];
		if (matchedTableCardFlipped && matchedTableCard.Card.HasValue && IsAttachedEffectType(matchedTableCard.Card.Value, CardEffectTypes.MagnetExtraCapture))
		{
			pendingFaceUpMagnetTriggerIndexes.Add(captureStartIndex);
		}

		if (IsAttachedEffectType(playedCard, CardEffectTypes.MagnetExtraCapture))
		{
			pendingFaceUpMagnetTriggerIndexes.Add(playedTableCardIndex);
		}

			PairCaptureEdgeFloatHold? pairCaptureEdgeFloat = null;
			bool pairCaptureEdgeFloatHandedToDeck = false;
			try
			{
				captureStartIndex = await TryResolveReverseAnchorCaptureStartAsync(
					captureStartIndex,
					playerIndex,
					turnSequenceGeneration);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}

				CaptureScoringRules.TableCard[] captureScoringTableCards = ToCaptureScoringTableCards();
				int scorePlayerIndex = ScoreService.GetCapturedScorePlayerIndex(
					captureScoringTableCards,
					captureStartIndex,
					playerIndex,
					player_count);
				CaptureFlowPlan captureFlow = CaptureFlowPlanner.Build(
					captureScoringTableCards,
					captureStartIndex,
					scorePlayerIndex,
					BuildCaptureScoringContext(scorePlayerIndex),
					awardScore: true,
					magnetExtraCaptureIndexes);
				bool capturedMiddleCards = captureFlow.Capture.CapturedMiddleCards;
				if (ShouldPlayPairCaptureEdgeFloat(captureStartIndex, playedTableCardIndex, playedRankCollectsTable))
				{
					pairCaptureEdgeFloat = await PlayPairCaptureEdgeFloatHoldAnimationAsync(captureStartIndex, playedTableCardIndex);
					if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			await ToSignal(GetTree().CreateTimer(TableCaptureDelaySeconds), SceneTreeTimer.SignalName.Timeout);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			if (IsNetworkMultiplayerMode()
				&& IsNetworkHost()
				&& TryBuildNetworkCaptureRevealVisualSync(captureStartIndex, scorePlayerIndex, magnetExtraCaptureIndexes, out NetworkCaptureRevealVisualSync captureRevealVisualSync)
				&& TryGetCurrentAuthoritativeVisualSnapshotId(out int captureRevealSnapshotId))
			{
				GetNetworkManager()?.BroadcastCaptureRevealVisualEventFromHost(
					captureRevealVisualSync,
					turnSequenceGeneration,
					captureRevealSnapshotId);
			}

			await RevealCapturedBaseCardsEdgesBeforeMiddleAsync(
				captureStartIndex,
				scorePlayerIndex,
				turnSequenceGeneration,
				pairCaptureEdgeFloat,
				pendingFaceUpMagnetTriggerIndexes,
				resolvedMagnetTriggerIndexes,
				magnetExtraCaptureIndexes,
				resolvedDoubleTriggerIndexes,
				resolvedOvertimeTriggerIndexes);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

				if (capturedMiddleCards)
				{
					List<TableSpecialCardTrigger> specialTriggers = _capturedSpecialCardTriggers;
					BuildCapturedSpecialCardTriggers(captureStartIndex, magnetExtraCaptureIndexes, specialTriggers);
				try
				{
					for (int triggerIndex = 0; triggerIndex < specialTriggers.Count; triggerIndex++)
					{
						TableSpecialCardTrigger specialTrigger = specialTriggers[triggerIndex];
						await PlayTableSpecialCardTriggerEffectAsync(specialTrigger);
						if (turnSequenceGeneration != _turnSequenceGeneration)
						{
							return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
						}

						await PublishAndResolveCardEventAsync(
							new CardGameEvent(
								CardGameEvents.OnCollect,
								playerIndex,
								sourceTableCardIndex: specialTrigger.TableCardIndex,
								captureStartIndex: captureStartIndex,
								extraTableCardIndexes: magnetExtraCaptureIndexes,
								originGlobalCenter: specialTrigger.GlobalPosition + CardVisualSize / 2.0f),
							turnSequenceGeneration);
						if (turnSequenceGeneration != _turnSequenceGeneration)
						{
							return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
						}
					}
				}
				finally
				{
					specialTriggers.Clear();
				}
			}

				List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
					captureStartIndex,
					false,
					false,
					magnetExtraCaptureIndexes,
					pairCaptureEdgeFloat);
				bool gardenEelSandBiteTriggered = capturedMiddleCards
					&& ShouldApplyGardenEelSandBite(playerIndex, captureStartIndex, playedRankCollectsTable);
			if (gardenEelSandBiteTriggered)
			{
				await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.SandAmbush);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

				bool hasDoubleEffect = captureFlow.Score.HasDoubleEffect;
				int gardenEelSandBiteBonusPoints = gardenEelSandBiteTriggered
					? ConsumeGardenEelSandBiteBonusPoints(playerIndex, captureStartIndex, scorePlayerIndex, hasDoubleEffect, playedRankCollectsTable)
					: 0;
				int endpointCrownBonusPoints = GetCaptureStartScoreBonusPoints(captureStartIndex, scorePlayerIndex, hasDoubleEffect);
				int midTideUpgradeBonusPoints = GetCaptureEndScoreBonusPoints(captureStartIndex, scorePlayerIndex, hasDoubleEffect);
				if (gardenEelSandBiteBonusPoints > 0)
				{
					_ = PlayTableCardPointPopupAsync(captureStartIndex, gardenEelSandBiteBonusPoints, hasDoubleEffect);
				}
				if (endpointCrownBonusPoints > 0)
				{
					_ = PlayTableCardPointPopupAsync(captureStartIndex, endpointCrownBonusPoints, hasDoubleEffect);
				}

				int captureTailTableCardIndex = GetCaptureTailTableCardIndex(captureStartIndex);
				if (midTideUpgradeBonusPoints > 0 && captureTailTableCardIndex >= 0)
				{
					_ = PlayTableCardPointPopupAsync(captureTailTableCardIndex, midTideUpgradeBonusPoints, hasDoubleEffect);
				}

				int collectedPoints = captureFlow.Score.Points
					+ gardenEelSandBiteBonusPoints
					+ endpointCrownBonusPoints
					+ midTideUpgradeBonusPoints;
				var adventureCaptureRawPoints = new List<int>();
				var adventureCaptureResolvedPoints = new List<int>();
				BuildAdventureCaptureDamageCardPointLists(
					captureScoringTableCards,
					captureStartIndex,
					scorePlayerIndex,
					magnetExtraCaptureIndexes,
					adventureCaptureRawPoints,
					adventureCaptureResolvedPoints);
				int capturedOldTableDamageDelta = prePlayTableCards.Count > 0
					? AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
						GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds() ?? Array.Empty<string>(),
						playerIndex == MainPlayerIndex,
						CountCapturedOldScoringTableCards(prePlayTableCards, captureStartIndex, magnetExtraCaptureIndexes),
						IsCapturedTableCardReference(_adventureTraceHuntMarkedTableCard, captureStartIndex, magnetExtraCaptureIndexes),
						IsCapturedTableCardReference(_adventureHuntOrderMarkedTableCard, captureStartIndex, magnetExtraCaptureIndexes))
					: 0;
				bool questionRewardWillTrigger = false;
				if (capturedMiddleCards)
				{
					for (int tableCardIndex = captureStartIndex; tableCardIndex < _tableCards.Count; tableCardIndex++)
					{
						if (IsQuestionTableCard(_tableCards[tableCardIndex]))
						{
							questionRewardWillTrigger = true;
							break;
						}
					}
				}
				bool isFirstCaptureThisBattle = playerIndex == MainPlayerIndex
					&& _adventureMainPlayerCaptureCountThisBattle == 0;
				int resolvedAdventureDamage = ResolveAdventureCaptureDamage(
					playerIndex,
					new AdventureCaptureDamageInput(
						BaseDamage: collectedPoints,
						FlatDamageDelta: adventureEnemyCurrentPlayCaptureDamageDelta + capturedOldTableDamageDelta,
						IsFirstCaptureThisBattle: isFirstCaptureThisBattle,
						IncludesQuestionReward: questionRewardWillTrigger,
						QuestionRewardEchoActive: _adventureMainPlayerQuestionRewardEchoActive,
						ScoringCardRawPoints: adventureCaptureRawPoints,
						ScoringCardResolvedPoints: adventureCaptureResolvedPoints));
				AdventurePostCaptureEffectResult? resolvedAdventurePostCaptureEffects = playerIndex == MainPlayerIndex
					? ResolveAdventurePostCaptureEffects(
						playerIndex,
						resolvedAdventureDamage,
						_adventureMainPlayerSuccessfulCapturesThisTurn + 1)
					: null;
				if (capturedMiddleCards && ShouldPlayCrabScoreReductionAbility(scorePlayerIndex, captureStartIndex, magnetExtraCaptureIndexes))
				{
					await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(1, turnSequenceGeneration, AdventureEnemyAbilities.TideGuard);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			if (collectedPoints > 0)
			{
				List<CapturedTableCardVisual> scoringCardVisuals = BuildCapturedTableCardVisuals(
					captureStartIndex,
					false,
					true,
					magnetExtraCaptureIndexes,
					includeCaptureStartScoring: gardenEelSandBiteBonusPoints > 0 || endpointCrownBonusPoints > 0,
					scorePlayerIndex: scorePlayerIndex);
				await PlayScoreFillCollectionAsync(
					scorePlayerIndex,
					scoringCardVisuals,
					turnSequenceGeneration,
					() => AddPlayerScore(scorePlayerIndex, resolvedAdventureDamage));
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			float captureFlightStaggerSeconds = pairCaptureEdgeFloat != null ? 0.0f : TableCaptureFlightStaggerSeconds;
			NetworkCaptureVisualSync captureVisualSync = new(
				playerIndex,
				captureStartIndex,
				BuildSortedExtraTableCardIndexesArray(magnetExtraCaptureIndexes),
				captureFlightStaggerSeconds);
			if (IsNetworkMultiplayerMode()
				&& IsNetworkHost()
				&& TryGetCurrentAuthoritativeVisualSnapshotId(out int snapshotId))
			{
				GetNetworkManager()?.BroadcastCaptureVisualEventFromHost(
					captureVisualSync,
					turnSequenceGeneration,
					snapshotId);
			}
			pairCaptureEdgeFloat?.QueueFreeOverlays(_animationPresenter);
			pairCaptureEdgeFloatHandedToDeck = pairCaptureEdgeFloat != null;
			await PlayCapturedCardsToDeckAnimationAsync(
				playerIndex,
				captureStartIndex,
				capturedCardVisuals,
				turnSequenceGeneration,
				magnetExtraCaptureIndexes,
				captureFlightStaggerSeconds);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			bool preserveTailTableCard = IsAdventureEnemyPlayer(playerIndex)
				&& AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
					GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds() ?? Array.Empty<string>(),
					captureFlow.TotalCapturedCardCount);
			TableCard? preservedTailTableCard = preserveTailTableCard
				? _tableCards[playedTableCardIndex]
				: null;
			CaptureTableCardsResult captureResult = await CaptureTableCardsAsync(
				playerIndex,
				captureStartIndex,
				true,
				false,
				turnSequenceGeneration,
				magnetExtraCaptureIndexes,
				adventureEnemyCurrentPlayCaptureDamageDelta,
				prePlayTableCards,
				playedCardUsedAttachedEffectOnPlan
					|| (capturedMiddleCards
						&& (IsAttachedEffectType(playedCard, CardEffectTypes.MagnetExtraCapture)
							|| IsAttachedEffectType(playedCard, CardEffectTypes.DoubleCollect)
							|| IsAttachedEffectType(playedCard, CardEffectTypes.ExtraTurn))),
				playedCard,
				preserveTailTableCard ? playedTableCardIndex : -1,
				suppressGardenEelCaptureRecord: gardenEelSandBiteBonusPoints > 0);
			int collectedCardCount = captureResult.CollectedCardCount;
			if (IsGameOver())
			{
				return new CardPlayBranchResult(true, new PlayCardResult(true, true, collectedCardCount > 0), false);
			}

			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
			if (preserveTailTableCard && preservedTailTableCard != null && _tableCards.Contains(preservedTailTableCard))
			{
				preservedTailTableCard.IsFaceUp = true;
			}
			if (!captureResult.QuestionRewardAnimation.HasValue)
			{
				RenderTableCards();
			}
			if (collectedCardCount > 0)
			{
				RenderPlayerCardPiles();
				await PlayPhysicalDeckShuffleAsync(playerIndex);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}
			}

			DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);
			if (captureResult.QuestionRewardAnimation.HasValue && !captureResult.QuestionRewardAnimation.Value.RequiresMagnetAttachment)
			{
				HideHandDisplaySlot(playerIndex, _playerHands[playerIndex].Count + captureResult.QuestionRewardAnimation.Value.ItemHandIndex);
			}

			await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			SendQuestionRewardVisualEventIfNeeded(playerIndex, captureResult.QuestionRewardAnimation, turnSequenceGeneration);
			await PlayQuestionCardRewardAnimationAsync(playerIndex, captureResult.QuestionRewardAnimation, turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}
			ApplyAdventureQuestionRewardEffects(playerIndex, captureResult.AdventureQuestionRewardEffects);
			await ApplyAdventurePostCaptureEffectsAsync(
				playerIndex,
				resolvedAdventurePostCaptureEffects ?? captureResult.AdventurePostCaptureEffects,
				turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			await MaybeResolveAppliedAdventureQuestionRewardRejudgeAsync(
				playerIndex,
				captureResult.QuestionRewardAnimation,
				turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			await ResolvePendingMagnetAttachmentsAsync(turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
			}

			TryAssignBorrowTideAfterCapture(playerIndex, captureStartIndex, playedCard.Rank);

			RenderPlayerHands();

			return new CardPlayBranchResult(false, default, resolvedOvertimeTriggerIndexes.Count > 0, captureVisualSync);
		}
		finally
		{
			ClearCardPlayTriggerScratchSets();
			if (!pairCaptureEdgeFloatHandedToDeck && pairCaptureEdgeFloat != null)
			{
				RestorePairCaptureEdgeFloatHold(pairCaptureEdgeFloat);
			}
		}
	}

	private async Task<CardPlayBranchResult> ResolveMissedPlayedCardAsync(
		int playerIndex,
		PlayingCard playedCard,
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int playedTableCardIndex,
		int turnSequenceGeneration)
	{
		bool overtimeTriggered = false;
		if (IsAttachedEffectType(playedCard, CardEffectTypes.ExtraTurn))
		{
			HashSet<int> resolvedOvertimeTriggerIndexes = _cardPlayResolvedOvertimeTriggerIndexes;
			resolvedOvertimeTriggerIndexes.Clear();
			try
			{
				await ResolveOvertimeTableCardTriggerAsync(playedTableCardIndex, turnSequenceGeneration, resolvedOvertimeTriggerIndexes);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
				}

				overtimeTriggered = resolvedOvertimeTriggerIndexes.Count > 0;
			}
			finally
			{
				resolvedOvertimeTriggerIndexes.Clear();
			}
		}

		await ApplySeaSlugSlimeCoatAfterMissAsync(playerIndex, playedTableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}

		await TryResolveEmptyTideContinuationAfterMissAsync(playedTableCardIndex, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}

		DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);
		RenderTableCards();
		await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}
		AdventureMainPlayerMissEffectResult mainPlayerMissEffects = ApplyAdventureMainPlayerMissEffects(playerIndex);
		if (!await ApplyAdventureMainPlayerMissTableEffectsAsync(mainPlayerMissEffects, playedTableCardIndex, turnSequenceGeneration))
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}
		ApplyAdventureEnemyMissEffects(playerIndex, playedTableCardIndex, prePlayTableCards);
		ApplyAdventureEnemyHookDredgeIfNeeded(prePlayTableCards, playerIndex);
		if (mainPlayerMissEffects.GrantExtraBaseCardAction)
		{
			overtimeTriggered = TryGrantAdventureMainPlayerExtraBaseCardAction() || overtimeTriggered;
		}

		return new CardPlayBranchResult(false, default, overtimeTriggered);
	}

	private static int[] BuildSortedExtraTableCardIndexesArray(IReadOnlyCollection<int> extraTableCardIndexes)
	{
		if (extraTableCardIndexes.Count == 0)
		{
			return Array.Empty<int>();
		}

		var result = new List<int>(extraTableCardIndexes.Count);
		foreach (int tableCardIndex in extraTableCardIndexes)
		{
			result.Add(tableCardIndex);
		}

		result.Sort();
		return result.ToArray();
	}

	private readonly record struct CardPlayBranchResult(
		bool ShouldReturn,
		PlayCardResult Result,
		bool OvertimeTriggered,
		NetworkCaptureVisualSync? CaptureVisualSync = null);

	private void ClearCardPlayTriggerScratchSets()
	{
		_cardPlayPendingFaceUpMagnetTriggerIndexes.Clear();
		_cardPlayResolvedMagnetTriggerIndexes.Clear();
		_cardPlayMagnetExtraCaptureIndexes.Clear();
		_cardPlayResolvedDoubleTriggerIndexes.Clear();
		_cardPlayResolvedOvertimeTriggerIndexes.Clear();
	}

	private bool TryBuildNetworkCaptureRevealVisualSync(
		int captureStartIndex,
		int scorePlayerIndex,
		HashSet<int>? magnetExtraCaptureIndexes,
		out NetworkCaptureRevealVisualSync visualSync)
	{
		visualSync = default;
		if (captureStartIndex < 0 || captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		var revealTableCardIndexes = new List<int>();
		var revealCardCodes = new List<string>();
		var revealAttachedItemCardIdsCsv = new List<string>();
		var revealPointValues = new List<int>();
		var revealPointDoubleFlags = new List<bool>();
		foreach (int tableCardIndex in CaptureRevealOrder.BuildBaseRevealIndexes(captureStartIndex, _tableCards.Count))
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand
				|| tableCard.IsFaceUp
				|| ShouldSkipOwnerPreviewRevealAnimation(tableCard))
			{
				continue;
			}

			Texture2D? revealTexture = null;
			if (tableCard.Card.HasValue)
			{
				revealTexture = tableCard.Card.Value.Texture;
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				revealTexture = tableCard.ItemOverlay.Value.Texture;
			}

			if (revealTexture == null)
			{
				continue;
			}

			revealTableCardIndexes.Add(tableCardIndex);
			revealCardCodes.Add(tableCard.Card.HasValue ? ToNetworkCardCode(tableCard.Card.Value) : string.Empty);
			revealAttachedItemCardIdsCsv.Add(tableCard.Card.HasValue ? tableCard.Card.Value.AttachedItemCardIdsCsv : string.Empty);
			bool hasDoubleEffect = tableCard.Card.HasValue && HasCapturedDoubleScoreEffect(captureStartIndex, magnetExtraCaptureIndexes);
			int pointValue = tableCard.Card.HasValue && IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex)
				? GetCapturedTableCardScoreValueForPlayer(tableCard, scorePlayerIndex, hasDoubleEffect)
				: 0;
			revealPointValues.Add(pointValue);
			revealPointDoubleFlags.Add(pointValue > 0 && hasDoubleEffect);
		}

		if (revealTableCardIndexes.Count == 0)
		{
			return false;
		}

		visualSync = new NetworkCaptureRevealVisualSync(
			GetLocalHandPlayerIndex(),
			revealTableCardIndexes.ToArray(),
			revealCardCodes.ToArray(),
			revealAttachedItemCardIdsCsv.ToArray(),
			revealPointValues.ToArray(),
			revealPointDoubleFlags.ToArray());
		return true;
	}

	private int FindMatchingTableCardIndex(PlayingCard playedCard)
	{
		return CardPlayResolver.FindMatchingTableCardIndex(ToCardPlayTableCards(), playedCard.Rank);
	}

	private bool ShouldPlayedCardCollectTable(PlayingCard playedCard)
	{
		return !item_mode && CardRankCatalog.ShouldCollectTableWhenItemModeDisabled(playedCard.Rank);
	}

	private CardPlayPlan ResolveCardPlayPlan(
		PlayingCard playedCard,
		int tableInsertIndexOverride = -1,
		bool playedCardFacesUp = false)
	{
		string primaryRank = ResolveAdventureMainPlayerPrimaryMatchRank(playedCard);
		var alternateRanks = new List<string>();
		List<string>? clubAlternateRanks = BuildAdventureMainPlayerAlternateRanks(playedCard);
		if (clubAlternateRanks != null)
		{
			foreach (string alternateRank in clubAlternateRanks)
			{
				if (!alternateRanks.Contains(alternateRank))
				{
					alternateRanks.Add(alternateRank);
				}
			}
		}

		List<string>? borrowTideAlternateRanks = BuildBorrowTideAlternateRanks(playedCard);
		if (borrowTideAlternateRanks != null)
		{
			foreach (string alternateRank in borrowTideAlternateRanks)
			{
				if (!alternateRanks.Contains(alternateRank))
				{
					alternateRanks.Add(alternateRank);
				}
			}
		}

		return CardPlayResolver.Resolve(
			ToCardPlayTableCards(),
			new CardPlayCard(
				primaryRank,
				ShouldPlayedCardCollectTable(playedCard),
				playedCard.Suit,
				ShouldCollectBySuit(playedCard)),
			alternateRanks.Count > 0 ? alternateRanks : null,
			insertAtTableStart: ShouldInsertMissedCardAtTableStart(playedCard),
			tableInsertIndexOverride: tableInsertIndexOverride,
			playedCardFacesUp: playedCardFacesUp);
	}

	private bool DidPlayedCardUseAttachedEffectOnPlan(PlayingCard playedCard, CardPlayPlan plan)
	{
		if (ShouldCollectBySuit(playedCard)
			&& !string.IsNullOrWhiteSpace(playedCard.Suit)
			&& CardPlayResolver.FindFirstMatchingSuitTableCardIndex(ToCardPlayTableCards(), playedCard.Suit) >= 0)
		{
			return true;
		}

		if (ShouldInsertMissedCardAtTableStart(playedCard)
			&& plan.MatchingTableCardIndex < 0
			&& !plan.PlayedCardCollectsTable)
		{
			return true;
		}

		return false;
	}

	private CardPlayTableCard[] ToCardPlayTableCards()
	{
		var tableCards = new CardPlayTableCard[_tableCards.Count];
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			TableCard tableCard = _tableCards[tableCardIndex];
			tableCards[tableCardIndex] = new CardPlayTableCard(
				tableCard.Card?.Rank,
				tableCard.IsFaceUp,
				tableCard.IsBuriedInSand,
				tableCard.Card?.Suit);
		}

		return tableCards;
	}

	private List<string>? BuildBorrowTideAlternateRanks(PlayingCard playedCard)
	{
		if (!ShouldApplyBorrowTideToPlayedCard(playedCard))
		{
			return null;
		}

		var alternateRanks = new List<string>();
		if (!string.IsNullOrWhiteSpace(_borrowTideAdjustedRank))
		{
			alternateRanks.Add(_borrowTideAdjustedRank);
		}

		foreach (string adjacentRank in GetAdjacentNumericRanks(playedCard.Rank))
		{
			if (!alternateRanks.Contains(adjacentRank))
			{
				alternateRanks.Add(adjacentRank);
			}
		}

		return alternateRanks.Count == 0 ? null : alternateRanks;
	}

	private bool ShouldApplyBorrowTideToPlayedCard(PlayingCard playedCard)
	{
		return _borrowTidePending
			&& _borrowTideOwnerPlayerIndex == _currentTurnPlayerIndex
			&& !playedCard.HasItemEffect
			&& TryGetNumericRankValue(playedCard.Rank, out _);
	}

	private bool ShouldInsertMissedCardAtTableStart(PlayingCard playedCard)
	{
		return IsAttachedEffectType(playedCard, CardEffectTypes.ReverseFlowAnchor);
	}

	private bool ShouldCollectBySuit(PlayingCard playedCard)
	{
		return !string.IsNullOrWhiteSpace(playedCard.Suit)
			&& (IsAttachedEffectType(playedCard, CardEffectTypes.SuitCollect)
				|| ShouldAdventureBorrowSuitSetAffectPlayedCard(playedCard));
	}

	private static IEnumerable<string> GetAdjacentNumericRanks(string rank)
	{
		if (!TryGetNumericRankValue(rank, out int rankValue))
		{
			yield break;
		}

		if (rankValue > 1)
		{
			yield return (rankValue - 1).ToString();
		}

		if (rankValue < 10)
		{
			yield return (rankValue + 1).ToString();
		}
	}

	private static bool TryGetNumericRankValue(string rank, out int rankValue)
	{
		return int.TryParse(rank, out rankValue) && rankValue >= 1 && rankValue <= 10;
	}
}
