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
	private bool _debugAdventureSavePresentationProbeRunning;
	private bool _debugAdventureSavePresentationProbeCompleted;
	private bool _debugAdventureDirectDamageProbeRunning;
	private bool _debugAdventureDirectDamageProbeCompleted;
	private bool _debugAdventureDirectDamageProbeApplied;
	private bool _debugAdventureRealTurnEndProbeRunning;
	private bool _debugAdventureRealTurnEndProbeCompleted;
	private bool _debugAdventureRealTurnEndProbeSucceeded;

	public bool DebugStartAdventureSaveGateProbe(int failuresBeforeSuccess)
	{
		if (AdventureSaveInProgress || _debugAdventureSaveGateProbeCompleted)
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
		return AdventureSaveInProgress;
	}

	public int DebugGetAdventureSaveGateProbeAttemptCount()
	{
		return _debugAdventureSaveGateProbeAttemptCount;
	}

	public string DebugGetAdventureSaveStatusKey()
	{
		return AdventureSaveStatusLocalizationKey;
	}

	public bool DebugStartAdventureSavePresentationProbe()
	{
		if (_debugAdventureSavePresentationProbeRunning || IsAdventureSaveGateBlocking())
		{
			return false;
		}

		_debugAdventureSavePresentationProbeRunning = true;
		_debugAdventureSavePresentationProbeCompleted = false;
		TrackBoardTask(
			RunDebugAdventureSavePresentationProbeAsync(),
			nameof(RunDebugAdventureSavePresentationProbeAsync));
		return true;
	}

	private async Task RunDebugAdventureSavePresentationProbeAsync()
	{
		try
		{
			await AdventureSaveGate.ExecuteAsync(
				async () =>
				{
					await WaitForBoardDelayAsync(0.2);
					return AdventureRunWriteResult.Succeeded;
				},
				result => result == AdventureRunWriteResult.Succeeded,
				_ => false);
		}
		finally
		{
			_debugAdventureSavePresentationProbeRunning = false;
			_debugAdventureSavePresentationProbeCompleted = true;
		}
	}

	public bool DebugIsAdventureSavePresentationProbeCompleted()
	{
		return _debugAdventureSavePresentationProbeCompleted;
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
		return GetAdventureRunSessionStore()?.GetReadModel().MainStageIndex ?? 0;
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

		TryUpdateMatchFlowState(state => state with
		{
			MainPlayerRealTurnActive = true,
			MainPlayerRealTurnStartPending = false,
		});
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

		TrackBoardTask(
			RunDebugPendingHandLimitDiscardAsync(ownerPlayerIndex),
			nameof(RunDebugPendingHandLimitDiscardAsync));
		return true;
	}

	private async Task RunDebugPendingHandLimitDiscardAsync(
		int ownerPlayerIndex)
	{
		bool discarded = false;
		try
		{
			if (_currentTurnPlayerIndex != ownerPlayerIndex
				&& !await SetCurrentTurnPlayerIndexAsync(ownerPlayerIndex))
			{
				return;
			}

			int turnGeneration = _turnSequenceGeneration;
			BeginNetworkAuthorityAction(
				AuthorityActionKind.QuestionReward,
				ownerPlayerIndex,
				turnGeneration,
				clientActionId: 0,
				itemOrRewardId: "question_mark");
			MarkNetworkAuthorityActionApplying();
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
		DebugRuleFixture fixture = CreateCurrentDebugRuleFixture();
		fixture.PlayerHands[ownerPlayerIndex].Clear();
		fixture.PlayerDecks[ownerPlayerIndex].Clear();
		fixture.PlayerItemHands[ownerPlayerIndex].Clear();
		if (!CommitDebugRuleFixture(fixture))
		{
			return false;
		}
		RenderPlayerHands();
		_pendingHandLimitDiscardCompletion.TrySetResult(-1);
		CheckForWinner();
		return true;
	}

	public bool DebugUnexpectedlyReleaseActiveSelectionOverlay()
	{
		SelectionOverlaySession? session = ActiveSelectionOverlaySession;
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
			CardPlayPlan plan = ResolveCardPlayPlan(
				_playerHands[localPlayerIndex][handIndex],
				localPlayerIndex);
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
		int basePlayCaptureThreshold,
		int decisionRollPercent,
		int randomCandidateOrdinal)
	{
		var result = new Godot.Collections.Dictionary
		{
			["eligible"] = false,
			["kind"] = EnemyAiAdventureSelectionKind.None.ToString(),
			["hand_index"] = -1,
		};
		if (tableCardCount < 0
			|| tableCardCount > 4
			|| !ContinuousCaptureRules.IsValidThreshold(basePlayCaptureThreshold)
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

		DebugRuleFixture fixture = CreateCurrentDebugRuleFixture();
		fixture.BasePlayCaptureThreshold = basePlayCaptureThreshold;
		fixture.TableCards.Clear();
		string[] tableCardKeys = { "Hearts:2", "Spades:3", "Diamonds:4", "Clubs:5" };
		for (int index = 0; index < tableCardCount; index++)
		{
			fixture.TableCards.Add(new TableCard(cards[tableCardKeys[index]], true));
		}

		fixture.PlayerHands[1].Clear();
		fixture.PlayerItemHands[1].Clear();
		fixture.PlayerHands[1].Add(includeCaptureCandidate && tableCardCount > 0
			? cards[tableCardKeys[0]]
			: cards["Hearts:Q"]);
		fixture.PlayerHands[1].Add(cards["Clubs:K"]);
		if (!CommitDebugRuleFixture(fixture))
		{
			return result;
		}

		bool eligible = TrySelectMainAdventureEnemyCard(
			1,
			decisionRollPercent,
			randomCandidateOrdinal,
			out EnemyAiAdventureSelectionResult selection);
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
		if (player_count > 2)
		{
			DebugForceNextOpeningDiceRollResult(1);
		}
		DealCardsToPlayers(playOpeningDealAnimation: false);
		return !_isOpeningDealAnimationPlaying;
	}

}
