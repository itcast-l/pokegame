using Godot;

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
		int sortOrder)
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
			};
	}
}
