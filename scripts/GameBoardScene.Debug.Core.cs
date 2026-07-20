using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private int _debugAdventureSaveGateProbeAttemptCount;
	private bool _debugAdventureSaveGateProbeCompleted;
	private bool _debugAdventureDirectDamageProbeRunning;
	private bool _debugAdventureDirectDamageProbeCompleted;
	private bool _debugAdventureDirectDamageProbeApplied;
	private bool _debugAdventureRealTurnEndProbeRunning;
	private bool _debugAdventureRealTurnEndProbeCompleted;
	private bool _debugAdventureRealTurnEndProbeSucceeded;

	public bool DebugStartAdventureSaveGateProbe(int failuresBeforeSuccess)
	{
		if (_adventureSaveInProgress || _debugAdventureSaveGateProbeCompleted)
		{
			return false;
		}

		_debugAdventureSaveGateProbeAttemptCount = 0;
		TrackBoardTask(RunDebugAdventureSaveGateProbeAsync(Math.Max(0, failuresBeforeSuccess)), nameof(RunDebugAdventureSaveGateProbeAsync));
		return true;
	}

	private async Task RunDebugAdventureSaveGateProbeAsync(int failuresBeforeSuccess)
	{
		AdventureSaveGateResult<AdventureRunWriteResult> gate = await ExecuteAdventureSaveGateAsync(
			() => ++_debugAdventureSaveGateProbeAttemptCount > failuresBeforeSuccess
				? AdventureRunWriteResult.Succeeded
				: AdventureRunWriteResult.SaveFailed,
			result => result == AdventureRunWriteResult.Succeeded,
			result => result == AdventureRunWriteResult.SaveFailed);
		_debugAdventureSaveGateProbeCompleted = gate.Succeeded;
	}

	public bool DebugIsAdventureSaveInProgress()
	{
		return _adventureSaveInProgress;
	}

	public int DebugGetAdventureSaveGateProbeAttemptCount()
	{
		return _debugAdventureSaveGateProbeAttemptCount;
	}

	public string DebugGetAdventureSaveStatusKey()
	{
		return _adventureSaveStatusLocalizationKey;
	}

	public bool DebugConfigureAdventurePersistFailures(int failureCount)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null || failureCount < 0 || IsAdventureSaveGateBlocking())
		{
			return false;
		}

		store.ConfigurePersistFailuresForDebug(failureCount);
		return true;
	}

	public int DebugGetAdventurePersistAttemptCount()
	{
		return GetAdventureRunSessionStore()?.GetPersistAttemptCountForDebug() ?? 0;
	}

	public bool DebugConfigureAdventureNextStageWriteAsInvalid()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store == null || IsAdventureSaveGateBlocking())
		{
			return false;
		}

		store.ConfigureNextTransactionalWriteResultForDebug(
			AdventureRunWriteResult.InvalidStage);
		return true;
	}

	public bool DebugResetAdventureRunToFirstStage()
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		return store != null
			&& !IsAdventureSaveGateBlocking()
			&& store.BeginNewMainRun(
				1,
				AdventureCampaignCatalog.GetMainAvatarId(1))
				== AdventureRunWriteResult.Succeeded;
	}

	public int DebugGetAdventureRunMainStageIndex()
	{
		return GetAdventureRunSessionStore()?.GetRunState().MainStageIndex ?? 0;
	}

	public bool DebugHasAdventureNextStageWriteOverride()
	{
		return GetAdventureRunSessionStore()
			?.HasNextTransactionalWriteResultForDebug() == true;
	}

	public bool DebugStartAdventureDirectDamageSaveGateProbe(int damageAmount)
	{
		if (damageAmount <= 0
			|| _debugAdventureDirectDamageProbeRunning
			|| IsAdventureSaveGateBlocking())
		{
			return false;
		}

		_debugAdventureDirectDamageProbeRunning = true;
		_debugAdventureDirectDamageProbeCompleted = false;
		_debugAdventureDirectDamageProbeApplied = false;
		TrackBoardTask(RunDebugAdventureDirectDamageSaveGateProbeAsync(damageAmount), nameof(RunDebugAdventureDirectDamageSaveGateProbeAsync));
		return true;
	}

	private async Task RunDebugAdventureDirectDamageSaveGateProbeAsync(int damageAmount)
	{
		try
		{
			AdventureDirectDamageSceneCommit damageCommit =
				await CommitAdventureDirectDamageAsync(damageAmount);
			if (!damageCommit.Succeeded)
			{
				return;
			}

			int turnSequenceGeneration = _turnSequenceGeneration;
			await PlayAdventureAvatarAttackAsync(
				AdventureEnemyPlayerIndex,
				MainPlayerIndex,
				turnSequenceGeneration,
				() =>
				{
					ApplyCommittedAdventureDirectDamage(
						AdventureEnemyPlayerIndex,
						damageCommit.CommitResult);
					_debugAdventureDirectDamageProbeApplied = true;
				});
		}
		finally
		{
			_debugAdventureDirectDamageProbeRunning = false;
			_debugAdventureDirectDamageProbeCompleted = true;
		}
	}

	public bool DebugIsAdventureDirectDamageSaveGateProbeCompleted()
	{
		return _debugAdventureDirectDamageProbeCompleted;
	}

	public bool DebugWasAdventureDirectDamageSaveGateProbeApplied()
	{
		return _debugAdventureDirectDamageProbeApplied;
	}

	public bool DebugStartAdventureRealTurnEndSaveGateProbe()
	{
		if (_debugAdventureRealTurnEndProbeRunning
			|| IsAdventureSaveGateBlocking()
			|| _currentTurnPlayerIndex != MainPlayerIndex)
		{
			return false;
		}

		_state.AdventureRealMainPlayerTurnActive = true;
		_state.AdventureRealMainPlayerTurnStartPending = false;
		_debugAdventureRealTurnEndProbeRunning = true;
		_debugAdventureRealTurnEndProbeCompleted = false;
		_debugAdventureRealTurnEndProbeSucceeded = false;
		TrackBoardTask(RunDebugAdventureRealTurnEndSaveGateProbeAsync(), nameof(RunDebugAdventureRealTurnEndSaveGateProbeAsync));
		return true;
	}

	private async Task RunDebugAdventureRealTurnEndSaveGateProbeAsync()
	{
		try
		{
			_debugAdventureRealTurnEndProbeSucceeded =
				await SetCurrentTurnPlayerIndexAsync(AdventureEnemyPlayerIndex);
		}
		finally
		{
			_debugAdventureRealTurnEndProbeRunning = false;
			_debugAdventureRealTurnEndProbeCompleted = true;
		}
	}

	public bool DebugIsAdventureRealTurnEndSaveGateProbeCompleted()
	{
		return _debugAdventureRealTurnEndProbeCompleted;
	}

	public bool DebugDidAdventureRealTurnEndSaveGateProbeSucceed()
	{
		return _debugAdventureRealTurnEndProbeSucceeded;
	}

	public bool DebugCanMainPlayerAct()
	{
		return CanMainPlayerAct();
	}
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

		int turnGeneration = _turnSequenceGeneration;
		BeginNetworkAuthorityAction(
			AuthorityActionKind.QuestionReward,
			ownerPlayerIndex,
			turnGeneration,
			clientActionId: 0,
			itemOrRewardId: "question_mark");
		MarkNetworkAuthorityActionApplying();
		TrackBoardTask(RunDebugPendingHandLimitDiscardAsync(ownerPlayerIndex, turnGeneration), nameof(RunDebugPendingHandLimitDiscardAsync));
		return true;
	}

	private async Task RunDebugPendingHandLimitDiscardAsync(
		int ownerPlayerIndex,
		int turnGeneration)
	{
		bool discarded = false;
		try
		{
			discarded = await ResolveHumanHandLimitDiscardAsync(ownerPlayerIndex, turnGeneration);
			if (!discarded
				&& turnGeneration == _turnSequenceGeneration
				&& !IsGameOver())
			{
				await ResolveActionCompletionAsync(
					ownerPlayerIndex,
					new PlayCardResult(false, false, CompletesTurn: false));
			}
		}
		finally
		{
			if (_networkAuthorityActionState.IsActive
				&& _networkAuthorityActionState.ActorSeat == ownerPlayerIndex)
			{
				CompleteNetworkAuthorityAction();
			}
			_isProcessingCardPlay = false;
			RenderPlayerHands();
			SendBoardSnapshotIfHost();
		}
	}

	public string DebugGetPendingItemSelectionModeName()
	{
		return _pendingItemSelectionMode.ToString();
	}

	public void DebugForceNextRequiredItemSelectionOverlayDisplayFailure()
	{
		_debugForceNextRequiredItemSelectionOverlayDisplayFailure = true;
	}

	public void DebugForceNextRequiredItemSelectionOverlayResourceFailure()
	{
		_debugForceNextRequiredItemSelectionOverlayDisplayFailure = true;
	}

	public void DebugForceNextRequiredItemSelectionOverlayInstantiationFailure()
	{
		_debugForceNextRequiredItemSelectionOverlayInstantiationFailure = true;
	}

	public bool DebugForcePendingRequiredSelectionZeroCandidateRecovery()
	{
		if (!IsNetworkHost()
			|| _pendingItemSelectionMode != PendingItemSelectionMode.HandLimitDiscard
			|| _pendingItemOwnerIndex < 0
			|| _pendingItemOwnerIndex >= player_count
			|| _pendingHandLimitDiscardCompletion == null)
		{
			return false;
		}

		int ownerPlayerIndex = _pendingItemOwnerIndex;
		_playerHands[ownerPlayerIndex].Clear();
		_playerDecks[ownerPlayerIndex].Clear();
		_playerItemHands[ownerPlayerIndex].Clear();
		RenderPlayerHands();
		_pendingHandLimitDiscardCompletion.TrySetResult(-1);
		return true;
	}

	public bool DebugUnexpectedlyReleaseActiveSelectionOverlay()
	{
		SelectionOverlaySession? session = _activeSelectionOverlaySession;
		if (session == null || !IsInstanceValid(session.Root))
		{
			return false;
		}

		session.Root.QueueFree();
		return true;
	}

	public bool DebugReleaseOpeningDealMigrationFault()
	{
		TaskCompletionSource<bool>? waiter = _debugOpeningDealFaultWaiter;
		if (waiter == null || waiter.Task.IsCompleted)
		{
			return false;
		}

		waiter.TrySetResult(true);
		return true;
	}

	public bool DebugDidOpeningDealFaultCallbackReturn()
	{
		return _debugOpeningDealFaultReleasedCallbackReturned;
	}

	public bool DebugCompletePendingHandLimitDiscard(int displayIndex)
	{
		if (_pendingItemSelectionMode != PendingItemSelectionMode.HandLimitDiscard
			|| !IsValidHandLimitDiscardSelection(
				_pendingItemOwnerIndex,
				displayIndex))
		{
			return false;
		}

		CompleteHandLimitDiscardSelection(displayIndex);
		return true;
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

	public bool DebugPlayFirstNonCapturingNetworkHostHandCard()
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
			|| localPlayerIndex >= _playerHands.Count)
		{
			return false;
		}

		for (int handIndex = 0; handIndex < _playerHands[localPlayerIndex].Count; handIndex++)
		{
			CardPlayPlan plan = ResolveCardPlayPlan(_playerHands[localPlayerIndex][handIndex]);
			if (plan.CaptureStartIndex >= 0)
			{
				continue;
			}

			return TryStartNetworkHandCardPlayFromHost(
				localPlayerIndex,
				handIndex,
				_turnSequenceGeneration,
				out _);
		}

		return false;
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

	public Godot.Collections.Dictionary DebugProbeAdventureEnemyLowTableAi(
		bool includeCaptureCandidate,
		int tableCardCount,
		int decisionRollPercent,
		int randomCandidateOrdinal)
	{
		var result = new Godot.Collections.Dictionary
		{
			["eligible"] = false,
			["kind"] = EnemyAiRules.AdventureSelectionKind.None.ToString(),
			["hand_index"] = -1,
		};
		if (tableCardCount < 0
			|| tableCardCount > 4
			|| _playerHands.Count <= 1
			|| _playerDecks.Count <= 1
			|| _playerItemHands.Count <= 1)
		{
			return result;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "2"),
			("Spades", "3"),
			("Diamonds", "4"),
			("Clubs", "5"),
			("Hearts", "Q"),
			("Clubs", "K"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return result;
		}

		_tableCards.Clear();
		string[] tableCardKeys = { "Hearts:2", "Spades:3", "Diamonds:4", "Clubs:5" };
		for (int index = 0; index < tableCardCount; index++)
		{
			_tableCards.Add(new TableCard(cards[tableCardKeys[index]], true));
		}

		_playerHands[1].Clear();
		_playerItemHands[1].Clear();
		_playerHands[1].Add(includeCaptureCandidate && tableCardCount > 0
			? cards[tableCardKeys[0]]
			: cards["Hearts:Q"]);
		_playerHands[1].Add(cards["Clubs:K"]);

		bool eligible = TrySelectMainAdventureEnemyCard(
			1,
			decisionRollPercent,
			randomCandidateOrdinal,
			out EnemyAiRules.AdventureSelectionResult selection);
		result["eligible"] = eligible;
		result["kind"] = selection.Kind.ToString();
		result["hand_index"] = selection.HandIndex;
		return result;
	}

	public bool DebugRestartWithoutOpeningDealAnimation()
	{
		if (!IsInsideTree()
			|| _playerCardPiles.Count != MaxPlayers
			|| _playerHandAreas.Count != MaxPlayers)
		{
			return false;
		}

		if (!DebugPrepareAdventureBattleForDeal())
		{
			return false;
		}
		DealCardsToPlayers(playOpeningDealAnimation: false);
		return !_isOpeningDealAnimationPlaying;
	}

}
