using Godot;

public partial class AdventureAvatarMenuScene
{
	private void BindLevelList()
	{
		_avatarFrames.Clear();
		_displayStageNumbers.Clear();
		_stageNumbers.Clear();
		_stageButtons.Clear();
		_stageNameLabels.Clear();
		_stageStatusLabels.Clear();

		var boundAvatarIds = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
		foreach (AdventureModeDefinition adventureMode in AdventureModes)
		{
			foreach (AdventureLevelDefinition level in adventureMode.Levels)
			{
				CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(level.EnemyAvatarId);
				if (avatarDefinition.AdventureStageNumber <= 0 || !boundAvatarIds.Add(avatarDefinition.Id))
				{
					continue;
				}

				BindLevelEntry(avatarDefinition);
			}
		}

		RebuildLevelList();
	}

	private void RebuildLevelList()
	{
		_displayStageNumbers.Clear();

		foreach ((string avatarId, OceanMenuButtonFrame frame) in _avatarFrames)
		{
			frame.Visible = false;
		}

		AdventureLevelDefinition[] levels = GetSelectedAdventureDefinition().Levels;
		for (int index = 0; index < levels.Length; index++)
		{
			string avatarId = CharacterAvatarCatalog.NormalizeAvatarId(levels[index].EnemyAvatarId);
			if (!_avatarFrames.TryGetValue(avatarId, out OceanMenuButtonFrame? frame))
			{
				continue;
			}

			frame.Visible = true;
			_displayStageNumbers[avatarId] = index + 1;

			if (frame.GetNodeOrNull<Label>("EntryContent/EntryRow/StageBadge/StageNumber") is Label badgeLabel)
			{
				badgeLabel.Text = FormatStageNumber(index + 1);
			}
		}
	}

	private void BindLevelEntry(CharacterAvatarDefinition avatarDefinition)
	{
		string nodeName = $"%{avatarDefinition.Id}LevelEntry";
		if (GetNodeOrNull<OceanMenuButtonFrame>(nodeName) is not OceanMenuButtonFrame frame)
		{
			GD.PushWarning($"Missing adventure level entry scene node: {nodeName}");
			return;
		}

		Color accentColor = GetAvatarAccentColor(avatarDefinition);
		frame.AccentColor = accentColor;
		_avatarFrames[avatarDefinition.Id] = frame;
		string adventureId = GetAdventureIdForAvatar(avatarDefinition.Id);
		_stageNumbers[avatarDefinition.Id] = GetCampaignStageIndex(
			new AdventureLevelDefinition(avatarDefinition.Id),
			adventureId);

		if (frame.GetNodeOrNull<CharacterAvatar>("EntryContent/EntryRow/EntryAvatarAnchor/EntryAvatar") is CharacterAvatar avatar)
		{
			ConfigureAvatarView(avatar, avatarDefinition, 0.74f);
		}

		string displayName = LocalizeAvatarDisplayName(avatarDefinition);
		Label nameLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryName");
		ApplyStageNameLabelText(nameLabel, displayName);
		nameLabel.Resized += QueueStageNameSizingRefresh;
		_stageNameLabels[avatarDefinition.Id] = nameLabel;

		Label statusLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryStatus");
		_stageStatusLabels[avatarDefinition.Id] = statusLabel;

		Label arrowLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryArrow");
		arrowLabel.AddThemeColorOverride("font_color", new Color(accentColor.R, accentColor.G, accentColor.B, 0.82f));

		Button button = frame.GetNode<Button>("Button");
		button.FocusMode = Control.FocusModeEnum.All;
		button.TooltipText = displayName;
		button.Pressed += () => SelectAvatar(avatarDefinition.Id);
		button.FocusEntered += () =>
		{
			SelectAvatar(avatarDefinition.Id);
			ScrollSelectedEntryIntoView();
		};
		_stageButtons[avatarDefinition.Id] = button;
	}
}
