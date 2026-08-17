using System.Globalization;
using Godot;

public partial class AdventureAvatarMenuScene
{
	private void SelectAvatar(string avatarId)
	{
		string normalized = NormalizeAdventureAvatarId(avatarId);
		if (!IsAvatarInSelectedAdventure(normalized) || !IsStageUnlocked(normalized))
		{
			return;
		}

		_selectedAvatarId = normalized;
		RefreshSelection();
	}

	private void RefreshSelection()
	{
		RefreshAdventureModeSelection();
		EnsureSelectedStageIsUnlocked();

		foreach ((string avatarId, OceanMenuButtonFrame frame) in _avatarFrames)
		{
			int stageNumber = GetStageNumber(avatarId);
			bool isUnlocked = IsStageUnlocked(avatarId);
			bool isCompleted = IsStageCompleted(avatarId);
			bool isAvailable = IsStageAvailable(avatarId);
			frame.Selected = _listSelectionHighlightVisible && avatarId == _selectedAvatarId;
			frame.Modulate = isUnlocked ? Colors.White : new Color(0.48f, 0.58f, 0.62f, 0.58f);
			frame.QueueRedraw();

			if (_stageButtons.TryGetValue(avatarId, out Button? button))
			{
				button.Disabled = !isUnlocked;
				button.MouseFilter = isUnlocked ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
			}

			if (_stageAvatars.TryGetValue(avatarId, out CharacterAvatar? avatar))
			{
				avatar.Visible = isUnlocked;
			}

			if (_stageNameLabels.TryGetValue(avatarId, out Label? nameLabel))
			{
				ApplyStageNameLabelText(nameLabel, GetStageDisplayName(avatarId));
			}

			if (_stageStatusLabels.TryGetValue(avatarId, out Label? statusLabel))
			{
				statusLabel.Text = GetStageStatusText(stageNumber);
				statusLabel.AddThemeColorOverride("font_color", GetStageStatusColor(isCompleted, isAvailable, isUnlocked));
			}
		}

		UpdatePreview();
	}

	private string GetStageDisplayName(string avatarId)
	{
		return IsStageUnlocked(avatarId)
			? LocalizeAvatarDisplayName(CharacterAvatarCatalog.GetById(avatarId))
			: LockedStageNamePlaceholder;
	}

	private void UpdatePreview()
	{
		CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(_selectedAvatarId);
		int displayStageNumber = GetDisplayStageNumber(avatarDefinition.Id);
		int stageNumber = GetStageNumber(avatarDefinition.Id);

		_previewStageLabel.Text = FormatStageNumber(displayStageNumber);
		_previewNameLabel.Text = LocalizeAvatarDisplayName(avatarDefinition);
		_previewStatusLabel.Text = GetStageStatusText(stageNumber);

		_previewStatusLabel.AddThemeColorOverride(
			"font_color",
				GetStageStatusColor(
					IsStageCompleted(avatarDefinition.Id),
					IsStageAvailable(avatarDefinition.Id),
					IsStageUnlocked(avatarDefinition.Id)));
		_startButton.Disabled = !IsStageUnlocked(avatarDefinition.Id);

		if (_previewAvatar != null && GodotObject.IsInstanceValid(_previewAvatar))
		{
			_previewAvatar.AvatarRoot = avatarDefinition.RootPath;
			_previewAvatar.AudioConfig = avatarDefinition.AudioConfig;
			_previewAvatar.AccentColor = GetAvatarAccentColor(avatarDefinition);
			_previewAvatar.PlayIdle();
		}
	}

	private string GetInitialAvatarId()
	{
		if (_selectedAdventureId == AdventureCampaignCatalog.TutorialCampaignId)
		{
			foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
			{
					if (!CharacterAvatarCatalog.IsTutorialCompleted(
						_profileSnapshot.CompletedAdventureTutorialIdsCsv,
					avatarDefinition.Id))
				{
					return avatarDefinition.Id;
				}
			}

			return GetSelectedAdventureEnemyDefinitions().Count > 0
				? GetSelectedAdventureEnemyDefinitions()[0].Id
				: CharacterAvatarCatalog.DefaultAvatarId;
		}

		int nextStage = Mathf.Clamp(
			_profileSnapshot.HighestCompletedMainAdventureStage + 1,
			1,
			AdventureCampaignCatalog.MainStageCount);
		CharacterAvatarDefinition? nextEnemy = GetSelectedAdventureEnemyForStage(nextStage);
		if (nextEnemy != null)
		{
			return nextEnemy.Id;
		}

		foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
		{
			if (IsStageUnlocked(avatarDefinition.Id))
			{
				return avatarDefinition.Id;
			}
		}

		foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
		{
			return avatarDefinition.Id;
		}

		return CharacterAvatarCatalog.DefaultAvatarId;
	}

	private string NormalizeAdventureAvatarId(string avatarId)
	{
		string normalized = CharacterAvatarCatalog.NormalizeAvatarId(avatarId);
		if (normalized != CharacterAvatarCatalog.DefaultAvatarId && IsAvatarInSelectedAdventure(normalized))
		{
			return normalized;
		}

		return GetInitialAvatarId();
	}

	private static string FormatStageNumber(int stageNumber)
	{
		return stageNumber.ToString("00");
	}

	private void EnsureSelectedStageIsUnlocked()
	{
		if (IsAvatarInSelectedAdventure(_selectedAvatarId) && IsStageUnlocked(_selectedAvatarId))
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

	private int GetDisplayStageNumber(string avatarId)
	{
		return _displayStageNumbers.TryGetValue(avatarId, out int displayStageNumber)
			? displayStageNumber
			: 1;
	}

	private bool IsStageUnlocked(string avatarId)
	{
		if (_selectedAdventureId == AdventureCampaignCatalog.TutorialCampaignId)
		{
			return AdventureCampaignCatalog.IsTutorialAvatar(avatarId);
		}

		int stageNumber = GetStageNumber(avatarId);
		return stageNumber >= 1
			&& stageNumber <= AdventureCampaignCatalog.MainStageCount
			&& stageNumber <= _profileSnapshot.HighestCompletedMainAdventureStage + 1;
	}

	private bool IsStageCompleted(string avatarId)
	{
		if (_selectedAdventureId == AdventureCampaignCatalog.TutorialCampaignId)
		{
			return CharacterAvatarCatalog.IsTutorialCompleted(
				_profileSnapshot.CompletedAdventureTutorialIdsCsv,
				avatarId);
		}

		int stageNumber = GetStageNumber(avatarId);
		return stageNumber >= 1
			&& stageNumber <= AdventureCampaignCatalog.MainStageCount
			&& stageNumber <= _profileSnapshot.HighestCompletedMainAdventureStage;
	}

	private bool IsStageAvailable(string avatarId)
	{
		if (_selectedAdventureId == AdventureCampaignCatalog.TutorialCampaignId)
		{
			return !IsStageCompleted(avatarId);
		}

		int stageNumber = GetStageNumber(avatarId);
		return stageNumber >= 1
			&& stageNumber <= AdventureCampaignCatalog.MainStageCount
			&& stageNumber == _profileSnapshot.HighestCompletedMainAdventureStage + 1;
	}

	private string GetStageStatusText(int stageNumber)
	{
		if (IsStageCompleted(GetAvatarIdForStage(stageNumber)))
		{
			return Tr("ADVENTURE_COMPLETED").ToString();
		}

		if (IsStageAvailable(GetAvatarIdForStage(stageNumber)))
		{
			return Tr("ADVENTURE_AVAILABLE").ToString();
		}

		return IsStageUnlocked(GetAvatarIdForStage(stageNumber))
			? Tr("ADVENTURE_UNLOCKED").ToString()
			: Tr("ADVENTURE_LOCKED").ToString();
	}

	private string GetAvatarIdForStage(int stageNumber)
	{
		foreach (AdventureLevelDefinition level in GetSelectedAdventureDefinition().Levels)
		{
			if (GetCampaignStageIndex(level, GetSelectedAdventureDefinition().Id) == stageNumber)
			{
				return level.EnemyAvatarId;
			}
		}

		return CharacterAvatarCatalog.DefaultAvatarId;
	}

	private static Color GetStageStatusColor(bool isCompleted, bool isAvailable, bool isUnlocked)
	{
		if (isCompleted)
		{
			return new Color(0.58f, 1.0f, 0.78f);
		}

		if (isAvailable)
		{
			return new Color(0.58f, 0.96f, 0.70f);
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
