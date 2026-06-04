using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Godot;

internal static class BackgroundFrameLoader
{
	internal static async Task<Texture2D[]> LoadTextureFramesAsync(
		Node owner,
		string framePattern,
		int frameCount,
		string warningLabel)
	{
		var paths = new List<string>(Math.Max(0, frameCount));
		for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
		{
			string path = string.Format(CultureInfo.InvariantCulture, framePattern, frameIndex);
			if (ResourceLoader.Exists(path))
			{
				paths.Add(path);
				_ = ResourceLoader.LoadThreadedRequest(path, "Texture2D", useSubThreads: false, ResourceLoader.CacheMode.Reuse);
			}
			else
			{
				GD.PushWarning($"Missing {warningLabel} frame: {path}");
			}
		}

		var frames = new List<Texture2D>(paths.Count);
		foreach (string path in paths)
		{
			Texture2D? texture = await LoadRequestedTextureAsync(owner, path);
			if (texture == null)
			{
				GD.PushWarning($"Missing {warningLabel} frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames.ToArray();
	}

	private static async Task<Texture2D?> LoadRequestedTextureAsync(Node owner, string path)
	{
		var progress = new Godot.Collections.Array();
		while (GodotObject.IsInstanceValid(owner) && owner.IsInsideTree())
		{
			ResourceLoader.ThreadLoadStatus status = ResourceLoader.LoadThreadedGetStatus(path, progress);
			switch (status)
			{
				case ResourceLoader.ThreadLoadStatus.Loaded:
					return ResourceLoader.LoadThreadedGet(path) as Texture2D;
				case ResourceLoader.ThreadLoadStatus.Failed:
				case ResourceLoader.ThreadLoadStatus.InvalidResource:
					return ResourceLoader.Load<Texture2D>(path);
			}

			await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
		}

		return null;
	}
}
