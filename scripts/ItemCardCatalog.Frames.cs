using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

public sealed partial class ItemCardCatalog
{
	private static Texture2D[] LoadFrames(ItemCardDefinition definition)
	{
		var frames = new List<Texture2D>();
		for (int frameIndex = 1; frameIndex <= definition.FrameCount; frameIndex++)
		{
			string path = GetFramePath(definition, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing item card frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames.ToArray();
	}

	private static string GetFramePath(ItemCardDefinition definition, int frameIndex)
	{
		return string.IsNullOrWhiteSpace(definition.FramePathPattern)
			? $"{ItemCardRootPath}/{definition.Id}/{definition.ArtRunName}/processed/frames/frame-{frameIndex:000}.png"
			: string.Format(CultureInfo.InvariantCulture, definition.FramePathPattern, frameIndex);
	}
}
