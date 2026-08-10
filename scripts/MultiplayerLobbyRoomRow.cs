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
		MainMenuInteractiveStyle.ApplyThemedControl(JoinButton, MainMenuInteractiveStyle.SecondaryMenuPalette, 15);
		Font primaryFont = LocalizationTypography.CreateCurrentFontVariation(620.0f, 0.50f);
		Font supportingFont = LocalizationTypography.CreateCurrentFontVariation(550.0f, 0.34f);
		ApplyReadableTextStyle(RoomNameLabel, primaryFont);
		ApplyReadableTextStyle(DetailsLabel, supportingFont);
		ApplyReadableTextStyle(StatusLabel, primaryFont);
		ApplyReadableTextStyle(JoinButton, primaryFont);
		JoinButton.ClipText = true;
		JoinButton.Pressed += OnJoinButtonPressed;
	}

	private static void ApplyReadableTextStyle(Control control, Font font)
	{
		control.AddThemeFontOverride("font", font);
		control.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.025f, 0.045f, 0.94f));
		control.AddThemeConstantOverride("outline_size", 1);
	}

	public void Configure(Godot.Collections.Dictionary room)
	{
		_room = room.Duplicate();
		ApplyRoomData();
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

	private void ApplyRoomData()
	{
		string roomName = ReadDictionaryString(_room, "room_name", ReadDictionaryString(_room, "host_name", string.Empty));
		string hostName = ReadDictionaryString(_room, "host_name", string.Empty);
		string address = ReadDictionaryString(_room, "address", string.Empty);
		string transport = ReadDictionaryString(_room, "transport", NetworkTransport.Lan.ToString());
		string status = ReadDictionaryString(_room, "status", NetworkSessionState.Idle.ToString());
		int playerCount = ReadDictionaryInt(_room, "player_count", NetworkSessionDefaults.DefaultPlayerCount);
		int winningScore = ReadDictionaryInt(_room, "winning_score", NetworkSessionDefaults.DefaultWinningScore);
		bool itemMode = ReadDictionaryBool(_room, "item_mode", NetworkSessionDefaults.DefaultItemMode);
		bool hasPassword = ReadDictionaryBool(_room, "has_password", false);
		string privacy = hasPassword ? Localize("MULTIPLAYER_LOBBY_PRIVACY_PRIVATE") : Localize("MULTIPLAYER_LOBBY_PRIVACY_PUBLIC");
		string itemModeText = itemMode ? Localize("PLAYER_COUNT_ITEM_MODE_ON") : Localize("PLAYER_COUNT_ITEM_MODE_OFF");
		string playerCountText = GetPlayerCountText(playerCount);
		string winningScoreText = string.Format(Localize("PLAYER_COUNT_WINNING_SCORE_OPTION"), winningScore);
		string localizedStatus = GetNetworkStateText(status);

		RoomNameLabel.Text = roomName;
		DetailsLabel.Text = LocalizeFormat("MULTIPLAYER_LOBBY_DETAILS_FORMAT", GetTransportDisplayText(transport), playerCountText, itemModeText, winningScoreText, privacy);
		bool canJoin = MultiplayerLobbyRules.CanJoinListedRoom(status);
		StatusLabel.Text = localizedStatus;
		StatusLabel.Visible = true;
		StatusLabel.AddThemeColorOverride(
			"font_color",
			canJoin ? MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder : MainMenuInteractiveStyle.DangerPalette.Text);
		JoinButton.Text = Localize("MULTIPLAYER_LOBBY_JOIN");
		JoinButton.Disabled = !canJoin;
		AddThemeStyleboxOverride("panel", CreateRowStyle(canJoin));

		var tooltipParts = new System.Collections.Generic.List<string>();
		if (!string.IsNullOrWhiteSpace(hostName))
		{
			tooltipParts.Add(hostName);
		}

		if (!string.IsNullOrWhiteSpace(address))
		{
			tooltipParts.Add(address);
		}

		if (!string.IsNullOrWhiteSpace(localizedStatus))
		{
			tooltipParts.Add(localizedStatus);
		}

		if (!string.IsNullOrWhiteSpace(privacy))
		{
			tooltipParts.Add(privacy);
		}

		string tooltipText = string.Join(" / ", tooltipParts);
		TooltipText = tooltipText;
	}

	private static StyleBoxFlat CreateRowStyle(bool canJoin)
	{
		Color border = canJoin
			? MainMenuInteractiveStyle.SecondaryMenuPalette.Border
			: new Color(0.48f, 0.52f, 0.54f, 0.32f);
		return new StyleBoxFlat
		{
			BgColor = new Color(0.11f, 0.15f, 0.17f, 0.78f),
			BorderColor = border,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 6,
			CornerRadiusTopRight = 6,
			CornerRadiusBottomLeft = 6,
			CornerRadiusBottomRight = 6,
			ContentMarginLeft = 10.0f,
			ContentMarginTop = 7.0f,
			ContentMarginRight = 10.0f,
			ContentMarginBottom = 7.0f,
		};
	}

	private string Localize(string key)
	{
		string text = Tr(key).ToString();
		return text == key ? key : text;
	}

	private string LocalizeFormat(string key, params object[] args)
	{
		return string.Format(Localize(key), args);
	}

	private string GetPlayerCountText(int playerCount)
	{
		return playerCount switch
		{
			2 => Localize("PLAYER_COUNT_2_TITLE"),
			3 => Localize("PLAYER_COUNT_3_TITLE"),
			4 => Localize("PLAYER_COUNT_4_TITLE"),
			_ => playerCount.ToString(),
		};
	}

	private string GetNetworkStateText(string rawState)
	{
		if (System.Enum.TryParse(rawState, ignoreCase: true, out NetworkSessionState state))
		{
			string key = state switch
			{
				NetworkSessionState.Idle => "NETWORK_STATE_IDLE",
				NetworkSessionState.Searching => "NETWORK_STATE_SEARCHING",
				NetworkSessionState.Hosting => "NETWORK_STATE_HOSTING",
				NetworkSessionState.Joining => "NETWORK_STATE_JOINING",
				NetworkSessionState.Lobby => "NETWORK_STATE_LOBBY",
				NetworkSessionState.Starting => "NETWORK_STATE_STARTING",
				NetworkSessionState.LoadingScene => "NETWORK_STATE_LOADING_SCENE",
				NetworkSessionState.InGame => "NETWORK_STATE_IN_GAME",
				NetworkSessionState.Reconnecting => "NETWORK_STATE_RECONNECTING",
				NetworkSessionState.MigratingHost => "NETWORK_STATE_MIGRATING_HOST",
				NetworkSessionState.Leaving => "NETWORK_STATE_LEAVING",
				NetworkSessionState.Failed => "NETWORK_STATE_FAILED",
				_ => "NETWORK_STATE_IDLE",
			};
			return Localize(key);
		}

		return rawState;
	}

	private static string GetTransportDisplayText(string rawTransport)
	{
		return rawTransport switch
		{
			"Lan" => "LAN",
			"Steam" => "Steam",
			_ => rawTransport,
		};
	}
}
