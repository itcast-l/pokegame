public sealed class ItemCardCatalog
{
	public const string MagnetItemCardId = "magnet";

	public bool TryGetDefinition(string itemCardId, out ItemCardDefinition definition)
	{
		definition = default;
		return false;
	}
}
