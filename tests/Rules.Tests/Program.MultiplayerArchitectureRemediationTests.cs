using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void AudioDisposalForbidsSynchronousDelayAndForcedGc()
	{
		string scriptsDirectory = Path.Combine(FindRepositoryRoot(), "scripts");
		string audioProductionText = string.Join(
			"\n",
			Directory.GetFiles(scriptsDirectory, "GameAudioManager*.cs")
				.Select(File.ReadAllText));

		False(Regex.IsMatch(audioProductionText, @"\bOS\s*\.\s*DelayMsec\s*\(", RegexOptions.CultureInvariant));
		False(Regex.IsMatch(audioProductionText, @"\bGC\s*\.\s*Collect\s*\(", RegexOptions.CultureInvariant));
		False(Regex.IsMatch(audioProductionText, @"\bGC\s*\.\s*WaitForPendingFinalizers\s*\(", RegexOptions.CultureInvariant));
	}

	private static void TestMuteAudioBlocksDirectStreamPlayerCreation()
	{
		string scriptsDirectory = Path.Combine(FindRepositoryRoot(), "scripts");
		string sfxText = File.ReadAllText(Path.Combine(scriptsDirectory, "GameAudioManager.Sfx.cs"));
		int directStreamOverloadStart = sfxText.IndexOf(
			"public void PlaySfx(\n\t\tAudioStream? stream,",
			StringComparison.Ordinal);
		int characterActionStart = sfxText.IndexOf(
			"public void PlayCharacterActionSfx(",
			directStreamOverloadStart,
			StringComparison.Ordinal);
		True(directStreamOverloadStart >= 0);
		True(characterActionStart > directStreamOverloadStart);

		string directStreamOverload = sfxText[directStreamOverloadStart..characterActionStart];
		True(directStreamOverload.Contains("if (_suppressPlayback || stream == null)", StringComparison.Ordinal));

		int sharedPlaybackStart = sfxText.IndexOf(
			"private void PlaySfxStream(",
			StringComparison.Ordinal);
		int suppressionGuard = sfxText.IndexOf(
			"if (_suppressPlayback)",
			sharedPlaybackStart,
			StringComparison.Ordinal);
		int playerRental = sfxText.IndexOf(
			"AudioStreamPlayer player = RentSfxPlayer();",
			sharedPlaybackStart,
			StringComparison.Ordinal);
		True(sharedPlaybackStart >= 0);
		True(suppressionGuard > sharedPlaybackStart);
		True(playerRental > suppressionGuard);

		string testsDirectory = Path.Combine(FindRepositoryRoot(), "tests");
		string testEnvironment = File.ReadAllText(Path.Combine(testsDirectory, "godot_test_env.sh"));
		True(testEnvironment.Contains(
			"export DECKLAND_TEST_MUTE_AUDIO=1",
			StringComparison.Ordinal));
		foreach (string runnerPath in Directory.GetFiles(testsDirectory, "run_*_smoke.sh"))
		{
			string runner = File.ReadAllText(runnerPath);
			if (!runner.Contains("--script res://tests/", StringComparison.Ordinal))
			{
				continue;
			}

			True(runner.Contains("godot_test_env.sh", StringComparison.Ordinal)
				|| runner.Contains("DECKLAND_TEST_MUTE_AUDIO=1", StringComparison.Ordinal));
		}
	}

	private static void GameBoardAsyncWorkStaysBehindLifetimeBoundary()
	{
		string scriptsDirectory = Path.Combine(FindRepositoryRoot(), "scripts");
		string[] boardFiles = Directory.GetFiles(scriptsDirectory, "GameBoardScene*.cs");
		foreach (string boardFile in boardFiles)
		{
			string fileName = Path.GetFileName(boardFile);
			string text = File.ReadAllText(boardFile);
			if (!string.Equals(fileName, "GameBoardScene.AsyncLifetime.cs", StringComparison.Ordinal))
			{
				False(text.Contains(".CreateTween(", StringComparison.Ordinal));
				False(text.Contains("GetTree().CreateTimer(", StringComparison.Ordinal));
				False(text.Contains("await ToSignal(", StringComparison.Ordinal));
				False(Regex.IsMatch(text, @"\basync\s+void\b", RegexOptions.CultureInvariant));
			}

			MatchCollection untrackedAsyncCalls = Regex.Matches(
				text,
				@"_\s*=\s*[^;\r\n]*Async\s*\(",
				RegexOptions.CultureInvariant);
			if (string.Equals(fileName, "GameBoardScene.NetworkVisualPlaybackController.cs", StringComparison.Ordinal))
			{
				Equal(1, untrackedAsyncCalls.Count);
				True(untrackedAsyncCalls[0].Value.Contains("ProcessAsync", StringComparison.Ordinal));
			}
			else
			{
				Equal(0, untrackedAsyncCalls.Count);
			}
		}

		string lifetimeText = File.ReadAllText(Path.Combine(scriptsDirectory, "BoardAsyncLifetime.cs"));
		True(lifetimeText.Contains("CancellationTokenSource", StringComparison.Ordinal));
		True(lifetimeText.Contains("CreateTimer", StringComparison.Ordinal));
		True(lifetimeText.Contains("TrackTween", StringComparison.Ordinal));
		True(lifetimeText.Contains("ObserveAsync", StringComparison.Ordinal));
	}

	private static void PhaseThreeLifecycleAndFallbackSmokesExist()
	{
		string testsDirectory = Path.Combine(FindRepositoryRoot(), "tests");
		string idleText = File.ReadAllText(Path.Combine(testsDirectory, "GameBoardIdlePerformanceTraceSmoke.gd"));
		string lifecycleText = File.ReadAllText(Path.Combine(testsDirectory, "GameBoardAsyncLifetimeShutdownSmoke.gd"));
		string missingAssetsText = File.ReadAllText(Path.Combine(testsDirectory, "DynamicBackgroundMissingAssetsSmoke.gd"));

		True(idleText.Contains("IDLE_OBSERVE_FRAMES := 300", StringComparison.Ordinal));
		True(idleText.Contains("DebugGetCurrentThreadAllocatedBytes", StringComparison.Ordinal));
		True(idleText.Contains("DebugGetNetworkVisualActivityCount", StringComparison.Ordinal));
		True(lifecycleText.Contains("DebugStartBoardAsyncLifetimeShutdownProbe", StringComparison.Ordinal));
		True(lifecycleText.Contains("DebugQueueDelayedNetworkVisualEventWithSnapshotFence", StringComparison.Ordinal));
		True(lifecycleText.Contains("DebugShowAdventureUpgradeSelectionOverlaySample", StringComparison.Ordinal));
		True(missingAssetsText.Contains("PermanentFallback", StringComparison.Ordinal));
		True(missingAssetsText.Contains("OBSERVE_FRAMES := 300", StringComparison.Ordinal));
		True(File.Exists(Path.Combine(testsDirectory, "run_game_board_async_lifetime_shutdown_smoke.sh")));
		True(File.Exists(Path.Combine(testsDirectory, "run_dynamic_background_missing_assets_smoke.sh")));
	}
}
