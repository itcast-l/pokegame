using Godot;

public partial class MultiplayerLobbyPlayerRow : PanelContainer
{
	[Export]
	public Label SeatLabel { get; set; } = default!;

	[Export]
	public Label PlayerNameLabel { get; set; } = default!;

	[Export]
	public Label RoleLabel { get; set; } = default!;

	[Export]
	public Label ReadyLabel { get; set; } = default!;

	private Godot.Collections.Dictionary _player = new();

	public void Configure(Godot.Collections.Dictionary player)
	{
		_player = player.Duplicate();
		ApplyPlayerData();
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

	private void ApplyPlayerData()
	{
		int seatIndex = ReadDictionaryInt(_player, "seat_index", 0);
		string displayName = ReadDictionaryString(_player, "display_name", string.Empty);
		bool isHost = ReadDictionaryBool(_player, "is_host", false);
		bool isLocal = ReadDictionaryBool(_player, "is_local", false);
		bool isReady = ReadDictionaryBool(_player, "is_ready", false);
		bool isConnected = ReadDictionaryBool(_player, "is_connected", true);

		SeatLabel.Text = (seatIndex + 1).ToString();
		PlayerNameLabel.Text = isLocal
			? $"{displayName} ({Localize("MULTIPLAYER_LOBBY_LOCAL_PLAYER")})"
			: displayName;
		RoleLabel.Text = isHost ? Localize("MULTIPLAYER_LOBBY_ROLE_HOST") : Localize("MULTIPLAYER_LOBBY_ROLE_PEER");
		ReadyLabel.Text = !isConnected
			? Localize("MULTIPLAYER_LOBBY_PLAYER_OFFLINE")
			: isReady
				? Localize("MULTIPLAYER_LOBBY_PLAYER_READY")
				: Localize("MULTIPLAYER_LOBBY_PLAYER_WAITING");
	}

	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? key : text;
	}
}
