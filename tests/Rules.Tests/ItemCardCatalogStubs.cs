public readonly struct ItemCardDefinition
{
	public ItemCardDefinition(string attachedEffectType)
		: this(string.Empty, attachedEffectType)
	{
	}

	public ItemCardDefinition(string id, string attachedEffectType)
	{
		Id = id;
		AttachedEffectType = attachedEffectType;
	}

	public string Id { get; }
	public string AttachedEffectType { get; }
}

public sealed class ItemCardCatalog
{
	public const string MagnetItemCardId = "magnet";

	public bool TryGetDefinition(string itemCardId, out ItemCardDefinition definition)
	{
		definition = default;
		return false;
	}
}
