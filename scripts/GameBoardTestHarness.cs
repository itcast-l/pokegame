#if DECKLAND_TEST_HOOKS
using System;
using System.Collections.Immutable;
using Godot;

public sealed record GameBoardTestConfiguration(
	bool DeferBoardInitialization = false,
	string RecordingSetupId = "",
	bool PerformanceTrace = false,
	float PerformanceTraceMinimumMilliseconds = 4.0f);

public partial class GameBoardTestHarness : RefCounted
{
	public GameBoardScene? CreateLocal(
		PackedScene packedScene,
		int playerCount = 2,
		bool itemMode = true,
		int winningScore = 100,
		string playerName = "")
	{
		return TryCreate(
			packedScene,
			GameBoardLaunchOptions.Local(playerCount, itemMode, winningScore, playerName));
	}

	public GameBoardScene? CreateSolo(
		PackedScene packedScene,
		int playerCount = 2,
		bool itemMode = true,
		int winningScore = 100,
		int difficulty = (int)SoloAiDifficulty.Medium,
		string playerName = "")
	{
		return TryCreate(
			packedScene,
			GameBoardLaunchOptions.Solo(
				playerCount,
				itemMode,
				winningScore,
				(SoloAiDifficulty)difficulty,
				playerName));
	}

	public GameBoardScene? CreateAdventure(
		PackedScene packedScene,
		string enemyAvatarId,
		int winningScore = 100,
		bool itemMode = true,
		int difficulty = (int)SoloAiDifficulty.Medium,
		string playerName = "")
	{
		return TryCreate(
			packedScene,
			GameBoardLaunchOptions.AdventureMain(
				enemyAvatarId,
				playerName,
				winningScore,
				itemMode,
				(SoloAiDifficulty)difficulty));
	}

	public GameBoardScene? CreateTutorial(
		PackedScene packedScene,
		string enemyAvatarId,
		int winningScore = 100,
		bool itemMode = true,
		int difficulty = (int)SoloAiDifficulty.Medium,
		string playerName = "")
	{
		if (!AdventureTutorialFlowRules.TryResolveForAvatar(
			enemyAvatarId,
			out AdventureTutorialFlowKind flow,
			out string error))
		{
			GD.PushError(error);
			return null;
		}

		return TryCreate(
			packedScene,
			GameBoardLaunchOptions.AdventureTutorial(
				enemyAvatarId,
				flow,
				playerName,
				winningScore,
				itemMode,
				(SoloAiDifficulty)difficulty));
	}

	public GameBoardScene? CreateNetwork(
		PackedScene packedScene,
		int playerCount,
		bool itemMode,
		int winningScore,
		string sessionId,
		string matchId,
		long matchSeed,
		int localPlayerIndex,
		string playerNamesCsv = "",
		string playerAvatarIdsCsv = "",
		bool waitForInitialize = true)
	{
		string[] playerNames = BuildCsvValues(playerNamesCsv, playerCount, index => $"Player {index + 1}");
		string[] playerAvatarIds = BuildCsvValues(
			playerAvatarIdsCsv,
			playerCount,
			_ => CharacterAvatarCatalog.DefaultAvatarId);
		var config = new NetworkSessionConfig(
			sessionId,
			"Test Room",
			string.Empty,
			matchId,
			matchSeed,
			NetworkTransport.Lan,
			playerCount,
			itemMode,
			winningScore,
			LobbyVisibility.Private);
		return TryCreate(
			packedScene,
			GameBoardLaunchOptions.Network(
				config,
				localPlayerIndex,
				playerNames,
				playerAvatarIds,
				waitForInitialize));
	}

	public bool ConfigureDeferredInitialization(GameBoardScene board)
	{
		return board.ConfigureForTestHarness(new GameBoardTestConfiguration(DeferBoardInitialization: true));
	}

	public bool ConfigureRecording(GameBoardScene board, string setupId)
	{
		return board.ConfigureForTestHarness(new GameBoardTestConfiguration(RecordingSetupId: setupId));
	}

	public bool ConfigurePerformanceTrace(GameBoardScene board, float minimumMilliseconds)
	{
		return board.ConfigureForTestHarness(new GameBoardTestConfiguration(
			PerformanceTrace: true,
			PerformanceTraceMinimumMilliseconds: minimumMilliseconds));
	}

	public int GetPlayerCount(GameBoardScene board)
	{
		return board.TestLaunchPlayerCount;
	}

	public bool IsAdventureTutorial(GameBoardScene board)
	{
		return board.TestLaunchIsAdventureTutorial;
	}

	public string GetAdventureEnemyAvatarId(GameBoardScene board)
	{
		return board.TestLaunchAdventureEnemyAvatarId;
	}

	private static GameBoardScene? TryCreate(PackedScene packedScene, GameBoardLaunchOptions options)
	{
		if (GameBoardSceneFactory.TryCreate(packedScene, options, out GameBoardScene board, out string error))
		{
			return board;
		}

		GD.PushError($"GameBoardTestHarness could not create a board: {error}");
		return null;
	}

	private static string[] BuildCsvValues(
		string csv,
		int count,
		Func<int, string> fallback)
	{
		string[] supplied = string.IsNullOrWhiteSpace(csv)
			? Array.Empty<string>()
			: csv.Split(',', StringSplitOptions.TrimEntries);
		var result = new string[count];
		for (int index = 0; index < result.Length; index++)
		{
			result[index] = index < supplied.Length && !string.IsNullOrWhiteSpace(supplied[index])
				? supplied[index]
				: fallback(index);
		}
		return result;
	}
}

public partial class GameBoardScene
{
	internal bool ConfigureForTestHarness(GameBoardTestConfiguration configuration)
	{
		if (_launchLifecycleState != LaunchLifecycleState.Configured || _runtimeConfiguration == null)
		{
			return false;
		}

		if (configuration.DeferBoardInitialization)
		{
			_runtimeConfiguration = RuntimeConfiguration with { NetworkWaitForInitialize = true };
		}
		if (!string.IsNullOrWhiteSpace(configuration.RecordingSetupId))
		{
			debug_recording_setup_id = configuration.RecordingSetupId.Trim();
		}
		if (configuration.PerformanceTrace)
		{
			debug_performance_trace = true;
			debug_performance_trace_min_ms = Math.Max(0.0f, configuration.PerformanceTraceMinimumMilliseconds);
		}
		return true;
	}

	internal int TestLaunchPlayerCount => RuntimeConfiguration.PlayerCount;
	internal bool TestLaunchIsAdventureTutorial => RuntimeConfiguration.IsAdventureTutorial;
	internal string TestLaunchAdventureEnemyAvatarId => RuntimeConfiguration.AdventureEnemyAvatarId;
}
#endif
