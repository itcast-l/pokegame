using System;
using Godot;

public partial class MainMenuScene : Control
{
	private void BuildPlayerCountDialog()
	{
		_playerCountDialogLayer = new CanvasLayer
		{
			Name = "PlayerCountDialogLayer",
			Layer = 35,
		};
		AddChild(_playerCountDialogLayer);

		_playerCountDialogRoot = new Control
		{
			Name = "PlayerCountDialogRoot",
			Visible = false,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		_playerCountDialogRoot.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerCountDialogLayer.AddChild(_playerCountDialogRoot);

		var scrim = new ColorRect
		{
			Name = "Scrim",
			Color = new Color(0.0f, 0.035f, 0.07f, 0.54f),
			MouseFilter = MouseFilterEnum.Stop,
		};
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

		var center = new CenterContainer
		{
			Name = "DialogCenter",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		center.SetAnchorsPreset(LayoutPreset.FullRect);
		_playerCountDialogRoot.AddChild(center);

		_playerCountDialogPanel = new PanelContainer
		{
			Name = "PlayerCountDialogPanel",
			CustomMinimumSize = new Vector2(660.0f, 360.0f),
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

		var title = CreateDialogLabel("选择游戏人数", 30, new Color(0.9f, 1.0f, 0.96f), HorizontalAlignment.Center);
		layout.AddChild(title);

		var message = CreateDialogLabel("选择本局参与人数", 18, new Color(0.66f, 0.96f, 0.92f, 0.9f), HorizontalAlignment.Center);
		layout.AddChild(message);

		var spacer = new Control
		{
			CustomMinimumSize = new Vector2(0.0f, 6.0f),
		};
		layout.AddChild(spacer);

		var actions = new HBoxContainer
		{
			Name = "DialogActions",
			Alignment = BoxContainer.AlignmentMode.Center,
		};
		actions.AddThemeConstantOverride("separation", 16);
		layout.AddChild(actions);

		actions.AddChild(CreatePlayerCountOption(2, "双人对局", "40张牌", new Color(0.58f, 1.0f, 0.92f)));
		actions.AddChild(CreatePlayerCountOption(3, "三人对局", "80张牌", new Color(0.95f, 0.86f, 0.52f)));
		actions.AddChild(CreatePlayerCountOption(4, "四人对局", "80张牌", new Color(0.58f, 0.82f, 1.0f)));
	}

	private Control CreatePlayerCountOption(int playerCount, string title, string deckText, Color accentColor)
	{
		var card = new PanelContainer
		{
			Name = $"{playerCount}PlayerOption",
			CustomMinimumSize = new Vector2(178.0f, 104.0f),
			MouseFilter = MouseFilterEnum.Stop,
		};
		card.AddThemeStyleboxOverride("panel", CreatePlayerCountOptionStyle(accentColor, false));

		var margin = new MarginContainer
		{
			Name = "OptionMargin",
		};
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 14);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 14);
		card.AddChild(margin);

		var layout = new VBoxContainer
		{
			Name = "OptionLayout",
			Alignment = BoxContainer.AlignmentMode.Center,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		layout.AddThemeConstantOverride("separation", 8);
		margin.AddChild(layout);

		var titleLabel = CreateDialogLabel(title, 24, new Color(0.92f, 1.0f, 0.97f), HorizontalAlignment.Center);
		titleLabel.VerticalAlignment = VerticalAlignment.Center;
		layout.AddChild(titleLabel);

		var deckBadge = new PanelContainer
		{
			Name = "DeckBadge",
			MouseFilter = MouseFilterEnum.Ignore,
		};
		deckBadge.AddThemeStyleboxOverride("panel", CreatePlayerCountBadgeStyle(accentColor));
		layout.AddChild(deckBadge);

		var deckBadgeMargin = new MarginContainer
		{
			Name = "DeckBadgeMargin",
		};
		deckBadgeMargin.AddThemeConstantOverride("margin_left", 14);
		deckBadgeMargin.AddThemeConstantOverride("margin_top", 3);
		deckBadgeMargin.AddThemeConstantOverride("margin_right", 14);
		deckBadgeMargin.AddThemeConstantOverride("margin_bottom", 3);
		deckBadge.AddChild(deckBadgeMargin);

		var deckLabel = CreateDialogLabel(deckText, 15, new Color(0.88f, 1.0f, 0.96f, 0.95f), HorizontalAlignment.Center);
		deckBadgeMargin.AddChild(deckLabel);

		var button = new Button
		{
			Name = "Button",
			Flat = true,
			FocusMode = FocusModeEnum.None,
			MouseFilter = MouseFilterEnum.Stop,
		};
		button.SetAnchorsPreset(LayoutPreset.FullRect);
		button.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
		button.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
		button.MouseEntered += () => card.AddThemeStyleboxOverride("panel", CreatePlayerCountOptionStyle(accentColor, true));
		button.MouseExited += () => card.AddThemeStyleboxOverride("panel", CreatePlayerCountOptionStyle(accentColor, false));
		button.Pressed += () => StartGameWithPlayerCount(playerCount);
		card.AddChild(button);

		return card;
	}

	private void ShowPlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		_playerCountDialogRoot.Visible = true;
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Stop;
		_playerCountDialogRoot.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		_playerCountDialogPanel.Scale = new Vector2(0.94f, 0.94f);
		_playerCountDialogPanel.PivotOffset = _playerCountDialogPanel.Size * 0.5f;

		_playerCountDialogTween = CreateTween();
		_playerCountDialogTween.SetParallel(true);
		_playerCountDialogTween.TweenProperty(_playerCountDialogRoot, "modulate:a", 1.0f, DialogFadeSeconds);
		_playerCountDialogTween.TweenProperty(_playerCountDialogPanel, "scale", Vector2.One, DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);
	}

	private void HidePlayerCountDialog()
	{
		_playerCountDialogTween?.Kill();
		_playerCountDialogRoot.MouseFilter = MouseFilterEnum.Ignore;

		_playerCountDialogTween = CreateTween();
		_playerCountDialogTween.SetParallel(true);
		_playerCountDialogTween.TweenProperty(_playerCountDialogRoot, "modulate:a", 0.0f, DialogFadeSeconds);
		_playerCountDialogTween.TweenProperty(_playerCountDialogPanel, "scale", new Vector2(0.96f, 0.96f), DialogFadeSeconds)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.In);
		_playerCountDialogTween.Finished += () => _playerCountDialogRoot.Visible = false;
	}

	private void StartGameWithPlayerCount(int playerCount)
	{
		PackedScene? gameBoardScene = GD.Load<PackedScene>(GameBoardScenePath);
		if (gameBoardScene == null)
		{
			GD.PushError($"Missing game board scene: {GameBoardScenePath}");
			return;
		}

		Node gameBoard = gameBoardScene.Instantiate();
		gameBoard.Set("player_count", playerCount);
		GetTree().Root.AddChild(gameBoard);
		GetTree().CurrentScene = gameBoard;
		QueueFree();
	}
}
