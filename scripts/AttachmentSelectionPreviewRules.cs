public static class AttachmentSelectionPreviewRules
{
	public static string GetPreviewItemCardId(string pendingItemCardId)
	{
		return string.IsNullOrWhiteSpace(pendingItemCardId)
			? ItemCardCatalog.MagnetItemCardId
			: pendingItemCardId;
	}
}
