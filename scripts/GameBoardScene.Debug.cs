using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private const int DebugTableShuffleRenderSignatureSentinel = unchecked((int)0xA17E5EED);
	private bool _debugTableShuffleProbeRunning;
	private ulong _debugTableShuffleProbeStartedUsec;
	private TableCard[] _debugTableShuffleOriginalCards = Array.Empty<TableCard>();
	private bool[] _debugTableShuffleOriginalHiddenStates = Array.Empty<bool>();
	private TableCard[] _debugTableShuffleSentinelCards = Array.Empty<TableCard>();
	private bool[] _debugTableShuffleSentinelHiddenStates = Array.Empty<bool>();
	private ulong _debugTableShuffleVortexInstanceId;
	private TextureRect? _debugTableShuffleReusedVortex;
	private Texture2D? _debugTableShuffleReuseSentinelTexture;
	private const string DebugPendingTargetHandSelectionItemCardId = "Q";
	private bool _debugLeafyBladeProbeRunning;
	private bool _debugBlackLedgerProbeRunning;
	private int _debugBlackLedgerExpectedDamage;
	private int _debugLeafyBladeTargetTableCardIndex = -1;
	private TableCard? _debugLeafyBladeReplacementTableCard;
	private bool _debugLeafyBladeReplacementHiddenSentinel;

	public bool DebugStartPendingHandLimitDiscardForSeat(int ownerPlayerIndex)
	{
		if (!item_mode
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| GetTotalHandCardCount(ownerPlayerIndex) <= 0
			|| _pendingHandLimitDiscardCompletion != null
			|| HasPendingItemSelection())
		{
			return false;
		}

		_ = ResolveHumanHandLimitDiscardAsync(ownerPlayerIndex, _turnSequenceGeneration);
		return true;
	}

	public bool DebugStartPendingTargetHandSelectionForSeat(int ownerPlayerIndex)
	{
		if (!item_mode
			|| ownerPlayerIndex < 0
			|| ownerPlayerIndex >= player_count
			|| HasPendingItemSelection()
			|| _pendingHandLimitDiscardCompletion != null
			|| _pendingMagnetAttachmentCompletion != null
			|| !_itemCardCatalog.TryGetDefinition(DebugPendingTargetHandSelectionItemCardId, out _))
		{
			return false;
		}

		int targetPlayerIndex = -1;
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (IsItemTargetPlayerCandidate(DebugPendingTargetHandSelectionItemCardId, ownerPlayerIndex, playerIndex))
			{
				targetPlayerIndex = playerIndex;
				break;
			}
		}

		if (targetPlayerIndex < 0)
		{
			return false;
		}

		Texture2D? placeholderTexture = _cardBackTexture;
		if (placeholderTexture == null)
		{
			for (int playerIndex = 0; playerIndex < _playerHands.Count && placeholderTexture == null; playerIndex++)
			{
				if (_playerHands[playerIndex].Count > 0)
				{
					placeholderTexture = _playerHands[playerIndex][0].Texture;
				}
			}
		}

		if (placeholderTexture == null
			|| ownerPlayerIndex >= _playerItemHands.Count)
		{
			return false;
		}

		_playerItemHands[ownerPlayerIndex].Add(new ItemCard(
			DebugPendingTargetHandSelectionItemCardId,
			placeholderTexture));
		_isProcessingCardPlay = true;
		StartPendingItemSelection(
			DebugPendingTargetHandSelectionItemCardId,
			ownerPlayerIndex,
			_playerItemHands[ownerPlayerIndex].Count - 1,
			_turnSequenceGeneration);
		return true;
	}

	public string DebugGetPendingItemSelectionModeName()
	{
		return _pendingItemSelectionMode.ToString();
	}

	public bool DebugPlayFirstAvailableNetworkHostHandCard()
	{
		if (!IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| _isOpeningDealAnimationPlaying
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| HasPendingItemSelection())
		{
			return false;
		}

		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex != _currentTurnPlayerIndex
			|| localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| _playerHands[localPlayerIndex].Count == 0)
		{
			return false;
		}

		return TryStartNetworkHandCardPlayFromHost(
			localPlayerIndex,
			0,
			_turnSequenceGeneration,
			out _);
	}

	public bool DebugPlayFirstAvailableLocalMainPlayerHandCard()
	{
		if (IsNetworkMultiplayerMode()
			|| _isOpeningDealAnimationPlaying
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| HasPendingItemSelection())
		{
			return false;
		}

		int localPlayerIndex = GetLocalHandPlayerIndex();
		if (localPlayerIndex != _currentTurnPlayerIndex
			|| localPlayerIndex < 0
			|| localPlayerIndex >= _playerHands.Count
			|| _playerHands[localPlayerIndex].Count == 0)
		{
			return false;
		}

		ConfirmLocalHandCardPlay(
			0,
			_turnSequenceGeneration,
			TryGetHandCardStartGlobalPosition(localPlayerIndex, 0));
		return true;
	}

	public bool DebugRestartWithoutOpeningDealAnimation()
	{
		if (!IsInsideTree()
			|| _playerCardPiles.Count != MaxPlayers
			|| _playerHandAreas.Count != MaxPlayers)
		{
			return false;
		}

		DealCardsToPlayers(playOpeningDealAnimation: false);
		return !_isOpeningDealAnimationPlaying;
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

		if (RecordAdventureAcquiredUpgrade(upgradeId)
			!= AdventureUpgradeAcquireResult.Acquired)
		{
			return false;
		}

		_ = ApplyAdventureUpgradeAcquisitionEffectsAsync(
			upgradeId,
			AdventureUpgradeTriggerSource.None,
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
		store.ReplaceEffectState(runState.EffectState.Clone());
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
		store.BeginNewRun(avatarStageNumber);
		store.MarkRunStartUpgradeGranted();
		if (store.AcquireUpgrade("spades_black_ledger")
			!= AdventureUpgradeAcquireResult.Acquired)
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
		store.BeginNewRun(avatarStageNumber);
		store.MarkRunStartUpgradeGranted();
		if (store.AcquireUpgrade("diamonds_question_mark_interest")
			!= AdventureUpgradeAcquireResult.Acquired
			|| store.AcquireUpgrade("diamonds_final_double")
				!= AdventureUpgradeAcquireResult.Acquired)
		{
			return false;
		}

		AdventureUpgradeRunState runState = store.GetRunState();
		runState.EffectState.QuestionRewardCount = 9;
		runState.EffectState.PermanentCaptureDamageBonus = 4;
		runState.EffectState.FinalDoubleCaptureDamageActive = false;
		store.ReplaceEffectState(runState.EffectState.Clone());
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

		RemoveLeafySeadragonBladeBreakTableCard(markedTableCardIndex);
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

		int nextTurnHealBeforeMiss = runState.EffectState.NextTurnHealAmount;
		int nextTurnDrawBeforeMiss = runState.EffectState.NextTurnDrawCount;
		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			tableCardCountAfterMiss);
		if (runState.HasAcquiredUpgrade("hearts_miss_bandage"))
		{
			runState.EffectState.NextTurnHealAmount = nextTurnHealBeforeMiss;
			runState.EffectState.NextTurnDrawCount = nextTurnDrawBeforeMiss;
			_state.AdventureExtraActionQueue.RegisterMissBandageForRealTurnEnd();
		}

		store!.ReplaceEffectState(runState.EffectState.Clone());
		RenderPlayerHands();
		if (result.GrantExtraBaseCardAction)
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

	public void DebugForceNextPendingTargetSelectionAiFallback()
	{
		_debugForceNextPendingTargetPlayerHeuristicFailure = true;
		_debugForceNextPendingTargetHandHeuristicFailure = true;
	}

	public void DebugForceNextPendingHandLimitDiscardAiFallback()
	{
		_debugForceNextPendingHandLimitDiscardHeuristicFailure = true;
	}

	public void DebugForceNextAdventureUpgradeOverlayDisplayFailure()
	{
		_debugForceNextAdventureUpgradeOverlayDisplayFailure = true;
	}

	public string DebugGetGameLoopPhaseName()
	{
		return GetGameLoopPhase().ToString();
	}

	public int DebugGetCurrentTurnPlayerIndex()
	{
		return _currentTurnPlayerIndex;
	}

	public int DebugGetLocalPlayerViewSlotIndex()
	{
		return GetPlayerViewSlotIndex(GetLocalHandPlayerIndex());
	}

	public string DebugGetLocalSeatControlModeName()
	{
		return TryGetSeatControlStateForUi(GetLocalHandPlayerIndex(), out SeatControlState state)
			? state.Mode.ToString()
			: string.Empty;
	}

	public void DebugSetNetworkVisualPlaybackDelaySeconds(double seconds)
	{
		_debugNetworkVisualPlaybackDelaySeconds = Math.Max(0.0, seconds);
	}

	public int DebugGetTotalHandCardCountForPlayer(int playerIndex)
	{
		return GetTotalHandCardCount(playerIndex);
	}

	public int DebugGetTotalItemHandCardCountForPlayer(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerItemHands.Count)
		{
			return 0;
		}

		return _playerItemHands[playerIndex].Count;
	}

	public bool DebugConfigureDrawCardItemAvailabilityScenario(
		int deckCardCount,
		int totalHandCardCountBeforeUse)
	{
		if (!item_mode
			|| player_count < 1
			|| deckCardCount < 0
			|| totalHandCardCountBeforeUse < 1
			|| totalHandCardCountBeforeUse > MaxTotalHandCards
			|| !_itemCardCatalog.TryGetDefinition(ItemCardCatalog.DrawCardItemCardId, out _)
			|| !TryLoadAdventureTutorialCards(
				new[] { ("Hearts", "5") },
				out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		PlayingCard sourceCard = cards["Hearts:5"];
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

		for (int handIndex = 1; handIndex < totalHandCardCountBeforeUse; handIndex++)
		{
			_playerHands[MainPlayerIndex].Add(sourceCard);
		}
	for (int deckIndex = 0; deckIndex < deckCardCount; deckIndex++)
	{
		_playerDecks[MainPlayerIndex].Add(sourceCard);
	}
	_playerItemHands[MainPlayerIndex].Add(new ItemCard(
		ItemCardCatalog.DrawCardItemCardId,
		sourceCard.Texture));
	for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
	{
		if (playerIndex != MainPlayerIndex)
		{
			_playerHands[playerIndex].Add(sourceCard);
		}
	}
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

	public bool DebugCanClickDrawCardItem()
	{
		return CanMainPlayerAct()
			&& _playerItemHands.Count > MainPlayerIndex
			&& _playerItemHands[MainPlayerIndex].Count == 1
			&& CanPlayItemCard(MainPlayerIndex, ItemCardCatalog.DrawCardItemCardId);
	}

	public void DebugClickDrawCardItem()
	{
		ConfirmMainPlayerItemCard(0);
	}

	public int DebugCountAiPlayableDrawCardItems()
	{
		return CountPlayableItemIndexes(MainPlayerIndex);
	}

	public bool DebugNetworkAuthorityAcceptsDrawCardItem()
	{
		return TryStartNetworkItemCardPlayFromHost(
			MainPlayerIndex,
			0,
			_turnSequenceGeneration,
			out _);
	}

	public int DebugGetQuestionRewardGrantCount()
	{
		return _debugQuestionRewardGrantCount;
	}

	public string DebugGetLastQuestionRewardItemCardId()
	{
		return _debugLastQuestionRewardItemCardId;
	}

	public Vector2 DebugGetCardPileCaptureTargetGlobalPosition(int playerIndex)
	{
		return GetCardPileCaptureTargetGlobalPosition(playerIndex);
	}

	public bool DebugAddScoreAndCheckWinner(int playerIndex, int points)
	{
		if (playerIndex < 0
			|| playerIndex >= player_count
			|| points <= 0
			|| IsGameOver())
		{
			return false;
		}

		AddPlayerScore(playerIndex, points);
		CheckForWinner();
		SendBoardSnapshotIfHost();
		return true;
	}

	public bool DebugSetPlayerScore(int playerIndex, int score)
	{
		if (playerIndex < 0 || playerIndex >= player_count || score < 0)
		{
			return false;
		}

		_playerScores[playerIndex] = score;
		UpdatePlayerScoreMeter(playerIndex, false);
		return true;
	}

	public bool DebugIsNetworkVisualQueueProcessing()
	{
		return NetworkVisualPlayback.IsProcessing;
	}

	public int DebugGetNetworkVisualEventQueueCount()
	{
		return NetworkVisualPlayback.PendingEventCount;
	}

	public int DebugGetNetworkLastAppliedSnapshotId()
	{
		return _networkLastAppliedSnapshotId;
	}

	public Godot.Collections.Dictionary? DebugConfigureNetworkVisualQueueFixture()
	{
		EnsureNetworkCardCache();
		if (_networkCardsByCode.Count == 0 || player_count < 2)
		{
			return null;
		}

		string cardCode = string.Empty;
		foreach (string candidate in _networkCardsByCode.Keys)
		{
			cardCode = candidate;
			break;
		}
		if (string.IsNullOrEmpty(cardCode))
		{
			return null;
		}

		EnsureNetworkStateLists();
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			_playerDecks[playerIndex].Clear();
			_playerHands[playerIndex].Clear();
			_playerItemHands[playerIndex].Clear();
			_playerDecks[playerIndex].Add(CreateNetworkPlayingCard(cardCode));
			_playerHands[playerIndex].Add(CreateNetworkPlayingCard(cardCode));
		}
		_tableCards.Clear();
		_tableCards.Add(new TableCard(CreateNetworkPlayingCard(cardCode), true));
		_tableCards.Add(new TableCard(CreateNetworkPlayingCard(cardCode), true));
		_winnerPlayerIndex = -1;
		_isProcessingCardPlay = false;
		_isResolvingTurnSequence = false;
		_isOpeningDealAnimationPlaying = false;

		Godot.Collections.Dictionary snapshot = BuildNetworkBoardSnapshotForSeat(
			GetLocalHandPlayerIndex(),
			_networkLastAppliedSnapshotId + 1);
		var seatStates = new Godot.Collections.Array();
		for (int seatIndex = 0; seatIndex < player_count; seatIndex++)
		{
			seatStates.Add(new Godot.Collections.Dictionary
			{
				["seat_index"] = seatIndex,
				["mode"] = "HumanConnected",
				["peer_id"] = 0,
				["stable_user_id"] = string.Empty,
				["reconnect_token"] = string.Empty,
			});
		}
		snapshot["seat_control_states"] = seatStates;
		snapshot["debug_target_hand_card_code"] = cardCode;
		return snapshot;
	}

	// Smoke-only trust-boundary probes. They do not apply or enqueue data.
	public bool DebugRejectsNetworkSnapshotPayload(
		Godot.Collections.Dictionary payload)
	{
		return !IsUsableNetworkSnapshot(payload, out _);
	}

	public bool DebugApplyNetworkSnapshotPayload(
		Godot.Collections.Dictionary payload)
	{
		if (!TryDecodeNetworkBoardSnapshot(payload, out ValidatedNetworkBoardSnapshot snapshot))
		{
			return false;
		}

		ApplyNetworkBoardSnapshotNow(snapshot);
		return true;
	}

	public string DebugGetNetworkStateFingerprint()
	{
		var parts = new List<string>
		{
			_networkLastAppliedSnapshotId.ToString(),
			_turnSequenceGeneration.ToString(),
			_currentTurnPlayerIndex.ToString(),
			_winnerPlayerIndex.ToString(),
			_tableCards.Count.ToString(),
		};
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			parts.Add(_playerScores[playerIndex].ToString());
			parts.Add(_playerDecks[playerIndex].Count.ToString());
			parts.Add(_playerHands[playerIndex].Count.ToString());
			parts.Add(_playerItemHands[playerIndex].Count.ToString());
		}
		parts.Add("table:");
		foreach (TableCard tableCard in _tableCards)
		{
			parts.Add(tableCard.Card.HasValue ? ToNetworkCardCode(tableCard.Card.Value) : string.Empty);
			parts.Add(tableCard.ItemOverlay.HasValue ? tableCard.ItemOverlay.Value.Id : string.Empty);
			parts.Add(tableCard.IsFaceUp ? "1" : "0");
		}

		return string.Join("|", parts);
	}

	public bool DebugRejectsNetworkVisualEventPayload(
		Godot.Collections.Dictionary payload)
	{
		return !TryDecodeNetworkVisualEvent(payload, out _);
	}

	// Decodes before the replacement snapshot on purpose. This lets the smoke suite
	// exercise a real queue race without allowing untyped payload data into playback.
	public bool DebugPlayDecodedNetworkVisualEventAfterSnapshot(
		Godot.Collections.Dictionary payload,
		Godot.Collections.Dictionary replacementSnapshot)
	{
		if (!TryDecodeNetworkVisualEvent(payload, out NetworkVisualEvent visualEvent)
			|| !TryDecodeNetworkBoardSnapshot(replacementSnapshot, out ValidatedNetworkBoardSnapshot snapshot))
		{
			return false;
		}

		ApplyNetworkBoardSnapshotNow(snapshot);
		_ = PlayNetworkCardPlayedEventAsync(visualEvent);
		return true;
	}

	// Stages a decoded reconnect snapshot before starting the production queue.
	// The processor must fence the older visual event and atomically apply the
	// staged snapshot from its finally block after draining the queue.
	public bool DebugQueueDecodedNetworkVisualEventWithSnapshotFence(
		Godot.Collections.Dictionary payload,
		Godot.Collections.Dictionary replacementSnapshot)
	{
		if (!TryDecodeNetworkVisualEvent(payload, out NetworkVisualEvent visualEvent)
			|| !TryDecodeNetworkBoardSnapshot(replacementSnapshot, out ValidatedNetworkBoardSnapshot snapshot)
			|| snapshot.SnapshotId <= visualEvent.SnapshotId)
		{
			return false;
		}

			NetworkVisualPlayback.StagePendingSnapshot(snapshot);
			NetworkVisualPlayback.Enqueue(visualEvent);
			return true;
	}

	public bool DebugQueueDelayedNetworkVisualEventWithSnapshotFence(
		Godot.Collections.Dictionary blockingPayload,
		Godot.Collections.Dictionary queuedPayload,
		Godot.Collections.Dictionary replacementSnapshot)
	{
		if (!TryDecodeNetworkVisualEvent(blockingPayload, out NetworkVisualEvent blockingEvent)
			|| !TryDecodeNetworkVisualEvent(queuedPayload, out NetworkVisualEvent queuedEvent)
			|| !TryDecodeNetworkBoardSnapshot(replacementSnapshot, out ValidatedNetworkBoardSnapshot snapshot)
			|| snapshot.SnapshotId <= blockingEvent.SnapshotId
			|| snapshot.SnapshotId <= queuedEvent.SnapshotId)
		{
			return false;
		}

		NetworkVisualPlayback.Enqueue(blockingEvent);
		NetworkVisualPlayback.Enqueue(queuedEvent);
		NetworkVisualPlayback.StagePendingSnapshot(snapshot);
		return true;
	}

	public bool DebugIsReconnectCatchupPendingCompletion()
	{
		return _networkReconnectCatchupPendingCompletion;
	}

	public bool DebugIsReconnectOverlayVisible()
	{
		return _networkReconnectOverlayPanel?.Visible == true;
	}

	public int DebugGetCaptureDamageTargetPlayerIndexForHandCard(int playerIndex, int handIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return -1;
		}

		PlayingCard playedCard = _playerHands[playerIndex][handIndex];
		CardPlayPlan plan = ResolveCardPlayPlan(playedCard);
		if (plan.CaptureStartIndex < 0)
		{
			return -1;
		}

		int scorePlayerIndex = ScoreService.GetCapturedScorePlayerIndex(
			ToCaptureScoringTableCards(),
			plan.CaptureStartIndex,
			playerIndex,
			player_count);
		return adventure_mode
			? GetAdventureDamageTargetPlayerIndex(scorePlayerIndex)
			: scorePlayerIndex;
	}

	public int DebugGetCapturePointsForHandCard(int playerIndex, int handIndex)
	{
		if (playerIndex < 0
			|| playerIndex >= _playerHands.Count
			|| handIndex < 0
			|| handIndex >= _playerHands[playerIndex].Count)
		{
			return 0;
		}

		PlayingCard playedCard = _playerHands[playerIndex][handIndex];
		CardPlayPlan plan = ResolveCardPlayPlan(playedCard);
		if (plan.CaptureStartIndex < 0)
		{
			return 0;
		}

		int scorePlayerIndex = ScoreService.GetCapturedScorePlayerIndex(
			ToCaptureScoringTableCards(),
			plan.CaptureStartIndex,
			playerIndex,
			player_count);
		return GetCapturedTablePoints(plan.CaptureStartIndex, scorePlayerIndex);
	}

	public bool DebugIsAdventureUpgradeSelectionVisible()
	{
		return IsSelectionOverlayVisible(SelectionOverlayKind.AdventureUpgrade)
			&& _adventureUpgradeSelectionCompletionSource != null;
	}

	public bool DebugShowAdventureUpgradeSelectionOverlaySample()
	{
		AdventureUpgradeOverlayOptionModel[] options =
		{
			new("clubs", "common", "示例强化一。"),
			new("diamonds", "rare", "示例强化二。"),
			new("spades", "uncommon", "示例强化三。"),
		};
		return ShowAdventureUpgradeSelectionOverlay(options);
	}

	public bool DebugShowAdventureQuestionRejudgeOverlaySample()
	{
		ItemCard? rewardItem = CreateItemCard(ItemCardCatalog.OvertimeItemCardId)
			?? CreateItemCard(ItemCardCatalog.BombItemCardId);
		if (!rewardItem.HasValue)
		{
			return false;
		}

		return ShowAdventureQuestionRejudgeOverlay(rewardItem.Value, (_, _) => DisposeActiveSelectionOverlay());
	}

	private static AdventureSuitUpgradeDefinition[] CreateDebugAdventureUpgradeAwaitOffer()
	{
		return new[]
		{
			new AdventureSuitUpgradeDefinition(
				"debug_upgrade_await_probe",
				"Debug Upgrade Await Probe",
				"clubs",
				"common",
				string.Empty,
				"Debug upgrade await probe.",
				string.Empty,
				string.Empty,
				true,
				0),
		};
	}

	private static void ResetDebugAwaitProbeResult(Godot.Collections.Dictionary result)
	{
		result.Clear();
		result["completed"] = false;
		result["completion_count"] = 0;
		result["error"] = string.Empty;
	}

	public bool DebugStartAdventureUpgradeSelectionAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureUpgradeSelectionAwaitProbe(result, replaceActive: false);
	}

	public bool DebugReplaceAdventureUpgradeSelectionAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureUpgradeSelectionAwaitProbe(result, replaceActive: true);
	}

	private bool StartDebugAdventureUpgradeSelectionAwaitProbe(
		Godot.Collections.Dictionary result,
		bool replaceActive)
	{
		if (result == null)
		{
			return false;
		}

		if (replaceActive)
		{
			if (GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade) == null)
			{
				return false;
			}
		}
		else if (_adventureUpgradeSelectionCompletionSource != null
			|| _activeSelectionOverlaySession != null)
		{
			return false;
		}

		ResetDebugAwaitProbeResult(result);
		_ = RunDebugAdventureUpgradeSelectionAwaitProbeAsync(result);
		return true;
	}

	private async Task RunDebugAdventureUpgradeSelectionAwaitProbeAsync(
		Godot.Collections.Dictionary result)
	{
		try
		{
			AdventureUpgradeSelectionResult selection =
				await ShowAdventureUpgradeSelectionAsync(
					AdventureUpgradeTriggerSource.None,
					CreateDebugAdventureUpgradeAwaitOffer());
			result["outcome"] = selection.Outcome.ToString();
			result["selected_index"] = selection.SelectedIndex;
		}
		catch (Exception exception)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completion_count"] = 1;
			result["completed"] = true;
		}
	}

	public bool DebugStartAdventureQuestionRejudgeAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureQuestionRejudgeAwaitProbe(result, replaceActive: false);
	}

	public bool DebugReplaceAdventureQuestionRejudgeAwaitProbe(Godot.Collections.Dictionary result)
	{
		return StartDebugAdventureQuestionRejudgeAwaitProbe(result, replaceActive: true);
	}

	private bool StartDebugAdventureQuestionRejudgeAwaitProbe(
		Godot.Collections.Dictionary result,
		bool replaceActive)
	{
		if (result == null)
		{
			return false;
		}

		if (replaceActive)
		{
			if (GetActiveSelectionOverlaySession(SelectionOverlayKind.QuestionRejudge) == null)
			{
				return false;
			}
		}
		else if (_activeSelectionOverlaySession != null)
		{
			return false;
		}

		ItemCard? rewardItem = CreateItemCard(ItemCardCatalog.OvertimeItemCardId)
			?? CreateItemCard(ItemCardCatalog.BombItemCardId);
		if (!rewardItem.HasValue)
		{
			return false;
		}

		ResetDebugAwaitProbeResult(result);
		_ = RunDebugAdventureQuestionRejudgeAwaitProbeAsync(rewardItem.Value, result);
		return true;
	}

	private async Task RunDebugAdventureQuestionRejudgeAwaitProbeAsync(
		ItemCard rewardItem,
		Godot.Collections.Dictionary result)
	{
		try
		{
			AdventureQuestionRejudgePromptResult prompt =
				await PromptAdventureQuestionRejudgeOverlayAsync(rewardItem);
			result["choice"] = prompt.Choice.ToString();
			result["rect_is_empty"] = prompt.PreviewGlobalRect == new Rect2();
			result["overlay_is_null"] = prompt.Overlay == null;
		}
		catch (Exception exception)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completion_count"] = 1;
			result["completed"] = true;
		}
	}

	public bool DebugDisposeActiveSelectionOverlayForAwaitProbe()
	{
		if (_activeSelectionOverlaySession == null)
		{
			return false;
		}

		DisposeActiveSelectionOverlay();
		return true;
	}

	public bool DebugCancelActiveSelectionOverlayForAwaitProbe()
	{
		if (_activeSelectionOverlaySession == null)
		{
			return false;
		}

		DismissActiveSelectionOverlay(invokeCancel: true);
		return true;
	}

	public bool DebugRestartMatchForSelectionAwaitProbe()
	{
		if (IsNetworkMultiplayerMode() || _activeSelectionOverlaySession == null)
		{
			return false;
		}

		DealCardsToPlayers(playOpeningDealAnimation: false);
		return true;
	}

	public bool DebugSelectAdventureQuestionRejudgeOption(int optionIndex)
	{
		return TrySelectActiveSelectionOverlayOption(
			SelectionOverlayKind.QuestionRejudge,
			optionIndex);
	}

	public int DebugGetAdventureUpgradeOptionCount()
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		return _adventureUpgradeSelectionCompletionSource != null && session != null
			? session.OptionCount
			: 0;
	}

	public bool DebugSelectAdventureUpgradeOption(int optionIndex)
	{
		if (_adventureUpgradeSelectionCompletionSource == null || optionIndex < 0)
		{
			return false;
		}

		return TrySelectActiveSelectionOverlayOption(SelectionOverlayKind.AdventureUpgrade, optionIndex);
	}

	public bool DebugSetAdventureUpgradeHoverState(int optionIndex, bool hovered)
	{
		SelectionOverlaySession? session = GetActiveSelectionOverlaySession(SelectionOverlayKind.AdventureUpgrade);
		if (session == null
			|| optionIndex < 0
			|| optionIndex >= session.OptionCount)
		{
			return false;
		}

		Button button = session.Root.GetNodeOrNull<Button>($"Margin/Layout/CardsRow/Option{optionIndex + 1}Button");
		if (button == null || !button.Visible)
		{
			return false;
		}

		SetAdventureUpgradeOverlayPointerHover(button, hovered);
		return true;
	}

	public int DebugGetAdventureAcquiredUpgradeCount()
	{
		return GetAdventureUpgradeRunState()?.GetAcquiredUpgradeIds().Count ?? 0;
	}

	public int DebugGetPendingAdventureUpgradeTriggerCount()
	{
		return _pendingAdventureUpgradeTriggers.Count;
	}

	public int DebugGetPendingAdventureWinnerPlayerIndex()
	{
		return _pendingAdventureWinnerPlayerIndex;
	}

	public bool DebugIsAdventureUpgradeResolverBusy()
	{
		return _isResolvingAdventureUpgradeSelection;
	}

	public int DebugGetPlayerScoreValue(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerScores.Length)
		{
			return -1;
		}

		return _playerScores[playerIndex];
	}

public int DebugGetPendingItemAttachmentRewardCount()
{
	return _pendingItemAttachmentRewards.Count;
}

public int DebugGetWinnerPlayerIndex()
{
	return _winnerPlayerIndex;
}

public int DebugGetNetworkAuthoritativeVisualSnapshotId()
{
	return _networkAuthoritativeVisualSnapshotId;
}

private bool _debugDragonCaptureProbeRunning;
private bool _debugCollectTableCaptureProbeRunning;

public bool DebugForceNextQuestionRewardItemCardId(
	string itemCardId)
{
	if (string.IsNullOrWhiteSpace(itemCardId)
		|| !_itemCardCatalog.TryGetDefinition(
			itemCardId,
			out ItemCardDefinition definition)
		|| !definition.CanBeQuestionReward
		|| !_itemCardCatalog.IsEnabled(itemCardId)
		|| !CreateItemCard(itemCardId).HasValue)
	{
		return false;
	}

	_debugForcedNextQuestionRewardItemCardId = itemCardId;
	return true;
}

public bool DebugConfigureDragonQuestionCaptureOwnershipProbe()
{
	if (_debugDragonCaptureProbeRunning
		|| !adventure_mode
		|| player_count < 2
		|| !AdventureEnemyHasAbility(
			AdventureEnemyAbilities.DragonPrince)
		|| HasPendingItemSelection())
	{
		return false;
	}

	var requiredCards = new (string Suit, string Rank)[]
	{
		("Clubs", "2"),
		("Diamonds", "3"),
		("Hearts", "4"),
		("Spades", "5"),
		("Clubs", "6"),
		("Spades", "A"),
	};
	int captureThreshold = GetAdventureEnemyAbilityValue(
		AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold,
		5);
	if (captureThreshold < 2
		|| captureThreshold > requiredCards.Length
		|| !TryLoadAdventureTutorialCards(
			requiredCards,
			out Dictionary<string, PlayingCard> cards))
	{
		return false;
	}

	ItemCard? questionMark =
		CreateItemCard(QuestionMarkItemCardId);
	if (!questionMark.HasValue
		|| 1 >= _playerHands.Count)
	{
		return false;
	}

	item_mode = true;
	_tableCards.Clear();
	for (int index = 0; index < captureThreshold - 1; index++)
	{
		(string suit, string rank) = requiredCards[index];
		_tableCards.Add(new TableCard(
			cards[$"{suit}:{rank}"],
			isFaceUp: true));
	}
	_tableCards.Add(new TableCard(
		null,
		isFaceUp: true,
		itemOverlay: questionMark));
	_playerHands[1].Clear();
	_playerHands[1].Add(cards["Spades:A"]);
	_pendingItemAttachmentRewards.Clear();
	_debugForcedNextQuestionRewardItemCardId = string.Empty;
	_dragonPrinceJackUsed = false;
	_winnerPlayerIndex = -1;
	RenderPlayerHands();
	RenderTableCards();
	return ShouldResolveDragonPrinceJack(1);
}

public bool DebugStartDragonQuestionCaptureOwnershipProbe(
	Godot.Collections.Dictionary result)
{
	if (result == null
		|| _debugDragonCaptureProbeRunning
		|| !ShouldResolveDragonPrinceJack(1))
	{
		return false;
	}

	result.Clear();
	result["completed"] = false;
	result["error"] = string.Empty;
	_debugDragonCaptureProbeRunning = true;
	_ = RunDebugDragonQuestionCaptureOwnershipProbeAsync(result);
	return true;
}

private async Task RunDebugDragonQuestionCaptureOwnershipProbeAsync(
	Godot.Collections.Dictionary result)
{
	try
	{
		PlayCardResult playResult =
			await ResolveDragonPrinceJackTurnStartAsync(
				1,
				_turnSequenceGeneration);
		result["played"] = playResult.Played;
		result["captured"] = playResult.Captured;
		result["completes_turn"] = playResult.CompletesTurn;
		result["reward_item_id"] =
			playResult.QuestionRewardAnimation.HasValue
				? playResult.QuestionRewardAnimation.Value
					.ItemCard.Id
				: string.Empty;
		result["pending_attachment_count"] =
			_pendingItemAttachmentRewards.Count;
		int enemyAttachmentCount = 0;
		foreach (PlayingCard card in _playerHands[1])
		{
			foreach (string attachedItemCardId in
				card.GetAttachedItemCardIds())
			{
				if (string.Equals(
					attachedItemCardId,
					MagnetItemCardId,
					StringComparison.Ordinal))
				{
					enemyAttachmentCount++;
					break;
				}
			}
		}
		result["enemy_magnet_attachment_count"] =
			enemyAttachmentCount;
	}
	catch (Exception exception)
	{
		result["error"] = exception.ToString();
	}
	finally
	{
		result["completed"] = true;
		_debugDragonCaptureProbeRunning = false;
	}
}

public bool DebugStartCollectTableCaptureOwnershipProbe(
	Godot.Collections.Dictionary result)
{
	if (result == null
		|| _debugCollectTableCaptureProbeRunning
		|| MainPlayerIndex >= _playerDecks.Count)
	{
		return false;
	}

	result.Clear();
	result["completed"] = false;
	result["error"] = string.Empty;
	_debugCollectTableCaptureProbeRunning = true;
	_ = RunDebugCollectTableCaptureOwnershipProbeAsync(result);
	return true;
}

private async Task RunDebugCollectTableCaptureOwnershipProbeAsync(
	Godot.Collections.Dictionary result)
{
	try
	{
		var request = new CardTriggerRequest(
			Definition: default!,
			Rule: default,
			Event: new CardGameEvent(
				"debug_collect_table_probe",
				MainPlayerIndex),
			Sequence: 0);
		PlayCardResult playResult =
			await ResolveCollectTableEffectAsync(
				request,
				_turnSequenceGeneration);
		result["played"] = playResult.Played;
		result["captured"] = playResult.Captured;
		result["deck_collected"] =
			playResult.DeckCollected;
	}
	catch (Exception exception)
	{
		result["error"] = exception.ToString();
	}
	finally
	{
		result["completed"] = true;
		_debugCollectTableCaptureProbeRunning = false;
	}
}

public bool DebugArmCaptureBeforeApplyBarrier()
{
	if (_debugCaptureBeforeApplyBarrier != null)
	{
		return false;
	}

	_debugCaptureBeforeApplyBarrierEntered = false;
	_debugCaptureBeforeApplyBarrier =
		new TaskCompletionSource<bool>();
	return true;
}

public bool DebugArmCaptureAfterApplyBarrier()
{
	if (_debugCaptureAfterApplyBarrier != null)
	{
		return false;
	}

	_debugCaptureAfterApplyBarrierEntered = false;
	_debugCaptureResultObservationArmed = true;
	_debugCaptureResultObserved = false;
	_debugCaptureResultApplied = false;
	_debugCaptureResultAborted = false;
	_debugCaptureAfterApplyBarrier =
		new TaskCompletionSource<bool>();
	return true;
}

public bool DebugReleaseCaptureBeforeApplyBarrier()
{
	TaskCompletionSource<bool>? completionSource =
		_debugCaptureBeforeApplyBarrier;
	if (completionSource == null)
	{
		return false;
	}

	_debugCaptureBeforeApplyBarrier = null;
	completionSource.TrySetResult(true);
	return true;
}

public bool DebugReleaseCaptureAfterApplyBarrier()
{
	TaskCompletionSource<bool>? completionSource =
		_debugCaptureAfterApplyBarrier;
	if (completionSource == null)
	{
		return false;
	}

	_debugCaptureAfterApplyBarrier = null;
	completionSource.TrySetResult(true);
	return true;
}

public bool DebugHasEnteredCaptureBeforeApplyBarrier()
{
	return _debugCaptureBeforeApplyBarrierEntered;
}

public bool DebugHasEnteredCaptureAfterApplyBarrier()
{
	return _debugCaptureAfterApplyBarrierEntered;
}

public bool DebugRestartMatchForCaptureOwnershipProbe()
{
	if (!_debugCaptureBeforeApplyBarrierEntered
		&& !_debugCaptureAfterApplyBarrierEntered)
	{
		return false;
	}

	int previousGeneration = _turnSequenceGeneration;
	DealCardsToPlayers(playOpeningDealAnimation: false);
	return _turnSequenceGeneration != previousGeneration;
}

public bool DebugReplaceTableCardReference(int tableCardIndex)
{
	if (tableCardIndex < 0
		|| tableCardIndex >= _tableCards.Count)
	{
		return false;
	}

	TableCard source = _tableCards[tableCardIndex];
	_tableCards[tableCardIndex] = new TableCard(
		source.Card,
		source.IsFaceUp,
		source.ItemOverlay,
		source.ItemOwnerPlayerIndex,
		source.CardOwnerPlayerIndex,
		source.RemoveCardOnCapture)
	{
		IsHiddenDuringCapture =
			source.IsHiddenDuringCapture,
		IsBuriedInSand = source.IsBuriedInSand,
		IsSlimeCoated = source.IsSlimeCoated,
	};
	return true;
}

public bool DebugWasCaptureOwnershipResultObserved()
{
	return _debugCaptureResultObserved;
}

public bool DebugGetObservedCaptureOwnershipApplied()
{
	return _debugCaptureResultApplied;
}

public bool DebugGetObservedCaptureOwnershipAborted()
{
	return _debugCaptureResultAborted;
}

public bool DebugConfigureMagnetGenerationOwnershipProbe()
{
	if (_isResolvingMagnetAttachmentSelection
		|| HasPendingItemSelection()
		|| MainPlayerIndex >= _playerHands.Count)
	{
		return false;
	}

	var requiredCards = new (string Suit, string Rank)[]
	{
		("Hearts", "5"),
	};
	if (!TryLoadAdventureTutorialCards(
		requiredCards,
		out Dictionary<string, PlayingCard> cards))
	{
		return false;
	}

	_playerHands[MainPlayerIndex].Clear();
	_playerHands[MainPlayerIndex].Add(cards["Hearts:5"]);
	_winnerPlayerIndex = -1;
	_isProcessingCardPlay = false;
	RenderPlayerHands();
	return PlayerHasAttachableMagnetCard(
		MainPlayerIndex,
		MagnetItemCardId);
}

public bool DebugStartMagnetGenerationOwnershipProbe(
	Godot.Collections.Dictionary result)
{
	if (result == null
		|| _isResolvingMagnetAttachmentSelection
		|| HasPendingItemSelection()
		|| !PlayerHasAttachableMagnetCard(
			MainPlayerIndex,
			MagnetItemCardId))
	{
		return false;
	}

	result.Clear();
	result["completed"] = false;
	result["error"] = string.Empty;
	result["generation"] = _turnSequenceGeneration;
	_ = RunDebugMagnetGenerationOwnershipProbeAsync(result);
	return true;
}

private async Task RunDebugMagnetGenerationOwnershipProbeAsync(
	Godot.Collections.Dictionary result)
{
	try
	{
		await ResolveHumanMagnetAttachmentAsync(
			MainPlayerIndex,
			MagnetItemCardId,
			_turnSequenceGeneration);
	}
	catch (Exception exception)
	{
		result["error"] = exception.ToString();
	}
	finally
	{
		result["completed"] = true;
	}
}

public bool DebugBeginMagnetGenerationOwnershipAnimation()
{
	if (_pendingMagnetAttachmentCompletion == null
		|| _pendingItemSelectionMode
			!= PendingItemSelectionMode.AttachmentRewardHandCard
		|| _isAnimatingMagnetAttachment)
	{
		return false;
	}

	_ = CompleteMagnetAttachmentSelectionAsync(0);
	return true;
}

public bool DebugIsMagnetGenerationOwnershipAnimationActive()
{
	return _isAnimatingMagnetAttachment;
}

public int DebugGetMagnetAttachmentAnimationNodeCount()
{
	int count = 0;
	foreach (Node child in GetChildren())
	{
		if (child.Name == "MagnetAttachmentCardAnimation"
			&& GodotObject.IsInstanceValid(child)
			&& !child.IsQueuedForDeletion())
		{
			count++;
		}
	}
	return count;
}

public bool DebugRestartMatchForMagnetGenerationOwnershipProbe()
{
	int previousGeneration = _turnSequenceGeneration;
	DealCardsToPlayers(playOpeningDealAnimation: false);
	return _turnSequenceGeneration != previousGeneration
		&& MainPlayerIndex < _playerHands.Count
		&& _playerHands[MainPlayerIndex].Count > 0;
}

public bool DebugHasCurrentOwnedMagnetSelection()
{
	TaskCompletionSource<bool>? completionSource =
		_pendingMagnetAttachmentCompletion;
	return completionSource != null
		&& OwnsPendingMagnetAttachmentSelection(
			completionSource,
			_pendingItemOwnerIndex,
			_pendingItemCardId,
			_pendingItemTurnSequenceGeneration);
}

public bool DebugIsProcessingCardPlayForOwnershipProbe()
{
	return _isProcessingCardPlay;
}

public int DebugCountMainPlayerHandCardsWithAttachment(
	string itemCardId)
{
	if (MainPlayerIndex >= _playerHands.Count)
	{
		return 0;
	}

	int count = 0;
	foreach (PlayingCard card in _playerHands[MainPlayerIndex])
	{
		foreach (string attachedItemCardId in
			card.GetAttachedItemCardIds())
		{
			if (string.Equals(
				attachedItemCardId,
				itemCardId,
				StringComparison.Ordinal))
			{
				count++;
				break;
			}
		}
	}
	return count;
}

public bool DebugCancelMagnetGenerationOwnershipProbe()
{
	if (_pendingMagnetAttachmentCompletion == null)
	{
		return false;
	}

	DisposeActiveSelectionOverlay();
	ClearPendingItemSelectionState();
	return true;
}

public bool DebugConfigureLeafyBladeGenerationOwnershipProbe()
{
	if (_debugLeafyBladeProbeRunning
		|| !adventure_mode
		|| player_count < 2
		|| !AdventureEnemyHasAbility(
			AdventureEnemyAbilities.LeafyMirage))
	{
		return false;
	}

	var requiredCards = new (string Suit, string Rank)[]
	{
		("Clubs", "2"),
		("Diamonds", "3"),
		("Hearts", "4"),
		("Spades", "5"),
		("Clubs", "6"),
		("Diamonds", "7"),
	};
	int requiredTableCardCount = GetAdventureEnemyAbilityValue(
		AdventureEnemyAbilityValueKeys.BladeBreakTableCardThreshold,
		5);
	if (requiredTableCardCount <= 0
		|| requiredTableCardCount > requiredCards.Length
		|| !TryLoadAdventureTutorialCards(
			requiredCards,
			out Dictionary<string, PlayingCard> cards))
	{
		return false;
	}

	_tableCards.Clear();
	for (int index = 0; index < requiredTableCardCount; index++)
	{
		(string suit, string rank) = requiredCards[index];
		_tableCards.Add(new TableCard(
			cards[$"{suit}:{rank}"],
			isFaceUp: true));
	}
	_leafySeadragonBladeBreakCooldownTurns = 0;
	_winnerPlayerIndex = -1;
	_debugLeafyBladeTargetTableCardIndex =
		PickLeafySeadragonBladeBreakTargetIndex();
	_debugLeafyBladeReplacementTableCard = null;
	_debugLeafyBladeReplacementHiddenSentinel = false;
	SetAdventureTraceHuntMarkedTableCard(
		null,
		playIntro: false);
	RenderTableCards();
	return _debugLeafyBladeTargetTableCardIndex >= 0
		&& ShouldResolveLeafySeadragonBladeBreak(
			1,
			new PlayCardResult(true, false));
}

public bool DebugStartLeafyBladeGenerationOwnershipProbe(
	Godot.Collections.Dictionary result)
{
	var playResult = new PlayCardResult(true, false);
	if (result == null
		|| _debugLeafyBladeProbeRunning
		|| _debugLeafyBladeTargetTableCardIndex < 0
		|| !ShouldResolveLeafySeadragonBladeBreak(
			1,
			playResult))
	{
		return false;
	}

	result.Clear();
	result["completed"] = false;
	result["error"] = string.Empty;
	result["generation"] = _turnSequenceGeneration;
	_debugLeafyBladeProbeRunning = true;
	_ = RunDebugLeafyBladeGenerationOwnershipProbeAsync(
		playResult,
		result);
	return true;
}

private async Task RunDebugLeafyBladeGenerationOwnershipProbeAsync(
	PlayCardResult playResult,
	Godot.Collections.Dictionary result)
{
	try
	{
		await ResolveLeafySeadragonBladeBreakAsync(
			1,
			playResult,
			_turnSequenceGeneration);
	}
	catch (Exception exception)
	{
		result["error"] = exception.ToString();
	}
	finally
	{
		result["completed"] = true;
		_debugLeafyBladeProbeRunning = false;
	}
}

public bool DebugHasLeafyBladeProjectilePhase()
{
	Node? projectile = FindChild(
		"LeafySeadragonBladeProjectile",
		recursive: true,
		owned: false);
	return projectile != null
		&& GodotObject.IsInstanceValid(projectile);
}

public bool DebugHasLeafyBladeBombPhase()
{
	Node? bombFx = FindChild(
		"BombPairDestroyFx",
		recursive: true,
		owned: false);
	return bombFx != null
		&& GodotObject.IsInstanceValid(bombFx);
}

public bool DebugRestartMatchForLeafyBladeOwnershipProbe()
{
	if (!_debugLeafyBladeProbeRunning)
	{
		return false;
	}

	int previousGeneration = _turnSequenceGeneration;
	DealCardsToPlayers(playOpeningDealAnimation: false);
	return _turnSequenceGeneration != previousGeneration;
}

public bool DebugReplaceLeafyBladeTargetReference()
{
	int tableCardIndex = _debugLeafyBladeTargetTableCardIndex;
	if (!_debugLeafyBladeProbeRunning
		|| tableCardIndex < 0
		|| tableCardIndex >= _tableCards.Count)
	{
		return false;
	}

	TableCard source = _tableCards[tableCardIndex];
	_debugLeafyBladeReplacementHiddenSentinel =
		source.IsHiddenDuringCapture;
	_debugLeafyBladeReplacementTableCard = new TableCard(
		source.Card,
		source.IsFaceUp,
		source.ItemOverlay,
		source.ItemOwnerPlayerIndex,
		source.CardOwnerPlayerIndex,
		source.RemoveCardOnCapture)
	{
		IsBuriedInSand = source.IsBuriedInSand,
		IsSlimeCoated = source.IsSlimeCoated,
		IsHiddenDuringCapture =
			_debugLeafyBladeReplacementHiddenSentinel,
	};
	_tableCards[tableCardIndex] =
		_debugLeafyBladeReplacementTableCard;
	return true;
}

public bool DebugLeafyBladeReplacementIsIntact()
{
	int tableCardIndex = _debugLeafyBladeTargetTableCardIndex;
	return _debugLeafyBladeReplacementTableCard != null
		&& tableCardIndex >= 0
		&& tableCardIndex < _tableCards.Count
		&& ReferenceEquals(
			_tableCards[tableCardIndex],
			_debugLeafyBladeReplacementTableCard)
		&& _debugLeafyBladeReplacementTableCard
			.IsHiddenDuringCapture
			== _debugLeafyBladeReplacementHiddenSentinel;
}

public int DebugGetLeafyBladeBreakCooldownTurns()
{
	return _leafySeadragonBladeBreakCooldownTurns;
}

public bool DebugMarkLeafyBladeTargetForTraceHunt()
{
	int tableCardIndex = _debugLeafyBladeTargetTableCardIndex;
	if (tableCardIndex < 0
		|| tableCardIndex >= _tableCards.Count)
	{
		return false;
	}

	SetAdventureTraceHuntMarkedTableCard(
		_tableCards[tableCardIndex],
		playIntro: false);
	RenderTableCards();
	return GetAdventureTraceHuntMarkedTableCardIndex()
		== tableCardIndex;
}

	private Texture2D? GetDebugFrameAnimationProbeTexture()
	{
		if (_cardBackTexture != null)
		{
			return _cardBackTexture;
		}
		foreach (TableCard tableCard in _tableCards)
		{
			if (tableCard.Card.HasValue) return tableCard.Card.Value.Texture;
			if (tableCard.ItemOverlay.HasValue) return tableCard.ItemOverlay.Value.Texture;
		}
		foreach (List<PlayingCard> hand in _playerHands)
		{
			if (hand.Count > 0) return hand[0].Texture;
		}
		return null;
	}

	public bool DebugHasFrameAnimationParentTweenProbeFrames()
	{
		EnsureCommonGameplayFxResourcesLoaded();
		EnsureItemModeOptionalFxResourcesLoaded();
		return _specialCardTriggerActivationFrames.Count > 1
			&& (_specialCardTriggerEffectFrames.Count > 1 || _enemySpecialAbilityAvatarFxFrames.Count > 1)
			&& _tableShuffleVortexFrames.Count > 1;
	}

	public bool DebugStartSpecialCardTriggerTimelineProbe(Godot.Collections.Dictionary result)
	{
		Texture2D? texture = GetDebugFrameAnimationProbeTexture();
		if (result == null || texture == null || _debugSpecialCardProbeRunning || !IsInsideTree()) return false;
		result.Clear();
	result["completed"] = false;
	result["error"] = string.Empty;
	_debugSpecialCardProbeRunning = true;
	_debugSpecialCardProbeTween = null;
	_debugSpecialCardProbeEffectRoot = null;
	_debugSpecialCardProbeElapsedSeconds = 0.0;
	_ = RunDebugSpecialCardTriggerTimelineProbeAsync(texture, result);
		return true;
	}

	private async Task RunDebugSpecialCardTriggerTimelineProbeAsync(Texture2D texture, Godot.Collections.Dictionary result)
	{
		try
		{
			await PlaySpecialCardTriggerEffectAsync(texture, _centerArena.GlobalPosition, zIndex: 0);
		}
		catch (Exception exception)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			_debugSpecialCardProbeElapsedSeconds = GetDebugSpecialCardProbeTweenElapsedSeconds();
			result["elapsed_seconds"] = _debugSpecialCardProbeElapsedSeconds;
			result["completed"] = true;
			_debugSpecialCardProbeRunning = false;
		}
	}

	private double GetDebugSpecialCardProbeTweenElapsedSeconds()
	{
		if (_debugSpecialCardProbeTween != null
			&& _debugSpecialCardProbeTween.IsValid())
		{
			return _debugSpecialCardProbeTween.GetTotalElapsedTime();
		}

		return _debugSpecialCardProbeElapsedSeconds;
	}

	public double DebugGetSpecialCardTriggerProbeElapsedSeconds() =>
		_debugSpecialCardProbeRunning
			? GetDebugSpecialCardProbeTweenElapsedSeconds()
			: _debugSpecialCardProbeElapsedSeconds;

	public float DebugGetSpecialCardTriggerProbeScaleX()
	{
		Control? effectRoot = _debugSpecialCardProbeEffectRoot;
		return effectRoot != null
			&& GodotObject.IsInstanceValid(effectRoot)
			? effectRoot.Scale.X
			: -1.0f;
	}

	public bool DebugConfigureTableShuffleOwnershipProbe()
	{
		if (_debugTableShuffleProbeRunning || !IsInsideTree()) return false;
		var requiredCards = new (string Suit, string Rank)[] { ("Clubs", "2"), ("Diamonds", "7"), ("Hearts", "5") };
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards)) return false;
		DebugReleaseTableShuffleVortexReuseSentinel();
		var originallyHidden = new TableCard(cards["Clubs:2"], true) { IsHiddenDuringCapture = true };
		var fixedCard = new TableCard(cards["Diamonds:7"], false) { IsBuriedInSand = true, IsHiddenDuringCapture = false };
		var ordinaryCard = new TableCard(cards["Hearts:5"], true) { IsHiddenDuringCapture = false };
		_tableCards.Clear();
		_tableCards.Add(originallyHidden);
		_tableCards.Add(fixedCard);
		_tableCards.Add(ordinaryCard);
		_winnerPlayerIndex = -1;
		_debugTableShuffleOriginalCards = new[] { originallyHidden, fixedCard, ordinaryCard };
		_debugTableShuffleOriginalHiddenStates = new[] { true, false, false };
		_debugTableShuffleSentinelCards = Array.Empty<TableCard>();
		_debugTableShuffleSentinelHiddenStates = Array.Empty<bool>();
		_debugTableShuffleVortexInstanceId = 0;
		RenderTableCards();
		return true;
	}

	public bool DebugStartTableShuffleOwnershipProbe(string caller, Godot.Collections.Dictionary result)
	{
		if (result == null || _debugTableShuffleProbeRunning || _tableCards.Count != 3 || (caller != "effect" && caller != "adventure")) return false;
		result.Clear();
		result["completed"] = false;
		result["caller_succeeded"] = false;
		result["error"] = string.Empty;
		_debugTableShuffleProbeRunning = true;
		_debugTableShuffleProbeStartedUsec = Time.GetTicksUsec();
		_ = RunDebugTableShuffleOwnershipProbeAsync(caller, result);
		return true;
	}

	private async Task RunDebugTableShuffleOwnershipProbeAsync(string caller, Godot.Collections.Dictionary result)
	{
		try
		{
			bool succeeded;
			if (caller == "effect")
			{
				PlayCardResult playResult = await ResolveShuffleTableEffectAsync(default, _turnSequenceGeneration);
				succeeded = playResult.Played;
			}
			else
			{
				succeeded = await ApplyAdventureMainPlayerMissTableEffectsAsync(
					new AdventureMainPlayerMissEffectResult(false, false, true, false), 0, _turnSequenceGeneration);
			}
			result["caller_succeeded"] = succeeded;
		}
		catch (Exception exception)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completed"] = true;
			_debugTableShuffleProbeRunning = false;
		}
	}

	private static TableCard CloneDebugTableShuffleCard(TableCard source) => new(
		source.Card, source.IsFaceUp, source.ItemOverlay, source.ItemOwnerPlayerIndex,
		source.CardOwnerPlayerIndex, source.RemoveCardOnCapture)
	{
		IsBuriedInSand = source.IsBuriedInSand,
		IsSlimeCoated = source.IsSlimeCoated,
		IsHiddenDuringCapture = source.IsHiddenDuringCapture,
	};

	public double DebugGetTableShuffleOwnershipProbeElapsedSeconds() => _debugTableShuffleProbeStartedUsec == 0 ? 0.0 : (Time.GetTicksUsec() - _debugTableShuffleProbeStartedUsec) / 1_000_000.0;

	public bool DebugIsTableShuffleGatherProbeActive()
	{
		TextureRect? vortex = GetNodeOrNull<TextureRect>("TableShuffleVortex");
		return _debugTableShuffleProbeRunning && DebugGetTableShuffleOwnershipProbeElapsedSeconds() < TableShuffleGatherSeconds && vortex != null && GodotObject.IsInstanceValid(vortex);
	}

	public bool DebugInjectTableShuffleOwnershipLoss(bool advanceGeneration, bool replaceAllReferences)
	{
		TextureRect? vortex = GetNodeOrNull<TextureRect>("TableShuffleVortex");
		if (!DebugIsTableShuffleGatherProbeActive() || vortex == null) return false;
		_debugTableShuffleVortexInstanceId = vortex.GetInstanceId();
		if (replaceAllReferences)
		{
			for (int index = 0; index < _tableCards.Count; index++) _tableCards[index] = CloneDebugTableShuffleCard(_tableCards[index]);
		}
		else _tableCards[0] = CloneDebugTableShuffleCard(_tableCards[0]);
		_debugTableShuffleSentinelCards = new TableCard[_tableCards.Count];
		_tableCards.CopyTo(_debugTableShuffleSentinelCards);
		_debugTableShuffleSentinelHiddenStates = new bool[_tableCards.Count];
		for (int index = 0; index < _tableCards.Count; index++)
		{
			bool hiddenSentinel = index == 1;
			_tableCards[index].IsHiddenDuringCapture = hiddenSentinel;
			_debugTableShuffleSentinelHiddenStates[index] = hiddenSentinel;
		}
		_tableRenderSignatureInitialized = true;
		_tableRenderSignature = DebugTableShuffleRenderSignatureSentinel;
		if (advanceGeneration) AdvanceTurnSequenceGeneration();
		return true;
	}

	public bool DebugTableShuffleProbeRetainedSentinelState()
	{
		if (_debugTableShuffleSentinelCards.Length == 0 || _debugTableShuffleSentinelCards.Length != _tableCards.Count) return false;
		for (int index = 0; index < _tableCards.Count; index++)
		{
			if (!ReferenceEquals(_debugTableShuffleSentinelCards[index], _tableCards[index]) || _tableCards[index].IsHiddenDuringCapture != _debugTableShuffleSentinelHiddenStates[index]) return false;
		}
		return true;
	}

	public bool DebugTableShuffleRenderSentinelIsUnchanged() => _tableRenderSignatureInitialized && _tableRenderSignature == DebugTableShuffleRenderSignatureSentinel;

	public bool DebugTableShuffleOriginalHiddenStatesAreRestored()
	{
		if (_debugTableShuffleOriginalCards.Length == 0 || _debugTableShuffleOriginalCards.Length != _debugTableShuffleOriginalHiddenStates.Length) return false;
		for (int index = 0; index < _debugTableShuffleOriginalCards.Length; index++)
		{
			if (_debugTableShuffleOriginalCards[index].IsHiddenDuringCapture != _debugTableShuffleOriginalHiddenStates[index]) return false;
		}
		return true;
	}

	public bool DebugRentTableShuffleVortexReuseSentinel()
	{
		if (_debugTableShuffleProbeRunning || _debugTableShuffleVortexInstanceId == 0) return false;
		DebugReleaseTableShuffleVortexReuseSentinel();
		Texture2D? sentinel = GetDebugFrameAnimationProbeTexture();
		if (sentinel == null) return false;
		foreach (Texture2D frame in _tableShuffleVortexFrames)
		{
			if (ReferenceEquals(frame, sentinel)) return false;
		}
		_debugTableShuffleReuseSentinelTexture = sentinel;
		_debugTableShuffleReusedVortex = _animationPresenter.RentTextureRect(this, "DebugTableShuffleVortexReuseSentinel", sentinel, new Vector2(32.0f, 32.0f), PlayedCardFlightZIndex + 500, CanvasItem.TextureFilterEnum.Nearest, zAsRelative: false);
		return _debugTableShuffleReusedVortex.GetInstanceId() == _debugTableShuffleVortexInstanceId;
	}

	public bool DebugTableShuffleVortexReuseSentinelIsIntact() => _debugTableShuffleReusedVortex != null && GodotObject.IsInstanceValid(_debugTableShuffleReusedVortex) && ReferenceEquals(_debugTableShuffleReusedVortex.Texture, _debugTableShuffleReuseSentinelTexture);

	public void DebugReleaseTableShuffleVortexReuseSentinel()
	{
		if (_debugTableShuffleReusedVortex != null && GodotObject.IsInstanceValid(_debugTableShuffleReusedVortex)) _animationPresenter.Release(_debugTableShuffleReusedVortex);
		_debugTableShuffleReusedVortex = null;
		_debugTableShuffleReuseSentinelTexture = null;
	}

}
