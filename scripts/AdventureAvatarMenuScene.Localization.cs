using Godot;

public partial class AdventureAvatarMenuScene
{
	private void RefreshLocalizedText()
	{
		RefreshSelection();
		RefreshAvatarUnlockPromptText();

		for (int index = _localizedLabels.Count - 1; index >= 0; index--)
		{
			(Label label, string key) = _localizedLabels[index];
			if (!GodotObject.IsInstanceValid(label))
			{
				_localizedLabels.RemoveAt(index);
				continue;
			}

			label.Text = Localize(key);
		}

		for (int index = _localizedButtons.Count - 1; index >= 0; index--)
		{
			(Button button, string key) = _localizedButtons[index];
			if (!GodotObject.IsInstanceValid(button))
			{
				_localizedButtons.RemoveAt(index);
				continue;
			}

			button.Text = Localize(key);
		}
	}

	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? GetAdventureMenuLocalizationFallback(key) : text;
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}
}
