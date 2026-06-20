public readonly struct ItemCardDefinition
{
	public ItemCardDefinition(string attachedEffectType)
	{
		AttachedEffectType = attachedEffectType;
	}

	public string AttachedEffectType { get; }
}

public sealed class ItemCardCatalog
{
	public bool TryGetDefinition(string itemCardId, out ItemCardDefinition definition)
	{
		definition = default;
		return false;
	}
}
