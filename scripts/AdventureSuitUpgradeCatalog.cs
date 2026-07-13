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
			AdventureUpgradeRuntimeValues.SetManifest(null);
			GD.PushWarning("Adventure suit upgrades catalog unavailable because Pandora is not loaded.");
			return;
		}

		GodotObject? category = pandora.Call("get_category", CategoryId).AsGodotObject();
		if (category == null)
		{
			AdventureUpgradeRuntimeValues.SetManifest(null);
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

		var entries = new List<AdventureUpgradeManifestEntry>();
		var catalogIds = new HashSet<string>(StringComparer.Ordinal);
		foreach (AdventureSuitUpgradeDefinition definition in _all)
		{
			if (!definition.Enabled)
			{
				continue;
			}

			if (!AdventureUpgradeRuntimeContract.TryGetMetadata(
				definition.Id,
				out AdventureUpgradeRuntimeMetadata runtimeMetadata))
			{
				GD.PushWarning(
					$"Enabled adventure suit upgrade '{definition.Id}' has no runtime manifest contract.");
				manifest = new AdventureUpgradeManifest(Array.Empty<AdventureUpgradeManifestEntry>());
				AdventureUpgradeRuntimeValues.SetManifest(null);
				return false;
			}

			string effectValueError = string.Empty;
			string descriptionKeyError = string.Empty;
			bool effectValuesValid = AdventureUpgradeRuntimeContract.TryValidateEffectValues(
					definition.Id,
					definition.EffectValuesRaw,
					out IReadOnlyDictionary<string, int> effectValues,
					out effectValueError);
			bool descriptionKeysValid = AdventureUpgradeRuntimeContract.TryValidateDescriptionValueKeys(
					definition.Id,
					definition.DescriptionValueKeysCsv,
					out IReadOnlyList<string> descriptionValueKeys,
					out descriptionKeyError);
			if (!effectValuesValid || !descriptionKeysValid)
			{
				GD.PushWarning(
					$"Enabled adventure suit upgrade '{definition.Id}' has invalid parameter contract: {effectValueError}{descriptionKeyError}");
				manifest = new AdventureUpgradeManifest(Array.Empty<AdventureUpgradeManifestEntry>());
				AdventureUpgradeRuntimeValues.SetManifest(null);
				return false;
			}

			catalogIds.Add(definition.Id);

			entries.Add(new AdventureUpgradeManifestEntry(
				definition.Id,
				definition.Suit,
				definition.Rarity,
				Enabled: true,
				runtimeMetadata.HasRuntimeHandler,
				runtimeMetadata.StateScope,
				runtimeMetadata.StackingMode,
				runtimeMetadata.TriggerPolicy,
				runtimeMetadata.MaximumValue,
				runtimeMetadata.ConflictGroup,
				runtimeMetadata.Priority,
				DescriptionKey: definition.DescriptionKey,
				EffectValues: effectValues,
				DescriptionValueKeys: descriptionValueKeys));
		}

		foreach (string runtimeUpgradeId in AdventureUpgradeRuntimeContract.All.Keys)
		{
			if (catalogIds.Contains(runtimeUpgradeId))
			{
				continue;
			}

			GD.PushWarning(
				$"Runtime adventure suit upgrade '{runtimeUpgradeId}' is not present in the enabled Pandora catalog.");
				manifest = new AdventureUpgradeManifest(Array.Empty<AdventureUpgradeManifestEntry>());
				AdventureUpgradeRuntimeValues.SetManifest(null);
				return false;
		}

		try
		{
			manifest = new AdventureUpgradeManifest(entries);
				if (!manifest.IsReady)
				{
					AdventureUpgradeRuntimeValues.SetManifest(null);
					return false;
			}

			_cachedManifest = manifest;
			AdventureUpgradeRuntimeValues.SetManifest(manifest);
			return true;
		}
		catch (ArgumentException error)
		{
			GD.PushWarning($"Adventure suit upgrade manifest is invalid: {error.Message}");
			manifest = new AdventureUpgradeManifest(Array.Empty<AdventureUpgradeManifestEntry>());
			AdventureUpgradeRuntimeValues.SetManifest(null);
			return false;
		}
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
		AdventureUpgradeRuntimeValues.SetManifest(null);
		GD.PushWarning($"Adventure suit upgrade catalog is invalid: {message}");
	}
}
