using System.Globalization;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private void SelectAvatar(string avatarId)
	{
		string normalized = NormalizeAdventureAvatarId(avatarId);
		if (!IsStageUnlocked(normalized))
		{
			return;
		}

		_selectedAvatarId = normalized;
		RefreshSelection();
	}

	private void RefreshSelection()
	{
		EnsureSelectedStageIsUnlocked();

		foreach ((string avatarId, OceanMenuButtonFrame frame) in _avatarFrames)
		{
			int stageNumber = GetStageNumber(avatarId);
			bool isUnlocked = IsStageUnlocked(stageNumber);
			bool isCompleted = CharacterAvatarCatalog.IsAdventureStageCompleted(stageNumber);
			bool isAvailable = CharacterAvatarCatalog.IsAdventureStageAvailable(stageNumber);
			frame.Selected = avatarId == _selectedAvatarId;
			frame.Modulate = isUnlocked ? Colors.White : new Color(0.48f, 0.58f, 0.62f, 0.58f);
			frame.QueueRedraw();

			if (_stageButtons.TryGetValue(avatarId, out Button? button))
			{
				button.Disabled = !isUnlocked;
				button.MouseFilter = isUnlocked ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
				button.TooltipText = isUnlocked
					? CharacterAvatarCatalog.GetById(avatarId).DisplayName
					: Tr("ADVENTURE_LOCKED").ToString();
			}

			if (_stageStatusLabels.TryGetValue(avatarId, out Label? statusLabel))
			{
				statusLabel.Text = GetStageStatusText(stageNumber);
				statusLabel.AddThemeColorOverride("font_color", GetStageStatusColor(isCompleted, isAvailable, isUnlocked));
			}
		}

		UpdatePreview();
	}

	private void UpdatePreview()
	{
		CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(_selectedAvatarId);
		int stageNumber = _stageNumbers.TryGetValue(avatarDefinition.Id, out int number) ? number : 1;

		_previewStageLabel.Text = FormatStageNumber(stageNumber);
		_previewNameLabel.Text = avatarDefinition.DisplayName;
		_previewStatusLabel.Text = GetStageStatusText(stageNumber);
		_previewAbilityTitleLabel.Text = Tr("ADVENTURE_ABILITY_TITLE").ToString();
		_previewAbilityNameLabel.Text = Tr(avatarDefinition.AdventureAbilityNameKey).ToString();
		_previewAbilityDescriptionLabel.Text = Tr(avatarDefinition.AdventureAbilityDescriptionKey).ToString();
		if (_previewAbilityDescriptionScroll != null && GodotObject.IsInstanceValid(_previewAbilityDescriptionScroll))
		{
			_previewAbilityDescriptionScroll.ScrollVertical = 0;
		}

		_previewStatusLabel.AddThemeColorOverride(
			"font_color",
			GetStageStatusColor(
				CharacterAvatarCatalog.IsAdventureStageCompleted(stageNumber),
				CharacterAvatarCatalog.IsAdventureStageAvailable(stageNumber),
				IsStageUnlocked(stageNumber)));
		_startButton.Disabled = !IsStageUnlocked(stageNumber);

		if (_previewAvatar != null && GodotObject.IsInstanceValid(_previewAvatar))
		{
			_previewAvatar.AvatarRoot = avatarDefinition.RootPath;
			_previewAvatar.AccentColor = GetAvatarAccentColor(avatarDefinition);
			_previewAvatar.PlayIdle();
		}
	}

	private string GetInitialAvatarId()
	{
		int nextStage = Mathf.Clamp(
			CharacterAvatarCatalog.LoadHighestCompletedAdventureStage() + 1,
			1,
			CharacterAvatarCatalog.GetAdventureStageCount());
		return CharacterAvatarCatalog.GetAdventureAvatarIdForStage(nextStage);
	}

	private static string NormalizeAdventureAvatarId(string avatarId)
	{
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		if (normalized != CharacterAvatarCatalog.DefaultAvatarId)
		{
			return normalized;
		}

		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			if (avatarDefinition.Id != CharacterAvatarCatalog.DefaultAvatarId)
			{
				return avatarDefinition.Id;
			}
		}

		return CharacterAvatarCatalog.DefaultAvatarId;
	}

	private static string FormatStageNumber(int stageNumber)
	{
		return stageNumber.ToString("00");
	}

	private void EnsureSelectedStageIsUnlocked()
	{
		if (IsStageUnlocked(_selectedAvatarId))
		{
			return;
		}

		_selectedAvatarId = GetInitialAvatarId();
	}

	private int GetStageNumber(string avatarId)
	{
		return _stageNumbers.TryGetValue(avatarId, out int stageNumber)
			? stageNumber
			: CharacterAvatarCatalog.GetAdventureStageNumber(avatarId);
	}

	private bool IsStageUnlocked(string avatarId)
	{
		return IsStageUnlocked(GetStageNumber(avatarId));
	}

	private static bool IsStageUnlocked(int stageNumber)
	{
		return CharacterAvatarCatalog.IsAdventureStageUnlocked(stageNumber);
	}

	private string GetStageStatusText(int stageNumber)
	{
		if (CharacterAvatarCatalog.IsAdventureStageCompleted(stageNumber))
		{
			return Tr("ADVENTURE_COMPLETED").ToString();
		}

		if (CharacterAvatarCatalog.IsAdventureStageAvailable(stageNumber))
		{
			return Tr("ADVENTURE_AVAILABLE").ToString();
		}

		return IsStageUnlocked(stageNumber)
			? Tr("ADVENTURE_TEST_AVAILABLE").ToString()
			: Tr("ADVENTURE_LOCKED").ToString();
	}

	private static Color GetStageStatusColor(bool isCompleted, bool isAvailable, bool isUnlocked)
	{
		if (isCompleted)
		{
			return new Color(0.58f, 1.0f, 0.78f);
		}

		if (isAvailable)
		{
			return new Color(1.0f, 0.92f, 0.48f);
		}

		return isUnlocked
			? new Color(0.80f, 0.95f, 1.0f)
			: new Color(0.58f, 0.68f, 0.72f);
	}

	private static Color GetAvatarAccentColor(CharacterAvatarDefinition avatarDefinition)
	{
		return TryParseAvatarAccentColor(avatarDefinition.AccentColorHtml, out Color accentColor)
			? accentColor
			: new Color(0.62f, 1.0f, 0.92f);
	}

	private static bool TryParseAvatarAccentColor(string rawValue, out Color color)
	{
		color = default;
		if (string.IsNullOrWhiteSpace(rawValue))
		{
			return false;
		}

		string hex = rawValue.Trim().TrimStart('#');
		if (hex.Length != 6 && hex.Length != 8)
		{
			return false;
		}

		if (!uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
		{
			return false;
		}

		float r;
		float g;
		float b;
		float a = 1.0f;
		if (hex.Length == 6)
		{
			r = ((value >> 16) & 0xff) / 255.0f;
			g = ((value >> 8) & 0xff) / 255.0f;
			b = (value & 0xff) / 255.0f;
		}
		else
		{
			r = ((value >> 24) & 0xff) / 255.0f;
			g = ((value >> 16) & 0xff) / 255.0f;
			b = ((value >> 8) & 0xff) / 255.0f;
			a = (value & 0xff) / 255.0f;
		}

		color = new Color(r, g, b, a);
		return true;
	}
}
