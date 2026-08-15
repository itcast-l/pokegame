using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

internal static partial class Program
{
	private static void GameBoardExportedSettersShortCircuitRedundantWork()
	{
		string repoRoot = FindRepositoryRoot();
		string text =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Exports.cs"));

		True(text.Contains("if (normalizedPlayerCount == _playerCount)", StringComparison.Ordinal));
		True(text.Contains("if (value == _itemMode)", StringComparison.Ordinal));
		True(text.Contains("if (normalizedWinningScore == _winningScore)", StringComparison.Ordinal));
	}

	private static void DecorativeUiArtAvoidsUnconditionalPerFrameRedraws()
	{
		string repoRoot = FindRepositoryRoot();

		string oceanMenuText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "OceanMenuButtonFrame.cs"));
		string oceanMenuInteractionText = File.ReadAllText(
			Path.Combine(repoRoot, "scripts", "OceanMenuButtonFrame.Interaction.cs"));
		False(Regex.IsMatch(
			oceanMenuText,
			@"_glow = Mathf\.Lerp\(_glow, target, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));
		False(oceanMenuText.Contains("SceneTree.SignalName.NodeAdded", StringComparison.Ordinal));
		False(oceanMenuText.Contains("SceneTree.SignalName.NodeRemoved", StringComparison.Ordinal));
		False(oceanMenuInteractionText.Contains("RebuildInteractionBindings", StringComparison.Ordinal));
		True(oceanMenuInteractionText.Contains("ChildEnteredTree += OnObservedChildEnteredTree", StringComparison.Ordinal));
		True(oceanMenuInteractionText.Contains("ChildExitingTree += OnObservedChildExitingTree", StringComparison.Ordinal));

		string oceanMenuTextureText = File.ReadAllText(
			Path.Combine(repoRoot, "scripts", "OceanMenuButtonFrame.TexturedRendering.cs"));
		True(oceanMenuTextureText.Contains(
			"private readonly StyleBoxTexture _texturedFrameStyle = new()",
			StringComparison.Ordinal));
		True(oceanMenuTextureText.Contains(
			"_cachedTexturedFrameTexture != texture",
			StringComparison.Ordinal));
		True(oceanMenuTextureText.Contains(
			"_cachedTexturePatchMarginHorizontal != TexturePatchMarginHorizontal",
			StringComparison.Ordinal));
		True(oceanMenuTextureText.Contains(
			"_cachedTexturePatchMarginVertical != TexturePatchMarginVertical",
			StringComparison.Ordinal));
		False(Regex.IsMatch(
			oceanMenuTextureText,
			@"DrawTexturedState\(Texture2D texture, Rect2 rect\).*?StyleBoxTexture\s+\w+\s*=\s*new",
			RegexOptions.Singleline));

		string oceanDialogText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "OceanDialogPanelArt.cs"));
		True(oceanDialogText.Contains("SparkRedrawInterval", StringComparison.Ordinal));
		True(oceanDialogText.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
		False(Regex.IsMatch(
			oceanDialogText,
			@"public override void _Process\(double delta\)\s*\{\s*_time \+= delta;\s*QueueRedraw\(\);\s*\}",
			RegexOptions.Singleline));

		string playerCountFrameText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "PlayerCountDialogFrameArt.cs"));
		False(Regex.IsMatch(
			playerCountFrameText,
			@"_glow = Mathf\.Lerp\(_glow, targetGlow, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));

		string mainMenuPanelText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuPlayerCountDialogPanelArt.cs"));
		True(mainMenuPanelText.Contains("IdleSparkRedrawInterval", StringComparison.Ordinal));
		True(mainMenuPanelText.Contains("ActiveGlowRedrawInterval", StringComparison.Ordinal));
		True(mainMenuPanelText.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
		False(Regex.IsMatch(
			mainMenuPanelText,
			@"_startButtonGlow = Mathf\.Lerp\(_startButtonGlow, targetGlow, .*?\);\s*QueueRedraw\(\);",
			RegexOptions.Singleline));
	}

	private static void DynamicBackgroundLayersGateImageModeRedrawToFallbackNeeds()
	{
		string repoRoot = FindRepositoryRoot();

		string underwaterText =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.ProceduralDraw.cs"));
		True(underwaterText.Contains("ShouldQueueRedrawForImageFallback()", StringComparison.Ordinal));
		True(underwaterText.Contains("if (ShouldQueueRedrawForImageFallback())", StringComparison.Ordinal));

		string adventureMapText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "AdventureMapDynamicLayer.cs"));
		True(adventureMapText.Contains("ShouldQueueRedrawForImageFallback()", StringComparison.Ordinal));
		True(adventureMapText.Contains("if (ShouldQueueRedrawForImageFallback())", StringComparison.Ordinal));
	}

	private static void MainMenuOceanDynamicsKeepsProceduralFallbackPathsInImageMode()
	{
		string repoRoot = FindRepositoryRoot();
		string text =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.ProceduralDraw.cs"));

		True(text.Contains("ShouldQueueRedrawInImageMode()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralSunbeams()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralSurfaceWake()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralCurrentRibbons()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralCausticRibbons()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawProceduralGlints()", StringComparison.Ordinal));
		True(text.Contains("ShouldDrawBubbles()", StringComparison.Ordinal));
		False(text.Contains("if (_activeRenderMode == OceanRenderMode.Procedural)", StringComparison.Ordinal));
	}

	private static void DynamicEnvironmentLayersShareVisibilityAndThrottleContract()
	{
		string repoRoot = FindRepositoryRoot();
		string[] paths =
		{
			Path.Combine(repoRoot, "scripts", "MainMenuOceanDynamics.cs"),
			Path.Combine(repoRoot, "scripts", "AdventureMapDynamicLayer.cs"),
			Path.Combine(repoRoot, "scripts", "UnderwaterAmbientLayer.cs"),
		};

		foreach (string path in paths)
		{
			string text = File.ReadAllText(path);
			True(text.Contains("VisibilityChanged += HandleVisibilityChanged", StringComparison.Ordinal));
			True(text.Contains("SetProcess(isVisible)", StringComparison.Ordinal));
			True(text.Contains("if (!IsVisibleInTree())", StringComparison.Ordinal));
			True(text.Contains("NotificationResized", StringComparison.Ordinal));
			True(text.Contains("image_update_fps", StringComparison.Ordinal));
			True(text.Contains("redraw_fps", StringComparison.Ordinal));
		}
	}

	private static void GameBoardDefersModeSpecificResourceLoadsUntilNeeded()
	{
		string repoRoot = FindRepositoryRoot();
		string gameBoardText =
			File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.cs"))
			+ File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Exports.cs"));
		string assetsText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Assets.cs"));

		True(gameBoardText.Contains("EnsureAdventureModeResourcesLoaded();", StringComparison.Ordinal));
		True(gameBoardText.Contains("EnsureItemModeResourcesLoaded();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadMantaRayTideShiftCycloneFxTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadLeafySeadragonBladeProjectileTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadBossKRoyalSeizureLanceTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadAdventureDamageEffectTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadItemCardTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadAttachmentCardOverlayTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadQuestionCardTransformTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadBombPairDestroyEffectTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadLensRevealWispTextures();", StringComparison.Ordinal));
		False(gameBoardText.Contains("LoadMagnetCountdownFxTextures();", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureAdventureModeResourcesLoaded()", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureItemModeResourcesLoaded()", StringComparison.Ordinal));
	}

	private static void GameBoardOptionalFxResourcesAreNoLongerStaticallyPreloaded()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "GameBoardScene.cs"));

		False(text.Contains("private static readonly Texture2D[] ScoreFillEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] EnemySpecialAbilityAvatarFxFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MantaRayTideShiftCycloneFxFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] DefaultAdventureDamageEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] CatAdventureDamageEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LeafySeadragonBladeFlightFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LeafySeadragonBladeImpactFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] BossKRoyalSeizureLanceFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] QuestionCardTransformFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] SpecialCardTriggerEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] SpecialCardTriggerActivationFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] BombPairDestroyEffectFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] LensRevealWispFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MagnetCountdownFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] TableShuffleVortexFrames", StringComparison.Ordinal));
		False(text.Contains("private static readonly Texture2D[] MagnetAttachmentApplyRingFrames", StringComparison.Ordinal));
	}

	private static void GameBoardWarmupIncludesOptionalFxResources()
	{
		string repoRoot = FindRepositoryRoot();
		string warmupText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.ResourceWarmup.cs"));
		string assetsText = File.ReadAllText(Path.Combine(repoRoot, "scripts", "GameBoardScene.Assets.cs"));

		True(warmupText.Contains("WarmupOptionalFxResourcesAsync", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:CoreGameplay", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:AdventureMode", StringComparison.Ordinal));
		True(warmupText.Contains("WarmupOptionalFx:ItemMode", StringComparison.Ordinal));
		True(assetsText.Contains("private void EnsureCommonGameplayFxResourcesLoaded()", StringComparison.Ordinal));
	}

	private static void GameBoardIdlePerformanceTraceSmokeExists()
	{
		string repoRoot = FindRepositoryRoot();
		string smokeScriptPath = Path.Combine(repoRoot, "tests", "GameBoardIdlePerformanceTraceSmoke.gd");
		string runnerPath = Path.Combine(repoRoot, "tests", "run_game_board_idle_performance_trace_smoke.sh");

		True(File.Exists(smokeScriptPath));
		True(File.Exists(runnerPath));

		string smokeText = File.ReadAllText(smokeScriptPath);
		string runnerText = File.ReadAllText(runnerPath);

		True(smokeText.Contains("IDLE_WINDOW_BEGIN", StringComparison.Ordinal));
		True(smokeText.Contains("IDLE_WINDOW_END", StringComparison.Ordinal));
		True(smokeText.Contains("debug_performance_trace", StringComparison.Ordinal));
		True(runnerText.Contains("RenderPlayerHands", StringComparison.Ordinal));
		True(runnerText.Contains("RenderTableCards", StringComparison.Ordinal));
		True(runnerText.Contains("IDLE_WINDOW_BEGIN", StringComparison.Ordinal));
		True(runnerText.Contains("IDLE_WINDOW_END", StringComparison.Ordinal));
	}

	private static void GameBoardFrameSchedulerReleasesInactiveControllers()
	{
		using var scheduler = new GameBoardFrameScheduler();
		int persistentTicks = 0;
		int oneShotTicks = 0;
		using IDisposable persistent = scheduler.Register(_ => persistentTicks++);
		IDisposable? oneShot = null;
		oneShot = scheduler.Register(_ =>
		{
			oneShotTicks++;
			oneShot!.Dispose();
		});

		True(scheduler.HasActiveRegistrations);
		scheduler.Tick(1.0 / 60.0);
		Equal(1, persistentTicks);
		Equal(1, oneShotTicks);
		scheduler.Tick(1.0 / 60.0);
		Equal(2, persistentTicks);
		Equal(1, oneShotTicks);

		persistent.Dispose();
		False(scheduler.HasActiveRegistrations);
	}

	private static void GameBoardLifecycleCoordinatorOrdersStartupAndShutdown()
	{
		var coordinator = new GameBoardLifecycleCoordinator();
		var calls = new List<string>();
		coordinator.Start(
			() => calls.Add("create_rules"),
			() => calls.Add("bind_nodes"),
			() => calls.Add("create_controllers"),
			() => calls.Add("register_endpoint"),
			() => calls.Add("start_match"));
		coordinator.Stop(
			() => calls.Add("stop_input_and_network"),
			() => calls.Add("unregister_endpoint"),
			() => calls.Add("cancel_async"),
			() => calls.Add("release_controllers"),
			() => calls.Add("clear_presentation"));
		coordinator.Stop(
			() => calls.Add("unexpected"),
			() => calls.Add("unexpected"),
			() => calls.Add("unexpected"),
			() => calls.Add("unexpected"),
			() => calls.Add("unexpected"));

		Equal(
			string.Join(',', new[]
			{
				"create_rules",
				"bind_nodes",
				"create_controllers",
				"register_endpoint",
				"start_match",
				"stop_input_and_network",
				"unregister_endpoint",
				"cancel_async",
				"release_controllers",
				"clear_presentation",
			}),
			string.Join(',', calls));

		string root = FindRepositoryRoot();
		string boardSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.cs"));
		True(boardSource.Contains("_lifecycleCoordinator.Start(", StringComparison.Ordinal));
		True(boardSource.Contains("_lifecycleCoordinator.Stop(", StringComparison.Ordinal));
	}

	private static void GameBoardHotPathsUseCachedNodeBindings()
	{
		string root = FindRepositoryRoot();
		string boardSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.cs"));
		string countdownSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.MagnetAttachmentCountdown.cs"));
		string upgradeSource = File.ReadAllText(Path.Combine(
			root,
			"scripts",
			"GameBoardScene.AdventureUpgradeSelectionOverlay.cs"));
		string bindingsSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardNodeBindings.cs"));
		string layoutSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.Layout.cs"));
		string renderSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.Render.cs"));
		string avatarSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.Avatars.cs"));
		string resultSource = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.Results.cs"));
		string architectureRunner = File.ReadAllText(Path.Combine(
			root,
			"tests",
			"run_runtime_architecture_smoke_case.sh"));

		True(boardSource.Contains("_frameScheduler.Tick(delta);", StringComparison.Ordinal));
		True(boardSource.Contains("_frameScheduler.HasActiveRegistrations", StringComparison.Ordinal));
		False(boardSource.Contains("UpdateMandatoryActionCountdown(delta);", StringComparison.Ordinal));
		False(boardSource.Contains("UpdateAdventureUpgradeOverlayCardMotion(delta);", StringComparison.Ordinal));
		True(countdownSource.Contains(
			"_frameScheduler.Register(UpdateMandatoryActionCountdown)",
			StringComparison.Ordinal));
		True(countdownSource.Contains(
			"_mandatoryActionCountdownFrameRegistration?.Dispose();",
			StringComparison.Ordinal));
		True(upgradeSource.Contains(
			"_frameScheduler.Register(UpdateAdventureUpgradeOverlayCardMotion)",
			StringComparison.Ordinal));
		True(upgradeSource.Contains(
			"_adventureUpgradeOverlayFrameRegistration?.Dispose();",
			StringComparison.Ordinal));

		foreach (string requiredBinding in new[]
		{
			"PlayerAreas",
			"AvatarAreas",
			"ScoreMeters",
			"ScoreEffectTargets",
			"HandAreas",
			"CardPileAreas",
			"QuestionCardTransformArea",
		})
		{
			True(bindingsSource.Contains(requiredBinding, StringComparison.Ordinal));
		}

		foreach (string hotPathSource in new[] { layoutSource, renderSource })
		{
			False(hotPathSource.Contains("GetNode", StringComparison.Ordinal));
			False(hotPathSource.Contains("FindChild", StringComparison.Ordinal));
		}
		True(renderSource.Contains(
			"PlayerCardPileVisualBinding",
			StringComparison.Ordinal));
		True(boardSource.Contains("LoadCachedCoreScene", StringComparison.Ordinal));
		True(avatarSource.Contains("ref _cachedCharacterAvatarScene", StringComparison.Ordinal));
		True(resultSource.Contains("ref _cachedResultBannerScene", StringComparison.Ordinal));
		True(resultSource.Contains("ref _cachedDefeatFinisherOverlayScene", StringComparison.Ordinal));
		True(architectureRunner.Contains("^ERROR:", StringComparison.Ordinal));
	}

	private static void ResourceExportAuditScriptChecksTrackedAssetsAndExportConfig()
	{
		string scriptPath = Path.Combine(FindRepositoryRoot(), "tools", "audit_export_resources.sh");
		True(File.Exists(scriptPath));
		string text = File.ReadAllText(scriptPath);

		True(text.Contains("git ls-files", StringComparison.Ordinal));
		True(text.Contains("generated-images", StringComparison.Ordinal));
		True(text.Contains("outputs", StringComparison.Ordinal));
		True(text.Contains("debug-sse.log", StringComparison.Ordinal));
		True(text.Contains("project.godot", StringComparison.Ordinal));
		True(text.Contains("export_presets.cfg", StringComparison.Ordinal));
		True(text.Contains("autoload", StringComparison.Ordinal));
		True(text.Contains("exclude_filter", StringComparison.Ordinal));
		True(text.Contains("raw", StringComparison.Ordinal));
		True(text.Contains("transparent-processing-meta.json", StringComparison.Ordinal));
		True(text.Contains("upgrade-popup-assets", StringComparison.Ordinal));
		True(text.Contains("source", StringComparison.Ordinal));
		True(text.Contains("Category Summary", StringComparison.Ordinal));
		True(text.Contains("upgrade_popup_processed", StringComparison.Ordinal));
		True(text.Contains("board_top_preview", StringComparison.Ordinal));
		True(text.Contains("Runtime References Into Export-Excluded Paths", StringComparison.Ordinal));
		True(text.Contains("Missing Tracked Manifest Image References", StringComparison.Ordinal));
		True(text.Contains("collect_runtime_export_excluded_resource_refs", StringComparison.Ordinal));
		True(text.Contains("collect_missing_tracked_manifest_image_refs", StringComparison.Ordinal));
		False(text.Contains("git rm", StringComparison.Ordinal));
		False(text.Contains("rm -rf", StringComparison.Ordinal));
	}

	private static void ResourceExportAuditDocDescribesReadOnlyCommand()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "RESOURCE_EXPORT_AUDIT.md");
		True(File.Exists(docPath));
		string text = File.ReadAllText(docPath);

		True(text.Contains("bash tools/audit_export_resources.sh", StringComparison.Ordinal));
		True(text.Contains("只报告", StringComparison.Ordinal));
		True(text.Contains("不自动删除", StringComparison.Ordinal));
		True(text.Contains("processed", StringComparison.Ordinal));
		True(text.Contains("raw", StringComparison.Ordinal));
	}

	private static void ExportPresetExcludesUpgradePopupSourceOnlyAssets()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "export_presets.cfg"));

		True(text.Contains("generated-images/**", StringComparison.Ordinal));
	}

	private static void GitignoreCoversUpgradePopupSourceOnlyAssets()
	{
		string text = File.ReadAllText(Path.Combine(FindRepositoryRoot(), ".gitignore"));

		True(text.Contains("/generated-images/upgrade-popup-assets/*/raw/", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/transparent-processing-meta.json", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/*.png", StringComparison.Ordinal));
		True(text.Contains("/generated-images/upgrade-popup-assets/*.png.import", StringComparison.Ordinal));
	}

	private static void TrackedGeneratedAssetInventoryDocSummarizesCurrentCategories()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "TRACKED_GENERATED_ASSET_INVENTORY.md");
		True(File.Exists(docPath));
		string text = File.ReadAllText(docPath);

		True(text.Contains("upgrade_popup_processed", StringComparison.Ordinal));
		True(text.Contains("upgrade_popup_raw", StringComparison.Ordinal));
		True(text.Contains("board_top_preview", StringComparison.Ordinal));
		True(text.Contains("generated_misc", StringComparison.Ordinal));
		True(text.Contains("debug_log", StringComparison.Ordinal));
		True(text.Contains("保留跟踪", StringComparison.Ordinal));
		True(text.Contains("候选取消跟踪", StringComparison.Ordinal));
	}

	private static void TrackedGeneratedAssetInventoryDocIncludesSuggestedManualCleanupBatches()
	{
		string docPath = Path.Combine(FindRepositoryRoot(), "docs", "TRACKED_GENERATED_ASSET_INVENTORY.md");
		string text = File.ReadAllText(docPath);

		True(text.Contains("Suggested Manual Cleanup Batches", StringComparison.Ordinal));
		True(text.Contains("Batch 1", StringComparison.Ordinal));
		True(text.Contains("board-top-extension-preview", StringComparison.Ordinal));
		True(text.Contains("git rm --cached --", StringComparison.Ordinal));
		True(text.Contains("review first", StringComparison.OrdinalIgnoreCase));
	}

	private static void GameBoardPauseMenuHasExtractedControllerBoundary()
	{
		string repoRoot = FindRepositoryRoot();
		string controllerPath = Path.Combine(repoRoot, "scripts", "GameBoardPauseMenuController.cs");
		string pauseMenuPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.PauseMenu.cs");
		string mainPath = Path.Combine(repoRoot, "scripts", "GameBoardScene.cs");

		True(File.Exists(controllerPath));
		string controllerText = File.ReadAllText(controllerPath);
		string pauseMenuText = File.ReadAllText(pauseMenuPath);
		string mainText = File.ReadAllText(mainPath);

		True(controllerText.Contains("internal sealed class GameBoardPauseMenuController", StringComparison.Ordinal));
		True(controllerText.Contains("internal void Show()", StringComparison.Ordinal));
		True(controllerText.Contains("internal void Hide()", StringComparison.Ordinal));
		True(controllerText.Contains("internal void RefreshLocalizedText()", StringComparison.Ordinal));
		True(pauseMenuText.Contains("GameBoardPauseMenuController", StringComparison.Ordinal));
		False(pauseMenuText.Contains("private CanvasLayer? _pauseMenuLayer", StringComparison.Ordinal));
		False(pauseMenuText.Contains("private PauseMenuOverlay? _pauseMenuRoot", StringComparison.Ordinal));
		False(pauseMenuText.Contains("private Button? _pauseContinueButton", StringComparison.Ordinal));
		False(mainText.Contains("PauseMenuScenePath", StringComparison.Ordinal));
	}

	private static void NetworkManagerSerializationHelpersMoveIntoPartialFile()
	{
		string repoRoot = FindRepositoryRoot();
		string mainPath = Path.Combine(repoRoot, "scripts", "NetworkManager.cs");
		string partialPath = Path.Combine(repoRoot, "scripts", "NetworkManager.Serialization.cs");
		string mainText = File.ReadAllText(mainPath);

		True(File.Exists(partialPath));
		string partialText = File.ReadAllText(partialPath);

		True(partialText.Contains("public partial class NetworkManager", StringComparison.Ordinal));
		True(partialText.Contains("private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal));
		True(partialText.Contains("private bool TryParseSharedConfig(", StringComparison.Ordinal));
		True(partialText.Contains("private static bool TryParsePlayer(", StringComparison.Ordinal));
		False(mainText.Contains("private static Godot.Collections.Dictionary ToDictionary(NetworkSessionConfig config)", StringComparison.Ordinal));
		False(mainText.Contains("private static bool TryParsePlayer(", StringComparison.Ordinal));
		False(mainText.Contains("private static TEnum ReadEnum<TEnum>", StringComparison.Ordinal));
	}


}
