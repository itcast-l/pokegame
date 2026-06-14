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

	public void Configure(Godot.Collections.Dictionary player)
	{
		int seatIndex = ReadDictionaryInt(player, "seat_index", 0);
		string displayName = ReadDictionaryString(player, "display_name", "Player");
		bool isHost = ReadDictionaryBool(player, "is_host", false);
		bool isLocal = ReadDictionaryBool(player, "is_local", false);
		bool isReady = ReadDictionaryBool(player, "is_ready", false);
		bool isConnected = ReadDictionaryBool(player, "is_connected", true);

		SeatLabel.Text = (seatIndex + 1).ToString();
		PlayerNameLabel.Text = isLocal ? $"{displayName} (你)" : displayName;
		RoleLabel.Text = isHost ? "Host" : "Peer";
		ReadyLabel.Text = !isConnected ? "离线" : isReady ? "Ready" : "Waiting";
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
