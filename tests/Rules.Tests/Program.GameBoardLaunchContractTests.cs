using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void RunGameBoardLaunchContractTests()
	{
		Run("game board launch factories define all production modes", GameBoardLaunchFactoriesDefineAllProductionModes);
		Run("game board tutorial launch requires matching campaign metadata", GameBoardTutorialLaunchRequiresMatchingCampaignMetadata);
		Run("game board network launch validates complete seat data", GameBoardNetworkLaunchValidatesCompleteSeatData);
		Run("game board network initialization replaces one matching snapshot", GameBoardNetworkInitializationReplacesOneMatchingSnapshot);
		Run("game board launch architecture forbids string configuration", GameBoardLaunchArchitectureForbidsStringConfiguration);
		Run("game board extracted domains have independent owners", GameBoardExtractedDomainsHaveIndependentOwners);
		Run("network scene load domain has an independent owner", NetworkSceneLoadDomainHasIndependentOwner);
		Run("network lobby roster domain has an independent owner", NetworkLobbyRosterDomainHasIndependentOwner);
		Run("network reconnect domain has an independent owner", NetworkReconnectDomainHasIndependentOwner);
		Run("extracted coordinators forbid god object and tree fields", ExtractedCoordinatorsForbidGodObjectAndTreeFields);
		Run("god object private field counts stay below architecture baseline", GodObjectPrivateFieldCountsStayBelowArchitectureBaseline);
	}

	private static void GameBoardLaunchFactoriesDefineAllProductionModes()
	{
		GameBoardLaunchOptions local = GameBoardLaunchOptions.Local(4, itemMode: false, winningScore: 200, playerName: "Alice");
		Equal(GameModeKind.LocalStandard, local.Mode);
		Equal(4, local.PlayerCount);
		False(local.SoloMode);

		GameBoardLaunchOptions solo = GameBoardLaunchOptions.Solo(
			2,
			itemMode: true,
			winningScore: 100,
			difficulty: SoloAiDifficulty.High,
			playerName: "Alice");
		Equal(GameModeKind.SoloStandard, solo.Mode);
		True(solo.SoloMode);
		Equal(SoloAiDifficulty.High, solo.SoloAiDifficulty);

		string mainAvatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		GameBoardLaunchOptions adventure = GameBoardLaunchOptions.AdventureMain(mainAvatarId, "Alice");
		Equal(GameModeKind.AdventureMain, adventure.Mode);
		Equal(mainAvatarId, adventure.AdventureEnemyAvatarId);
		True(adventure.SoloMode);

		GameBoardLaunchOptions tutorial = GameBoardLaunchOptions.AdventureTutorial(
			"fish",
			AdventureTutorialFlowKind.BasicCapture,
			"Alice");
		Equal(GameModeKind.AdventureTutorial, tutorial.Mode);
		Equal(AdventureTutorialFlowKind.BasicCapture, tutorial.TutorialFlow!.Value);
	}

	private static void GameBoardTutorialLaunchRequiresMatchingCampaignMetadata()
	{
		True(AdventureTutorialFlowRules.TryResolveForAvatar(
			"lanternfish",
			out AdventureTutorialFlowKind flow,
			out string error));
		Equal(string.Empty, error);
		Equal(AdventureTutorialFlowKind.BombItem, flow);

		ExpectThrows<ArgumentException>(() => GameBoardLaunchOptions.AdventureTutorial(
			"lanternfish",
			AdventureTutorialFlowKind.BasicCapture));
		ExpectThrows<ArgumentException>(() => GameBoardLaunchOptions.AdventureTutorial(
			"missing_tutorial",
			AdventureTutorialFlowKind.BasicCapture));
	}

	private static void GameBoardNetworkLaunchValidatesCompleteSeatData()
	{
		NetworkSessionConfig config = CreateLaunchTestNetworkConfig();
		GameBoardLaunchOptions options = GameBoardLaunchOptions.Network(
			config,
			localPlayerIndex: 1,
			playerNames: new[] { "Host", "Client" },
			playerAvatarIds: new[] { "cat", "fish" });
		Equal(GameModeKind.NetworkStandard, options.Mode);
		Equal(1, options.LocalPlayerIndex);
		True(options.NetworkWaitForInitialize);

		ExpectThrows<ArgumentException>(() => GameBoardLaunchOptions.Network(
			config,
			localPlayerIndex: 2,
			playerNames: new[] { "Host", "Client" },
			playerAvatarIds: new[] { "cat", "fish" }));
		ExpectThrows<ArgumentException>(() => GameBoardLaunchOptions.Network(
			config,
			localPlayerIndex: 1,
			playerNames: new[] { "Host" },
			playerAvatarIds: new[] { "cat", "fish" }));
	}

	private static void GameBoardNetworkInitializationReplacesOneMatchingSnapshot()
	{
		NetworkSessionConfig config = CreateLaunchTestNetworkConfig();
		GameBoardRuntimeConfiguration runtime = GameBoardRuntimeConfiguration.FromLaunchOptions(
			GameBoardLaunchOptions.Network(
				config,
				localPlayerIndex: 1,
				playerNames: new[] { "Host", "Client" },
				playerAvatarIds: new[] { "cat", "fish" }));
		var initialization = new NetworkBoardInitialization(
			Config: config,
			AuthorityEpoch: 1,
			LocalPlayerIndex: 1,
			PlayerNames: new[] { "Host", "Client" },
			PlayerAvatarIds: new[] { "cat", "fish" });

		GameBoardRuntimeConfiguration initialized = runtime.ApplyNetworkInitialization(initialization);
		False(initialized.NetworkWaitForInitialize);
		Equal(config.MatchSeed, initialized.MatchSeed);
		ExpectThrows<InvalidOperationException>(() => initialized.ApplyNetworkInitialization(initialization));

		NetworkSessionConfig wrongMatch = config with { MatchId = "wrong-match" };
		ExpectThrows<InvalidOperationException>(() => runtime.ApplyNetworkInitialization(
			initialization with { Config = wrongMatch }));
	}

	private static NetworkSessionConfig CreateLaunchTestNetworkConfig()
	{
		return new NetworkSessionConfig(
			SessionId: "session-1",
			RoomName: "Room",
			RoomPassword: string.Empty,
			MatchId: "match-1",
			MatchSeed: 12345,
			Transport: NetworkTransport.Lan,
			PlayerCount: 2,
			ItemMode: true,
			WinningScore: 100,
			LobbyVisibility: LobbyVisibility.Private);
	}

	private static void GameBoardLaunchArchitectureForbidsStringConfiguration()
	{
		string repoRoot = FindRepositoryRoot();
		string scriptsRoot = Path.Combine(repoRoot, "scripts");
		string testsRoot = Path.Combine(repoRoot, "tests");
		string startupNames = string.Join('|', new[]
		{
			"player_count",
			"item_mode",
			"winning_score",
			"adventure_mode",
			"adventure_tutorial_mode",
			"adventure_tutorial_flow",
			"solo_mode",
			"solo_ai_difficulty",
			"local_player_index",
			"network_session_id",
			"network_match_id",
			"match_seed",
			"player_names_csv",
			"player_avatar_ids_csv",
			"network_wait_for_initialize",
			"adventure_enemy_avatar_id",
		});
		var stringConfigurationPattern = new Regex(
			$"\\.(?:Set|Get|set|get)\\(\\s*\"(?:{startupNames})\"",
			RegexOptions.CultureInvariant);
		var gdscriptBoardStringConfigurationPattern = new Regex(
			$"\\b[A-Za-z_][A-Za-z0-9_]*board[A-Za-z0-9_]*\\s*\\.(?:set|get)\\(\\s*\"(?:{startupNames})\"",
			RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

		foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs"))
		{
			False(stringConfigurationPattern.IsMatch(File.ReadAllText(path)));
		}
		foreach (string path in Directory.GetFiles(testsRoot, "*.gd"))
		{
			False(gdscriptBoardStringConfigurationPattern.IsMatch(File.ReadAllText(path)));
		}

		string projections = File.ReadAllText(Path.Combine(scriptsRoot, "GameBoardScene.Exports.cs"));
		False(Regex.IsMatch(
			projections,
			$@"\[Export[^\]]*\]\s*public\s+[^\n]+\s+(?:{startupNames})\b",
			RegexOptions.CultureInvariant));
		False(projections.Contains("set =>", StringComparison.Ordinal));

		string configureCallers = string.Join(
			'\n',
			Directory.GetFiles(scriptsRoot, "*.cs")
				.Where(path => !path.EndsWith("GameBoardScene.cs", StringComparison.Ordinal)
					&& !path.EndsWith("GameBoardSceneFactory.cs", StringComparison.Ordinal))
				.Select(File.ReadAllText));
		False(configureCallers.Contains("ConfigureForLaunch(", StringComparison.Ordinal));

		string tutorialScene = File.ReadAllText(Path.Combine(repoRoot, "scenes", "adventure_tutorial_game_board_scene.tscn"));
		string itemTutorialScene = File.ReadAllText(Path.Combine(repoRoot, "scenes", "adventure_item_tutorial_game_board_scene.tscn"));
		False(Regex.IsMatch(tutorialScene + itemTutorialScene, $@"^(?:{startupNames})\s*=", RegexOptions.Multiline));

		string project = File.ReadAllText(Path.Combine(repoRoot, "pokegame.csproj"));
		True(project.Contains("scripts/GameBoardTestHarness.cs", StringComparison.Ordinal));
		True(project.Contains("'$(EnableTestHooks)' != 'true'", StringComparison.Ordinal));
	}

	private static void ExpectThrows<TException>(Action action)
		where TException : Exception
	{
		try
		{
			action();
		}
		catch (TException)
		{
			return;
		}

		throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
	}
}
