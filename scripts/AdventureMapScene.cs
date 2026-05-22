using Godot;

public partial class AdventureMapScene : Control
{
	private static readonly Vector2 MapContentSize = new(3072.0f, 1536.0f);

	[Export]
	public string? MapDataPath { get; set; }

	[Export]
	public string? MapBaseTexturePath { get; set; }

	[Export]
	public string? selected_avatar_id { get; set; }

	private Control? _mapViewport;
	private Control? _mapContent;
	private TextureRect? _backgroundArt;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Stop;
		_mapViewport = GetNodeOrNull<Control>("MapViewport");
		_mapContent = GetNodeOrNull<Control>("MapViewport/MapContent");
		_backgroundArt = GetNodeOrNull<TextureRect>("MapViewport/MapContent/BackgroundArt");

		ApplyBaseTexture();
		ApplyViewportLayout();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized && IsNodeReady())
		{
			ApplyViewportLayout();
		}
	}

	private void ApplyBaseTexture()
	{
		if (_backgroundArt == null || string.IsNullOrWhiteSpace(MapBaseTexturePath))
		{
			return;
		}

		Texture2D? texture = GD.Load<Texture2D>(MapBaseTexturePath);
		if (texture == null)
		{
			GD.PushWarning($"Adventure map base texture not found: {MapBaseTexturePath}");
			return;
		}

		_backgroundArt.Texture = texture;
	}

	private void ApplyViewportLayout()
	{
		if (_mapViewport == null || _mapContent == null)
		{
			return;
		}

		Vector2 viewportSize = Size;
		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
		{
			viewportSize = GetViewportRect().Size;
		}

		if (viewportSize.X <= 0.0f || viewportSize.Y <= 0.0f)
		{
			return;
		}

		_mapViewport.SetAnchorsPreset(LayoutPreset.FullRect);
		_mapViewport.OffsetLeft = 0.0f;
		_mapViewport.OffsetTop = 0.0f;
		_mapViewport.OffsetRight = 0.0f;
		_mapViewport.OffsetBottom = 0.0f;

		float scale = Mathf.Min(viewportSize.X / MapContentSize.X, viewportSize.Y / MapContentSize.Y);
		Vector2 scaledSize = MapContentSize * scale;
		_mapContent.Position = (viewportSize - scaledSize) * 0.5f;
		_mapContent.Scale = Vector2.One * scale;
		_mapContent.Size = MapContentSize;
		_mapContent.CustomMinimumSize = MapContentSize;
	}
}
