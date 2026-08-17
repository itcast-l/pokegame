using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Deckland.Rules;

internal static partial class Program
{
	private static void NetworkReplicaVisualsDoNotMutateRuleCollections()
	{
		string repoRoot = FindRepositoryRoot();
		string boardText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.cs"));
		string cardsText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkCards.cs"));
		string predictionText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkPrediction.cs"));
		string visualQueueText = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.NetworkVisualQueue*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string snapshotText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkSnapshot.cs"));
		string controllerText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkVisualPlaybackController.cs"));
		string debugText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.Debug.Network.cs"));
		string authorityStateText = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.NetworkAuthority*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));

		True(boardText.Contains(
			"private IReadOnlyList<IReadOnlyList<PlayingCard>> _playerHands =>",
			StringComparison.Ordinal));
		True(boardText.Contains(
			"? presentation.PlayerHands",
			StringComparison.Ordinal));
		True(boardText.Contains(
			": CurrentRuleProjection.PlayerHands;",
			StringComparison.Ordinal));
		True(boardText.Contains(
			"private IReadOnlyList<TableCard> _tableCards =>",
			StringComparison.Ordinal));
		True(boardText.Contains(
			": CurrentRuleProjection.TableCards;",
			StringComparison.Ordinal));
		False(boardText.Contains("GameBoardState _state", StringComparison.Ordinal));
		True(cardsText.Contains(
			"private sealed class NetworkReplicaPresentationState",
			StringComparison.Ordinal));
		True(cardsText.Contains(
			"IReadOnlyList<IReadOnlyList<PlayingCard>> playerDeckSource",
			StringComparison.Ordinal));
		True(cardsText.Contains(
			"TableCard.Restore(",
			StringComparison.Ordinal));
		True(controllerText.Contains(
			"if (!EnsureNetworkReplicaPresentationState())",
			StringComparison.Ordinal));
		True(snapshotText.Contains(
			"if (!TryApplyReplicaRuleSnapshot(snapshot))",
			StringComparison.Ordinal));
		True(debugText.Contains(
			"DebugUseReplicaRuleSession(preserveCurrentStateForPresentation: true);",
			StringComparison.Ordinal));
		True(debugText.Contains(
			"TryApplyReplicaRuleSnapshot(validatedSnapshot)",
			StringComparison.Ordinal));
		True(authorityStateText.Contains(
			"TrySubmitGrantItemToHandRuleCommand(",
			StringComparison.Ordinal));
		False(Regex.IsMatch(
			authorityStateText,
			@"_playerItemHands\s*\[[^\r\n;]+\]\s*\.(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear)\s*\("));

		string replicaMutationSources = string.Concat(
			predictionText,
			visualQueueText,
			snapshotText);
		foreach (string forbiddenMutation in new[]
		{
			"_tableCards.Add(",
			"_tableCards.AddRange(",
			"_tableCards.Insert(",
			"_tableCards.RemoveAt(",
			"_tableCards.RemoveRange(",
			"_tableCards.Clear(",
			"_playerDecks[",
			"_playerHands[",
			"_playerItemHands[",
		})
		{
			if (forbiddenMutation is "_playerDecks[" or "_playerHands[" or "_playerItemHands[")
			{
				continue;
			}
			False(replicaMutationSources.Contains(forbiddenMutation, StringComparison.Ordinal));
		}

		False(Regex.IsMatch(
			predictionText + visualQueueText,
			@"_(?:playerDecks|playerHands|playerItemHands)\s*\[[^\r\n;]+\]\s*\.(?:Add|AddRange|Insert|Remove|RemoveAt|RemoveRange|Clear)\s*\("));
		False(Regex.IsMatch(
			predictionText + visualQueueText,
			@"_(?:playerHands|playerItemHands|tableCards)\s*\[[^\r\n;]+\]\s*="));
		False(snapshotText.Contains("ApplyValidatedCountArray(", StringComparison.Ordinal));
		False(snapshotText.Contains("ApplyHandsFromSnapshot(", StringComparison.Ordinal));
		False(snapshotText.Contains("ApplyItemHandsFromSnapshot(", StringComparison.Ordinal));
		False(snapshotText.Contains("ApplyTableCardsFromSnapshot(", StringComparison.Ordinal));
		False(predictionText.Contains("AttachMagnetToHandCard(", StringComparison.Ordinal));
		False(visualQueueText.Contains("Shuffle(_player", StringComparison.Ordinal));
	}

	private static void GameBoardNetworkVisualPlaybackUsesIndependentController()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.Network.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualQueue.cs");
		string playbackPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualQueue.Playback.cs");
		string controllerPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.NetworkVisualPlaybackController.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);
		True(File.Exists(playbackPath));
		string playbackText = File.ReadAllText(playbackPath);
		True(File.Exists(controllerPath));
		string controllerText = File.ReadAllText(controllerPath);

		True(partialText.Contains("public partial class GameBoardScene", StringComparison.Ordinal));
		True(partialText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		True(partialText.Contains("internal sealed record NetworkVisualEvent", StringComparison.Ordinal));
		True(partialText.Contains("private bool TryDecodeNetworkVisualEvent", StringComparison.Ordinal));
		True(playbackText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(playbackText.Contains("private async Task PlayNetworkItemCardVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(playbackText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		True(partialText.Contains("NetworkCaptureRevealEntry[] CaptureRevealEntries", StringComparison.Ordinal));
		True(Regex.IsMatch(
			controllerText,
			@"^internal sealed class NetworkVisualPlaybackController$",
			RegexOptions.Multiline | RegexOptions.CultureInvariant));
		True(controllerText.Contains("private readonly Queue<NetworkVisualEvent> _pendingEvents", StringComparison.Ordinal));
		True(controllerText.Contains("private readonly Func<NetworkVisualEvent, Task> _playEventAsync", StringComparison.Ordinal));
		True(controllerText.Contains("private readonly Func<ValidatedNetworkBoardSnapshot, bool> _applySnapshot", StringComparison.Ordinal));
		True(controllerText.Contains("private readonly Func<NetworkVisualEvent, bool> _recoverPlaybackFailure", StringComparison.Ordinal));
		True(controllerText.Contains("private readonly Action _onQueueStable", StringComparison.Ordinal));
		False(Regex.IsMatch(
			controllerText,
			@"private\s+(?:readonly\s+)?(?:GameBoardScene|SceneTree|NodePath)\??\s+_",
			RegexOptions.CultureInvariant));
		True(controllerText.Contains("private async Task ProcessAsync()", StringComparison.Ordinal));
		True(controllerText.Contains("IsFastForwarding = true", StringComparison.Ordinal));
		True(controllerText.Contains("_pendingEvents.Clear()", StringComparison.Ordinal));
		False(partialText.Contains("IsUsableNetworkVisualEvent(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadNetworkInt(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadNetworkString(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadArray(payload", StringComparison.Ordinal));
		False(partialText.Contains("= ReadIntArray(payload", StringComparison.Ordinal));
		False(partialText.Contains("Split(',', StringSplitOptions", StringComparison.Ordinal));
		False(mainText.Contains("internal void QueueNetworkCardPlayedEvent(Godot.Collections.Dictionary payload)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(mainText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(partialText.Contains("private async Task PlayNetworkCardPlayedEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(partialText.Contains("private async Task PlayNetworkCaptureRevealVisualEventAsync(NetworkVisualEvent payload)", StringComparison.Ordinal));
		False(partialText.Contains("private async Task ProcessNetworkVisualQueueAsync()", StringComparison.Ordinal));
	}

	private static void NetworkVisualQueueAdmitsOnlyBoundedPendingEvents()
	{
		for (int pendingEventCount = 0; pendingEventCount < NetworkVisualQueuePolicy.MaxPendingEventCount; pendingEventCount++)
		{
			Equal(
				NetworkVisualQueueAdmission.Enqueue,
				NetworkVisualQueuePolicy.ClassifyAdmission(pendingEventCount, fastForwarding: false));
		}

		Equal(
			NetworkVisualQueueAdmission.EnterFastForward,
			NetworkVisualQueuePolicy.ClassifyAdmission(
				NetworkVisualQueuePolicy.MaxPendingEventCount,
				fastForwarding: false));
		Equal(
			NetworkVisualQueueAdmission.EnterFastForward,
			NetworkVisualQueuePolicy.ClassifyAdmission(int.MaxValue, fastForwarding: false));
	}

	private static void NetworkVisualQueueEntersAndRemainsInFastForward()
	{
		Equal(
			NetworkVisualQueueAdmission.DropWhileFastForwarding,
			NetworkVisualQueuePolicy.ClassifyAdmission(0, fastForwarding: true));
		Equal(
			NetworkVisualQueueAdmission.DropWhileFastForwarding,
			NetworkVisualQueuePolicy.ClassifyAdmission(
				NetworkVisualQueuePolicy.MaxPendingEventCount,
				fastForwarding: true));

		True(NetworkVisualQueuePolicy.ClassifyAdmission(0, false) == NetworkVisualQueueAdmission.Enqueue);
		True(NetworkVisualQueuePolicy.ClassifyAdmission(0, false) != NetworkVisualQueueAdmission.DropWhileFastForwarding);
	}

	private static void NetworkVisualQueueSaturatingCountersStayNonnegative()
	{
		Equal(64, NetworkVisualQueuePolicy.SaturatingAdd(0, 64));
		Equal(12, NetworkVisualQueuePolicy.SaturatingAdd(12, 0));
		Equal(12, NetworkVisualQueuePolicy.SaturatingAdd(12, -4));
		Equal(int.MaxValue, NetworkVisualQueuePolicy.SaturatingAdd(int.MaxValue - 1, 64));
		True(NetworkVisualQueuePolicy.SaturatingAdd(0, int.MaxValue) >= 0);
	}

	private static void NetworkVisualPlaybackFailuresStayIsolatedFromAuthority()
	{
		string repoRoot = FindRepositoryRoot();
		string controllerText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkVisualPlaybackController.cs"));
		string queueText = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.NetworkVisualQueue*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string transactionText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.NetworkAuthorityTransaction.cs"));
		string replicationText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"NetworkManager.BoardReplication.cs"));
		string lifetimeText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"BoardAsyncLifetime.cs"));
		string tableCaptureFlowText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.TableCaptureFlow.cs"));
		string tableCardTriggersText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.TableCardTriggers.cs"));
		string baseCardEnchantsText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.BaseCardEnchants.cs"));
		string turnEndText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.TurnEndState.cs"));
		string resultsText = File.ReadAllText(Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.Results.cs"));
		string smokePath = Path.Combine(
			repoRoot,
			"tests",
			"NetworkVisualFailureIsolationSmoke.gd");
		string boundsSmokePath = Path.Combine(
			repoRoot,
			"tests",
			"NetworkSnapshotBoundsSmoke.gd");

		True(controllerText.Contains("PlaybackFailureCount", StringComparison.Ordinal));
		True(controllerText.Contains("catch (Exception error)", StringComparison.Ordinal));
		True(controllerText.Contains("_recoverPlaybackFailure(visualEvent)", StringComparison.Ordinal));
		True(controllerText.Contains("IsFastForwarding = true", StringComparison.Ordinal));
		True(queueText.Contains("ActionIdKey", StringComparison.Ordinal));
		True(queueText.Contains("IsAuthorityHostVisualOnly", StringComparison.Ordinal));
		True(queueText.Contains("PlayNetworkAuthorityHostVisualEventAsync", StringComparison.Ordinal));
		True(transactionText.Contains("_networkAuthorityStagedHostVisualEvents", StringComparison.Ordinal));
		True(transactionText.Contains("QueueCommittedNetworkAuthorityHostVisualEvents", StringComparison.Ordinal));
		True(transactionText.Contains("_networkAuthorityRuleCenterArenaGlobalPosition = _centerArena.GlobalPosition", StringComparison.Ordinal));
		True(transactionText.Contains("_networkAuthorityRuleTableLayoutRect = GetTableCardLayoutRect()", StringComparison.Ordinal));
		True(transactionText.Contains("PresentCommittedNetworkAuthorityMatchOutcome();", StringComparison.Ordinal));
		True(replicationText.Contains("StageBoardVisualEventForLocalHost", StringComparison.Ordinal));
		True(replicationText.Contains("if (LocalSeatIndex != actorSeatIndex)", StringComparison.Ordinal));
		True(lifetimeText.Contains("!tween.IsValid()", StringComparison.Ordinal));
		True(tableCaptureFlowText.Contains("GetAuthorityRuleSafeTableLayoutRect()", StringComparison.Ordinal));
		False(tableCaptureFlowText.Contains("_centerArena.GlobalPosition", StringComparison.Ordinal));
		True(tableCardTriggersText.Contains("centerArenaGlobalPosition + GetTableCardPosition", StringComparison.Ordinal));
		False(baseCardEnchantsText.Contains("_centerArena.GlobalPosition + GetTableCardPosition", StringComparison.Ordinal));
		True(turnEndText.Contains("PresentCommittedNetworkAuthorityMatchOutcome()", StringComparison.Ordinal));
		True(turnEndText.Contains("if (!_networkAuthorityRuleTransactionActive", StringComparison.Ordinal));
		True(resultsText.Contains("if (_networkAuthorityRuleTransactionActive)", StringComparison.Ordinal));
		True(File.Exists(smokePath));
		string smokeText = File.ReadAllText(smokePath);
		True(smokeText.Contains("DebugInjectNetworkVisualPlaybackFailures", StringComparison.Ordinal));
		True(smokeText.Contains("DebugKillTrackedBoardTweens", StringComparison.Ordinal));
		True(smokeText.Contains("queue_free()", StringComparison.Ordinal));
		True(File.Exists(boundsSmokePath));
		string boundsSmokeText = File.ReadAllText(boundsSmokePath);
		True(boundsSmokeText.Contains("Accepted visual event without action ID", StringComparison.Ordinal));
		True(boundsSmokeText.Contains("Accepted visual event with negative action ID", StringComparison.Ordinal));
		True(boundsSmokeText.Contains("Accepted visual event with non-integer action ID", StringComparison.Ordinal));
		True(File.Exists(Path.Combine(
			repoRoot,
			"tests",
			"run_network_visual_failure_isolation_smoke.sh")));
	}

}
