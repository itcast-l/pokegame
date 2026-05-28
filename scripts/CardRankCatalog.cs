using System;
using System.Collections.Generic;
using Godot;

public readonly record struct CardRankDefinition(
	string Rank,
	int ScoreValue,
	int SuitedScoreValue,
	bool ExcludeFromStandardDeck,
	bool CollectsTableWhenItemModeDisabled);

public static class CardRankCatalog
{
	private const string CardRankResourceDirectory = "res://resources/card_ranks";

	private static IReadOnlyDictionary<string, CardRankDefinition>? _definitionsByRank;

	public static int GetScoreValue(string rank, bool hasSuit)
	{
		if (string.IsNullOrWhiteSpace(rank))
		{
			return 0;
		}

		if (GetDefinitions().TryGetValue(rank, out CardRankDefinition definition))
		{
			return hasSuit && definition.SuitedScoreValue >= 0
				? definition.SuitedScoreValue
				: definition.ScoreValue;
		}

		return int.TryParse(rank, out int numericRank) ? Math.Max(0, numericRank) : 0;
	}

	public static bool ShouldExcludeFromStandardDeck(string rank)
	{
		if (IsExcludedRank(rank))
		{
			return true;
		}

		int suffixStartIndex = rank.LastIndexOf('_') + 1;
		return suffixStartIndex > 0
			&& suffixStartIndex < rank.Length
			&& IsExcludedRank(rank[suffixStartIndex..]);
	}

	public static bool ShouldCollectTableWhenItemModeDisabled(string rank)
	{
		return GetDefinitions().TryGetValue(rank, out CardRankDefinition definition)
			&& definition.CollectsTableWhenItemModeDisabled;
	}

	private static bool IsExcludedRank(string rank)
	{
		return GetDefinitions().TryGetValue(rank, out CardRankDefinition definition)
			&& definition.ExcludeFromStandardDeck;
	}

	private static IReadOnlyDictionary<string, CardRankDefinition> GetDefinitions()
	{
		if (_definitionsByRank != null)
		{
			return _definitionsByRank;
		}

		var definitionsByRank = new Dictionary<string, CardRankDefinition>(StringComparer.Ordinal);
		using DirAccess? directory = DirAccess.Open(CardRankResourceDirectory);
		if (directory == null)
		{
			GD.PushWarning($"Missing card rank resource directory: {CardRankResourceDirectory}");
			_definitionsByRank = definitionsByRank;
			return _definitionsByRank;
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

			string path = $"{CardRankResourceDirectory}/{resourceFileName}";
			CardRankDefinition? definition = LoadCardRankDefinition(path);
			if (definition == null)
			{
				GD.PushWarning($"Invalid card rank definition resource: {path}");
				continue;
			}

			CardRankDefinition value = definition.Value;
			if (string.IsNullOrWhiteSpace(value.Rank))
			{
				GD.PushWarning($"Card rank definition resource is missing a rank: {path}");
				continue;
			}

			definitionsByRank[value.Rank] = value;
		}

		_definitionsByRank = definitionsByRank;
		return _definitionsByRank;
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

	private static CardRankDefinition? LoadCardRankDefinition(string path)
	{
		Resource? resource = GD.Load(path);
		if (resource is CardRankDefinitionResource cardRankResource)
		{
			return cardRankResource.ToDefinition();
		}

		if (resource == null)
		{
			return null;
		}

		return new CardRankDefinition(
			GetResourceString(resource, "Rank"),
			GetResourceInt(resource, "ScoreValue"),
			GetResourceInt(resource, "SuitedScoreValue", -1),
			GetResourceBool(resource, "ExcludeFromStandardDeck"),
			GetResourceBool(resource, "CollectsTableWhenItemModeDisabled"));
	}

	private static string GetResourceString(Resource resource, string propertyName, string fallback = "")
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsString();
	}

	private static int GetResourceInt(Resource resource, string propertyName, int fallback = 0)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsInt32();
	}

	private static bool GetResourceBool(Resource resource, string propertyName, bool fallback = false)
	{
		Variant value = resource.Get(propertyName);
		return value.VariantType == Variant.Type.Nil ? fallback : value.AsBool();
	}
}
