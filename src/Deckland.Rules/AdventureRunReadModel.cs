using System.Collections.Immutable;

namespace Deckland.Rules;

public sealed partial record MatchReadModel
{
	public AdventureRunReadModel Adventure { get; init; } = AdventureRunReadModel.Empty;
}

public sealed record AdventureRunReadModel(
	long Revision,
	bool IsActive,
	bool HasReadyManifest,
	string CampaignId,
	int MainStageIndex,
	string EnemyAvatarId,
	int CurrentAvatarStageNumber,
	int CurrentRunStageIndex,
	bool ShouldGrantRunStartUpgrade,
	bool ShouldGrantVictoryUpgrade,
	int CurrentHealth,
	int MaxHealth,
	bool HasUsedRevive,
	ImmutableArray<string> AcquiredUpgradeIds,
	ImmutableArray<string> RetiredUpgradeIds,
	AdventureEffectStateSnapshot Effects)
{
	public static int MaxActiveUpgradeCount =>
		global::AdventureUpgradeRunState.MaxActiveUpgradeCount;

	public static int MaxClubMissClimbGrowthStacks =>
		global::AdventureUpgradeEffectState.MaxClubMissClimbGrowthStacks;

	public static AdventureRunReadModel Empty { get; } = new(
		0,
		false,
		false,
		string.Empty,
		0,
		string.Empty,
		0,
		0,
		false,
		false,
		100,
		100,
		false,
		ImmutableArray<string>.Empty,
		ImmutableArray<string>.Empty,
		default);

	public bool IsQuestionRewardEchoActive =>
		Effects.FinalDoubleCaptureDamageActive;

	public bool HasAcquiredUpgrade(string upgradeId)
	{
		if (string.IsNullOrWhiteSpace(upgradeId))
		{
			return false;
		}

		return AcquiredUpgradeIds.Contains(upgradeId.Trim(), StringComparer.Ordinal);
	}

	public ImmutableArray<string> GetAcquiredUpgradeIds() => AcquiredUpgradeIds;
	public ImmutableArray<string> GetRetiredUpgradeIds() => RetiredUpgradeIds;

	public bool HasRetiredUpgrade(string upgradeId)
	{
		return !string.IsNullOrWhiteSpace(upgradeId)
			&& RetiredUpgradeIds.Contains(upgradeId.Trim(), StringComparer.Ordinal);
	}

	public IReadOnlyList<global::AdventureUpgradeTriggerSource> BuildUpgradeTriggerOrder(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered) =>
		global::AdventureUpgradeTriggerRules.BuildTriggerOrder(
			runStartTriggered,
			bigCaptureTriggered,
			victoryTriggered);

	public IReadOnlyList<global::AdventureUpgradeDefinitionModel> BuildUpgradeOffer(
		IReadOnlyList<global::AdventureUpgradeDefinitionModel> definitions,
		Func<int, int> nextIntExclusive,
		global::AdventureUpgradeManifest? manifest = null) =>
		global::AdventureUpgradeChoiceRules.BuildOffer(
			definitions,
			AcquiredUpgradeIds,
			Math.Max(1, CurrentRunStageIndex),
			nextIntExclusive,
			manifest,
			RetiredUpgradeIds);

	public bool ShouldGrantBigCaptureUpgrade(int totalCapturedCardCount) =>
		global::AdventureUpgradeChoiceRules.ShouldGrantBigCapture(totalCapturedCardCount);

	public int GetUpgradeNonNegativeInt(string upgradeId, string key) =>
		global::AdventureUpgradeRuntimeValues.GetNonNegativeInt(upgradeId, key);

	public global::AdventureClubCaptureExtensionResult ResolveClubCaptureExtensions(
		bool hasMiddleScoringCards,
		bool leftAdjacentBaseCardAvailable) =>
		global::AdventureUpgradeEffectService.ResolveClubCaptureExtensions(
			CreateEffectState(Effects),
			AcquiredUpgradeIds,
			hasMiddleScoringCards,
			leftAdjacentBaseCardAvailable);

	public global::AdventureClubCaptureScoringResult ResolveClubCaptureScoring() =>
		global::AdventureUpgradeEffectService.ResolveClubCaptureScoring(AcquiredUpgradeIds);

	public int ResolveOldTableCaptureDamageDelta(
		bool capturingPlayerIsMain,
		int capturedOldScoringCardCount,
		bool capturedTraceMark,
		bool capturedHuntMark) =>
		global::AdventureUpgradeEffectService.ResolveOldTableCaptureDamageDelta(
			AcquiredUpgradeIds,
			capturingPlayerIsMain,
			capturedOldScoringCardCount,
			capturedTraceMark,
			capturedHuntMark);

	public global::AdventureEnemyMissEffectResult ResolveEnemyMissEffects(
		int tableCardCountAfterMiss,
		bool hadOldTableCardsBeforeMiss) =>
		global::AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			AcquiredUpgradeIds,
			tableCardCountAfterMiss,
			hadOldTableCardsBeforeMiss);

	public global::AdventureEnemyCaptureEffectResult ResolveEnemyCaptureEffects() =>
		global::AdventureUpgradeEffectService.ResolveEnemyCaptureEffects(AcquiredUpgradeIds);

	public global::AdventureCaptureRewardApplicationPlan BuildCaptureRewardApplicationPlan(
		global::AdventureQuestionRewardEffectResult? questionRewardEffects,
		global::AdventurePostCaptureEffectResult? postCaptureEffects,
		int currentHandCount,
		int maximumHandCount,
		int availableDeckCardCount) =>
		global::AdventureUpgradeEffectService.BuildCaptureRewardApplicationPlan(
			questionRewardEffects,
			postCaptureEffects,
			currentHandCount,
			maximumHandCount,
			availableDeckCardCount);

	public global::AdventureCaptureDamageResult PreviewCaptureDamage(
		global::AdventureCaptureDamageInput input) =>
		global::AdventureUpgradeEffectService.ApplyCaptureDamage(
			CreateEffectState(Effects),
			AcquiredUpgradeIds,
			input);

	public global::AdventureCaptureDamageModifierSnapshot FreezeCaptureDamageModifiers() =>
		global::AdventureUpgradeEffectService.FreezeCaptureDamageModifiers(
			CreateEffectState(Effects),
			AcquiredUpgradeIds);

	public global::AdventureClubPlayAdjustmentResult ResolveClubPlayAdjustments(string playedCardRank) =>
		global::AdventureUpgradeEffectService.ResolveClubPlayAdjustments(
			CreateEffectState(Effects),
			AcquiredUpgradeIds,
			playedCardRank);

	public int GetBattleStartDrawCount() =>
		global::AdventureUpgradeEffectService.GetBattleStartDrawCount(AcquiredUpgradeIds);

	public bool ShouldOfferQuestionRewardRejudge(bool questionRejudgeUsedThisBattle) =>
		global::AdventureUpgradeEffectService.ShouldOfferQuestionRewardRejudge(
			AcquiredUpgradeIds,
			questionRejudgeUsedThisBattle);

	public global::AdventureQuestionRejudgeRuleResult ResolveQuestionRewardRejudgeChoice(
		bool questionRejudgeUsedThisBattle,
		global::AdventureQuestionRejudgeRuleChoice choice) =>
		global::AdventureUpgradeEffectService.ResolveQuestionRewardRejudgeChoice(
			AcquiredUpgradeIds,
			questionRejudgeUsedThisBattle,
			choice);

	public int ResolveBlackLedgerRedirectTableCardIndex(
		bool capturingPlayerIsMain,
		IReadOnlyList<int> tableCardScoreValues,
		int captureStartIndex) =>
		global::AdventureUpgradeEffectService.ResolveBlackLedgerRedirectTableCardIndex(
			global::AdventureUpgradeEffectService.HasBlackLedger(AcquiredUpgradeIds),
			capturingPlayerIsMain,
			tableCardScoreValues,
			captureStartIndex);

	public bool ShouldPreserveEnemyCaptureTailCard(int totalCapturedCardCount) =>
		global::AdventureUpgradeEffectService.ShouldPreserveEnemyCaptureTailCard(
			AcquiredUpgradeIds,
			totalCapturedCardCount);

	public bool ShouldGrantLinkedStemsQuestionReward(int successfulCapturesThisTurn) =>
		global::AdventureUpgradeEffectService.ShouldGrantLinkedStemsQuestionReward(
			AcquiredUpgradeIds,
			successfulCapturesThisTurn);

	public string FormatUpgradeDescription(
		string upgradeId,
		string? description,
		IReadOnlyDictionary<string, int>? values) =>
		global::AdventureUpgradeDescriptionRules.Format(upgradeId, description, values);

	internal static AdventureRunReadModel FromState(
		global::AdventureUpgradeRunState state,
		long revision)
	{
		ArgumentNullException.ThrowIfNull(state);
		var upgradeIds = state.GetAcquiredUpgradeIds().ToArray();
		Array.Sort(upgradeIds, StringComparer.Ordinal);
		var retiredUpgradeIds = state.GetRetiredUpgradeIds().ToArray();
		Array.Sort(retiredUpgradeIds, StringComparer.Ordinal);
		return new AdventureRunReadModel(
			revision,
			state.IsActive,
			state.HasReadyManifest,
			state.CampaignId,
			state.MainStageIndex,
			state.EnemyAvatarId,
			state.CurrentAvatarStageNumber,
			state.CurrentRunStageIndex,
			state.ShouldGrantRunStartUpgrade,
			state.ShouldGrantVictoryUpgrade,
			state.CurrentHealth,
			state.MaxHealth,
			state.HasUsedRevive,
			upgradeIds.ToImmutableArray(),
			retiredUpgradeIds.ToImmutableArray(),
			ToSnapshot(state.EffectState));
	}

	internal static AdventureEffectStateSnapshot ToSnapshot(
		global::AdventureUpgradeEffectState state) => new(
		state.PermanentCaptureDamageBonus,
		state.QuestionRewardCount,
		state.FinalDoubleCaptureDamageActive,
		state.HealingOverflowRemainder,
		state.NextCaptureDamageBonus,
		state.NextTurnHealAmount,
		state.NextTurnDrawCount,
		state.ClubShortBranchReturnUsedThisTurn,
		state.ClubMissClimbGrowthStacks,
		state.ClubBorrowSuitSetArmed,
		state.ClubGapRootingUsedThisTurn);

	internal static global::AdventureUpgradeEffectState CreateEffectState(
		AdventureEffectStateSnapshot snapshot)
	{
		return new global::AdventureUpgradeEffectState
		{
			PermanentCaptureDamageBonus = snapshot.PermanentCaptureDamageBonus,
			QuestionRewardCount = snapshot.QuestionRewardCount,
			FinalDoubleCaptureDamageActive = snapshot.FinalDoubleCaptureDamageActive,
			HealingOverflowRemainder = snapshot.HealingOverflowRemainder,
			NextCaptureDamageBonus = snapshot.NextCaptureDamageBonus,
			NextTurnHealAmount = snapshot.NextTurnHealAmount,
			NextTurnDrawCount = snapshot.NextTurnDrawCount,
			ClubShortBranchReturnUsedThisTurn = snapshot.ClubShortBranchReturnUsedThisTurn,
			ClubMissClimbGrowthStacks = snapshot.ClubMissClimbGrowthStacks,
			ClubBorrowSuitSetArmed = snapshot.ClubBorrowSuitSetArmed,
			ClubGapRootingUsedThisTurn = snapshot.ClubGapRootingUsedThisTurn,
		};
	}
}
