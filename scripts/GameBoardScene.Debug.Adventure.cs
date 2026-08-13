using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Deckland.Rules;
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

	private DebugRuleFixture CreateNonTerminalAdventureDebugRuleFixture()
	{
		DebugRuleFixture fixture = CreateEmptyDebugRuleFixture();
		for (int playerIndex = 0; playerIndex < fixture.PlayerScores.Count; playerIndex++)
		{
			fixture.PlayerScores[playerIndex] = 100;
		}
		return fixture;
	}

	public bool DebugAcquireAdventureUpgrade(string upgradeId)
	{
		if (!ShouldUseAdventureUpgradeSystem() || string.IsNullOrWhiteSpace(upgradeId))
		{
			return false;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
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

		TrackBoardTask(ApplyAdventureUpgradeAcquisitionEffectsAsync(
			grantResult,
			_turnSequenceGeneration), nameof(ApplyAdventureUpgradeAcquisitionEffectsAsync));
		RenderPlayerHands();
		return true;
	}

	public bool DebugConfigureAdventureMissClimbBadgeScenario(int growthStacks)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureRunReadModel? runState = store?.GetReadModel();
		if (!ShouldUseAdventureUpgradeSystem()
			|| player_count != 2
			|| store == null
			|| runState?.IsActive != true
			|| growthStacks < 0
			|| growthStacks > AdventureRunReadModel.MaxClubMissClimbGrowthStacks)
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:4"]);
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Spades:6"]);
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Diamonds:10"]);
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Clubs:J"]);
		fixture.PlayerItemHands[MainPlayerIndex].Add(itemCard.Value);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerHands[1].Add(cards["Diamonds:9"]);
		fixture.PlayerDecks[1].Add(cards["Clubs:8"]);
		fixture.TableCards.Add(new TableCard(cards["Clubs:4"], true));

		AdventureEffectStateSnapshot effectState = runState.Effects with
		{
			ClubMissClimbGrowthStacks = growthStacks,
		};
		if (store.ConfigureEffectStateForDebug(effectState)
			!= AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_mainPlayerHandSelectionInputEnabled = false;
		_selectedMainPlayerHandDisplayIndex = -1;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public int DebugGetAdventureMissClimbGrowthStacks()
	{
		return GetAdventureUpgradeRunState()?.Effects.ClubMissClimbGrowthStacks ?? 0;
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
		AdventureRunReadModel? initialRunState = store?.GetReadModel();
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
		ItemCard? bombCard = CreateItemCard(BombItemCardId);
		if (!bombCard.HasValue
			|| !TryLoadAdventureTutorialCards(
				requiredCards,
				out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.TableCards.Add(new TableCard(cards["Hearts:5"], true));
		fixture.TableCards.Add(new TableCard(cards["Spades:10"], true));
		fixture.TableCards.Add(new TableCard(
			null,
			isFaceUp: false,
			itemOverlay: bombCard.Value,
			itemOwnerPlayerIndex: MainPlayerIndex));
		fixture.TableCards.Add(new TableCard(cards["Clubs:4"], true));
		fixture.PlayerHands[1].Add(cards["Diamonds:5"]);
		// Adventure capture damage for this fixed 10-and-4 middle-card setup.
		_debugBlackLedgerExpectedDamage = 14;
		fixture.PlayerScores[MainPlayerIndex] = 100;
		fixture.PlayerScores[1] = 100;
		fixture.CurrentTurnSeat = 1;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
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
		TrackBoardTask(RunDebugBlackLedgerCaptureProbeAsync(result), nameof(RunDebugBlackLedgerCaptureProbeAsync));
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
		catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		fixture.PlayerHands[1].Add(cards["Diamonds:9"]);
		fixture.TableCards.Add(new TableCard(cards["Clubs:5"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		fixture.PlayerHands[1].Add(cards["Diamonds:9"]);
		fixture.TableCards.Add(new TableCard(cards["Clubs:5"], true));
		fixture.TableCards.Add(new TableCard(cards["Clubs:2"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public bool DebugConfigureAdventureQuestionRewardTimingScenario()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureRunReadModel? initialRunState = store?.GetReadModel();
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

		AdventureRunReadModel runState = store.GetReadModel();
		AdventureEffectStateSnapshot timingEffectState = runState.Effects with
		{
			QuestionRewardCount = 9,
			PermanentCaptureDamageBonus = 4,
			FinalDoubleCaptureDamageActive = false,
		};
		if (store.ConfigureEffectStateForDebug(timingEffectState)
			!= AdventureRunWriteResult.Succeeded)
		{
			return false;
		}
		return ConfigureAdventureQuestionRewardTimingCapture(includeQuestionReward: true);
	}

	public bool DebugConfigureAdventureQuestionRewardTimingFollowUpCapture()
	{
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true
			|| !runState.HasAcquiredUpgrade("diamonds_question_mark_interest")
			|| !runState.HasAcquiredUpgrade("diamonds_final_double")
		|| runState.Effects.QuestionRewardCount != 10
			|| runState.Effects.PermanentCaptureDamageBonus != 5
			|| !runState.Effects.FinalDoubleCaptureDamageActive)
		{
			return false;
		}

		return ConfigureAdventureQuestionRewardTimingCapture(includeQuestionReward: false);
	}

	public bool DebugConfigureAdventureDoubleQuestionRewardCapacityScenario()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		AdventureRunReadModel? initialRunState = store?.GetReadModel();
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		for (int handIndex = 1; handIndex < MaxTotalHandCards; handIndex++)
		{
			fixture.PlayerHands[MainPlayerIndex].Add(cards["Spades:A"]);
		}
		fixture.PlayerHands[1].Add(cards["Diamonds:9"]);
		fixture.TableCards.Add(new TableCard(cards["Clubs:5"], true));
		fixture.TableCards.Add(new TableCard(null, true, questionMark));
		fixture.TableCards.Add(new TableCard(cards["Diamonds:3"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		TryUpdateMatchFlowState(state => state with
		{
			SuccessfulCapturesThisTurn = 1,
		});
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return true;
	}

	public Godot.Collections.Dictionary DebugGetAdventureQuestionRewardTimingState()
	{
		var result = new Godot.Collections.Dictionary();
		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return result;
		}

		result["question_reward_count"] = runState.Effects.QuestionRewardCount;
		result["permanent_capture_damage_bonus"] = runState.Effects.PermanentCaptureDamageBonus;
		result["final_double_capture_damage_active"] = runState.Effects.FinalDoubleCaptureDamageActive;
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		fixture.PlayerHands[1].Add(cards["Diamonds:9"]);
		fixture.TableCards.Add(new TableCard(cards["Clubs:5"], true));
		if (questionMark.HasValue)
		{
			fixture.TableCards.Add(new TableCard(null, true, questionMark));
		}
		fixture.TableCards.Add(new TableCard(cards["Diamonds:3"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
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

		var tableCards = new List<CaptureScoringTableCard>(ToCaptureScoringTableCards())
		{
			ToCaptureScoringTableCard(new TableCard(_playerHands[MainPlayerIndex][0], true)),
		};
		var rawPoints = new List<int>();
		var resolvedPoints = new List<int>();
		var scoringTableIndexes = new List<int>();
		bool includeEndpoints = (GetAdventureUpgradeRunState()?.ResolveClubCaptureScoring() ?? default)
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Diamonds:8"]);
		fixture.PlayerHands[1].Add(cards["Spades:7"]);
		fixture.TableCards.Add(new TableCard(cards["Hearts:9"], true));
		fixture.TableCards.Add(new TableCard(cards["Clubs:2"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		fixture.PlayerHands[1].Add(cards["Diamonds:10"]);
		fixture.TableCards.Add(new TableCard(cards["Spades:2"], true));
		fixture.TableCards.Add(new TableCard(cards["Diamonds:7"], false));
		fixture.TableCards.Add(new TableCard(cards["Clubs:9"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
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

		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Spades:A"]);
		fixture.PlayerHands[1].Add(cards["Diamonds:9"]);
		fixture.TableCards.Add(new TableCard(cards["Clubs:2"], true));
		fixture.TableCards.Add(new TableCard(cards["Clubs:5"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
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
		SetAdventureTraceHuntMarkedTableCard(null, playIntro: false);
		DebugRuleFixture fixture = CreateNonTerminalAdventureDebugRuleFixture();
		fixture.PlayerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
		fixture.PlayerDecks[MainPlayerIndex].Add(cards["Hearts:7"]);
		fixture.PlayerHands[1].Add(cards["Clubs:A"]);
		fixture.TableCards.Add(new TableCard(cards["Spades:2"], true));
		fixture.TableCards.Add(new TableCard(cards["Diamonds:7"], false));
		fixture.TableCards.Add(new TableCard(cards["Clubs:5"], true));
		fixture.CurrentTurnSeat = MainPlayerIndex;
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		_isOpeningDealAnimationPlaying = false;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
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

		MoveLeafySeadragonBladeBreakTableCardToDeck(
			1,
			markedTableCardIndex,
			_turnSequenceGeneration);
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

		if (!TrySubmitAttachItemToHandCardRuleCommand(
			MainPlayerIndex,
			handIndex,
			itemCardId.Trim(),
			ImmutableArray<int>.Empty,
			_turnSequenceGeneration,
			out _))
		{
			return false;
		}
		RenderPlayerHands();
		return true;
	}

	public bool DebugSimulateAdventureEnemyMiss(int tableCardCountAfterMiss, bool hadOldTableCardsBeforeMiss)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return false;
		}

		AdventureEnemyMissEffectResult effectResult = runState.ResolveEnemyMissEffects(
			tableCardCountAfterMiss,
			hadOldTableCardsBeforeMiss);
		TryUpdateMatchFlowState(state => state with
		{
			PendingBaseCardFacesUp = effectResult.NextCardFacesUp,
			PendingCardPlacement = (PendingCardPlacementMode)(int)effectResult.InsertMode,
			PendingCaptureDamageDelta = effectResult.NextCaptureDamageDelta,
		});
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

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
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
		AdventureRunReadModel? runState = store?.GetReadModel();
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
			TrySubmitRegisterRealTurnEndMarkerRuleCommand(
				MainPlayerIndex,
				AdventureMissBandageRealTurnEndMarkerId,
				_turnSequenceGeneration);
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
		return CurrentMatchFlowState.PendingBaseCardFacesUp;
	}

	public string DebugGetAdventureEnemyNextCardInsertModeName()
	{
		return GetPendingAdventureEnemyCardInsertMode().ToString();
	}

	public int DebugGetAdventureEnemyNextCaptureDamageDelta()
	{
		return CurrentMatchFlowState.PendingCaptureDamageDelta;
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
		return CurrentMatchFlowState.QuestionRejudgeUsedThisBattle;
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
