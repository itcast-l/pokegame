using System;
using Godot;

public partial class MainMenuScene : Control
{
	private const float PlayerCountDialogHideSeconds = 0.16f;
	private const float PlayerCountDialogBlurLod = 2.8f;
	private const int DefaultMenuWinningScore = 100;
	private static readonly int[] MenuWinningScoreOptions = { 50, 100, 200 };
	private const string PlayerCountDialogScrimShaderCode = """
shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_lod = 2.8;
uniform vec4 tint_color : source_color = vec4(0.0, 0.035, 0.07, 0.48);

void fragment() {
	vec4 blurred = textureLod(screen_texture, SCREEN_UV, blur_lod);
	vec3 tinted = mix(blurred.rgb, tint_color.rgb, tint_color.a);
	COLOR = vec4(tinted, 1.0);
}
""";
	private static readonly Color PlayerCountDialogScrimTint = new(0.0f, 0.035f, 0.07f, 0.48f);

	private void BuildPlayerCountDialog()
	{
		_playerCountDialogRoot = new Control
		{
			Name = "PlayerCountDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_playerCountDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_playerCountDialogRoot);

		var scrim = CreatePlayerCountDialogScrim();
		scrim.SetAnchorsPreset(LayoutPreset.FullRect);
		scrim.GuiInput += inputEvent =>
		{
			if (inputEvent is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
			{
				HidePlayerCountDialog();
				GetViewport().SetInputAsHandled();
			}
		};
		_playerCountDialogRoot.AddChild(scrim);

		_playerCountDialogMotion = new Control
		{
			Name = "DialogMotion",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_playerCountDialogMotion.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerCountDialogRoot.AddChild(_playerCountDialogMotion);

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerCountDialogMotion.AddChild(center);

		_playerCountDialogPanel = new PanelContainer
		{
			Name = "PlayerCountDialogPanel",
			CustomMinimumSize = new Vector2(560.0f, 500.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_playerCountDialogPanel.AddThemeStyleboxOverride("panel", CreateDialogPanelStyle());
		center.AddChild(_playerCountDialogPanel);

		var margin = new MarginContainer
		{
			Name = "DialogMargin",
		};
		margin.AddThemeConstantOverride("margin_left", 32);
		margin.AddThemeConstantOverride("margin_top", 26);
		margin.AddThemeConstantOverride("margin_right", 32);
		margin.AddThemeConstantOverride("margin_bottom", 26);
		_playerCountDialogPanel.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "DialogLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		layout.AddThemeConstantOverride("separation", 12);
		margin.AddChild(layout);

		var pearl = new Control
		{
			Name = "PearlDivider",
			CustomMinimumSize = new Vector2(0.0f, 20.0f),
		};
		pearl.Draw += () => DrawPearlDivider(pearl);
		layout.AddChild(pearl);

		var title = CreateLocalizedDialogLabel("PLAYER_COUNT_TITLE", 30, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateLocalizedDialogLabel("PLAYER_COUNT_MESSAGE", 18, new Color(0.66f, 0.96f, 0.92f, 0.9f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 6.0f),
		};
		layout.AddChild(spacer);

		var optionsList = new VBoxContainer
		{
			Name = "DialogOptions",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		optionsList.AddThemeConstantOverride("separation", 10);
		layout.AddChild(optionsList);

		_playerCountOption = CreatePlayerCountDropdown(optionsList);
		_itemModeOption = CreateItemModeDropdown(optionsList);
		_winningScoreOption = CreateWinningScoreDropdown(optionsList);

		var actionSpacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 8.0f),
		};
		layout.AddChild(actionSpacer);

		layout.AddChild(CreateLocalizedDialogButton("PLAYER_COUNT_START_GAME", new Color(0.58f, 1.0f, 0.92f), StartGameWithSelectedOptions, false, 230.0f));
	}

	private static ColorRect CreatePlayerCountDialogScrim()
	{
		var shader = new Shader
		{
			Code = PlayerCountDialogScrimShaderCode,
		};
		var material = new ShaderMaterial
		{
			Shader = shader,
		};
		material.SetShaderParameter("blur_lod", PlayerCountDialogBlurLod);
		material.SetShaderParameter("tint_color", PlayerCountDialogScrimTint);

		return new ColorRect
		{
			Name = "Scrim",
			Color = Colors.White,
			Material = material,
			MouseFilter = MouseFilterEnum.Stop,
		};
	}

	private OptionButton CreatePlayerCountDropdown(Container parent)
	{
		var option = CreatePlayerCountDialogOption(parent, "PLAYER_COUNT_PLAYERS");
		option.Name = "PlayerCountOption";
		for (int playerCount = MinMenuPlayers; playerCount <= MaxMenuPlayers; playerCount++)
		{
			option.AddItem(GetPlayerCountOptionText(playerCount), playerCount);
		}

		option.Select(0);
		return option;
	}

	private OptionButton CreateItemModeDropdown(Container parent)
	{
		var option = CreatePlayerCountDialogOption(parent, "PLAYER_COUNT_ITEM_MODE");
		option.Name = "ItemModeOption";
		option.AddItem(Localize("PLAYER_COUNT_ITEM_MODE_ON"), 1);
		option.AddItem(Localize("PLAYER_COUNT_ITEM_MODE_OFF"), 0);
		option.Select(DefaultItemMode ? 0 : 1);
		return option;
	}

	private OptionButton CreateWinningScoreDropdown(Container parent)
	{
		var option = CreatePlayerCountDialogOption(parent, "PLAYER_COUNT_WINNING_SCORE");
		option.Name = "WinningScoreOption";
		for (int index = 0; index < MenuWinningScoreOptions.Length; index++)
		{
			int winningScore = MenuWinningScoreOptions[index];
			option.AddItem(GetWinningScoreOptionText(winningScore), winningScore);
			if (winningScore == DefaultMenuWinningScore)
			{
				option.Select(index);
			}
		}

		return option;
	}

	private OptionButton CreatePlayerCountDialogOption(Container parent, string labelKey)
	{
		var row = CreatePlayerCountDialogOptionRow(parent, labelKey);
		var spacer = new Control
		{
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddChild(spacer);

		var option = new OptionButton
		{
			CustomMinimumSize = new Vector2(210.0f, 42.0f),
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		StyleDropdownOptionButton(option);
		row.AddChild(option);
		return option;
	}

	private HBoxContainer CreatePlayerCountDialogOptionRow(Container parent, string labelKey)
	{
		var panel = new PanelContainer
		{
			Name = $"{labelKey}RowPanel",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		panel.AddThemeStyleboxOverride("panel", CreateSettingsRowStyle());
		parent.AddChild(panel);

		var margin = new MarginContainer
		{
			Name = "RowMargin",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 8);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 8);
		panel.AddChild(margin);

		var row = new HBoxContainer
		{
			Name = "RowLayout",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		row.AddThemeConstantOverride("separation", 12);
		margin.AddChild(row);

		var label = CreateLocalizedDialogLabel(labelKey, 18, new Color(0.88f, 1.0f, 0.96f, 0.96f), HorizontalAlignment.Left);
		label.CustomMinimumSize = new Vector2(116.0f, 40.0f);
		label.VerticalAlignment = VerticalAlignment.Center;
		row.AddChild(label);

		return row;
	}

	private void RefreshPlayerCountDialogOptions()
	{
		if (_playerCountOption != null)
		{
			for (int index = 0; index < _playerCountOption.ItemCount; index++)
			{
				int playerCount = _playerCountOption.GetItemId(index);
				_playerCountOption.SetItemText(index, GetPlayerCountOptionText(playerCount));
			}
		}

		if (_itemModeOption != null)
		{
			_itemModeOption.SetItemText(0, Localize("PLAYER_COUNT_ITEM_MODE_ON"));
			_itemModeOption.SetItemText(1, Localize("PLAYER_COUNT_ITEM_MODE_OFF"));
		}

		if (_winningScoreOption != null)
		{
			for (int index = 0; index < _winningScoreOption.ItemCount; index++)
			{
				int winningScore = _winningScoreOption.GetItemId(index);
				_winningScoreOption.SetItemText(index, GetWinningScoreOptionText(winningScore));
			}
		}
	}

	private string GetPlayerCountOptionText(int playerCount)
	{
		return playerCount switch
		{
			2 => Localize("PLAYER_COUNT_2_TITLE"),
			3 => Localize("PLAYER_COUNT_3_TITLE"),
			4 => Localize("PLAYER_COUNT_4_TITLE"),
			_ => playerCount.ToString(),
		};
	}

	private string GetWinningScoreOptionText(int winningScore)
	{
		return LocalizeFormat("PLAYER_COUNT_WINNING_SCORE_OPTION", winningScore);
	}

	private void ShowPlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		_playerCountDialogRoot.Visible = true;
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_playerCountDialogRoot.Modulate = Colors.White;
		UpdatePlayerCountDialogPanelPosition();
		_playerCountDialogMotion.Position = Vector2.Zero;
		_playerCountDialogMotion.Scale = Vector2.One;
		_playerCountDialogPanel.PivotOffset = _playerCountDialogPanel.Size * 0.5f;
	}

	private void HidePlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		_avatarEntry.Visible = true;
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Ignore;
		ResetMenuCamera();

		_playerCountDialogTween = CreateTween();
		_playerCountDialogTween.TweenProperty(_playerCountDialogRoot, "modulate:a", 0.0f, PlayerCountDialogHideSeconds);
		_playerCountDialogTween.Finished += () => _playerCountDialogRoot.Visible = false;
	}

	private void UpdatePlayerCountDialogPanelPosition()
	{
		if (_playerCountDialogRoot != null && _playerCountDialogRoot.Visible)
		{
			_playerCountDialogRoot.Position = GetSecondaryMenuDepthPosition();
			_playerCountDialogRoot.Scale = GetSecondaryMenuWorldScale();
		}
	}

	private void StartGameWithSelectedOptions()
	{
		int playerCount = _playerCountOption.GetSelectedId();
		bool itemMode = _itemModeOption.GetSelectedId() == 1;
		int winningScore = _winningScoreOption.GetSelectedId();
		StartGameWithOptions(playerCount, itemMode, winningScore);
	}

	private void StartGameWithOptions(int playerCount, bool itemMode, int winningScore)
	{
		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", playerCount);
		gameBoard.Set("item_mode", itemMode);
		gameBoard.Set("winning_score", winningScore);
		gameBoard.Set("solo_mode", true);
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		QueueFree();
	}
}
