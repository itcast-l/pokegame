using System;
using System.Collections.Generic;

public partial class GameBoardScene
{
	private readonly record struct AdventureClubCaptureExtensionPlan(
		TableCard? GapRootingTarget,
		AdventureUpgradeEffectState? PlannedEffectState);

	private readonly record struct AdventureCapturePreparation(
		CaptureResolutionContext Context,
		AdventureCaptureDamageInput DamageInput);

	private BaseCardActionContext CreateSyntheticCaptureActionContext(string actionSource)
	{
		long actionId = _state.AdventureNextBaseCardActionId;
		_state.AdventureNextBaseCardActionId = actionId == long.MaxValue ? 1 : actionId + 1;
		return new BaseCardActionContext(
			actionId,
			RootChainId: 0,
			actionSource,
			IsExtraAction: false,
			PlayedCardInstanceId: 0,
			PlayedCard: default,
			Array.Empty<string>());
	}

	private AdventureCaptureMatchSource ResolveAdventureCaptureMatchSource(
		PlayingCard playedCard,
		CardPlayPlan plan)
	{
		if (plan.PlayedCardCollectsTable)
		{
			return AdventureCaptureMatchSource.CollectAll;
		}

		if (!string.Equals(plan.EffectiveRank, playedCard.Rank, StringComparison.Ordinal))
		{
			return AdventureCaptureMatchSource.AlternateRank;
		}

		if (plan.MatchingTableCardIndex >= 0
			&& plan.MatchingTableCardIndex < _tableCards.Count)
		{
			PlayingCard? matchedCard = _tableCards[plan.MatchingTableCardIndex].Card;
			if (matchedCard.HasValue
				&& !string.Equals(matchedCard.Value.Rank, playedCard.Rank, StringComparison.Ordinal)
				&& string.Equals(matchedCard.Value.Suit, playedCard.Suit, StringComparison.Ordinal))
			{
				return IsAttachedEffectType(playedCard, CardEffectTypes.SuitCollect)
					? AdventureCaptureMatchSource.AttachedSuitCollect
					: AdventureCaptureMatchSource.SuitFallback;
			}
		}

		return AdventureCaptureMatchSource.Rank;
	}

	private IReadOnlyList<string> BuildPlayedCardPlanTriggeredAttachmentIds(
		PlayingCard playedCard,
		CardPlayPlan plan,
		bool capturedMiddleCards)
	{
		var triggeredIds = new List<string>();
		void AddEffect(string effectType)
		{
			if (TryGetAttachedItemIdByEffectType(playedCard, effectType, out string itemCardId)
				&& !triggeredIds.Contains(itemCardId))
			{
				triggeredIds.Add(itemCardId);
			}
		}

		if (BorrowTideRules.DidUseBorrowTide(
			IsAttachedEffectType(playedCard, CardEffectTypes.BorrowTide),
			playedCard.Rank,
			plan.EffectiveRank))
		{
			AddEffect(CardEffectTypes.BorrowTide);
		}

		if (ResolveAdventureCaptureMatchSource(playedCard, plan)
			== AdventureCaptureMatchSource.AttachedSuitCollect)
		{
			AddEffect(CardEffectTypes.SuitCollect);
		}

		if (plan.CaptureStartIndex < 0
			&& plan.TableInsertIndex == 0
			&& IsAttachedEffectType(playedCard, CardEffectTypes.ReverseFlowAnchor))
		{
			AddEffect(CardEffectTypes.ReverseFlowAnchor);
		}

		if (capturedMiddleCards)
		{
			AddEffect(CardEffectTypes.MagnetExtraCapture);
			AddEffect(CardEffectTypes.DoubleCollect);
			AddEffect(CardEffectTypes.ExtraTurn);
		}

		triggeredIds.Sort(StringComparer.Ordinal);
		return triggeredIds;
	}

	private void AddTriggeredAttachmentIdForEffect(
		ICollection<string> triggeredIds,
		TableCard tableCard,
		string effectType)
	{
		if (tableCard.Card.HasValue
			&& TryGetAttachedItemIdByEffectType(tableCard.Card.Value, effectType, out string itemCardId)
			&& !triggeredIds.Contains(itemCardId))
		{
			triggeredIds.Add(itemCardId);
		}
	}

	private void AddTriggeredAttachmentIdsForIndexes(
		ICollection<string> triggeredIds,
		IEnumerable<int> tableCardIndexes,
		string effectType)
	{
		foreach (int tableCardIndex in tableCardIndexes)
		{
			if (tableCardIndex >= 0 && tableCardIndex < _tableCards.Count)
			{
				AddTriggeredAttachmentIdForEffect(
					triggeredIds,
					_tableCards[tableCardIndex],
					effectType);
			}
		}
	}

	private void AddTriggeredAttachmentIdsInCapture(
		ICollection<string> triggeredIds,
		int captureStartIndex,
		IEnumerable<int>? extraTableCardIndexes,
		string effectType)
	{
		for (int tableCardIndex = Math.Max(0, captureStartIndex);
			tableCardIndex < _tableCards.Count;
			tableCardIndex++)
		{
			if (!_tableCards[tableCardIndex].IsBuriedInSand)
			{
				AddTriggeredAttachmentIdForEffect(
					triggeredIds,
					_tableCards[tableCardIndex],
					effectType);
			}
		}

		if (extraTableCardIndexes != null)
		{
			AddTriggeredAttachmentIdsForIndexes(
				triggeredIds,
				extraTableCardIndexes,
				effectType);
		}
	}

	private AdventureClubCaptureExtensionPlan
		BuildAdventureClubCaptureExtensionPlan(
			int playerIndex,
			int captureStartIndex,
			IReadOnlySet<int>? extraTableCardIndexes)
	{
		if (playerIndex != MainPlayerIndex)
		{
			return default;
		}

		bool hasScoringCaptureCard = false;
		for (int tableCardIndex = captureStartIndex + 1;
			tableCardIndex < _tableCards.Count - 1;
			tableCardIndex++)
		{
			TableCard middleTableCard = _tableCards[tableCardIndex];
			if (!middleTableCard.IsBuriedInSand
				&& middleTableCard.Card.HasValue
				&& GetCardScoreValue(middleTableCard.Card.Value) > 0)
			{
				hasScoringCaptureCard = true;
				break;
			}
		}

		if (!hasScoringCaptureCard && extraTableCardIndexes != null)
		{
			foreach (int tableCardIndex in extraTableCardIndexes)
			{
				if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
				{
					continue;
				}

				TableCard extraTableCard = _tableCards[tableCardIndex];
				if (!extraTableCard.IsBuriedInSand
					&& extraTableCard.Card.HasValue
					&& GetCardScoreValue(extraTableCard.Card.Value) > 0)
				{
					hasScoringCaptureCard = true;
					break;
				}
			}
		}

		bool leftAdjacentBaseCardAvailable = captureStartIndex > 0
			&& !_tableCards[captureStartIndex - 1].IsBuriedInSand
			&& _tableCards[captureStartIndex - 1].Card.HasValue;
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return default;
		}

		AdventureUpgradeEffectState plannedEffectState =
			runState.EffectState.Clone();
		AdventureClubCaptureExtensionResult result =
			AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
				plannedEffectState,
				runState.GetAcquiredUpgradeIds(),
				hasScoringCaptureCard,
				leftAdjacentBaseCardAvailable);
		TableCard? gapRootingTarget = result.CaptureLeftAdjacentBaseCard
			? _tableCards[captureStartIndex - 1]
			: null;
		return new AdventureClubCaptureExtensionPlan(
			gapRootingTarget,
			plannedEffectState);
	}

	private CaptureResolutionContext BuildAdventureCaptureResolutionContext(
		int playerIndex,
		int captureStartIndex,
		int scorePlayerIndex,
		BaseCardActionContext? actionContext,
		string actionSource,
		CaptureStatePlan capturePlan,
		AdventureCaptureMatchSource matchSource,
		IEnumerable<string>? triggeredAttachedItemCardIds,
		TableCard? gapRootingTarget,
		int preservedTableCardIndex)
	{
		BaseCardActionContext normalizedAction = actionContext
			?? CreateSyntheticCaptureActionContext(actionSource);
		var capturedEntries = new List<AdventureCapturedCardEntry>();

		void AddCapturedEntry(int tableCardIndex, AdventureCaptureEntrySource source)
		{
			if (tableCardIndex < 0
				|| tableCardIndex >= _tableCards.Count
				|| tableCardIndex == preservedTableCardIndex)
			{
				return;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			capturedEntries.Add(
				CreateCapturedEntry(tableCard, tableCardIndex, source));
		}

		AdventureCapturedCardEntry CreateCapturedEntry(
			TableCard tableCard,
			int tableCardIndex,
			AdventureCaptureEntrySource source)
		{
			long baseCardInstanceId = tableCard.Card.HasValue
				&& !tableCard.RemoveCardOnCapture
				? tableCard.Card.Value.InstanceId
				: 0;
			return new AdventureCapturedCardEntry(
				tableCard.InstanceId,
				baseCardInstanceId,
				tableCard,
				tableCardIndex,
				tableCard.IsFaceUp,
				tableCard.IsBuriedInSand,
				baseCardInstanceId > 0
					&& baseCardInstanceId == normalizedAction.PlayedCardInstanceId,
				source);
		}

		foreach (int tableCardIndex in capturePlan.BaseTableCardIndexes)
		{
			AddCapturedEntry(tableCardIndex, AdventureCaptureEntrySource.PrimaryRange);
		}
		foreach (int tableCardIndex in capturePlan.ExtraTableCardIndexes)
		{
			AddCapturedEntry(tableCardIndex, AdventureCaptureEntrySource.MagnetExtension);
		}

		if (gapRootingTarget != null)
		{
			int gapRootingTableCardIndex = _tableCards.IndexOf(gapRootingTarget);
			if (gapRootingTableCardIndex >= 0)
			{
				capturedEntries.Add(CreateCapturedEntry(
					gapRootingTarget,
					gapRootingTableCardIndex,
					AdventureCaptureEntrySource.GapRootingExtension));
			}
		}

		var scoringExtraTableCardIndexes = new HashSet<int>(
			capturePlan.ExtraTableCardIndexes);

		var triggeredAttachmentIds = new List<string>(
			triggeredAttachedItemCardIds ?? Array.Empty<string>());
		var seenCapturedTableCardIds = new HashSet<long>();
		var endpointCrownAttachmentIds = new List<string>();
		var midTideAttachmentIds = new List<string>();
		int visibleCapturedCardCount = 0;
		foreach (AdventureCapturedCardEntry entry in capturedEntries)
		{
			if (!seenCapturedTableCardIds.Add(entry.TableCardInstanceId))
			{
				continue;
			}

			if (entry.WasFaceUp && !entry.WasBuried)
			{
				visibleCapturedCardCount++;
			}
			if (!entry.TableCard.Card.HasValue)
			{
				continue;
			}

			PlayingCard card = entry.TableCard.Card.Value;
			if (TryGetAttachedItemIdByEffectType(
				card,
				CardEffectTypes.EndpointCrown,
				out string endpointCrownItemCardId))
			{
				endpointCrownAttachmentIds.Add(endpointCrownItemCardId);
			}
			if (TryGetAttachedItemIdByEffectType(
				card,
				CardEffectTypes.MidTideUpgrade,
				out string midTideItemCardId))
			{
				midTideAttachmentIds.Add(midTideItemCardId);
			}
		}

		bool hasCompleteMiddleCapture = visibleCapturedCardCount > 2;
		bool includeCrownCanopyEndpoints = playerIndex == MainPlayerIndex
			&& normalizedAction.PlayedCardInstanceId > 0
			&& AdventureUpgradeEffectService.ResolveClubCaptureScoring(
				GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds()
					?? Array.Empty<string>())
				.IncludeCaptureEndpointsInScoring;
		bool includeCaptureStartInScoring =
			includeCrownCanopyEndpoints
			|| endpointCrownAttachmentIds.Count > 0;
		bool includeCaptureEndInScoring =
			includeCrownCanopyEndpoints
			|| (hasCompleteMiddleCapture && midTideAttachmentIds.Count > 0);
		if (includeCaptureStartInScoring)
		{
			triggeredAttachmentIds.AddRange(endpointCrownAttachmentIds);
		}
		if (includeCaptureEndInScoring)
		{
			triggeredAttachmentIds.AddRange(midTideAttachmentIds);
		}

		var scoringCardRawPoints = new List<int>();
		var scoringCardResolvedPoints = new List<int>();
		var scoringCardTableIndexes = new List<int>();
		BuildAdventureCaptureDamageCardPointLists(
			ToCaptureScoringTableCards(),
			captureStartIndex,
			scorePlayerIndex,
			scoringExtraTableCardIndexes,
			scoringCardRawPoints,
			scoringCardResolvedPoints,
			scoringCardTableIndexes,
			includeCaptureStartInScoring,
			includeCaptureEndInScoring,
			preservedTableCardIndex);

		int scoreItemCount = Math.Min(
			scoringCardTableIndexes.Count,
			Math.Min(scoringCardRawPoints.Count, scoringCardResolvedPoints.Count));
		var scoreItems = new List<AdventureCaptureScoreItem>(scoreItemCount);
		var scoreItemTableIndexes = new List<int>(scoreItemCount);
		for (int index = 0; index < scoreItemCount; index++)
		{
			int tableCardIndex = scoringCardTableIndexes[index];
			if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			long baseCardInstanceId = tableCard.Card.HasValue
				&& !tableCard.RemoveCardOnCapture
				? tableCard.Card.Value.InstanceId
				: 0;
			scoreItems.Add(new AdventureCaptureScoreItem(
				tableCard.InstanceId,
				baseCardInstanceId,
				scoringCardRawPoints[index],
				scoringCardResolvedPoints[index],
				IsExplicitDuplicate: false,
				SourceUpgradeId: string.Empty));
			scoreItemTableIndexes.Add(tableCardIndex);
		}

		if (playerIndex == MainPlayerIndex
			&& GetAdventureUpgradeRunState()
				?.HasAcquiredUpgrade("diamonds_top_prize_repeat") == true)
		{
			int highestScoreItemIndex = AdventureCaptureScoreItemRules
				.ResolveTopPrizeRepeatIndex(scoreItems, scoreItemTableIndexes);
			if (highestScoreItemIndex >= 0)
			{
				scoreItems.Add(scoreItems[highestScoreItemIndex] with
				{
					IsExplicitDuplicate = true,
					SourceUpgradeId = "diamonds_top_prize_repeat",
				});
			}
		}

		bool includesQuestionReward = false;
		foreach (AdventureCapturedCardEntry entry in capturedEntries)
		{
			if (IsQuestionTableCard(entry.TableCard))
			{
				includesQuestionReward = true;
				break;
			}
		}

		return new CaptureResolutionContext(
			normalizedAction,
			capturedEntries,
			scoreItems,
			matchSource,
			triggeredAttachmentIds)
		{
			IncludesQuestionReward = includesQuestionReward,
		};
	}

	private AdventureCapturePreparation BuildAdventureCapturePreparation(
		int playerIndex,
		int captureStartIndex,
		int scorePlayerIndex,
		CaptureStatePlan capturePlan,
		IReadOnlyCollection<TableCard>? prePlayTableCards,
		int adventureFlatDamageDelta,
		BaseCardActionContext? actionContext,
		string actionSource,
		AdventureCaptureMatchSource matchSource,
		IEnumerable<string>? triggeredAttachedItemCardIds,
		TableCard? gapRootingTarget,
		int preservedTableCardIndex)
	{
		CaptureResolutionContext context =
			BuildAdventureCaptureResolutionContext(
				playerIndex,
				captureStartIndex,
				scorePlayerIndex,
				actionContext,
				actionSource,
				capturePlan,
				matchSource,
				triggeredAttachedItemCardIds,
				gapRootingTarget,
				preservedTableCardIndex);

		long baseDamage = 0;
		var rawPoints = new List<int>(context.NormalScoreItems.Count);
		var resolvedPoints = new List<int>(context.NormalScoreItems.Count);
		var tableIndexes = new List<int>(context.NormalScoreItems.Count);
		var capturedEntriesById = new Dictionary<long, AdventureCapturedCardEntry>();
		foreach (AdventureCapturedCardEntry entry in context.CapturedCards)
		{
			capturedEntriesById[entry.TableCardInstanceId] = entry;
		}
		foreach (AdventureCaptureScoreItem scoreItem in context.NormalScoreItems)
		{
			baseDamage = Math.Min(
				int.MaxValue,
				baseDamage + Math.Max(0L, scoreItem.ResolvedPoints));
			rawPoints.Add(scoreItem.RawPoints);
			resolvedPoints.Add(scoreItem.ResolvedPoints);
			tableIndexes.Add(
				capturedEntriesById.TryGetValue(
					scoreItem.TableCardInstanceId,
					out AdventureCapturedCardEntry capturedEntry)
					? capturedEntry.PreCaptureTableIndex
					: -1);
		}

		int oldTableDamageDelta = adventureFlatDamageDelta;
		if (prePlayTableCards != null && prePlayTableCards.Count > 0)
		{
			var oldTableCards = new HashSet<TableCard>(prePlayTableCards);
			int capturedOldScoringCardCount = 0;
			foreach (AdventureCaptureScoreItem scoreItem in context.NormalScoreItems)
			{
				if (capturedEntriesById.TryGetValue(
						scoreItem.TableCardInstanceId,
						out AdventureCapturedCardEntry capturedEntry)
					&& oldTableCards.Contains(capturedEntry.TableCard))
				{
					capturedOldScoringCardCount++;
				}
			}

			bool capturedTraceMark = false;
			bool capturedHuntMark = false;
			foreach (AdventureCapturedCardEntry capturedEntry in context.CapturedCards)
			{
				capturedTraceMark |= ReferenceEquals(
					capturedEntry.TableCard,
					_adventureTraceHuntMarkedTableCard);
				capturedHuntMark |= ReferenceEquals(
					capturedEntry.TableCard,
					_adventureHuntOrderMarkedTableCard);
			}

			int resolvedOldTableDelta = AdventureUpgradeEffectService
				.ResolveOldTableCaptureDamageDelta(
					GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds()
						?? Array.Empty<string>(),
					playerIndex == MainPlayerIndex,
					capturedOldScoringCardCount,
					capturedTraceMark,
					capturedHuntMark);
			oldTableDamageDelta = (int)Math.Clamp(
				(long)oldTableDamageDelta + resolvedOldTableDelta,
				int.MinValue,
				int.MaxValue);
		}

		AdventureCaptureDamageInput damageInput =
			FreezeAdventureCaptureDamageInput(
				playerIndex,
				new AdventureCaptureDamageInput(
					BaseDamage: (int)baseDamage,
					FlatDamageDelta: oldTableDamageDelta,
					IsFirstCaptureThisBattle:
						playerIndex == MainPlayerIndex
						&& _adventureMainPlayerCaptureCountThisBattle == 0,
					IncludesQuestionReward: context.IncludesQuestionReward,
					QuestionRewardEchoActive:
						GetAdventureUpgradeRunState()
							?.IsQuestionRewardEchoActive == true,
					ScoringCardRawPoints: rawPoints,
					ScoringCardResolvedPoints: resolvedPoints,
					ScoringCardTableIndexes: tableIndexes,
					CapturerIsMainPlayer: playerIndex == MainPlayerIndex,
					CaptureContext: context));
		return new AdventureCapturePreparation(context, damageInput);
	}
}
