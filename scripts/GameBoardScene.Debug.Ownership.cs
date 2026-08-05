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
	private bool _debugLeafyBladeProbeRunning;
	private bool _debugBlackLedgerProbeRunning;
	private int _debugBlackLedgerExpectedDamage;
	private int _debugLeafyBladeTargetTableCardIndex = -1;
	private TableCard? _debugLeafyBladeReplacementTableCard;
	private bool _debugLeafyBladeReplacementHiddenSentinel;

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

	_debugForcedQuestionRewardItemCardIds.Enqueue(itemCardId);
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
	DebugRuleFixture fixture = CreateCurrentDebugRuleFixture();
	fixture.TableCards.Clear();
	for (int index = 0; index < captureThreshold - 1; index++)
	{
		(string suit, string rank) = requiredCards[index];
		fixture.TableCards.Add(new TableCard(
			cards[$"{suit}:{rank}"],
			isFaceUp: true));
	}
	fixture.TableCards.Add(new TableCard(
		null,
		isFaceUp: true,
		itemOverlay: questionMark));
	fixture.PlayerHands[1].Clear();
	fixture.PlayerHands[1].Add(cards["Spades:A"]);
	fixture.CurrentTurnSeat = 1;
	if (!CommitDebugRuleFixture(fixture))
	{
		return false;
	}
	if (!TrySubmitSetPendingAttachmentRewardsRuleCommand(
		ImmutableArray<PendingAttachmentRewardState>.Empty,
		_turnSequenceGeneration))
	{
		return false;
	}
	_debugForcedQuestionRewardItemCardIds.Clear();
	TryUpdateMatchFlowState(state => state with
	{
		OneShotCardReplacementUsed = false,
	});
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
	TrackBoardTask(RunDebugDragonQuestionCaptureOwnershipProbeAsync(result), nameof(RunDebugDragonQuestionCaptureOwnershipProbeAsync));
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
			GetPendingAttachmentRewards().Length;
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
	catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
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
	TrackBoardTask(RunDebugCollectTableCaptureOwnershipProbeAsync(result), nameof(RunDebugCollectTableCaptureOwnershipProbeAsync));
	return true;
}

private async Task RunDebugCollectTableCaptureOwnershipProbeAsync(
	Godot.Collections.Dictionary result)
{
	try
	{
		var request = new CardTriggerRequest(
			DefinitionId: "debug_collect_table_probe",
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
	catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
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
	if (!DebugPrepareAdventureBattleForDeal())
	{
		return false;
	}
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
	TableCard replacement = TableCard.Restore(
		source.InstanceId,
		source.Card,
		source.IsFaceUp,
		source.ItemOverlay,
		source.ItemOwnerPlayerIndex,
		source.CardOwnerPlayerIndex,
		source.RemoveCardOnCapture);
	replacement.IsHiddenDuringCapture = source.IsHiddenDuringCapture;
	replacement.IsBuriedInSand = source.IsBuriedInSand;
	replacement.IsSlimeCoated = source.IsSlimeCoated;
	return TryReplaceDebugTableCardPresentationReference(
		tableCardIndex,
		replacement);
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

	var players = ImmutableArray.CreateBuilder<PlayerStateSnapshot>(player_count);
	for (int seat = 0; seat < player_count; seat++)
	{
		players.Add(CreateScriptedPlayerState(
			seat,
			Array.Empty<PlayingCard>(),
			seat == MainPlayerIndex
				? new[] { cards["Hearts:5"] }
				: Array.Empty<PlayingCard>()));
	}
	if (!TryInitializeScriptedRuleMatch(
			players.MoveToImmutable(),
			ImmutableArray<TableCardState>.Empty))
	{
		return false;
	}

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
	RecordDebugMagnetOwnershipState(result, "before");
	BeginNetworkAuthorityAction(
			AuthorityActionKind.QuestionReward,
			MainPlayerIndex,
			_turnSequenceGeneration,
			clientActionId: 0,
			itemOrRewardId: MagnetItemCardId);
		MarkNetworkAuthorityActionApplying();
		BeginNetworkAuthorityQuestionRewardTransaction(
			MainPlayerIndex,
			MagnetItemCardId,
			requiresAttachment: true,
			reserveReplacement: false,
			_turnSequenceGeneration);
	Task probeTask = RunDebugMagnetGenerationOwnershipProbeAsync(result);
	RecordDebugMagnetOwnershipState(result, "after_start");
	TrackBoardTask(probeTask, nameof(RunDebugMagnetGenerationOwnershipProbeAsync));
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
	catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
	{
		result["error"] = exception.ToString();
	}
	finally
	{
		RecordDebugMagnetOwnershipState(result, "completed");
		result["completed"] = true;
	}
}

private void RecordDebugMagnetOwnershipState(
	Godot.Collections.Dictionary result,
	string prefix)
{
	result[$"{prefix}_scene_generation"] = _turnSequenceGeneration;
	result[$"{prefix}_rule_generation"] =
		_authorityRuleSession?.ReadModel.TurnGeneration ?? -1;
	result[$"{prefix}_current_turn"] = _currentTurnPlayerIndex;
	result[$"{prefix}_rule_session"] = _authorityRuleSession != null;
	result[$"{prefix}_resolving"] = _isResolvingMagnetAttachmentSelection;
	result[$"{prefix}_attachable"] = PlayerHasAttachableMagnetCard(
		MainPlayerIndex,
		MagnetItemCardId);
	result[$"{prefix}_selection"] = CurrentMandatorySelection.Kind.ToString();
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

	TrackBoardTask(CompleteMagnetAttachmentSelectionAsync(0), nameof(CompleteMagnetAttachmentSelectionAsync));
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
	if (!DebugPrepareAdventureBattleForDeal())
	{
		return false;
	}
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

	DebugRuleFixture fixture = CreateCurrentDebugRuleFixture();
	fixture.TableCards.Clear();
	for (int index = 0; index < requiredTableCardCount; index++)
	{
		(string suit, string rank) = requiredCards[index];
		fixture.TableCards.Add(new TableCard(
			cards[$"{suit}:{rank}"],
			isFaceUp: true));
	}
	fixture.CurrentTurnSeat = 1;
	if (!CommitDebugRuleFixture(fixture))
	{
		return false;
	}
	TryUpdateMatchFlowState(state => state with
	{
		DestructiveEffectCooldownTurns = 0,
	});
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
	TrackBoardTask(RunDebugLeafyBladeGenerationOwnershipProbeAsync(
		playResult,
		result), nameof(RunDebugLeafyBladeGenerationOwnershipProbeAsync));
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
	catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
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
	if (!DebugPrepareAdventureBattleForDeal())
	{
		return false;
	}
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
	_debugLeafyBladeReplacementTableCard = TableCard.Restore(
		source.InstanceId,
		source.Card,
		source.IsFaceUp,
		source.ItemOverlay,
		source.ItemOwnerPlayerIndex,
		source.CardOwnerPlayerIndex,
		source.RemoveCardOnCapture);
	_debugLeafyBladeReplacementTableCard.IsBuriedInSand = source.IsBuriedInSand;
	_debugLeafyBladeReplacementTableCard.IsSlimeCoated = source.IsSlimeCoated;
	_debugLeafyBladeReplacementTableCard.IsHiddenDuringCapture =
		_debugLeafyBladeReplacementHiddenSentinel;
	return TryReplaceDebugTableCardPresentationReference(
		tableCardIndex,
		_debugLeafyBladeReplacementTableCard);
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
	return CurrentMatchFlowState.DestructiveEffectCooldownTurns;
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
			if (tableCard.Card.HasValue) return GetPlayingCardTexture(tableCard.Card.Value);
			if (tableCard.ItemOverlay.HasValue) return GetItemCardTexture(tableCard.ItemOverlay.Value);
		}
		foreach (List<PlayingCard> hand in _playerHands)
		{
			if (hand.Count > 0) return GetPlayingCardTexture(hand[0]);
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
	TrackBoardTask(RunDebugSpecialCardTriggerTimelineProbeAsync(texture, result), nameof(RunDebugSpecialCardTriggerTimelineProbeAsync));
		return true;
	}

	private async Task RunDebugSpecialCardTriggerTimelineProbeAsync(Texture2D texture, Godot.Collections.Dictionary result)
	{
		try
		{
			await PlaySpecialCardTriggerEffectAsync(texture, _centerArena.GlobalPosition, zIndex: 0);
		}
		catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
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
		DebugRuleFixture fixture = CreateCurrentDebugRuleFixture();
		fixture.TableCards.Clear();
		fixture.TableCards.Add(originallyHidden);
		fixture.TableCards.Add(fixedCard);
		fixture.TableCards.Add(ordinaryCard);
		if (!CommitDebugRuleFixture(fixture)) return false;
		_debugTableShuffleOriginalCards = new TableCard[_tableCards.Count];
		_tableCards.CopyTo(_debugTableShuffleOriginalCards);
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
		TrackBoardTask(RunDebugTableShuffleOwnershipProbeAsync(caller, result), nameof(RunDebugTableShuffleOwnershipProbeAsync));
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
		catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
		{
			result["error"] = exception.ToString();
		}
		finally
		{
			result["completed"] = true;
			_debugTableShuffleProbeRunning = false;
		}
	}

	private static TableCard CloneDebugTableShuffleCard(TableCard source)
	{
		TableCard clone = TableCard.Restore(
			source.InstanceId, source.Card, source.IsFaceUp, source.ItemOverlay,
			source.ItemOwnerPlayerIndex, source.CardOwnerPlayerIndex,
			source.RemoveCardOnCapture);
		clone.IsBuriedInSand = source.IsBuriedInSand;
		clone.IsSlimeCoated = source.IsSlimeCoated;
		clone.IsHiddenDuringCapture = source.IsHiddenDuringCapture;
		return clone;
	}

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
			for (int index = 0; index < _tableCards.Count; index++)
			{
				if (!TryReplaceDebugTableCardPresentationReference(
					index,
					CloneDebugTableShuffleCard(_tableCards[index]))) return false;
			}
		}
		else if (!TryReplaceDebugTableCardPresentationReference(
			0,
			CloneDebugTableShuffleCard(_tableCards[0]))) return false;
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
