using Godot;

public partial class MainMenuAvatarEntry : Control
{
	[Signal]
	public delegate void AvatarPressedEventHandler();

	private CharacterAvatarDefinition _avatarDefinition = CharacterAvatarCatalog.Default;
	private string _playerName = PlayerNameRules.DefaultPlayerName;
	private CharacterAvatar _avatar = default!;
	private Label _nameLabel = default!;
	private Button _button = default!;

	public Button Button => _button;

	public override void _Ready()
	{
		_avatar = GetNode<CharacterAvatar>("Frame/Margin/Layout/AvatarAnchor/CharacterAvatar");
		_nameLabel = GetNode<Label>("Frame/Margin/Layout/NameLabel");
		_button = GetNode<Button>("Button");
		_button.FocusMode = Control.FocusModeEnum.All;
		_button.Pressed += OnPressed;
		ApplyAvatarDefinition();
		ApplyPlayerName();
	}

	public void SetAvatar(CharacterAvatarDefinition avatarDefinition)
	{
		_avatarDefinition = avatarDefinition;
		if (IsNodeReady())
		{
			ApplyAvatarDefinition();
		}
	}

	public void SetPlayerName(string playerName)
	{
		_playerName = PlayerNameRules.Normalize(playerName);
		if (IsNodeReady())
		{
			ApplyPlayerName();
		}
	}

	private void ApplyAvatarDefinition()
	{
		_avatar.AvatarRoot = _avatarDefinition.RootPath;
		_avatar.AudioConfig = _avatarDefinition.AudioConfig;
	}

	private void ApplyPlayerName()
	{
		_nameLabel.Text = _playerName;
		_button.TooltipText = $"修改头像和名字：{_playerName}";
	}

	private void OnPressed()
	{
		_avatar.PlayScore();
		EmitSignal(SignalName.AvatarPressed);
	}
}
