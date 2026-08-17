using System.Collections.Immutable;

public sealed record GameBoardRuntimeConfiguration
{
	public int PlayerCount { get; init; }
	public bool ItemMode { get; init; }
	public int WinningScore { get; init; }
	public GameModeKind Mode { get; init; }
	public bool IsAdventure => Mode is GameModeKind.AdventureMain or GameModeKind.AdventureTutorial;
	public bool IsAdventureTutorial => Mode == GameModeKind.AdventureTutorial;
	public bool IsSolo => Mode is GameModeKind.SoloStandard or GameModeKind.AdventureMain or GameModeKind.AdventureTutorial;
	public AdventureTutorialFlowKind? TutorialFlow { get; init; }
	public SoloAiDifficulty SoloAiDifficulty { get; init; }
	public int LocalPlayerIndex { get; init; }
	public string NetworkSessionId { get; init; } = string.Empty;
	public string NetworkMatchId { get; init; } = string.Empty;
	public long MatchSeed { get; init; }
	public ImmutableArray<string> PlayerNames { get; init; } = ImmutableArray<string>.Empty;
	public ImmutableArray<string> PlayerAvatarIds { get; init; } = ImmutableArray<string>.Empty;
	public bool NetworkWaitForInitialize { get; init; }
	public string AdventureEnemyAvatarId { get; init; } = string.Empty;

	public static GameBoardRuntimeConfiguration FromLaunchOptions(GameBoardLaunchOptions options)
	{
		GameBoardLaunchOptions validated = GameBoardLaunchOptions.Validate(options);
		return new GameBoardRuntimeConfiguration
		{
			PlayerCount = validated.PlayerCount,
			ItemMode = validated.ItemMode,
			WinningScore = validated.WinningScore,
			Mode = validated.Mode,
			TutorialFlow = validated.TutorialFlow,
			SoloAiDifficulty = validated.SoloAiDifficulty,
			LocalPlayerIndex = validated.LocalPlayerIndex,
			NetworkSessionId = validated.NetworkSessionId,
			NetworkMatchId = validated.NetworkMatchId,
			MatchSeed = validated.MatchSeed,
			PlayerNames = validated.PlayerNames,
			PlayerAvatarIds = validated.PlayerAvatarIds,
			NetworkWaitForInitialize = validated.NetworkWaitForInitialize,
			AdventureEnemyAvatarId = validated.AdventureEnemyAvatarId,
		};
	}

	internal GameBoardRuntimeConfiguration ApplyNetworkInitialization(NetworkBoardInitialization initialization)
	{
		if (Mode != GameModeKind.NetworkStandard
			|| !NetworkWaitForInitialize
			|| !string.Equals(NetworkSessionId, initialization.Config.SessionId, System.StringComparison.Ordinal)
			|| !string.Equals(NetworkMatchId, initialization.Config.MatchId, System.StringComparison.Ordinal)
			|| LocalPlayerIndex != initialization.LocalPlayerIndex)
		{
			throw new System.InvalidOperationException(
				"Network initialization does not match the configured session, match, seat, or lifecycle state.");
		}

		return FromLaunchOptions(GameBoardLaunchOptions.WithNetworkInitialization(
			new GameBoardLaunchOptions
			{
				PlayerCount = PlayerCount,
				ItemMode = ItemMode,
				WinningScore = WinningScore,
				Mode = Mode,
				TutorialFlow = TutorialFlow,
				SoloMode = IsSolo,
				SoloAiDifficulty = SoloAiDifficulty,
				LocalPlayerIndex = LocalPlayerIndex,
				NetworkSessionId = NetworkSessionId,
				NetworkMatchId = NetworkMatchId,
				MatchSeed = MatchSeed,
				PlayerNames = PlayerNames,
				PlayerAvatarIds = PlayerAvatarIds,
				NetworkWaitForInitialize = NetworkWaitForInitialize,
				AdventureEnemyAvatarId = AdventureEnemyAvatarId,
			},
			initialization));
	}
}
