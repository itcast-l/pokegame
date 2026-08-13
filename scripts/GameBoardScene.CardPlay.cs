using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
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

		IReadOnlyList<PlayingCard> hand = _playerHands[playerIndex];
		if (handIndex < 0 || handIndex >= hand.Count)
		{
			return new PlayCardResult(false, false);
		}

		PlayingCard playedCard = hand[handIndex];
		MatchStateSnapshot? adventureSaveRollbackSnapshot = null;
		if (ShouldUseAdventureUpgradeSystem())
		{
			if (!TryCaptureAuthorityRuleSnapshot(
				out MatchStateSnapshot rollbackSnapshot,
				out string rollbackSnapshotError))
			{
				GD.PushError(
					"Failed to capture the pre-play adventure save rollback snapshot: "
					+ rollbackSnapshotError);
				return new PlayCardResult(false, false);
			}

			adventureSaveRollbackSnapshot = rollbackSnapshot;
		}
		ExtraActionGrantState? pendingExtraActionGrant =
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
		CardPlayRuleOptions ruleOptions = BuildCardPlayRuleOptions(playedCard);
		CardPlayPlan cardPlayPlan = ResolveCardPlayPlan(
			ruleOptions,
			adventureEnemyNextBaseCardModifiers.TableInsertIndex,
			adventureEnemyNextBaseCardModifiers.FacesUp);
		bool playedRankCollectsTable = cardPlayPlan.PlayedCardCollectsTable;
		Vector2 startGlobalPosition;
		if (_networkAuthorityRuleTransactionActive)
		{
			startGlobalPosition = GetNetworkAuthorityRuleVisualStartGlobalPosition(
				startGlobalPositionOverride ?? Vector2.Zero);
		}
		else
		{
			Control handArea = _playerHandAreas[playerIndex];
			Vector2 cardOffset = GetHandCardOffset(playerIndex, hand.Count, handArea.Size);
			Vector2 firstCardPosition = GetHandFirstCardPosition(playerIndex, hand.Count, handArea.Size, cardOffset);
			Control? sourceCardSlot = handArea.GetNodeOrNull<Control>($"HandCard_{handIndex}");
			TextureRect? sourceCardVisual = FindHandCardVisual(sourceCardSlot);
			startGlobalPosition = startGlobalPositionOverride
				?? sourceCardVisual?.GlobalPosition
				?? handArea.GlobalPosition + firstCardPosition + handIndex * cardOffset;
			if (sourceCardSlot != null)
			{
				sourceCardSlot.Visible = false;
			}
		}

		if (!TrySubmitPlayCardFromHandRuleCommand(
			playerIndex,
			handIndex,
			turnSequenceGeneration,
			ruleOptions,
			adventureEnemyNextBaseCardModifiers.TableInsertIndex,
			adventureEnemyNextBaseCardModifiers.FacesUp,
			out RuleCommandResult rulePlayResult))
		{
			return new PlayCardResult(false, false);
		}
		await WaitForAuthorityTransactionFaultAsync("after_move");
		await WaitForAuthorityTransactionFaultAsync("after_insert");
		int playedTableCardIndex = rulePlayResult.Mutation.TargetIndex;
		int captureStartIndex = rulePlayResult.Mutation.CaptureStartIndex;
		if (playedTableCardIndex < 0
			|| playedTableCardIndex >= _tableCards.Count
			|| playedTableCardIndex != cardPlayPlan.TableInsertIndex
			|| captureStartIndex != cardPlayPlan.CaptureStartIndex)
		{
			GD.PushError("The rule card-play result diverged from the scene presentation plan.");
			return new PlayCardResult(false, false);
		}

		// The live board is now transient Applying state. The last publishable
		// checkpoint remains Prepared until the complete result is finalized.
		MarkNetworkAuthorityPrimaryMutationApplied();
		ExtraActionGrantState? acceptedExtraActionGrant =
			AcceptAdventureExtraActionGrantForAction(
				playerIndex,
				pendingExtraActionGrant);
		BaseCardActionContext baseCardActionContext =
			CreateBaseCardActionContext(
				playedCard,
				acceptedExtraActionGrant);

		try
		{
				ApplyAdventureCanopyRevealToNewTableCard(playedTableCardIndex);

			ShiftHandDisplayStateAfterRemovedCard(playerIndex, handIndex);
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
				bool matchedTableCardFlipped = cardPlayPlan.MatchedTableCardFlipped;
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
				await WaitForAuthorityTransactionFaultAsync("before_capture");
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
					turnSequenceGeneration,
					adventureSaveRollbackSnapshot);
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
			HashSet<int> resolvedOvertimeTriggerIndexes = CaptureResolution.BeginOvertimeTriggerResolution();
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
				CaptureResolution.EndOvertimeTriggerResolution();
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
		AdventureMissSceneResolution missResolution =
			await ResolveAdventureMissActionAsync(
				playerIndex,
				playedCard.Rank,
				triggeredAttachedItemCardIds);
		if (!missResolution.Succeeded
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}
		await ApplyAdventurePostCaptureEffectsAsync(
			playerIndex,
			missResolution.RecoveryEffectResult with { HealAmount = 0 },
			turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new CardPlayBranchResult(true, new PlayCardResult(false, false), false);
		}
		AdventureMainPlayerMissEffectResult mainPlayerMissEffects = missResolution.EffectResult;
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
		foreach (int tableCardIndex in MatchReadModel.BuildBaseCaptureRevealIndexes(captureStartIndex, _tableCards.Count))
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
				revealTexture = GetPlayingCardTexture(tableCard.Card.Value);
			}
			else if (tableCard.ItemOverlay.HasValue)
			{
				revealTexture = GetItemCardTexture(tableCard.ItemOverlay.Value);
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
		return MatchReadModel.FindMatchingTableCardIndex(ToCardPlayTableCards(), playedCard.Rank);
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
		return ResolveCardPlayPlan(
			BuildCardPlayRuleOptions(playedCard),
			tableInsertIndexOverride,
			playedCardFacesUp);
	}

	private CardPlayPlan ResolveCardPlayPlan(
		ImmutableArray<TableCardState> tableCards,
		PlayingCard playedCard)
	{
		CardPlayRuleOptions options = BuildCardPlayRuleOptions(playedCard);
		return MatchReadModel.ResolveCardPlay(
			ToCardPlayTableCards(tableCards),
			new CardPlayCard(
				options.PrimaryMatchRank,
				options.CollectEntireTable,
				options.Suit,
				options.CollectBySuit,
				options.SuitMatchIsFallbackOnly),
			options.AlternateMatchRanks.IsDefaultOrEmpty
				? null
				: options.AlternateMatchRanks,
			insertAtTableStart: options.InsertAtTableStart);
	}

	private CardPlayPlan ResolveCardPlayPlan(
		CardPlayRuleOptions options,
		int tableInsertIndexOverride = -1,
		bool playedCardFacesUp = false)
	{
		return MatchReadModel.ResolveCardPlay(
			ToCardPlayTableCards(),
			new CardPlayCard(
				options.PrimaryMatchRank,
				options.CollectEntireTable,
				options.Suit,
				options.CollectBySuit,
				options.SuitMatchIsFallbackOnly),
			options.AlternateMatchRanks.IsDefaultOrEmpty
				? null
				: options.AlternateMatchRanks,
			insertAtTableStart: options.InsertAtTableStart,
			tableInsertIndexOverride: tableInsertIndexOverride,
			playedCardFacesUp: playedCardFacesUp);
	}

	private CardPlayRuleOptions BuildCardPlayRuleOptions(PlayingCard playedCard)
	{
		string primaryRank = ResolveAdventureMainPlayerPrimaryMatchRank(playedCard);
		var alternateRanks = ImmutableArray.CreateBuilder<string>();
		List<string>? clubAlternateRanks = BuildAdventureMainPlayerAlternateRanks(playedCard);
		if (clubAlternateRanks != null)
		{
			foreach (string alternateRank in clubAlternateRanks)
			{
				if (!alternateRanks.Contains(alternateRank, StringComparer.Ordinal))
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
				if (!alternateRanks.Contains(alternateRank, StringComparer.Ordinal))
				{
					alternateRanks.Add(alternateRank);
				}
			}
		}

		bool collectBySuit = ShouldCollectBySuit(playedCard);
		bool suitMatchIsFallbackOnly = collectBySuit
			&& !IsAttachedEffectType(playedCard, CardEffectTypes.SuitCollect)
			&& ShouldAdventureBorrowSuitSetAffectPlayedCard(playedCard);
		return new CardPlayRuleOptions(
			primaryRank,
			alternateRanks.ToImmutable(),
			playedCard.Suit,
			ShouldPlayedCardCollectTable(playedCard),
			collectBySuit,
			suitMatchIsFallbackOnly,
			ShouldInsertMissedCardAtTableStart(playedCard));
	}

	private readonly record struct CardPlayRuleOptions(
		string PrimaryMatchRank,
		ImmutableArray<string> AlternateMatchRanks,
		string Suit,
		bool CollectEntireTable,
		bool CollectBySuit,
		bool SuitMatchIsFallbackOnly,
		bool InsertAtTableStart);

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

	private static CardPlayTableCard[] ToCardPlayTableCards(
		ImmutableArray<TableCardState> sourceTableCards)
	{
		var tableCards = new CardPlayTableCard[sourceTableCards.Length];
		for (int tableCardIndex = 0; tableCardIndex < sourceTableCards.Length; tableCardIndex++)
		{
			TableCardState tableCard = sourceTableCards[tableCardIndex];
			tableCards[tableCardIndex] = new CardPlayTableCard(
				tableCard.Card?.Rank,
				tableCard.IsFaceUp,
				tableCard.IsBuried,
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
