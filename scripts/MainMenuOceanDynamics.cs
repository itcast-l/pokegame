using Godot;
using System.Collections.Generic;

public partial class MainMenuOceanDynamics : Control
{
	private const float WaterlineRatio = 0.34f;

	[Export(PropertyHint.Range, "0,80,1")]
	public int bubble_count = 42;

	[Export(PropertyHint.Range, "0,60,1")]
	public int glint_count = 26;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float motion_scale = 1.0f;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Bubble> _bubbles = new();
	private readonly List<Glint> _glints = new();
	private double _time;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 80110;
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
		_time += delta * motion_scale;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		DrawSunbeams();
		DrawSurfaceWake();
		DrawCurrentRibbons();
		DrawCausticRibbons();
		DrawBubbleStreams();
		DrawGlints();
	}

	private void Rebuild()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		_bubbles.Clear();
		_glints.Clear();

		float[] bubbleColumns = { 0.09f, 0.46f, 0.67f, 0.84f, 0.94f };
		for (int i = 0; i < bubble_count; i++)
		{
			float column = bubbleColumns[i % bubbleColumns.Length] + _rng.RandfRange(-0.025f, 0.025f);
			_bubbles.Add(new Bubble(
				column,
				_rng.RandfRange(0.16f, 1.02f),
				_rng.RandfRange(2.2f, 8.5f),
				_rng.RandfRange(28.0f, 88.0f),
				_rng.RandfRange(0.12f, 0.42f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(7.0f, 21.0f)));
		}

		for (int i = 0; i < glint_count; i++)
		{
			_glints.Add(new Glint(
				_rng.RandfRange(0.38f, 0.98f),
				_rng.RandfRange(WaterlineRatio + 0.06f, 0.9f),
				_rng.RandfRange(1.4f, 4.2f),
				_rng.RandfRange(0.55f, 1.35f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.08f, 0.26f)));
		}
	}

	private void DrawSunbeams()
	{
		float t = (float)_time;
		for (int i = 0; i < 5; i++)
		{
			float x = Size.X * (0.47f + i * 0.12f + Mathf.Sin(t * 0.08f + i) * 0.015f);
			float width = Size.X * (0.07f + i * 0.014f);
			float alpha = 0.062f + Mathf.Sin(t * 0.42f + i * 1.7f) * 0.024f;
			Vector2[] beam =
			{
				new(x - width * 0.3f, 0.0f),
				new(x + width * 0.8f, 0.0f),
				new(x + width * 1.65f, Size.Y),
				new(x - width * 1.1f, Size.Y),
			};
			DrawColoredPolygon(beam, new Color(0.74f, 1.0f, 0.96f, Mathf.Max(alpha, 0.0f)));
		}
	}

	private void DrawSurfaceWake()
	{
		float t = (float)_time;
		float baseY = Size.Y * 0.315f;
		for (int line = 0; line < 6; line++)
		{
			float y = baseY + line * Size.Y * 0.018f;
			Vector2[] points = new Vector2[56];
			for (int i = 0; i < points.Length; i++)
			{
				float ratio = i / (float)(points.Length - 1);
				float x = ratio * Size.X;
				float wave = Mathf.Sin(ratio * Mathf.Tau * (2.0f + line * 0.22f) + t * (0.46f + line * 0.06f)) * (5.0f + line);
				float ripple = Mathf.Sin(ratio * Mathf.Tau * 8.0f - t * 0.7f + line) * 1.8f;
				points[i] = new Vector2(x, y + wave + ripple);
			}

			DrawPolyline(points, new Color(0.78f, 1.0f, 0.96f, 0.16f - line * 0.012f), 1.65f, true);
		}
	}

	private void DrawCurrentRibbons()
	{
		float t = (float)_time;
		for (int ribbon = 0; ribbon < 8; ribbon++)
		{
			float yBase = Size.Y * (WaterlineRatio + 0.04f + ribbon * 0.07f);
			float drift = Mathf.PosMod(t * (42.0f + ribbon * 8.0f) + ribbon * Size.X * 0.18f, Size.X * 1.18f) - Size.X * 0.14f;
			Vector2[] points = new Vector2[34];
			for (int i = 0; i < points.Length; i++)
			{
				float ratio = i / (float)(points.Length - 1);
				float x = drift + ratio * Size.X * 0.46f;
				float y = yBase + ratio * Size.Y * 0.16f + Mathf.Sin(ratio * Mathf.Tau * 1.6f + t * 0.9f + ribbon) * 9.0f;
				points[i] = new Vector2(x, y);
			}

			float alpha = 0.07f + Mathf.Sin(t * 0.55f + ribbon) * 0.024f;
			DrawPolyline(points, new Color(0.62f, 1.0f, 0.94f, Mathf.Max(alpha, 0.025f)), 2.2f, true);
		}
	}

	private void DrawCausticRibbons()
	{
		float t = (float)_time;
		for (int ribbon = 0; ribbon < 7; ribbon++)
		{
			float y = Size.Y * (0.58f + ribbon * 0.045f);
			Vector2[] points = new Vector2[46];
			for (int i = 0; i < points.Length; i++)
			{
				float ratio = i / (float)(points.Length - 1);
				float x = Size.X * (0.35f + ratio * 0.63f);
				float wave = Mathf.Sin(ratio * Mathf.Tau * 2.4f + t * 0.55f + ribbon) * (8.0f + ribbon * 0.9f);
				points[i] = new Vector2(x, y + wave);
			}
			DrawPolyline(points, new Color(0.66f, 1.0f, 0.9f, 0.08f), 1.6f, true);
		}
	}

	private void DrawBubbleStreams()
	{
		float t = (float)_time;
		float waterlineY = Size.Y * WaterlineRatio;
		float underwaterHeight = Size.Y - waterlineY;
		foreach (Bubble bubble in _bubbles)
		{
			float rise = Mathf.PosMod(bubble.YRatio * underwaterHeight + t * bubble.Speed, underwaterHeight + bubble.Radius * 4.0f);
			float x = Size.X * bubble.XRatio + Mathf.Sin(t * 0.82f + bubble.Phase) * bubble.Drift;
			float y = Size.Y + bubble.Radius - rise;
			if (y < waterlineY + bubble.Radius)
			{
				y += underwaterHeight;
			}
			float pulse = 0.72f + 0.28f * Mathf.Sin(t * 1.8f + bubble.Phase);
			Color fill = new(0.65f, 0.98f, 1.0f, bubble.Alpha * 0.15f * pulse);
			Color rim = new(0.75f, 1.0f, 1.0f, bubble.Alpha * 0.72f * pulse);

			DrawCircle(new Vector2(x, y), bubble.Radius, fill);
			DrawArc(new Vector2(x, y), bubble.Radius, -Mathf.Pi * 0.12f, Mathf.Pi * 1.16f, 18, rim, 1.25f, true);
			DrawCircle(new Vector2(x - bubble.Radius * 0.26f, y - bubble.Radius * 0.28f), Mathf.Max(1.0f, bubble.Radius * 0.18f), new Color(1.0f, 1.0f, 1.0f, bubble.Alpha * 0.56f));
		}
	}

	private void DrawGlints()
	{
		float t = (float)_time;
		foreach (Glint glint in _glints)
		{
			float pulse = Mathf.Max(0.0f, Mathf.Sin(t * glint.Speed + glint.Phase));
			if (pulse <= 0.04f)
			{
				continue;
			}

			Vector2 position = new(
				Size.X * glint.XRatio + Mathf.Sin(t * 0.18f + glint.Phase) * 12.0f,
				Size.Y * glint.YRatio + Mathf.Cos(t * 0.22f + glint.Phase) * 8.0f);
			float radius = glint.Radius * (0.8f + pulse * 0.7f);
			Color color = new(0.82f, 1.0f, 0.9f, glint.Alpha * pulse);
			DrawLine(position - new Vector2(radius, 0.0f), position + new Vector2(radius, 0.0f), color, 1.0f, true);
			DrawLine(position - new Vector2(0.0f, radius), position + new Vector2(0.0f, radius), color, 1.0f, true);
			DrawCircle(position, radius * 0.22f, color);
		}
	}

	private readonly record struct Bubble(float XRatio, float YRatio, float Radius, float Speed, float Alpha, float Phase, float Drift);
	private readonly record struct Glint(float XRatio, float YRatio, float Radius, float Speed, float Phase, float Alpha);
}
