using Godot;

public partial class MultiplayerLobbyRoomRow : PanelContainer
{
	[Signal]
	public delegate void JoinRequestedEventHandler(Godot.Collections.Dictionary room);

	[Export]
	public Label RoomNameLabel { get; set; } = default!;

	[Export]
	public Label DetailsLabel { get; set; } = default!;

	[Export]
	public Label StatusLabel { get; set; } = default!;

	[Export]
	public Button JoinButton { get; set; } = default!;

	private Godot.Collections.Dictionary _room = new();

	public override void _Ready()
	{
		JoinButton.Pressed += OnJoinButtonPressed;
	}

	public void Configure(Godot.Collections.Dictionary room)
	{
		_room = room.Duplicate();
		string roomName = ReadDictionaryString(_room, "room_name", ReadDictionaryString(_room, "host_name", "房间"));
		string hostName = ReadDictionaryString(_room, "host_name", "Host");
		string address = ReadDictionaryString(_room, "address", string.Empty);
		string transport = ReadDictionaryString(_room, "transport", NetworkTransport.Lan.ToString());
		string status = ReadDictionaryString(_room, "status", NetworkSessionState.Idle.ToString());
		int playerCount = ReadDictionaryInt(_room, "player_count", NetworkSessionDefaults.DefaultPlayerCount);
		int winningScore = ReadDictionaryInt(_room, "winning_score", NetworkSessionDefaults.DefaultWinningScore);
		bool itemMode = ReadDictionaryBool(_room, "item_mode", NetworkSessionDefaults.DefaultItemMode);
		bool hasPassword = ReadDictionaryBool(_room, "has_password", false);
		string privacy = hasPassword ? "加锁" : "公开";
		string itemModeText = itemMode ? "道具开启" : "道具关闭";

		RoomNameLabel.Text = roomName;
		DetailsLabel.Text = $"{transport} / {playerCount}人 / {itemModeText} / {winningScore}分 / {privacy}";
		StatusLabel.Text = status;
		JoinButton.Disabled = !MultiplayerLobbyRules.CanJoinListedRoom(status);
		JoinButton.TooltipText = $"{hostName} / {address} / {status} / {privacy}";
		TooltipText = JoinButton.TooltipText;
	}

	private void OnJoinButtonPressed()
	{
		EmitSignal(SignalName.JoinRequested, _room);
	}

	private static string ReadDictionaryString(Godot.Collections.Dictionary dictionary, string key, string fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) && !string.IsNullOrWhiteSpace(value.AsString())
			? value.AsString()
			: fallback;
	}

	private static int ReadDictionaryInt(Godot.Collections.Dictionary dictionary, string key, int fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsInt32() : fallback;
	}

	private static bool ReadDictionaryBool(Godot.Collections.Dictionary dictionary, string key, bool fallback)
	{
		return dictionary.TryGetValue(key, out Variant value) ? value.AsBool() : fallback;
	}
}
