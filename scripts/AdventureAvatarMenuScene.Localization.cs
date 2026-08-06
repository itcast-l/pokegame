using Godot;

public partial class AdventureAvatarMenuScene
{
	private void RefreshLocalizedText()
	{
		RefreshSelection();
		RefreshAvatarUnlockPromptText();
		RefreshRunResumeDialogText();

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
		if (text == key)
		{
			GD.PushError($"Missing adventure menu localization key: {key}");
		}

		return text;
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private string LocalizeAvatarDisplayName(CharacterAvatarDefinition avatarDefinition)
	{
		if (!string.IsNullOrWhiteSpace(avatarDefinition.DisplayNameKey))
		{
			string text = Localize(avatarDefinition.DisplayNameKey);
			if (text != avatarDefinition.DisplayNameKey)
			{
				return text;
			}
		}

		return string.IsNullOrWhiteSpace(avatarDefinition.DisplayName)
			? avatarDefinition.Id
			: avatarDefinition.DisplayName;
	}

	private void ApplyStageNameLabelText(Label label, string text)
	{
		label.Text = text;
		label.AddThemeFontSizeOverride("font_size", GetFittingStageNameFontSize(label, text));
	}

	private void QueueStageNameSizingRefresh()
	{
		if (_stageNameSizingRefreshQueued || !IsInsideTree())
		{
			return;
		}

		_stageNameSizingRefreshQueued = true;
		CallDeferred(nameof(RefreshStageNameSizingDeferred));
	}

	private void RefreshStageNameSizingDeferred()
	{
		_stageNameSizingRefreshQueued = false;
		if (!IsNodeReady())
		{
			return;
		}

		foreach ((string avatarId, Label label) in _stageNameLabels)
		{
			if (!GodotObject.IsInstanceValid(label))
			{
				continue;
			}

			ApplyStageNameLabelText(label, GetStageDisplayName(avatarId));
		}
	}

	private static int GetFittingStageNameFontSize(Label label, string text)
	{
		float availableWidth = label.Size.X;
		if (availableWidth <= 0.0f || string.IsNullOrEmpty(text))
		{
			return StageNameBaseFontSize;
		}

		Font font = label.GetThemeFont("font");
		for (int fontSize = StageNameBaseFontSize; fontSize > StageNameMinFontSize; fontSize--)
		{
			float width = font.GetStringSize(text, label.HorizontalAlignment, -1.0f, fontSize).X;
			if (width <= availableWidth + StageNameWidthTolerance)
			{
				return fontSize;
			}
		}

		return StageNameMinFontSize;
	}
}
