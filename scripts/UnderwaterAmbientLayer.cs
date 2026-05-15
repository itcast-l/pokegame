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

	private readonly RandomNumberGenerator _rng = new();
	private readonly List<Bubble> _bubbles = new();
	private readonly List<Mote> _motes = new();
	private readonly List<SeagrassBlade> _seagrassBlades = new();
	private double _time;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		_rng.Seed = 4107;
		RebuildAmbient();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			RebuildAmbient();
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

		DrawEdgeDepth();
		DrawSeagrass();
		DrawWaterCurrents();
		DrawMotes();
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
		float edgeAlpha = 0.14f + pulse * 0.035f;
		var deepTeal = new Color(0.0f, 0.13f, 0.19f, edgeAlpha);

		DrawRect(new Rect2(0.0f, 0.0f, Size.X, Size.Y * 0.13f), deepTeal);
		DrawRect(new Rect2(0.0f, Size.Y * 0.86f, Size.X, Size.Y * 0.14f), deepTeal);
		DrawRect(new Rect2(0.0f, 0.0f, Size.X * 0.09f, Size.Y), deepTeal);
		DrawRect(new Rect2(Size.X * 0.91f, 0.0f, Size.X * 0.09f, Size.Y), deepTeal);
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
			Vector2[] topPoints =
			{
				new(offset - Size.X * 0.12f, topY + sway),
				new(offset, topY + 8.0f - sway * 0.25f),
				new(offset + Size.X * 0.12f, topY + sway * 0.35f),
			};
			Vector2[] bottomPoints =
			{
				new(offset - Size.X * 0.12f, bottomY - sway * 0.25f),
				new(offset, bottomY - 10.0f + sway * 0.2f),
				new(offset + Size.X * 0.12f, bottomY - sway),
			};
			DrawPolyline(topPoints, lineColor, 1.25f, true);
			DrawPolyline(bottomPoints, lineColor, 1.25f, true);
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
			Vector2[] stem =
			{
				blade.Base - normal,
				mid - normal * 0.45f,
				tip,
				mid + normal * 0.45f,
				blade.Base + normal,
			};

			DrawColoredPolygon(stem, blade.Color);
			DrawPolyline(new[] { blade.Base, mid, tip }, new Color(0.76f, 1.0f, 0.88f, blade.Color.A * 0.62f), 1.1f, true);

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
			DrawArc(position, bubble.Radius, -Mathf.Pi * 0.15f, Mathf.Pi * 1.18f, 18, new Color(0.76f, 1.0f, 1.0f, bubble.Alpha), 1.4f, true);
			DrawCircle(position + new Vector2(-bubble.Radius * 0.28f, -bubble.Radius * 0.25f), Mathf.Max(bubble.Radius * 0.18f, 1.0f), new Color(1.0f, 1.0f, 1.0f, bubble.Alpha * 0.75f));
		}
	}

	private readonly record struct Bubble(Vector2 Base, float Radius, float Speed, float Phase, float Drift, float Alpha);
	private readonly record struct Mote(Vector2 Base, float Radius, float Speed, float Phase, float Drift, float Alpha);
	private readonly record struct SeagrassBlade(Vector2 Base, float Height, float Width, float Phase, float Speed, float Sway, Color Color, float Flip, bool GrowsDown);
}
