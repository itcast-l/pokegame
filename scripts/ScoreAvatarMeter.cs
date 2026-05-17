using System;
using Godot;

[Tool]
public partial class ScoreAvatarMeter : Control
{
	public static readonly Vector2 DefaultSize = new(92.0f, 92.0f);

	private const float FullCircleRadians = 6.2831855f;
	private const float FillAnimationSpeed = 150.0f;
	private const float PulseFadeSpeed = 2.8f;

	private readonly Color _backgroundColor = new(0.0f, 0.08f, 0.12f, 0.08f);
	private readonly Color _rimColor = new(0.92f, 0.98f, 1.0f, 0.24f);
	private Color _fillColor = new(0.58f, 0.93f, 1.0f, 0.46f);
	private Color _glowColor = new(0.86f, 0.99f, 1.0f, 0.36f);
	private float _circleDiameter = 76.0f;
	private float _displayScore;
	private float _targetScore;
	private float _maxScore = 100.0f;
	private float _pulseAmount;

	[Export(PropertyHint.Range, "40,120,1")]
	public float circle_diameter
	{
		get => _circleDiameter;
		set
		{
			_circleDiameter = Mathf.Clamp(value, 24.0f, 200.0f);
			if (IsInsideTree())
			{
				QueueRedraw();
			}
		}
	}

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		if (Size == Vector2.Zero)
		{
			Size = DefaultSize;
		}

		PivotOffset = Size / 2.0f;
		SetProcess(false);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			PivotOffset = Size / 2.0f;
			QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		float previousScore = _displayScore;
		float previousPulse = _pulseAmount;
		_displayScore = Mathf.MoveToward(_displayScore, _targetScore, (float)delta * FillAnimationSpeed);
		_pulseAmount = Mathf.MoveToward(_pulseAmount, 0.0f, (float)delta * PulseFadeSpeed);

		if (!Mathf.IsEqualApprox(previousScore, _displayScore) || !Mathf.IsEqualApprox(previousPulse, _pulseAmount))
		{
			QueueRedraw();
		}

		if (Mathf.IsEqualApprox(_displayScore, _targetScore) && Mathf.IsZeroApprox(_pulseAmount))
		{
			SetProcess(false);
		}
	}

	public override void _Draw()
	{
		Vector2 center = Size / 2.0f;
		float radius = Mathf.Min(_circleDiameter, Mathf.Min(Size.X, Size.Y)) / 2.0f;
		float progress = Mathf.Clamp(_displayScore / _maxScore, 0.0f, 1.0f);

		if (_pulseAmount > 0.0f)
		{
			var pulseColor = new Color(_fillColor.R, _fillColor.G, _fillColor.B, 0.18f * _pulseAmount);
			DrawArc(center, radius + 2.0f + 4.0f * _pulseAmount, 0.0f, FullCircleRadians, 96, pulseColor, 4.0f + 5.0f * _pulseAmount, true);
		}

		DrawCircle(center, radius, _backgroundColor);
		DrawBottomFill(center, radius - 2.0f, progress);
		DrawFillSurface(center, radius - 2.0f, progress);

		DrawArc(center, radius - 1.0f, 0.0f, FullCircleRadians, 96, _rimColor, 2.0f, true);
		DrawArc(center, radius - 4.0f, 0.0f, FullCircleRadians, 96, _glowColor, 1.5f, true);
		DrawCircle(center + new Vector2(-radius * 0.18f, -radius * 0.20f), radius * 0.28f, new Color(1.0f, 1.0f, 1.0f, 0.055f));
	}

	public void Configure(Color fillColor, Color glowColor)
	{
		_fillColor = fillColor;
		_glowColor = glowColor;
		QueueRedraw();
	}

	public void SetScore(int score, int maxScore, bool animate)
	{
		_maxScore = Math.Max(1, maxScore);
		float nextTarget = Mathf.Clamp((float)score, 0.0f, _maxScore);
		bool gainedScore = nextTarget > _targetScore;
		_targetScore = nextTarget;

		if (!animate)
		{
			_displayScore = _targetScore;
			_pulseAmount = 0.0f;
			SetProcess(false);
			QueueRedraw();
			return;
		}

		if (gainedScore)
		{
			_pulseAmount = 1.0f;
		}

		SetProcess(true);
	}

	private void DrawBottomFill(Vector2 center, float radius, float progress)
	{
		if (progress <= 0.001f)
		{
			return;
		}

		if (progress >= 0.999f)
		{
			DrawCircle(center, radius, _fillColor);
			return;
		}

		float topY = center.Y + radius - radius * 2.0f * progress;
		int firstY = Mathf.CeilToInt(topY);
		int lastY = Mathf.FloorToInt(center.Y + radius);
		for (int y = firstY; y <= lastY; y++)
		{
			float verticalDistance = y - center.Y;
			float halfWidth = Mathf.Sqrt(Mathf.Max(0.0f, radius * radius - verticalDistance * verticalDistance));
			DrawLine(
				new Vector2(center.X - halfWidth, y),
				new Vector2(center.X + halfWidth, y),
				_fillColor,
				1.5f,
				true);
		}
	}

	private void DrawFillSurface(Vector2 center, float radius, float progress)
	{
		if (progress <= 0.001f || progress >= 0.999f)
		{
			return;
		}

		float topY = center.Y + radius - radius * 2.0f * progress;
		float verticalDistance = topY - center.Y;
		float halfWidth = Mathf.Sqrt(Mathf.Max(0.0f, radius * radius - verticalDistance * verticalDistance));
		DrawLine(
			new Vector2(center.X - halfWidth, topY),
			new Vector2(center.X + halfWidth, topY),
			_glowColor,
			2.0f,
			true);
	}
}
