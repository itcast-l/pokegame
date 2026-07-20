using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private CaptureResolutionCoordinator? _captureResolutionCoordinator;

	private CaptureResolutionCoordinator CaptureResolution
		=> _captureResolutionCoordinator ??= new CaptureResolutionCoordinator(this);

	private Task<CardPlayBranchResult> ResolvePlayedCardCaptureAsync(
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
		return CaptureResolution.ResolveAsync(new CaptureResolutionRequest(
			playerIndex,
			playedCard,
			prePlayTableCards,
			captureStartIndex,
			playedTableCardIndex,
			matchedTableCardFlipped,
			playedRankCollectsTable,
			adventureEnemyCurrentPlayCaptureDamageDelta,
			baseCardActionContext,
			captureMatchSource,
			turnSequenceGeneration));
	}

	private readonly record struct CaptureResolutionRequest(
		int PlayerIndex,
		PlayingCard PlayedCard,
		IReadOnlyCollection<TableCard> PrePlayTableCards,
		int CaptureStartIndex,
		int PlayedTableCardIndex,
		bool MatchedTableCardFlipped,
		bool PlayedRankCollectsTable,
		int AdventureEnemyCurrentPlayCaptureDamageDelta,
		BaseCardActionContext BaseActionContext,
		AdventureCaptureMatchSource CaptureMatchSource,
		int TurnSequenceGeneration);

	private sealed class CaptureResolutionCoordinator
	{
		private readonly GameBoardScene _board;
		private readonly HashSet<int> _pendingFaceUpMagnetTriggerIndexes = new();
		private readonly HashSet<int> _resolvedMagnetTriggerIndexes = new();
		private readonly HashSet<int> _magnetExtraCaptureIndexes = new();
		private readonly HashSet<int> _resolvedDoubleTriggerIndexes = new();
		private readonly HashSet<int> _resolvedOvertimeTriggerIndexes = new();
		private readonly List<TableSpecialCardTrigger> _capturedSpecialCardTriggers = new();

		internal CaptureResolutionCoordinator(GameBoardScene board)
		{
			_board = board;
		}

		internal HashSet<int> PendingFaceUpMagnetTriggerIndexes => _pendingFaceUpMagnetTriggerIndexes;
		internal HashSet<int> ResolvedMagnetTriggerIndexes => _resolvedMagnetTriggerIndexes;
		internal HashSet<int> MagnetExtraCaptureIndexes => _magnetExtraCaptureIndexes;
		internal HashSet<int> ResolvedDoubleTriggerIndexes => _resolvedDoubleTriggerIndexes;
		internal HashSet<int> ResolvedOvertimeTriggerIndexes => _resolvedOvertimeTriggerIndexes;
		internal List<TableSpecialCardTrigger> CapturedSpecialCardTriggers => _capturedSpecialCardTriggers;

		internal Task<CardPlayBranchResult> ResolveAsync(CaptureResolutionRequest request)
		{
			return _board.ResolvePlayedCardCaptureCoreAsync(this, request);
		}

		internal HashSet<int> BeginOvertimeTriggerResolution()
		{
			_resolvedOvertimeTriggerIndexes.Clear();
			return _resolvedOvertimeTriggerIndexes;
		}

		internal void EndOvertimeTriggerResolution()
		{
			_resolvedOvertimeTriggerIndexes.Clear();
		}

		internal void ClearTriggerScratch()
		{
			_pendingFaceUpMagnetTriggerIndexes.Clear();
			_resolvedMagnetTriggerIndexes.Clear();
			_magnetExtraCaptureIndexes.Clear();
			_resolvedDoubleTriggerIndexes.Clear();
			_resolvedOvertimeTriggerIndexes.Clear();
			_capturedSpecialCardTriggers.Clear();
		}
	}

	private async Task<CardPlayBranchResult> ResolvePlayedCardCaptureCoreAsync(
		CaptureResolutionCoordinator scratch,
		CaptureResolutionRequest request)
	{

		int playerIndex = request.PlayerIndex;
		PlayingCard playedCard = request.PlayedCard;
		IReadOnlyCollection<TableCard> prePlayTableCards = request.PrePlayTableCards;
		int captureStartIndex = request.CaptureStartIndex;
		int playedTableCardIndex = request.PlayedTableCardIndex;
		bool matchedTableCardFlipped = request.MatchedTableCardFlipped;
		bool playedRankCollectsTable = request.PlayedRankCollectsTable;
		int adventureEnemyCurrentPlayCaptureDamageDelta = request.AdventureEnemyCurrentPlayCaptureDamageDelta;
		BaseCardActionContext baseCardActionContext = request.BaseActionContext;
		AdventureCaptureMatchSource captureMatchSource = request.CaptureMatchSource;
		int turnSequenceGeneration = request.TurnSequenceGeneration;

	PlayGameSfx(GameSfx.CaptureBite, -1.0f, 0.97f, 1.03f);
	scratch.ClearTriggerScratch();
	HashSet<int> pendingFaceUpMagnetTriggerIndexes =
		scratch.PendingFaceUpMagnetTriggerIndexes;
	HashSet<int> resolvedMagnetTriggerIndexes =
		scratch.ResolvedMagnetTriggerIndexes;
	HashSet<int> magnetExtraCaptureIndexes =
		scratch.MagnetExtraCaptureIndexes;
	HashSet<int> resolvedDoubleTriggerIndexes =
		scratch.ResolvedDoubleTriggerIndexes;
		HashSet<int> resolvedOvertimeTriggerIndexes =
			scratch.ResolvedOvertimeTriggerIndexes;
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

		await WaitForBoardDelayAsync(TableCaptureDelaySeconds);
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
				scratch.CapturedSpecialCardTriggers;
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
			TrackBoardTask(PlayTableCardPointPopupAsync(
				captureStartIndex,
				gardenEelSandBiteBonusPoints,
				hasDoubleEffect), nameof(PlayTableCardPointPopupAsync));
		}
		if (endpointCrownBonusPoints > 0)
		{
			TrackBoardTask(PlayTableCardPointPopupAsync(
				captureStartIndex,
				endpointCrownBonusPoints,
				hasDoubleEffect), nameof(PlayTableCardPointPopupAsync));
		}

		int captureTailTableCardIndex =
			GetCaptureTailTableCardIndex(captureStartIndex);
		if (midTideUpgradeBonusPoints > 0
			&& captureTailTableCardIndex >= 0)
		{
			TrackBoardTask(PlayTableCardPointPopupAsync(
				captureTailTableCardIndex,
				midTideUpgradeBonusPoints,
				hasDoubleEffect), nameof(PlayTableCardPointPopupAsync));
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
		scratch.ClearTriggerScratch();
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
}
