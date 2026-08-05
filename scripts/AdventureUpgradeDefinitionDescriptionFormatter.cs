internal static class AdventureUpgradeDefinitionDescriptionFormatter
{
	internal static string Format(
		AdventureSuitUpgradeDefinition definition,
		AdventureUpgradeManifestEntry? manifestEntry,
		string? localizedDescription,
		int currentItemCount)
	{
		return AdventureUpgradeManifest.FormatDescription(
			definition.Id,
			localizedDescription,
			manifestEntry?.EffectValues,
			currentItemCount);
	}
}
