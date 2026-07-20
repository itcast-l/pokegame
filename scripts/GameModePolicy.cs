using System;

public sealed class GameModePolicy
{
	private GameModePolicy(GameModeKind kind)
	{
		Kind = kind;
	}

	public GameModeKind Kind { get; }
	public bool IsNetworkStandard => Kind == GameModeKind.NetworkStandard;
	public bool IsAdventureTutorial => Kind == GameModeKind.AdventureTutorial;
	public bool IsAdventureMain => Kind == GameModeKind.AdventureMain;
	public bool IsAdventure => IsAdventureTutorial || IsAdventureMain;
	public bool IsSoloStandard => Kind == GameModeKind.SoloStandard;
	public bool IsLocalStandard => Kind == GameModeKind.LocalStandard;
	public bool CanReadAdventureRun => IsAdventureMain;
	public bool CanWriteAdventureRun => IsAdventureMain;

	public static GameModePolicy LocalStandard { get; } = new(GameModeKind.LocalStandard);
	public static GameModePolicy SoloStandard { get; } = new(GameModeKind.SoloStandard);
	public static GameModePolicy AdventureTutorial { get; } = new(GameModeKind.AdventureTutorial);
	public static GameModePolicy AdventureMain { get; } = new(GameModeKind.AdventureMain);
	public static GameModePolicy NetworkStandard { get; } = new(GameModeKind.NetworkStandard);

	public static GameModePolicy Derive(
		bool hasNetworkSession,
		bool adventureMode,
		bool adventureTutorialMode,
		bool soloMode)
	{
		if (hasNetworkSession)
		{
			return NetworkStandard;
		}

		if (adventureMode && adventureTutorialMode)
		{
			return AdventureTutorial;
		}

		if (adventureMode)
		{
			return AdventureMain;
		}

		if (soloMode)
		{
			return SoloStandard;
		}

		return LocalStandard;
	}

	public static GameModePolicy FromKind(GameModeKind kind)
	{
		return kind switch
		{
			GameModeKind.NetworkStandard => NetworkStandard,
			GameModeKind.AdventureTutorial => AdventureTutorial,
			GameModeKind.AdventureMain => AdventureMain,
			GameModeKind.SoloStandard => SoloStandard,
			_ => LocalStandard,
		};
	}

	public override string ToString() => Kind.ToString();
}
