using Godot;
using System;
using System.Collections.Generic;

public sealed class AdventureSuitUpgradeCatalog
{
	private const string CategoryId = "adventure_suit_upgrades";

	private readonly List<AdventureSuitUpgradeDefinition> _all = new();
	private readonly Dictionary<string, AdventureSuitUpgradeDefinition> _byId = new(StringComparer.Ordinal);

	public IReadOnlyList<AdventureSuitUpgradeDefinition> All => _all;

	public static AdventureSuitUpgradeCatalog Load(Node sceneNode)
	{
		var catalog = new AdventureSuitUpgradeCatalog();
		catalog.LoadFromNode(sceneNode);
		return catalog;
	}

	public void LoadFromNode(Node sceneNode)
	{
		_all.Clear();
		_byId.Clear();

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
		foreach (Variant entityVariant in entities)
		{
			GodotObject? entity = entityVariant.AsGodotObject();
			if (entity == null)
			{
				continue;
			}

			string id = entity.Call("get_entity_id").AsString();
			if (string.IsNullOrWhiteSpace(id))
			{
				continue;
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
					GetEntityInt(entity, "sort_order")));
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

	private static string BuildDefaultDescriptionKey(string id)
	{
		return string.IsNullOrWhiteSpace(id)
			? string.Empty
			: $"UPGRADE_DESC_{id.Trim().ToUpperInvariant()}";
	}
}
