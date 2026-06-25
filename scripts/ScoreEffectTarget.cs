using Godot;

[Tool]
public partial class ScoreEffectTarget : Marker2D
{
	private readonly Color _fillColor = new(1.0f, 1.0f, 1.0f, 0.08f);
	private readonly Color _rimColor = new(1.0f, 1.0f, 1.0f, 0.42f);
	private readonly Color _crossColor = new(1.0f, 1.0f, 1.0f, 0.58f);

	private Texture2D? _markerTexture;

	[Export]
	public Texture2D? MarkerTexture
	{
		get => _markerTexture;
		set
		{
			if (_markerTexture == value)
			{
				return;
			}

			_markerTexture = value;
			if (IsInsideTree())
			{
				QueueRedraw();
			}
		}
	}

	private float _markerSize = 40.0f;

	[Export(PropertyHint.Range, "16,128,1")]
	public float MarkerSize
	{
		get => _markerSize;
		set
		{
			_markerSize = Mathf.Clamp(value, 16.0f, 128.0f);
			if (IsInsideTree())
			{
				QueueRedraw();
			}
		}
	}

	public Vector2 MarkerGlobalPosition => GlobalPosition;

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
		{
			return;
		}

		Vector2 center = Vector2.Zero;
		float radius = MarkerSize * 0.5f;
		if (MarkerTexture != null)
		{
			Vector2 size = new(MarkerSize, MarkerSize);
			DrawTextureRect(MarkerTexture, new Rect2(center - size / 2.0f, size), false);
			return;
		}

		DrawCircle(center, radius, _fillColor);
		DrawArc(center, radius, 0.0f, Mathf.Tau, 48, _rimColor, 2.0f, true);
		DrawLine(new Vector2(center.X - radius * 0.45f, center.Y), new Vector2(center.X + radius * 0.45f, center.Y), _crossColor, 1.5f, true);
		DrawLine(new Vector2(center.X, center.Y - radius * 0.45f), new Vector2(center.X, center.Y + radius * 0.45f), _crossColor, 1.5f, true);
	}
}
