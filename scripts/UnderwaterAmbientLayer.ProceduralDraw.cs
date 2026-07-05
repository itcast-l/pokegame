using Godot;

public partial class UnderwaterAmbientLayer
{
	private bool ShouldDrawProceduralSeagrass()
	{
		return _activeRenderMode != AmbientRenderMode.ImageBased || _seagrassFrames.Length == 0;
	}

	private bool ShouldDrawProceduralSunbeams()
	{
		return _activeRenderMode != AmbientRenderMode.ImageBased || _sunbeamFrames.Length == 0;
	}

	private bool ShouldDrawProceduralWaterCurrents()
	{
		return _activeRenderMode != AmbientRenderMode.ImageBased || _waterCurrentFrames.Length == 0;
	}

	private bool ShouldDrawProceduralCenterCaustics()
	{
		return _activeRenderMode != AmbientRenderMode.ImageBased || _centerCausticFrames.Length == 0;
	}

	private bool ShouldDrawProceduralMotes()
	{
		return _activeRenderMode != AmbientRenderMode.ImageBased || _moteFrames.Length == 0;
	}

	private bool ShouldDrawProceduralCenterGlints()
	{
		return _activeRenderMode != AmbientRenderMode.ImageBased || _centerGlintFrames.Length == 0;
	}

	private bool ShouldQueueRedrawForImageFallback()
	{
		return ShouldDrawProceduralSunbeams()
			|| ShouldDrawProceduralSeagrass()
			|| ShouldDrawProceduralWaterCurrents()
			|| ShouldDrawProceduralCenterCaustics()
			|| ShouldDrawProceduralMotes()
			|| ShouldDrawProceduralCenterGlints();
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
}
