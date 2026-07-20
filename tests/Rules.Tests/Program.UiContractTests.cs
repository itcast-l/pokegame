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

		foreach (string networkFile in new[] { "NetworkManager.cs", "NetworkManager.Steam.cs", "NetworkManager.Lobby.cs" })
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
		PlayingCard left = new("hearts", "2", new Godot.Texture2D());
		PlayingCard redirected = new("spades", "10", new Godot.Texture2D());
		PlayingCard right = new("clubs", "3", new Godot.Texture2D());
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
		int shuffleIndex = method.IndexOf("Shuffle(_playerDecks[playerIndex]);", captureApplyIndex, StringComparison.Ordinal);
		int cutIndex = method.IndexOf("Cut(_playerDecks[playerIndex]);", shuffleIndex, StringComparison.Ordinal);
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
		var card = new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false);

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
		var card = new PotentialCaptureRules.Card("8", CollectsTable: false, HasDoubleEffect: false, Suit: "hearts", CollectBySuit: true);

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
		var card = new PotentialCaptureRules.Card(
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
			new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false),
			new PotentialCaptureRules.Card("K", CollectsTable: false, HasDoubleEffect: false),
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
		var card = new PotentialCaptureRules.Card("A", CollectsTable: false, HasDoubleEffect: false);

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

	private static void CharacterAvatarDisplayNamesAreLocalizedForEveryLocale()
	{
		string repoRoot = FindRepositoryRoot();
		string avatarDirectory = Path.Combine(repoRoot, "resources", "character_avatars");
		string localizationPath = Path.Combine(repoRoot, "localization", "ui.csv");
		True(Directory.Exists(avatarDirectory));
		True(File.Exists(localizationPath));

		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);
		List<string> header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		string[] avatarPaths = Directory.GetFiles(avatarDirectory, "*.tres");
		True(avatarPaths.Length > 0);

		foreach (string avatarPath in avatarPaths)
		{
			string avatarText = File.ReadAllText(avatarPath);
			string avatarId = ReadTresString(avatarText, "Id");
			string fallbackName = ReadTresString(avatarText, "DisplayName");
			string displayNameKey = ReadTresString(avatarText, "DisplayNameKey");
			True(!string.IsNullOrWhiteSpace(avatarId));
			True(!string.IsNullOrWhiteSpace(fallbackName));
			True(!string.IsNullOrWhiteSpace(displayNameKey));
			True(displayNameKey.StartsWith("AVATAR_", StringComparison.Ordinal));
			True(displayNameKey.EndsWith("_NAME", StringComparison.Ordinal));
			if (!rowsByKey.TryGetValue(displayNameKey, out List<string>? row))
			{
				throw new InvalidOperationException($"missing localization row for {displayNameKey}");
			}

			Equal(header.Count, row.Count);

			for (int index = 1; index < header.Count; index++)
			{
				string locale = header[index];
				string localizedName = row[index].Trim();
				True(!string.IsNullOrWhiteSpace(localizedName));
				if (locale != "zh_CN" && locale != "zh_TW")
				{
					False(ContainsHanCharacter(localizedName));
				}
			}
		}
	}

	private static void PlayerCountLocalizationDifficultyRowsExistForEveryLocale()
	{
		string localizationPath = Path.Combine(FindRepositoryRoot(), "localization", "ui.csv");
		True(File.Exists(localizationPath));

		string[] localizationLines = File.ReadAllLines(localizationPath);
		True(localizationLines.Length > 1);
		List<string> header = ParseCsvLine(localizationLines[0]);
		True(header.Count > 1);

		var rowsByKey = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		foreach (string line in localizationLines[1..])
		{
			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			List<string> row = ParseCsvLine(line);
			if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]))
			{
				continue;
			}

			rowsByKey[row[0].Trim()] = row;
		}

		foreach (string key in new[]
		{
			"PLAYER_COUNT_DIFFICULTY",
			"PLAYER_COUNT_DIFFICULTY_LOW",
			"PLAYER_COUNT_DIFFICULTY_MEDIUM",
			"PLAYER_COUNT_DIFFICULTY_HIGH",
		})
			{
				True(rowsByKey.TryGetValue(key, out List<string>? row));
				row ??= new List<string>();
				Equal(header.Count, row.Count);

			for (int index = 1; index < row.Count; index++)
			{
				True(!string.IsNullOrWhiteSpace(row[index]));
			}
		}
	}

	private static void RuntimeLocalizationKeysExistForEveryReference()
	{
		string repoRoot = FindRepositoryRoot();
		var rowsByKey = ReadLocalizationRowsByKey(Path.Combine(repoRoot, "localization", "ui.csv"), out _);
		var missing = new List<string>();

		foreach (string relativePath in EnumerateRuntimeLocalizationReferenceFiles(repoRoot))
		{
			string absolutePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
			foreach (LocalizationReference reference in FindLocalizationReferences(relativePath, File.ReadAllLines(absolutePath)))
			{
				if (!rowsByKey.ContainsKey(reference.Key))
				{
					missing.Add($"{reference.Key} ({reference.RelativePath}:{reference.LineNumber})");
				}
			}
		}

		if (missing.Count > 0)
		{
			throw new InvalidOperationException("missing runtime localization keys: " + string.Join(", ", missing));
		}
	}

	private static void RuntimeLocalizationRowsCoverEverySupportedLanguage()
	{
		string repoRoot = FindRepositoryRoot();
		string localizationPath = Path.Combine(repoRoot, "localization", "ui.csv");
		var rowsByKey = ReadLocalizationRowsByKey(localizationPath, out List<string> header);
		List<string> expectedLocales = ReadSupportedLocalePreferences(repoRoot);
		List<string> actualLocales = header.Skip(1).Select(locale => locale.Trim()).ToList();

		if (expectedLocales.Count != actualLocales.Count)
		{
			throw new InvalidOperationException($"expected {expectedLocales.Count} localization locales, got {actualLocales.Count}");
		}

		for (int index = 0; index < expectedLocales.Count; index++)
		{
			if (!string.Equals(expectedLocales[index], actualLocales[index], StringComparison.Ordinal))
			{
				throw new InvalidOperationException($"localization header locale mismatch at column {index + 1}: expected {expectedLocales[index]}, got {actualLocales[index]}");
			}
		}

		foreach (KeyValuePair<string, List<string>> entry in rowsByKey)
		{
			List<string> row = entry.Value;
			if (row.Count != header.Count)
			{
				throw new InvalidOperationException($"localization row {entry.Key} has {row.Count} columns; expected {header.Count}");
			}

			for (int index = 1; index < row.Count; index++)
			{
				if (string.IsNullOrWhiteSpace(row[index]))
				{
					throw new InvalidOperationException($"localization row {entry.Key} is missing a value for locale {header[index]}");
				}
			}
		}
	}

	private static void RuntimeScenesDoNotExposeHardcodedNaturalLanguageText()
	{
		string repoRoot = FindRepositoryRoot();
		foreach (string relativePath in RuntimeHardcodedSceneRelativePaths)
		{
			string absolutePath = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
			True(File.Exists(absolutePath));

			string[] lines = File.ReadAllLines(absolutePath);
			for (int index = 0; index < lines.Length; index++)
			{
				Match match = Regex.Match(lines[index], "^\\s*(?<property>(?:text|placeholder_text|tooltip_text|popup/item_\\d+/text))\\s*=\\s*\"(?<value>[^\"]*)\"$");
				if (!match.Success)
				{
					continue;
				}

				string value = match.Groups["value"].Value.Trim();
				if (IsAllowedRuntimeSceneText(value))
				{
					continue;
				}

				throw new InvalidOperationException($"hardcoded runtime scene text in {relativePath}:{index + 1} -> {value}");
			}
		}
	}

	private static void AvatarFeedbackEffectResourcesStayContiguous()
	{
		string repoRoot = FindRepositoryRoot();
		foreach ((string root, int minFrameCount) in new[]
		{
			("res://art/fx/avatar_feedback_score/run-001/processed", 4),
			("res://art/fx/avatar_feedback_hit/run-001/processed", 4),
			("res://art/fx/avatar_feedback_victory/run-001/processed", 4),
			("res://art/fx/avatar_feedback_defeat/run-001/processed", 4),
		})
		{
			int frameCount = CountContiguousFrames(repoRoot, root, "fx-{0}.png");
			True(frameCount >= minFrameCount);
		}
	}

	private static void AdventureUpgradePopupSceneHasNoEmbeddedImagePayloads()
	{
		string scenePath = Path.Combine(FindRepositoryRoot(), "scenes", "upgrade_choice_popup_asset_scene.tscn");
		string text = File.ReadAllText(scenePath);

		False(text.Contains("[sub_resource type=\"Image\""));
		False(text.Contains("[sub_resource type=\"ImageTexture\""));
		False(text.Contains("PackedByteArray("));
	}

	private static void AdventureUpgradeVisualCatalogExposesTwelveProcessedCards()
	{
		string repoRoot = FindRepositoryRoot();
		string catalogPath = Path.Combine(
			repoRoot,
			"resources",
			"adventure_upgrades",
			"visuals",
			"adventure_upgrade_card_visual_catalog.tres");
		True(File.Exists(catalogPath));
		string text = File.ReadAllText(catalogPath);

		Equal(12, Regex.Matches(text, @"(?m)^\[sub_resource type=""Resource"" id=""Entry_").Count);

		MatchCollection entryBlocks = Regex.Matches(
			text,
			@"(?ms)^\[sub_resource type=""Resource"" id=""Entry_[^""]+""\]\s+script = ExtResource\(""[^""]+""\)\s+Suit = ""([^""]+)""\s+Tier = ""([^""]+)""\s+TexturePath = ""([^""]+)""\s+Region = Rect2\(([^)]+)\)");
		Equal(12, entryBlocks.Count);

		var seenKeys = new HashSet<string>(StringComparer.Ordinal);
		var seenSuits = new HashSet<string>(StringComparer.Ordinal);
		var seenTiers = new HashSet<string>(StringComparer.Ordinal);

		for (int index = 0; index < entryBlocks.Count; index++)
		{
			string suit = entryBlocks[index].Groups[1].Value.Trim().ToLowerInvariant();
			string tier = entryBlocks[index].Groups[2].Value.Trim().ToLowerInvariant();
			string texturePath = entryBlocks[index].Groups[3].Value;
			True(seenKeys.Add($"{suit}::{tier}"));
			seenSuits.Add(suit);
			seenTiers.Add(tier);

			True(texturePath.StartsWith("res://art/ui/adventure_upgrade_cards/", StringComparison.Ordinal));
			True(texturePath.Contains("/processed/", StringComparison.Ordinal));

			string[] regionParts = entryBlocks[index].Groups[4].Value.Split(',', StringSplitOptions.TrimEntries);
			Equal(4, regionParts.Length);
			True(float.TryParse(regionParts[2], out float width));
			True(float.TryParse(regionParts[3], out float height));
			True(width > 0.0f);
			True(height > 0.0f);
			AssertResPathExists(repoRoot, texturePath);
		}

		Equal(4, seenSuits.Count);
		True(seenSuits.Contains("hearts"));
		True(seenSuits.Contains("spades"));
		True(seenSuits.Contains("diamonds"));
		True(seenSuits.Contains("clubs"));

		Equal(3, seenTiers.Count);
		True(seenTiers.Contains("common"));
		True(seenTiers.Contains("uncommon"));
		True(seenTiers.Contains("rare"));
	}

	private static void AdventureUpgradeVisualCatalogIncludesPaletteFieldsAndRarityTierMappings()
	{
		string repoRoot = FindRepositoryRoot();
		string catalogPath = Path.Combine(
			repoRoot,
			"resources",
			"adventure_upgrades",
			"visuals",
			"adventure_upgrade_card_visual_catalog.tres");
		True(File.Exists(catalogPath));
		string text = File.ReadAllText(catalogPath);

		Equal(12, Regex.Matches(text, @"(?m)^TitleColor = Color\(([^)]+)\)$").Count);
		Equal(12, Regex.Matches(text, @"(?m)^GlowColor = Color\(([^)]+)\)$").Count);
		Equal(12, Regex.Matches(text, @"(?m)^RuleColor = Color\(([^)]+)\)$").Count);

		Equal(3, Regex.Matches(text, @"(?m)^\[sub_resource type=""Resource"" id=""RarityTier_").Count);

		MatchCollection mappingBlocks = Regex.Matches(
			text,
			@"(?ms)^\[sub_resource type=""Resource"" id=""RarityTier_[^""]+""\]\s+script = ExtResource\(""[^""]+""\)\s+Rarity = ""([^""]+)""\s+Tier = ""([^""]+)""$");
		Equal(3, mappingBlocks.Count);

		var tierMappings = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (Match mappingBlock in mappingBlocks)
		{
			string rarity = mappingBlock.Groups[1].Value.Trim().ToLowerInvariant();
			string mappedTier = mappingBlock.Groups[2].Value.Trim().ToLowerInvariant();
			tierMappings[rarity] = mappedTier;
		}

		Equal("common", tierMappings["common"]);
		Equal("uncommon", tierMappings["rare"]);
		Equal("rare", tierMappings["legendary"]);
	}

	private static void AdventureUpgradeVisualResolverReturnsAtlasTextureFromCatalog()
	{
		string resolverPath = Path.Combine(FindRepositoryRoot(), "scripts", "AdventureUpgradeCardVisualResolver.cs");
		True(File.Exists(resolverPath));

		string text = File.ReadAllText(resolverPath);
		True(text.Contains("adventure_upgrade_card_visual_catalog.tres", StringComparison.Ordinal));
		True(text.Contains("new AtlasTexture", StringComparison.Ordinal));
		True(text.Contains("entry.Region", StringComparison.Ordinal));
		True(text.Contains("entry.TexturePath", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeVisualResolverUsesCatalogRarityTierMappings()
	{
		string resolverPath = Path.Combine(FindRepositoryRoot(), "scripts", "AdventureUpgradeCardVisualResolver.cs");
		True(File.Exists(resolverPath));

		string text = File.ReadAllText(resolverPath);
		True(text.Contains("ResolveTier(", StringComparison.Ordinal));
		True(text.Contains("LoadCatalog()", StringComparison.Ordinal));
		True(text.Contains("catalog.ResolveTier", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeRuntimeNoLongerHardcodesPopupArtSwitchTables()
	{
		string runtimePath = Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.AdventureUpgrades.cs");
		string text = File.ReadAllText(runtimePath);
		string overlayRuntimePath = Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.AdventureUpgradeSelectionOverlay.cs");
		string overlayText = File.ReadAllText(overlayRuntimePath);
		string choiceArtPath = Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"AdventureUpgradeChoiceArt.cs");
		string choiceArtText = File.ReadAllText(choiceArtPath);
		False(text.Contains("GetAdventureUpgradeCardTextureRegion(", StringComparison.Ordinal));
		False(text.Contains("GetAdventureUpgradeCardTexturePath(", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/hearts/processed/hearts-common.png", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/spades/processed/spades-common.png", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/diamonds/processed/diamonds-common.png", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/clubs/processed/clubs-common.png", StringComparison.Ordinal));
		False(text.Contains("MapAdventureUpgradeRarityToTextureTier(", StringComparison.Ordinal));
		False(text.Contains("ApplyAdventureUpgradeChoicePalette(", StringComparison.Ordinal));
		False(text.Contains("normalizedSuit == ", StringComparison.Ordinal));
		False(text.Contains("normalizedRarity == ", StringComparison.Ordinal));
		True(overlayText.Contains("AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity", StringComparison.Ordinal));
		False(overlayText.Contains("AdventureUpgradeChoiceVisualResolver.LoadCardBaseTexture", StringComparison.Ordinal));
		False(overlayText.Contains("AdventureUpgradeChoiceVisualResolver.LoadSuitTexture", StringComparison.Ordinal));
		False(overlayText.Contains("SetSuitVisualTier", StringComparison.Ordinal));
		False(choiceArtText.Contains("adventure_upgrade_card_perspective", StringComparison.Ordinal));
		False(choiceArtText.Contains("ThicknessLayer", StringComparison.Ordinal));
		False(choiceArtText.Contains("SuitBadgeLayer", StringComparison.Ordinal));
		False(choiceArtText.Contains("adventure_upgrade_card_cleanup", StringComparison.Ordinal));
	}

	private static void AdventureUpgradePreviewUsesSharedCatalogCardInstances()
	{
		string repoRoot = FindRepositoryRoot();
		string previewScenePath = Path.Combine(repoRoot, "scenes", "upgrade_choice_popup_asset_scene.tscn");
		string sharedCardScenePath = Path.Combine(repoRoot, "scenes", "adventure_upgrade_catalog_card.tscn");
		string sharedCardScriptPath = Path.Combine(repoRoot, "scripts", "AdventureUpgradeCatalogCard.cs");
		string text = File.ReadAllText(previewScenePath);

		True(File.Exists(sharedCardScenePath));
		True(File.Exists(sharedCardScriptPath));
		Equal(12, Regex.Matches(text, @"instance=ExtResource\(""2_card""\)").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""hearts""").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""diamonds""").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""spades""").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""clubs""").Count);
		False(text.Contains("AtlasTexture", StringComparison.Ordinal));
		False(text.Contains("UpgradeChoicePopupPreview.gd", StringComparison.Ordinal));
		False(text.Contains("hearts-common.png", StringComparison.Ordinal));
		False(text.Contains("spades-common.png", StringComparison.Ordinal));
		False(text.Contains("diamonds-common.png", StringComparison.Ordinal));
		False(text.Contains("clubs-common.png", StringComparison.Ordinal));
		string cardScript = File.ReadAllText(sharedCardScriptPath);
		True(cardScript.Contains("LoadAtlasTextureForRarity", StringComparison.Ordinal));
		False(cardScript.Contains("LoadCardBaseTexture", StringComparison.Ordinal));
		False(cardScript.Contains("LoadSuitTexture", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeOverlaySampleArtUsesCatalogKeysInsteadOfHardcodedAtlasMetadata()
	{
		string overlayScenePath = Path.Combine(FindRepositoryRoot(), "scenes", "adventure_upgrade_selection_overlay.tscn");
		string text = File.ReadAllText(overlayScenePath);

		False(text.Contains("AtlasTexture_sample_", StringComparison.Ordinal));
		False(text.Contains("art/ui/adventure_upgrade_cards/", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"diamonds\"", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"hearts\"", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"spades\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"uncommon\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"common\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"rare\"", StringComparison.Ordinal));
	}

	private static void ItemCardVisualResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string itemCardDirectory = Path.Combine(repoRoot, "resources", "item_cards");
		True(Directory.Exists(itemCardDirectory));

		string[] itemCardPaths = Directory.GetFiles(itemCardDirectory, "*.tres");
		True(itemCardPaths.Length > 0);

		foreach (string itemCardPath in itemCardPaths)
		{
			string itemCardText = File.ReadAllText(itemCardPath);
			string itemCardId = ReadTresString(itemCardText, "Id");
			True(!string.IsNullOrWhiteSpace(itemCardId));

			int frameCount = Math.Max(1, ReadTresInt(itemCardText, "FrameCount", 1));
			string framePathPattern = ReadTresString(itemCardText, "FramePathPattern");
			if (string.IsNullOrWhiteSpace(framePathPattern))
			{
				string artRunName = ReadTresString(itemCardText, "ArtRunName");
				True(!string.IsNullOrWhiteSpace(artRunName));
				framePathPattern = $"res://art/cards/item_cards/{itemCardId}/{artRunName}/processed/frames/frame-{{0:000}}.png";
			}

			AssertFramePatternExists(repoRoot, framePathPattern, frameCount);

			string attachmentOverlayTexturePath = ReadTresString(itemCardText, "AttachmentOverlayTexturePath");
			if (!string.IsNullOrWhiteSpace(attachmentOverlayTexturePath))
			{
				AssertResPathExists(repoRoot, attachmentOverlayTexturePath);
			}
		}
	}

	private static void AttachmentSelectionPreviewFollowsPendingItemCard()
	{
		Equal("double", AttachmentSelectionPreviewRules.GetPreviewItemCardId("double"));
		Equal("magnet", AttachmentSelectionPreviewRules.GetPreviewItemCardId("magnet"));
		Equal("magnet", AttachmentSelectionPreviewRules.GetPreviewItemCardId(string.Empty));
	}

	private static void MainMenuBubblesRiseAndFadeThroughLifecycle()
	{
		MainMenuBubbleMotionState early = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 0.4f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState middle = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 2.0f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState late = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 3.8f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState wrapped = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 4.2f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);

		True(middle.RiseOffsetY < early.RiseOffsetY - 35.0f);
		True(late.RiseOffsetY < middle.RiseOffsetY - 25.0f);
		True(late.AlphaMultiplier < middle.AlphaMultiplier);
		True(wrapped.RiseOffsetY > late.RiseOffsetY + 80.0f);
		True(wrapped.AlphaMultiplier < middle.AlphaMultiplier);
	}

	private static void MainMenuBubblesHideBeforeCrossingWaterline()
	{
		float visible = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			centerY: 430.0f,
			topOffset: 32.0f,
			waterlineY: 340.0f,
			fadeDistance: 48.0f);
		float hidden = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			centerY: 365.0f,
			topOffset: 32.0f,
			waterlineY: 340.0f,
			fadeDistance: 48.0f);

		True(visible > 0.9f);
		Equal(0.0f, hidden);
	}

	private static void MainMenuBubbleVisualResourcePointsToExistingFile()
	{
		AssertResPathExists(FindRepositoryRoot(), "res://art/fx/environment/main_menu_bubble/run-001/processed/fx-1.png");
	}

	private static void BoardEffectResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		foreach ((string pattern, int frameCount) in new[]
		{
			("res://art/cards/ocean_animated_deck/back/processed/frames/frame-{0:000}.png", 8),
			("res://art/cards/deck_shuffle/overhand_drop/game_frames/frame-{0:000}.png", 6),
			("res://art/fx/question_card_transform/run-001/processed/fx-{0}.png", 6),
			("res://art/fx/special_card_trigger/ocean_chrysanthemum_preview5/sea_anemone_rays/processed/fx-{0}.png", 4),
			("res://art/fx/bomb_pair_destroy/card_rip_burst/run-001/processed/fx-{0}.png", 4),
			("res://art/fx/lens_reveal_sprites/run-001/processed/fx-{0}.png", 4),
			("res://art/fx/adventure_trace_hunt_marker/run-004/processed/fx-{0}.png", 4),
		})
		{
			AssertFramePatternExists(repoRoot, pattern, frameCount);
		}

		foreach (string resPath in new[]
		{
			"res://art/fx/slime_card_overlay/run-001/processed/card-aspect.png",
			"res://art/ui/result_text_prompt/run-001/processed/victory-text-prompt-effect.png",
			"res://art/ui/result_text_prompt/run-001/processed/defeat-text-prompt-effect.png",
		})
		{
			AssertResPathExists(repoRoot, resPath);
		}
	}

	private static void DiceRollPresentationResourcesMapRollAndResultFrames()
	{
		string repoRoot = FindRepositoryRoot();

		Equal(16, DiceRollPresentationRules.RollingPathFrameCount);
		Equal("res://art/props/dice/neutral_tabletop/run-002/rolling_path/frame-001.png", DiceRollPresentationRules.GetRollingPathFramePath(1));
		Equal("res://art/props/dice/neutral_tabletop/run-002/rolling_path/frame-016.png", DiceRollPresentationRules.GetRollingPathFramePath(16));
		Equal(string.Empty, DiceRollPresentationRules.GetRollingPathFramePath(0));
		Equal(string.Empty, DiceRollPresentationRules.GetRollingPathFramePath(17));

		for (int result = DiceRollPresentationRules.MinResult; result <= DiceRollPresentationRules.MaxResult; result++)
		{
			True(DiceRollPresentationRules.TryGetResultMotionFramePath(result, out string resultPath));
			Equal($"res://art/props/dice/neutral_tabletop/run-002/result_motion_frames/result-{result}.png", resultPath);
			AssertResPathExists(repoRoot, resultPath);
		}

		False(DiceRollPresentationRules.TryGetResultMotionFramePath(0, out _));
		False(DiceRollPresentationRules.TryGetResultMotionFramePath(7, out _));
		AssertFramePatternExists(repoRoot, DiceRollPresentationRules.RollingPathFramePattern, DiceRollPresentationRules.RollingPathFrameCount);
		AssertResPathExists(repoRoot, DiceRollPresentationRules.RollingPathPreviewGifPath);
	}


	private static void GameBoardExportedSettersShortCircuitRedundantWork()
	{
		string repoRoot = FindRepositoryRoot();
		string text =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Exports.cs"));

		True(text.Contains("if (normalizedPlayerCount == _playerCount)", StringComparison.Ordinal));
		True(text.Contains("if (value == _itemMode)", StringComparison.Ordinal));
		True(text.Contains("if (normalizedWinningScore == _winningScore)", StringComparison.Ordinal));
	}

	private static void DecorativeUiArtAvoidsUnconditionalPerFrameRedraws()
	{
		string repoRoot = FindRepositoryRoot();

		string oceanMenuText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "OceanMenuButtonFrame.cs"));
		False(Regex.IsMatch(
			oceanMenuText,
			@"_glow = Mathf\.Lerp\(_glow, target, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));

		string oceanDialogText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "OceanDialogPanelArt.cs"));
		True(oceanDialogText.Contains("SparkRedrawInterval", StringComparison.Ordinal));
		True(oceanDialogText.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
		False(Regex.IsMatch(
			oceanDialogText,
			@"public override void _Process\(double delta\)\s*\{\s*_time \+= delta;\s*QueueRedraw\(\);\s*\}",
			RegexOptions.Singleline));

		string playerCountFrameText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "PlayerCountDialogFrameArt.cs"));
		False(Regex.IsMatch(
			playerCountFrameText,
			@"_glow = Mathf\.Lerp\(_glow, targetGlow, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));

		string mainMenuPanelText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuPlayerCountDialogPanelArt.cs"));
		True(mainMenuPanelText.Contains("IdleSparkRedrawInterval", StringComparison.Ordinal));
		True(mainMenuPanelText.Contains("ActiveGlowRedrawInterval", StringComparison.Ordinal));
		True(mainMenuPanelText.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
		False(Regex.IsMatch(
			mainMenuPanelText,
			@"_startButtonGlow = Mathf\.Lerp\(_startButtonGlow, targetGlow, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));
	}

	private static void DynamicBackgroundLayersGateImageModeRedrawToFallbackNeeds()
	{
		string repoRoot = FindRepositoryRoot();

		string underwaterText =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.ProceduralDraw.cs"));
		True(underwaterText.Contains("ShouldQueueRedrawForImageFallback()", StringComparison.Ordinal));
		True(underwaterText.Contains("if (ShouldQueueRedrawForImageFallback())", StringComparison.Ordinal));

		string adventureMapText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "AdventureMapDynamicLayer.cs"));
		True(adventureMapText.Contains("ShouldQueueRedrawForImageFallback()", StringComparison.Ordinal));
		True(adventureMapText.Contains("if (ShouldQueueRedrawForImageFallback())", StringComparison.Ordinal));
	}

	private static void MainMenuOceanDynamicsKeepsProceduralFallbackPathsInImageMode()
	{
		string repoRoot = FindRepositoryRoot();
		string text =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.ProceduralDraw.cs"));

		True(text.Contains("ShouldQueueRedrawInImageMode()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralSunbeams()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralSurfaceWake()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralCurrentRibbons()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralCausticRibbons()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralGlints()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawBubbles()", StringComparison.Ordinal));
		False(text.Contains("if (_activeRenderMode == OceanRenderMode.Procedural)", StringComparison.Ordinal));
	}

	private static void DynamicEnvironmentLayersShareVisibilityAndThrottleContract()
	{
		string repoRoot = FindRepositoryRoot();
		string[] paths =
		{
			Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.cs"),
			Path.Combine(repoRoot, "scripts", "AdventureMapDynamicLayer.cs"),
			Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.cs"),
		};

		foreach (string path in paths)
		{
			string text = File.ReadAllText(path);
			True(text.Contains("VisibilityChanged += HandleVisibilityChanged", StringComparison.Ordinal));
			True(text.Contains("SetProcess(isVisible)", StringComparison.Ordinal));
			True(text.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
			True(text.Contains("NotificationResized", StringComparison.Ordinal));
			True(text.Contains("image_update_fps", StringComparison.Ordinal));
			True(text.Contains("redraw_fps", StringComparison.Ordinal));
		}
	}

	private static void GameBoardDefersModeSpecificResourceLoadsUntilNeeded()
	{
		string repoRoot = FindRepositoryRoot();
		string gameBoardText =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Exports.cs"));
		string assetsText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Assets.cs"));

		True(gameBoardText.Contains("EnsureAdventureModeResourcesLoaded();", StringComparison.Ordinal));
		True(gameBoardText.Contains("EnsureItemModeResourcesLoaded();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadMantaRayTideShiftCycloneFxTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadLeafySeadragonBladeProjectileTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadBossKRoyalSeizureLanceTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadAdventureDamageEffectTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadItemCardTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadAttachmentCardOverlayTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadQuestionCardTransformTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadBombPairDestroyEffectTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadLensRevealWispTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadMagnetCountdownFxTextures();", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureAdventureModeResourcesLoaded()", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureItemModeResourcesLoaded()", StringComparison.Ordinal));
	}

	private static void GameBoardOptionalFxResourcesAreNoLongerStaticallyPreloaded()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.cs"));

		False(text.Contains("private static readonly Texture2D[] ScoreFillEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] EnemySpecialAbilityAvatarFxFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MantaRayTideShiftCycloneFxFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] DefaultAdventureDamageEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] CatAdventureDamageEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LeafySeadragonBladeFlightFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LeafySeadragonBladeImpactFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] BossKRoyalSeizureLanceFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] QuestionCardTransformFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] SpecialCardTriggerEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] SpecialCardTriggerActivationFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] BombPairDestroyEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LensRevealWispFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MagnetCountdownFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] TableShuffleVortexFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MagnetAttachmentApplyRingFrames", StringComparison.Ordinal));
	}

	private static void GameBoardWarmupIncludesOptionalFxResources()
	{
		string repoRoot = FindRepositoryRoot();
		string warmupText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.ResourceWarmup.cs"));
		string assetsText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Assets.cs"));

		True(warmupText.Contains("WarmupOptionalFxResourcesAsync", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:CoreGameplay", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:AdventureMode", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:ItemMode", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureCommonGameplayFxResourcesLoaded()", StringComparison.Ordinal));
	}

	private static void GameBoardIdlePerformanceTraceSmokeExists()
	{
		string repoRoot = FindRepositoryRoot();
		string smokeScriptPath = Path.Combine(repoRoot, "tests", "GameBoardIdlePerformanceTraceSmoke.gd");
		string runnerPath = Path.Combine(repoRoot, "tests", "run_game_board_idle_performance_trace_smoke.sh");

		True(File.Exists(smokeScriptPath));
		True(File.Exists(runnerPath));

		string smokeText = File.ReadAllText(smokeScriptPath);
		string runnerText = File.ReadAllText(runnerPath);

		True(smokeText.Contains("IDLE_WINDOW_BEGIN", StringComparison.Ordinal));
		True(smokeText.Contains("IDLE_WINDOW_END", StringComparison.Ordinal));
		True(smokeText.Contains("debug_performance_trace", StringComparison.Ordinal));
		True(runnerText.Contains("RenderPlayerHands", StringComparison.Ordinal));
		True(runnerText.Contains("RenderTableCards", StringComparison.Ordinal));
		True(runnerText.Contains("IDLE_WINDOW_BEGIN", StringComparison.Ordinal));
		True(runnerText.Contains("IDLE_WINDOW_END", StringComparison.Ordinal));
	}

	private static void ResourceExportAuditScriptChecksTrackedAssetsAndExportConfig()
	{
		string scriptPath = Path.Combine(FindRepositoryRoot(), "tools", "audit_export_resources.sh");
		True(File.Exists(scriptPath));
		string text = File.ReadAllText(scriptPath);

		True(text.Contains("git ls-files", StringComparison.Ordinal));
		True(text.Contains("generated-images", StringComparison.Ordinal));
		True(text.Contains("outputs", StringComparison.Ordinal));
		True(text.Contains("debug-sse.log", StringComparison.Ordinal));
		True(text.Contains("project.godot", StringComparison.Ordinal));
		True(text.Contains("export_presets.cfg", StringComparison.Ordinal));
		True(text.Contains("autoload", StringComparison.Ordinal));
		True(text.Contains("exclude_filter", StringComparison.Ordinal));
		True(text.Contains("raw", StringComparison.Ordinal));
		True(text.Contains("transparent-processing-meta.json", StringComparison.Ordinal));
		True(text.Contains("upgrade-popup-assets", StringComparison.Ordinal));
		True(text.Contains("source", StringComparison.Ordinal));
		True(text.Contains("Category Summary", StringComparison.Ordinal));
		True(text.Contains("upgrade_popup_processed", StringComparison.Ordinal));
		True(text.Contains("board_top_preview", StringComparison.Ordinal));
		False(text.Contains("git rm", StringComparison.Ordinal));
		False(text.Contains("rm -rf", StringComparison.Ordinal));
	}

	private static void ResourceExportAuditDocDescribesReadOnlyCommand()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "RESOURCE_EXPORT_AUDIT.md");
		True(File.Exists(docPath));
		string text = File.ReadAllText(docPath);

		True(text.Contains("bash tools/audit_export_resources.sh", StringComparison.Ordinal));
		True(text.Contains("只报告", StringComparison.Ordinal));
		True(text.Contains("不自动删除", StringComparison.Ordinal));
		True(text.Contains("processed", StringComparison.Ordinal));
		True(text.Contains("raw", StringComparison.Ordinal));
	}

	private static void ExportPresetExcludesUpgradePopupSourceOnlyAssets()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "export_presets.cfg"));

		True(text.Contains("generated-images/**", StringComparison.Ordinal));
	}

	private static void GitignoreCoversUpgradePopupSourceOnlyAssets()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".gitignore"));

		True(text.Contains("/generated-images/upgrade-popup-assets/*/raw/", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/transparent-processing-meta.json", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/*.png", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/*.png.import", StringComparison.Ordinal));
	}

	private static void TrackedGeneratedAssetInventoryDocSummarizesCurrentCategories()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "TRACKED_GENERATED_ASSET_INVENTORY.md");
		True(File.Exists(docPath));
		string text = File.ReadAllText(docPath);

		True(text.Contains("upgrade_popup_processed", StringComparison.Ordinal));
		True(text.Contains("upgrade_popup_raw", StringComparison.Ordinal));
		True(text.Contains("board_top_preview", StringComparison.Ordinal));
		True(text.Contains("generated_misc", StringComparison.Ordinal));
		True(text.Contains("debug_log", StringComparison.Ordinal));
		True(text.Contains("保留跟踪", StringComparison.Ordinal));
		True(text.Contains("候选取消跟踪", StringComparison.Ordinal));
	}

	private static void TrackedGeneratedAssetInventoryDocIncludesSuggestedManualCleanupBatches()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "TRACKED_GENERATED_ASSET_INVENTORY.md");
		string text = File.ReadAllText(docPath);

		True(text.Contains("Suggested Manual Cleanup Batches", StringComparison.Ordinal));
		True(text.Contains("Batch 1", StringComparison.Ordinal));
		True(text.Contains("board-top-extension-preview", StringComparison.Ordinal));
		True(text.Contains("git rm --cached --", StringComparison.Ordinal));
		True(text.Contains("review first", StringComparison.OrdinalIgnoreCase));
	}

	private static void GameBoardPauseMenuHasExtractedControllerBoundary()
	{
		string repoRoot = FindRepositoryRoot();
		string controllerPath = Path.Combine(repoRoot, "scripts", "GameBoardPauseMenuController.cs");
		string pauseMenuPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.PauseMenu.cs");
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.cs");

		True(File.Exists(controllerPath));
		string controllerText = File.ReadAllText(controllerPath);
		string pauseMenuText = File.ReadAllText(pauseMenuPath);
		string mainText = File.ReadAllText(mainPath);

		True(controllerText.Contains("internal sealed class GameBoardPauseMenuController", StringComparison.Ordinal));
		True(controllerText.Contains("internal void Show()", StringComparison.Ordinal));
		True(controllerText.Contains("internal void Hide()", StringComparison.Ordinal));
		True(controllerText.Contains("internal void RefreshLocalizedText()", StringComparison.Ordinal));
		True(pauseMenuText.Contains("GameBoardPauseMenuController", StringComparison.Ordinal));
		False(pauseMenuText.Contains("private CanvasLayer? _pauseMenuLayer", StringComparison.Ordinal));
		False(pauseMenuText.Contains("private PauseMenuOverlay? _pauseMenuRoot", StringComparison.Ordinal));
		False(pauseMenuText.Contains("private Button? _pauseContinueButton", StringComparison.Ordinal));
		False(mainText.Contains("PauseMenuScenePath", StringComparison.Ordinal));
	}

	private static void NetworkManagerSerializationHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Serialization.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal));
		True(partialText.Contains("private static NetworkSessionConfig ParseConfig(Godot.Collections.Dictionary dictionary)", StringComparison.Ordinal));
		True(partialText.Contains("private static NetworkPlayerPublicState ParsePlayer(Godot.Collections.Dictionary dictionary)", StringComparison.Ordinal));
		False(mainText.Contains("private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal));
		False(mainText.Contains("private static NetworkPlayerPublicState ParsePlayer(Godot.Collections.Dictionary dictionary)", StringComparison.Ordinal));
		False(mainText.Contains("private static TEnum ReadEnum<TEnum>", StringComparison.Ordinal));
	}


}
