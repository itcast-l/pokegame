using System.Collections.Immutable;
using System.Reflection;
using Deckland.Rules;

internal static partial class Program
{
	private static void AuthorityItemCommandsAreAtomic()
	{
		AuthorityRuleSession disabledSession = CreateItemCommandSession(itemRulesEnabled: false);
		MatchReadModel disabledBefore = disabledSession.ReadModel;
		RuleCommandResult disabled = disabledSession.Submit(new PlayItemFromHandCommand(
			251,
			disabledBefore.Revision,
			disabledBefore.LifecycleGeneration,
			disabledBefore.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "bomb"));
		False(disabled.Accepted);
		Equal(RuleRejectionCode.ModuleRejected, disabled.Rejection.Code);
		AssertReadModelsEquivalent(disabledBefore, disabled.ReadModel);

		AuthorityRuleSession session = CreateItemCommandSession(itemRulesEnabled: true);
		MatchReadModel before = session.ReadModel;
		RuleCommandResult wrongItem = session.Submit(new PlayItemFromHandCommand(
			252,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "lens"));
		False(wrongItem.Accepted);
		AssertReadModelsEquivalent(before, wrongItem.ReadModel);

		RuleCommandResult played = session.Submit(new PlayItemFromHandCommand(
			253,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "bomb",
			Destination: RuleItemPlayDestination.Table,
			TableIndex: 0,
			TableItemFacesUp: false));
		True(played.Accepted);
		Equal(1, played.ReadModel.Players[0].ItemHand.Length);
		Equal("bomb", played.ReadModel.TableCards[0].ItemOverlay!.Value.Id);
		Equal(PresentationIntentKind.ItemPlayed, played.PresentationIntents[0].Kind);

		RuleCommandResult invalidGrant = session.Submit(new GrantItemToHandCommand(
			254,
			played.Revision,
			played.ReadModel.LifecycleGeneration,
			played.TurnGeneration,
			Seat: 0,
			ItemId: "reward",
			MaximumHandSize: 3,
			DiscardDisplayIndex: 0,
			ExpectedDiscardCardRuntimeInstanceId: 999));
		False(invalidGrant.Accepted);
		AssertReadModelsEquivalent(played.ReadModel, invalidGrant.ReadModel);

		RuleCommandResult granted = session.Submit(new GrantItemToHandCommand(
			255,
			played.Revision,
			played.ReadModel.LifecycleGeneration,
			played.TurnGeneration,
			Seat: 0,
			ItemId: "reward",
			MaximumHandSize: 3,
			DiscardDisplayIndex: 0,
			ExpectedDiscardCardRuntimeInstanceId: 1));
		True(granted.Accepted);
		Equal(1, granted.ReadModel.Players[0].Hand.Length);
		Equal(2, granted.ReadModel.Players[0].ItemHand.Length);
		Equal("reward", granted.ReadModel.Players[0].ItemHand[1].Id);
		Equal(PresentationIntentKind.HandCardDiscarded, granted.PresentationIntents[0].Kind);
		Equal(PresentationIntentKind.ItemGranted, granted.PresentationIntents[1].Kind);

		RuleCommandResult attached = session.Submit(new AttachItemToHandCardCommand(
			256,
			granted.Revision,
			granted.ReadModel.LifecycleGeneration,
			granted.TurnGeneration,
			Seat: 0,
			HandIndex: 0,
			ItemId: "magnet"));
		True(attached.Accepted);
		Equal(2L, attached.ReadModel.Players[0].Hand[0].RuntimeInstanceId);
		Equal("magnet", attached.ReadModel.Players[0].Hand[0].NormalizedAttachedEffectIds.Single());

		RuleCommandResult replaced = session.Submit(new ReplaceItemInHandCommand(
			257,
			attached.Revision,
			attached.ReadModel.LifecycleGeneration,
			attached.TurnGeneration,
			Seat: 0,
			ItemHandIndex: 0,
			ExpectedItemId: "lens",
			ReplacementItemId: "rerolled"));
		True(replaced.Accepted);
		Equal("rerolled", replaced.ReadModel.Players[0].ItemHand[0].Id);

		int rewardDisplayIndex = replaced.ReadModel.Players[0].Hand.Length + 1;
		RuleCommandResult discarded = session.Submit(new DiscardHandCardCommand(
			258,
			replaced.Revision,
			replaced.ReadModel.LifecycleGeneration,
			replaced.TurnGeneration,
			Seat: 0,
			DisplayIndex: rewardDisplayIndex,
			ExpectedItemId: "reward"));
		True(discarded.Accepted);
		Equal(1, discarded.ReadModel.Players[0].ItemHand.Length);
		Equal("reward", discarded.Mutation.LogicalItemId);
	}

	private static void AuthorityItemRandomSelectionAdvancesOnlyThroughCommands()
	{
		var random = new TestRuleRandomSource(77);
		AuthorityRuleSession session = CreateItemCommandSession(
			itemRulesEnabled: true,
			random);
		MatchReadModel before = session.ReadModel;
		RuleRandomState randomBefore = random.CaptureState();
		RuleCommandResult invalid = session.Submit(new ChooseRandomHandCardIndexCommand(
			261,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			CandidateHandIndexes: ImmutableArray.Create(0, 99)));
		False(invalid.Accepted);
		Equal(randomBefore, random.CaptureState());
		AssertReadModelsEquivalent(before, invalid.ReadModel);

		RuleCommandResult selected = session.Submit(new ChooseRandomHandCardIndexCommand(
			262,
			before.Revision,
			before.LifecycleGeneration,
			before.TurnGeneration,
			Seat: 0,
			CandidateHandIndexes: ImmutableArray.Create(0, 1)));
		True(selected.Accepted);
		True(selected.Mutation.SourceIndex is 0 or 1);
		False(randomBefore.Equals(random.CaptureState()));
		Equal(2, selected.ReadModel.Players[0].Hand.Length);
	}

	private static void AuthorityGameplayRandomCommandsPreserveSequenceAndAtomicity()
	{
		var random = new TestRuleRandomSource(79);
		var expectedRandom = new TestRuleRandomSource(79);
		var session = new AuthorityRuleSession(random, CreateItemRuleProfile());
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray.Create(
					new LogicalCardState(10, "clubs:2", "clubs", "2"),
					new LogicalCardState(11, "clubs:3", "clubs", "3"),
					new LogicalCardState(12, "clubs:4", "clubs", "4"),
					new LogicalCardState(13, "clubs:5", "clubs", "5")),
				ImmutableArray.Create(new LogicalCardState(1, "hearts:4", "hearts", "4")),
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray.Create(new LogicalCardState(3, "spades:9", "spades", "9")),
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var table = ImmutableArray.Create(
			CreateRuleTableCardState(100, 20, "diamonds:2"),
			CreateRuleTableCardState(101, 21, "diamonds:3"),
			CreateRuleTableCardState(102, 22, "diamonds:4"));
		True(session.Submit(new InitializeScriptedMatchCommand(
			263,
			0,
			1,
			0,
			2,
			100,
			players,
			table)).Accepted);

		MatchReadModel beforeInvalid = session.ReadModel;
		RuleRandomState randomBeforeInvalid = random.CaptureState();
		RuleCommandResult invalidWeighted = session.Submit(new ChooseWeightedOptionCommand(
			264,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			Options: ImmutableArray.Create(
				new RuleWeightedOption("bomb", 1),
				new RuleWeightedOption("bomb", 2))));
		False(invalidWeighted.Accepted);
		Equal(randomBeforeInvalid, random.CaptureState());
		AssertReadModelsEquivalent(beforeInvalid, invalidWeighted.ReadModel);

		RuleCommandResult targetSeat = session.Submit(new ChooseRandomTargetSeatCommand(
			265,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			CandidateSeats: ImmutableArray.Create(1)));
		True(targetSeat.Accepted);
		Equal(1, targetSeat.Mutation.SourceIndex);
		expectedRandom.NextIntInclusive(0, 0);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		RuleCommandResult targetHand = session.Submit(new ChooseRandomHandDisplayIndexCommand(
			266,
			targetSeat.Revision,
			targetSeat.ReadModel.LifecycleGeneration,
			targetSeat.TurnGeneration,
			ActingSeat: 0,
			HandSeat: 1));
		True(targetHand.Accepted);
		Equal(0, targetHand.Mutation.SourceIndex);
		expectedRandom.NextIntInclusive(0, 0);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		string expectedWeightedId = string.Empty;
		if (expectedRandom.NextIntInclusive(1, 2) <= 2)
		{
			expectedWeightedId = "bomb";
		}
		if (expectedRandom.NextIntInclusive(1, 5) <= 3)
		{
			expectedWeightedId = "lens";
		}
		RuleCommandResult weighted = session.Submit(new ChooseWeightedOptionCommand(
			267,
			targetHand.Revision,
			targetHand.ReadModel.LifecycleGeneration,
			targetHand.TurnGeneration,
			ActingSeat: 0,
			Options: ImmutableArray.Create(
				new RuleWeightedOption("bomb", 2),
				new RuleWeightedOption("lens", 3),
				new RuleWeightedOption("magnet", 0))));
		True(weighted.Accepted);
		Equal(expectedWeightedId, weighted.Mutation.LogicalItemId);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		int expectedTableIndex = 0;
		if (expectedRandom.NextIntInclusive(1, 1) == 1)
		{
			expectedTableIndex = 0;
		}
		if (expectedRandom.NextIntInclusive(1, 2) == 1)
		{
			expectedTableIndex = 2;
		}
		RuleCommandResult tableCard = session.Submit(new ChooseRandomTableCardIndexCommand(
			268,
			weighted.Revision,
			weighted.ReadModel.LifecycleGeneration,
			weighted.TurnGeneration,
			ActingSeat: 0,
			Candidates: ImmutableArray.Create(
				new RuleTableCardReference(0, 100),
				new RuleTableCardReference(2, 102))));
		True(tableCard.Accepted);
		Equal(expectedTableIndex, tableCard.Mutation.SourceIndex);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		var expectedDeck = tableCard.ReadModel.Players[0].Deck
			.Select(card => card.RuntimeInstanceId)
			.ToList();
		for (int index = expectedDeck.Count - 1; index > 0; index--)
		{
			int swapIndex = expectedRandom.NextIntInclusive(0, index);
			(expectedDeck[index], expectedDeck[swapIndex]) = (expectedDeck[swapIndex], expectedDeck[index]);
		}
		int cutIndex = expectedRandom.NextIntInclusive(1, expectedDeck.Count - 1);
		expectedDeck.Reverse(0, cutIndex);
		expectedDeck.Reverse(cutIndex, expectedDeck.Count - cutIndex);
		expectedDeck.Reverse();

		RuleCommandResult shuffled = session.Submit(new ShuffleDeckCommand(
			269,
			tableCard.Revision,
			tableCard.ReadModel.LifecycleGeneration,
			tableCard.TurnGeneration,
			ActingSeat: 0,
			DeckSeat: 0,
			CutAfterShuffle: true));
		True(shuffled.Accepted);
		True(expectedDeck.SequenceEqual(
			shuffled.ReadModel.Players[0].Deck.Select(card => card.RuntimeInstanceId)));
		Equal(expectedRandom.CaptureState(), random.CaptureState());
	}

	private static void SliceThreeGameplayRandomnessStaysBehindRuleCommands()
	{
		string root = FindRepositoryRoot();
		string questionCards = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.QuestionCards.cs"));
		string targetItems = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TargetItemTargeting.cs"));
		string tableTriggers = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TableCardTriggers.cs"));
		string capture = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.CaptureResolutionCoordinator.cs"));
		string tableCapture = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.TableCapture.cs"));
		string effectResolver = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.EffectResolver.cs"));
		string deck = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.Deck.cs"));

		foreach (string source in new[]
		{
			questionCards,
			targetItems,
			tableTriggers,
			capture,
			effectResolver,
		})
		{
			False(source.Contains("_rng.", StringComparison.Ordinal));
		}

		True(questionCards.Contains(
			"TrySubmitChooseWeightedOptionRuleCommand(",
			StringComparison.Ordinal));
		True(targetItems.Contains(
			"TrySubmitChooseRandomTargetSeatRuleCommand(",
			StringComparison.Ordinal));
		True(targetItems.Contains(
			"TrySubmitChooseRandomHandDisplayIndexRuleCommand(",
			StringComparison.Ordinal));
		True(tableTriggers.Contains(
			"TrySubmitChooseRandomTableCardIndexRuleCommand(",
			StringComparison.Ordinal));
			True(tableTriggers.Contains(
				"TrySubmitSetTableCardsFaceUpRuleCommand(",
				StringComparison.Ordinal));
			False(tableTriggers.Contains("tableCard.IsFaceUp = true", StringComparison.Ordinal));
			True(tableTriggers.Contains(
				"TrySubmitRemoveTableCardAttachmentRuleCommand(",
				StringComparison.Ordinal));
			False(tableTriggers.Contains("ClearAttachedItemEffect(", StringComparison.Ordinal));
			False(tableTriggers.Contains(".IsHiddenDuringCapture =", StringComparison.Ordinal));
			True(tableCapture.Contains(
				"TrySubmitSetTableCardsFaceUpRuleCommand(",
				StringComparison.Ordinal));
			False(tableCapture.Contains(".IsFaceUp = true", StringComparison.Ordinal));
		True(capture.Contains(
			"TrySubmitShuffleDeckRuleCommand(",
			StringComparison.Ordinal));
		True(effectResolver.Contains(
			"TrySubmitShuffleDeckRuleCommand(",
			StringComparison.Ordinal));
		False(capture.Contains("Shuffle(_playerDecks", StringComparison.Ordinal));
		False(effectResolver.Contains("Shuffle(_playerDecks", StringComparison.Ordinal));
		False(deck.Contains("private void Shuffle(", StringComparison.Ordinal));
		False(deck.Contains("private void Cut(", StringComparison.Ordinal));
	}

	private static void AuthorityItemTransfersAreOrderedAtomicAndDeterministic()
	{
		var random = new TestRuleRandomSource(81);
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
		var session = new AuthorityRuleSession(random, profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray.Create(new LogicalCardState(10, "clubs:2", "clubs", "2")),
				ImmutableArray.Create(new LogicalCardState(1, "clubs:4", "clubs", "4")),
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray.Create(
					new LogicalCardState(20, "hearts:5", "hearts", "5", ImmutableArray.Create("magnet")),
					new LogicalCardState(21, "spades:6", "spades", "6"),
					new LogicalCardState(22, "diamonds:7", "diamonds", "7")),
				ImmutableArray.Create(new LogicalCardState(3, "spades:9", "spades", "9")),
				ImmutableArray.Create(new LogicalItemState("lens")),
				0));
		True(session.Submit(new InitializeScriptedMatchCommand(
			270,
			0,
			1,
			0,
			2,
			100,
			players,
			ImmutableArray<TableCardState>.Empty)).Accepted);

		MatchReadModel beforeInvalidHand = session.ReadModel;
		RuleRandomState randomBeforeInvalidHand = random.CaptureState();
		RuleCommandResult invalidHand = session.Submit(new TransferHandCardCommand(
			271,
			beforeInvalidHand.Revision,
			beforeInvalidHand.LifecycleGeneration,
			beforeInvalidHand.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			SourceDisplayIndex: 0,
			MaximumHandSize: 4,
			ExpectedCardRuntimeInstanceId: 999));
		False(invalidHand.Accepted);
		AssertReadModelsEquivalent(beforeInvalidHand, invalidHand.ReadModel);
		Equal(randomBeforeInvalidHand, random.CaptureState());

		RuleCommandResult transferredItem = session.Submit(new TransferHandCardCommand(
			272,
			beforeInvalidHand.Revision,
			beforeInvalidHand.LifecycleGeneration,
			beforeInvalidHand.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			SourceDisplayIndex: 1,
			MaximumHandSize: 4,
			ExpectedItemId: "lens"));
		True(transferredItem.Accepted);
		Equal("lens", transferredItem.ReadModel.Players[0].ItemHand.Single().Id);
		Equal(0, transferredItem.ReadModel.Players[1].ItemHand.Length);
		Equal(PresentationIntentKind.HandCardTransferred, transferredItem.PresentationIntents.Single().Kind);

		RuleCommandResult transferredCard = session.Submit(new TransferHandCardCommand(
			273,
			transferredItem.Revision,
			transferredItem.ReadModel.LifecycleGeneration,
			transferredItem.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			SourceDisplayIndex: 0,
			MaximumHandSize: 4,
			ExpectedCardRuntimeInstanceId: 3));
		True(transferredCard.Accepted);
		Equal(3L, transferredCard.ReadModel.Players[0].Hand[1].RuntimeInstanceId);
		Equal(0, transferredCard.ReadModel.Players[1].Hand.Length);

		MatchReadModel beforeInvalidDeck = transferredCard.ReadModel;
		RuleRandomState randomBeforeInvalidDeck = random.CaptureState();
		RuleCommandResult invalidDeck = session.Submit(new TransferDeckCardsCommand(
			274,
			beforeInvalidDeck.Revision,
			beforeInvalidDeck.LifecycleGeneration,
			beforeInvalidDeck.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			MaximumCount: 2,
			PreserveCardEffectsWhenEnteringDeck: false,
			ExpectedSourceTopCardRuntimeInstanceIds: ImmutableArray.Create(999L, 21L)));
		False(invalidDeck.Accepted);
		AssertReadModelsEquivalent(beforeInvalidDeck, invalidDeck.ReadModel);
		Equal(randomBeforeInvalidDeck, random.CaptureState());

		RuleCommandResult transferredDeck = session.Submit(new TransferDeckCardsCommand(
			275,
			beforeInvalidDeck.Revision,
			beforeInvalidDeck.LifecycleGeneration,
			beforeInvalidDeck.TurnGeneration,
			ActingSeat: 0,
			SourceSeat: 1,
			MaximumCount: 2,
			PreserveCardEffectsWhenEnteringDeck: false,
			ExpectedSourceTopCardRuntimeInstanceIds: ImmutableArray.Create(20L, 21L)));
		True(transferredDeck.Accepted);
		Equal(3, transferredDeck.ReadModel.Players[0].Deck.Length);
		Equal(1, transferredDeck.ReadModel.Players[1].Deck.Length);
		Equal(2, transferredDeck.Mutation.CollectedCardCount);
		Equal(PresentationIntentKind.DeckCardsTransferred, transferredDeck.PresentationIntents.Single().Kind);
		LogicalCardState transferredEnchantedCard = transferredDeck.ReadModel.Players[0].Deck.Single(
			card => card.RuntimeInstanceId == 20);
		Equal(0, transferredEnchantedCard.NormalizedAttachedEffectIds.Length);
		False(randomBeforeInvalidDeck.Equals(random.CaptureState()));
	}

	private static void AuthorityTableItemCommandsAreOrderedAtomicAndDeterministic()
	{
		var random = new TestRuleRandomSource(91);
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
		var session = new AuthorityRuleSession(random, profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			new TableCardState(
				100,
				new LogicalCardState(
					10,
					"clubs:2",
					"clubs",
					"2",
					ImmutableArray.Create("overtime")),
				false,
				false,
				false,
				false,
				null,
				-1,
				1,
				false),
			new TableCardState(
				101,
				new LogicalCardState(11, "hearts:3", "hearts", "3"),
				false,
				false,
				false,
				false,
				null,
				-1,
				1,
				false),
			new TableCardState(
				102,
				null,
				false,
				false,
				false,
				false,
				new LogicalItemState("bomb"),
				1,
				-1,
				false));
		True(session.Submit(new InitializeScriptedMatchCommand(
			280,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards)).Accepted);

		MatchReadModel beforePlace = session.ReadModel;
		RuleCommandResult placed = session.Submit(new PlaceTableItemCommand(
			281,
			beforePlace.Revision,
			beforePlace.LifecycleGeneration,
			beforePlace.TurnGeneration,
			ActingSeat: 0,
			ItemId: "question_mark",
			TableIndex: 3,
			IsFaceUp: true));
		True(placed.Accepted);
		Equal(4, placed.ReadModel.TableCards.Length);
		Equal("question_mark", placed.ReadModel.TableCards[3].ItemOverlay!.Value.Id);
		Equal(PresentationIntentKind.TableItemPlaced, placed.PresentationIntents.Single().Kind);

		RuleRandomState randomBeforeInvalidChoice = random.CaptureState();
		RuleCommandResult invalidChoice = session.Submit(new ChooseTableCardsCommand(
			282,
			placed.Revision,
			placed.ReadModel.LifecycleGeneration,
			placed.TurnGeneration,
			ActingSeat: 0,
			Candidates: ImmutableArray.Create(new RuleTableCardReference(0, 999)),
			MaximumCount: 1));
		False(invalidChoice.Accepted);
		AssertReadModelsEquivalent(placed.ReadModel, invalidChoice.ReadModel);
		Equal(randomBeforeInvalidChoice, random.CaptureState());

		RuleCommandResult chosen = session.Submit(new ChooseTableCardsCommand(
			283,
			placed.Revision,
			placed.ReadModel.LifecycleGeneration,
			placed.TurnGeneration,
			ActingSeat: 0,
			Candidates: ImmutableArray.Create(
				new RuleTableCardReference(0, 100),
				new RuleTableCardReference(1, 101)),
			MaximumCount: 1));
		True(chosen.Accepted);
		Equal(1, chosen.PresentationIntents.Length);
		Equal(PresentationIntentKind.TableCardsSelected, chosen.PresentationIntents[0].Kind);
		False(randomBeforeInvalidChoice.Equals(random.CaptureState()));

		int chosenIndex = chosen.PresentationIntents[0].TargetIndex;
		long chosenRuntimeId = chosen.PresentationIntents[0].LogicalEntityId;
		RuleCommandResult revealed = session.Submit(new SetTableCardsFaceUpCommand(
			284,
			chosen.Revision,
			chosen.ReadModel.LifecycleGeneration,
			chosen.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(new RuleTableCardReference(chosenIndex, chosenRuntimeId)),
			ExpectedFaceUp: false,
			FaceUp: true));
		True(revealed.Accepted);
		True(revealed.ReadModel.TableCards[chosenIndex].IsFaceUp);

		int otherIndex = chosenIndex == 0 ? 1 : 0;
		long otherRuntimeId = revealed.ReadModel.TableCards[otherIndex].RuntimeInstanceId;
		RuleCommandResult invalidBatchReveal = session.Submit(new SetTableCardsFaceUpCommand(
			285,
			revealed.Revision,
			revealed.ReadModel.LifecycleGeneration,
			revealed.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(
				new RuleTableCardReference(chosenIndex, chosenRuntimeId),
				new RuleTableCardReference(otherIndex, otherRuntimeId)),
			ExpectedFaceUp: false,
			FaceUp: true));
		False(invalidBatchReveal.Accepted);
		AssertReadModelsEquivalent(revealed.ReadModel, invalidBatchReveal.ReadModel);
		False(invalidBatchReveal.ReadModel.TableCards[otherIndex].IsFaceUp);

		RuleCommandResult hidden = session.Submit(new SetTableCardsHiddenCommand(
			286,
			revealed.Revision,
			revealed.ReadModel.LifecycleGeneration,
			revealed.TurnGeneration,
			ActingSeat: 0,
			TableCards: ImmutableArray.Create(new RuleTableCardReference(otherIndex, otherRuntimeId)),
			ExpectedHidden: false,
			Hidden: true));
		True(hidden.Accepted);
		True(hidden.ReadModel.TableCards[otherIndex].IsHiddenDuringCapture);

		RuleCommandResult invalidDetach = session.Submit(new RemoveTableCardAttachmentCommand(
			296,
			hidden.Revision,
			hidden.ReadModel.LifecycleGeneration,
			hidden.TurnGeneration,
			ActingSeat: 0,
			TableIndex: 0,
			ExpectedTableCardRuntimeInstanceId: 100,
			ItemId: "double"));
		False(invalidDetach.Accepted);
		AssertReadModelsEquivalent(hidden.ReadModel, invalidDetach.ReadModel);

		RuleCommandResult detached = session.Submit(new RemoveTableCardAttachmentCommand(
			297,
			hidden.Revision,
			hidden.ReadModel.LifecycleGeneration,
			hidden.TurnGeneration,
			ActingSeat: 0,
			TableIndex: 0,
			ExpectedTableCardRuntimeInstanceId: 100,
			ItemId: "overtime"));
		True(detached.Accepted);
		Equal(0, detached.ReadModel.TableCards[0].Card!.Value.NormalizedAttachedEffectIds.Length);
		Equal("overtime", detached.Mutation.LogicalItemId);

		long placedRuntimeId = placed.Mutation.LogicalEntityId;
		RuleCommandResult moved = session.Submit(new MoveTableCardCommand(
			287,
			detached.Revision,
			detached.ReadModel.LifecycleGeneration,
			detached.TurnGeneration,
			ActingSeat: 0,
			SourceIndex: 3,
			TargetIndex: 0,
			ExpectedRuntimeInstanceId: placedRuntimeId));
		True(moved.Accepted);
		Equal(placedRuntimeId, moved.ReadModel.TableCards[0].RuntimeInstanceId);

		ImmutableArray<long> removalIds = moved.ReadModel.TableCards
			.Take(2)
			.Select(card => card.RuntimeInstanceId)
			.ToImmutableArray();
		RuleCommandResult invalidRemoval = session.Submit(new RemoveTableCardRangeCommand(
			288,
			moved.Revision,
			moved.ReadModel.LifecycleGeneration,
			moved.TurnGeneration,
			ActingSeat: 0,
			StartIndex: 0,
			ExpectedRuntimeInstanceIds: removalIds.SetItem(1, 999)));
		False(invalidRemoval.Accepted);
		AssertReadModelsEquivalent(moved.ReadModel, invalidRemoval.ReadModel);

		RuleCommandResult removed = session.Submit(new RemoveTableCardRangeCommand(
			289,
			moved.Revision,
			moved.ReadModel.LifecycleGeneration,
			moved.TurnGeneration,
			ActingSeat: 0,
			StartIndex: 0,
			ExpectedRuntimeInstanceIds: removalIds));
		True(removed.Accepted);
		Equal(2, removed.ReadModel.TableCards.Length);
		Equal(2, removed.Mutation.RemovedTableCardCount);
		Equal(PresentationIntentKind.TableCardsRemoved, removed.PresentationIntents.Single().Kind);
	}

	private static void AuthorityTableShufflePlanSurvivesBarriersAndSnapshots()
	{
		var random = new TestRuleRandomSource(101);
		var profile = new MatchRuleProfile(new IRuleModule[]
		{
			new TestRuleModule(
				"items",
				0,
				capabilities: new MatchCapabilitySet(MatchCapability.ItemRules)),
		});
		var session = new AuthorityRuleSession(random, profile);
		var players = ImmutableArray.Create(
			new PlayerStateSnapshot(
				0,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0),
			new PlayerStateSnapshot(
				1,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalCardState>.Empty,
				ImmutableArray<LogicalItemState>.Empty,
				0));
		var tableCards = ImmutableArray.Create(
			CreateRuleTableCardState(300, 30, "clubs:2"),
			CreateRuleTableCardState(301, 31, "hearts:3"),
			CreateRuleTableCardState(302, 32, "spades:4"),
			CreateRuleTableCardState(303, 33, "diamonds:5"));
		True(session.Submit(new InitializeScriptedMatchCommand(
			290,
			0,
			1,
			0,
			2,
			100,
			players,
			tableCards)).Accepted);

		ImmutableArray<RuleTableCardReference> references = session.ReadModel.TableCards
			.Select((card, index) => new RuleTableCardReference(index, card.RuntimeInstanceId))
			.ToImmutableArray();
		MatchReadModel beforeInvalid = session.ReadModel;
		RuleRandomState randomBeforeInvalid = random.CaptureState();
		RuleCommandResult invalid = session.Submit(new PlanTableShuffleCommand(
			291,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			TableCards: references,
			FixedTableIndexes: ImmutableArray.Create(1, 1)));
		False(invalid.Accepted);
		AssertReadModelsEquivalent(beforeInvalid, invalid.ReadModel);
		Equal(randomBeforeInvalid, random.CaptureState());

		RuleCommandResult planned = session.Submit(new PlanTableShuffleCommand(
			292,
			beforeInvalid.Revision,
			beforeInvalid.LifecycleGeneration,
			beforeInvalid.TurnGeneration,
			ActingSeat: 0,
			TableCards: references,
			FixedTableIndexes: ImmutableArray.Create(1)));
		True(planned.Accepted);
		True(planned.ReadModel.HasPendingPresentationBarrier);
		Equal(4, planned.PresentationIntents.Length);
		True(planned.PresentationIntents.All(intent =>
			intent.Kind == PresentationIntentKind.TableShufflePlanned
			&& intent.BarrierId == planned.ReadModel.PendingPresentationBarrierId));
		ImmutableArray<long> plannedOrder = planned.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds;
		Equal(4, plannedOrder.Length);
		Equal(301L, plannedOrder[1]);
		False(plannedOrder.SequenceEqual(references.Select(reference => reference.RuntimeInstanceId)));
		False(randomBeforeInvalid.Equals(random.CaptureState()));

		MatchStateSnapshot snapshot = session.CaptureSnapshot();
		var restoredRandom = new TestRuleRandomSource(999);
		AuthorityRuleSession restored = AuthorityRuleSession.Restore(snapshot, restoredRandom, profile);
		True(restored.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds.SequenceEqual(plannedOrder));
		Equal(snapshot.RandomState, restoredRandom.CaptureState());

		RuleCommandResult blocked = restored.Submit(new MoveTableCardCommand(
			293,
			restored.ReadModel.Revision,
			restored.ReadModel.LifecycleGeneration,
			restored.ReadModel.TurnGeneration,
			ActingSeat: 0,
			SourceIndex: 0,
			TargetIndex: 3,
			ExpectedRuntimeInstanceId: 300));
		False(blocked.Accepted);
		Equal(RuleRejectionCode.PresentationBarrierPending, blocked.Rejection.Code);

		MatchReadModel beforeWrongCompletion = restored.ReadModel;
		RuleCommandResult wrongCompletion = restored.Submit(new CompletePresentationBarrierCommand(
			294,
			beforeWrongCompletion.Revision,
			beforeWrongCompletion.LifecycleGeneration,
			beforeWrongCompletion.TurnGeneration,
			beforeWrongCompletion.PendingPresentationBarrierId + 1));
		False(wrongCompletion.Accepted);
		AssertReadModelsEquivalent(beforeWrongCompletion, wrongCompletion.ReadModel);

		RuleCommandResult completed = restored.Submit(new CompletePresentationBarrierCommand(
			295,
			beforeWrongCompletion.Revision,
			beforeWrongCompletion.LifecycleGeneration,
			beforeWrongCompletion.TurnGeneration,
			beforeWrongCompletion.PendingPresentationBarrierId));
		True(completed.Accepted);
		False(completed.ReadModel.HasPendingPresentationBarrier);
		Equal(0, completed.ReadModel.NormalizedPendingTableOrderRuntimeInstanceIds.Length);
		True(completed.ReadModel.TableCards
			.Select(card => card.RuntimeInstanceId)
			.SequenceEqual(plannedOrder));
	}

	private static void SliceFourRandomCommandsPreserveExactSequenceAndAtomicity()
	{
		var random = new TestRuleRandomSource(103);
		var expectedRandom = new TestRuleRandomSource(103);
		AuthorityRuleSession session = CreateItemCommandSession(
			itemRulesEnabled: true,
			random);

		MatchReadModel beforeInvalidRange = session.ReadModel;
		RuleRandomState randomBeforeInvalidRange = random.CaptureState();
		RuleCommandResult invalidRange = session.Submit(new ChooseRandomIndexCommand(
			600,
			beforeInvalidRange.Revision,
			beforeInvalidRange.LifecycleGeneration,
			beforeInvalidRange.TurnGeneration,
			ActingSeat: 0,
			MinimumInclusive: 8,
			MaximumInclusive: 7));
		False(invalidRange.Accepted);
		Equal(randomBeforeInvalidRange, random.CaptureState());
		AssertReadModelsEquivalent(beforeInvalidRange, invalidRange.ReadModel);

		int expectedIndex = expectedRandom.NextIntInclusive(4, 8);
		RuleCommandResult chosenIndex = session.Submit(new ChooseRandomIndexCommand(
			601,
			beforeInvalidRange.Revision,
			beforeInvalidRange.LifecycleGeneration,
			beforeInvalidRange.TurnGeneration,
			ActingSeat: 0,
			MinimumInclusive: 4,
			MaximumInclusive: 8));
		True(chosenIndex.Accepted);
		Equal(expectedIndex, chosenIndex.Mutation.SourceIndex);
		Equal(expectedRandom.CaptureState(), random.CaptureState());

		MatchReadModel beforeInvalidReservoir = chosenIndex.ReadModel;
		RuleRandomState randomBeforeInvalidReservoir = random.CaptureState();
		RuleCommandResult invalidReservoir = session.Submit(
			new ChooseReservoirHandDisplayIndexCommand(
				602,
				beforeInvalidReservoir.Revision,
				beforeInvalidReservoir.LifecycleGeneration,
				beforeInvalidReservoir.TurnGeneration,
				ActingSeat: 0,
				HandSeat: 0,
				CandidateDisplayIndexes: ImmutableArray.Create(0, 4)));
		False(invalidReservoir.Accepted);
		Equal(randomBeforeInvalidReservoir, random.CaptureState());
		AssertReadModelsEquivalent(beforeInvalidReservoir, invalidReservoir.ReadModel);

		ImmutableArray<int> candidates = ImmutableArray.Create(0, 3, 1);
		int expectedDisplayIndex = -1;
		for (int ordinal = 0; ordinal < candidates.Length; ordinal++)
		{
			if (expectedRandom.NextIntInclusive(1, ordinal + 1) == 1)
			{
				expectedDisplayIndex = candidates[ordinal];
			}
		}
		RuleCommandResult chosenDisplayIndex = session.Submit(
			new ChooseReservoirHandDisplayIndexCommand(
				603,
				beforeInvalidReservoir.Revision,
				beforeInvalidReservoir.LifecycleGeneration,
				beforeInvalidReservoir.TurnGeneration,
				ActingSeat: 0,
				HandSeat: 0,
				CandidateDisplayIndexes: candidates));
		True(chosenDisplayIndex.Accepted);
		Equal(expectedDisplayIndex, chosenDisplayIndex.Mutation.SourceIndex);
		Equal(candidates.Length, chosenDisplayIndex.Mutation.CollectedCardCount);
		Equal(expectedRandom.CaptureState(), random.CaptureState());
	}

	private static void SliceFiveAiRandomCommandSequenceIsDeterministicAndAtomic()
	{
		var random = new TestRuleRandomSource(109);
		var expectedRandom = new TestRuleRandomSource(109);
		AuthorityRuleSession session = CreateItemCommandSession(
			itemRulesEnabled: true,
			random);

		MatchReadModel beforeRejected = session.ReadModel;
		RuleRandomState randomBeforeRejected = random.CaptureState();
		RuleCommandResult rejected = session.Submit(new ChooseRandomIndexCommand(
			700,
			beforeRejected.Revision,
			beforeRejected.LifecycleGeneration,
			beforeRejected.TurnGeneration,
			ActingSeat: 1,
			MinimumInclusive: 0,
			MaximumInclusive: 99));
		False(rejected.Accepted);
		Equal(RuleRejectionCode.WrongTurn, rejected.Rejection.Code);
		Equal(randomBeforeRejected, random.CaptureState());
		AssertReadModelsEquivalent(beforeRejected, rejected.ReadModel);

		(int MinimumInclusive, int MaximumInclusive)[] aiRanges =
		{
			(0, 99),
			(0, int.MaxValue),
			(0, int.MaxValue),
			(0, 99),
			(0, int.MaxValue),
			(0, 6),
			(0, int.MaxValue),
			(0, int.MaxValue),
			(0, int.MaxValue),
			(0, 99),
			(0, 9999),
			(0, 4),
			(0, int.MaxValue),
		};
		for (int index = 0; index < aiRanges.Length; index++)
		{
			(int minimumInclusive, int maximumInclusive) = aiRanges[index];
			int expectedIndex = expectedRandom.NextIntInclusive(
				minimumInclusive,
				maximumInclusive);
			MatchReadModel beforeCommand = session.ReadModel;
			RuleCommandResult result = session.Submit(new ChooseRandomIndexCommand(
				701 + index,
				beforeCommand.Revision,
				beforeCommand.LifecycleGeneration,
				beforeCommand.TurnGeneration,
				ActingSeat: 0,
				minimumInclusive,
				maximumInclusive));

			True(result.Accepted);
			Equal(expectedIndex, result.Mutation.SourceIndex);
			Equal(beforeCommand.Revision + 1, result.ReadModel.Revision);
			Equal(expectedRandom.CaptureState(), random.CaptureState());
		}
	}

}
