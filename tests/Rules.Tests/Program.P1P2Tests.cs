using System;
using System.IO;

internal static partial class Program
{
	private static void AdventureVictoryRetriesProduceOneEffectiveCommit()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		string avatarId = AdventureCampaignCatalog.GetMainAvatarId(1);
		Equal(AdventureRunWriteResult.Succeeded, store.BeginNewMainRun(1, avatarId));
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Failed);
		saveManager.SaveSlotResultSequence.Enqueue(Godot.Error.Ok);

		Equal(AdventureVictoryCommitResult.SaveFailed, store.CommitMainAdventureVictory(avatarId));
		Equal(AdventureVictoryCommitResult.SaveFailed, store.CommitMainAdventureVictory(avatarId));
		Equal(AdventureVictoryCommitResult.Succeeded, store.CommitMainAdventureVictory(avatarId));
		Equal(1, LoadStoredProfile(saveManager, slotId).HighestCompletedMainAdventureStage);
		Equal(2, ReadStoredRunSnapshot(saveManager, slotId).MainStageIndex);
		int saveCountAfterSuccess = saveManager.SaveToSlotCallCount;
		Equal(AdventureVictoryCommitResult.AlreadyCommitted, store.CommitMainAdventureVictory(avatarId));
		Equal(saveCountAfterSuccess, saveManager.SaveToSlotCallCount);
	}

	private static void DirectItemTargetRulesCoverQueenAndKingCandidates()
	{
		False(DirectItemTargetRules.IsEligibleOpponent(0, 0, 4, 3));
		False(DirectItemTargetRules.IsEligibleOpponent(0, 4, 4, 3));
		False(DirectItemTargetRules.IsEligibleOpponent(0, 2, 4, 0));
		True(DirectItemTargetRules.IsEligibleOpponent(0, 2, 4, 1));

		Equal(DirectItemHandCardSource.BaseHand, DirectItemTargetRules.ResolveHandCardSource(2, 3, 0));
		Equal(DirectItemHandCardSource.BaseHand, DirectItemTargetRules.ResolveHandCardSource(2, 3, 1));
		Equal(DirectItemHandCardSource.ItemHand, DirectItemTargetRules.ResolveHandCardSource(2, 3, 2));
		Equal(DirectItemHandCardSource.ItemHand, DirectItemTargetRules.ResolveHandCardSource(2, 3, 4));
		Equal(DirectItemHandCardSource.Invalid, DirectItemTargetRules.ResolveHandCardSource(2, 3, 5));

		Equal(0, DirectItemTargetRules.GetDeckTransferCount(0, 3));
		Equal(1, DirectItemTargetRules.GetDeckTransferCount(1, 3));
		Equal(2, DirectItemTargetRules.GetDeckTransferCount(2, 3));
		Equal(3, DirectItemTargetRules.GetDeckTransferCount(3, 3));
		Equal(3, DirectItemTargetRules.GetDeckTransferCount(8, 3));
	}

	private static void QueenAndKingStayDisabledDirectTriggerResources()
	{
		string root = FindRepositoryRoot();
		foreach (string fileName in new[] { "queen_runtime.tres", "king_runtime.tres" })
		{
			string text = File.ReadAllText(Path.Combine(root, "resources", "item_cards", fileName));
			True(text.Contains("EnabledInGame = false", StringComparison.Ordinal));
			True(text.Contains("PlayableFromHand = false", StringComparison.Ordinal));
			True(text.Contains("PlayPresentation = \"direct_trigger\"", StringComparison.Ordinal));
			False(text.Contains("PlayPresentation = \"targeted_trigger\"", StringComparison.Ordinal));
		}
	}

	private static void PendingTargetCancellationCompletesAuthorityBeforeBroadcast()
	{
		string source = File.ReadAllText(Path.Combine(
			FindRepositoryRoot(),
			"scripts",
			"GameBoardScene.ItemCards.cs"));
		int methodStart = source.IndexOf("private void CancelPendingItemSelection()", StringComparison.Ordinal);
		int methodEnd = source.IndexOf("private void ClearPendingItemSelectionState()", methodStart, StringComparison.Ordinal);
		True(methodStart >= 0 && methodEnd > methodStart);
		string method = source[methodStart..methodEnd];
		int clearIndex = method.IndexOf("ClearPendingItemSelectionState();", StringComparison.Ordinal);
		int completeIndex = method.IndexOf("CompleteNetworkAuthorityAction();", StringComparison.Ordinal);
		int broadcastIndex = method.IndexOf("SendBoardSnapshotIfHost();", StringComparison.Ordinal);
		True(clearIndex >= 0 && completeIndex > clearIndex && broadcastIndex > completeIndex);
		True(method.Contains("ResetNetworkLocallyCommittedPendingSelectionState();", StringComparison.Ordinal));
		True(method.Contains("ResetNetworkPredictedItemVisualState();", StringComparison.Ordinal));
		True(method.Contains("ClearNetworkPendingActionState();", StringComparison.Ordinal));
	}

	private static void McpAutoloadsUseEditorGatedExportSafeProxies()
	{
		string root = FindRepositoryRoot();
		string project = File.ReadAllText(Path.Combine(root, "project.godot"));
		string plugin = File.ReadAllText(Path.Combine(root, "addons", "godot_mcp", "plugin.gd"));
		string exportPreset = File.ReadAllText(Path.Combine(root, "export_presets.cfg"));
		string[] proxyPaths =
		{
			"scripts/dev_mcp/mcp_screenshot_proxy.gd",
			"scripts/dev_mcp/mcp_input_service_proxy.gd",
			"scripts/dev_mcp/mcp_game_inspector_proxy.gd",
		};

		True(exportPreset.Contains("addons/godot_mcp/**", StringComparison.Ordinal));
		foreach (string proxyPath in proxyPaths)
		{
			True(project.Contains($"res://{proxyPath}", StringComparison.Ordinal));
			True(plugin.Contains($"res://{proxyPath}", StringComparison.Ordinal));
			string proxy = File.ReadAllText(Path.Combine(root, proxyPath));
			True(proxy.Contains("OS.has_feature(\"editor\")", StringComparison.Ordinal));
			True(proxy.Contains("load(SERVICE_PATH)", StringComparison.Ordinal));
			False(proxy.Contains("preload(\"res://addons/godot_mcp", StringComparison.Ordinal));
		}
	}

	private static void ExportPresetExcludesRawGeneratedArtPatterns()
	{
		string preset = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "export_presets.cfg"));
		foreach (string pattern in new[]
		{
			"art/**/raw/**",
			"art/**/generated/**",
			"art/**/raw-sheet.png",
			"art/**/raw-sheet.png.import",
			"art/**/raw-sheet-clean.png",
			"art/**/raw-sheet-clean.png.import",
			"art/**/sheet-transparent.png",
			"art/**/sheet-transparent.png.import",
		})
		{
			True(preset.Contains(pattern, StringComparison.Ordinal));
		}
	}
}
