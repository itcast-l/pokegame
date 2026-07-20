using Godot;

public static class ItemCardCatalogPreflight
{
	public const string ErrorTitleKey = "ITEM_CARD_CONFIG_ERROR_TITLE";
	public const string ErrorBodyKey = "ITEM_CARD_CONFIG_ERROR_BODY";

	public static bool TryValidate(Node context, out ItemCardCatalog catalog)
	{
		if (ItemCardCatalog.TryLoad(context, out catalog, out string errorMessage))
		{
			return true;
		}

		GD.PushError($"Item card catalog preflight failed: {errorMessage}");
		return false;
	}

	public static void ShowErrorDialog(Control owner)
	{
		AcceptDialog dialog = owner.GetNodeOrNull<AcceptDialog>("ItemCardConfigErrorDialog") ?? new AcceptDialog
		{
			Name = "ItemCardConfigErrorDialog",
			Exclusive = true,
		};
		if (dialog.GetParent() == null)
		{
			owner.AddChild(dialog);
		}

		dialog.Title = Localize(ErrorTitleKey);
		dialog.DialogText = Localize(ErrorBodyKey);
		dialog.PopupCentered();
	}

	private static string Localize(string key)
	{
		string translated = TranslationServer.Translate(key);
		return string.IsNullOrWhiteSpace(translated) || translated == key ? key : translated;
	}
}
