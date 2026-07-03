using Godot;

public partial class OceanDialogPanelArt : Control
{
	private const string DefaultPanelTexturePath = "res://assets/ui/player_count_dialog_interactive_panel.png";
	private const double SparkRedrawInterval = 1.0 / 12.0;
	private static readonly Vector2 SourcePanelSize = new(744.0f, 480.0f);
	private static readonly Rect2[] BuiltInRowRegions =
	{
		new(124.0f, 142.0f, 496.0f, 56.0f),
		new(124.0f, 224.0f, 496.0f, 56.0f),
		new(124.0f, 306.0f, 496.0f, 56.0f),
	};
	private static readonly Rect2 BuiltInActionWellRegion = new(214.0f, 378.0f, 316.0f, 76.0f);
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

	[Export]
	public bool ShowTitleCrest { get; set; } = true;

	[Export]
	public bool ShowTitleGuides { get; set; } = true;

	[Export]
	public bool ShowSideGems { get; set; } = true;

	[Export]
	public bool ShowBottomGem { get; set; } = true;

	[Export]
	public bool CoverBuiltInActionWell { get; set; } = true;

	[Export]
	public bool CoverBuiltInRowSlots { get; set; } = true;

	[Export(PropertyHint.Range, "0.10,0.35,0.01")]
	public float TitleBandRatio { get; set; } = 0.19f;

	[Export(PropertyHint.Range, "0.08,0.35,0.01")]
	public float InnerTopRatio { get; set; } = 0.19f;

	[Export(PropertyHint.Range, "0.05,0.22,0.01")]
	public float InnerBottomRatio { get; set; } = 0.10f;

	[Export(PropertyHint.Range, "0.03,0.14,0.01")]
	public float InnerSideRatio { get; set; } = 0.09f;

	[Export]
	public Color AccentColor { get; set; } = new(0.32f, 1.0f, 0.96f, 1.0f);

	[Export]
	public Color GoldAccentColor { get; set; } = new(0.98f, 0.83f, 0.52f, 1.0f);

	[Export(PropertyHint.File, "*.png")]
	public string PanelTexturePath { get; set; } = DefaultPanelTexturePath;

	[Export]
	public Texture2D? PanelTextureOverride { get; set; }

	[Export]
	public Texture2D? PanelSparkTexture { get; set; }

	[Export]
	public Texture2D? RowCoverTexture { get; set; }

	[Export]
	public Texture2D? ActionWellCoverTexture { get; set; }

	private Texture2D? _panelTexture;
	private readonly StyleBoxFlat _slotCoverGlowStyle = new();
	private readonly StyleBoxFlat _slotCoverStyle = new();
	private readonly StyleBoxFlat _wellCoverStyle = new();
	private double _time;
	private double _redrawAccumulator;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		string texturePath = string.IsNullOrWhiteSpace(PanelTexturePath) ? DefaultPanelTexturePath : PanelTexturePath;
		_panelTexture = PanelTextureOverride ?? GD.Load<Texture2D>(texturePath);

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
		_redrawAccumulator += delta;
		if (_redrawAccumulator < SparkRedrawInterval)
		{
			return;
		}

		_redrawAccumulator %= SparkRedrawInterval;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (_panelTexture == null || Size.X < 120.0f || Size.Y < 120.0f)
		{
			return;
		}

		DrawTextureRect(_panelTexture, new Rect2(Vector2.Zero, Size), false);
		if (CoverBuiltInRowSlots)
		{
			DrawBuiltInRowCovers();
		}
		if (CoverBuiltInActionWell)
		{
			DrawActionWellCover();
		}
		DrawAnimatedSparks();
	}

	private void DrawBuiltInRowCovers()
	{
		foreach (Rect2 sourceRect in BuiltInRowRegions)
		{
			Rect2 coverRect = ScaleSourceRect(sourceRect).GrowIndividual(5.0f, 4.0f, 5.0f, 4.0f);
			DrawSlotCover(coverRect, 0.08f);
		}
	}

	private void ConfigureCoverStyles(StyleBoxFlat glowStyle, StyleBoxFlat fillStyle, int radius)
	{
		glowStyle.BgColor = new Color(0.01f, 0.11f, 0.16f, 0.84f);
		glowStyle.BorderWidthLeft = 0;
		glowStyle.BorderWidthTop = 0;
		glowStyle.BorderWidthRight = 0;
		glowStyle.BorderWidthBottom = 0;
		glowStyle.CornerRadiusTopLeft = radius + 1;
		glowStyle.CornerRadiusTopRight = radius + 1;
		glowStyle.CornerRadiusBottomLeft = radius + 1;
		glowStyle.CornerRadiusBottomRight = radius + 1;

		fillStyle.BgColor = new Color(0.02f, 0.12f, 0.17f, 0.96f);
		fillStyle.BorderWidthLeft = 0;
		fillStyle.BorderWidthTop = 0;
		fillStyle.BorderWidthRight = 0;
		fillStyle.BorderWidthBottom = 0;
		fillStyle.CornerRadiusTopLeft = radius;
		fillStyle.CornerRadiusTopRight = radius;
		fillStyle.CornerRadiusBottomLeft = radius;
		fillStyle.CornerRadiusBottomRight = radius;
	}

	private void DrawSlotCover(Rect2 coverRect, float extraGlow)
	{
		if (RowCoverTexture != null)
		{
			DrawTextureRect(RowCoverTexture, coverRect, false, new Color(1.0f, 1.0f, 1.0f, 0.92f + extraGlow * 0.4f));
			return;
		}

		int radius = Mathf.RoundToInt(Mathf.Clamp(coverRect.Size.Y * 0.34f, 16.0f, 28.0f));
		ConfigureCoverStyles(_slotCoverGlowStyle, _slotCoverStyle, radius);
		_slotCoverGlowStyle.BgColor = new Color(0.01f, 0.11f, 0.16f, 0.82f + extraGlow);
		DrawStyleBox(_slotCoverGlowStyle, coverRect.Grow(1.5f));
		DrawStyleBox(_slotCoverStyle, coverRect);
	}

	private void DrawActionWellCover()
	{
		Rect2 coverRect = ScaleSourceRect(BuiltInActionWellRegion).GrowIndividual(6.0f, 5.0f, 6.0f, 4.0f);
		if (ActionWellCoverTexture != null)
		{
			DrawTextureRect(ActionWellCoverTexture, coverRect, false);
			return;
		}

		int radius = Mathf.RoundToInt(Mathf.Clamp(coverRect.Size.Y * 0.38f, 18.0f, 30.0f));
		_wellCoverStyle.BgColor = new Color(0.02f, 0.12f, 0.17f, 0.97f);
		_wellCoverStyle.BorderWidthLeft = 0;
		_wellCoverStyle.BorderWidthTop = 0;
		_wellCoverStyle.BorderWidthRight = 0;
		_wellCoverStyle.BorderWidthBottom = 0;
		_wellCoverStyle.CornerRadiusTopLeft = radius;
		_wellCoverStyle.CornerRadiusTopRight = radius;
		_wellCoverStyle.CornerRadiusBottomLeft = radius;
		_wellCoverStyle.CornerRadiusBottomRight = radius;
		DrawStyleBox(_wellCoverStyle, coverRect);
	}

	private Rect2 ScaleSourceRect(Rect2 sourceRect)
	{
		Vector2 position = new(
			sourceRect.Position.X / SourcePanelSize.X * Size.X,
			sourceRect.Position.Y / SourcePanelSize.Y * Size.Y);
		Vector2 scaledSize = new(
			sourceRect.Size.X / SourcePanelSize.X * Size.X,
			sourceRect.Size.Y / SourcePanelSize.Y * Size.Y);
		return new Rect2(position, scaledSize);
	}

	private void DrawAnimatedSparks()
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
