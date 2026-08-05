using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;

internal static partial class Program
{
	private static void AdventureVictoryRetriesProduceOneEffectiveCommit()
	{
		ResetGameSaveStoreTestState();
		string slotId = GameSaveStore.GetProfileSlotId(1);
		Godot.TestSaveManagerNode saveManager = InstallSaveManagerForTests(slotId);
		SeedProfile(saveManager, slotId, CreateProgressData(playerName: "Runner"));
		var store = new AdventureRunSessionStore();
		store.SetManifest(CreateFullAdventureUpgradeManifest());
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
		int[] candidates = { 1, 2, 3 };
		Equal(1, DirectItemTargetRules.PickUniformCandidate(candidates, 0));
		Equal(2, DirectItemTargetRules.PickUniformCandidate(candidates, 1));
		Equal(3, DirectItemTargetRules.PickUniformCandidate(candidates, 2));
		Equal(-1, DirectItemTargetRules.PickUniformCandidate(candidates, -1));
		Equal(-1, DirectItemTargetRules.PickUniformCandidate(Array.Empty<int>(), 0));

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

	private static void QueenAndKingStayDisabledDirectTriggerPandoraEntries()
	{
		string root = FindRepositoryRoot();
		using JsonDocument document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "data.pandora")));
		var pandoraIds = new HashSet<string>(StringComparer.Ordinal);
		var checkedRoyalIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (JsonElement entity in document.RootElement
			.GetProperty("_entity_data")
			.GetProperty("_entities")
			.EnumerateArray())
		{
			if (entity.GetProperty("_category_id").GetString() != "item_cards")
			{
				continue;
			}

			string id = entity.GetProperty("_id").GetString() ?? string.Empty;
			True(pandoraIds.Add(id));
			if (id is not ("Q" or "K"))
			{
				continue;
			}

			JsonElement properties = entity.GetProperty("_property_overrides");
			False(properties.GetProperty("enabled_in_game").GetProperty("value").GetBoolean());
			False(properties.GetProperty("playable_from_hand").GetProperty("value").GetBoolean());
			False(properties.GetProperty("can_be_question_reward").GetProperty("value").GetBoolean());
			Equal("direct_trigger", properties.GetProperty("play_presentation").GetProperty("value").GetString());
			checkedRoyalIds.Add(id);
		}
		True(checkedRoyalIds.SetEquals(new[] { "Q", "K" }));

		string visualDirectory = Path.Combine(root, "resources", "item_cards");
		string[] visualFiles = Directory.GetFiles(visualDirectory, "*.tres");
		var visualIds = new HashSet<string>(StringComparer.Ordinal);
		string[] forbiddenFields =
		{
			"DisplayName", "DescriptionKey", "EnabledInGame", "PlayableFromHand",
			"CanBeQuestionReward", "RewardDelivery", "Rarity", "TagsCsv",
			"PlayPresentation", "Trigger", "EffectType", "EffectValuesRaw",
			"Priority", "AttachedTrigger", "AttachedEffectType",
			"AttachedEffectValuesRaw", "AttachedPriority", "Weight", "Description",
		};
		foreach (string visualFile in visualFiles)
		{
			string text = File.ReadAllText(visualFile);
			True(visualIds.Add(ReadTresString(text, "Id")));
			True(text.Contains("ItemCardVisualDefinitionResource", StringComparison.Ordinal));
			foreach (string forbiddenField in forbiddenFields)
			{
				False(text.Contains($"\n{forbiddenField} =", StringComparison.Ordinal));
			}
		}
		True(pandoraIds.SetEquals(visualIds));
		True(File.Exists(Path.Combine(visualDirectory, "jack.tres")));
		True(File.Exists(Path.Combine(visualDirectory, "queen.tres")));
		True(File.Exists(Path.Combine(visualDirectory, "king.tres")));
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
