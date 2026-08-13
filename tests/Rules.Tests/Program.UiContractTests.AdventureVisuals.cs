using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void AvatarFeedbackEffectResourcesStayContiguous()
	{
		string repoRoot = FindRepositoryRoot();
		foreach ((string root, int minFrameCount) in new[]
		{
			("res://art/fx/avatar_feedback_score/run-001/processed", 4),
			("res://art/fx/avatar_feedback_hit/run-001/processed", 4),
			("res://art/fx/avatar_feedback_victory/run-001/processed", 4),
			("res://art/fx/avatar_feedback_defeat/run-001/processed", 4),
		})
		{
			int frameCount = CountContiguousFrames(repoRoot, root, "fx-{0}.png");
			True(frameCount >= minFrameCount);
		}
	}

	private static void AdventureUpgradePopupSceneHasNoEmbeddedImagePayloads()
	{
		string scenePath = Path.Combine(FindRepositoryRoot(), "scenes", "upgrade_choice_popup_asset_scene.tscn");
		string text = File.ReadAllText(scenePath);

		False(text.Contains("[sub_resource type=\"Image\""));
		False(text.Contains("[sub_resource type=\"ImageTexture\""));
		False(text.Contains("PackedByteArray("));
	}

	private static void AdventureUpgradeVisualCatalogExposesTwelveProcessedCards()
	{
		string repoRoot = FindRepositoryRoot();
		string catalogPath = Path.Combine(
			repoRoot,
			"resources",
			"adventure_upgrades",
			"visuals",
			"adventure_upgrade_card_visual_catalog.tres");
		True(File.Exists(catalogPath));
		string text = File.ReadAllText(catalogPath);

		Equal(12, Regex.Matches(text, @"(?m)^\[sub_resource type=""Resource"" id=""Entry_").Count);

		MatchCollection entryBlocks = Regex.Matches(
			text,
			@"(?ms)^\[sub_resource type=""Resource"" id=""Entry_[^""]+""\]\s+script = ExtResource\(""[^""]+""\)\s+Suit = ""([^""]+)""\s+Tier = ""([^""]+)""\s+TexturePath = ""([^""]+)""\s+Region = Rect2\(([^)]+)\)");
		Equal(12, entryBlocks.Count);

		var seenKeys = new HashSet<string>(StringComparer.Ordinal);
		var seenSuits = new HashSet<string>(StringComparer.Ordinal);
		var seenTiers = new HashSet<string>(StringComparer.Ordinal);

		for (int index = 0; index < entryBlocks.Count; index++)
		{
			string suit = entryBlocks[index].Groups[1].Value.Trim().ToLowerInvariant();
			string tier = entryBlocks[index].Groups[2].Value.Trim().ToLowerInvariant();
			string texturePath = entryBlocks[index].Groups[3].Value;
			True(seenKeys.Add($"{suit}::{tier}"));
			seenSuits.Add(suit);
			seenTiers.Add(tier);

			True(texturePath.StartsWith("res://art/ui/adventure_upgrade_cards/", StringComparison.Ordinal));
			True(texturePath.Contains("/processed/", StringComparison.Ordinal));

			string[] regionParts = entryBlocks[index].Groups[4].Value.Split(',', StringSplitOptions.TrimEntries);
			Equal(4, regionParts.Length);
			True(float.TryParse(regionParts[2], out float width));
			True(float.TryParse(regionParts[3], out float height));
			True(width > 0.0f);
			True(height > 0.0f);
			AssertResPathExists(repoRoot, texturePath);
		}

		Equal(4, seenSuits.Count);
		True(seenSuits.Contains("hearts"));
		True(seenSuits.Contains("spades"));
		True(seenSuits.Contains("diamonds"));
		True(seenSuits.Contains("clubs"));

		Equal(3, seenTiers.Count);
		True(seenTiers.Contains("common"));
		True(seenTiers.Contains("uncommon"));
		True(seenTiers.Contains("rare"));
	}

	private static void AdventureUpgradeVisualCatalogIncludesPaletteFieldsAndRarityTierMappings()
	{
		string repoRoot = FindRepositoryRoot();
		string catalogPath = Path.Combine(
			repoRoot,
			"resources",
			"adventure_upgrades",
			"visuals",
			"adventure_upgrade_card_visual_catalog.tres");
		True(File.Exists(catalogPath));
		string text = File.ReadAllText(catalogPath);

		Equal(12, Regex.Matches(text, @"(?m)^TitleColor = Color\(([^)]+)\)$").Count);
		Equal(12, Regex.Matches(text, @"(?m)^GlowColor = Color\(([^)]+)\)$").Count);
		Equal(12, Regex.Matches(text, @"(?m)^RuleColor = Color\(([^)]+)\)$").Count);

		Equal(3, Regex.Matches(text, @"(?m)^\[sub_resource type=""Resource"" id=""RarityTier_").Count);

		MatchCollection mappingBlocks = Regex.Matches(
			text,
			@"(?ms)^\[sub_resource type=""Resource"" id=""RarityTier_[^""]+""\]\s+script = ExtResource\(""[^""]+""\)\s+Rarity = ""([^""]+)""\s+Tier = ""([^""]+)""$");
		Equal(3, mappingBlocks.Count);

		var tierMappings = new Dictionary<string, string>(StringComparer.Ordinal);
		foreach (Match mappingBlock in mappingBlocks)
		{
			string rarity = mappingBlock.Groups[1].Value.Trim().ToLowerInvariant();
			string mappedTier = mappingBlock.Groups[2].Value.Trim().ToLowerInvariant();
			tierMappings[rarity] = mappedTier;
		}

		Equal("common", tierMappings["common"]);
		Equal("uncommon", tierMappings["rare"]);
		Equal("rare", tierMappings["legendary"]);
	}

	private static void AdventureUpgradeVisualResolverReturnsAtlasTextureFromCatalog()
	{
		string resolverPath = Path.Combine(FindRepositoryRoot(), "scripts", "AdventureUpgradeCardVisualResolver.cs");
		True(File.Exists(resolverPath));

		string text = File.ReadAllText(resolverPath);
		True(text.Contains("adventure_upgrade_card_visual_catalog.tres", StringComparison.Ordinal));
		True(text.Contains("new AtlasTexture", StringComparison.Ordinal));
		True(text.Contains("entry.Region", StringComparison.Ordinal));
		True(text.Contains("entry.TexturePath", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeVisualResolverUsesCatalogRarityTierMappings()
	{
		string resolverPath = Path.Combine(FindRepositoryRoot(), "scripts", "AdventureUpgradeCardVisualResolver.cs");
		True(File.Exists(resolverPath));

		string text = File.ReadAllText(resolverPath);
		True(text.Contains("ResolveTier(", StringComparison.Ordinal));
		True(text.Contains("LoadCatalog()", StringComparison.Ordinal));
		True(text.Contains("catalog.ResolveTier", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeRuntimeNoLongerHardcodesPopupArtSwitchTables()
	{
		string repoRoot = FindRepositoryRoot();
		string text = string.Concat(
			Directory.GetFiles(
				Path.Combine(repoRoot, "scripts"),
				"GameBoardScene.AdventureUpgrades*.cs")
			.OrderBy(path => path, StringComparer.Ordinal)
			.Select(File.ReadAllText));
		string overlayRuntimePath = Path.Combine(
			repoRoot,
			"scripts",
			"GameBoardScene.AdventureUpgradeSelectionOverlay.cs");
		string overlayText = File.ReadAllText(overlayRuntimePath);
		string choiceArtPath = Path.Combine(
			repoRoot,
			"scripts",
			"AdventureUpgradeChoiceArt.cs");
		string choiceArtText = File.ReadAllText(choiceArtPath);
		string obsoleteResolverPath = Path.Combine(repoRoot, "scripts", "AdventureUpgradeChoiceVisualResolver.cs");
		False(File.Exists(obsoleteResolverPath));
		False(text.Contains("GetAdventureUpgradeCardTextureRegion(", StringComparison.Ordinal));
		False(text.Contains("GetAdventureUpgradeCardTexturePath(", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/hearts/processed/hearts-common.png", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/spades/processed/spades-common.png", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/diamonds/processed/diamonds-common.png", StringComparison.Ordinal));
		False(text.Contains("res://art/ui/adventure_upgrade_cards/clubs/processed/clubs-common.png", StringComparison.Ordinal));
		False(text.Contains("MapAdventureUpgradeRarityToTextureTier(", StringComparison.Ordinal));
		False(text.Contains("ApplyAdventureUpgradeChoicePalette(", StringComparison.Ordinal));
		False(text.Contains("normalizedSuit == ", StringComparison.Ordinal));
		False(text.Contains("normalizedRarity == ", StringComparison.Ordinal));
		True(overlayText.Contains("AdventureUpgradeCardVisualResolver.LoadAtlasTextureForRarity", StringComparison.Ordinal));
		False(overlayText.Contains("AdventureUpgradeChoiceVisualResolver.LoadCardBaseTexture", StringComparison.Ordinal));
		False(overlayText.Contains("AdventureUpgradeChoiceVisualResolver.LoadSuitTexture", StringComparison.Ordinal));
		False(overlayText.Contains("SetSuitVisualTier", StringComparison.Ordinal));
		False(choiceArtText.Contains("adventure_upgrade_card_perspective", StringComparison.Ordinal));
		False(choiceArtText.Contains("ThicknessLayer", StringComparison.Ordinal));
		False(choiceArtText.Contains("SuitBadgeLayer", StringComparison.Ordinal));
		False(choiceArtText.Contains("adventure_upgrade_card_cleanup", StringComparison.Ordinal));
	}

	private static void AdventureUpgradePreviewUsesSharedCatalogCardInstances()
	{
		string repoRoot = FindRepositoryRoot();
		string previewScenePath = Path.Combine(repoRoot, "scenes", "upgrade_choice_popup_asset_scene.tscn");
		string sharedCardScenePath = Path.Combine(repoRoot, "scenes", "adventure_upgrade_catalog_card.tscn");
		string sharedCardScriptPath = Path.Combine(repoRoot, "scripts", "AdventureUpgradeCatalogCard.cs");
		string text = File.ReadAllText(previewScenePath);

		True(File.Exists(sharedCardScenePath));
		True(File.Exists(sharedCardScriptPath));
		Equal(12, Regex.Matches(text, @"instance=ExtResource\(""2_card""\)").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""hearts""").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""diamonds""").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""spades""").Count);
		Equal(3, Regex.Matches(text, @"PreviewSuit = ""clubs""").Count);
		False(text.Contains("AtlasTexture", StringComparison.Ordinal));
		False(text.Contains("UpgradeChoicePopupPreview.gd", StringComparison.Ordinal));
		False(text.Contains("hearts-common.png", StringComparison.Ordinal));
		False(text.Contains("spades-common.png", StringComparison.Ordinal));
		False(text.Contains("diamonds-common.png", StringComparison.Ordinal));
		False(text.Contains("clubs-common.png", StringComparison.Ordinal));
		string cardScript = File.ReadAllText(sharedCardScriptPath);
		True(cardScript.Contains("LoadAtlasTextureForRarity", StringComparison.Ordinal));
		False(cardScript.Contains("LoadCardBaseTexture", StringComparison.Ordinal));
		False(cardScript.Contains("LoadSuitTexture", StringComparison.Ordinal));
	}

	private static void AdventureUpgradeOverlaySampleArtUsesCatalogKeysInsteadOfHardcodedAtlasMetadata()
	{
		string overlayScenePath = Path.Combine(FindRepositoryRoot(), "scenes", "adventure_upgrade_selection_overlay.tscn");
		string text = File.ReadAllText(overlayScenePath);

		False(text.Contains("AtlasTexture_sample_", StringComparison.Ordinal));
		False(text.Contains("art/ui/adventure_upgrade_cards/", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"diamonds\"", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"hearts\"", StringComparison.Ordinal));
		True(text.Contains("PreviewSuit = \"spades\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"uncommon\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"common\"", StringComparison.Ordinal));
		True(text.Contains("PreviewTier = \"rare\"", StringComparison.Ordinal));
	}

	private static void ItemCardVisualResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		string itemCardDirectory = Path.Combine(repoRoot, "resources", "item_cards");
		True(Directory.Exists(itemCardDirectory));

		string[] itemCardPaths = Directory.GetFiles(itemCardDirectory, "*.tres");
		True(itemCardPaths.Length > 0);

		foreach (string itemCardPath in itemCardPaths)
		{
			string itemCardText = File.ReadAllText(itemCardPath);
			string itemCardId = ReadTresString(itemCardText, "Id");
			True(!string.IsNullOrWhiteSpace(itemCardId));

			int frameCount = Math.Max(1, ReadTresInt(itemCardText, "FrameCount", 1));
			string framePathPattern = ReadTresString(itemCardText, "FramePathPattern");
			if (string.IsNullOrWhiteSpace(framePathPattern))
			{
				string artRunName = ReadTresString(itemCardText, "ArtRunName");
				True(!string.IsNullOrWhiteSpace(artRunName));
				framePathPattern = $"res://art/cards/item_cards/{itemCardId}/{artRunName}/processed/frames/frame-{{0:000}}.png";
			}

			AssertFramePatternExists(repoRoot, framePathPattern, frameCount);

			string attachmentOverlayTexturePath = ReadTresString(itemCardText, "AttachmentOverlayTexturePath");
			if (!string.IsNullOrWhiteSpace(attachmentOverlayTexturePath))
			{
				AssertResPathExists(repoRoot, attachmentOverlayTexturePath);
			}
		}
	}

	private static void AttachmentSelectionPreviewFollowsPendingItemCard()
	{
		Equal("double", AttachmentSelectionPreviewRules.GetPreviewItemCardId("double"));
		Equal("magnet", AttachmentSelectionPreviewRules.GetPreviewItemCardId("magnet"));
		Equal("magnet", AttachmentSelectionPreviewRules.GetPreviewItemCardId(string.Empty));
	}

	private static void MainMenuBubblesRiseAndFadeThroughLifecycle()
	{
		MainMenuBubbleMotionState early = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 0.4f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState middle = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 2.0f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState late = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 3.8f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);
		MainMenuBubbleMotionState wrapped = MainMenuBubbleMotionRules.Evaluate(timeSeconds: 4.2f, phase: 0.0f, durationSeconds: 4.0f, riseDistance: 120.0f);

		True(middle.RiseOffsetY < early.RiseOffsetY - 35.0f);
		True(late.RiseOffsetY < middle.RiseOffsetY - 25.0f);
		True(late.AlphaMultiplier < middle.AlphaMultiplier);
		True(wrapped.RiseOffsetY > late.RiseOffsetY + 80.0f);
		True(wrapped.AlphaMultiplier < middle.AlphaMultiplier);
	}

	private static void MainMenuBubblesHideBeforeCrossingWaterline()
	{
		float visible = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			centerY: 430.0f,
			topOffset: 32.0f,
			waterlineY: 340.0f,
			fadeDistance: 48.0f);
		float hidden = MainMenuBubbleMotionRules.GetSurfaceAlphaMultiplier(
			centerY: 365.0f,
			topOffset: 32.0f,
			waterlineY: 340.0f,
			fadeDistance: 48.0f);

		True(visible > 0.9f);
		Equal(0.0f, hidden);
	}

	private static void MainMenuBubbleVisualResourcePointsToExistingFile()
	{
		AssertResPathExists(FindRepositoryRoot(), "res://art/fx/environment/main_menu_bubble/run-001/processed/fx-1.png");
	}

	private static void BoardEffectResourcesPointToExistingFiles()
	{
		string repoRoot = FindRepositoryRoot();
		foreach ((string pattern, int frameCount) in new[]
		{
			("res://art/cards/ocean_animated_deck/back/processed/frames/frame-{0:000}.png", 8),
			("res://art/cards/deck_shuffle/overhand_drop/game_frames/frame-{0:000}.png", 6),
			("res://art/fx/question_card_transform/run-001/processed/fx-{0}.png", 6),
			("res://art/fx/special_card_trigger/ocean_chrysanthemum_preview5/sea_anemone_rays/processed/fx-{0}.png", 4),
			("res://art/fx/bomb_pair_destroy/card_rip_burst/run-001/processed/fx-{0}.png", 4),
			("res://art/fx/lens_reveal_sprites/run-001/processed/fx-{0}.png", 4),
			("res://art/fx/adventure_trace_hunt_marker/run-004/processed/fx-{0}.png", 4),
		})
		{
			AssertFramePatternExists(repoRoot, pattern, frameCount);
		}

		foreach (string resPath in new[]
		{
			"res://art/fx/slime_card_overlay/run-001/processed/card-aspect.png",
			"res://art/ui/result_text_prompt/run-001/processed/victory-text-prompt-effect.png",
			"res://art/ui/result_text_prompt/run-001/processed/defeat-text-prompt-effect.png",
		})
		{
			AssertResPathExists(repoRoot, resPath);
		}
	}

	private static void DiceRollPresentationResourcesMapRollAndResultFrames()
	{
		string repoRoot = FindRepositoryRoot();

		Equal(16, DiceRollPresentationRules.RollingPathFrameCount);
		Equal("res://art/props/dice/neutral_tabletop/run-002/rolling_path/frame-001.png", DiceRollPresentationRules.GetRollingPathFramePath(1));
		Equal("res://art/props/dice/neutral_tabletop/run-002/rolling_path/frame-016.png", DiceRollPresentationRules.GetRollingPathFramePath(16));
		Equal(string.Empty, DiceRollPresentationRules.GetRollingPathFramePath(0));
		Equal(string.Empty, DiceRollPresentationRules.GetRollingPathFramePath(17));

		for (int result = DiceRollPresentationRules.MinResult; result <= DiceRollPresentationRules.MaxResult; result++)
		{
			True(DiceRollPresentationRules.TryGetResultMotionFramePath(result, out string resultPath));
			Equal($"res://art/props/dice/neutral_tabletop/run-002/result_motion_frames/result-{result}.png", resultPath);
			AssertResPathExists(repoRoot, resultPath);
		}

		False(DiceRollPresentationRules.TryGetResultMotionFramePath(0, out _));
		False(DiceRollPresentationRules.TryGetResultMotionFramePath(7, out _));
		AssertFramePatternExists(repoRoot, DiceRollPresentationRules.RollingPathFramePattern, DiceRollPresentationRules.RollingPathFrameCount);
	}


}
