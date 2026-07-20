using Godot;
using System;
using System.Collections.Generic;

public sealed partial class ItemCardCatalog
{
	private static bool TryLoadVisualDefinitions(
		out IReadOnlyDictionary<string, ItemCardVisualDefinition> visualsById,
		out string errorMessage)
	{
		var loadedVisuals = new Dictionary<string, ItemCardVisualDefinition>(StringComparer.Ordinal);
		using DirAccess? directory = DirAccess.Open(ItemCardResourceDirectory);
		if (directory == null)
		{
			visualsById = loadedVisuals;
			errorMessage = $"Missing item card visual resource directory: {ItemCardResourceDirectory}.";
			return false;
		}

		directory.ListDirBegin();
		while (true)
		{
			string fileName = directory.GetNext();
			if (string.IsNullOrEmpty(fileName))
			{
				break;
			}

			if (directory.CurrentIsDir() || !TryGetResourceFileName(fileName, out string resourceFileName))
			{
				continue;
			}

			string path = $"{ItemCardResourceDirectory}/{resourceFileName}";
			Resource? resource = GD.Load(path);
			if (resource is not ItemCardVisualDefinitionResource visualResource)
			{
				visualsById = loadedVisuals;
				errorMessage = $"Invalid item card visual resource: {path}.";
				return false;
			}

			ItemCardVisualDefinition visual = visualResource.ToDefinition();
			if (string.IsNullOrWhiteSpace(visual.Id)
				|| string.IsNullOrWhiteSpace(visual.SpriteName)
				|| string.IsNullOrWhiteSpace(visual.ArtRunName)
				|| visual.FrameCount <= 0)
			{
				visualsById = loadedVisuals;
				errorMessage = $"Item card visual resource has invalid required fields: {path}.";
				return false;
			}

			if (!loadedVisuals.TryAdd(visual.Id, visual))
			{
				visualsById = loadedVisuals;
				errorMessage = $"Duplicate item card visual ID '{visual.Id}'.";
				return false;
			}
		}
		directory.ListDirEnd();

		if (loadedVisuals.Count == 0)
		{
			visualsById = loadedVisuals;
			errorMessage = "The item card visual resource directory is empty.";
			return false;
		}

		visualsById = loadedVisuals;
		errorMessage = string.Empty;
		return true;
	}

	private static bool TryGetResourceFileName(string fileName, out string resourceFileName)
	{
		const string ResourceExtension = ".tres";
		const string RemapExtension = ".remap";

		if (fileName.EndsWith(ResourceExtension, StringComparison.OrdinalIgnoreCase))
		{
			resourceFileName = fileName;
			return true;
		}

		if (fileName.EndsWith(ResourceExtension + RemapExtension, StringComparison.OrdinalIgnoreCase))
		{
			resourceFileName = fileName[..^RemapExtension.Length];
			return true;
		}

		resourceFileName = string.Empty;
		return false;
	}
}
