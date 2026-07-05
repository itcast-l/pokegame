using Godot;

public partial class MainMenuOceanDynamics
{
	private void DrawSunbeams()
	{
		float t = (float)_time;
		for (int i = 0; i < 5; i++)
		{
			float x = Size.X * (0.47f + i * 0.12f + Mathf.Sin(t * 0.08f + i) * 0.015f);
			float width = Size.X * (0.07f + i * 0.014f);
			float alpha = 0.062f + Mathf.Sin(t * 0.42f + i * 1.7f) * 0.024f;
			_sunbeamPoints[0] = new Vector2(x - width * 0.3f, 0.0f);
			_sunbeamPoints[1] = new Vector2(x + width * 0.8f, 0.0f);
			_sunbeamPoints[2] = new Vector2(x + width * 1.65f, Size.Y);
			_sunbeamPoints[3] = new Vector2(x - width * 1.1f, Size.Y);
			DrawColoredPolygon(_sunbeamPoints, new Color(0.74f, 1.0f, 0.96f, Mathf.Max(alpha, 0.0f)));
		}
	}

	private void DrawSurfaceWake()
	{
		float t = (float)_time;
		float baseY = Size.Y * 0.315f;
		for (int line = 0; line < 6; line++)
		{
			float y = baseY + line * Size.Y * 0.018f;
			for (int i = 0; i < _surfaceWakePoints.Length; i++)
			{
				float ratio = i / (float)(_surfaceWakePoints.Length - 1);
				float x = ratio * Size.X;
				float wave = Mathf.Sin(ratio * Mathf.Tau * (2.0f + line * 0.22f) + t * (0.46f + line * 0.06f)) * (5.0f + line);
				float ripple = Mathf.Sin(ratio * Mathf.Tau * 8.0f - t * 0.7f + line) * 1.8f;
				_surfaceWakePoints[i] = new Vector2(x, y + wave + ripple);
			}

			DrawPolyline(_surfaceWakePoints, new Color(0.78f, 1.0f, 0.96f, 0.16f - line * 0.012f), 1.65f, true);
		}
	}

	private void DrawCurrentRibbons()
	{
		float t = (float)_time;
		for (int ribbon = 0; ribbon < 8; ribbon++)
		{
			float yBase = Size.Y * (WaterlineRatio + 0.04f + ribbon * 0.07f);
			float drift = Mathf.PosMod(t * (42.0f + ribbon * 8.0f) + ribbon * Size.X * 0.18f, Size.X * 1.18f) - Size.X * 0.14f;
			for (int i = 0; i < _currentRibbonPoints.Length; i++)
			{
				float ratio = i / (float)(_currentRibbonPoints.Length - 1);
				float x = drift + ratio * Size.X * 0.46f;
				float y = yBase + ratio * Size.Y * 0.16f + Mathf.Sin(ratio * Mathf.Tau * 1.6f + t * 0.9f + ribbon) * 9.0f;
				_currentRibbonPoints[i] = new Vector2(x, y);
			}

			float alpha = 0.07f + Mathf.Sin(t * 0.55f + ribbon) * 0.024f;
			DrawPolyline(_currentRibbonPoints, new Color(0.62f, 1.0f, 0.94f, Mathf.Max(alpha, 0.025f)), 2.2f, true);
		}
	}

	private void DrawCausticRibbons()
	{
		float t = (float)_time;
		for (int ribbon = 0; ribbon < 7; ribbon++)
		{
			float y = Size.Y * (0.58f + ribbon * 0.045f);
			for (int i = 0; i < _causticRibbonPoints.Length; i++)
			{
				float ratio = i / (float)(_causticRibbonPoints.Length - 1);
				float x = Size.X * (0.35f + ratio * 0.63f);
				float wave = Mathf.Sin(ratio * Mathf.Tau * 2.4f + t * 0.55f + ribbon) * (8.0f + ribbon * 0.9f);
				_causticRibbonPoints[i] = new Vector2(x, y + wave);
			}
			DrawPolyline(_causticRibbonPoints, new Color(0.66f, 1.0f, 0.9f, 0.08f), 1.6f, true);
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

	private void DrawBubbles()
	{
		Texture2D? bubbleTexture = GetBubbleTexture();
		foreach (Bubble bubble in _bubbles)
		{
			if (!TryGetBubbleLayout(bubble, out Vector2 position, out float radius, out float alpha))
			{
				continue;
			}

			if (bubbleTexture != null)
			{
				DrawBubbleTexture(bubbleTexture, position, radius, alpha);
				continue;
			}

			DrawProceduralBubble(position, radius, alpha);
		}
	}

	private void DrawBubbleTexture(Texture2D bubbleTexture, Vector2 position, float radius, float alpha)
	{
		float side = Mathf.Max(radius * 3.35f, 8.0f);
		Vector2 size = new(side, side);
		Color modulate = new(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha * 1.22f, 0.0f, 1.0f));
		DrawTextureRect(bubbleTexture, new Rect2(position - size / 2.0f, size), false, modulate, false);
	}

	private void DrawProceduralBubble(Vector2 position, float radius, float alpha)
	{
		Color aura = new(0.62f, 0.95f, 1.0f, alpha * 0.10f);
		Color ring = new(0.86f, 1.0f, 1.0f, alpha * 0.55f);
		Color core = new(1.0f, 1.0f, 1.0f, alpha * 0.28f);

		DrawCircle(position, radius * 1.18f, aura);
		DrawArc(position, radius, 0.0f, Mathf.Tau, 32, ring, Mathf.Max(1.0f, radius * 0.12f), true);
		DrawCircle(position + new Vector2(-radius * 0.24f, -radius * 0.22f), Mathf.Max(1.0f, radius * 0.14f), core);
	}

	private bool ShouldDrawProceduralSunbeams()
	{
		return _activeRenderMode != OceanRenderMode.ImageBased || _sunbeamFrames.Length == 0;
	}

	private bool ShouldDrawProceduralSurfaceWake()
	{
		return _activeRenderMode != OceanRenderMode.ImageBased || _surfaceWakeFrames.Length == 0;
	}

	private bool ShouldDrawProceduralCurrentRibbons()
	{
		return _activeRenderMode != OceanRenderMode.ImageBased || _currentRibbonFrames.Length == 0;
	}

	private bool ShouldDrawProceduralCausticRibbons()
	{
		return _activeRenderMode != OceanRenderMode.ImageBased || _causticRibbonFrames.Length == 0;
	}

	private bool ShouldDrawProceduralGlints()
	{
		return _activeRenderMode != OceanRenderMode.ImageBased || _glintFrames.Length == 0;
	}

	private bool ShouldDrawBubbles()
	{
		if (_bubbles.Count == 0)
		{
			return false;
		}

		return _activeRenderMode != OceanRenderMode.ImageBased || GetBubbleTexture() == null;
	}

	private bool ShouldQueueRedrawInImageMode()
	{
		return ShouldDrawProceduralSunbeams()
			|| ShouldDrawProceduralSurfaceWake()
			|| ShouldDrawProceduralCurrentRibbons()
			|| ShouldDrawProceduralCausticRibbons()
			|| ShouldDrawProceduralGlints()
			|| ShouldDrawBubbles();
	}
}
