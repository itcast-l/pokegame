using Godot;

public partial class MainMenuAvatarEntry : Control
{
	[Signal]
	public delegate void AvatarPressedEventHandler();

	private CharacterAvatarDefinition _avatarDefinition = CharacterAvatarCatalog.Default;
	private CharacterAvatar _avatar = default!;
	private Button _button = default!;

	public Button Button => _button;

	public override void _Ready()
	{
		_avatar = GetNode<CharacterAvatar>("Frame/Margin/Layout/AvatarAnchor/CharacterAvatar");
		_button = GetNode<Button>("Button");
		_button.FocusMode = Control.FocusModeEnum.All;
		_button.Pressed += OnPressed;
		ApplyAvatarDefinition();
	}

	public void SetAvatar(CharacterAvatarDefinition avatarDefinition)
	{
		_avatarDefinition = avatarDefinition;
		if (IsNodeReady())
		{
			ApplyAvatarDefinition();
		}
	}

	private void ApplyAvatarDefinition()
	{
		_avatar.AvatarRoot = _avatarDefinition.RootPath;
		_avatar.AudioConfig = _avatarDefinition.AudioConfig;
	}

	private void OnPressed()
	{
		_avatar.PlayScore();
		EmitSignal(SignalName.AvatarPressed);
	}
}
