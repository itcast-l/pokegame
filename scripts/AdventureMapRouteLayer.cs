using System.Collections.Generic;
using Godot;

public partial class AdventureMapRouteLayer : Control
{
	private static readonly Vector2[] FallbackRoute =
	{
		new(360.0f, 1230.0f),
		new(560.0f, 1055.0f),
		new(770.0f, 870.0f),
		new(990.0f, 710.0f),
		new(1240.0f, 610.0f),
		new(1500.0f, 560.0f),
		new(1770.0f, 575.0f),
		new(2050.0f, 665.0f),
		new(2330.0f, 815.0f),
		new(2580.0f, 1015.0f),
		new(2780.0f, 1230.0f),
	};

	[Export]
	public Color RouteColor { get; set; } = new(0.62f, 1.0f, 0.9f, 0.68f);

	[Export]
	public Color RouteShadowColor { get; set; } = new(0.0f, 0.04f, 0.06f, 0.72f);

	[Export(PropertyHint.Range, "1.0,18.0,0.5")]
	public float RouteWidth { get; set; } = 5.0f;

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		QueueRedraw();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			QueueRedraw();
		}
	}

	public override void _Draw()
	{
		List<Vector2> routePoints = GetRoutePoints();
		if (routePoints.Count < 2)
		{
			return;
		}

		Vector2[] points = routePoints.ToArray();
		DrawPolyline(points, RouteShadowColor, RouteWidth + 5.0f, true);
		DrawPolyline(points, RouteColor, RouteWidth, true);

		foreach (Vector2 point in points)
		{
			DrawCircle(point, RouteWidth * 2.0f, new Color(0.0f, 0.05f, 0.07f, 0.58f));
			DrawCircle(point, RouteWidth * 1.18f, new Color(0.86f, 1.0f, 0.92f, 0.88f));
		}
	}

	private List<Vector2> GetRoutePoints()
	{
		var routePoints = new List<Vector2>();
		Control? avatarLayer = GetParent()?.GetNodeOrNull<Control>("AvatarLayer");
		if (avatarLayer != null)
		{
			foreach (Node child in avatarLayer.GetChildren())
			{
				if (child is Control control && control.Visible)
				{
					routePoints.Add(control.Position + control.Size * 0.5f);
				}
			}
		}

		if (routePoints.Count >= 2)
		{
			routePoints.Sort((left, right) => left.X.CompareTo(right.X));
			return routePoints;
		}

		routePoints.AddRange(FallbackRoute);
		return routePoints;
	}
}
