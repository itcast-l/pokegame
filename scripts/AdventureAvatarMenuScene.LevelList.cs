using Godot;

public partial class AdventureAvatarMenuScene
{
	private void BindLevelList()
	{
		_avatarFrames.Clear();
		_stageNumbers.Clear();
		_stageButtons.Clear();
		_stageStatusLabels.Clear();

		foreach (CharacterAvatarDefinition avatarDefinition in GetSelectedAdventureEnemyDefinitions())
		{
			if (avatarDefinition.AdventureStageNumber <= 0)
			{
				continue;
			}

			BindLevelEntry(avatarDefinition, avatarDefinition.AdventureStageNumber);
		}
	}

	private void RebuildLevelList()
	{
		foreach ((string avatarId, OceanMenuButtonFrame frame) in _avatarFrames)
		{
			frame.Visible = IsAvatarInSelectedAdventure(avatarId);
		}
	}

	private void BindLevelEntry(CharacterAvatarDefinition avatarDefinition, int stageNumber)
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
		_stageNumbers[avatarDefinition.Id] = stageNumber;

		Label badgeLabel = frame.GetNode<Label>("EntryContent/EntryRow/StageBadge/StageNumber");
		badgeLabel.Text = FormatStageNumber(stageNumber);

		if (frame.GetNodeOrNull<CharacterAvatar>("EntryContent/EntryRow/EntryAvatarAnchor/EntryAvatar") is CharacterAvatar avatar)
		{
			ConfigureAvatarView(avatar, avatarDefinition, 0.74f);
		}

		Label nameLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryName");
		nameLabel.Text = avatarDefinition.DisplayName;

		Label statusLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryStatus");
		_stageStatusLabels[avatarDefinition.Id] = statusLabel;

		Label arrowLabel = frame.GetNode<Label>("EntryContent/EntryRow/EntryArrow");
		arrowLabel.AddThemeColorOverride("font_color", new Color(accentColor.R, accentColor.G, accentColor.B, 0.82f));

		Button button = frame.GetNode<Button>("Button");
		button.FocusMode = Control.FocusModeEnum.All;
		button.TooltipText = avatarDefinition.DisplayName;
		button.Pressed += () => SelectAvatar(avatarDefinition.Id);
		button.FocusEntered += () =>
		{
			SelectAvatar(avatarDefinition.Id);
			ScrollSelectedEntryIntoView();
		};
		_stageButtons[avatarDefinition.Id] = button;
	}
}
