using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private async Task<PlayCardResult> PlayRandomHandEntryForPlayerAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return new PlayCardResult(false, false);
		}

		if (TrySelectMainAdventureEnemyCard(playerIndex, out EnemyAiAdventureSelectionResult adventureSelection))
		{
			if (adventureSelection.Kind is EnemyAiAdventureSelectionKind.Capture
				or EnemyAiAdventureSelectionKind.Build)
			{
				PlayCardResult selectedResult = await PlayCardFromHandAsync(
					playerIndex,
					adventureSelection.HandIndex,
					turnSequenceGeneration);
				return selectedResult.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
					? selectedResult
					: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
			}

			return await PlayRandomPlayableHandEntryWithoutCapturePriorityAsync(playerIndex, turnSequenceGeneration);
		}

		int playableItemCount = CountPlayableItemIndexes(playerIndex, player);
		if (TryPickEarlyTableNewCardHandIndex(playerIndex, out int earlyTableHandIndex))
		{
			PlayCardResult earlyTableResult = await PlayCardFromHandAsync(playerIndex, earlyTableHandIndex, turnSequenceGeneration);
			return earlyTableResult.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
				? earlyTableResult
				: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}

		if (TryPickAiCaptureHandIndex(playerIndex, out int captureHandIndex))
		{
			return await PlayCardFromHandAsync(playerIndex, captureHandIndex, turnSequenceGeneration);
		}

		int playableCount = player.Hand.Length + playableItemCount;
		if (playableCount == 0)
		{
			return new PlayCardResult(false, false);
		}

		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				playableCount - 1,
				turnSequenceGeneration,
				out int selectedIndex))
		{
			return await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}
		PlayCardResult result;
		if (selectedIndex < player.Hand.Length)
		{
			result = await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
			return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
				? result
				: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}

		if (!TryGetPlayableItemIndexAt(
				playerIndex,
				player,
				selectedIndex - player.Hand.Length,
				out int itemHandIndex))
		{
			return new PlayCardResult(false, false);
		}

		result = await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
			? result
			: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> PlayRandomPlayableHandEntryWithoutCapturePriorityAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return new PlayCardResult(false, false);
		}

		int playableItemCount = CountPlayableItemIndexes(playerIndex, player);
		int playableCount = player.Hand.Length + playableItemCount;
		if (playableCount == 0)
		{
			return new PlayCardResult(false, false);
		}

		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				playableCount - 1,
				turnSequenceGeneration,
				out int selectedIndex))
		{
			return await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}
		PlayCardResult result;
		if (selectedIndex < player.Hand.Length)
		{
			result = await PlayCardFromHandAsync(playerIndex, selectedIndex, turnSequenceGeneration);
			return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
				? result
				: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
		}

		if (!TryGetPlayableItemIndexAt(
				playerIndex,
				player,
				selectedIndex - player.Hand.Length,
				out int itemHandIndex))
		{
			return new PlayCardResult(false, false);
		}

		result = await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
		return result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver()
			? result
			: await PlayFirstAvailableHandEntryForPlayerAsync(playerIndex, turnSequenceGeneration);
	}

	private async Task<PlayCardResult> PlayFirstAvailableHandEntryForPlayerAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return new PlayCardResult(false, false);
		}

		for (int handIndex = 0; handIndex < player.Hand.Length; handIndex++)
		{
			PlayCardResult result = await PlayCardFromHandAsync(playerIndex, handIndex, turnSequenceGeneration);
			if (result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return result;
			}
		}

		int playableItemCount = CountPlayableItemIndexes(playerIndex, player);
		for (int playableItemOrdinal = 0; playableItemOrdinal < playableItemCount; playableItemOrdinal++)
		{
			if (!TryGetPlayableItemIndexAt(
					playerIndex,
					player,
					playableItemOrdinal,
					out int itemHandIndex))
			{
				continue;
			}

			PlayCardResult result = await PlayItemCardFromHandAsync(playerIndex, itemHandIndex, turnSequenceGeneration);
			if (result.Played || turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return result;
			}
		}

		return new PlayCardResult(false, false);
	}

	private bool TryPickAiCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (IsOfflineSoloAiPlayer(playerIndex))
		{
			return TryPickSoloAiCaptureHandIndex(playerIndex, out handIndex);
		}

		if (!adventure_mode || !IsAdventureEnemyPlayer(playerIndex))
		{
			return TryPickBestCaptureHandIndex(playerIndex, out handIndex);
		}

		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out _))
		{
			return false;
		}

		return TryPickBestCaptureHandIndex(playerIndex, out handIndex);
	}

	private bool TrySelectMainAdventureEnemyCard(
		int playerIndex,
		out EnemyAiAdventureSelectionResult selection)
	{
		selection = new EnemyAiAdventureSelectionResult(
			EnemyAiAdventureSelectionKind.None,
			-1);
		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				99,
				_turnSequenceGeneration,
				out int decisionRollPercent)
			|| !TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				int.MaxValue,
				_turnSequenceGeneration,
				out int randomCandidateOrdinal))
		{
			return false;
		}

		return TrySelectMainAdventureEnemyCard(
			playerIndex,
			decisionRollPercent,
			randomCandidateOrdinal,
			out selection);
	}

	private bool TrySelectMainAdventureEnemyCard(
		int playerIndex,
		int decisionRollPercent,
		int randomCandidateOrdinal,
		out EnemyAiAdventureSelectionResult selection)
	{
		selection = new EnemyAiAdventureSelectionResult(
			EnemyAiAdventureSelectionKind.None,
			-1);
		if (!adventure_mode
			|| adventure_tutorial_mode
			|| !IsAdventureEnemyPlayer(playerIndex)
			|| !AdventureCampaignCatalog.TryGetMainStageIndex(adventure_enemy_avatar_id, out int mainStageIndex))
		{
			return false;
		}
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return false;
		}

		int royalCaptureThreshold = IsBossKEnemy(playerIndex)
			? GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			: int.MaxValue;
		selection = MatchReadModel.SelectAdventureCard(
			ToCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCards(playerIndex, player.Hand),
			BuildCaptureScoringContext(playerIndex),
			MatchReadModel.GetAdventureAiProfile(mainStageIndex),
			royalCaptureThreshold,
			decisionRollPercent,
			randomCandidateOrdinal);
		return true;
	}

	private bool TryPickEarlyTableNewCardHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return false;
		}
		if (!TrySubmitChooseRandomIndexRuleCommand(
				playerIndex,
				0,
				int.MaxValue,
				_turnSequenceGeneration,
				out int randomOrdinal))
		{
			return false;
		}

		return MatchReadModel.TryPickEarlyTableNewCardIndex(
			readModel.TableCards.Length,
			player.Hand.Length,
			handCardIndex =>
			{
				CardPlayPlan plan = ResolveCardPlayPlan(
					readModel.TableCards,
					FromRuleCardState(player.Hand[handCardIndex]));
				return plan.MatchingTableCardIndex < 0 && !plan.PlayedCardCollectsTable;
			},
			randomOrdinal,
			out handIndex);
	}

	private bool TryPickBestCaptureHandIndex(int playerIndex, out int handIndex)
	{
		handIndex = -1;
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player))
		{
			return false;
		}

		int royalCaptureThreshold = IsBossKEnemy(playerIndex)
			? GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			: int.MaxValue;
		return MatchReadModel.TryPickBestCaptureCardIndex(
			ToCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCards(playerIndex, player.Hand),
			BuildCaptureScoringContext(playerIndex),
			royalCaptureThreshold,
			out handIndex);
	}

	private int GetPotentialNormalCaptureCount(PlayingCard card)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel))
		{
			return 0;
		}

		return GetPotentialNormalCaptureCount(readModel, card);
	}

	private int GetPotentialNormalCaptureCount(MatchReadModel readModel, PlayingCard card)
	{
		return MatchReadModel.GetPotentialNormalCaptureCount(
			ToCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCard(MainPlayerIndex, card));
	}

	private int GetPotentialNormalCapturePoints(int playerIndex, PlayingCard card)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel))
		{
			return 0;
		}

		return MatchReadModel.GetPotentialNormalCapturePoints(
			ToCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCard(playerIndex, card, ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card)),
			BuildCaptureScoringContext(playerIndex));
	}

	private bool HasPotentialCapturedMiddleTableCards(int captureStartIndex)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel))
		{
			return false;
		}

		return MatchReadModel.HasPotentialCapturedMiddleTableCards(
			ToCaptureScoringTableCards(readModel.TableCards),
			captureStartIndex);
	}

	private async Task ResolveAdventureEnemyTurnStartAbilityAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (IsAdventureEnemyWithMantaRayTideShift(playerIndex))
		{
			if (CurrentMatchFlowState.EnemyTurnStartControlChecked)
			{
				return;
			}

			TryUpdateMatchFlowState(state => state with
			{
				EnemyTurnStartControlChecked = true,
			});
		}

		if (!ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.TideShift);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		TableCard tideShiftedCard = _tableCards[0];
		Texture2D? triggerTexture = GetVisibleTableCardTexture(tideShiftedCard);
		if (triggerTexture != null)
		{
			if (!_networkAuthorityRuleTransactionActive
				&& TryGetTableCardPosition(0, out Vector2 cardPosition))
			{
				await PlaySpecialCardTriggerEffectAsync(triggerTexture, _centerArena.GlobalPosition + cardPosition, 0);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
				{
					return;
				}
			}
		}

		await PlayMantaRayTideShiftTableMoveAnimationAsync(turnSequenceGeneration);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver() || !ShouldResolveMantaRayTideShift(playerIndex))
		{
			return;
		}

		if (!TrySubmitMoveTableCardRuleCommand(
			playerIndex,
			0,
			_tableCards.Count - 1,
			tideShiftedCard.InstanceId,
			turnSequenceGeneration,
			out _))
		{
			return;
		}
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderTableCards();
	}

	private bool ShouldResolveMantaRayTideShift(int playerIndex)
	{
		return TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			&& adventure_mode
			&& playerIndex == 1
			&& CurrentMatchFlowState.EnemyTurnStartControlChecked
			&& IsMantaRayTideShiftEnemyTurn(playerIndex)
			&& readModel.TableCards.Length >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.TideShiftMinTableCards, 4)
			&& (!IsBossKEnemy(playerIndex) || ShouldResolveBossKTideShift(playerIndex));
	}

	private bool ShouldResolveBossKTideShift(int playerIndex)
	{
		if (!BossKCanUseActiveControl(playerIndex) || ShouldResolveDragonPrinceJack(playerIndex))
		{
			return false;
		}

		int bossCaptureBefore = GetBestPotentialNormalCaptureCount(playerIndex);
		int bossCaptureAfter = GetBestPotentialNormalCaptureCountAfterTideShift(playerIndex);
		if (bossCaptureAfter > bossCaptureBefore
			&& bossCaptureAfter >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5))
		{
			return true;
		}

		int playerCaptureBefore = GetBestPotentialNormalCaptureCount(MainPlayerIndex);
		int playerCaptureAfter = GetBestPotentialNormalCaptureCountAfterTideShift(MainPlayerIndex);
		return playerCaptureBefore >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			&& playerCaptureAfter < playerCaptureBefore;
	}

	private int GetBestPotentialNormalCaptureCountAfterTideShift(int playerIndex)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player)
			|| readModel.TableCards.IsEmpty)
		{
			return 0;
		}

		return MatchReadModel.GetBestPotentialNormalCaptureCount(
			ToTideShiftedCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCards(player.Hand));
	}

	private int GetBestPotentialNormalCaptureCount(int playerIndex)
	{
		if (!TryGetAuthorityRuleReadModel(out MatchReadModel readModel)
			|| !TryGetRulePlayer(readModel, playerIndex, out PlayerStateSnapshot player)
			|| readModel.TableCards.IsEmpty)
		{
			return 0;
		}

		return MatchReadModel.GetBestPotentialNormalCaptureCount(
			ToCaptureScoringTableCards(readModel.TableCards),
			ToPotentialCaptureCards(player.Hand));
	}

	private PotentialCaptureCard[] ToPotentialCaptureCards(
		int playerIndex,
		ImmutableArray<LogicalCardState> cards)
	{
		var potentialCards = new PotentialCaptureCard[cards.Length];
		for (int index = 0; index < cards.Length; index++)
		{
			PlayingCard card = FromRuleCardState(cards[index]);
			potentialCards[index] = ToPotentialCaptureCard(
				playerIndex,
				card,
				ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card));
		}

		return potentialCards;
	}

	private PotentialCaptureCard[] ToPotentialCaptureCards(
		ImmutableArray<LogicalCardState> cards)
	{
		var potentialCards = new PotentialCaptureCard[cards.Length];
		for (int index = 0; index < cards.Length; index++)
		{
			potentialCards[index] = ToPotentialCaptureCard(
				MainPlayerIndex,
				FromRuleCardState(cards[index]));
		}

		return potentialCards;
	}

	private PotentialCaptureCard[] ToPotentialCaptureCards(int playerIndex, IReadOnlyList<PlayingCard> cards)
	{
		var potentialCards = new PotentialCaptureCard[cards.Count];
		for (int index = 0; index < cards.Count; index++)
		{
			PlayingCard card = cards[index];
			potentialCards[index] = ToPotentialCaptureCard(
				playerIndex,
				card,
				ShouldApplyNarwhalDoubleToPlayedCard(playerIndex, card));
		}

		return potentialCards;
	}

	private PotentialCaptureCard[] ToPotentialCaptureCards(IReadOnlyList<PlayingCard> cards)
	{
		var potentialCards = new PotentialCaptureCard[cards.Count];
		for (int index = 0; index < cards.Count; index++)
		{
			potentialCards[index] = ToPotentialCaptureCard(MainPlayerIndex, cards[index]);
		}

		return potentialCards;
	}

	private PotentialCaptureCard ToPotentialCaptureCard(int playerIndex, PlayingCard card, bool hasDoubleEffect = false)
	{
		string primaryRank = playerIndex == MainPlayerIndex
			? ResolveAdventureMainPlayerPrimaryMatchRank(card)
			: card.Rank;
		List<string>? alternateRanks = playerIndex == MainPlayerIndex
			? BuildAdventureMainPlayerAlternateRanks(card)
			: null;
		return new PotentialCaptureCard(
			primaryRank,
			ShouldPlayedCardCollectTable(card),
			hasDoubleEffect,
			card.Suit,
			ShouldCollectBySuit(card),
			alternateRanks);
	}

	private CaptureScoringTableCard[] ToTideShiftedCaptureScoringTableCards(
		ImmutableArray<TableCardState> sourceTableCards)
	{
		var tableCards = new CaptureScoringTableCard[sourceTableCards.Length];
		for (int shiftedIndex = 0; shiftedIndex < sourceTableCards.Length; shiftedIndex++)
		{
			int tableCardIndex = shiftedIndex == sourceTableCards.Length - 1 ? 0 : shiftedIndex + 1;
			tableCards[shiftedIndex] = ToCaptureScoringTableCard(sourceTableCards[tableCardIndex]);
		}

		return tableCards;
	}

	private bool IsMantaRayTideShiftEnemyTurn(int playerIndex)
	{
		return playerIndex == 1
			&& IsAdventureEnemyWithMantaRayTideShift(playerIndex)
			&& _currentTurnPlayerIndex == playerIndex;
	}

	private bool IsAdventureEnemyWithMantaRayTideShift(int playerIndex)
	{
		return playerIndex == 1
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TideShift);
	}

	private void ResetMantaRayTideShiftTurnStateIfNeeded(int previousPlayerIndex)
	{
		if (!adventure_mode
			|| !AdventureEnemyHasAbility(AdventureEnemyAbilities.TideShift)
			|| previousPlayerIndex == 1
			|| _currentTurnPlayerIndex != 1)
		{
			return;
		}

		TryUpdateMatchFlowState(state => state with
		{
			EnemyTurnStartControlChecked = false,
			ActiveControlUsedThisTurn = false,
		});
	}

	private int AddBombCardToTable(ItemCard bombCard, int ownerPlayerIndex)
	{
		return TrySubmitPlaceTableItemRuleCommand(
			ownerPlayerIndex,
			bombCard.Id,
			_tableCards.Count,
			isFaceUp: false,
			ownerPlayerIndex,
			_turnSequenceGeneration,
			out RuleCommandResult result)
			? result.Mutation.TargetIndex
			: -1;
	}

	private int InsertBombCardIntoTable(ItemCard bombCard, int ownerPlayerIndex, int tableCardIndex)
	{
		int clampedIndex = Mathf.Clamp(tableCardIndex, 0, _tableCards.Count);
		return TrySubmitPlaceTableItemRuleCommand(
			ownerPlayerIndex,
			bombCard.Id,
			clampedIndex,
			isFaceUp: false,
			ownerPlayerIndex,
			_turnSequenceGeneration,
			out RuleCommandResult result)
			? result.Mutation.TargetIndex
			: -1;
	}

}
