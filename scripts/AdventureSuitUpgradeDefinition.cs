using Godot;
using System;
using System.Collections.Generic;

public readonly struct AdventureSuitUpgradeDefinition
{
	public AdventureSuitUpgradeDefinition(
		string id,
		string displayName,
		string suit,
		string rarity,
		string triggerText,
		string effectText,
		string descriptionKey,
		string notes,
		bool enabled,
		int sortOrder,
		string effectValuesRaw = "",
		string descriptionValueKeysCsv = "")
	{
		Id = id;
		DisplayName = displayName;
		Suit = suit;
		Rarity = rarity;
		TriggerText = triggerText;
		EffectText = effectText;
		DescriptionKey = descriptionKey;
		Notes = notes;
		Enabled = enabled;
		SortOrder = sortOrder;
		EffectValuesRaw = effectValuesRaw ?? string.Empty;
		DescriptionValueKeysCsv = descriptionValueKeysCsv ?? string.Empty;
	}

	public string Id { get; }
	public string DisplayName { get; }
	public string Suit { get; }
	public string Rarity { get; }
	public string TriggerText { get; }
	public string EffectText { get; }
	public string DescriptionKey { get; }
	public string Notes { get; }
	public bool Enabled { get; }
	public int SortOrder { get; }
	public string EffectValuesRaw { get; }
	public string DescriptionValueKeysCsv { get; }

	public Godot.Collections.Dictionary ToDictionary()
	{
		return new Godot.Collections.Dictionary
		{
			["id"] = Id,
			["display_name"] = DisplayName,
				["suit"] = Suit,
				["rarity"] = Rarity,
				["trigger_text"] = TriggerText,
				["effect_text"] = EffectText,
				["description_key"] = DescriptionKey,
				["notes"] = Notes,
				["enabled"] = Enabled,
				["sort_order"] = SortOrder,
				["effect_values"] = EffectValuesRaw,
				["description_value_keys"] = DescriptionValueKeysCsv,
			};
	}
}
