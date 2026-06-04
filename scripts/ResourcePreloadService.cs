using System;
using System.Threading.Tasks;
using Godot;

internal static class ResourcePreloadService
{
	internal static async Task<bool> WaitForNextFrameAsync(
		Node owner,
		TurnContext turnContext,
		Func<TurnContext, bool> isCurrentContext,
		Func<bool> isCancelled)
	{
		await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
		return owner.IsInsideTree() && isCurrentContext(turnContext) && !isCancelled();
	}
}
