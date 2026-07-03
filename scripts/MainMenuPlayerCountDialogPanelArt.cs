using Godot;

public partial class MainMenuPlayerCountDialogPanelArt : Control
{
	private const string PanelTexturePath = "res://assets/ui/player_count_dialog_interactive_panel.png";
	private const double IdleSparkRedrawInterval = 1.0 / 12.0;
	private const double ActiveGlowRedrawInterval = 1.0 / 24.0;
	private const float GlowEpsilon = 0.001f;
	private static readonly PanelSpark[] PanelSparks =
	{
		new(new Vector2(0.500f, 0.066f), 8.8f, 0.42f, 1.65f, 0.45f, 11.0f, 5.8f),
		new(new Vector2(0.022f, 0.514f), 7.0f, 0.38f, 1.50f, 1.65f, 9.0f, 6.0f),
		new(new Vector2(0.978f, 0.514f), 7.0f, 0.38f, 1.58f, 4.35f, 9.0f, 6.0f),
		new(new Vector2(0.165f, 0.216f), 4.1f, 0.28f, 1.90f, 0.90f, 4.6f, 4.6f),
		new(new Vector2(0.835f, 0.216f), 4.1f, 0.28f, 1.84f, 3.55f, 4.6f, 4.6f),
		new(new Vector2(0.150f, 0.808f), 2.6f, 0.18f, 1.72f, 2.15f, 0.0f, 3.8f),
		new(new Vector2(0.850f, 0.808f), 2.6f, 0.18f, 1.76f, 5.05f, 0.0f, 3.8f),
		new(new Vector2(0.338f, 0.812f), 3.8f, 0.24f, 2.10f, 0.25f, 5.2f, 4.2f),
		new(new Vector2(0.662f, 0.812f), 3.8f, 0.24f, 2.04f, 3.25f, 5.2f, 4.2f),
		new(new Vector2(0.500f, 0.866f), 4.8f, 0.30f, 1.96f, 2.75f, 6.0f, 5.0f),
	};

	private Texture2D? _panelTexture;
	[Export]
	public Texture2D? PanelTextureOverride { get; set; }

	private Control? _startGameFrame;
	private Button? _startGameButton;
	private double _time;
	private double _redrawAccumulator;
	private float _startButtonGlow;
	[Export]
	public Texture2D? PanelSparkTexture { get; set; }

	[Export]
	public Texture2D? StartButtonGlowTexture { get; set; }

	private readonly Vector2[] _buttonOuterPoints = new Vector2[6];
	private readonly Vector2[] _buttonInnerPoints = new Vector2[6];
	private readonly Vector2[] _buttonClosedOuterPoints = new Vector2[7];
	private readonly Vector2[] _buttonClosedInnerPoints = new Vector2[7];

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_startGameFrame = GetNodeOrNull<Control>("StartGameFrame");
		_startGameButton = GetNodeOrNull<Button>("StartGameFrame/StartGameButton");
		_panelTexture = PanelTextureOverride ?? GD.Load<Texture2D>(PanelTexturePath);

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
		if (!IsVisibleInTree())
		{
			return;
		}

		_time += delta;

		float targetGlow = 0.0f;
		if (_startGameButton != null)
		{
			bool hovered = _startGameButton.GetGlobalRect().HasPoint(GetGlobalMousePosition());
			targetGlow = hovered || _startGameButton.HasFocus() ? 1.0f : 0.0f;
			if (_startGameButton.ButtonPressed)
			{
				targetGlow = 0.62f;
			}
		}

		float nextGlow = Mathf.Lerp(_startButtonGlow, targetGlow, Mathf.Clamp((float)delta * 10.0f, 0.0f, 1.0f));
		bool glowChanged = Mathf.Abs(nextGlow - _startButtonGlow) > GlowEpsilon;
		_startButtonGlow = nextGlow;
		if (glowChanged)
		{
			_redrawAccumulator = 0.0;
			QueueRedraw();
			return;
		}

		_redrawAccumulator += delta;
		double redrawInterval = (_startButtonGlow > 0.01f || targetGlow > 0.01f)
			? ActiveGlowRedrawInterval
			: IdleSparkRedrawInterval;
		if (_redrawAccumulator < redrawInterval)
		{
			return;
		}

		_redrawAccumulator %= redrawInterval;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_panelTexture == null)
		{
			return;
		}

		DrawTextureRect(_panelTexture, new Rect2(Vector2.Zero, Size), false);
		DrawAnimatedPanelSparks();
		DrawStartButtonHoverGlow();
	}

	private void DrawAnimatedPanelSparks()
	{
		float t = (float)_time;
		foreach (PanelSpark spark in PanelSparks)
		{
			float flash = Mathf.Max(0.0f, Mathf.Sin(t * spark.Speed + spark.Phase));
			flash = Mathf.Pow(flash, spark.FlashPower);
			float pulse = 0.08f + 0.92f * flash;
			pulse *= 0.72f + 0.28f * Mathf.Max(0.0f, Mathf.Sin(t * (spark.Speed * 2.3f) + spark.Phase * 1.7f));
			Vector2 center = new(Size.X * spark.PositionRatio.X, Size.Y * spark.PositionRatio.Y);
			float radius = spark.Radius * (0.82f + pulse * 0.42f);
			Color aura = new(0.24f, 0.98f, 0.98f, spark.Alpha * pulse * 0.28f);
			Color core = new(0.88f, 1.0f, 0.96f, spark.Alpha * pulse);
			Color centerDot = new(1.0f, 0.98f, 0.84f, spark.Alpha * (0.44f + pulse * 0.56f));

			if (PanelSparkTexture != null)
			{
				DrawTextureRect(
					PanelSparkTexture,
					new Rect2(center - new Vector2(radius * 1.25f, radius * 1.25f), new Vector2(radius * 2.5f, radius * 2.5f)),
					false,
					aura);
				DrawTextureRect(
					PanelSparkTexture,
					new Rect2(center - new Vector2(radius * 0.72f, radius * 0.72f), new Vector2(radius * 1.44f, radius * 1.44f)),
					false,
					centerDot);
				continue;
			}

			DrawCircle(center, radius * 2.4f, aura);
			DrawCircle(center, radius * 1.1f, new Color(core.R, core.G, core.B, core.A * 0.46f));
			DrawCircle(center, radius * 0.28f, centerDot);

			if (spark.RayLength <= 0.0f)
			{
				continue;
			}

			float rayLength = spark.RayLength * (0.76f + pulse * 0.34f);
			float horizontalThickness = 1.1f + pulse * 0.5f;
			float diagonalThickness = 0.85f + pulse * 0.4f;
			DrawLine(
				center + new Vector2(-rayLength, 0.0f),
				center + new Vector2(rayLength, 0.0f),
				new Color(core.R, core.G, core.B, core.A * 0.80f),
				horizontalThickness);
			DrawLine(
				center + new Vector2(0.0f, -rayLength),
				center + new Vector2(0.0f, rayLength),
				new Color(core.R, core.G, core.B, core.A * 0.84f),
				horizontalThickness);
			DrawLine(
				center + new Vector2(-rayLength * 0.58f, -rayLength * 0.58f),
				center + new Vector2(rayLength * 0.58f, rayLength * 0.58f),
				new Color(centerDot.R, centerDot.G, centerDot.B, centerDot.A * 0.54f),
				diagonalThickness);
			DrawLine(
				center + new Vector2(-rayLength * 0.58f, rayLength * 0.58f),
				center + new Vector2(rayLength * 0.58f, -rayLength * 0.58f),
				new Color(centerDot.R, centerDot.G, centerDot.B, centerDot.A * 0.54f),
				diagonalThickness);
		}
	}

	private void DrawStartButtonHoverGlow()
	{
		if (_startGameFrame == null || _startGameFrame.Size.X <= 0.0f || _startGameFrame.Size.Y <= 0.0f)
		{
			return;
		}

		if (_startButtonGlow <= 0.01f)
		{
			return;
		}

		Rect2 rect = new(_startGameFrame.Position, _startGameFrame.Size);
		float w = rect.Size.X;
		float h = rect.Size.Y;
		float bevel = h * 0.34f;
		Vector2 origin = rect.Position;
		float pulse = 0.86f + 0.14f * Mathf.Sin((float)_time * 6.0f);
		float glow = _startButtonGlow * pulse;

		if (StartButtonGlowTexture != null)
		{
			DrawTextureRect(
				StartButtonGlowTexture,
				rect.GrowIndividual(8.0f, 8.0f, 8.0f, 8.0f),
				false,
				new Color(1.0f, 1.0f, 1.0f, 0.28f * glow));
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
		Color outerLine = new(0.78f, 1.0f, 0.95f, 0.34f * glow);

		DrawColoredPolygon(_buttonOuterPoints, outerAura);
		DrawPolyline(_buttonClosedOuterPoints, outerLine, 2.2f + glow * 1.2f, true);
	}

	private static void CloseInto(Vector2[] points, Vector2[] closed)
	{
		for (int index = 0; index < points.Length; index++)
		{
			closed[index] = points[index];
		}

		closed[points.Length] = points[0];
	}

	private readonly struct PanelSpark
	{
		public PanelSpark(Vector2 positionRatio, float radius, float alpha, float speed, float phase, float rayLength, float flashPower)
		{
			PositionRatio = positionRatio;
			Radius = radius;
			Alpha = alpha;
			Speed = speed;
			Phase = phase;
			RayLength = rayLength;
			FlashPower = flashPower;
		}

		public Vector2 PositionRatio { get; }
		public float Radius { get; }
		public float Alpha { get; }
		public float Speed { get; }
		public float Phase { get; }
		public float RayLength { get; }
		public float FlashPower { get; }
	}
}
