using Godot;

public partial class MainMenuAvatarEntry : Control
{
	[Signal]
	public delegate void AvatarPressedEventHandler();

	private CharacterAvatarDefinition _avatarDefinition = CharacterAvatarCatalog.Default;
	private string _playerName = PlayerNameRules.DefaultPlayerName;
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
		ApplyPlayerName();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationTranslationChanged && IsNodeReady())
		{
			ApplyPlayerName();
		}
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
		_button.TooltipText = string.Format(Localize("MAIN_MENU_AVATAR_TOOLTIP"), _playerName);
	}

	private void OnPressed()
	{
		_avatar.PlayScore();
		EmitSignal(SignalName.AvatarPressed);
	}

	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? key : text;
	}
}
