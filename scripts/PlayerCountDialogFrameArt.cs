using System.Collections.Generic;
using Godot;

public partial class PlayerCountDialogFrameArt : Control
{
	public enum FrameKind
	{
		RowSlot,
		ActionButton,
	}

	private const string SourceTexturePath = "res://assets/ui/player_count_dialog_interactive_panel.png";
	private static readonly Rect2 RowSlotRegion = new(124.0f, 142.0f, 496.0f, 56.0f);
	private static readonly Rect2 ActionButtonRegion = new(248.0f, 392.0f, 248.0f, 48.0f);

	[Export]
	public FrameKind Kind { get; set; } = FrameKind.ActionButton;

	[Export]
	public bool ReactToHover { get; set; } = true;

	[Export]
	public bool HighlightFocus { get; set; } = true;

	[Export]
	public Texture2D? SourceTextureOverride { get; set; }

	private Texture2D? _sourceTexture;
	private Button? _button;
	private readonly List<Control> _interactiveControls = new();
	private float _glow;
	[Export]
	public Texture2D? ActionButtonHoverTexture { get; set; }

	[Export]
	public Texture2D? RowSlotHoverTexture { get; set; }

	[Export]
	public Texture2D? AccentSparkTexture { get; set; }

	private readonly Vector2[] _buttonOuterPoints = new Vector2[6];
	private readonly Vector2[] _buttonInnerPoints = new Vector2[6];
	private readonly Vector2[] _buttonClosedOuterPoints = new Vector2[7];
	private readonly Vector2[] _buttonClosedInnerPoints = new Vector2[7];
	private readonly StyleBoxFlat _rowGlowStyle = new();
	private readonly StyleBoxFlat _rowRimStyle = new();
	private double _time;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_button = GetNodeOrNull<Button>("Button") ?? FindDirectButtonChild();
		if (_button == null)
		{
			CollectInteractiveDescendants(this);
		}
		_sourceTexture = SourceTextureOverride ?? GD.Load<Texture2D>(SourceTexturePath);

		SetProcess(true);
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		_time += delta;
		float targetGlow = 0.0f;
		if (ReactToHover)
		{
			if (_button != null)
			{
				bool hovered = _button.GetGlobalRect().HasPoint(GetGlobalMousePosition());
				targetGlow = hovered || (HighlightFocus && _button.HasFocus()) ? 1.0f : 0.0f;
				if (_button.ButtonPressed)
				{
					targetGlow = 0.62f;
				}
			}
			else
			{
				targetGlow = EvaluateDescendantInteraction();
			}
		}

		_glow = Mathf.Lerp(_glow, targetGlow, Mathf.Clamp((float)delta * 10.0f, 0.0f, 1.0f));
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_sourceTexture == null || Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		Rect2 rect = _button != null
			? new Rect2(_button.Position, _button.Size)
			: new Rect2(Vector2.Zero, Size);

		if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
		{
			return;
		}

		Rect2 sourceRegion = Kind == FrameKind.RowSlot ? RowSlotRegion : ActionButtonRegion;
		DrawTextureRectRegion(_sourceTexture, rect, sourceRegion, Colors.White);

		if (_glow <= 0.01f)
		{
			return;
		}

		if (Kind == FrameKind.ActionButton)
		{
			DrawActionButtonHoverGlow(rect);
			return;
		}

		DrawRowSlotHoverGlow(rect);
	}

	private Button? FindDirectButtonChild()
	{
		for (int index = 0; index < GetChildCount(); index++)
		{
			if (GetChild(index) is Button button)
			{
				return button;
			}
		}

		return null;
	}

	private void CollectInteractiveDescendants(Node node)
	{
		for (int index = 0; index < node.GetChildCount(); index++)
		{
			Node child = node.GetChild(index);
			if (child is Control control && control != this)
			{
				if (control is BaseButton || control is Godot.Range)
				{
					_interactiveControls.Add(control);
				}
			}

			CollectInteractiveDescendants(child);
		}
	}

	private float EvaluateDescendantInteraction()
	{
		if (_interactiveControls.Count == 0)
		{
			return 0.0f;
		}

		Vector2 mousePosition = GetGlobalMousePosition();
		bool hovered = false;
		bool focused = false;
		bool pressed = false;

		foreach (Control control in _interactiveControls)
		{
			if (!GodotObject.IsInstanceValid(control) || !control.IsVisibleInTree())
			{
				continue;
			}

			Rect2 rect = control.GetGlobalRect();
			hovered |= rect.HasPoint(mousePosition);
			focused |= HighlightFocus && control.HasFocus();

			if (control is BaseButton button && button.ButtonPressed)
			{
				pressed = true;
			}
		}

		if (pressed)
		{
			return 0.62f;
		}

		return hovered || focused ? 1.0f : 0.0f;
	}

	private void DrawActionButtonHoverGlow(Rect2 rect)
	{
		float w = rect.Size.X;
		float h = rect.Size.Y;
		float bevel = h * 0.34f;
		Vector2 origin = rect.Position;
		float pulse = 0.86f + 0.14f * Mathf.Sin((float)_time * 6.0f);
		float glow = _glow * pulse;

		if (ActionButtonHoverTexture != null)
		{
			DrawTextureRect(
				ActionButtonHoverTexture,
				rect.GrowIndividual(4.0f, 4.0f, 4.0f, 4.0f),
				false,
				new Color(1.0f, 1.0f, 1.0f, 0.35f * glow));
			return;
		}

		_buttonOuterPoints[0] = origin + new Vector2(bevel, 2.0f);
		_buttonOuterPoints[1] = origin + new Vector2(w - bevel, 2.0f);
		_buttonOuterPoints[2] = origin + new Vector2(w - 2.0f, h * 0.5f);
		_buttonOuterPoints[3] = origin + new Vector2(w - bevel, h - 2.0f);
		_buttonOuterPoints[4] = origin + new Vector2(bevel, h - 2.0f);
		_buttonOuterPoints[5] = origin + new Vector2(2.0f, h * 0.5f);

		_buttonInnerPoints[0] = origin + new Vector2(bevel + 10.0f, 11.0f);
		_buttonInnerPoints[1] = origin + new Vector2(w - bevel - 10.0f, 11.0f);
		_buttonInnerPoints[2] = origin + new Vector2(w - 18.0f, h * 0.5f);
		_buttonInnerPoints[3] = origin + new Vector2(w - bevel - 10.0f, h - 11.0f);
		_buttonInnerPoints[4] = origin + new Vector2(bevel + 10.0f, h - 11.0f);
		_buttonInnerPoints[5] = origin + new Vector2(18.0f, h * 0.5f);

		CloseInto(_buttonOuterPoints, _buttonClosedOuterPoints);
		CloseInto(_buttonInnerPoints, _buttonClosedInnerPoints);

		Color outerAura = new(0.18f, 0.98f, 0.96f, 0.11f * glow);
		Color innerFill = new(0.08f, 0.54f, 0.58f, 0.14f * glow);
		Color outerLine = new(0.78f, 1.0f, 0.95f, 0.34f * glow);
		Color innerLine = new(1.0f, 0.95f, 0.78f, 0.32f * glow);

		DrawColoredPolygon(_buttonOuterPoints, outerAura);
		DrawColoredPolygon(_buttonInnerPoints, innerFill);
		DrawPolyline(_buttonClosedOuterPoints, outerLine, 2.2f + glow * 1.2f, true);
		DrawPolyline(_buttonClosedInnerPoints, innerLine, 1.4f + glow * 0.8f, true);

		Vector2 center = rect.GetCenter();
		float streakInset = h * 0.68f;
		for (int index = 0; index < 2; index++)
		{
			float y = rect.Position.Y + h * (0.34f + index * 0.22f);
			DrawLine(
				new Vector2(rect.Position.X + streakInset, y),
				new Vector2(rect.End.X - streakInset, y + (index == 0 ? -2.0f : 2.0f)),
				new Color(0.82f, 1.0f, 0.94f, 0.10f * glow),
				1.0f);
		}

		DrawCircle(center, h * 0.10f, new Color(1.0f, 0.96f, 0.82f, 0.18f * glow));
		DrawButtonAccentSpark(rect.Position + new Vector2(12.0f, h * 0.5f), glow);
		DrawButtonAccentSpark(rect.Position + new Vector2(w - 12.0f, h * 0.5f), glow);
		DrawButtonAccentSpark(rect.Position + new Vector2(w * 0.5f, h + 4.0f), glow * 0.82f);
	}

	private void DrawRowSlotHoverGlow(Rect2 rect)
	{
		if (RowSlotHoverTexture != null)
		{
			DrawTextureRect(
				RowSlotHoverTexture,
				rect.GrowIndividual(2.0f, 2.0f, 2.0f, 2.0f),
				false,
				new Color(1.0f, 1.0f, 1.0f, 0.24f * _glow));
			return;
		}

		float glow = _glow * (0.88f + 0.12f * Mathf.Sin((float)_time * 5.2f));
		int radius = Mathf.RoundToInt(Mathf.Clamp(rect.Size.Y * 0.32f, 12.0f, 24.0f));

		_rowGlowStyle.BgColor = new Color(0.06f, 0.34f, 0.40f, 0.10f * glow);
		_rowGlowStyle.BorderColor = new Color(0.72f, 1.0f, 0.95f, 0.20f * glow);
		_rowGlowStyle.BorderWidthLeft = 1;
		_rowGlowStyle.BorderWidthTop = 1;
		_rowGlowStyle.BorderWidthRight = 1;
		_rowGlowStyle.BorderWidthBottom = 1;
		_rowGlowStyle.CornerRadiusTopLeft = radius + 2;
		_rowGlowStyle.CornerRadiusTopRight = radius + 2;
		_rowGlowStyle.CornerRadiusBottomLeft = radius + 2;
		_rowGlowStyle.CornerRadiusBottomRight = radius + 2;

		_rowRimStyle.BgColor = new Color(0.04f, 0.26f, 0.30f, 0.08f * glow);
		_rowRimStyle.BorderColor = new Color(0.96f, 0.95f, 0.82f, 0.20f * glow);
		_rowRimStyle.BorderWidthLeft = 1;
		_rowRimStyle.BorderWidthTop = 1;
		_rowRimStyle.BorderWidthRight = 1;
		_rowRimStyle.BorderWidthBottom = 1;
		_rowRimStyle.CornerRadiusTopLeft = radius;
		_rowRimStyle.CornerRadiusTopRight = radius;
		_rowRimStyle.CornerRadiusBottomLeft = radius;
		_rowRimStyle.CornerRadiusBottomRight = radius;

		DrawStyleBox(_rowGlowStyle, rect.Grow(1.0f));
		DrawStyleBox(_rowRimStyle, rect);
	}

	private static void CloseInto(Vector2[] points, Vector2[] closed)
	{
		for (int index = 0; index < points.Length; index++)
		{
			closed[index] = points[index];
		}

		closed[points.Length] = points[0];
	}

	private void DrawButtonAccentSpark(Vector2 center, float glow)
	{
		if (AccentSparkTexture != null)
		{
			float size = 7.5f + glow * 4.0f;
			Rect2 rect = new(center - new Vector2(size, size), new Vector2(size * 2.0f, size * 2.0f));
			DrawTextureRect(AccentSparkTexture, rect, false, new Color(1.0f, 1.0f, 1.0f, 0.42f * glow));
			return;
		}

		float radius = 3.0f + glow * 1.8f;
		Color aura = new(0.30f, 1.0f, 0.96f, 0.18f * glow);
		Color line = new(0.96f, 0.98f, 0.88f, 0.36f * glow);

		DrawCircle(center, radius * 2.2f, aura);
		DrawLine(center + new Vector2(-radius, 0.0f), center + new Vector2(radius, 0.0f), line, 1.1f);
		DrawLine(center + new Vector2(0.0f, -radius), center + new Vector2(0.0f, radius), line, 1.1f);
	}
}
