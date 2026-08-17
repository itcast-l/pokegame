using Godot;

public partial class MainMenuItemCodexCard : Button
{
	private const float BaseCardWidth = 196.0f;
	private const int MaxDescriptionFontSize = 12;
	private const int MinDescriptionFontSize = 9;

	private PanelContainer _artFrame = default!;
	private TextureRect _artTexture = default!;
	private Label _titleLabel = default!;
	private Label _descriptionLabel = default!;
	private Panel _hoverBorder = default!;
	private Panel _focusBorder = default!;
	private bool _interactive;
	private bool _hovered;
	private bool _focusHighlightEnabled = true;
	private string _itemCardId = string.Empty;

	public string ItemCardId => _itemCardId;
	public bool IsInteractive => _interactive;
	public bool HasCardTexture => _artTexture?.Texture != null;
	public bool FocusHighlightEnabled => _focusHighlightEnabled;

	public override void _Ready()
	{
		_artFrame = GetNode<PanelContainer>("Content/ArtFrame");
		_artTexture = GetNode<TextureRect>("Content/ArtFrame/ArtMargin/ArtTexture");
		_titleLabel = GetNode<Label>("Content/TitleLabel");
		_descriptionLabel = GetNode<Label>("Content/DescriptionPanel/DescriptionMargin/DescriptionLabel");
		_hoverBorder = GetNode<Panel>("HoverBorder");
		_focusBorder = GetNode<Panel>("FocusBorder");

		MouseEntered += OnMouseEntered;
		MouseExited += OnMouseExited;
		FocusEntered += RefreshFeedback;
		FocusExited += RefreshFeedback;
		Resized += RefreshTypography;
		RefreshFeedback();
	}

	public void ConfigureCard(
		string itemCardId,
		Texture2D? texture,
		string title,
		string description,
		bool interactive)
	{
		_itemCardId = itemCardId?.Trim() ?? string.Empty;
		_artTexture.Texture = texture;
		_titleLabel.Text = title?.Trim() ?? string.Empty;
		_descriptionLabel.Text = description?.Trim() ?? string.Empty;
		_interactive = interactive;
		_hovered = false;
		FocusMode = interactive ? FocusModeEnum.All : FocusModeEnum.None;
		MouseFilter = interactive ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
		ButtonMask = interactive ? MouseButtonMask.Left : (MouseButtonMask)0;
		MouseDefaultCursorShape = interactive ? CursorShape.PointingHand : CursorShape.Arrow;
		ActionMode = ActionModeEnum.Release;
		Disabled = !interactive;
		RefreshTypography();
		RefreshFeedback();
	}

	public void SetFocusHighlightEnabled(bool enabled)
	{
		_focusHighlightEnabled = enabled;
		RefreshFeedback();
	}

#if DECKLAND_TEST_HOOKS
	public Godot.Collections.Dictionary DebugGetCardState()
	{
		return new Godot.Collections.Dictionary
		{
			["id"] = _itemCardId,
			["title"] = _titleLabel.Text,
			["description"] = _descriptionLabel.Text,
			["description_font_size"] = _descriptionLabel.GetThemeFontSize("font_size"),
			["interactive"] = _interactive,
			["has_texture"] = HasCardTexture,
			["card_texture_path"] = _artTexture.Texture?.ResourcePath ?? string.Empty,
			["feedback_visible"] = _hoverBorder.Visible || _focusBorder.Visible,
			["hover_feedback_visible"] = _hoverBorder.Visible,
			["focus_feedback_visible"] = _focusBorder.Visible,
			["focus_highlight_enabled"] = _focusHighlightEnabled,
		};
	}
#endif

	private void OnMouseEntered()
	{
		_hovered = true;
		RefreshFeedback();
	}

	private void OnMouseExited()
	{
		_hovered = false;
		RefreshFeedback();
	}

	private void RefreshFeedback()
	{
		if (_hoverBorder == null || _focusBorder == null)
		{
			return;
		}

		bool showFocus = _interactive && _focusHighlightEnabled && HasFocus();
		_focusBorder.Visible = showFocus;
		_hoverBorder.Visible = _interactive && _hovered && !showFocus;
	}

	private void RefreshTypography()
	{
		if (_titleLabel == null || _descriptionLabel == null)
		{
			return;
		}

		float widthScale = Mathf.Max(0.75f, Size.X / BaseCardWidth);
		_artFrame.CustomMinimumSize = new Vector2(0.0f, Mathf.Round(168.0f * widthScale));
		_titleLabel.CustomMinimumSize = new Vector2(0.0f, Mathf.Round(34.0f * widthScale));
		int titleLength = Mathf.Max(1, _titleLabel.Text.Length);
		int titleBaseSize = titleLength switch
		{
			> 28 => 12,
			> 20 => 14,
			_ => 16,
		};
		_titleLabel.AddThemeFontSizeOverride(
			"font_size",
			Mathf.Max(11, Mathf.RoundToInt(titleBaseSize * widthScale)));

		int effectiveLength = Mathf.CeilToInt(_descriptionLabel.Text.Length / widthScale);
		int descriptionBaseSize = effectiveLength switch
		{
			> 150 => MinDescriptionFontSize,
			> 105 => 10,
			> 72 => 11,
			_ => MaxDescriptionFontSize,
		};
		_descriptionLabel.AddThemeFontSizeOverride(
			"font_size",
			Mathf.Max(MinDescriptionFontSize, Mathf.RoundToInt(descriptionBaseSize * widthScale)));
	}
}
