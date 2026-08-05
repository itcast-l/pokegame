using System.Collections.Generic;

public readonly record struct CaptureScoringCard(
	string Rank,
	int ScoreValue,
	bool HasDoubleCollectEffect,
	string Suit = "");

public readonly record struct CaptureScoringTableCard(
	CaptureScoringCard? Card,
	bool IsBuriedInSand,
	bool IsSlimeCoated,
	bool HasDoubleCollectItemEffect,
	bool HasPairDestroyItemEffect,
	int ItemOwnerPlayerIndex);

public readonly record struct CaptureScoringContext(
	bool SuppressSlimeCoatedScore,
	bool ApplyScoreReductionPerCard,
	int ScoreReductionPerCard);

public readonly record struct EnemyAiAdventureProfile(
	int BestCaptureChancePercent,
	int AnyCaptureChancePercent,
	int PairOnlyCaptureChancePercent,
	int EarlyBuildChancePercent,
	bool UsePointScoring);

public enum EnemyAiAdventureSelectionKind
{
	None,
	Capture,
	Build,
}

public readonly record struct EnemyAiAdventureSelectionResult(
	EnemyAiAdventureSelectionKind Kind,
	int HandIndex);

public readonly record struct PotentialCaptureCard(
	string Rank,
	bool CollectsTable,
	bool HasDoubleEffect,
	string Suit = "",
	bool CollectBySuit = false,
	IReadOnlyList<string>? AlternateRanks = null);

public enum SoloAiDifficulty
{
	Low = 0,
	Medium = 1,
	High = 2,
}

public readonly record struct SoloAiProfile(
	int BestCaptureChancePercent,
	int AnyCaptureChancePercent,
	bool UsePointScoring,
	bool AvoidPairOnlyCaptures);

public readonly record struct SoloAiHandCardDecisionCandidate(
	int Index,
	int ScoreValue,
	bool CanCaptureNow);

public readonly record struct GameTurnPlayerCardState(
	int HandCardCount,
	int DeckCardCount,
	int ItemCardCount,
	bool HasPlayableItemCard)
{
	public bool HasResources => HandCardCount > 0 || DeckCardCount > 0 || ItemCardCount > 0;
	public bool CanActNow => HandCardCount > 0 || HasPlayableItemCard;
}
