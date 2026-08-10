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

	public override void _Ready()
	{
		SeatLabel.AddThemeColorOverride("font_color", MainMenuInteractiveStyle.SpadePalette.ActiveBorder);
		PlayerNameLabel.AddThemeColorOverride("font_color", new Color(0.91f, 0.95f, 0.96f, 1.0f));
		Font primaryFont = LocalizationTypography.CreateCurrentFontVariation(620.0f, 0.50f);
		Font supportingFont = LocalizationTypography.CreateCurrentFontVariation(570.0f, 0.38f);
		ApplyReadableTextStyle(SeatLabel, supportingFont);
		ApplyReadableTextStyle(PlayerNameLabel, primaryFont);
		ApplyReadableTextStyle(RoleLabel, supportingFont);
		ApplyReadableTextStyle(ReadyLabel, supportingFont);
		AddThemeStyleboxOverride("panel", CreateRowStyle(new Color(0.48f, 0.56f, 0.60f, 0.34f)));
	}

	private static void ApplyReadableTextStyle(Control control, Font font)
	{
		control.AddThemeFontOverride("font", font);
		control.AddThemeColorOverride("font_outline_color", new Color(0.0f, 0.025f, 0.045f, 0.94f));
		control.AddThemeConstantOverride("outline_size", 1);
	}

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
		RoleLabel.AddThemeColorOverride(
			"font_color",
			isHost ? MainMenuInteractiveStyle.SecondaryMenuPalette.ActiveBorder : MainMenuInteractiveStyle.SpadePalette.Text);
		ReadyLabel.AddThemeColorOverride(
			"font_color",
			!isConnected
				? MainMenuInteractiveStyle.DangerPalette.Text
				: isReady
					? MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.ActiveBorder
					: MainMenuInteractiveStyle.SpadePalette.Text);
		Color rowBorder = !isConnected
			? MainMenuInteractiveStyle.DangerPalette.Border
			: isReady
				? MainMenuInteractiveStyle.SecondaryMenuPrimaryPalette.Border
				: isLocal
					? MainMenuInteractiveStyle.SecondaryMenuPalette.Border
					: new Color(0.48f, 0.56f, 0.60f, 0.34f);
		AddThemeStyleboxOverride("panel", CreateRowStyle(rowBorder));
	}

	private static StyleBoxFlat CreateRowStyle(Color border)
	{
		return new StyleBoxFlat
		{
			BgColor = new Color(0.11f, 0.15f, 0.17f, 0.76f),
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
}
