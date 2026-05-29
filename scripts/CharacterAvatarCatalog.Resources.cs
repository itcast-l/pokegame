using System;
using System.Collections.Generic;
using Godot;

public static partial class CharacterAvatarCatalog
{
	private static CharacterAvatarDefinition[] LoadAvatarDefinitions()
	{
		var definitions = new List<CharacterAvatarDefinition>();
		using DirAccess? directory = DirAccess.Open(AvatarResourceDirectory);
		if (directory == null)
		{
			GD.PushWarning($"Missing character avatar resource directory: {AvatarResourceDirectory}");
			return Array.Empty<CharacterAvatarDefinition>();
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

			string path = $"{AvatarResourceDirectory}/{resourceFileName}";
			CharacterAvatarDefinition? definition = LoadAvatarDefinition(path);
			if (definition == null)
			{
				GD.PushWarning($"Invalid character avatar definition resource: {path}");
				continue;
			}

			if (string.IsNullOrWhiteSpace(definition.Id) || string.IsNullOrWhiteSpace(definition.RootPath))
			{
				GD.PushWarning($"Character avatar definition resource is missing id or root path: {path}");
				continue;
			}

			definitions.Add(definition);
		}

		if (definitions.Count == 0)
		{
			GD.PushWarning($"No character avatar definitions found in {AvatarResourceDirectory}");
			return Array.Empty<CharacterAvatarDefinition>();
		}

		definitions.Sort((a, b) =>
		{
			int stageComparison = a.AdventureStageNumber.CompareTo(b.AdventureStageNumber);
			return stageComparison != 0 ? stageComparison : string.CompareOrdinal(a.Id, b.Id);
		});
		return definitions.ToArray();
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
