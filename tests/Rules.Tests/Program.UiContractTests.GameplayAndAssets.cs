using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void ResultPresentationUsesLocalSeatForNetworkDefeat()
	{
		True(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 0,
			localPlayerIndex: 2,
			isNetworkMultiplayer: true,
			adventureMode: false,
			soloMode: false));

		False(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 2,
			localPlayerIndex: 2,
			isNetworkMultiplayer: true,
			adventureMode: false,
			soloMode: false));
	}

	private static void ResultPresentationPreservesLocalWinAndOfflineRules()
	{
		False(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 0,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: false,
			soloMode: false));

		False(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 1,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: false,
			soloMode: false));

		True(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 1,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: true,
			soloMode: false));

		True(ResultPresentationRules.ShouldShowDefeatResult(
			winnerPlayerIndex: 1,
			localPlayerIndex: 0,
			isNetworkMultiplayer: false,
			adventureMode: false,
			soloMode: true));
	}

	private static void ResultPresentationEnablesNetworkReturnPrimaryAction()
	{
		True(ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			isNetworkMultiplayer: true,
			shouldAdvanceAdventure: false));

		True(ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			isNetworkMultiplayer: false,
			shouldAdvanceAdventure: false));

		True(ResultPresentationRules.ShouldEnableWinnerPrimaryAction(
			isNetworkMultiplayer: true,
			shouldAdvanceAdventure: true));
		True(ResultPresentationRules.ShouldEnableWinnerReturnAction(
			isAdventureSaveBlocking: false));
		False(ResultPresentationRules.ShouldEnableWinnerReturnAction(
			isAdventureSaveBlocking: true));
	}

	private static void RewardResolverUsesWeightedReservoirSelection()
	{
		var candidates = new[]
		{
			new RewardCandidate("bomb", 2),
			new RewardCandidate("magnet", 3),
			new RewardCandidate("lens", 5),
		};
		int[] rolls = { 2, 1, 6 };
		int rollIndex = 0;

		RewardPlan reward = RewardResolver.PickWeightedReward(candidates, maxInclusive => rolls[rollIndex++]);

		True(reward.HasReward);
		Equal("magnet", reward.ItemCardId);
	}

	private static void ItemCardEffectSupportBlocksReservedEmptyEffects()
	{
		True(ItemCardEffectSupport.IsSupportedEffectType("grant_reward"));
		False(ItemCardEffectSupport.IsSupportedEffectType("block_match"));
		False(ItemCardEffectSupport.IsSupportedEffectType("reverse_collect"));
		False(ItemCardEffectSupport.IsSupportedEffectType(string.Empty));
	}

	private static void ItemCardManifestContractsRejectInvalidPandoraData()
	{
		ItemCardDefinition validReward = CreateContractTestItemCard(
			id: "draw_card",
			effectType: CardEffectTypes.DrawCard,
			effectValues: "count=1",
			canBeQuestionReward: true,
			playableFromHand: true,
			rewardDelivery: RewardDeliveryMode.Hand);
		True(ItemCardManifestValidationRules.ValidatePandoraEntries(
			new[] { CreatePandoraEntry(validReward) },
			new[] { "draw_card" }).IsValid);

		ItemCardPandoraManifestEntry missingField = new(
			validReward.Id,
			new HashSet<string>(ItemCardManifestValidationRules.RequiredGameplayProperties, StringComparer.Ordinal)
			{
				"effect_values",
			},
			validReward);
		((HashSet<string>)missingField.PresentGameplayProperties).Remove("effect_values");
		False(ItemCardManifestValidationRules.ValidatePandoraEntries(
			new[] { missingField },
			new[] { validReward.Id }).IsValid);

		ItemCardPandoraManifestEntry duplicate = CreatePandoraEntry(validReward);
		False(ItemCardManifestValidationRules.ValidatePandoraEntries(
			new[] { CreatePandoraEntry(validReward), duplicate },
			new[] { validReward.Id }).IsValid);
		False(ItemCardManifestValidationRules.ValidatePandoraEntries(
			new[] { CreatePandoraEntry(validReward) },
			Array.Empty<string>()).IsValid);
		False(ItemCardManifestValidationRules.ValidatePandoraEntries(
			new[] { CreatePandoraEntry(validReward) },
			new[] { validReward.Id, "extra_visual" }).IsValid);

		False(ItemCardEffectContractRegistry.Validate(
			CreateContractTestItemCard(
				id: "unknown_effect",
				effectType: "unknown_effect",
				effectValues: "value=1",
				canBeQuestionReward: false,
				playableFromHand: false,
				rewardDelivery: RewardDeliveryMode.None)).IsValid);
		False(ItemCardEffectContractRegistry.Validate(
			CreateContractTestItemCard(
				id: "unknown_presentation",
				effectType: CardEffectTypes.DrawCard,
				effectValues: "count=1",
				playPresentation: "unknown_presentation",
				canBeQuestionReward: true,
				playableFromHand: true,
				rewardDelivery: RewardDeliveryMode.Hand)).IsValid);
		False(ItemCardEffectContractRegistry.Validate(
			CreateContractTestItemCard(
				id: "unknown_value",
				effectType: CardEffectTypes.DrawCard,
				effectValues: "count=1;unknown=2",
				canBeQuestionReward: true,
				playableFromHand: true,
				rewardDelivery: RewardDeliveryMode.Hand)).IsValid);
		False(ItemCardEffectContractRegistry.Validate(
			CreateContractTestItemCard(
				id: "illegal_delivery",
				effectType: CardEffectTypes.DrawCard,
				effectValues: "count=1",
				canBeQuestionReward: true,
				playableFromHand: true,
				rewardDelivery: RewardDeliveryMode.ImmediateAttachment)).IsValid);

		ItemCardDefinition attachmentReward = CreateContractTestItemCard(
			id: "magnet",
			effectType: CardEffectTypes.AttachModifier,
			effectValues: "modifier=magnet",
			canBeQuestionReward: true,
			playableFromHand: false,
			rewardDelivery: RewardDeliveryMode.ImmediateAttachment);
		True(ItemCardEffectContractRegistry.Validate(attachmentReward).IsValid);
		ItemCardDefinition invalidHandAttachmentReward = CreateContractTestItemCard(
			id: "suit_collect",
			effectType: CardEffectTypes.AttachModifier,
			effectValues: "modifier=suit_collect",
			canBeQuestionReward: true,
			playableFromHand: true,
			rewardDelivery: RewardDeliveryMode.Hand);
		False(ItemCardEffectContractRegistry.Validate(invalidHandAttachmentReward).IsValid);
		False(ItemCardEffectContractRegistry.Validate(
			CreateContractTestItemCard(
				id: "playable_immediate_attachment",
				effectType: CardEffectTypes.AttachModifier,
				effectValues: "modifier=magnet",
				canBeQuestionReward: true,
				playableFromHand: true,
				rewardDelivery: RewardDeliveryMode.ImmediateAttachment)).IsValid);
		string root = FindRepositoryRoot();
		string catalogSource = File.ReadAllText(Path.Combine(root, "scripts", "ItemCardCatalog.cs"));
		string loadingSource = File.ReadAllText(Path.Combine(root, "scripts", "ItemCardCatalog.Loading.cs"));
		string syncTool = File.ReadAllText(Path.Combine(root, "tools", "ItemCardManifestSync.gd"));
		True(catalogSource.Contains("public static bool TryLoad", StringComparison.Ordinal));
		True(catalogSource.Contains("public static ItemCardCatalog LoadRequired", StringComparison.Ordinal));
		False(catalogSource.Contains("LoadedFromPandora", StringComparison.Ordinal));
		False(loadingSource.Contains("ShouldUseFallback", StringComparison.Ordinal));
		False(syncTool.Contains("args.has(\"--write\")", StringComparison.Ordinal));

		True(ItemCardEffectContractRegistry.HasExactResolverCoverage(
			ItemCardEffectContractRegistry.ResolverEffectTypes,
			out _));
		True(ItemCardEffectContractRegistry.HasExactPresentationCoverage(
			ItemCardEffectContractRegistry.CustomPresentationTypes,
			ItemCardEffectContractRegistry.GenericPresentations,
			out _));
	}

	private static ItemCardPandoraManifestEntry CreatePandoraEntry(ItemCardDefinition definition)
	{
		return new ItemCardPandoraManifestEntry(
			definition.Id,
			ItemCardManifestValidationRules.RequiredGameplayProperties,
			definition);
	}

	private static void ItemCardCatalogPreflightFailuresRemainRetryableAndGateStarts()
	{
		string root = FindRepositoryRoot();
		string catalog = File.ReadAllText(Path.Combine(root, "scripts", "ItemCardCatalog.cs"));
		int failedBuild = catalog.IndexOf("if (!TryBuild(", StringComparison.Ordinal);
		int cacheSuccess = catalog.IndexOf("_cached = catalog;", StringComparison.Ordinal);
		True(failedBuild >= 0 && cacheSuccess > failedBuild);
		False(catalog[..cacheSuccess].Contains("_cached = default", StringComparison.Ordinal));

		string pandora = File.ReadAllText(Path.Combine(root, "scripts", "ItemCardCatalog.Pandora.cs"));
		True(pandora.Contains("Pandora is not loaded.", StringComparison.Ordinal));
		True(pandora.Contains("Missing Pandora category", StringComparison.Ordinal));
		True(pandora.Contains("category is empty", StringComparison.Ordinal));
		string visuals = File.ReadAllText(Path.Combine(root, "scripts", "ItemCardCatalog.Resources.cs"));
		True(visuals.Contains("Duplicate item card visual ID", StringComparison.Ordinal));
		True(visuals.Contains("Invalid item card visual resource", StringComparison.Ordinal));

		string mainMenu = File.ReadAllText(Path.Combine(root, "scripts", "MainMenuScene.PlayerCountDialog.cs"));
		string adventure = File.ReadAllText(Path.Combine(root, "scripts", "AdventureAvatarMenuScene.Navigation.cs"));
		string adventureResume = File.ReadAllText(Path.Combine(root, "scripts", "AdventureAvatarMenuScene.RunResumeDialog.cs"));
		True(mainMenu.Contains("ItemCardCatalogPreflight.TryValidate", StringComparison.Ordinal));
		True(adventure.IndexOf("EnsureItemCardCatalogReady()", StringComparison.Ordinal)
			< adventure.IndexOf("BeginNewMainRun(", StringComparison.Ordinal));
		True(adventureResume.IndexOf("EnsureItemCardCatalogReady()", StringComparison.Ordinal)
			< adventureResume.IndexOf("store.ClearRun()", StringComparison.Ordinal));

		foreach (string networkFile in new[]
		{
			"NetworkManager.cs",
			"NetworkManager.Steam.cs",
			"NetworkManager.MatchLifecycle.cs",
			"NetworkManager.SceneLoadHandshake.cs",
		})
		{
			True(File.ReadAllText(Path.Combine(root, "scripts", networkFile))
				.Contains("EnsureItemCardCatalogReady()", StringComparison.Ordinal));
		}
	}

	private static ItemCardDefinition CreateContractTestItemCard(
		string id,
		string effectType,
		string effectValues,
		bool canBeQuestionReward,
		bool playableFromHand,
		RewardDeliveryMode rewardDelivery,
		string playPresentation = CardPlayPresentationTypes.DirectTrigger)
	{
		return new ItemCardDefinition(
			id,
			id,
			id,
			$"ITEM_DESC_{id.ToUpperInvariant()}",
			enabledInGame: true,
			playableFromHand,
			canBeQuestionReward,
			"run-004",
			string.Empty,
			4,
			1,
			"common",
			string.Empty,
			string.Empty,
			0,
			playPresentation,
			effectType == CardEffectTypes.GrantReward ? CardGameEvents.OnCollect : CardGameEvents.OnPlay,
			effectType,
			effectValues,
			0,
			string.Empty,
			CardEffectTypes.None,
			string.Empty,
			0,
			string.Empty,
			100,
			string.Empty,
			rewardDelivery);
	}

	private static void DrawItemAvailabilityUsesPostUseHandCapacityAndDeckCount()
	{
		True(ItemCardEffectSupport.CanFulfillDrawCardEffect(1, 8, 8, 1));
		False(ItemCardEffectSupport.CanFulfillDrawCardEffect(0, 8, 8, 1));
		False(ItemCardEffectSupport.CanFulfillDrawCardEffect(2, 8, 8, 2));
		True(ItemCardEffectSupport.CanFulfillDrawCardEffect(2, 7, 8, 2));
		False(ItemCardEffectSupport.CanFulfillDrawCardEffect(1, 7, 8, 2));
		False(ItemCardEffectSupport.CanFulfillDrawCardEffect(1, 0, 8, 1));
	}

	private static void PlayingCardAttachmentReplacesExistingEffect()
	{
		PlayingCard card = CreatePlayingCard()
			.WithAttachedItemEffect("magnet")
			.WithAttachedItemEffect("double");

		True(card.HasItemEffect);
		False(card.HasAttachedItemEffect("magnet"));
		True(card.HasAttachedItemEffect("double"));
		Equal("double", card.AttachedItemCardIdsCsv);
		Equal(1, card.GetAttachedItemCardIds().Count);
		Equal("double", card.GetAttachedItemCardIds()[0]);
	}

	private static void CaptureStateApplyCaptureClearsEffectsWhenPreserveDisabled()
	{
		GameBoardState state = CreateCaptureState(CreatePlayingCard().WithAttachedItemEffect("magnet"));
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		CaptureStateResult result = service.ApplyCapture(state, plan, playerIndex: 0, preserveCardEffectsWhenEnteringDeck: false);

		Equal(1, result.CollectedCardCount);
		Equal(1, state.PlayerDecks[0].Count);
		False(state.PlayerDecks[0][0].HasItemEffect);
		Equal(0, state.TableCards.Count);
	}

	private static void CaptureStateApplyCapturePreservesEffectsWhenPreserveEnabled()
	{
		GameBoardState state = CreateCaptureState(CreatePlayingCard().WithAttachedItemEffect("magnet"));
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		CaptureStateResult result = service.ApplyCapture(state, plan, playerIndex: 0, preserveCardEffectsWhenEnteringDeck: true);

		Equal(1, result.CollectedCardCount);
		Equal(1, state.PlayerDecks[0].Count);
		True(state.PlayerDecks[0][0].HasAttachedItemEffect("magnet"));
		Equal("magnet", state.PlayerDecks[0][0].AttachedItemCardIdsCsv);
		Equal(0, state.TableCards.Count);
	}

	private static void CaptureStateRedirectsOneCapturedCardToAnotherPlayerDeck()
	{
		var state = new GameBoardState();
		state.PlayerDecks.Add(new List<PlayingCard>());
		state.PlayerDecks.Add(new List<PlayingCard>());
			PlayingCard left = new("hearts", "2");
			PlayingCard redirected = new("spades", "10");
			PlayingCard right = new("clubs", "3");
		state.TableCards.Add(new TableCard(left, isFaceUp: true));
		state.TableCards.Add(new TableCard(redirected, isFaceUp: true));
		state.TableCards.Add(new TableCard(right, isFaceUp: true));
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		CaptureStateResult result = service.ApplyCapture(
			state,
			plan,
			playerIndex: 1,
			preserveCardEffectsWhenEnteringDeck: false,
			redirectedTableCardIndex: 1,
			redirectedPlayerIndex: 0);

		Equal(3, result.CollectedCardCount);
		Equal(3, result.RemovedTableCardCount);
		Equal(1, state.PlayerDecks[0].Count);
		Equal("10", state.PlayerDecks[0][0].Rank);
		Equal(2, state.PlayerDecks[1].Count);
		Equal(0, state.TableCards.Count);
	}

	private static void CapturedCardEffectPersistsThroughDeckAndCanBeReplaced()
	{
		GameBoardState state = CreateCaptureState(CreatePlayingCard().WithAttachedItemEffect("magnet"));
		state.PlayerHands.Add(new List<PlayingCard>());
		var service = new CaptureStateService();

		CaptureStatePlan plan = service.CreatePlan(state, 0, null);
		service.ApplyCapture(state, plan, playerIndex: 0, preserveCardEffectsWhenEnteringDeck: true);

		PlayingCard drawnCard = state.PlayerDecks[0][0];
		state.PlayerHands[0].Add(drawnCard);
		state.PlayerDecks[0].RemoveAt(0);

		state.PlayerHands[0][0] = state.PlayerHands[0][0].WithAttachedItemEffect("double");

		False(state.PlayerHands[0][0].HasAttachedItemEffect("magnet"));
		True(state.PlayerHands[0][0].HasAttachedItemEffect("double"));
		Equal("double", state.PlayerHands[0][0].AttachedItemCardIdsCsv);
	}

	private static void CaptureRefillShufflesBeforeReplacementDrawAnimation()
	{
		string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.CaptureResolutionCoordinator.cs"));
		int methodStart = source.IndexOf("private async Task<CardPlayBranchResult> ResolvePlayedCardCaptureCoreAsync(", StringComparison.Ordinal);
		int methodEnd = source.Length;
		True(methodStart >= 0);
		True(methodEnd > methodStart);

		string method = source[methodStart..methodEnd];
		int captureApplyIndex = method.IndexOf("CaptureTableCardsResult captureResult = await CaptureTableCardsAsync", StringComparison.Ordinal);
		int shuffleIndex = method.IndexOf("TrySubmitShuffleDeckRuleCommand(", captureApplyIndex, StringComparison.Ordinal);
		int cutIndex = method.IndexOf("cutAfterShuffle: true", shuffleIndex, StringComparison.Ordinal);
		int shuffleAnimationIndex = method.IndexOf("await PlayPhysicalDeckShuffleAsync(playerIndex);", cutIndex, StringComparison.Ordinal);
		int drawReplacementIndex = method.IndexOf("DrawnHandCard? replacementCard = DrawReplacementCard(playerIndex);", cutIndex, StringComparison.Ordinal);
		int drawAnimationIndex = method.IndexOf("await PlayReplacementDrawAnimationAsync(playerIndex, replacementCard, turnSequenceGeneration);", drawReplacementIndex, StringComparison.Ordinal);

		True(captureApplyIndex >= 0);
		True(shuffleIndex > captureApplyIndex);
		True(cutIndex > shuffleIndex);
		True(shuffleAnimationIndex > cutIndex);
		True(drawReplacementIndex > shuffleAnimationIndex);
		True(drawAnimationIndex > drawReplacementIndex);
	}

	private static void CaptureRevealOrderOpensEdgesBeforeMiddle()
	{
		int[] order = CaptureRevealOrder.BuildBaseRevealIndexes(captureStartIndex: 2, tableCardCount: 6);

		Equal("2,5,3,4", string.Join(",", order));
	}

	private static void PotentialCaptureIgnoresBuriedMatches()
	{
		var tableCards = new[]
		{
			TableCard("A", 2, isBuriedInSand: true),
			TableCard("A", 3),
			TableCard("Q", 4),
		};
		var card = new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false);

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(3, captureCount);
		Equal(4, points);
	}

	private static void PotentialCaptureCountsSuitCollectFromFirstMatchingSuit()
	{
		var tableCards = new[]
		{
			TableCard("2", 1, suit: "clubs"),
			TableCard("4", 4, suit: "hearts"),
			TableCard("6", 5, suit: "spades"),
			TableCard("8", 6, suit: "hearts"),
		};
		var card = new PotentialCaptureCard("8", CollectsTable: false, HasDoubleEffect: false, Suit: "hearts", CollectBySuit: true);

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(4, captureCount);
		Equal(11, points);
	}

	private static void PotentialCaptureUsesAlternateRanksForClubGraftMatching()
	{
		var tableCards = new[]
		{
			TableCard("5", 2),
			TableCard("7", 4),
			TableCard("9", 6),
		};
		var card = new PotentialCaptureCard(
			"6",
			CollectsTable: false,
			HasDoubleEffect: false,
			Suit: "",
			CollectBySuit: false,
			AlternateRanks: new[] { "5", "7" });

		int captureCount = PotentialCaptureRules.GetPotentialNormalCaptureCount(tableCards, card);
		int points = PotentialCaptureRules.GetPotentialNormalCapturePoints(tableCards, card, DefaultScoringContext);

		Equal(4, captureCount);
		Equal(10, points);
	}

	private static void PotentialCapturePrefersRoyalThreshold()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
			TableCard("2", 1),
			TableCard("3", 1),
			TableCard("K", 12),
			TableCard("Q", 12),
		};
		var handCards = new[]
		{
			new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureCard("K", CollectsTable: false, HasDoubleEffect: false),
		};

		bool picked = PotentialCaptureRules.TryPickBestCaptureCardIndex(
			tableCards,
			handCards,
			DefaultScoringContext,
			royalCaptureThreshold: 5,
			out int handIndex);

		True(picked);
		Equal(0, handIndex);
	}

	private static void PotentialCaptureIdentifiesPairOnlyCaptures()
	{
		var tableCards = new[]
		{
			TableCard("A", 1),
		};
		var card = new PotentialCaptureCard("A", CollectsTable: false, HasDoubleEffect: false);

		True(PotentialCaptureRules.IsPairOnlyCapture(tableCards, card));
	}


	private static void CharacterAudioConfigResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string audioConfigDirectory = Path.Combine(repoRoot, "resources", "character_audio");
		True(Directory.Exists(audioConfigDirectory));

		string[] configPaths = Directory.GetFiles(audioConfigDirectory, "*.tres");
		True(configPaths.Length > 0);

		int audioReferenceCount = 0;
		foreach (string configPath in configPaths)
		{
			string configText = File.ReadAllText(configPath);
			foreach (Match match in Regex.Matches(configText, "path=\"res://([^\"]+\\.(?:mp3|wav))\""))
			{
				audioReferenceCount++;
				string path = Path.Combine(repoRoot, match.Groups[1].Value.Replace('/', Path.DirectorySeparatorChar));
				True(File.Exists(path));
				True(new FileInfo(path).Length > 1024);
			}
		}

		True(audioReferenceCount > 0);
	}

	private static void LanAddressHelperComputesIpv4BroadcastAddress()
	{
		IPAddress? broadcast = LanAddressHelper.TryComputeBroadcastAddress(
			IPAddress.Parse("192.168.12.34"),
			IPAddress.Parse("255.255.255.0"));

		Equal("192.168.12.255", broadcast?.ToString());
	}

	private static void CharacterAvatarVisualResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string avatarDirectory = Path.Combine(repoRoot, "resources", "character_avatars");
		True(Directory.Exists(avatarDirectory));

		string[] avatarPaths = Directory.GetFiles(avatarDirectory, "*.tres");
		True(avatarPaths.Length > 0);

		foreach (string avatarPath in avatarPaths)
		{
			string avatarText = File.ReadAllText(avatarPath);
			string rootPath = ReadTresString(avatarText, "RootPath");
			True(!string.IsNullOrWhiteSpace(rootPath));

			int idleFrameCount = Math.Max(1, ReadTresInt(avatarText, "IdleFrameCount", 1));
			int actionFrameCount = Math.Max(1, ReadTresInt(avatarText, "ActionFrameCount", 1));
			AssertFramePatternExists(repoRoot, $"{rootPath}/idle/processed/idle-{{0}}.png", idleFrameCount);
			foreach (string action in new[] { "score", "attack", "hit", "victory", "defeat" })
			{
				AssertFramePatternExists(repoRoot, $"{rootPath}/{action}/processed/{action}-{{0}}.png", actionFrameCount);
			}

			string attackFxRoot = ReadTresString(avatarText, "AttackFxRoot");
			if (!string.IsNullOrWhiteSpace(attackFxRoot))
			{
				AssertFramePatternExists(repoRoot, $"{attackFxRoot}/processed/fx-{{0}}.png", 1);
			}

			string damageEffectFramePattern = ReadTresString(avatarText, "DamageEffectFramePattern");
			int damageEffectFrameCount = ReadTresInt(avatarText, "DamageEffectFrameCount", 0);
			if (!string.IsNullOrWhiteSpace(damageEffectFramePattern) && damageEffectFrameCount > 0)
			{
				AssertFramePatternExists(repoRoot, damageEffectFramePattern, damageEffectFrameCount);
			}
		}
	}

}
