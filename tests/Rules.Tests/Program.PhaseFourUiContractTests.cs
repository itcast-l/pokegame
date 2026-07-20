using System;
using System.IO;

internal static partial class Program
{
	private static void PhaseFourFixedUiScenesHaveExtractedControllerBoundaries()
	{
		string root = FindRepositoryRoot();
		string resumeScene = File.ReadAllText(Path.Combine(root, "scenes", "adventure_run_resume_dialog.tscn"));
		string resumeController = File.ReadAllText(Path.Combine(root, "scripts", "AdventureRunResumeDialog.cs"));
		string resumeMenu = File.ReadAllText(Path.Combine(root, "scripts", "AdventureAvatarMenuScene.RunResumeDialog.cs"));
		string tutorialScene = File.ReadAllText(Path.Combine(root, "scenes", "adventure_tutorial_overlay.tscn"));
		string tutorialController = File.ReadAllText(Path.Combine(root, "scripts", "AdventureTutorialOverlay.cs"));
		string tutorialBoard = File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.AdventureTutorial.cs"))
			+ File.ReadAllText(Path.Combine(root, "scripts", "GameBoardScene.AdventureTutorial.Overlay.cs"));

		True(resumeScene.Contains("res://scripts/AdventureRunResumeDialog.cs", StringComparison.Ordinal));
		True(resumeScene.Contains("node name=\"AdventureRunResumeDialog\"", StringComparison.Ordinal));
		True(resumeScene.Contains("parent=\"DialogMotion/Panel/ContentMargin/Layout/ButtonRow\"", StringComparison.Ordinal));
		True(resumeController.Contains("public override void _ExitTree()", StringComparison.Ordinal));
		True(resumeController.Contains("public void ShowDialog(", StringComparison.Ordinal));
		True(resumeMenu.Contains("res://scenes/adventure_run_resume_dialog.tscn", StringComparison.Ordinal));
		False(resumeMenu.Contains("new ColorRect", StringComparison.Ordinal));
		False(resumeMenu.Contains("new Label", StringComparison.Ordinal));
		False(resumeMenu.Contains("new Button", StringComparison.Ordinal));

		True(tutorialScene.Contains("res://scripts/AdventureTutorialOverlay.cs", StringComparison.Ordinal));
		foreach (string nodeName in new[]
		{
			"AdventureTutorialInputBlocker",
			"AdventureTutorialPointer",
			"AdventureTutorialTargetOutline",
			"AdventureTutorialCallout",
			"AdventureTutorialText",
			"AdventureTutorialContinueButton",
		})
		{
			True(tutorialScene.Contains($"node name=\"{nodeName}\"", StringComparison.Ordinal));
		}

		True(tutorialController.Contains("public void SetTargetRect(", StringComparison.Ordinal));
		True(tutorialController.Contains("public void SetContent(", StringComparison.Ordinal));
		True(tutorialController.Contains("public void ShowOverlay()", StringComparison.Ordinal));
		True(tutorialController.Contains("public void HideOverlay()", StringComparison.Ordinal));
		True(tutorialController.Contains("public void Pulse()", StringComparison.Ordinal));
		True(tutorialController.Contains("public override void _ExitTree()", StringComparison.Ordinal));
		True(tutorialController.Contains("_pulseTween?.Kill();", StringComparison.Ordinal));
		True(tutorialBoard.Contains("res://scenes/adventure_tutorial_overlay.tscn", StringComparison.Ordinal));
		True(tutorialBoard.Contains("overlay.SetTweenFactory(CreateBoardTween);", StringComparison.Ordinal));
		False(tutorialBoard.Contains("new ColorRect", StringComparison.Ordinal));
		False(tutorialBoard.Contains("new Line2D", StringComparison.Ordinal));
		False(tutorialBoard.Contains("new Label", StringComparison.Ordinal));
		False(tutorialBoard.Contains("new Button", StringComparison.Ordinal));
		True(File.Exists(Path.Combine(root, "tests", "run_phase_four_ui_smoke_suite.sh")));
		True(File.Exists(Path.Combine(root, "tests", "run_phase_four_ui_verbose_leak_suite.sh")));
	}
}
