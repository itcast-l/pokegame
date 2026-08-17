using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private const int GameBoardPrivateFieldBaseline = 364;
	private const int NetworkManagerPrivateFieldBaseline = 157;

	private static void GameBoardExtractedDomainsHaveIndependentOwners()
	{
		string scriptsRoot = Path.Combine(FindRepositoryRoot(), "scripts");
		string selectionSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"GameBoardScene.SelectionOverlayController.cs"));
		string saveGateSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"AdventureSaveGateCoordinator.cs"));
		string visualSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"GameBoardScene.NetworkVisualPlaybackController.cs"));
		string captureSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"GameBoardScene.CaptureResolutionCoordinator.cs"));

		True(Regex.IsMatch(
			selectionSource,
			@"^internal sealed class SelectionOverlayCoordinator$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		True(selectionSource.Contains(
			"private SelectionOverlaySession? _activeSession;",
			StringComparison.Ordinal));
		True(selectionSource.Contains("ReleaseSubscriptions()", StringComparison.Ordinal));
		False(selectionSource.Contains("_activeSelectionOverlaySession", StringComparison.Ordinal));

		True(Regex.IsMatch(
			saveGateSource,
			@"^internal sealed class AdventureSaveGateCoordinator$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		True(saveGateSource.Contains(
			"internal AdventureSaveGateState State { get; private set; }",
			StringComparison.Ordinal));
		True(saveGateSource.Contains("private static readonly double[] RetryDelays", StringComparison.Ordinal));
		False(saveGateSource.Contains("GameBoardScene", StringComparison.Ordinal));

		True(Regex.IsMatch(
			visualSource,
			@"^internal sealed class NetworkVisualPlaybackController$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		True(visualSource.Contains(
			"private readonly Queue<NetworkVisualEvent> _pendingEvents",
			StringComparison.Ordinal));

		True(captureSource.Contains(
			"Task<CardPlayBranchResult>> _resolveAsync;",
			StringComparison.Ordinal));
		False(captureSource.Contains(
			"new CaptureResolutionCoordinator(this)",
			StringComparison.Ordinal));
		False(Regex.IsMatch(
			captureSource,
			@"private\s+(?:readonly\s+)?GameBoardScene\??\s+_",
			RegexOptions.CultureInvariant));
	}

	private static void NetworkSceneLoadDomainHasIndependentOwner()
	{
		string scriptsRoot = Path.Combine(FindRepositoryRoot(), "scripts");
		string coordinatorSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"NetworkSceneLoadCoordinator.cs"));
		string managerSource = string.Join(
			Environment.NewLine,
			Directory.GetFiles(scriptsRoot, "NetworkManager*.cs")
				.Select(File.ReadAllText));

		True(Regex.IsMatch(
			coordinatorSource,
			@"^internal sealed class NetworkSceneLoadCoordinator$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		True(coordinatorSource.Contains(
			"private readonly HashSet<int> _expectedSeats",
			StringComparison.Ordinal));
		True(coordinatorSource.Contains(
			"private readonly HashSet<int> _loadedSeats",
			StringComparison.Ordinal));
		True(managerSource.Contains(
			"private readonly NetworkSceneLoadCoordinator _sceneLoadCoordinator = new();",
			StringComparison.Ordinal));
		False(managerSource.Contains("_loadHandshake", StringComparison.Ordinal));
		False(File.Exists(Path.Combine(scriptsRoot, "SceneLoadHandshakeTracker.cs")));
	}

	private static void NetworkLobbyRosterDomainHasIndependentOwner()
	{
		string scriptsRoot = Path.Combine(FindRepositoryRoot(), "scripts");
		string rosterSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"NetworkLobbyRoster.cs"));
		string managerSource = string.Join(
			Environment.NewLine,
			Directory.GetFiles(scriptsRoot, "NetworkManager*.cs")
				.Select(File.ReadAllText));

		True(Regex.IsMatch(
			rosterSource,
			@"^internal sealed class NetworkLobbyRoster$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		foreach (string ownedField in new[]
		{
			"private readonly List<NetworkPlayerPublicState> _players",
			"private readonly List<SeatControlState> _seatControls",
			"private readonly Dictionary<int, ReconnectClaimRecord> _reconnectClaimsBySeat",
			"private readonly Dictionary<int, string> _issuedSeatCredentialsBySeat",
			"private readonly Dictionary<int, bool> _pendingReadyByPeerId",
		})
		{
			True(rosterSource.Contains(ownedField, StringComparison.Ordinal));
		}
		True(managerSource.Contains(
			"private readonly NetworkLobbyRoster _lobbyRoster = new();",
			StringComparison.Ordinal));
		foreach (string removedOwner in new[]
		{
			"_players",
			"_seatControlStates",
			"_reconnectClaimsBySeat",
			"_issuedSeatCredentialsBySeat",
			"_pendingLobbyReadyByPeerId",
		})
		{
			False(managerSource.Contains(removedOwner, StringComparison.Ordinal));
		}
		False(Regex.IsMatch(
			managerSource,
			@"_lobbyRoster\.(?:Players|SeatControls|ReconnectClaimsBySeat|IssuedSeatCredentialsBySeat|PendingReadyByPeerId)(?:\[[^\]]+\]\s*=|\.(?:Add|AddRange|Clear|Insert|Remove|RemoveAt)\()",
			RegexOptions.CultureInvariant));
	}

	private static void NetworkReconnectDomainHasIndependentOwner()
	{
		string scriptsRoot = Path.Combine(FindRepositoryRoot(), "scripts");
		string coordinatorSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"NetworkReconnectCoordinator.cs"));
		string managerSource = string.Join(
			Environment.NewLine,
			Directory.GetFiles(scriptsRoot, "NetworkManager*.cs")
				.Select(File.ReadAllText));

		True(Regex.IsMatch(
			coordinatorSource,
			@"^internal sealed class NetworkReconnectCoordinator$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		True(coordinatorSource.Contains(
			"private NetworkReconnectContext _context;",
			StringComparison.Ordinal));
		True(coordinatorSource.Contains(
			"private double _windowDeadlineSeconds;",
			StringComparison.Ordinal));
		True(coordinatorSource.Contains(
			"private double _retryDeadlineSeconds;",
			StringComparison.Ordinal));
		True(managerSource.Contains(
			"private readonly NetworkReconnectCoordinator _reconnectCoordinator = new();",
			StringComparison.Ordinal));
		foreach (string removedOwner in new[]
		{
			"_nextReconnectAttemptAt",
			"_reconnectAttemptInFlight",
			"_reconnectAttemptStartedAt",
			"_reconnectWindowStartedAt",
			"ClientReconnectWindowSeconds",
			"ReconnectRetrySeconds",
		})
		{
			False(managerSource.Contains(removedOwner, StringComparison.Ordinal));
		}
	}

	private static void ExtractedCoordinatorsForbidGodObjectAndTreeFields()
	{
		string scriptsRoot = Path.Combine(FindRepositoryRoot(), "scripts");
		string selectionSource = ExtractSourceSegment(
			File.ReadAllText(Path.Combine(scriptsRoot, "GameBoardScene.SelectionOverlayController.cs")),
			"internal sealed class SelectionOverlayCoordinator",
			"public partial class GameBoardScene");
		string saveGateSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"AdventureSaveGateCoordinator.cs"));
		string visualSource = ExtractSourceSegment(
			File.ReadAllText(Path.Combine(scriptsRoot, "GameBoardScene.NetworkVisualPlaybackController.cs")),
			"internal sealed class NetworkVisualPlaybackController",
			endMarker: null);
		string captureSource = ExtractSourceSegment(
			File.ReadAllText(Path.Combine(scriptsRoot, "GameBoardScene.CaptureResolutionCoordinator.cs")),
			"private sealed class CaptureResolutionCoordinator",
			"private async Task<CardPlayBranchResult> ResolvePlayedCardCaptureCoreAsync(");
		string sceneLoadSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"NetworkSceneLoadCoordinator.cs"));
		string rosterSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"NetworkLobbyRoster.cs"));
		string reconnectSource = File.ReadAllText(Path.Combine(
			scriptsRoot,
			"NetworkReconnectCoordinator.cs"));
		var forbiddenOwnerField = new Regex(
			@"private\s+(?:readonly\s+)?(?:GameBoardScene|NetworkManager|SceneTree|NodePath)\??\s+_",
			RegexOptions.CultureInvariant);

		foreach (string source in new[]
		{
			selectionSource,
			saveGateSource,
			visualSource,
			captureSource,
			sceneLoadSource,
			rosterSource,
			reconnectSource,
		})
		{
			False(forbiddenOwnerField.IsMatch(source));
		}
	}

	private static void GodObjectPrivateFieldCountsStayBelowArchitectureBaseline()
	{
		string scriptsRoot = Path.Combine(FindRepositoryRoot(), "scripts");
		int gameBoardFieldCount = CountPrivateFields(
			Directory.GetFiles(scriptsRoot, "GameBoardScene*.cs"));
		int networkManagerFieldCount = CountPrivateFields(
			Directory.GetFiles(scriptsRoot, "NetworkManager*.cs"));

		True(gameBoardFieldCount < GameBoardPrivateFieldBaseline);
		True(networkManagerFieldCount < NetworkManagerPrivateFieldBaseline);
	}

	private static string ExtractSourceSegment(
		string source,
		string startMarker,
		string? endMarker)
	{
		int start = source.IndexOf(startMarker, StringComparison.Ordinal);
		True(start >= 0);
		int end = endMarker == null
			? source.Length
			: source.IndexOf(endMarker, start, StringComparison.Ordinal);
		True(end > start);
		return source[start..end];
	}

	private static int CountPrivateFields(string[] paths)
	{
		var fieldPattern = new Regex(
			@"^[ \t]*private[ \t]+(?:(?:static|readonly|const|volatile|new|unsafe)[ \t]+)*[^\r\n()=;{}]+[ \t]+_[A-Za-z0-9_]+(?:[ \t]*=[^;\r\n]*)?;",
			RegexOptions.Multiline | RegexOptions.CultureInvariant);
		return paths.Sum(path => fieldPattern.Matches(File.ReadAllText(path)).Count);
	}
}
