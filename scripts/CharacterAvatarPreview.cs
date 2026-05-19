using Godot;

public partial class CharacterAvatarPreview : Control
{
	private CharacterAvatar _avatar = default!;

	public override void _Ready()
	{
		_avatar = GetNode<CharacterAvatar>("PreviewArea/AvatarAnchor/CharacterAvatar");
		BindAction("Controls/ActionButtons/Idle", CharacterAvatar.AvatarAction.Idle);
		BindAction("Controls/ActionButtons/Score", CharacterAvatar.AvatarAction.Score);
		BindAction("Controls/ActionButtons/Attack", CharacterAvatar.AvatarAction.Attack);
		BindAction("Controls/ActionButtons/Hit", CharacterAvatar.AvatarAction.Hit);
		BindAction("Controls/ActionButtons/Victory", CharacterAvatar.AvatarAction.Victory);
		BindAction("Controls/ActionButtons/Defeat", CharacterAvatar.AvatarAction.Defeat);
	}

	private void BindAction(string buttonPath, CharacterAvatar.AvatarAction action)
	{
		Button button = GetNode<Button>(buttonPath);
		button.Pressed += () => _avatar.PlayAction(action);
	}
}
