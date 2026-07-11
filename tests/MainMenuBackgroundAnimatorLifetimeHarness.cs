using System;
using System.Threading.Tasks;
using Godot;

public partial class MainMenuBackgroundAnimatorLifetimeHarness
	: MainMenuBackgroundAnimator
{
	public bool DebugHasEnteredLoadBarrier { get; private set; }

	protected override async Task<Texture2D[]>
		LoadBackgroundFramesAsync()
	{
		SceneTree tree = GetTree();
		DebugHasEnteredLoadBarrier = true;
		await tree.ToSignal(
			tree,
			SceneTree.SignalName.ProcessFrame);
		return Array.Empty<Texture2D>();
	}
}
