using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private bool DebugPrepareAdventureBattleForDeal()
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return true;
		}
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		return store?.HasActiveRun == true
			&& store.ResetBattleScopedEffects() == AdventureRunWriteResult.Succeeded;
	}

	public bool DebugAcquireAdventureUpgrade(string upgradeId)
	{
		if (!ShouldUseAdventureUpgradeSystem() || string.IsNullOrWhiteSpace(upgradeId))
		{
			return false;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true || runState.HasAcquiredUpgrade(upgradeId))
		{
			return false;
		}

		AdventureUpgradeGrantCommitResult grantResult = GrantAdventureUpgrade(
			upgradeId,
			AdventureUpgradeTriggerSource.None);
		if (grantResult.WriteResult != AdventureRunWriteResult.Succeeded
			|| grantResult.AcquireResult != AdventureUpgradeAcquireResult.Acquired)
		{
			return false;
		}

		_ = ApplyAdventureUpgradeAcquisitionEffectsAsync(
			grantResult,
			_turnSequenceGeneration);
		RenderPlayerHands();
		return true;
	}

	public bool DebugConfigureAdventureMissClimbBadgeScenario(int growthStacks)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (!ShouldUseAdventureUpgradeSystem()
			|| player_count != 2
			|| store == null
			|| runState?.IsActive != true
			|| growthStacks < 0
			|| growthStacks > AdventureUpgradeEffectState.MaxClubMissClimbGrowthStacks)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "4"),
			("Spades", "6"),
			("Diamonds", "10"),
			("Clubs", "J"),
			("Clubs", "4"),
			("Spades", "A"),
			("Hearts", "5"),
			("Diamonds", "9"),
			("Clubs", "8"),
		};
		ItemCard? itemCard = CreateItemCard(BombItemCardId);
		if (!itemCard.HasValue
			|| !TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:4"]);
		_playerHands[MainPlayerIndex].Add(cards["Spades:6"]);
		_playerHands[MainPlayerIndex].Add(cards["Diamonds:10"]);
		_playerHands[MainPlayerIndex].Add(cards["Clubs:J"]);
		_playerItemHands[MainPlayerIndex].Add(itemCard.Value);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerDecks[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerHands[1].Add(cards["Diamonds:9"]);
		_playerDecks[1].Add(cards["Clubs:8"]);
		_tableCards.Add(new TableCard(cards["Clubs:4"], true));

		runState.EffectState.ClubMissClimbGrowthStacks = growthStacks;
		if (store.ConfigureEffectStateForDebug(runState.EffectState.Clone())
			!= AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		_mainPlayerHandSelectionInputEnabled = false;
		_selectedMainPlayerHandDisplayIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public int DebugGetAdventureMissClimbGrowthStacks()
	{
		return GetAdventureUpgradeRunState()?.EffectState.ClubMissClimbGrowthStacks ?? 0;
	}

	public int DebugGetAdventureMissClimbBadgeCount()
	{
		if (MainPlayerIndex >= _playerHandAreas.Count)
		{
			return 0;
		}

		int badgeCount = 0;
		Control handArea = _playerHandAreas[MainPlayerIndex];
		for (int childIndex = 0; childIndex < handArea.GetChildCount(); childIndex++)
		{
			if (handArea.GetChild(childIndex) is not Control cardSlot)
			{
				continue;
			}

			TextureRect? cardVisual = FindHandCardVisual(cardSlot);
			if (cardVisual?.GetNodeOrNull<Control>(AdventureMissClimbBadgeNodeName) != null)
			{
				badgeCount++;
			}
		}

		return badgeCount;
	}

	public bool DebugSetAdventureMissClimbBadgeHoverState(int handIndex, bool hovered)
	{
		if (MainPlayerIndex >= _playerHandAreas.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[MainPlayerIndex].Count)
		{
			return false;
		}

		Control? cardSlot = _playerHandAreas[MainPlayerIndex]
			.GetNodeOrNull<Control>($"HandCard_{handIndex}");
		TextureRect? cardVisual = FindHandCardVisual(cardSlot);
		if (cardSlot == null
			|| cardVisual?.GetNodeOrNull<Control>(AdventureMissClimbBadgeNodeName) == null)
		{
			return false;
		}

		cardSlot.EmitSignal(hovered
			? Control.SignalName.MouseEntered
			: Control.SignalName.MouseExited);
		return true;
	}

	public bool DebugSetAdventureMissClimbBadgeKeyboardSelection(int handIndex, bool selected)
	{
		if (!selected)
		{
			_mainPlayerHandSelectionInputEnabled = false;
			_selectedMainPlayerHandDisplayIndex = -1;
			RenderPlayerHands();
			return true;
		}

		if (handIndex < 0
			|| handIndex >= _playerHands[MainPlayerIndex].Count
			|| !IsMainPlayerHandDisplayIndexSelectable(handIndex))
		{
			return false;
		}

		_mainPlayerHandSelectionInputEnabled = true;
		_selectedMainPlayerHandDisplayIndex = handIndex;
		RenderPlayerHands();
		return IsMainPlayerHandDisplayIndexSelected(handIndex);
	}

	public bool DebugConfigureBlackLedgerCaptureProbe()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? initialRunState = store?.GetRunState();
		if (_debugBlackLedgerProbeRunning
			|| !adventure_mode
			|| player_count != 2
			|| store == null
			|| initialRunState?.IsActive != true)
		{
			return false;
		}
		int avatarStageNumber = initialRunState.CurrentAvatarStageNumber;
		if (store.BeginNewRun(avatarStageNumber) != AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		AdventureUpgradeGrantCommitResult blackLedgerGrant = store.GrantUpgrade(
			"spades_black_ledger",
			AdventureUpgradeTriggerSource.RunStart);
		if (blackLedgerGrant.WriteResult != AdventureRunWriteResult.Succeeded
			|| blackLedgerGrant.AcquireResult != AdventureUpgradeAcquireResult.Acquired)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Spades", "10"),
			("Clubs", "4"),
			("Diamonds", "5"),
		};
		if (!TryLoadAdventureTutorialCards(
			requiredCards,
			out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_tableCards.Clear();
		_tableCards.Add(new TableCard(cards["Hearts:5"], true));
		_tableCards.Add(new TableCard(cards["Spades:10"], true));
		_tableCards.Add(new TableCard(cards["Clubs:4"], true));
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			_playerDecks[playerIndex].Clear();
			_playerHands[playerIndex].Clear();
			_playerItemHands[playerIndex].Clear();
		}
		_playerHands[1].Add(cards["Diamonds:5"]);
		// Adventure capture damage for this fixed 10-and-4 middle-card setup.
		_debugBlackLedgerExpectedDamage = 14;
		_playerScores[MainPlayerIndex] = 100;
		_playerScores[1] = 100;
		_winnerPlayerIndex = -1;
		_currentTurnPlayerIndex = 1;
		_isOpeningDealAnimationPlaying = false;
		RenderPlayerHands();
		RenderPlayerCardPiles();
		RenderTableCards();
		UpdatePlayerScoreMeter(MainPlayerIndex, false);
		UpdatePlayerScoreMeter(1, false);
		return _debugBlackLedgerExpectedDamage > 0;
	}

	public bool DebugStartBlackLedgerCaptureProbe(
		Godot.Collections.Dictionary result)
	{
		if (result == null
			|| _debugBlackLedgerProbeRunning
			|| _playerHands.Count <= 1
			|| _playerHands[1].Count != 1)
		{
			return false;
		}

		result.Clear();
		result["completed"] = false;
		result["error"] = string.Empty;
		_debugBlackLedgerProbeRunning = true;
		_ = RunDebugBlackLedgerCaptureProbeAsync(result);
		return true;
	}

	private async Task RunDebugBlackLedgerCaptureProbeAsync(
		Godot.Collections.Dictionary result)
	{
		try
		{
			int expectedDamage = _debugBlackLedgerExpectedDamage;
			int mainHealthBefore = _playerScores[MainPlayerIndex];
			PlayCardResult playResult = await PlayCardFromHandAsync(
				1,
				0,
				_turnSequenceGeneration);
			result["played"] = playResult.Played;
			result["captured"] = playResult.Captured;
			result["expected_damage"] = expectedDamage;
			result["actual_damage"] = mainHealthBefore
				- _playerScores[MainPlayerIndex];
			result["main_deck_count"] = _playerDecks[MainPlayerIndex].Count;
			result["enemy_deck_count"] = _playerDecks[1].Count;
			result["main_has_ten"] = _playerDecks[MainPlayerIndex]
				.Exists(card => string.Equals(card.Rank, "10", StringComparison.Ordinal));
			result["enemy_has_ten"] = _playerDecks[1]
				.Exists(card => string.Equals(card.Rank, "10", StringComparison.Ordinal));
		}
		catch (Exception exception)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completed"] = true;
			_debugBlackLedgerProbeRunning = false;
		}
	}

	public bool DebugConfigureAdventurePairCaptureReturnScenario()
	{
		if (!adventure_mode || player_count != 2)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Clubs", "5"),
			("Spades", "A"),
			("Diamonds", "9"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Diamonds:9"]);
		_tableCards.Add(new TableCard(cards["Clubs:5"], true));
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public bool DebugConfigureAdventureCrownCanopyScenario()
	{
		if (!adventure_mode || player_count != 2)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Clubs", "5"),
			("Clubs", "2"),
			("Spades", "A"),
			("Diamonds", "9"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Diamonds:9"]);
		_tableCards.Add(new TableCard(cards["Clubs:5"], true));
		_tableCards.Add(new TableCard(cards["Clubs:2"], true));
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public bool DebugConfigureAdventureQuestionRewardTimingScenario()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? initialRunState = store?.GetRunState();
		if (!adventure_mode
			|| player_count != 2
			|| store == null
			|| initialRunState?.IsActive != true)
		{
			return false;
		}

		int avatarStageNumber = initialRunState.CurrentAvatarStageNumber;
		if (store.BeginNewRun(avatarStageNumber) != AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		AdventureUpgradeGrantCommitResult interestGrant = store.GrantUpgrade(
			"diamonds_question_mark_interest",
			AdventureUpgradeTriggerSource.RunStart);
		AdventureUpgradeGrantCommitResult finalDoubleGrant = store.GrantUpgrade(
			"diamonds_final_double",
			AdventureUpgradeTriggerSource.None);
		if (interestGrant.WriteResult != AdventureRunWriteResult.Succeeded
			|| interestGrant.AcquireResult != AdventureUpgradeAcquireResult.Acquired
			|| finalDoubleGrant.WriteResult != AdventureRunWriteResult.Succeeded
			|| finalDoubleGrant.AcquireResult != AdventureUpgradeAcquireResult.Acquired)
		{
			return false;
		}

		AdventureUpgradeRunState runState = store.GetRunState();
		runState.EffectState.QuestionRewardCount = 9;
		runState.EffectState.PermanentCaptureDamageBonus = 4;
		runState.EffectState.FinalDoubleCaptureDamageActive = false;
		if (store.ConfigureEffectStateForDebug(runState.EffectState.Clone())
			!= AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		return ConfigureAdventureQuestionRewardTimingCapture(includeQuestionReward: true);
	}

	public bool DebugConfigureAdventureQuestionRewardTimingFollowUpCapture()
	{
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !runState.HasAcquiredUpgrade("diamonds_question_mark_interest")
			|| !runState.HasAcquiredUpgrade("diamonds_final_double")
			|| runState.EffectState.QuestionRewardCount != 10
			|| runState.EffectState.PermanentCaptureDamageBonus != 5
			|| !runState.EffectState.FinalDoubleCaptureDamageActive)
		{
			return false;
		}

		return ConfigureAdventureQuestionRewardTimingCapture(includeQuestionReward: false);
	}

	public bool DebugConfigureAdventureDoubleQuestionRewardCapacityScenario()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? initialRunState = store?.GetRunState();
		if (!adventure_mode
			|| player_count != 2
			|| store == null
			|| initialRunState?.IsActive != true
			|| store.BeginNewRun(initialRunState.CurrentAvatarStageNumber)
				!= AdventureRunWriteResult.Succeeded)
		{
			return false;
		}

		AdventureUpgradeGrantCommitResult linkedStemsGrant = store.GrantUpgrade(
			"clubs_linked_stems",
			AdventureUpgradeTriggerSource.RunStart);
		if (linkedStemsGrant.WriteResult != AdventureRunWriteResult.Succeeded
			|| linkedStemsGrant.AcquireResult != AdventureUpgradeAcquireResult.Acquired)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Clubs", "5"),
			("Diamonds", "3"),
			("Spades", "A"),
			("Diamonds", "9"),
		};
		if (!TryLoadAdventureTutorialCards(
			requiredCards,
			out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		ItemCard? questionMark = CreateItemCard(QuestionMarkItemCardId);
		if (!questionMark.HasValue
			|| !DebugForceNextQuestionRewardItemCardId("bomb")
			|| !DebugForceNextQuestionRewardItemCardId("lens"))
		{
			return false;
		}

		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			_playerDecks[playerIndex].Clear();
			_playerHands[playerIndex].Clear();
			_playerItemHands[playerIndex].Clear();
		}
		_tableCards.Clear();
		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		for (int handIndex = 1; handIndex < MaxTotalHandCards; handIndex++)
		{
			_playerHands[MainPlayerIndex].Add(cards["Spades:A"]);
		}
		_playerHands[1].Add(cards["Diamonds:9"]);
		_tableCards.Add(new TableCard(cards["Clubs:5"], true));
		_tableCards.Add(new TableCard(null, true, questionMark));
		_tableCards.Add(new TableCard(cards["Diamonds:3"], true));
		_adventureMainPlayerSuccessfulCapturesThisTurn = 1;
		_pendingItemAttachmentRewards.Clear();
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public Godot.Collections.Dictionary DebugGetAdventureQuestionRewardTimingState()
	{
		var result = new Godot.Collections.Dictionary();
		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return result;
		}

		result["question_reward_count"] = runState.EffectState.QuestionRewardCount;
		result["permanent_capture_damage_bonus"] = runState.EffectState.PermanentCaptureDamageBonus;
		result["final_double_capture_damage_active"] = runState.EffectState.FinalDoubleCaptureDamageActive;
		return result;
	}

	private bool ConfigureAdventureQuestionRewardTimingCapture(bool includeQuestionReward)
	{
		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Clubs", "5"),
			("Diamonds", "3"),
			("Spades", "A"),
			("Diamonds", "9"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		ItemCard? questionMark = includeQuestionReward
			? CreateItemCard(QuestionMarkItemCardId)
			: null;
		if (includeQuestionReward
			&& (!questionMark.HasValue
				|| !DebugForceNextQuestionRewardItemCardId("bomb")))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Diamonds:9"]);
		_tableCards.Add(new TableCard(cards["Clubs:5"], true));
		if (questionMark.HasValue)
		{
			_tableCards.Add(new TableCard(null, true, questionMark));
		}
		_tableCards.Add(new TableCard(cards["Diamonds:3"], true));
		_pendingItemAttachmentRewards.Clear();
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public Godot.Collections.Dictionary DebugGetCrownCanopyCaptureProbe()
	{
		var result = new Godot.Collections.Dictionary();
		if (_playerHands.Count <= MainPlayerIndex
			|| _playerHands[MainPlayerIndex].Count == 0
			|| _tableCards.Count != 2)
		{
			return result;
		}

		var tableCards = new List<CaptureScoringRules.TableCard>(ToCaptureScoringTableCards())
		{
			ToCaptureScoringTableCard(new TableCard(_playerHands[MainPlayerIndex][0], true)),
		};
		var rawPoints = new List<int>();
		var resolvedPoints = new List<int>();
		var scoringTableIndexes = new List<int>();
		bool includeEndpoints = AdventureUpgradeEffectService.ResolveClubCaptureScoring(
			GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds() ?? Array.Empty<string>())
			.IncludeCaptureEndpointsInScoring;
		int endpointPoints = BuildAdventureCaptureDamageCardPointLists(
			tableCards,
			0,
			MainPlayerIndex,
			new HashSet<int> { 1 },
			rawPoints,
			resolvedPoints,
			scoringTableIndexes,
			includeCaptureStartInScoring: includeEndpoints,
			includeCaptureEndInScoring: includeEndpoints);
		int scoreFillVisualCount = includeEndpoints ? 3 : 1;
		result["capture_start_index"] = 0;
		result["raw_point_count"] = rawPoints.Count;
		result["resolved_point_count"] = resolvedPoints.Count;
		result["scoring_table_index_count"] = scoringTableIndexes.Count;
		result["raw_point_sum"] = SumDebugPoints(rawPoints);
		result["resolved_point_sum"] = SumDebugPoints(resolvedPoints);
		result["endpoint_points"] = endpointPoints;
		result["score_fill_visual_count"] = scoreFillVisualCount;
		return result;
	}

	private static int SumDebugPoints(IReadOnlyList<int> points)
	{
		int sum = 0;
		foreach (int point in points)
		{
			sum += point;
		}
		return sum;
	}

	public bool DebugConfigureAdventureSuitCollectReturnScenario()
	{
		if (!adventure_mode || player_count != 2)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Hearts", "9"),
			("Clubs", "2"),
			("Spades", "7"),
			("Diamonds", "8"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Diamonds:8"]);
		_playerHands[1].Add(cards["Spades:7"]);
		_tableCards.Add(new TableCard(cards["Hearts:9"], true));
		_tableCards.Add(new TableCard(cards["Clubs:2"], true));
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public bool DebugConfigureAdventureTriggeredAttachmentMissScenario()
	{
		if (!adventure_mode || player_count != 2)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Spades", "2"),
			("Diamonds", "7"),
			("Clubs", "9"),
			("Spades", "A"),
			("Diamonds", "10"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Diamonds:10"]);
		_tableCards.Add(new TableCard(cards["Spades:2"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:7"], false));
		_tableCards.Add(new TableCard(cards["Clubs:9"], true));
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public bool DebugConfigureAdventureGapRootingScenario()
	{
		if (!adventure_mode || player_count != 2)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Clubs", "2"),
			("Clubs", "5"),
			("Spades", "A"),
			("Diamonds", "9"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		_playerHands[1].Add(cards["Diamonds:9"]);
		_tableCards.Add(new TableCard(cards["Clubs:2"], true));
		_tableCards.Add(new TableCard(cards["Clubs:5"], true));
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public bool DebugConfigureAdventureTraceHuntMarkerScenario()
	{
		if (!ShouldUseAdventureUpgradeSystem() || player_count != 2)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "5"),
			("Hearts", "7"),
			("Clubs", "5"),
			("Diamonds", "7"),
			("Spades", "2"),
			("Clubs", "A"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		ClearAdventureTraceHuntMarkerIntro();
		_adventureTraceHuntMarkedTableCard = null;
		_playerDecks.Clear();
		_playerHands.Clear();
		_playerItemHands.Clear();
		_tableCards.Clear();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerDecks.Add(new List<PlayingCard>());
			_playerHands.Add(new List<PlayingCard>());
			_playerItemHands.Add(new List<ItemCard>());
		}

		_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		_playerDecks[MainPlayerIndex].Add(cards["Hearts:7"]);
		_playerHands[1].Add(cards["Clubs:A"]);
		_tableCards.Add(new TableCard(cards["Spades:2"], true));
		_tableCards.Add(new TableCard(cards["Diamonds:7"], false));
		_tableCards.Add(new TableCard(cards["Clubs:5"], true));
		_currentTurnPlayerIndex = MainPlayerIndex;
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_winnerPlayerIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public int DebugGetAdventureTraceHuntMarkedTableCardIndex()
	{
		return GetAdventureTraceHuntMarkedTableCardIndex();
	}

	public bool DebugHasAdventureTraceHuntMarkerOverlay()
	{
		return FindAdventureTraceHuntMarkerOverlay() != null;
	}

	public string DebugGetAdventureTraceHuntMarkerOverlayTexturePath()
	{
		return FindAdventureTraceHuntMarkerOverlay()?.Texture?.ResourcePath ?? string.Empty;
	}

	public bool DebugHasAdventureTraceHuntMarkerIntro()
	{
		return _adventureTraceHuntMarkerIntro != null
			&& GodotObject.IsInstanceValid(_adventureTraceHuntMarkerIntro)
			&& !_adventureTraceHuntMarkerIntro.IsQueuedForDeletion();
	}

	public bool DebugRetargetAdventureTraceHuntMarker(int tableCardIndex)
	{
		if (tableCardIndex < 0 || tableCardIndex >= _tableCards.Count)
		{
			return false;
		}

		SetAdventureTraceHuntMarkedTableCard(_tableCards[tableCardIndex], playIntro: true);
		RenderTableCards();
		return true;
	}

	public bool DebugRemoveMarkedAdventureTraceHuntCardWithBombPair()
	{
		if (GetAdventureTraceHuntMarkedTableCardIndex() < 0 || _tableCards.Count == 0)
		{
			return false;
		}

		RemoveBombPairAndCardsBetween(0, _tableCards.Count - 1);
		RenderTableCards();
		return true;
	}

	public bool DebugRemoveMarkedAdventureTraceHuntCardWithLeafyBlade()
	{
		int markedTableCardIndex = GetAdventureTraceHuntMarkedTableCardIndex();
		if (markedTableCardIndex < 0
			|| markedTableCardIndex >= _tableCards.Count
			|| !_tableCards[markedTableCardIndex].Card.HasValue)
		{
			return false;
		}

		MoveLeafySeadragonBladeBreakTableCardToDeck(1, markedTableCardIndex);
		RenderTableCards();
		return true;
	}

	public bool DebugClearAdventureUpgradeRunForTraceHuntMarker()
	{
		ClearAdventureUpgradeRun();
		RenderTableCards();
		return GetAdventureTraceHuntMarkedTableCardIndex() < 0;
	}

	public bool DebugAttachItemEffectToMainPlayerHandCard(int handIndex, string itemCardId)
	{
		if (handIndex < 0
			|| handIndex >= _playerHands[MainPlayerIndex].Count
			|| string.IsNullOrWhiteSpace(itemCardId))
		{
			return false;
		}

		_playerHands[MainPlayerIndex][handIndex] = _playerHands[MainPlayerIndex][handIndex].WithAttachedItemEffect(itemCardId.Trim());
		RenderPlayerHands();
		return true;
	}

	public bool DebugSimulateAdventureEnemyMiss(int tableCardCountAfterMiss, bool hadOldTableCardsBeforeMiss)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return false;
		}

		AdventureEnemyMissEffectResult effectResult = AdventureUpgradeEffectService.ResolveEnemyMissEffects(
			runState.GetAcquiredUpgradeIds(),
			tableCardCountAfterMiss,
			hadOldTableCardsBeforeMiss);
		_adventureEnemyNextBaseCardFacesUp = effectResult.NextCardFacesUp;
		_adventureEnemyNextCardInsertMode = effectResult.InsertMode;
		_adventureEnemyNextCaptureDamageDelta = effectResult.NextCaptureDamageDelta;
		if (effectResult.GrantMainPlayerExtraBaseAction)
		{
			TryGrantAdventureMainPlayerExtraBaseCardAction(
				AdventureInterceptBeatUpgradeId,
				resumeNormalTurnAfterChain: true);
		}

		if (effectResult.DirectDamageToEnemy > 0)
		{
			AddPlayerScore(MainPlayerIndex, effectResult.DirectDamageToEnemy);
		}

		return true;
	}

	public bool DebugSimulateAdventureEnemyCapture()
	{
		if (!ShouldUseAdventureUpgradeSystem() || player_count <= 1)
		{
			return false;
		}

		AdventureUpgradeRunState? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return false;
		}

		ApplyAdventureEnemyCaptureEffects(1);
		return true;
	}

	public bool DebugSimulateAdventureMainPlayerMiss(int tableCardCountAfterMiss)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureUpgradeRunState? runState = store?.GetRunState();
		if (runState?.IsActive != true)
		{
			return false;
		}

		AdventureMissCommitResult commitResult = store!.ResolveMissAction(
			new AdventureMissCommitInput(
				string.Empty,
				tableCardCountAfterMiss,
				Array.Empty<string>()));
		if (commitResult.WriteResult != AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		if (commitResult.CommitBandageAtRealTurnEnd)
		{
			_state.AdventureExtraActionQueue.RegisterMissBandageForRealTurnEnd();
		}
		RenderPlayerHands();
		if (commitResult.EffectResult.GrantExtraBaseCardAction)
		{
			TryGrantAdventureMainPlayerExtraBaseCardAction(
				AdventureContinueBranchUpgradeId);
		}

		return true;
	}

	public bool DebugGetAdventureEnemyNextCardFacesUp()
	{
		return _adventureEnemyNextBaseCardFacesUp;
	}

	public string DebugGetAdventureEnemyNextCardInsertModeName()
	{
		return _adventureEnemyNextCardInsertMode.ToString();
	}

	public int DebugGetAdventureEnemyNextCaptureDamageDelta()
	{
		return _adventureEnemyNextCaptureDamageDelta;
	}

	public bool DebugHasAdventurePendingExtraBaseCardAction()
	{
		return HasPendingAdventureExtraBaseCardAction();
	}

	public string DebugResolveAdventureQuestionRewardRejudge(string itemCardId)
	{
		if (string.IsNullOrWhiteSpace(itemCardId))
		{
			return string.Empty;
		}

		return MaybeRejudgeQuestionRewardItemCardIdAsync(itemCardId.Trim(), MainPlayerIndex)
			.GetAwaiter()
			.GetResult();
	}

	public string DebugResolveAdventureQuestionRewardRejudgeWithChoice(string itemCardId, bool rerollReward)
	{
		if (string.IsNullOrWhiteSpace(itemCardId))
		{
			return string.Empty;
		}

		return MaybeRejudgeQuestionRewardItemCardIdAsync(
			itemCardId.Trim(),
			MainPlayerIndex,
			rerollReward ? AdventureQuestionRejudgeChoice.Reroll : AdventureQuestionRejudgeChoice.Keep)
			.GetAwaiter()
			.GetResult();
	}

	public bool DebugHasAdventureQuestionRejudgeUsed()
	{
		return _adventureQuestionRejudgeUsedThisBattle;
	}

	public void DebugForceNextPendingHandLimitDiscardAiFallback()
	{
		_debugForceNextPendingHandLimitDiscardHeuristicFailure = true;
	}

	public void DebugForceNextAdventureUpgradeOverlayDisplayFailure()
	{
		_debugForceNextAdventureUpgradeOverlayDisplayFailure = true;
	}

}
