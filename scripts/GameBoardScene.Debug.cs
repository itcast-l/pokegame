using System;
using Godot;

public partial class GameBoardScene
{
	private const string DebugPendingTargetHandSelectionItemCardId = "Q";
	private bool _debugForceNextPendingTargetPlayerHeuristicFailure;
	private bool _debugForceNextPendingTargetHandHeuristicFailure;
	private bool _debugForceNextPendingHandLimitDiscardHeuristicFailure;
	private bool _debugForceNextAdventureUpgradeOverlayDisplayFailure;
	private double _debugNetworkVisualPlaybackDelaySeconds;
	private int _debugQuestionRewardGrantCount;
	private string _debugLastQuestionRewardItemCardId = string.Empty;

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

		RecordAdventureAcquiredUpgrade(upgradeId);
		ApplyAdventureUpgradeAcquisitionEffects(upgradeId, AdventureUpgradeTriggerSource.None);
		return true;
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
			TryGrantAdventureMainPlayerExtraBaseCardAction(keepsTurnAfterAction: true);
		}

		if (effectResult.DirectDamageToEnemy > 0)
		{
			AddPlayerScore(MainPlayerIndex, effectResult.DirectDamageToEnemy);
		}

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

		AdventureMainPlayerMissEffectResult result = AdventureUpgradeEffectService.OnMainPlayerMissResolved(
			runState.EffectState,
			runState.GetAcquiredUpgradeIds(),
			tableCardCountAfterMiss);
		store!.ReplaceEffectState(runState.EffectState.Clone());
		if (result.GrantExtraBaseCardAction)
		{
			TryGrantAdventureMainPlayerExtraBaseCardAction();
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
		return _adventureMainPlayerPendingExtraBaseCardAction;
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
		return _networkVisualQueueProcessing;
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

	private bool ConsumeDebugForceNextPendingTargetPlayerHeuristicFailure()
	{
		if (!_debugForceNextPendingTargetPlayerHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingTargetPlayerHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextPendingTargetHandHeuristicFailure()
	{
		if (!_debugForceNextPendingTargetHandHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingTargetHandHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextPendingHandLimitDiscardHeuristicFailure()
	{
		if (!_debugForceNextPendingHandLimitDiscardHeuristicFailure)
		{
			return false;
		}

		_debugForceNextPendingHandLimitDiscardHeuristicFailure = false;
		return true;
	}

	private bool ConsumeDebugForceNextAdventureUpgradeOverlayDisplayFailure()
	{
		if (!_debugForceNextAdventureUpgradeOverlayDisplayFailure)
		{
			return false;
		}

		_debugForceNextAdventureUpgradeOverlayDisplayFailure = false;
		return true;
	}

	private double GetDebugNetworkVisualPlaybackDelaySeconds()
	{
		return _debugNetworkVisualPlaybackDelaySeconds;
	}
}
