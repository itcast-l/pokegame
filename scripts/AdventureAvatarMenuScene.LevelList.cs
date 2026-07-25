using Godot;

public partial class AdventureAvatarMenuScene
{
	private void BindLevelList()
	{
		ClearLevelEntries();
	}

	private void RebuildLevelList()
	{
		ClearLevelEntries();

		AdventureModeDefinition adventureMode = GetSelectedAdventureDefinition();
		for (int index = 0; index < adventureMode.Levels.Length; index++)
		{
			AdventureLevelDefinition level = adventureMode.Levels[index];
			CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(level.EnemyAvatarId);
			int stageNumber = GetCampaignStageIndex(level, adventureMode.Id);
			if (avatarDefinition.Id == CharacterAvatarCatalog.DefaultAvatarId || stageNumber <= 0)
			{
				GD.PushWarning($"Skipping invalid adventure level entry: {level.EnemyAvatarId}");
				continue;
			}

			if (CreateLevelEntry() is not OceanMenuButtonFrame frame)
			{
				continue;
			}

			frame.Name = $"{avatarDefinition.Id}LevelEntry";
			_levelList.AddChild(frame);
			BindLevelEntry(frame, avatarDefinition, stageNumber, index + 1);
		}
	}

	private void ClearLevelEntries()
	{
		_levelScrollTween?.Kill();
		_levelScrollTarget = 0;
		if (_levelScroll != null && GodotObject.IsInstanceValid(_levelScroll))
		{
			_levelScroll.ScrollVertical = 0;
		}

		if (_levelList != null && GodotObject.IsInstanceValid(_levelList))
		{
			foreach (Node child in _levelList.GetChildren())
			{
				_levelList.RemoveChild(child);
				child.QueueFree();
			}
		}

		_avatarFrames.Clear();
		_displayStageNumbers.Clear();
		_stageNumbers.Clear();
		_stageButtons.Clear();
		_stageNameLabels.Clear();
		_stageStatusLabels.Clear();
	}

	private OceanMenuButtonFrame? CreateLevelEntry()
	{
		if (LevelEntryScene == null)
		{
			GD.PushError("Adventure avatar menu is missing its LevelEntryScene template.");
			return null;
		}

		if (LevelEntryScene.Instantiate() is OceanMenuButtonFrame frame)
		{
			return frame;
		}

		GD.PushError("Adventure level entry template root must use OceanMenuButtonFrame.");
		return null;
	}

	private void BindLevelEntry(
		OceanMenuButtonFrame frame,
		CharacterAvatarDefinition avatarDefinition,
		int stageNumber,
		int displayStageNumber)
	{
		string avatarId = avatarDefinition.Id;
		Color accentColor = GetAvatarAccentColor(avatarDefinition);
		frame.AccentColor = accentColor;
		_avatarFrames[avatarId] = frame;
		_displayStageNumbers[avatarId] = displayStageNumber;
		_stageNumbers[avatarId] = stageNumber;

		if (frame.GetNodeOrNull<Label>("EntryContent/EntryRow/StageBadge/StageNumber") is Label badgeLabel)
		{
			badgeLabel.Text = FormatStageNumber(displayStageNumber);
		}

		if (frame.GetNodeOrNull<CharacterAvatar>("EntryContent/EntryRow/EntryAvatarAnchor/EntryAvatar") is CharacterAvatar avatar)
		{
			ConfigureAvatarView(avatar, avatarDefinition, 0.74f);
		}

		string displayName = LocalizeAvatarDisplayName(avatarDefinition);
		Label nameLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryName");
		ApplyStageNameLabelText(nameLabel, displayName);
		nameLabel.Resized += QueueStageNameSizingRefresh;
		_stageNameLabels[avatarId] = nameLabel;

		Label statusLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryStatus");
		_stageStatusLabels[avatarId] = statusLabel;

		Label arrowLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryArrow");
		arrowLabel.AddThemeColorOverride("font_color", new Color(accentColor.R, accentColor.G, accentColor.B, 0.82f));

		Button button = frame.GetNode<Button>("Button");
		button.FocusMode = Control.FocusModeEnum.All;
		button.TooltipText = displayName;
		button.Pressed += () => SelectAvatar(avatarId);
		button.FocusEntered += () =>
		{
			SelectAvatar(avatarId);
			ScrollSelectedEntryIntoView();
		};
		_stageButtons[avatarId] = button;
	}
}
