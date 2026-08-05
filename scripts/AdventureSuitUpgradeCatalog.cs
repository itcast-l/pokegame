using Godot;
using System;
using System.Collections.Generic;

public sealed class AdventureSuitUpgradeCatalog
{
	private const string CategoryId = "adventure_suit_upgrades";

	private readonly List<AdventureSuitUpgradeDefinition> _all = new();
	private readonly Dictionary<string, AdventureSuitUpgradeDefinition> _byId = new(StringComparer.Ordinal);
	private AdventureUpgradeManifest? _cachedManifest;

	public IReadOnlyList<AdventureSuitUpgradeDefinition> All => _all;

	public static AdventureSuitUpgradeCatalog Load(Node sceneNode)
	{
		var catalog = new AdventureSuitUpgradeCatalog();
		catalog.LoadFromNode(sceneNode);
		catalog.PublishManifest(sceneNode);
		return catalog;
	}

	public void LoadFromNode(Node sceneNode)
	{
		_all.Clear();
		_byId.Clear();
		_cachedManifest = null;

		Node? pandora = sceneNode.GetNodeOrNull<Node>("/root/Pandora");
		if (pandora == null || !pandora.Call("is_loaded").AsBool())
		{
			GD.PushWarning("Adventure suit upgrades catalog unavailable because Pandora is not loaded.");
			return;
		}

		GodotObject? category = pandora.Call("get_category", CategoryId).AsGodotObject();
		if (category == null)
		{
			GD.PushWarning($"Missing Pandora category: {CategoryId}");
			return;
		}

		Godot.Collections.Array entities = pandora.Call("get_all_entities", category).AsGodotArray();
		var definitions = new List<AdventureSuitUpgradeDefinition>();
		var definitionIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (Variant entityVariant in entities)
		{
			GodotObject? entity = entityVariant.AsGodotObject();
			if (entity == null)
			{
				InvalidateCatalog("Adventure upgrade category contains a non-object entity.");
				return;
			}

			string id = entity.Call("get_entity_id").AsString();
			if (string.IsNullOrWhiteSpace(id) || !definitionIds.Add(id))
			{
				InvalidateCatalog($"Adventure upgrade category contains duplicate or empty ID '{id}'.");
				return;
			}

		string[] requiredProperties =
		{
				"suit", "rarity", "trigger_text", "effect_text", "description_key", "notes",
				"enabled", "sort_order", "effect_values", "description_value_keys",
			};
			foreach (string propertyName in requiredProperties)
			{
				if (!entity.Call("has_entity_property", propertyName).AsBool())
				{
					InvalidateCatalog($"Adventure upgrade '{id}' is missing property '{propertyName}'.");
					return;
				}
			}

			definitions.Add(new AdventureSuitUpgradeDefinition(
				id,
				entity.Call("get_entity_name").AsString(),
				GetEntityString(entity, "suit"),
				GetEntityString(entity, "rarity"),
				GetEntityString(entity, "trigger_text"),
				GetEntityString(entity, "effect_text"),
				GetEntityString(entity, "description_key", BuildDefaultDescriptionKey(id)),
				GetEntityString(entity, "notes"),
				GetEntityBool(entity, "enabled"),
				GetEntityInt(entity, "sort_order"),
				GetEntityString(entity, "effect_values"),
				GetEntityString(entity, "description_value_keys")));
		}

		definitions.Sort((a, b) =>
		{
			int orderComparison = a.SortOrder.CompareTo(b.SortOrder);
			return orderComparison != 0 ? orderComparison : string.CompareOrdinal(a.Id, b.Id);
		});

		foreach (AdventureSuitUpgradeDefinition definition in definitions)
		{
			_all.Add(definition);
			_byId[definition.Id] = definition;
		}
	}

	public bool TryGetDefinition(string id, out AdventureSuitUpgradeDefinition definition)
	{
		return _byId.TryGetValue(id, out definition);
	}

	public IReadOnlyList<AdventureSuitUpgradeDefinition> GetDefinitionsForSuit(string suit)
	{
		var results = new List<AdventureSuitUpgradeDefinition>();
		foreach (AdventureSuitUpgradeDefinition definition in _all)
		{
			if (string.Equals(definition.Suit, suit, StringComparison.Ordinal))
			{
				results.Add(definition);
			}
		}

		return results;
	}

	public Godot.Collections.Array GetAllDefinitions()
	{
		return ToDataArray(_all);
	}

	public Godot.Collections.Array GetDefinitionsForSuitData(string suit)
	{
		return ToDataArray(GetDefinitionsForSuit(suit));
	}

	public Godot.Collections.Dictionary GetDefinitionData(string id)
	{
		return TryGetDefinition(id, out AdventureSuitUpgradeDefinition definition)
			? definition.ToDictionary()
			: new Godot.Collections.Dictionary();
	}

	internal bool TryCreateManifest(out AdventureUpgradeManifest manifest)
	{
		if (_cachedManifest?.IsReady == true)
		{
			manifest = _cachedManifest;
			return true;
		}

		var entries = new List<AdventureUpgradeCatalogEntry>(_all.Count);
		foreach (AdventureSuitUpgradeDefinition definition in _all)
		{
			entries.Add(new AdventureUpgradeCatalogEntry(
				definition.Id,
				definition.Suit,
				definition.Rarity,
				definition.Enabled,
				definition.DescriptionKey,
				definition.EffectValuesRaw,
				definition.DescriptionValueKeysCsv));
		}

		if (!AdventureUpgradeManifest.TryCreateFromCatalog(
			entries,
			out manifest,
			out string errorMessage))
		{
			GD.PushWarning(errorMessage);
			return false;
		}

		_cachedManifest = manifest;
		return true;
	}

	private static string GetEntityString(GodotObject entity, string propertyName)
	{
		return GetEntityString(entity, propertyName, string.Empty);
	}

	private static string GetEntityString(GodotObject entity, string propertyName, string fallbackValue)
	{
		return entity.Call("has_entity_property", propertyName).AsBool()
			? entity.Call("get_string", propertyName).AsString()
			: fallbackValue;
	}

	private static bool GetEntityBool(GodotObject entity, string propertyName)
	{
		return entity.Call("has_entity_property", propertyName).AsBool()
			&& entity.Call("get_bool", propertyName).AsBool();
	}

	private static int GetEntityInt(GodotObject entity, string propertyName)
	{
		return entity.Call("has_entity_property", propertyName).AsBool()
			? entity.Call("get_integer", propertyName).AsInt32()
			: 0;
	}

	private static Godot.Collections.Array ToDataArray(IReadOnlyList<AdventureSuitUpgradeDefinition> definitions)
	{
		var array = new Godot.Collections.Array();
		foreach (AdventureSuitUpgradeDefinition definition in definitions)
		{
			array.Add(definition.ToDictionary());
		}

		return array;
	}

	private void PublishManifest(Node sceneNode)
	{
		if (!TryCreateManifest(out AdventureUpgradeManifest manifest))
		{
			return;
		}

		sceneNode
			.GetNodeOrNull<AdventureRunSessionStore>("/root/AdventureRunSessionStore")
			?.SetManifest(manifest);
	}

	private static string BuildDefaultDescriptionKey(string id)
	{
		return string.IsNullOrWhiteSpace(id)
			? string.Empty
			: $"UPGRADE_DESC_{id.Trim().ToUpperInvariant()}";
	}

	private void InvalidateCatalog(string message)
	{
		_all.Clear();
		_byId.Clear();
		_cachedManifest = null;
		GD.PushWarning($"Adventure suit upgrade catalog is invalid: {message}");
	}
}
