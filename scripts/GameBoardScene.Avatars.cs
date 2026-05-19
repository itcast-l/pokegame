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

		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			DraggableAvatarArea? area = _playerAvatarAreas[playerIndex];
			if (area == null || !GodotObject.IsInstanceValid(area))
			{
				continue;
			}

			CharacterAvatar? avatar = InstantiateAvatar(avatarScene, playerIndex);
			if (avatar == null)
			{
				continue;
			}

			area.AddChild(avatar);
			FitAvatarToArea(avatar);
			_playerAvatars[playerIndex] = avatar;
		}
	}

	private static CharacterAvatar? InstantiateAvatar(PackedScene avatarScene, int playerIndex)
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
		avatar.AccentColor = PlayerAvatarAccentColors[playerIndex];
		avatar.AttackDirection = GetAvatarAttackDirection(playerIndex);
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

	private static Vector2 GetAvatarAttackDirection(int playerIndex)
	{
		return playerIndex switch
		{
			0 => Vector2.Up,
			1 => Vector2.Down,
			2 => Vector2.Right,
			3 => Vector2.Left,
			_ => Vector2.Right,
		};
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

		avatar.PlayAction(action);
	}
}
