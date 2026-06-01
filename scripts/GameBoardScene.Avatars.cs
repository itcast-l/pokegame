using Godot;

public partial class GameBoardScene
{
	private static readonly string[] PlayerAvatarAreaNodeNames =
	{
		"Player1AvatarArea",
		"Player2AvatarArea",
		"Player3AvatarArea",
		"Player4AvatarArea",
	};
	private static readonly Color[] PlayerAvatarAccentColors =
	{
		new(1.0f, 0.84f, 0.48f, 1.0f),
		new(0.56f, 0.92f, 1.0f, 1.0f),
		new(1.0f, 0.62f, 0.84f, 1.0f),
		new(0.60f, 1.0f, 0.78f, 1.0f),
	};

	private readonly DraggableAvatarArea?[] _playerAvatarAreas = new DraggableAvatarArea?[MaxPlayers];
	private readonly CharacterAvatar?[] _playerAvatars = new CharacterAvatar?[MaxPlayers];

	private void BindPlayerAvatarAreas()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			DraggableAvatarArea? area = GetNodeOrNull<DraggableAvatarArea>($"%{PlayerAvatarAreaNodeNames[playerIndex]}");
			if (area == null)
			{
				GD.PushWarning($"Missing avatar area node: {PlayerAvatarAreaNodeNames[playerIndex]}.");
				continue;
			}

			_playerAvatarAreas[playerIndex] = area;
			area.TooltipText = GetPlayerScoreTooltip(playerIndex);
		}
	}

	private void CreatePlayerAvatars()
	{
		PackedScene? avatarScene = GD.Load<PackedScene>(CharacterAvatarScenePath);
		if (avatarScene == null)
		{
			GD.PushError($"Missing avatar scene: {CharacterAvatarScenePath}");
			return;
		}

		CharacterAvatarDefinition playerAvatarDefinition = CharacterAvatarCatalog.LoadSelectedAvatar();
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			DraggableAvatarArea? area = _playerAvatarAreas[playerIndex];
			if (area == null || !GodotObject.IsInstanceValid(area))
			{
				continue;
			}

			CharacterAvatarDefinition avatarDefinition = GetAvatarDefinitionForPlayer(playerIndex, playerAvatarDefinition);
			CharacterAvatar? avatar = InstantiateAvatar(avatarScene, playerIndex, avatarDefinition);
			if (avatar == null)
			{
				continue;
			}

			area.AddChild(avatar);
			FitAvatarToArea(avatar);
			_playerAvatars[playerIndex] = avatar;
		}
	}

	private CharacterAvatarDefinition GetAvatarDefinitionForPlayer(int playerIndex, CharacterAvatarDefinition playerAvatarDefinition)
	{
		if (!adventure_mode)
		{
			return playerAvatarDefinition;
		}

		return playerIndex switch
		{
			0 => CharacterAvatarCatalog.Default,
			1 => CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id),
			_ => playerAvatarDefinition,
		};
	}

	private static string NormalizeAdventureEnemyAvatarId(string avatarId)
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

	private bool AdventureEnemyHasAbility(string abilityId)
	{
		return adventure_mode
			&& CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id).HasAbility(abilityId);
	}

	private int GetAdventureEnemyAbilityValue(string key, int fallback)
	{
		return adventure_mode
			? CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id).GetAbilityValueInt(key, fallback)
			: fallback;
	}

	private string GetAdventureEnemyAbilityValue(string key, string fallback)
	{
		return adventure_mode
			? CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id).GetAbilityValueString(key, fallback)
			: fallback;
	}

	private static CharacterAvatar? InstantiateAvatar(PackedScene avatarScene, int playerIndex, CharacterAvatarDefinition avatarDefinition)
	{
		Node avatarNode = avatarScene.Instantiate();
		if (avatarNode is not CharacterAvatar avatar)
		{
			GD.PushError($"{CharacterAvatarScenePath} root must use CharacterAvatar.");
			avatarNode.QueueFree();
			return null;
		}

		avatar.Name = $"Player{playerIndex + 1}Avatar";
		avatar.MouseFilter = MouseFilterEnum.Ignore;
		avatar.AvatarRoot = avatarDefinition.RootPath;
		avatar.AudioConfig = avatarDefinition.AudioConfig;
		avatar.AccentColor = PlayerAvatarAccentColors[playerIndex];
		return avatar;
	}

	private static void FitAvatarToArea(Control avatar)
	{
		avatar.SetAnchorsPreset(LayoutPreset.FullRect);
		avatar.OffsetLeft = 0.0f;
		avatar.OffsetTop = 0.0f;
		avatar.OffsetRight = 0.0f;
		avatar.OffsetBottom = 0.0f;
	}

	private void PlayPlayerAvatarAction(int playerIndex, CharacterAvatar.AvatarAction action)
	{
		if (playerIndex < 0 || playerIndex >= _playerAvatars.Length)
		{
			return;
		}

		CharacterAvatar? avatar = _playerAvatars[playerIndex];
		if (avatar == null || !GodotObject.IsInstanceValid(avatar))
		{
			return;
		}

		PlayPlayerAvatarActionSfx(playerIndex, action);
		avatar.PlayAction(action);
	}

	private void UpdatePlayerAvatarTooltip(int playerIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerAvatarAreas.Length)
		{
			return;
		}

		DraggableAvatarArea? area = _playerAvatarAreas[playerIndex];
		if (area == null || !GodotObject.IsInstanceValid(area))
		{
			return;
		}

		area.TooltipText = GetPlayerScoreTooltip(playerIndex);
	}
}
