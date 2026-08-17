using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;

public sealed record GameBoardLaunchOptions
{
	internal GameBoardLaunchOptions()
	{
	}

	public int PlayerCount { get; init; }
	public bool ItemMode { get; init; }
	public int WinningScore { get; init; }
	public GameModeKind Mode { get; init; }
	public AdventureTutorialFlowKind? TutorialFlow { get; init; }
	public bool SoloMode { get; init; }
	public SoloAiDifficulty SoloAiDifficulty { get; init; }
	public int LocalPlayerIndex { get; init; }
	public string NetworkSessionId { get; init; } = string.Empty;
	public string NetworkMatchId { get; init; } = string.Empty;
	public long MatchSeed { get; init; }
	public ImmutableArray<string> PlayerNames { get; init; } = ImmutableArray<string>.Empty;
	public ImmutableArray<string> PlayerAvatarIds { get; init; } = ImmutableArray<string>.Empty;
	public bool NetworkWaitForInitialize { get; init; }
	public string AdventureEnemyAvatarId { get; init; } = string.Empty;

	public static GameBoardLaunchOptions Local(
		int playerCount,
		bool itemMode,
		int winningScore,
		string playerName = "")
	{
		return CreateStandard(
			GameModeKind.LocalStandard,
			playerCount,
			itemMode,
			winningScore,
			playerName);
	}

	public static GameBoardLaunchOptions Solo(
		int playerCount,
		bool itemMode,
		int winningScore,
		SoloAiDifficulty difficulty,
		string playerName = "")
	{
		GameBoardLaunchOptions options = CreateStandard(
			GameModeKind.SoloStandard,
			playerCount,
			itemMode,
			winningScore,
			playerName) with
		{
			SoloMode = true,
			SoloAiDifficulty = difficulty,
		};
		return Validate(options);
	}

	public static GameBoardLaunchOptions AdventureMain(
		string enemyAvatarId,
		string playerName = "",
		int winningScore = NetworkSessionDefaults.DefaultWinningScore,
		bool itemMode = true,
		SoloAiDifficulty difficulty = SoloAiDifficulty.Medium)
	{
		string normalizedAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(enemyAvatarId);
		if (!AdventureCampaignCatalog.TryGetMainStageIndex(normalizedAvatarId, out _))
		{
			throw new ArgumentException(
				$"Adventure main metadata does not define enemy avatar '{enemyAvatarId}'.",
				nameof(enemyAvatarId));
		}

		return Validate(new GameBoardLaunchOptions
		{
			PlayerCount = 2,
			ItemMode = itemMode,
			WinningScore = winningScore,
			Mode = GameModeKind.AdventureMain,
			SoloMode = true,
			SoloAiDifficulty = difficulty,
			PlayerNames = NormalizeNames(new[] { playerName, string.Empty }),
			AdventureEnemyAvatarId = normalizedAvatarId,
		});
	}

	public static GameBoardLaunchOptions AdventureTutorial(
		string enemyAvatarId,
		AdventureTutorialFlowKind flow,
		string playerName = "",
		int winningScore = NetworkSessionDefaults.DefaultWinningScore,
		bool itemMode = true,
		SoloAiDifficulty difficulty = SoloAiDifficulty.Medium)
	{
		if (!AdventureTutorialFlowRules.TryResolveForAvatar(enemyAvatarId, out AdventureTutorialFlowKind metadataFlow, out string error))
		{
			throw new ArgumentException(error, nameof(enemyAvatarId));
		}
		if (metadataFlow != flow)
		{
			throw new ArgumentException(
				$"Tutorial flow '{flow}' does not match metadata flow '{metadataFlow}' for '{enemyAvatarId}'.",
				nameof(flow));
		}

		return Validate(new GameBoardLaunchOptions
		{
			PlayerCount = 2,
			ItemMode = itemMode,
			WinningScore = winningScore,
			Mode = GameModeKind.AdventureTutorial,
			TutorialFlow = flow,
			SoloMode = true,
			SoloAiDifficulty = difficulty,
			PlayerNames = NormalizeNames(new[] { playerName, string.Empty }),
			AdventureEnemyAvatarId = CharacterAvatarCatalog.NormalizeAvatarId(enemyAvatarId),
		});
	}

	public static GameBoardLaunchOptions Network(
		NetworkSessionConfig config,
		int localPlayerIndex,
		IReadOnlyList<string> playerNames,
		IReadOnlyList<string> playerAvatarIds,
		bool waitForInitialize = true)
	{
		return Validate(new GameBoardLaunchOptions
		{
			PlayerCount = config.PlayerCount,
			ItemMode = config.ItemMode,
			WinningScore = config.WinningScore,
			Mode = GameModeKind.NetworkStandard,
			LocalPlayerIndex = localPlayerIndex,
			NetworkSessionId = config.SessionId?.Trim() ?? string.Empty,
			NetworkMatchId = config.MatchId?.Trim() ?? string.Empty,
			MatchSeed = config.MatchSeed,
			PlayerNames = NormalizeNames(playerNames),
			PlayerAvatarIds = NormalizeNames(playerAvatarIds),
			NetworkWaitForInitialize = waitForInitialize,
		});
	}

	internal static GameBoardLaunchOptions WithNetworkInitialization(
		GameBoardLaunchOptions options,
		NetworkBoardInitialization initialization)
	{
		return Validate(options with
		{
			PlayerCount = initialization.Config.PlayerCount,
			ItemMode = initialization.Config.ItemMode,
			WinningScore = initialization.Config.WinningScore,
			LocalPlayerIndex = initialization.LocalPlayerIndex,
			NetworkSessionId = initialization.Config.SessionId.Trim(),
			NetworkMatchId = initialization.Config.MatchId.Trim(),
			MatchSeed = initialization.Config.MatchSeed,
			PlayerNames = NormalizeNames(initialization.PlayerNames),
			PlayerAvatarIds = NormalizeNames(initialization.PlayerAvatarIds),
			NetworkWaitForInitialize = false,
		});
	}

	internal static GameBoardLaunchOptions Validate(GameBoardLaunchOptions options)
	{
		if (!Enum.IsDefined(options.Mode))
		{
			throw new ArgumentOutOfRangeException(nameof(options.Mode), options.Mode, "Game mode is invalid.");
		}
		if (options.PlayerCount is < NetworkSessionDefaults.MinPlayers or > NetworkSessionDefaults.MaxPlayers)
		{
			throw new ArgumentOutOfRangeException(nameof(options.PlayerCount), options.PlayerCount, "Player count is outside the supported range.");
		}
		if (options.WinningScore is not (50 or 100 or 200))
		{
			throw new ArgumentOutOfRangeException(nameof(options.WinningScore), options.WinningScore, "Winning score must be 50, 100, or 200.");
		}
		if (!Enum.IsDefined(options.SoloAiDifficulty))
		{
			throw new ArgumentOutOfRangeException(nameof(options.SoloAiDifficulty), options.SoloAiDifficulty, "Solo AI difficulty is invalid.");
		}

		if (options.Mode == GameModeKind.NetworkStandard)
		{
			if (string.IsNullOrWhiteSpace(options.NetworkSessionId)
				|| string.IsNullOrWhiteSpace(options.NetworkMatchId)
				|| options.LocalPlayerIndex < 0
				|| options.LocalPlayerIndex >= options.PlayerCount
				|| options.PlayerNames.Length != options.PlayerCount
				|| options.PlayerAvatarIds.Length != options.PlayerCount)
			{
				throw new ArgumentException("Network launch options must contain valid session, match, seat, name, and avatar data.", nameof(options));
			}
		}
		else if (!string.IsNullOrWhiteSpace(options.NetworkSessionId)
			|| !string.IsNullOrWhiteSpace(options.NetworkMatchId)
			|| options.NetworkWaitForInitialize)
		{
			throw new ArgumentException("Only network launch options may contain network session state.", nameof(options));
		}

		switch (options.Mode)
		{
			case GameModeKind.LocalStandard when options.SoloMode
				|| options.TutorialFlow.HasValue
				|| !string.IsNullOrWhiteSpace(options.AdventureEnemyAvatarId):
				throw new ArgumentException("Local launch options contain an incompatible mode flag.", nameof(options));
			case GameModeKind.SoloStandard when !options.SoloMode
				|| options.TutorialFlow.HasValue
				|| !string.IsNullOrWhiteSpace(options.AdventureEnemyAvatarId):
				throw new ArgumentException("Solo launch options contain an incompatible mode flag.", nameof(options));
			case GameModeKind.AdventureMain when !options.SoloMode
				|| options.TutorialFlow.HasValue
				|| !AdventureCampaignCatalog.IsMainAvatar(options.AdventureEnemyAvatarId):
				throw new ArgumentException("Adventure main launch options do not match campaign metadata.", nameof(options));
			case GameModeKind.AdventureTutorial when !options.SoloMode
				|| !options.TutorialFlow.HasValue
				|| !AdventureTutorialFlowRules.TryResolveForAvatar(
					options.AdventureEnemyAvatarId,
					out AdventureTutorialFlowKind metadataFlow,
					out _)
				|| metadataFlow != options.TutorialFlow.Value:
				throw new ArgumentException("Adventure tutorial launch options do not match campaign metadata.", nameof(options));
			case GameModeKind.NetworkStandard when options.SoloMode
				|| options.TutorialFlow.HasValue
				|| !string.IsNullOrWhiteSpace(options.AdventureEnemyAvatarId):
				throw new ArgumentException("Network launch options contain an incompatible mode flag.", nameof(options));
		}

		return options with
		{
			NetworkSessionId = options.NetworkSessionId.Trim(),
			NetworkMatchId = options.NetworkMatchId.Trim(),
			AdventureEnemyAvatarId = options.AdventureEnemyAvatarId.Trim(),
		};
	}

	private static GameBoardLaunchOptions CreateStandard(
		GameModeKind mode,
		int playerCount,
		bool itemMode,
		int winningScore,
		string playerName)
	{
		return Validate(new GameBoardLaunchOptions
		{
			PlayerCount = playerCount,
			ItemMode = itemMode,
			WinningScore = winningScore,
			Mode = mode,
			SoloMode = mode == GameModeKind.SoloStandard,
			SoloAiDifficulty = SoloAiDifficulty.Medium,
			PlayerNames = NormalizeNames(new[] { playerName }),
		});
	}

	private static ImmutableArray<string> NormalizeNames(IEnumerable<string> values)
	{
		var builder = ImmutableArray.CreateBuilder<string>();
		foreach (string value in values)
		{
			builder.Add(value?.Trim() ?? string.Empty);
		}
		return builder.ToImmutable();
	}
}
