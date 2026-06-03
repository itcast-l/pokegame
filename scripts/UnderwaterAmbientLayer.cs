using Godot;
using System.Collections.Generic;

public partial class UnderwaterAmbientLayer : Control
{
	[Export(PropertyHint.Range, "0,80,1")]
	public int bubble_count = 26;

	[Export(PropertyHint.Range, "0,140,1")]
	public int mote_count = 64;

	[Export(PropertyHint.Range, "0,80,1")]
	public int seagrass_blade_count = 44;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float motion_scale = 1.0f;

	[Export(PropertyHint.Range, "0.0,1.0,0.05")]
	public float edge_depth_alpha_scale = 1.0f;

	[Export(PropertyHint.Range, "1,60,1")]
	public int redraw_fps = 20;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float sunbeam_intensity = 0.0f;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float center_caustic_intensity = 0.0f;

	[Export(PropertyHint.Range, "0,80,1")]
	public int center_glint_count = 0;

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Bubble> _bubbles = new();
	private readonly List<Mote> _motes = new();
	private readonly List<SeagrassBlade> _seagrassBlades = new();
	private readonly List<CenterGlint> _centerGlints = new();
	private readonly Vector2[] _sunbeamPoints = new Vector2[4];
	private readonly Vector2[] _waterCurrentTopPoints = new Vector2[3];
	private readonly Vector2[] _waterCurrentBottomPoints = new Vector2[3];
	private readonly Vector2[] _centerCausticPoints = new Vector2[9];
	private readonly Vector2[] _seagrassStemPoints = new Vector2[5];
	private readonly Vector2[] _seagrassCenterLinePoints = new Vector2[3];
	private double _time;
	private double _redrawAccumulator;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 4107;
		RebuildAmbient();
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			RebuildAmbient();
			QueueRedraw();
		}
	}

	public override void _Process(double delta)
	{
		if (!IsVisibleInTree())
		{
			return;
		}

		_time += delta * motion_scale;
		_redrawAccumulator += delta;
		double redrawInterval = 1.0 / Mathf.Max(redraw_fps, 1);
		if (_redrawAccumulator < redrawInterval)
		{
			return;
		}

		_redrawAccumulator %= redrawInterval;
		QueueRedraw();
	}

	public override void _Draw()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		DrawEdgeDepth();
		DrawSunbeams();
		DrawSeagrass();
		DrawWaterCurrents();
		DrawCenterCaustics();
		DrawMotes();
		DrawCenterGlints();
		DrawBubbles();
	}

	private void RebuildAmbient()
	{
		if (Size.X <= 0.0f || Size.Y <= 0.0f)
		{
			return;
		}

		_bubbles.Clear();
		_motes.Clear();
		_seagrassBlades.Clear();
		_centerGlints.Clear();

		for (int i = 0; i < bubble_count; i++)
		{
			int side = _rng.RandiRange(0, 3);
			_bubbles.Add(new Bubble(
				EdgePosition(side, 0.18f),
				_rng.RandfRange(3.0f, 14.0f),
				_rng.RandfRange(0.15f, 0.5f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(6.0f, 24.0f),
				_rng.RandfRange(0.18f, 0.42f)));
		}

		for (int i = 0; i < mote_count; i++)
		{
			int side = _rng.RandiRange(0, 3);
			_motes.Add(new Mote(
				EdgePosition(side, 0.28f),
				_rng.RandfRange(0.8f, 2.4f),
				_rng.RandfRange(0.08f, 0.28f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(10.0f, 38.0f),
				_rng.RandfRange(0.1f, 0.28f)));
		}

		for (int i = 0; i < seagrass_blade_count; i++)
		{
			int side = i % 2 == 0 ? 2 : 3;
			Vector2 basePosition = EdgePosition(side, 0.12f);
			bool fromBottom = _rng.Randf() < 0.64f;
			basePosition.Y = fromBottom
				? _rng.RandfRange(Size.Y * 0.58f, Size.Y * 0.98f)
				: _rng.RandfRange(Size.Y * 0.04f, Size.Y * 0.36f);

			_seagrassBlades.Add(new SeagrassBlade(
				basePosition,
				_rng.RandfRange(Size.Y * 0.09f, Size.Y * 0.23f),
				_rng.RandfRange(3.0f, 8.0f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.45f, 0.95f),
				_rng.RandfRange(10.0f, 28.0f),
				new Color(
					_rng.RandfRange(0.10f, 0.20f),
					_rng.RandfRange(0.58f, 0.88f),
					_rng.RandfRange(0.58f, 0.82f),
					_rng.RandfRange(0.15f, 0.31f)),
				side == 3 ? -1.0f : 1.0f,
				!fromBottom));
		}

		for (int i = 0; i < center_glint_count; i++)
		{
			_centerGlints.Add(new CenterGlint(
				_rng.RandfRange(0.30f, 0.72f),
				_rng.RandfRange(0.25f, 0.66f),
				_rng.RandfRange(1.8f, 4.6f),
				_rng.RandfRange(0.55f, 1.45f),
				_rng.RandfRange(0.0f, Mathf.Tau),
				_rng.RandfRange(0.10f, 0.28f)));
		}
	}

	private Vector2 EdgePosition(int side, float bandRatio)
	{
		float bandX = Size.X * bandRatio;
		float bandY = Size.Y * bandRatio;

		return side switch
		{
			0 => new Vector2(_rng.RandfRange(0.0f, Size.X), _rng.RandfRange(0.0f, bandY)),
			1 => new Vector2(_rng.RandfRange(0.0f, Size.X), _rng.RandfRange(Size.Y - bandY, Size.Y)),
			2 => new Vector2(_rng.RandfRange(0.0f, bandX), _rng.RandfRange(0.0f, Size.Y)),
			_ => new Vector2(_rng.RandfRange(Size.X - bandX, Size.X), _rng.RandfRange(0.0f, Size.Y)),
		};
	}

	private void DrawEdgeDepth()
	{
		float pulse = 0.5f + 0.5f * Mathf.Sin((float)_time * 0.55f);
		float edgeAlpha = (0.14f + pulse * 0.035f) * edge_depth_alpha_scale;
		var deepTeal = new Color(0.0f, 0.13f, 0.19f, edgeAlpha);

		DrawRect(new Rect2(0.0f, 0.0f, Size.X, Size.Y * 0.13f), deepTeal);
		DrawRect(new Rect2(0.0f, Size.Y * 0.86f, Size.X, Size.Y * 0.14f), deepTeal);
		DrawRect(new Rect2(0.0f, 0.0f, Size.X * 0.09f, Size.Y), deepTeal);
		DrawRect(new Rect2(Size.X * 0.91f, 0.0f, Size.X * 0.09f, Size.Y), deepTeal);
	}

	private void DrawSunbeams()
	{
		if (sunbeam_intensity <= 0.0f)
		{
			return;
		}

		float t = (float)_time;
		for (int side = 0; side < 2; side++)
		{
			float sign = side == 0 ? 1.0f : -1.0f;
			float originX = side == 0 ? 0.0f : Size.X;
			for (int i = 0; i < 5; i++)
			{
				float phase = i * 1.27f + side * 2.4f;
				float pulse = 0.64f + 0.36f * Mathf.Sin(t * 0.46f + phase);
				float drift = Mathf.Sin(t * 0.12f + phase) * Size.X * 0.01f;
				float topOffset = Size.X * (0.035f + i * 0.032f);
				float bottomOffset = Size.X * (0.14f + i * 0.055f);
				float width = Size.X * (0.030f + i * 0.008f);
				float bottomY = Size.Y * (0.24f + i * 0.026f);
				float alpha = (0.035f + i * 0.006f) * pulse * sunbeam_intensity;

				_sunbeamPoints[0] = new Vector2(originX + sign * (topOffset - width + drift), 0.0f);
				_sunbeamPoints[1] = new Vector2(originX + sign * (topOffset + width * 1.4f + drift), 0.0f);
				_sunbeamPoints[2] = new Vector2(originX + sign * (bottomOffset + width * 2.4f + drift), bottomY);
				_sunbeamPoints[3] = new Vector2(originX + sign * (bottomOffset - width * 1.6f + drift), bottomY);
				DrawColoredPolygon(_sunbeamPoints, new Color(0.78f, 1.0f, 0.94f, Mathf.Clamp(alpha, 0.0f, 0.19f)));
			}
		}
	}

	private void DrawWaterCurrents()
	{
		var lineColor = new Color(0.55f, 0.95f, 1.0f, 0.12f);
		float topY = Size.Y * 0.075f;
		float bottomY = Size.Y * 0.925f;

		for (int i = 0; i < 10; i++)
		{
			float offset = i * Size.X / 9.0f;
			float sway = Mathf.Sin((float)_time * 0.45f + i * 0.7f) * 14.0f;
			_waterCurrentTopPoints[0] = new Vector2(offset - Size.X * 0.12f, topY + sway);
			_waterCurrentTopPoints[1] = new Vector2(offset, topY + 8.0f - sway * 0.25f);
			_waterCurrentTopPoints[2] = new Vector2(offset + Size.X * 0.12f, topY + sway * 0.35f);
			_waterCurrentBottomPoints[0] = new Vector2(offset - Size.X * 0.12f, bottomY - sway * 0.25f);
			_waterCurrentBottomPoints[1] = new Vector2(offset, bottomY - 10.0f + sway * 0.2f);
			_waterCurrentBottomPoints[2] = new Vector2(offset + Size.X * 0.12f, bottomY - sway);
			DrawPolyline(_waterCurrentTopPoints, lineColor, 1.25f, true);
			DrawPolyline(_waterCurrentBottomPoints, lineColor, 1.25f, true);
		}
	}

	private void DrawCenterCaustics()
	{
		if (center_caustic_intensity <= 0.0f)
		{
			return;
		}

		float t = (float)_time;
		for (int strand = 0; strand < 18; strand++)
		{
			int column = strand % 6;
			int row = strand / 6;
			float phase = strand * 1.37f;
			Vector2 center = new(
				Size.X * (0.31f + column * 0.078f + Mathf.Sin(t * 0.17f + phase) * 0.010f),
				Size.Y * (0.31f + row * 0.105f + Mathf.Cos(t * 0.21f + phase) * 0.014f));
			float angle = -0.44f + (strand % 5) * 0.22f + Mathf.Sin(t * 0.23f + phase) * 0.14f;
			float length = Size.X * (0.050f + (strand % 3) * 0.010f);
			Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
			Vector2 normal = new(-direction.Y, direction.X);

			for (int i = 0; i < _centerCausticPoints.Length; i++)
			{
				float ratio = i / (float)(_centerCausticPoints.Length - 1);
				float bend = Mathf.Sin(ratio * Mathf.Tau * 1.65f + t * (0.54f + strand * 0.012f) + phase) * (2.0f + (strand % 4) * 0.35f);
				_centerCausticPoints[i] = center + direction * ((ratio - 0.5f) * length) + normal * bend;
			}

			float pulse = 0.64f + 0.36f * Mathf.Sin(t * 0.58f + phase);
			float alpha = 0.050f * pulse * center_caustic_intensity;
			DrawPolyline(_centerCausticPoints, new Color(0.76f, 1.0f, 0.88f, Mathf.Clamp(alpha, 0.0f, 0.075f)), 1.25f, true);
		}
	}

	private void DrawSeagrass()
	{
		foreach (SeagrassBlade blade in _seagrassBlades)
		{
			float direction = blade.GrowsDown ? 1.0f : -1.0f;
			float wave = Mathf.Sin((float)_time * blade.Speed + blade.Phase);
			float sidePush = blade.Flip * (0.45f + 0.55f * Mathf.Sin((float)_time * blade.Speed * 0.7f + blade.Phase));
			Vector2 tip = blade.Base + new Vector2(wave * blade.Sway + sidePush * blade.Sway * 0.35f, blade.Height * direction);
			Vector2 mid = blade.Base + new Vector2(wave * blade.Sway * 0.42f + sidePush * blade.Sway * 0.16f, blade.Height * direction * 0.52f);
			var normal = new Vector2(blade.Width, 0.0f);
			_seagrassStemPoints[0] = blade.Base - normal;
			_seagrassStemPoints[1] = mid - normal * 0.45f;
			_seagrassStemPoints[2] = tip;
			_seagrassStemPoints[3] = mid + normal * 0.45f;
			_seagrassStemPoints[4] = blade.Base + normal;
			_seagrassCenterLinePoints[0] = blade.Base;
			_seagrassCenterLinePoints[1] = mid;
			_seagrassCenterLinePoints[2] = tip;

			DrawColoredPolygon(_seagrassStemPoints, blade.Color);
			DrawPolyline(_seagrassCenterLinePoints, new Color(0.76f, 1.0f, 0.88f, blade.Color.A * 0.62f), 1.1f, true);

			Vector2 glowPosition = blade.Base.Lerp(tip, 0.34f + 0.18f * Mathf.Sin((float)_time * blade.Speed + blade.Phase * 1.7f));
			DrawCircle(glowPosition, Mathf.Max(blade.Width * 0.42f, 1.6f), new Color(0.34f, 1.0f, 0.9f, blade.Color.A * 0.7f));
		}
	}

	private void DrawMotes()
	{
		foreach (Mote mote in _motes)
		{
			Vector2 position = mote.Base + new Vector2(
				Mathf.Sin((float)_time * mote.Speed + mote.Phase) * mote.Drift,
				Mathf.Cos((float)_time * mote.Speed * 0.8f + mote.Phase) * mote.Drift * 0.45f);
			DrawCircle(position, mote.Radius, new Color(0.72f, 1.0f, 0.95f, mote.Alpha));
		}
	}

	private void DrawCenterGlints()
	{
		if (center_caustic_intensity <= 0.0f)
		{
			return;
		}

		float t = (float)_time;
		foreach (CenterGlint glint in _centerGlints)
		{
			float pulse = Mathf.Max(0.0f, Mathf.Sin(t * glint.Speed + glint.Phase));
			if (pulse <= 0.08f)
			{
				continue;
			}

			Vector2 position = new(
				Size.X * glint.XRatio + Mathf.Sin(t * 0.18f + glint.Phase) * 10.0f,
				Size.Y * glint.YRatio + Mathf.Cos(t * 0.24f + glint.Phase) * 7.0f);
			float radius = glint.Radius * (0.72f + pulse * 0.95f);
			Color color = new(0.86f, 1.0f, 0.90f, glint.Alpha * pulse * center_caustic_intensity);
			DrawLine(position - new Vector2(radius, 0.0f), position + new Vector2(radius, 0.0f), color, 1.05f, true);
			DrawLine(position - new Vector2(0.0f, radius), position + new Vector2(0.0f, radius), color, 1.05f, true);
			DrawCircle(position, radius * 0.22f, color);
		}
	}

	private void DrawBubbles()
	{
		foreach (Bubble bubble in _bubbles)
		{
			float rise = Mathf.PosMod((float)_time * bubble.Speed * 34.0f + bubble.Phase * 18.0f, Size.Y * 0.24f);
			Vector2 position = bubble.Base + new Vector2(
				Mathf.Sin((float)_time * bubble.Speed + bubble.Phase) * bubble.Drift,
				-rise + Mathf.Cos((float)_time * bubble.Speed * 1.4f + bubble.Phase) * 5.0f);

			if (position.Y < -bubble.Radius)
			{
				position.Y += Size.Y + bubble.Radius * 2.0f;
			}

			DrawCircle(position, bubble.Radius, new Color(0.72f, 0.98f, 1.0f, bubble.Alpha * 0.18f));
			DrawArc(position, bubble.Radius, -Mathf.Pi * 0.15f, Mathf.Pi * 1.18f, 10, new Color(0.76f, 1.0f, 1.0f, bubble.Alpha), 1.4f, true);
			DrawCircle(position + new Vector2(-bubble.Radius * 0.28f, -bubble.Radius * 0.25f), Mathf.Max(bubble.Radius * 0.18f, 1.0f), new Color(1.0f, 1.0f, 1.0f, bubble.Alpha * 0.75f));
		}
	}

	private readonly record struct Bubble(Vector2 Base, float Radius, float Speed, float Phase, float Drift, float Alpha);
	private readonly record struct Mote(Vector2 Base, float Radius, float Speed, float Phase, float Drift, float Alpha);
	private readonly record struct SeagrassBlade(Vector2 Base, float Height, float Width, float Phase, float Speed, float Sway, Color Color, float Flip, bool GrowsDown);
	private readonly record struct CenterGlint(float XRatio, float YRatio, float Radius, float Speed, float Phase, float Alpha);
}
