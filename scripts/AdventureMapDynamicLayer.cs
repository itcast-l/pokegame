using System.Collections.Generic;
using Godot;

public partial class AdventureMapDynamicLayer : Control
{
	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float MotionScale { get; set; } = 1.0f;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<CurrentRibbon> _ribbons = new();
	private readonly List<Sparkle> _sparkles = new();
	private double _time;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 23581;
		Rebuild();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			Rebuild();
		}
	}

	public override void _Process(double delta)
	{
		_time += delta * MotionScale;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		DrawCurrentRibbons();
		DrawSparkles();
	}

	private void Rebuild()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		_ribbons.Clear();
		_sparkles.Clear();

		for (int i = 0; i < 9; i++)
		{
			_ribbons.Add(new CurrentRibbon(
				_rng.RandfRange(0.12f, 0.88f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.18f, 0.46f),
				_rng.RandfRange(18.0f, 54.0f),
				_rng.RandfRange(0.035f, 0.09f)));
		}

		for (int i = 0; i < 44; i++)
		{
			_sparkles.Add(new Sparkle(
				new Vector2(_rng.RandfRange(0.06f, 0.94f), _rng.RandfRange(0.08f, 0.92f)),
				_rng.RandfRange(1.2f, 3.8f),
				_rng.RandfRange(0.35f, 1.15f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.08f, 0.24f)));
		}
	}

	private void DrawCurrentRibbons()
	{
		float t = (float)_time;
		foreach (CurrentRibbon ribbon in _ribbons)
		{
			Vector2[] points = new Vector2[36];
			float drift = Mathf.PosMod(t * ribbon.Speed + ribbon.Phase * 80.0f, Size.X * 1.22f) - Size.X * 0.16f;
			for (int i = 0; i < points.Length; i++)
			{
				float ratio = i / (float)(points.Length - 1);
				float x = drift + ratio * Size.X * 0.48f;
				float wave = Mathf.Sin(ratio * Mathf.Tau * 1.6f + t * 0.62f + ribbon.Phase) * ribbon.Amplitude;
				float y = Size.Y * ribbon.YRatio + wave;
				points[i] = new Vector2(x, y);
			}

			DrawPolyline(points, new Color(0.62f, 1.0f, 0.94f, ribbon.Alpha), 2.0f, true);
		}
	}

	private void DrawSparkles()
	{
		float t = (float)_time;
		foreach (Sparkle sparkle in _sparkles)
		{
			float pulse = Mathf.Max(0.0f, Mathf.Sin(t * sparkle.Speed + sparkle.Phase));
			if (pulse <= 0.03f)
			{
				continue;
			}

			Vector2 position = new(
				Size.X * sparkle.PositionRatio.X + Mathf.Sin(t * 0.26f + sparkle.Phase) * 10.0f,
				Size.Y * sparkle.PositionRatio.Y + Mathf.Cos(t * 0.22f + sparkle.Phase) * 7.0f);
			float radius = sparkle.Radius * (0.65f + pulse * 0.75f);
			Color color = new(0.82f, 1.0f, 0.92f, sparkle.Alpha * pulse);
			DrawLine(position - new Vector2(radius, 0.0f), position + new Vector2(radius, 0.0f), color, 1.0f, true);
			DrawLine(position - new Vector2(0.0f, radius), position + new Vector2(0.0f, radius), color, 1.0f, true);
			DrawCircle(position, radius * 0.18f, color);
		}
	}

	private readonly record struct CurrentRibbon(float YRatio, float Phase, float Speed, float Amplitude, float Alpha);
	private readonly record struct Sparkle(Vector2 PositionRatio, float Radius, float Speed, float Phase, float Alpha);
}
