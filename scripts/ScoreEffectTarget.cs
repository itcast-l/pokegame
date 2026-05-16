using Godot;

[Tool]
public partial class ScoreEffectTarget : Marker2D
{
	private readonly Color _fillColor = new(1.0f, 1.0f, 1.0f, 0.08f);
	private readonly Color _rimColor = new(1.0f, 1.0f, 1.0f, 0.42f);
	private readonly Color _crossColor = new(1.0f, 1.0f, 1.0f, 0.58f);

	public Vector2 MarkerGlobalPosition => GlobalPosition;

	public override void _Draw()
	{
		if (!Engine.IsEditorHint())
		{
			return;
		}

		Vector2 center = Vector2.Zero;
		const float radius = 20.0f;
		DrawCircle(center, radius, _fillColor);
		DrawArc(center, radius, 0.0f, Mathf.Tau, 48, _rimColor, 2.0f, true);
		DrawLine(new Vector2(center.X - radius * 0.45f, center.Y), new Vector2(center.X + radius * 0.45f, center.Y), _crossColor, 1.5f, true);
		DrawLine(new Vector2(center.X, center.Y - radius * 0.45f), new Vector2(center.X, center.Y + radius * 0.45f), _crossColor, 1.5f, true);
	}
}
