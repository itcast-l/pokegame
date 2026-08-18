using System;
using Godot;

public partial class OceanMenuButtonFrame : Control
{
	private const float GlowEpsilon = 0.001f;
	private static readonly Color AdventureSelectedGlowColor = new(0.48f, 0.98f, 1.0f, 1.0f);

	public enum ButtonFrameStyle
	{
		OceanPlaque,
		CardSlabs,
		AdventureGlassPlaque,
		AdventureGlassSelector,
		AdventureGlassMenuRow,
		FirstVersionRow,
		FirstVersionBack,
		FirstVersionPrimary,
		NeutralSmokeRow,
		NeutralSmokeBack,
		NeutralSmokePrimary,
		IndividualToolRow,
		IndividualAction,
		IndividualSecondaryAction
	}

	[Export]
	public ButtonFrameStyle FrameStyle { get; set; } = ButtonFrameStyle.OceanPlaque;

	[Export]
	public Color AccentColor { get; set; } = new(0.55f, 1.0f, 0.92f, 1.0f);

	[Export]
	public bool FlipOrnament { get; set; }

	private bool _selected;
	private bool _highlightFocus = true;
	private bool _showIdleTexture = true;

	[Export]
	public bool Selected
	{
		get => _selected;
		set
		{
			if (_selected == value)
			{
				return;
			}
			_selected = value;
			RefreshTargetState();
		}
	}

	[Export]
	public bool HighlightFocus
	{
		get => _highlightFocus;
		set
		{
			if (_highlightFocus == value)
			{
				return;
			}
			_highlightFocus = value;
			RefreshTargetState();
		}
	}

	[Export]
	public bool UsePlaqueGlowBackdrop { get; set; } = true;

	[Export]
	public bool UseToolRowIdleBackdrop { get; set; } = true;

	[Export]
	public bool ShowIdleTexture
	{
		get => _showIdleTexture;
		set
		{
			if (_showIdleTexture == value)
			{
				return;
			}
			_showIdleTexture = value;
			RefreshTargetState();
		}
	}

	[Export(PropertyHint.Range, "0.2,1.0,0.05")]
	public float CardSlabSurfaceOpacityScale { get; set; } = 1.0f;

	[Export]
	public bool SuppressVisuals { get; set; }

	[Export]
	public bool UseNinePatchTexture { get; set; }

	[Export]
	public Vector2 VisualInset { get; set; } = Vector2.Zero;

	[Export(PropertyHint.Range, "0,128,1")]
	public float TexturePatchMarginHorizontal { get; set; } = 24.0f;

	[Export(PropertyHint.Range, "0,128,1")]
	public float TexturePatchMarginVertical { get; set; } = 18.0f;

	[Export]
	public Texture2D? OceanPlaqueTexture { get; set; }

	[Export]
	public Texture2D? OceanPlaqueFlippedTexture { get; set; }

	[Export]
	public Texture2D? CardSlabTexture { get; set; }

	[Export]
	public Texture2D? SelectionGlowTexture { get; set; }

	[Export]
	public Texture2D? FocusTexture { get; set; }

	[Export]
	public Texture2D? PressedTexture { get; set; }

	[Export(PropertyHint.Range, "0.5,3.0,0.05")]
	public float SelectionGlowStrength { get; set; } = 1.0f;

	private float _glow;
	private float _targetGlow;
#if DECKLAND_TEST_HOOKS
	private long _processCallCount;
#endif

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		VisibilityChanged += OnFrameVisibilityChanged;
		InitializeInteractionBindings();
		RefreshTargetState();
	}

	public override void _ExitTree()
	{
		SetProcess(false);
		Callable visibilityChanged = Callable.From(OnFrameVisibilityChanged);
		if (IsConnected(CanvasItem.SignalName.VisibilityChanged, visibilityChanged))
		{
			Disconnect(CanvasItem.SignalName.VisibilityChanged, visibilityChanged);
		}
		ClearInteractionObservation();
	}

	public override void _Process(double delta)
	{
#if DECKLAND_TEST_HOOKS
		_processCallCount++;
#endif
		if (!IsVisibleInTree())
		{
			SetProcess(false);
			return;
		}

		float nextGlow = Mathf.MoveToward(_glow, _targetGlow, Math.Max(0.001f, (float)delta * 4.0f));
		bool glowChanged = Mathf.Abs(nextGlow - _glow) > GlowEpsilon;
		_glow = nextGlow;
		if (glowChanged)
		{
			QueueRedraw();
		}
		if (Mathf.Abs(_glow - _targetGlow) <= GlowEpsilon)
		{
			_glow = _targetGlow;
			SetProcess(false);
		}
	}

#if DECKLAND_TEST_HOOKS
	public long DebugGetProcessCallCount()
	{
		return _processCallCount;
	}

	public float DebugGetGlow()
	{
		return _glow;
	}

	public int DebugGetBoundInteractiveControlCount()
	{
		return _interactionBindings.Count;
	}

	public int DebugGetObservedSubtreeNodeCount()
	{
		return _observedSubtreeNodes.Count;
	}

	public long DebugGetSubtreeChangeCallbackCount()
	{
		return _subtreeChangeCallbackCount;
	}

	public ulong DebugGetPrimaryButtonInstanceId()
	{
		return _button != null && GodotObject.IsInstanceValid(_button)
			? _button.GetInstanceId()
			: 0;
	}
#endif

	public override void _Draw()
	{
		if (SuppressVisuals)
		{
			return;
		}

		Vector2 clampedInset = new(
			Mathf.Clamp(VisualInset.X, 0.0f, Size.X * 0.5f),
			Mathf.Clamp(VisualInset.Y, 0.0f, Size.Y * 0.5f));
		Rect2 rect = clampedInset != Vector2.Zero
			? new Rect2(clampedInset, Size - clampedInset * 2.0f)
			: UseNinePatchTexture
				? new Rect2(Vector2.Zero, Size)
				: FrameStyle is ButtonFrameStyle.NeutralSmokeRow or ButtonFrameStyle.IndividualToolRow
					? new Rect2(Vector2.Zero, Size)
					: _button != null
						? new Rect2(_button.Position, _button.Size)
						: new Rect2(Vector2.Zero, Size);

		if (rect.Size.X <= 0.0f || rect.Size.Y <= 0.0f)
		{
			return;
		}

		if (TryDrawTexturedFrame(rect))
		{
			return;
		}

		float w = rect.Size.X;
		float h = rect.Size.Y;
		if (FrameStyle == ButtonFrameStyle.CardSlabs)
		{
			DrawCardSlabFrame(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.AdventureGlassPlaque)
		{
			DrawAdventureGlassPlaqueFrame(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.AdventureGlassSelector)
		{
			DrawAdventureGlassSelectorFrame(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.AdventureGlassMenuRow)
		{
			DrawAdventureGlassMenuRowFrame(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.FirstVersionRow)
		{
			DrawFirstVersionRow(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.FirstVersionBack)
		{
			DrawFirstVersionBack(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.FirstVersionPrimary)
		{
			DrawFirstVersionPrimary(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.NeutralSmokeRow)
		{
			DrawNeutralSmokeRow(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.NeutralSmokeBack)
		{
			DrawNeutralSmokeBack(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.NeutralSmokePrimary)
		{
			DrawNeutralSmokePrimary(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.IndividualToolRow)
		{
			DrawIndividualToolRow(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.IndividualAction)
		{
			DrawIndividualAction(rect);
			return;
		}
		if (FrameStyle == ButtonFrameStyle.IndividualSecondaryAction)
		{
			DrawIndividualSecondaryAction(rect);
			return;
		}

		if (w < h * 2.05f || h < 28.0f)
		{
			DrawCompactFrame(rect);
			return;
		}

		float bevel = h * 0.28f;
		Vector2 origin = rect.Position;

		_plaquePoints[0] = origin + new Vector2(bevel, 2.0f);
		_plaquePoints[1] = origin + new Vector2(w - bevel * 0.48f, 2.0f);
		_plaquePoints[2] = origin + new Vector2(w - 2.0f, h * 0.5f);
		_plaquePoints[3] = origin + new Vector2(w - bevel * 0.48f, h - 2.0f);
		_plaquePoints[4] = origin + new Vector2(bevel, h - 2.0f);
		_plaquePoints[5] = origin + new Vector2(2.0f, h * 0.5f);

		_insetPoints[0] = origin + new Vector2(bevel + 12.0f, 12.0f);
		_insetPoints[1] = origin + new Vector2(w - bevel * 0.58f - 12.0f, 10.0f);
		_insetPoints[2] = origin + new Vector2(w - 18.0f, h * 0.5f);
		_insetPoints[3] = origin + new Vector2(w - bevel * 0.58f - 12.0f, h - 10.0f);
		_insetPoints[4] = origin + new Vector2(bevel + 12.0f, h - 12.0f);
		_insetPoints[5] = origin + new Vector2(18.0f, h * 0.5f);

		Color shadow = new(0.0f, 0.02f, 0.04f, 0.32f + _glow * 0.08f);
		for (int i = 0; i < _plaquePoints.Length; i++)
		{
			_shadowPoints[i] = _plaquePoints[i] + new Vector2(0.0f, 5.0f);
		}

		float selectedBoost = Selected ? 0.08f : 0.0f;
		Color outer = new(0.02f, 0.22f + _glow * 0.08f + selectedBoost, 0.28f + _glow * 0.08f + selectedBoost, 0.8f);
		Color inner = new(0.04f, 0.42f + _glow * 0.15f + selectedBoost, 0.46f + _glow * 0.13f + selectedBoost, 0.66f);
		Color rim = AccentColor.Lerp(Colors.White, 0.18f + _glow * 0.22f);
		Color highlight = AccentColor.Lerp(new Color(1.0f, 0.94f, 0.66f), _glow * 0.45f);

		DrawColoredPolygon(_shadowPoints, shadow);
		if (Selected)
		{
			ConfigureSelectionGlowStyle(_selectionGlowStyle);
			DrawStyleBox(
				_selectionGlowStyle,
				new Rect2(rect.Position - new Vector2(3.0f, 3.0f), rect.Size + new Vector2(6.0f, 6.0f)));
		}

		CloseInto(_plaquePoints, _closedPlaquePoints);
		CloseInto(_insetPoints, _closedInsetPoints);
		DrawColoredPolygon(_plaquePoints, outer);
		DrawColoredPolygon(_insetPoints, inner);
		DrawPolyline(_closedPlaquePoints, new Color(rim.R, rim.G, rim.B, 0.58f + _glow * 0.28f), 2.0f + _glow, true);
		DrawPolyline(_closedInsetPoints, new Color(0.82f, 1.0f, 0.95f, 0.22f + _glow * 0.18f), 1.2f, true);

		DrawSideAccent(origin, w, h, highlight);
		DrawCausticStreaks(origin, w, h);
	}
}
