using System;
using System.Collections.Generic;

namespace Deckland.Rules;

public sealed partial record MatchReadModel
{
	public static int ResolveAdventureTopPrizeRepeatIndex(
		IReadOnlyList<global::AdventureCaptureScoreItem>? scoreItems,
		IReadOnlyList<int>? preCaptureTableIndexes,
		int minimumScoreItemCount = 3) =>
		global::AdventureCaptureScoreItemRules.ResolveTopPrizeRepeatIndex(
			scoreItems,
			preCaptureTableIndexes,
			minimumScoreItemCount);

	public static bool TryPickEarlyTableNewCardIndex(
		IReadOnlyList<global::CardPlayTableCard> tableCards,
		IReadOnlyList<global::CardPlayCard> handCards,
		int randomOrdinal,
		out int handIndex) =>
		global::AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCards,
			handCards,
			randomOrdinal,
			out handIndex);

	public static bool TryPickEarlyTableNewCardIndex(
		int tableCardCount,
		int handCardCount,
		Func<int, bool> isNewTableCardCandidate,
		int randomOrdinal,
		out int handIndex) =>
		global::AiCardSelectionRules.TryPickEarlyTableNewCardIndex(
			tableCardCount,
			handCardCount,
			isNewTableCardCandidate,
			randomOrdinal,
			out handIndex);

	public static global::CaptureFlowPlan BuildCaptureFlow(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		global::CaptureScoringContext context,
		IReadOnlySet<int>? extraTableCardIndexes = null) =>
		global::CaptureFlowPlanner.Build(
			tableCards,
			captureStartIndex,
			scorePlayerIndex,
			context,
			extraTableCardIndexes);

	public static global::CapturePlan ResolveCapture(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex) =>
		global::CaptureResolver.Resolve(tableCards, captureStartIndex);

	public static int[] BuildBaseCaptureRevealIndexes(int captureStartIndex, int tableCardCount) =>
		global::CaptureRevealOrder.BuildBaseRevealIndexes(captureStartIndex, tableCardCount);

	public static global::ScoreBreakdown GetCapturedScore(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int scorePlayerIndex,
		global::CaptureScoringContext context,
		IReadOnlySet<int>? extraTableCardIndexes = null) =>
		global::CaptureScoringRules.GetCapturedScore(
			tableCards,
			captureStartIndex,
			scorePlayerIndex,
			context,
			extraTableCardIndexes);

	public static int GetCapturedScorePlayerIndex(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		int fallbackPlayerIndex,
		int playerCount) =>
		global::CaptureScoringRules.GetCapturedScorePlayerIndex(
			tableCards,
			captureStartIndex,
			fallbackPlayerIndex,
			playerCount);

	public static int GetCapturedTableCardScoreValue(
		global::CaptureScoringTableCard tableCard,
		global::CaptureScoringContext context,
		bool hasDoubleEffect) =>
		global::CaptureScoringRules.GetCapturedTableCardScoreValue(tableCard, context, hasDoubleEffect);

	public static int ApplyCapturedCardScoreModifiers(
		int points,
		global::CaptureScoringContext context,
		bool hasDoubleEffect) =>
		global::CaptureScoringRules.ApplyCapturedCardScoreModifiers(points, context, hasDoubleEffect);

	public static bool HasCapturedDoubleScoreEffect(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		IReadOnlySet<int>? extraTableCardIndexes = null) =>
		global::CaptureScoringRules.HasCapturedDoubleScoreEffect(
			tableCards,
			captureStartIndex,
			extraTableCardIndexes);

	public static bool IsCapturedTableCardScoringIndex(
		int tableCardIndex,
		int captureStartIndex,
		int tableCardCount) =>
		global::CaptureScoringRules.IsCapturedTableCardScoringIndex(
			tableCardIndex,
			captureStartIndex,
			tableCardCount);

	public static bool HasCapturedScoringTableCards(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex,
		IReadOnlySet<int>? extraTableCardIndexes = null) =>
		global::CaptureScoringRules.HasCapturedScoringTableCards(
			tableCards,
			captureStartIndex,
			extraTableCardIndexes);

	public static bool HasCapturedMiddleTableCards(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex) =>
		global::CaptureScoringRules.HasCapturedMiddleTableCards(tableCards, captureStartIndex);

	public static bool IsSlimeScoreSuppressed(
		bool isSlimeCoated,
		global::CaptureScoringContext context) =>
		global::CaptureScoringRules.IsSlimeScoreSuppressed(isSlimeCoated, context);

	public static global::CardPlayPlan ResolveCardPlay(
		IReadOnlyList<global::CardPlayTableCard> tableCards,
		global::CardPlayCard playedCard,
		IReadOnlyList<string>? alternateRanks = null,
		bool insertAtTableStart = false,
		int tableInsertIndexOverride = -1,
		bool playedCardFacesUp = false) =>
		global::CardPlayResolver.Resolve(
			tableCards,
			playedCard,
			alternateRanks,
			insertAtTableStart,
			tableInsertIndexOverride,
			playedCardFacesUp);

	public static int FindMatchingTableCardIndex(
		IReadOnlyList<global::CardPlayTableCard> tableCards,
		string rank) =>
		global::CardPlayResolver.FindMatchingTableCardIndex(tableCards, rank);

	public static global::EnemyAiAdventureProfile GetAdventureAiProfile(int mainStageIndex) =>
		global::EnemyAiRules.GetAdventureProfile(mainStageIndex);

	public static global::EnemyAiAdventureSelectionResult SelectAdventureCard(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		IReadOnlyList<global::PotentialCaptureCard> handCards,
		global::CaptureScoringContext context,
		global::EnemyAiAdventureProfile profile,
		int royalCaptureThreshold,
		int decisionRollPercent,
		int randomCandidateOrdinal) =>
		global::EnemyAiRules.SelectAdventureCard(
			tableCards,
			handCards,
			context,
			profile,
			royalCaptureThreshold,
			decisionRollPercent,
			randomCandidateOrdinal);

	public static global::GameLoopPhase GetGameLoopPhase(global::GameLoopPhaseState state) =>
		global::GameLoopPhaseRules.GetPhase(state);

	public static bool CanBeginMainPlayerAction(global::GameLoopPhaseState state) =>
		global::GameLoopPhaseRules.CanBeginMainPlayerAction(state);

	public static bool CanBeginOpponentSequence(global::GameLoopPhaseState state) =>
		global::GameLoopPhaseRules.CanBeginOpponentSequence(state);

	public static int GetNextPlayerIndex(
		ReadOnlySpan<int> turnOrder,
		int currentPlayerIndex,
		int playerCount,
		int fallbackPlayerIndex) =>
		global::GameTurnRules.GetNextPlayerIndex(
			turnOrder,
			currentPlayerIndex,
			playerCount,
			fallbackPlayerIndex);

	public static bool TryGetNextTurnPlayerIndex(
		ReadOnlySpan<int> turnOrder,
		ReadOnlySpan<global::GameTurnPlayerCardState> playerStates,
		int currentPlayerIndex,
		int playerCount,
		out int nextPlayerIndex) =>
		global::GameTurnRules.TryGetNextTurnPlayerIndex(
			turnOrder,
			playerStates,
			currentPlayerIndex,
			playerCount,
			out nextPlayerIndex);

	public static bool IsSerializedOutcomeValid(
		global::MatchOutcomeKind kind,
		int winnerPlayerIndex,
		int playerCount) =>
		global::MatchOutcomeRules.IsSerializedOutcomeValid(kind, winnerPlayerIndex, playerCount);

	public static bool TryPickCaptureHandIndexForNetworkAiProxy(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		IReadOnlyList<global::PotentialCaptureCard> handCards,
		global::CaptureScoringContext context,
		int decisionRollPercent,
		int randomCaptureOrdinal,
		out int handIndex) =>
		global::NetworkAiFallbackRules.TryPickCaptureHandIndexForNetworkAiProxy(
			tableCards,
			handCards,
			context,
			decisionRollPercent,
			randomCaptureOrdinal,
			out handIndex);

	public static bool HasPlayableAiTurnAction(int handCardCount, int playableItemCount) =>
		global::NetworkAiFallbackRules.HasPlayableAiTurnAction(handCardCount, playableItemCount);

	public static bool ShouldResumeAiProxyTurnAfterAuthorityAction(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool isGameOver,
		bool isProcessingCardPlay) =>
		global::NetworkAiFallbackRules.ShouldResumeAiProxyTurnAfterAuthorityAction(
			isNetworkMultiplayer,
			isNetworkHost,
			isGameOver,
			isProcessingCardPlay);

	public static bool ShouldStartAiProxyForPendingSelection(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool hasAiOwnedPendingSelection) =>
		global::NetworkAiFallbackRules.ShouldStartAiProxyForPendingSelection(
			isNetworkMultiplayer,
			isNetworkHost,
			hasAiOwnedPendingSelection);

	public static bool ShouldRecoverFromUnhandledAiTurnFailure(
		bool isNetworkMultiplayer,
		bool isNetworkHost,
		bool isGameOver) =>
		global::NetworkAiFallbackRules.ShouldRecoverFromUnhandledAiTurnFailure(
			isNetworkMultiplayer,
			isNetworkHost,
			isGameOver);

	public static bool ShouldSkipBlockedItem(
		global::NetworkAiBlockedItemState blockedItem,
		int ownerSeatIndex,
		string itemCardId) =>
		global::NetworkAiFallbackRules.ShouldSkipBlockedItem(blockedItem, ownerSeatIndex, itemCardId);

	public static bool ShouldClearBlockedItemForTurnChange(
		int previousTurnPlayerIndex,
		int nextTurnPlayerIndex) =>
		global::NetworkAiFallbackRules.ShouldClearBlockedItemForTurnChange(
			previousTurnPlayerIndex,
			nextTurnPlayerIndex);

	public static int PickRandomLegalCandidate(
		int totalCandidateCount,
		Func<int, bool> isLegalCandidate,
		int randomOrdinal) =>
		global::NetworkAiFallbackRules.PickRandomLegalCandidate(
			totalCandidateCount,
			isLegalCandidate,
			randomOrdinal);

	public static bool TryPickBestCaptureCardIndex(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		IReadOnlyList<global::PotentialCaptureCard> handCards,
		global::CaptureScoringContext context,
		int royalCaptureThreshold,
		out int handIndex) =>
		global::PotentialCaptureRules.TryPickBestCaptureCardIndex(
			tableCards,
			handCards,
			context,
			royalCaptureThreshold,
			out handIndex);

	public static int GetPotentialNormalCaptureCount(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		global::PotentialCaptureCard card) =>
		global::PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);

	public static int GetPotentialNormalCapturePoints(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		global::PotentialCaptureCard card,
		global::CaptureScoringContext context) =>
		global::PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, context);

	public static bool HasPotentialCapturedMiddleTableCards(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		int captureStartIndex) =>
		global::PotentialCaptureRules.HasPotentialCapturedMiddleTableCards(tableCards, captureStartIndex);

	public static int GetBestPotentialNormalCaptureCount(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		IReadOnlyList<global::PotentialCaptureCard> handCards) =>
		global::PotentialCaptureRules.GetBestPotentialNormalCaptureCount(tableCards, handCards);

	public static global::SoloAiDifficulty NormalizeSoloAiDifficulty(int difficultyValue) =>
		global::SoloAiRules.NormalizeDifficulty(difficultyValue);

	public static bool TryPickSoloAiCaptureCardIndex(
		IReadOnlyList<global::CaptureScoringTableCard> tableCards,
		IReadOnlyList<global::PotentialCaptureCard> handCards,
		global::CaptureScoringContext context,
		global::SoloAiDifficulty difficulty,
		int royalCaptureThreshold,
		int decisionRollPercent,
		int randomCaptureOrdinal,
		out int handIndex) =>
		global::SoloAiRules.TryPickCaptureCardIndex(
			tableCards,
			handCards,
			context,
			difficulty,
			royalCaptureThreshold,
			decisionRollPercent,
			randomCaptureOrdinal,
			out handIndex);

	public static int PickSoloAiDiscardDisplayIndex(
		global::SoloAiDifficulty difficulty,
		IReadOnlyList<global::SoloAiHandCardDecisionCandidate> normalHandCards,
		int totalDisplayCount,
		int tieBreakOrdinal) =>
		global::SoloAiRules.PickDiscardDisplayIndex(
			difficulty,
			normalHandCards,
			totalDisplayCount,
			tieBreakOrdinal);

	public static int PickSoloAiMagnetHandIndex(
		global::SoloAiDifficulty difficulty,
		IReadOnlyList<global::SoloAiHandCardDecisionCandidate> legalHandCards,
		int tieBreakOrdinal) =>
		global::SoloAiRules.PickMagnetHandIndex(difficulty, legalHandCards, tieBreakOrdinal);

	public static bool TryGetNextTurnGenerationAfterRestore(
		int localGeneration,
		int checkpointGeneration,
		out int restoredGeneration) =>
		global::TurnGenerationRules.TryGetNextAfterRestore(
			localGeneration,
			checkpointGeneration,
			out restoredGeneration);
}
