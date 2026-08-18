using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void SliceFourGameplayBoundariesStayBehindNeutralRuleCommands()
	{
		string root = FindRepositoryRoot();
		string[] paths =
		{
			"scripts/GameBoardScene.AdventureUpgrades.cs",
			"scripts/GameBoardScene.AdventureUpgradeOffers.cs",
			"scripts/GameBoardScene.BossTableAbilities.cs",
			"scripts/GameBoardScene.DragonPrinceTableAbility.cs",
			"scripts/GameBoardScene.TableAbilityTriggers.cs",
			"scripts/GameBoardScene.TableTriggerHelpers.cs",
			"scripts/GameBoardScene.TurnAdvancement.cs",
			"scripts/AdventureExtraActionChain.cs",
		};
		var sources = paths.ToDictionary(
			path => path,
			path => File.ReadAllText(Path.Combine(root, path)),
			StringComparer.Ordinal);
		sources["scripts/GameBoardScene.AdventureUpgrades.cs"] = string.Concat(
			Directory.GetFiles(
				Path.Combine(root, "scripts"),
				"GameBoardScene.AdventureUpgrades*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		foreach (string source in sources.Values)
		{
			False(source.Contains("_rng.", StringComparison.Ordinal));
		}

		string upgrades = sources["scripts/GameBoardScene.AdventureUpgrades.cs"];
		True(upgrades.Contains("TrySubmitMoveTableCardRuleCommand(", StringComparison.Ordinal));
		False(upgrades.Contains("_tableCards.Insert(", StringComparison.Ordinal));
		string tableAbilities = sources["scripts/GameBoardScene.TableAbilityTriggers.cs"];
		True(tableAbilities.Contains(
			"TrySubmitSetTableCardsScoreSuppressedRuleCommand(",
			StringComparison.Ordinal));
		False(tableAbilities.Contains(".IsSlimeCoated =", StringComparison.Ordinal));

		string boss = sources["scripts/GameBoardScene.BossTableAbilities.cs"];
		True(boss.Contains("TrySubmitMoveTableCardToDeckRuleCommand(", StringComparison.Ordinal));
		True(boss.Contains("TrySubmitTransferTopDeckCardRuleCommand(", StringComparison.Ordinal));
		string dragon = sources["scripts/GameBoardScene.DragonPrinceTableAbility.cs"];
		True(dragon.Contains("TrySubmitPlaceTableCardRuleCommand(", StringComparison.Ordinal));

		string turn = sources["scripts/GameBoardScene.TurnAdvancement.cs"];
		True(turn.Contains("TrySubmitEndRealTurnRuleCommand(", StringComparison.Ordinal));
		False(turn.Contains(".IsHiddenDuringCapture =", StringComparison.Ordinal));
		string extraActionAdapter = sources["scripts/AdventureExtraActionChain.cs"];
		False(extraActionAdapter.Contains("ExtraActionQueueState", StringComparison.Ordinal));
		False(extraActionAdapter.Contains("_state.", StringComparison.Ordinal));
		string upgradeOffers = sources["scripts/GameBoardScene.AdventureUpgradeOffers.cs"];
		False(upgradeOffers.Contains(
			"_pendingAdventureUpgradeTriggers",
			StringComparison.Ordinal));
		False(upgradeOffers.Contains(
			"_pendingAdventureWinnerPlayerIndex",
			StringComparison.Ordinal));
		True(upgradeOffers.Contains(
			"CurrentMatchFlowState.NormalizedPendingProgressionChoices",
			StringComparison.Ordinal));
		True(upgradeOffers.Contains("next.ToImmutable()", StringComparison.Ordinal));
		False(upgradeOffers.Contains("next.MoveToImmutable()", StringComparison.Ordinal));
		string itemCards = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.ItemCards.cs"));
		string questionCards = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.QuestionCards.cs"));
		False(itemCards.Contains(
			"_plannedQuestionRewardDiscardExclusions",
			StringComparison.Ordinal));
		False(questionCards.Contains(
			"_plannedQuestionRewardDiscardExclusions",
			StringComparison.Ordinal));
		True(itemCards.Contains(
			"pending.NormalizedLegalSelectionIndexes.Contains(displayIndex)",
			StringComparison.Ordinal));

		string extraActionCore = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"ExtraActionQueue.cs"));
		False(extraActionCore.Contains("Adventure", StringComparison.Ordinal));
		string legacyState = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"LegacyCardStateModels.cs"));
		False(legacyState.Contains("AdventureExtraAction", StringComparison.Ordinal));
		string rulesProject = File.ReadAllText(Path.Combine(
			root,
			"src",
			"Deckland.Rules",
			"Deckland.Rules.csproj"));
		False(rulesProject.Contains("../../scripts/AdventureExtraActionChain.cs", StringComparison.Ordinal));
	}

	private static void SliceFiveAiRandomnessStaysBehindRuleCommands()
	{
		string root = FindRepositoryRoot();
		string turnAi = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TurnAi.cs"));
		string soloAi = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.SoloAi.cs"));
		string networkAi = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.NetworkAiProxy.cs"));

		foreach (string source in new[] { turnAi, soloAi, networkAi })
		{
			False(source.Contains("_rng.", StringComparison.Ordinal));
			False(source.Contains("_playerHands", StringComparison.Ordinal));
			False(source.Contains("_playerItemHands", StringComparison.Ordinal));
			True(source.Contains(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringComparison.Ordinal));
			True(source.Contains(
				"TryGetAuthorityRuleReadModel(",
				StringComparison.Ordinal));
		}
		False(soloAi.Contains("_tableCards", StringComparison.Ordinal));
		False(networkAi.Contains("_tableCards", StringComparison.Ordinal));
		False(turnAi.Contains("ToCaptureScoringTableCards()", StringComparison.Ordinal));
		True(turnAi.Contains(
			"ToCaptureScoringTableCards(readModel.TableCards)",
			StringComparison.Ordinal));
		True(soloAi.Contains(
			"ToPotentialCaptureCards(playerIndex, player.Hand)",
			StringComparison.Ordinal));
		True(networkAi.Contains(
			"ToPotentialCaptureCards(\n\t\t\t\t\tplayerIndex,\n\t\t\t\t\tplayer.Hand)",
			StringComparison.Ordinal));
		True(turnAi.Contains(
			"CardPlayRuleOptions options = BuildCardPlayRuleOptions(card);",
			StringComparison.Ordinal));
		True(turnAi.Contains("options.AlternateMatchRanks", StringComparison.Ordinal));
		True(turnAi.Contains("options.SuitMatchIsFallbackOnly", StringComparison.Ordinal));

		Equal(
			5,
			turnAi.Split(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringSplitOptions.None).Length - 1);
		Equal(
			4,
			soloAi.Split(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringSplitOptions.None).Length - 1);
		Equal(
			3,
			networkAi.Split(
				"TrySubmitChooseRandomIndexRuleCommand(",
				StringSplitOptions.None).Length - 1);
		True(turnAi.Contains("out int decisionRollPercent", StringComparison.Ordinal));
		True(turnAi.Contains("out int randomCandidateOrdinal", StringComparison.Ordinal));
		True(soloAi.Contains("out int randomCaptureOrdinal", StringComparison.Ordinal));
		True(networkAi.Contains("9999", StringComparison.Ordinal));
	}

	private static TableCardState CreateRuleTableCardState(
		long tableRuntimeInstanceId,
		long cardRuntimeInstanceId,
		string code)
	{
		string[] parts = code.Split(':');
		return new TableCardState(
			tableRuntimeInstanceId,
			new LogicalCardState(cardRuntimeInstanceId, code, parts[0], parts[1]),
			false,
			false,
			false,
			false,
			null,
			-1,
			1,
			false);
	}

	private static AuthorityRuleSession CreateItemCommandSession(
		bool itemRulesEnabled,
		TestRuleRandomSource? random = null)
	{
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(
					new LogicalCardState(1, "clubs:4", "clubs", "4"),
					new LogicalCardState(2, "hearts:8", "hearts", "8")),
				ImmutableArray.Create(
					new LogicalItemState("bomb"),
					new LogicalItemState("lens")),
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(3, "spades:9", "spades", "9")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		MatchRuleProfile profile = itemRulesEnabled
			? CreateItemRuleProfile()
			: MatchRuleProfile.Empty;
		var session = new AuthorityRuleSession(
			random ?? new TestRuleRandomSource(55),
			profile);
		True(session.Submit(new InitializeScriptedMatchCommand(
			250,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);
		return session;
	}

	private static MatchRuleProfile CreateItemRuleProfile()
	{
		return new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
	}

	private static void AuthorityOutcomeCommandResolvesEqualScoreExhaustionAsDraw()
	{
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				12),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				12));
		var session = new AuthorityRuleSession(new TestRuleRandomSource(44), MatchRuleProfile.Empty);
		True(session.Submit(new InitializeScriptedMatchCommand(
			241,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);

		MatchReadModel before = session.ReadModel;
		RuleCommandResult outcome = session.Submit(new EvaluateMatchOutcomeCommand(
			242,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration));
		True(outcome.Accepted);
		Equal(Deckland.Rules.MatchOutcome.Draw, outcome.ReadModel.Outcome);
		Equal(-1, outcome.ReadModel.WinnerSeat);
	}

	private static AuthorityRuleSession CreateGameplayCommandSession(
		int playerZeroScore = 0,
		ImmutableArray<LogicalCardState> playerZeroDeck = default,
		MatchRuleProfile? profile = null)
	{
		if (playerZeroDeck.IsDefault)
		{
			playerZeroDeck = ImmutableArray<LogicalCardState>.Empty;
		}

		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				playerZeroDeck,
				ImmutableArray.Create(new LogicalCardState(1, "spades:5", "spades", "5")),
				ImmutableArray<LogicalItemState>.Empty,
				playerZeroScore),
			new PlayerStateSnapshot(
				1,
				ImmutableArray.Create(new LogicalCardState(4, "clubs:9", "clubs", "9")),
				ImmutableArray.Create(new LogicalCardState(5, "diamonds:8", "diamonds", "8")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			new TableCardState(
				11,
				new LogicalCardState(2, "hearts:5", "hearts", "5"),
				true,
				false,
				false,
				false,
				null,
				-1,
				-1,
				false),
			new TableCardState(
				12,
				new LogicalCardState(3, "clubs:2", "clubs", "2"),
				true,
				false,
				false,
				false,
				null,
				-1,
				-1,
				false));
		var session = new AuthorityRuleSession(
			new TestRuleRandomSource(43),
			profile ?? MatchRuleProfile.Empty);
		RuleCommandResult initialized = session.Submit(new InitializeScriptedMatchCommand(
			200,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards));
		True(initialized.Accepted);
		return session;
	}

	private static void AssertExtraActionsEquivalent(
		ExtraActionQueueSnapshot expected,
		ExtraActionQueueSnapshot actual)
	{
		Equal(expected.NextRootChainId, actual.NextRootChainId);
		Equal(expected.ActiveRootChainId, actual.ActiveRootChainId);
		Equal(expected.RootResumesNormalTurn, actual.RootResumesNormalTurn);
		Equal(expected.RootDerivationStopped, actual.RootDerivationStopped);
		Equal(expected.RootEventCount, actual.RootEventCount);
		Equal(expected.RootMaxDepth, actual.RootMaxDepth);
		Equal(
			expected.ExtraActionsExecutedThisRealTurn,
			actual.ExtraActionsExecutedThisRealTurn);
		Equal(
			expected.ContinueBranchUsedThisRealTurn,
			actual.ContinueBranchUsedThisRealTurn);
		Equal(expected.CurrentGrant, actual.CurrentGrant);
		True(expected.QueuedGrants.SequenceEqual(actual.QueuedGrants));
		True(expected.SourcesInRootChain.SequenceEqual(actual.SourcesInRootChain));
		True(expected.PendingRealTurnEndMarkerIds.SequenceEqual(
			actual.PendingRealTurnEndMarkerIds));
	}

	private static void AssertReadModelsEquivalent(MatchReadModel expected, MatchReadModel actual)
	{
		Equal(expected.Revision, actual.Revision);
		Equal(expected.LifecycleGeneration, actual.LifecycleGeneration);
		Equal(expected.TurnGeneration, actual.TurnGeneration);
		Equal(expected.PlayerCount, actual.PlayerCount);
		Equal(expected.WinningScore, actual.WinningScore);
		Equal(expected.CurrentTurnSeat, actual.CurrentTurnSeat);
		Equal(expected.Outcome, actual.Outcome);
		Equal(expected.WinnerSeat, actual.WinnerSeat);
		Equal(
			expected.CompletedTurnsSinceQuestionRefresh,
			actual.CompletedTurnsSinceQuestionRefresh);
		Equal(expected.QuestionRefreshAnchorSeat, actual.QuestionRefreshAnchorSeat);
		Equal(expected.PendingPresentationBarrierId, actual.PendingPresentationBarrierId);
		True(expected.NormalizedPendingTableOrderRuntimeInstanceIds.SequenceEqual(
			actual.NormalizedPendingTableOrderRuntimeInstanceIds));
		MandatorySelectionState expectedSelection = expected.NormalizedMandatorySelection;
		MandatorySelectionState actualSelection = actual.NormalizedMandatorySelection;
		Equal(expectedSelection.Kind, actualSelection.Kind);
		Equal(expectedSelection.Seat, actualSelection.Seat);
		Equal(expectedSelection.SourceHandIndex, actualSelection.SourceHandIndex);
		Equal(expectedSelection.TurnGeneration, actualSelection.TurnGeneration);
		Equal(expectedSelection.ItemId, actualSelection.ItemId);
		True(expectedSelection.NormalizedLegalSelectionIndexes.SequenceEqual(
			actualSelection.NormalizedLegalSelectionIndexes));
		True(expected.NormalizedPendingAttachmentRewards.SequenceEqual(
			actual.NormalizedPendingAttachmentRewards));
		AssertExtraActionsEquivalent(
			expected.NormalizedExtraActions,
			actual.NormalizedExtraActions);
		Equal(expected.NormalizedMatchFlow, actual.NormalizedMatchFlow);
		Equal(expected.Players.Length, actual.Players.Length);
		for (int seat = 0; seat < expected.Players.Length; seat++)
		{
			PlayerStateSnapshot expectedPlayer = expected.Players[seat];
			PlayerStateSnapshot actualPlayer = actual.Players[seat];
			Equal(expectedPlayer.Seat, actualPlayer.Seat);
			Equal(expectedPlayer.Score, actualPlayer.Score);
			AssertCardStatesEquivalent(expectedPlayer.Deck, actualPlayer.Deck);
			AssertCardStatesEquivalent(expectedPlayer.Hand, actualPlayer.Hand);
			True(expectedPlayer.ItemHand.SequenceEqual(actualPlayer.ItemHand));
		}

		Equal(expected.TableCards.Length, actual.TableCards.Length);
		for (int index = 0; index < expected.TableCards.Length; index++)
		{
			TableCardState expectedCard = expected.TableCards[index];
			TableCardState actualCard = actual.TableCards[index];
			Equal(expectedCard.RuntimeInstanceId, actualCard.RuntimeInstanceId);
			Equal(expectedCard.IsFaceUp, actualCard.IsFaceUp);
			Equal(expectedCard.IsHiddenDuringCapture, actualCard.IsHiddenDuringCapture);
			Equal(expectedCard.IsBuried, actualCard.IsBuried);
			Equal(expectedCard.IsScoreSuppressed, actualCard.IsScoreSuppressed);
			Equal(expectedCard.ItemOverlay, actualCard.ItemOverlay);
			Equal(expectedCard.ItemOwnerSeat, actualCard.ItemOwnerSeat);
			Equal(expectedCard.CardOwnerSeat, actualCard.CardOwnerSeat);
			Equal(expectedCard.RemoveOnCapture, actualCard.RemoveOnCapture);
			Equal(expectedCard.Card.HasValue, actualCard.Card.HasValue);
			if (expectedCard.Card.HasValue)
			{
				AssertCardStatesEquivalent(
					ImmutableArray.Create(expectedCard.Card.Value),
					ImmutableArray.Create(actualCard.Card!.Value));
			}
		}
	}

	private static void AssertCardStatesEquivalent(
		ImmutableArray<LogicalCardState> expected,
		ImmutableArray<LogicalCardState> actual)
	{
		Equal(expected.Length, actual.Length);
		for (int index = 0; index < expected.Length; index++)
		{
			Equal(expected[index].RuntimeInstanceId, actual[index].RuntimeInstanceId);
			Equal(expected[index].Code, actual[index].Code);
			Equal(expected[index].Suit, actual[index].Suit);
			Equal(expected[index].Rank, actual[index].Rank);
			True(expected[index].NormalizedAttachedEffectIds.SequenceEqual(
				actual[index].NormalizedAttachedEffectIds));
		}
	}

	private static InitializeMatchCommand CreateInitializeCommand(
		long actionId,
		bool createOpeningBarrier = false)
	{
		var cards = ImmutableArray.CreateBuilder<LogicalCardState>();
		long instanceId = 1;
		foreach (string rank in new[] { "1", "2", "3" })
		{
			foreach (string suit in new[] { "hearts", "clubs", "diamonds", "spades" })
			{
				cards.Add(new LogicalCardState(
					instanceId++,
					$"{suit}:{rank}",
					suit,
					rank));
			}
		}

		return new InitializeMatchCommand(
			actionId,
			LifecycleGeneration: 1,
			PlayerCount: 2,
			WinningScore: 100,
			StartingHandSize: 3,
			DealEqualRanks: true,
			Deck: cards.ToImmutable(),
			CreateOpeningPresentationBarrier: createOpeningBarrier);
	}

	private static void AssertNoForbiddenGodotType(Type? type)
	{
		if (type == null)
		{
			return;
		}

		string fullName = type.FullName ?? type.Name;
		False(fullName == "Godot.Node"
			|| fullName == "Godot.GodotObject"
			|| fullName == "Godot.Texture2D"
			|| fullName == "Godot.Vector2"
			|| fullName == "Godot.Variant"
			|| fullName.StartsWith("Godot.Collections.", StringComparison.Ordinal));

		if (type.HasElementType)
		{
			AssertNoForbiddenGodotType(type.GetElementType());
		}

		if (type.IsGenericType)
		{
			foreach (Type genericArgument in type.GetGenericArguments())
			{
				AssertNoForbiddenGodotType(genericArgument);
			}
		}
	}

	private static bool ThrowsArgumentException(Action action)
	{
		try
		{
			action();
			return false;
		}
		catch (ArgumentException)
		{
			return true;
		}
	}

	private sealed class TestRuleModule : IRuleModule
	{
		internal TestRuleModule(
			string id,
			int order,
			ImmutableArray<string> dependencies = default,
			MatchCapabilitySet capabilities = default)
		{
			Id = id;
			Order = order;
			Dependencies = dependencies.IsDefault ? ImmutableArray<string>.Empty : dependencies;
			Capabilities = capabilities;
		}

		public string Id { get; }
		public int Order { get; }
		public ImmutableArray<string> Dependencies { get; }
		public MatchCapabilitySet Capabilities { get; }

		public RuleModuleDecision Evaluate(MatchCommand command, MatchReadModel readModel)
		{
			return RuleModuleDecision.Allow;
		}
	}

	private sealed class TestRuleRandomSource : IRuleRandomSource
	{
		private ulong _seed;
		private ulong _state;

		internal TestRuleRandomSource(ulong seed)
		{
			_seed = seed;
			_state = seed;
		}

		public RuleRandomState CaptureState()
		{
			return new RuleRandomState(_seed, _state);
		}

		public void RestoreState(RuleRandomState state)
		{
			_seed = state.Seed;
			_state = state.State;
		}

		public int NextIntInclusive(int minimum, int maximum)
		{
			if (minimum > maximum)
			{
				throw new ArgumentOutOfRangeException(nameof(minimum));
			}

			_state = unchecked(_state * 6364136223846793005UL + 1442695040888963407UL);
			ulong range = (ulong)((long)maximum - minimum + 1);
			return minimum + (int)(_state % range);
		}
	}
}
