using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	public int DebugGetLocalPlayerViewSlotIndex()
	{
		return GetPlayerViewSlotIndex(GetLocalHandPlayerIndex());
	}

	public int DebugGetLocalPlayerIndex()
	{
		return GetLocalHandPlayerIndex();
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

	public void DebugInjectNetworkVisualPlaybackFailures(int count)
	{
		_debugNetworkVisualPlaybackFailuresToInject = Math.Max(0, count);
	}

	public string DebugGetMatchOutcomeKindName()
	{
		return _matchOutcomeKind.ToString();
	}

	public bool DebugConfigureAuthorityTransactionFixture(string fixture)
	{
		if (!IsNetworkMultiplayerMode()
			|| !IsNetworkHost()
			|| player_count != 2
			|| _isOpeningDealAnimationPlaying
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| HasPendingItemSelection()
			|| _networkAuthorityActionState.IsActive)
		{
			return false;
		}

		var requiredCards = new (string Suit, string Rank)[]
		{
			("Hearts", "2"),
			("Hearts", "5"),
			("Hearts", "6"),
			("Spades", "9"),
			("Spades", "8"),
			("Diamonds", "3"),
			("Diamonds", "4"),
			("Clubs", "5"),
			("Clubs", "7"),
		};
		if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
		{
			return false;
		}

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			_playerHands[playerIndex].Clear();
			_playerDecks[playerIndex].Clear();
			_playerItemHands[playerIndex].Clear();
			_playerScores[playerIndex] = 0;
		}
		_tableCards.Clear();
		ClearPendingItemSelectionState();
		ClearNetworkPendingActionState();
		ResetNetworkPredictedClientState();
		_matchOutcomeKind = MatchOutcomeKind.InProgress;
		_winnerPlayerIndex = -1;
		_currentTurnPlayerIndex = 1;
		_completedTurnsSinceQuestionCardRefresh = 0;
		_questionCardRefreshAnchorPlayerIndex = -1;
		_rng.Seed = 20260720;

		switch (fixture)
		{
			case "base_capture":
				_playerHands[1].Add(cards["Hearts:5"]);
				_playerHands[0].Add(cards["Spades:9"]);
				_tableCards.Add(new TableCard(cards["Clubs:5"], true));
				_tableCards.Add(new TableCard(cards["Diamonds:3"], true));
				break;
			case "question_capture":
				ItemCard? questionMark = CreateItemCard(QuestionMarkItemCardId);
				if (!questionMark.HasValue
					|| !DebugForceNextQuestionRewardItemCardId(ItemCardCatalog.DrawCardItemCardId))
				{
					return false;
				}
				_playerHands[1].Add(cards["Hearts:5"]);
				_playerHands[0].Add(cards["Spades:9"]);
				_tableCards.Add(new TableCard(cards["Clubs:5"], true));
				_tableCards.Add(new TableCard(null, true, questionMark));
				break;
			case "item_shuffle":
				ItemCard? shuffle = CreateItemCard(ItemCardCatalog.ShuffleItemCardId);
				if (!shuffle.HasValue)
				{
					return false;
				}
				_playerItemHands[1].Add(shuffle.Value);
				_playerDecks[1].Add(cards["Spades:9"]);
				_playerHands[0].Add(cards["Hearts:6"]);
				_tableCards.Add(new TableCard(cards["Hearts:2"], true));
				_tableCards.Add(new TableCard(cards["Clubs:7"], true));
				_tableCards.Add(new TableCard(cards["Diamonds:4"], true));
				_tableCards.Add(new TableCard(cards["Spades:8"], true));
				break;
			default:
				return false;
		}

		InvalidateRenderCaches();
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		SendBoardSnapshotIfHost();
		return true;
	}

	public bool DebugIsAuthorityTransactionFixtureCompleted(string fixture)
	{
		if (_nextNetworkAuthorityActionId != 1
			|| _networkAuthorityActionState.IsActive
			|| _isProcessingCardPlay
			|| HasPendingItemSelection())
		{
			return false;
		}

		return fixture switch
		{
			"base_capture" => _playerScores[1] > 0 && _tableCards.Count == 0,
			"question_capture" => _tableCards.Count == 0
				&& _playerItemHands[1].Count == 1
				&& string.Equals(
					_playerItemHands[1][0].Id,
					ItemCardCatalog.DrawCardItemCardId,
					StringComparison.Ordinal)
				&& _currentTurnPlayerIndex == 1,
			"item_shuffle" => _playerItemHands[1].Count == 0
				&& _tableCards.Count == 4
				&& _currentTurnPlayerIndex == 0,
			_ => false,
		};
	}

	public string DebugGetAuthorityTransactionStableFingerprint()
	{
		var canonical = new StringBuilder(2048);
		canonical.Append("authority-transaction-board-v1\n");
		canonical.Append("turn=").Append(_currentTurnPlayerIndex).Append('\n');
		canonical.Append("outcome=").Append(_matchOutcomeKind).Append(':').Append(_winnerPlayerIndex).Append('\n');
		canonical.Append("question=").Append(_completedTurnsSinceQuestionCardRefresh)
			.Append(':').Append(_questionCardRefreshAnchorPlayerIndex).Append('\n');
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			canonical.Append("player=").Append(playerIndex).Append(" score=")
				.Append(_playerScores[playerIndex]).Append('\n');
			AppendPlayingCards(canonical, "deck", _playerDecks[playerIndex]);
			AppendPlayingCards(canonical, "hand", _playerHands[playerIndex]);
			canonical.Append("items=");
			foreach (ItemCard itemCard in _playerItemHands[playerIndex])
			{
				AppendLengthPrefixed(canonical, itemCard.Id);
			}
			canonical.Append('\n');
		}
		canonical.Append("table=");
		foreach (TableCard tableCard in _tableCards)
		{
			AppendLengthPrefixed(
				canonical,
				tableCard.Card.HasValue ? ToNetworkCardCode(tableCard.Card.Value) : string.Empty);
			AppendLengthPrefixed(
				canonical,
				tableCard.Card.HasValue ? tableCard.Card.Value.AttachedItemCardIdsCsv : string.Empty);
			AppendLengthPrefixed(canonical, tableCard.ItemOverlay?.Id ?? string.Empty);
			canonical.Append(tableCard.IsFaceUp ? '1' : '0')
				.Append(tableCard.IsBuriedInSand ? '1' : '0')
				.Append(tableCard.IsSlimeCoated ? '1' : '0')
				.Append(':').Append(tableCard.CardOwnerPlayerIndex)
				.Append(':').Append(tableCard.ItemOwnerPlayerIndex).Append(';');
		}

		return Convert.ToHexString(
			SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
			.ToLowerInvariant();

		void AppendPlayingCards(StringBuilder builder, string label, IReadOnlyList<PlayingCard> playingCards)
		{
			builder.Append(label).Append('=');
			foreach (PlayingCard card in playingCards)
			{
				AppendLengthPrefixed(builder, ToNetworkCardCode(card));
				AppendLengthPrefixed(builder, card.AttachedItemCardIdsCsv);
			}
			builder.Append('\n');
		}

		static void AppendLengthPrefixed(StringBuilder builder, string value)
		{
			string normalized = value ?? string.Empty;
			builder.Append(normalized.Length).Append(':').Append(normalized).Append(';');
		}
	}

	public string DebugGetAuthorityTransactionRngState()
	{
		return _rng.State.ToString(CultureInfo.InvariantCulture);
	}

	public long DebugGetNextNetworkAuthorityActionId()
	{
		return _nextNetworkAuthorityActionId;
	}

	public bool DebugSubmitAuthorityTransactionFixtureAction(string fixture)
	{
		if (!IsNetworkMultiplayerMode()
			|| GetLocalHandPlayerIndex() != 1
			|| _currentTurnPlayerIndex != 1
			|| _networkMigrationInputLocked
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| _networkPendingHandIndex >= 0
			|| _networkPendingItemHandIndex >= 0)
		{
			return false;
		}

		if ((fixture == "base_capture" || fixture == "question_capture")
			&& _playerHands[1].Count == 1
			&& string.Equals(ToNetworkCardCode(_playerHands[1][0]), "Hearts:5", StringComparison.Ordinal))
		{
			RequestNetworkHandCardPlay(0, _turnSequenceGeneration);
			return true;
		}

		if (fixture == "item_shuffle"
			&& _playerItemHands[1].Count == 1
			&& string.Equals(
				_playerItemHands[1][0].Id,
				ItemCardCatalog.ShuffleItemCardId,
				StringComparison.Ordinal))
		{
			RequestNetworkItemCardPlay(0, _turnSequenceGeneration);
			return true;
		}

		return false;
	}

	public bool DebugStartCoreTurnResolutionProbe(
		string scenario,
		Godot.Collections.Dictionary result)
	{
		if (result == null
			|| player_count < 3
			|| player_count > 4
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| HasPendingItemSelection())
		{
			return false;
		}

		result.Clear();
		result["completed"] = false;
		result["error"] = string.Empty;
		TrackBoardTask(RunCoreTurnResolutionProbeAsync(scenario, result), nameof(RunCoreTurnResolutionProbeAsync));
		return true;
	}

	private async Task RunCoreTurnResolutionProbeAsync(
		string scenario,
		Godot.Collections.Dictionary result)
	{
		try
		{
			var requiredCards = new (string Suit, string Rank)[]
			{
				("Hearts", "2"),
				("Spades", "3"),
				("Diamonds", "4"),
				("Clubs", "5"),
			};
			if (!TryLoadAdventureTutorialCards(requiredCards, out Dictionary<string, PlayingCard> cards))
			{
				throw new InvalidOperationException("Core turn probe could not load fixed playing cards.");
			}
			var availableCards = new List<PlayingCard>
			{
				cards["Hearts:2"],
				cards["Spades:3"],
				cards["Diamonds:4"],
				cards["Clubs:5"],
			};
			for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
			{
				_playerHands[playerIndex].Clear();
				_playerDecks[playerIndex].Clear();
				_playerItemHands[playerIndex].Clear();
				_playerScores[playerIndex] = 0;
			}
			if (availableCards.Count < player_count)
			{
				throw new InvalidOperationException("Core turn probe has too few playing cards.");
			}

			_tableCards.Clear();
			_matchOutcomeKind = MatchOutcomeKind.InProgress;
			_winnerPlayerIndex = -1;
			_isOpeningDealAnimationPlaying = false;
			_isProcessingCardPlay = false;
			_isResolvingTurnSequence = false;
			AdvanceTurnSequenceGeneration();

			int actorPlayerIndex;
			int expectedTurnPlayerIndex = -1;
			switch (scenario)
			{
				case "human_last_noncompleting_item":
					actorPlayerIndex = MainPlayerIndex;
					for (int playerIndex = 1; playerIndex < player_count; playerIndex++)
					{
						_playerHands[playerIndex].Add(availableCards[playerIndex]);
					}
					expectedTurnPlayerIndex = player_count >= 3 ? 2 : 1;
					break;
				case "ai_last_noncompleting_item":
					actorPlayerIndex = 2;
					for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
					{
						if (playerIndex != actorPlayerIndex)
						{
							_playerHands[playerIndex].Add(availableCards[playerIndex]);
						}
					}
					expectedTurnPlayerIndex = 1;
					break;
				case "consecutive_skips":
					actorPlayerIndex = MainPlayerIndex;
					expectedTurnPlayerIndex = player_count == 4 ? 3 : 1;
					_playerHands[expectedTurnPlayerIndex].Add(availableCards[expectedTurnPlayerIndex]);
					for (int playerIndex = 1; playerIndex < player_count; playerIndex++)
					{
						if (playerIndex != expectedTurnPlayerIndex)
						{
							ItemCard? blockedItem = CreateItemCard("draw_card");
							if (blockedItem.HasValue)
							{
								_playerItemHands[playerIndex].Add(blockedItem.Value);
							}
						}
					}
					break;
				case "all_blocked":
					actorPlayerIndex = MainPlayerIndex;
					for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
					{
						ItemCard? blockedItem = CreateItemCard("draw_card");
						if (!blockedItem.HasValue)
						{
							throw new InvalidOperationException("Core turn probe could not create draw_card.");
						}
						_playerItemHands[playerIndex].Add(blockedItem.Value);
						_playerScores[playerIndex] = 10;
					}
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown core turn probe scenario.");
			}

			_currentTurnPlayerIndex = actorPlayerIndex;
			InvalidateRenderCaches();
			RenderPlayerCardPiles();
			RenderPlayerHands();
			RenderTableCards();
			await ResolveActionCompletionAsync(
				actorPlayerIndex,
				new PlayCardResult(true, false, CompletesTurn: false));
			SendBoardSnapshotIfHost();

			result["scenario"] = scenario;
			result["actor"] = actorPlayerIndex;
			result["actor_ai_controlled"] = IsNetworkMultiplayerMode()
				? GetNetworkManager()?.IsSeatAiControlled(actorPlayerIndex) == true
				: actorPlayerIndex != MainPlayerIndex;
			result["expected_turn"] = expectedTurnPlayerIndex;
			result["current_turn"] = _currentTurnPlayerIndex;
			result["outcome"] = _matchOutcomeKind.ToString();
			result["winner"] = _winnerPlayerIndex;
			result["pending_selection"] = HasPendingItemSelection();
			result["completed"] = true;
		}
		catch (Exception exception) when (exception is not OperationCanceledException || !IsBoardStopping)
		{
			result["error"] = exception.ToString();
			result["completed"] = true;
		}
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

		int targetPlayerIndex = adventure_mode
			? GetAdventureDamageTargetPlayerIndex(playerIndex)
			: playerIndex;
		if (targetPlayerIndex == MainPlayerIndex && ShouldUseAdventureUpgradeSystem())
		{
			AdventureRunSessionStore? store = GetAdventureRunSessionStore();
			AdventureDirectDamageCommitResult commitResult = store?.ApplyDirectDamage(
				new AdventureDirectDamageCommitInput(
					points,
					_adventureMainPlayerHalfHealthRescueUsed,
					_adventureMainPlayerQuarterHealthRescueUsed))
				?? new AdventureDirectDamageCommitResult(
					AdventureRunWriteResult.SaveFailed,
					0,
					0,
					default,
					default);
			if (commitResult.WriteResult != AdventureRunWriteResult.Succeeded)
			{
				return false;
			}
			ApplyCommittedAdventureDirectDamage(playerIndex, commitResult);
		}
		else
		{
			AddPlayerScore(playerIndex, points);
		}
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

	public int DebugGetNetworkVisualActivityCount()
	{
		return NetworkVisualPlayback.ActivityCount;
	}

	public int DebugGetNetworkVisualPlaybackFailureCount()
	{
		return NetworkVisualPlayback.PlaybackFailureCount;
	}

	public bool DebugIsNetworkVisualFastForwarding()
	{
		return NetworkVisualPlayback.IsFastForwarding;
	}

	public int DebugGetNetworkAuthorityHostVisualDecodeFailureCount()
	{
		return _networkAuthorityHostVisualDecodeFailureCount;
	}

	public int DebugGetNetworkAuthorityHostVisualPlaybackCount()
	{
		return _networkAuthorityHostVisualPlaybackCount;
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
				["player_session_id"] = string.Empty,
				["credential_generation"] = SeatCredentialRules.InitialGeneration,
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
		TrackBoardTask(PlayNetworkCardPlayedEventAsync(visualEvent), nameof(PlayNetworkCardPlayedEventAsync));
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

	public bool DebugQueueDecodedNetworkVisualEvent(
		Godot.Collections.Dictionary payload)
	{
		if (!TryDecodeNetworkVisualEvent(payload, out NetworkVisualEvent visualEvent))
		{
			return false;
		}

		NetworkVisualPlayback.Enqueue(visualEvent);
		return true;
	}

	public bool DebugSubmitNetworkVisualSnapshot(
		Godot.Collections.Dictionary payload)
	{
		if (!TryDecodeNetworkBoardSnapshot(payload, out ValidatedNetworkBoardSnapshot snapshot))
		{
			return false;
		}

		NetworkVisualPlayback.SubmitSnapshot(snapshot);
		return true;
	}

	public bool DebugQueueDecodedNetworkAuthorityHostVisualEvent(
		Godot.Collections.Dictionary payload)
	{
		if (!TryDecodeNetworkVisualEvent(
			payload,
			out NetworkVisualEvent visualEvent,
			authorityHostVisualOnly: true))
		{
			return false;
		}

		Vector2 startGlobalPosition = visualEvent.Actor >= 0 && visualEvent.Actor < player_count
			? TryGetHandCardStartGlobalPosition(visualEvent.Actor, visualEvent.HandIndex)
				?? GetVisibleCardPileTopGlobalPosition(visualEvent.Actor)
			: Vector2.Zero;
		NetworkVisualPlayback.Enqueue(visualEvent with
		{
			IsAuthorityHostVisualOnly = true,
			AuthorityHostStartGlobalPosition = startGlobalPosition,
		});
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


}
