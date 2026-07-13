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
		if (playerIndex == MainPlayerIndex)
		{
			await BeginRealMainPlayerTurnIfNeededAsync(turnSequenceGeneration);
			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return new PlayCardResult(false, false);
			}
		}

		List<PlayingCard> hand = _playerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return new PlayCardResult(false, false);
		}

		PlayingCard playedCard = hand[handIndex];
		AdventureExtraActionGrant? pendingExtraActionGrant =
			PeekAdventureExtraActionGrantForAction(playerIndex);
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
		AdventureExtraActionGrant? acceptedExtraActionGrant =
			AcceptAdventureExtraActionGrantForAction(
				playerIndex,
				pendingExtraActionGrant);
		BaseCardActionContext baseCardActionContext =
			CreateBaseCardActionContext(
				playedCard,
				acceptedExtraActionGrant);

		try
		{
			ConsumeAdventureBorrowSuitSetAfterPlayIfNeeded(playedCard);
			ApplyAdventureCanopyRevealToNewTableCard(startResult.PlayedTableCardIndex);

		ShiftHandDisplayStateAfterRemovedCard(playerIndex, handIndex);
			int captureStartIndex = startResult.CaptureStartIndex;
			int playedTableCardIndex = startResult.PlayedTableCardIndex;
			bool plannedCapturedMiddleCards = captureStartIndex >= 0
				&& HasCapturedMiddleTableCards(captureStartIndex);
			baseCardActionContext = baseCardActionContext with
			{
				TriggeredAttachedItemCardIds = BuildPlayedCardPlanTriggeredAttachmentIds(
					playedCard,
					cardPlayPlan,
					plannedCapturedMiddleCards),
			};
			AdventureCaptureMatchSource captureMatchSource =
				ResolveAdventureCaptureMatchSource(playedCard, cardPlayPlan);
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
				return new PlayCardResult(
					false,
					false,
					ConsumedAdventureExtraActionGrant: acceptedExtraActionGrant,
					BaseCardActionContext: baseCardActionContext);
			}

			RenderPlayerHands();
			RenderTableCards();

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
					baseCardActionContext,
					captureMatchSource,
					turnSequenceGeneration);
		}
		else
		{
				branchResult = await ResolveMissedPlayedCardAsync(
					playerIndex,
					playedCard,
					prePlayTableCards,
					playedTableCardIndex,
					baseCardActionContext,
					turnSequenceGeneration);
			}
			baseCardActionContext = branchResult.ResolvedActionContext
				?? baseCardActionContext;

			if (branchResult.ShouldReturn)
			{
				return branchResult.Result with
				{
					ConsumedAdventureExtraActionGrant = acceptedExtraActionGrant,
					BaseCardActionContext = baseCardActionContext,
				};
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
				ConsumedAdventureExtraActionGrant: acceptedExtraActionGrant,
				BaseCardActionContext: baseCardActionContext);
		}
		finally
		{
			CompleteAdventureExtraActionGrantAction(acceptedExtraActionGrant);
		}
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
		BaseCardActionContext baseCardActionContext,
		AdventureCaptureMatchSource captureMatchSource,
		int turnSequenceGeneration)
{
	PlayGameSfx(GameSfx.CaptureBite, -1.0f, 0.97f, 1.03f);
	ClearCardPlayTriggerScratchSets();
	HashSet<int> pendingFaceUpMagnetTriggerIndexes =
		_cardPlayPendingFaceUpMagnetTriggerIndexes;
	HashSet<int> resolvedMagnetTriggerIndexes =
		_cardPlayResolvedMagnetTriggerIndexes;
	HashSet<int> magnetExtraCaptureIndexes =
		_cardPlayMagnetExtraCaptureIndexes;
	HashSet<int> resolvedDoubleTriggerIndexes =
		_cardPlayResolvedDoubleTriggerIndexes;
		HashSet<int> resolvedOvertimeTriggerIndexes =
			_cardPlayResolvedOvertimeTriggerIndexes;
		var triggeredAttachedItemCardIds = new List<string>(
			baseCardActionContext.TriggeredAttachedItemCardIds);
	var captureTableReferenceSnapshot =
		new List<TableCard>(_tableCards);
	if (captureStartIndex < 0
		|| captureStartIndex >= captureTableReferenceSnapshot.Count
		|| playedTableCardIndex < 0
		|| playedTableCardIndex
			>= captureTableReferenceSnapshot.Count)
	{
		return new CardPlayBranchResult(
			true,
			new PlayCardResult(false, false),
			false);
	}

	TableCard matchedTableCard =
		captureTableReferenceSnapshot[captureStartIndex];
	if (matchedTableCardFlipped
		&& matchedTableCard.Card.HasValue
		&& IsAttachedEffectType(
			matchedTableCard.Card.Value,
			CardEffectTypes.MagnetExtraCapture))
	{
		pendingFaceUpMagnetTriggerIndexes.Add(captureStartIndex);
	}
	if (IsAttachedEffectType(
		playedCard,
		CardEffectTypes.MagnetExtraCapture))
	{
		pendingFaceUpMagnetTriggerIndexes.Add(playedTableCardIndex);
	}

	PairCaptureEdgeFloatHold? pairCaptureEdgeFloat = null;
	bool pairCaptureEdgeFloatHandedToDeck = false;
		try
		{
			int captureStartIndexBeforeReverseAnchor = captureStartIndex;
			int reverseAnchorSourceIndex = GetCaptureTailTableCardIndex(captureStartIndex);
			captureStartIndex =
				await TryResolveReverseAnchorCaptureStartAsync(
				captureStartIndex,
				playerIndex,
				turnSequenceGeneration);
		if (!IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot)
			|| IsGameOver())
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
					false);
			}
			if (captureStartIndex != captureStartIndexBeforeReverseAnchor
				&& reverseAnchorSourceIndex >= 0
				&& reverseAnchorSourceIndex < _tableCards.Count)
			{
				AddTriggeredAttachmentIdForEffect(
					triggeredAttachedItemCardIds,
					_tableCards[reverseAnchorSourceIndex],
					CardEffectTypes.ReverseAnchor);
			}

			CaptureScoringRules.TableCard[] captureScoringTableCards =
			ToCaptureScoringTableCards();
		int scorePlayerIndex =
			ScoreService.GetCapturedScorePlayerIndex(
				captureScoringTableCards,
				captureStartIndex,
				playerIndex,
				player_count);
		CaptureFlowPlan captureFlow = CaptureFlowPlanner.Build(
			captureScoringTableCards,
				captureStartIndex,
				scorePlayerIndex,
				BuildCaptureScoringContext(scorePlayerIndex),
				magnetExtraCaptureIndexes);
		bool capturedMiddleCards =
			captureFlow.Capture.CapturedMiddleCards;
		if (ShouldPlayPairCaptureEdgeFloat(
			captureStartIndex,
			playedTableCardIndex,
			playedRankCollectsTable))
		{
			pairCaptureEdgeFloat =
				await PlayPairCaptureEdgeFloatHoldAnimationAsync(
					captureStartIndex,
					playedTableCardIndex);
			if (!IsCaptureTableReferenceSnapshotCurrent(
					turnSequenceGeneration,
					captureTableReferenceSnapshot)
				|| IsGameOver())
			{
				return new CardPlayBranchResult(
					true,
					new PlayCardResult(false, false),
					false);
			}
		}

		await ToSignal(
			GetTree().CreateTimer(TableCaptureDelaySeconds),
			SceneTreeTimer.SignalName.Timeout);
		if (!IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot)
			|| IsGameOver())
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		if (IsNetworkMultiplayerMode()
			&& IsNetworkHost()
			&& TryBuildNetworkCaptureRevealVisualSync(
				captureStartIndex,
				scorePlayerIndex,
				magnetExtraCaptureIndexes,
				out NetworkCaptureRevealVisualSync
					captureRevealVisualSync)
			&& TryGetCurrentAuthoritativeVisualSnapshotId(
				out int captureRevealSnapshotId))
		{
			GetNetworkManager()
				?.BroadcastCaptureRevealVisualEventFromHost(
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
		if (!IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot)
			|| IsGameOver())
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		if (capturedMiddleCards)
		{
			List<TableSpecialCardTrigger> specialTriggers =
				_capturedSpecialCardTriggers;
			BuildCapturedSpecialCardTriggers(
				captureStartIndex,
				magnetExtraCaptureIndexes,
				specialTriggers);
			try
			{
				for (int triggerIndex = 0;
					triggerIndex < specialTriggers.Count;
					triggerIndex++)
				{
					TableSpecialCardTrigger specialTrigger =
						specialTriggers[triggerIndex];
					await PlayTableSpecialCardTriggerEffectAsync(
						specialTrigger);
					if (!IsCaptureTableReferenceSnapshotCurrent(
							turnSequenceGeneration,
							captureTableReferenceSnapshot)
						|| IsGameOver())
					{
						return new CardPlayBranchResult(
							true,
							new PlayCardResult(false, false),
							false);
					}

					await PublishAndResolveCardEventAsync(
						new CardGameEvent(
							CardGameEvents.OnCollect,
							playerIndex,
							sourceTableCardIndex:
								specialTrigger.TableCardIndex,
							captureStartIndex: captureStartIndex,
							extraTableCardIndexes:
								magnetExtraCaptureIndexes,
							originGlobalCenter:
								specialTrigger.GlobalPosition
									+ CardVisualSize / 2.0f),
						turnSequenceGeneration);
					if (!IsCaptureTableReferenceSnapshotCurrent(
							turnSequenceGeneration,
							captureTableReferenceSnapshot)
						|| IsGameOver())
					{
						return new CardPlayBranchResult(
							true,
							new PlayCardResult(false, false),
							false);
					}
				}
			}
			finally
			{
				specialTriggers.Clear();
			}
			}

			AddTriggeredAttachmentIdsForIndexes(
				triggeredAttachedItemCardIds,
				resolvedMagnetTriggerIndexes,
				CardEffectTypes.MagnetExtraCapture);
			AddTriggeredAttachmentIdsForIndexes(
				triggeredAttachedItemCardIds,
				resolvedDoubleTriggerIndexes,
				CardEffectTypes.DoubleCollect);
			AddTriggeredAttachmentIdsForIndexes(
				triggeredAttachedItemCardIds,
				resolvedOvertimeTriggerIndexes,
				CardEffectTypes.ExtraTurn);

			int blackLedgerRedirectTableCardIndex =
			ResolveBlackLedgerRedirectTableCardIndex(
				playerIndex,
				captureStartIndex);
		List<CapturedTableCardVisual> capturedCardVisuals =
			BuildCapturedTableCardVisuals(
				captureStartIndex,
				false,
				false,
				magnetExtraCaptureIndexes,
				pairCaptureEdgeFloat);
		bool gardenEelSandBiteTriggered = capturedMiddleCards
			&& ShouldApplyGardenEelSandBite(
				playerIndex,
				captureStartIndex,
				playedRankCollectsTable);
		if (gardenEelSandBiteTriggered)
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(
				playerIndex,
				turnSequenceGeneration,
				AdventureEnemyAbilities.SandAmbush);
			if (!IsCaptureTableReferenceSnapshotCurrent(
					turnSequenceGeneration,
					captureTableReferenceSnapshot)
				|| IsGameOver())
			{
				return new CardPlayBranchResult(
					true,
					new PlayCardResult(false, false),
					false);
			}
		}

		bool hasDoubleEffect = captureFlow.Score.HasDoubleEffect;
		int gardenEelSandBiteBonusPoints = gardenEelSandBiteTriggered
			? GetHalfCapturedCardScoreValueForPlayer(
				_tableCards[captureStartIndex].Card!.Value,
				scorePlayerIndex,
				hasDoubleEffect)
			: 0;
		int endpointCrownBonusPoints =
			GetCaptureStartScoreBonusPoints(
				captureStartIndex,
				scorePlayerIndex,
				hasDoubleEffect);
			int midTideUpgradeBonusPoints =
				GetCaptureEndScoreBonusPoints(
				captureStartIndex,
				scorePlayerIndex,
					hasDoubleEffect);
			if (endpointCrownBonusPoints > 0)
			{
				AddTriggeredAttachmentIdsInCapture(
					triggeredAttachedItemCardIds,
					captureStartIndex,
					magnetExtraCaptureIndexes,
					CardEffectTypes.EndpointCrown);
			}
			if (midTideUpgradeBonusPoints > 0)
			{
				AddTriggeredAttachmentIdsInCapture(
					triggeredAttachedItemCardIds,
					captureStartIndex,
					magnetExtraCaptureIndexes,
					CardEffectTypes.MidTideUpgrade);
			}
		if (gardenEelSandBiteBonusPoints > 0)
		{
			_ = PlayTableCardPointPopupAsync(
				captureStartIndex,
				gardenEelSandBiteBonusPoints,
				hasDoubleEffect);
		}
		if (endpointCrownBonusPoints > 0)
		{
			_ = PlayTableCardPointPopupAsync(
				captureStartIndex,
				endpointCrownBonusPoints,
				hasDoubleEffect);
		}

		int captureTailTableCardIndex =
			GetCaptureTailTableCardIndex(captureStartIndex);
		if (midTideUpgradeBonusPoints > 0
			&& captureTailTableCardIndex >= 0)
		{
			_ = PlayTableCardPointPopupAsync(
				captureTailTableCardIndex,
				midTideUpgradeBonusPoints,
				hasDoubleEffect);
		}

		AdventureClubCaptureScoringResult clubCaptureScoring =
			AdventureUpgradeEffectService.ResolveClubCaptureScoring(
				GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds()
					?? Array.Empty<string>());
		bool includeCrownCanopyCaptureEndpoints =
			playerIndex == MainPlayerIndex
			&& clubCaptureScoring.IncludeCaptureEndpointsInScoring;
		bool preserveTailTableCard =
			IsAdventureEnemyPlayer(playerIndex)
			&& AdventureUpgradeEffectService
				.ShouldPreserveEnemyCaptureTailCard(
					GetAdventureUpgradeRunState()
						?.GetAcquiredUpgradeIds()
						?? Array.Empty<string>(),
					captureFlow.TotalCapturedCardCount);
		int preservedTailTableCardIndex = preserveTailTableCard
			? playedTableCardIndex
			: -1;
		CaptureStatePlan captureStatePlan = _captureStateService.CreatePlan(
			_state,
			captureStartIndex,
			magnetExtraCaptureIndexes);
		AdventureClubCaptureExtensionPlan previewClubCaptureExtension =
			BuildAdventureClubCaptureExtensionPlan(
				playerIndex,
				captureStartIndex,
				magnetExtraCaptureIndexes);
		AdventureCapturePreparation previewCapturePreparation =
			BuildAdventureCapturePreparation(
				playerIndex,
				captureStartIndex,
				scorePlayerIndex,
				captureStatePlan,
				prePlayTableCards,
				adventureEnemyCurrentPlayCaptureDamageDelta
					+ gardenEelSandBiteBonusPoints,
				baseCardActionContext,
				"base_card",
				captureMatchSource,
				triggeredAttachedItemCardIds,
				previewClubCaptureExtension.GapRootingTarget,
				preservedTailTableCardIndex);
		if (capturedMiddleCards
			&& ShouldPlayCrabScoreReductionAbility(
				scorePlayerIndex,
				captureStartIndex,
				magnetExtraCaptureIndexes))
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(
				1,
				turnSequenceGeneration,
				AdventureEnemyAbilities.TideGuard);
			if (!IsCaptureTableReferenceSnapshotCurrent(
					turnSequenceGeneration,
					captureTableReferenceSnapshot)
				|| IsGameOver())
			{
				return new CardPlayBranchResult(
					true,
					new PlayCardResult(false, false),
					false);
			}
		}

		AdventureCaptureDamageResult previewCaptureDamage =
			PreviewAdventureCaptureDamage(
				playerIndex,
				previewCapturePreparation.DamageInput);

		if (previewCaptureDamage.FinalDamage > 0)
		{
			List<CapturedTableCardVisual> scoringCardVisuals =
				BuildCapturedTableCardVisuals(
					captureStartIndex,
					false,
					true,
					magnetExtraCaptureIndexes,
					includeCaptureStartScoring:
						gardenEelSandBiteBonusPoints > 0
							|| endpointCrownBonusPoints > 0
							|| includeCrownCanopyCaptureEndpoints,
					includeCaptureEndScoring:
						includeCrownCanopyCaptureEndpoints,
					scorePlayerIndex: scorePlayerIndex);
			await PlayScoreFillCollectionAsync(
				scorePlayerIndex,
				scoringCardVisuals,
				turnSequenceGeneration,
				() => { });
			if (!IsCaptureTableReferenceSnapshotCurrent(
					turnSequenceGeneration,
					captureTableReferenceSnapshot)
				|| IsGameOver())
			{
				return new CardPlayBranchResult(
					true,
					new PlayCardResult(false, false),
					false);
			}
		}

		float captureFlightStaggerSeconds =
			pairCaptureEdgeFloat != null
				? 0.0f
				: TableCaptureFlightStaggerSeconds;
		NetworkCaptureVisualSync captureVisualSync = new(
			playerIndex,
			captureStartIndex,
			BuildSortedExtraTableCardIndexesArray(
				magnetExtraCaptureIndexes),
			captureFlightStaggerSeconds,
			blackLedgerRedirectTableCardIndex);
		pairCaptureEdgeFloat?.QueueFreeOverlays(_animationPresenter);
		pairCaptureEdgeFloatHandedToDeck =
			pairCaptureEdgeFloat != null;
		await PlayCapturedCardsToDeckAnimationAsync(
			playerIndex,
			captureStartIndex,
			capturedCardVisuals,
			turnSequenceGeneration,
			magnetExtraCaptureIndexes,
			captureFlightStaggerSeconds,
			blackLedgerRedirectTableCardIndex);
		if (!IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot)
			|| IsGameOver())
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		TableCard? preservedTailTableCard = preserveTailTableCard
			? _tableCards[playedTableCardIndex]
			: null;
			CaptureTableCardsResult captureResult = await CaptureTableCardsAsync(
				playerIndex,
				captureStartIndex,
				reserveReplacementCard: true,
				turnSequenceGeneration: turnSequenceGeneration,
				extraTableCardIndexes: magnetExtraCaptureIndexes,
				adventureFlatDamageDelta:
					adventureEnemyCurrentPlayCaptureDamageDelta
						+ gardenEelSandBiteBonusPoints,
				prePlayTableCards: prePlayTableCards,
				preserveTailTableCardIndex:
					preservedTailTableCardIndex,
				suppressGardenEelCaptureRecord:
					gardenEelSandBiteBonusPoints > 0,
				expectedTableCardReferences:
					captureTableReferenceSnapshot,
				commitImmediatelyBeforeApply: () =>
				{
					if (gardenEelSandBiteTriggered)
					{
						ConsumeGardenEelSandBiteBonusPoints(
							playerIndex,
							captureStartIndex,
							scorePlayerIndex,
							hasDoubleEffect,
							playedRankCollectsTable);
					}

						if (IsNetworkMultiplayerMode()
						&& IsNetworkHost()
						&& TryGetCurrentAuthoritativeVisualSnapshotId(
							out int snapshotId))
					{
						GetNetworkManager()
							?.BroadcastCaptureVisualEventFromHost(
								captureVisualSync,
								turnSequenceGeneration,
								snapshotId);
					}
					},
					blackLedgerRedirectTableCardIndex:
						blackLedgerRedirectTableCardIndex,
				baseCardActionContext: baseCardActionContext,
				actionSource: "base_card",
				matchSource: captureMatchSource,
				triggeredAttachedItemCardIds:
					triggeredAttachedItemCardIds);
		if (!captureResult.Applied
			|| captureResult.Aborted)
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		int collectedCardCount = captureResult.CollectedCardCount;
		if (IsGameOver())
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(
					true,
					true,
					collectedCardCount > 0),
				false);
		}

		Shuffle(_playerDecks[playerIndex]);
		Cut(_playerDecks[playerIndex]);
		if (blackLedgerRedirectTableCardIndex >= 0
			&& playerIndex != MainPlayerIndex)
		{
			Shuffle(_playerDecks[MainPlayerIndex]);
			Cut(_playerDecks[MainPlayerIndex]);
		}
		if (preserveTailTableCard
			&& preservedTailTableCard != null
			&& _tableCards.Contains(preservedTailTableCard))
		{
			preservedTailTableCard.IsFaceUp = true;
		}
		if (!QuestionRewardPresentationRules
			.ShouldDelayTableRenderForQuestionReward(
				captureResult.QuestionRewardAnimation.HasValue,
				IsLocalHandPlayer(playerIndex)))
		{
			RenderTableCards();
		}
		if (collectedCardCount > 0)
		{
			RenderPlayerCardPiles();
			await PlayPhysicalDeckShuffleAsync(playerIndex);
			if (turnSequenceGeneration != _turnSequenceGeneration)
			{
				return new CardPlayBranchResult(
					true,
					new PlayCardResult(false, false),
					false);
			}
			if (blackLedgerRedirectTableCardIndex >= 0
				&& playerIndex != MainPlayerIndex)
			{
				await PlayPhysicalDeckShuffleAsync(MainPlayerIndex);
				if (turnSequenceGeneration != _turnSequenceGeneration)
				{
					return new CardPlayBranchResult(
						true,
						new PlayCardResult(false, false),
						false);
				}
			}
		}

			DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);
		if (captureResult.QuestionRewardAnimation.HasValue
			&& !captureResult.QuestionRewardAnimation.Value
				.RequiresMagnetAttachment)
		{
			HideHandDisplaySlot(
				playerIndex,
				_playerHands[playerIndex].Count
					+ captureResult.QuestionRewardAnimation.Value
						.ItemHandIndex);
		}

			await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		SendQuestionRewardVisualEventIfNeeded(
			playerIndex,
			captureResult.QuestionRewardAnimation,
			turnSequenceGeneration);
		await PlayQuestionCardRewardAnimationAsync(
			playerIndex,
			captureResult.QuestionRewardAnimation,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}
		await ApplyAdventureCaptureRewardEffectsAsync(
			playerIndex,
			captureResult.AdventureQuestionRewardEffects,
			captureResult.AdventurePostCaptureEffects,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		await MaybeResolveAppliedAdventureQuestionRewardRejudgeAsync(
			playerIndex,
			captureResult.QuestionRewardAnimation,
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		await ResolvePendingMagnetAttachmentsAsync(
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration)
		{
			return new CardPlayBranchResult(
				true,
				new PlayCardResult(false, false),
				false);
		}

		RenderPlayerHands();
			return new CardPlayBranchResult(
				false,
				default,
				resolvedOvertimeTriggerIndexes.Count > 0,
				captureVisualSync,
				captureResult.AdventureCaptureContext?.Action);
	}
	finally
	{
		ClearCardPlayTriggerScratchSets();
		if (!pairCaptureEdgeFloatHandedToDeck
			&& pairCaptureEdgeFloat != null)
		{
			if (IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot))
			{
				RestorePairCaptureEdgeFloatHold(
					pairCaptureEdgeFloat);
			}
			else
			{
				pairCaptureEdgeFloat.QueueFreeOverlays(
					_animationPresenter);
			}
		}
	}
}

	private async Task<CardPlayBranchResult> ResolveMissedPlayedCardAsync(
		int playerIndex,
		PlayingCard playedCard,
		IReadOnlyCollection<TableCard> prePlayTableCards,
		int playedTableCardIndex,
		BaseCardActionContext baseCardActionContext,
		int turnSequenceGeneration)
	{
		bool overtimeTriggered = false;
		var triggeredAttachedItemCardIds = new List<string>(
			baseCardActionContext.TriggeredAttachedItemCardIds);
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
				if (overtimeTriggered
					&& TryGetAttachedItemIdByEffectType(
						playedCard,
						CardEffectTypes.ExtraTurn,
						out string overtimeItemCardId)
					&& !triggeredAttachedItemCardIds.Contains(overtimeItemCardId))
				{
					triggeredAttachedItemCardIds.Add(overtimeItemCardId);
				}
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

		bool emptyTideContinuationTriggered =
			await TryResolveEmptyTideContinuationAfterMissAsync(
				playedTableCardIndex,
				turnSequenceGeneration);
		if (emptyTideContinuationTriggered
			&& TryGetAttachedItemIdByEffectType(
				playedCard,
				CardEffectTypes.EmptyTideContinuation,
				out string emptyTideItemCardId)
			&& !triggeredAttachedItemCardIds.Contains(emptyTideItemCardId))
		{
			triggeredAttachedItemCardIds.Add(emptyTideItemCardId);
		}
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
		triggeredAttachedItemCardIds.Sort(StringComparer.Ordinal);
		baseCardActionContext = baseCardActionContext with
		{
			TriggeredAttachedItemCardIds = triggeredAttachedItemCardIds,
		};
		await ApplyAdventurePostCaptureEffectsAsync(
			playerIndex,
			ResolveAdventureEnchantedRecoveryActionEffects(
				playerIndex,
				triggeredAttachedItemCardIds,
				successfulCapture: false),
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}
		AdventureMainPlayerMissEffectResult mainPlayerMissEffects = ApplyAdventureMainPlayerMissEffects(playerIndex);
		if (!await ApplyAdventureMainPlayerMissTableEffectsAsync(mainPlayerMissEffects, playedTableCardIndex, turnSequenceGeneration))
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}
		ApplyAdventureEnemyMissEffects(playerIndex, playedTableCardIndex, prePlayTableCards);
		if (mainPlayerMissEffects.GrantExtraBaseCardAction)
		{
			overtimeTriggered = TryGrantAdventureMainPlayerExtraBaseCardAction(
				AdventureContinueBranchUpgradeId) || overtimeTriggered;
		}

		return new CardPlayBranchResult(
			false,
			default,
			overtimeTriggered,
			ResolvedActionContext: baseCardActionContext);
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
		NetworkCaptureVisualSync? CaptureVisualSync = null,
		BaseCardActionContext? ResolvedActionContext = null);

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

		bool collectBySuit = ShouldCollectBySuit(playedCard);
		bool suitMatchIsFallbackOnly = collectBySuit
			&& !IsAttachedEffectType(playedCard, CardEffectTypes.SuitCollect)
			&& ShouldAdventureBorrowSuitSetAffectPlayedCard(playedCard);
		return CardPlayResolver.Resolve(
			ToCardPlayTableCards(),
			new CardPlayCard(
				primaryRank,
				ShouldPlayedCardCollectTable(playedCard),
				playedCard.Suit,
				collectBySuit,
				suitMatchIsFallbackOnly),
			alternateRanks.Count > 0 ? alternateRanks : null,
			insertAtTableStart: ShouldInsertMissedCardAtTableStart(playedCard),
			tableInsertIndexOverride: tableInsertIndexOverride,
			playedCardFacesUp: playedCardFacesUp);
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
		IReadOnlyList<string> alternateRanks = BorrowTideRules.GetAlternateRanks(
			IsAttachedEffectType(playedCard, CardEffectTypes.BorrowTide),
			playedCard.Rank);
		if (alternateRanks.Count == 0)
		{
			return null;
		}

		return new List<string>(alternateRanks);
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
