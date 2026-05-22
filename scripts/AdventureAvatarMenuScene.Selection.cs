using Godot;

public partial class AdventureAvatarMenuScene
{
	private void SelectAvatar(string avatarId)
	{
		_selectedAvatarId = NormalizeAdventureAvatarId(avatarId);
		RefreshSelection();
	}

	private void RefreshSelection()
	{
		foreach ((string avatarId, OceanMenuButtonFrame frame) in _avatarFrames)
		{
			frame.Selected = avatarId == _selectedAvatarId;
			frame.QueueRedraw();
		}

		UpdatePreview();
	}

	private void UpdatePreview()
	{
		CharacterAvatarDefinition avatarDefinition = CharacterAvatarCatalog.GetById(_selectedAvatarId);
		int stageNumber = _stageNumbers.TryGetValue(avatarDefinition.Id, out int number) ? number : 1;

		_previewStageLabel.Text = FormatStageNumber(stageNumber);
		_previewNameLabel.Text = avatarDefinition.DisplayName;

		if (_previewAvatar != null && GodotObject.IsInstanceValid(_previewAvatar))
		{
			_previewAvatar.AvatarRoot = avatarDefinition.RootPath;
			_previewAvatar.AccentColor = GetAvatarAccentColor(avatarDefinition);
			_previewAvatar.PlayIdle();
		}
	}

	private string GetInitialAvatarId()
	{
		return NormalizeAdventureAvatarId(CharacterAvatarCatalog.LoadSelectedAvatar().Id);
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

	private static Color GetAvatarAccentColor(CharacterAvatarDefinition avatarDefinition)
	{
		return avatarDefinition.Id is "dragon_prince_j" or "dragon_queen_q"
			? new Color(1.0f, 0.86f, 0.46f)
			: avatarDefinition.Id == "boss_k"
				? new Color(0.24f, 0.88f, 1.0f)
				: new Color(0.62f, 1.0f, 0.92f);
	}
}
