using System;
using System.Collections.Generic;
using Deckland.Rules;
using Godot;

internal readonly record struct BaseCardActionContext(
	long ActionId,
	long RootChainId,
	string ActionSource,
	bool IsExtraAction,
	long PlayedCardInstanceId,
	PlayingCard PlayedCard,
	IReadOnlyList<string> TriggeredAttachedItemCardIds);

internal readonly record struct PlayCardResult(
	bool Played,
	bool Captured,
	bool DeckCollected = false,
	QuestionCardRewardAnimation? QuestionRewardAnimation = null,
	bool CompletesTurn = true,
	NetworkItemPlayVisualSync? ItemPlayVisualSync = null,
	NetworkCaptureVisualSync? CaptureVisualSync = null,
	NetworkHandPlayVisualSync? HandPlayVisualSync = null,
	AdventureQuestionRewardEffectResult? AdventureQuestionRewardEffects = null,
	AdventurePostCaptureEffectResult? AdventurePostCaptureEffects = null,
	ExtraActionGrantState? ConsumedAdventureExtraActionGrant = null,
	BaseCardActionContext? BaseCardActionContext = null)
{
	public bool AdventureConsumedExtraBaseCardAction => ConsumedAdventureExtraActionGrant.HasValue;
}

internal readonly record struct CapturedTableCardVisual(
	int TableCardIndex,
	Texture2D StartTexture,
	Texture2D? ItemOverlayTexture,
	Vector2 GlobalPosition,
	int ZIndex);

internal readonly record struct ItemAttachmentOverlay(Texture2D Texture, int Priority);

internal readonly record struct ShuffledTableCardVisual(
	Texture2D MainTexture,
	Texture2D? ItemOverlayTexture,
	Vector2 ItemOverlayPosition,
	Vector2 ItemOverlaySize,
	bool AnimateCardBack,
	float Alpha,
	Vector2 StartGlobalPosition,
	Vector2 TargetGlobalPosition,
	Vector2 ShuffleCenterGlobalPosition,
	int ZIndex);

internal readonly record struct TableSpecialCardTrigger(int TableCardIndex, Texture2D Texture, Vector2 GlobalPosition, int ZIndex, int Priority);

internal readonly record struct QuestionCardRewardGrant(
	ItemCard ItemCard,
	int ItemHandIndex,
	bool RequiresMagnetAttachment = false,
	bool RejudgeResolvedForReward = false);

internal readonly record struct QuestionCardRewardAnimation(
	ItemCard ItemCard,
	int ItemHandIndex,
	Vector2 StartGlobalPosition,
	bool RequiresMagnetAttachment = false,
	bool RejudgeResolvedForReward = false);

internal readonly record struct DrawnHandCard(PlayingCard Card, int HandIndex);

internal readonly record struct NetworkItemPlayVisualSync(
	string ItemCardId,
	string PlayPresentation,
	int ItemHandIndex,
	int TargetTableCardIndex = -1);

internal readonly record struct NetworkHandPlayVisualSync(
	int TableCardIndex,
	int MatchingTableCardIndex = -1,
	bool MatchedTableCardFlipped = false,
	string BorrowTideAdjustedRank = "");

internal readonly record struct NetworkCaptureVisualSync(
	int PlayerIndex,
	int CaptureStartIndex,
	int[] ExtraTableCardIndexes,
	float FlightStaggerSeconds,
	int RedirectedTableCardIndex = -1);

internal readonly record struct NetworkCaptureRevealVisualSync(
	int PlayerIndex,
	int[] RevealTableCardIndexes,
	string[] RevealCardCodes,
	string[] RevealAttachedItemCardIdsCsv,
	int[] RevealPointValues,
	bool[] RevealPointDoubleFlags);

internal readonly record struct CaptureTableCardsResult(
	bool Applied,
	bool Aborted,
	int CollectedCardCount,
	int RemovedTableCardCount,
	QuestionCardRewardAnimation? QuestionRewardAnimation,
	AdventureQuestionRewardEffectResult? AdventureQuestionRewardEffects = null,
	AdventurePostCaptureEffectResult? AdventurePostCaptureEffects = null,
	CaptureResolutionContext? AdventureCaptureContext = null)
{
	public bool Completed => Applied && !Aborted;

	public static CaptureTableCardsResult NotApplied => new(
		Applied: false,
		Aborted: false,
		CollectedCardCount: 0,
		RemovedTableCardCount: 0,
		QuestionRewardAnimation: null);

	public static CaptureTableCardsResult AbortedBeforeApply => new(
		Applied: false,
		Aborted: true,
		CollectedCardCount: 0,
		RemovedTableCardCount: 0,
		QuestionRewardAnimation: null);
}
