using System;
using System.Collections.Generic;
using System.IO;
using Godot;

public partial class MainMenuOceanDynamics
{
	private Texture2D? GetBubbleTexture()
	{
		if (_bubbleTextureLoadAttempted)
		{
			return _bubbleTexture;
		}

		_bubbleTextureLoadAttempted = true;
		_bubbleTexture = GD.Load<Texture2D>(BubbleTexturePath);
		if (_bubbleTexture != null)
		{
			return _bubbleTexture;
		}

		string globalPath = ProjectSettings.GlobalizePath(BubbleTexturePath);
		if (File.Exists(globalPath))
		{
			Image image = Image.LoadFromFile(globalPath);
			if (!image.IsEmpty())
			{
				_bubbleTexture = ImageTexture.CreateFromImage(image);
			}
		}

		if (_bubbleTexture == null)
		{
			GD.PushWarning($"Missing main menu bubble texture: {BubbleTexturePath}");
		}

		return _bubbleTexture;
	}

	private void EnsureImageAssetsLoaded(bool retryIfAllMissing = false)
	{
		if (_imageAssetsLoaded && !(retryIfAllMissing && AllImageFramesMissing()))
		{
			return;
		}

		_sunbeamFrames = FrameAnimationHelpers.LoadTextureFrames(SunbeamFramePattern, SunbeamFrameCount, "main menu sunbeam", preferImageFile: true);
		_surfaceWakeFrames = FrameAnimationHelpers.LoadTextureFrames(SurfaceWakeFramePattern, SurfaceWakeFrameCount, "main menu surface wake", preferImageFile: true);
		_currentRibbonFrames = FrameAnimationHelpers.LoadTextureFrames(CurrentRibbonFramePattern, CurrentRibbonFrameCount, "main menu current ribbon", preferImageFile: true);
		_causticRibbonFrames = FrameAnimationHelpers.LoadTextureFrames(CausticRibbonFramePattern, CausticRibbonFrameCount, "main menu caustic ribbon", preferImageFile: true);
		_glintFrames = FrameAnimationHelpers.LoadTextureFrames(GlintFramePattern, GlintFrameCount, "main menu glint", preferImageFile: true);
		_ = GetBubbleTexture();
		_imageAssetsLoaded = true;
	}

	private bool AllImageFramesMissing()
	{
		return _sunbeamFrames.Length == 0
			&& _surfaceWakeFrames.Length == 0
			&& _currentRibbonFrames.Length == 0
			&& _causticRibbonFrames.Length == 0
			&& _glintFrames.Length == 0
			&& _bubbleTexture == null;
	}

	private bool HasNoImageInstances()
	{
		return _sunbeamInstances.Count == 0
			&& _surfaceWakeInstances.Count == 0
			&& _currentRibbonInstances.Count == 0
			&& _causticRibbonInstances.Count == 0
			&& _glintInstances.Count == 0
			&& _bubbleImageInstances.Count == 0;
	}

	private void EnsureImageLayers()
	{
		GetOrCreateLayer(ref _sunbeamLayer, "SunbeamLayer");
		GetOrCreateLayer(ref _surfaceWakeLayer, "SurfaceWakeLayer");
		GetOrCreateLayer(ref _currentRibbonLayer, "CurrentRibbonLayer");
		GetOrCreateLayer(ref _causticRibbonLayer, "CausticRibbonLayer");
		GetOrCreateLayer(ref _glintLayer, "GlintLayer");
		GetOrCreateLayer(ref _bubbleLayer, "BubbleLayer");
	}

	private void SetImageLayersVisible(bool visible)
	{
		SetLayerVisible(_sunbeamLayer, visible);
		SetLayerVisible(_surfaceWakeLayer, visible);
		SetLayerVisible(_currentRibbonLayer, visible);
		SetLayerVisible(_causticRibbonLayer, visible);
		SetLayerVisible(_glintLayer, visible);
		SetLayerVisible(_bubbleLayer, visible);
	}

	private Control GetOrCreateLayer(ref Control? layer, string name)
	{
		if (layer != null && GodotObject.IsInstanceValid(layer))
		{
			layer.Visible = true;
			return layer;
		}

		layer = new Control
		{
			Name = name,
			MouseFilter = MouseFilterEnum.Ignore,
			AnchorRight = 1.0f,
			AnchorBottom = 1.0f,
			GrowHorizontal = Control.GrowDirection.Both,
			GrowVertical = Control.GrowDirection.Both,
		};
		layer.SetAnchorsPreset(LayoutPreset.FullRect);
		layer.OffsetRight = 0.0f;
		layer.OffsetBottom = 0.0f;
		AddChild(layer);
		return layer;
	}

	private static void SetLayerVisible(Control? layer, bool visible)
	{
		if (layer != null && GodotObject.IsInstanceValid(layer))
		{
			layer.Visible = visible;
		}
	}

	private void RebuildImageInstances()
	{
		ClearImageInstances();
		BuildSunbeamInstances();
		BuildSurfaceWakeInstances();
		BuildCurrentRibbonInstances();
		BuildCausticRibbonInstances();
		BuildGlintInstances();
		BuildBubbleImageInstances();
	}

	private void ClearImageInstances()
	{
		ClearImageLayer(_sunbeamLayer, _sunbeamInstances);
		ClearImageLayer(_surfaceWakeLayer, _surfaceWakeInstances);
		ClearImageLayer(_currentRibbonLayer, _currentRibbonInstances);
		ClearImageLayer(_causticRibbonLayer, _causticRibbonInstances);
		ClearImageLayer(_glintLayer, _glintInstances);
		ClearBubbleLayer(_bubbleLayer, _bubbleImageInstances);
	}

	private static void ClearImageLayer(Control? layer, List<ImageElement> instances)
	{
		instances.Clear();
		if (layer == null || !GodotObject.IsInstanceValid(layer))
		{
			return;
		}

		while (layer.GetChildCount() > 0)
		{
			layer.GetChild(0).Free();
		}
	}

	private static void ClearBubbleLayer(Control? layer, List<BubbleImageElement> instances)
	{
		instances.Clear();
		if (layer == null || !GodotObject.IsInstanceValid(layer))
		{
			return;
		}

		while (layer.GetChildCount() > 0)
		{
			layer.GetChild(0).Free();
		}
	}

	private void BuildSunbeamInstances()
	{
		if (_sunbeamLayer == null || _sunbeamFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < SunbeamInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_sunbeamLayer,
				$"Sunbeam{index + 1}",
				_sunbeamFrames,
				SunbeamFrameSize,
				SunbeamFramesPerSecond);
			instance.XRatio = 0.56f + index * 0.08f + _rng.RandfRange(-0.008f, 0.008f);
			instance.Scale = 0.44f + index * 0.035f;
			instance.Alpha = 0.028f + index * 0.004f;
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.01f, 0.03f);
			instance.Drift = _rng.RandfRange(0.0f, 0.003f);
			instance.Secondary = _rng.RandfRange(0.01f, 0.02f);
			_sunbeamInstances.Add(instance);
		}
	}

	private void BuildSurfaceWakeInstances()
	{
		if (_surfaceWakeLayer == null || _surfaceWakeFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < SurfaceWakeInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_surfaceWakeLayer,
				$"SurfaceWake{index + 1}",
				_surfaceWakeFrames,
				SurfaceWakeFrameSize,
				SurfaceWakeFramesPerSecond);
			instance.XRatio = 0.56f + _rng.RandfRange(-0.035f, 0.035f);
			instance.YRatio = 0.304f + index * 0.010f + _rng.RandfRange(-0.0025f, 0.0025f);
			instance.Scale = 0.30f + index * 0.025f;
			instance.Alpha = 0.050f - index * 0.005f;
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.02f, 0.05f);
			instance.Drift = _rng.RandfRange(1.0f, 3.0f);
			_surfaceWakeInstances.Add(instance);
		}
	}

	private void BuildCurrentRibbonInstances()
	{
		if (_currentRibbonLayer == null || _currentRibbonFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < CurrentRibbonInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_currentRibbonLayer,
				$"CurrentRibbon{index + 1}",
				_currentRibbonFrames,
				CurrentRibbonFrameSize,
				CurrentRibbonFramesPerSecond);
			instance.XRatio = -0.18f + index * 0.11f;
			instance.YRatio = WaterlineRatio + 0.08f + index * 0.045f + _rng.RandfRange(-0.006f, 0.006f);
			instance.Scale = 0.14f + _rng.RandfRange(0.025f, 0.06f);
			instance.Alpha = 0.042f + _rng.RandfRange(0.004f, 0.014f);
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = 0.025f + index * 0.004f;
			instance.Drift = _rng.RandfRange(2.0f, 5.0f);
			_currentRibbonInstances.Add(instance);
		}
	}

	private void BuildCausticRibbonInstances()
	{
		if (_causticRibbonLayer == null || _causticRibbonFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < CausticRibbonInstanceCount; index++)
		{
			var instance = CreateImageElement(
				_causticRibbonLayer,
				$"CausticRibbon{index + 1}",
				_causticRibbonFrames,
				CausticRibbonFrameSize,
				CausticRibbonFramesPerSecond);
			instance.XRatio = 0.58f + _rng.RandfRange(-0.08f, 0.08f);
			instance.YRatio = 0.58f + index * 0.045f + _rng.RandfRange(-0.008f, 0.008f);
			instance.Scale = 0.28f + _rng.RandfRange(0.04f, 0.12f);
			instance.Alpha = 0.07f + _rng.RandfRange(0.0f, 0.02f);
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.18f, 0.32f);
			instance.Drift = _rng.RandfRange(2.0f, 6.0f);
			_causticRibbonInstances.Add(instance);
		}
	}

	private void BuildGlintInstances()
	{
		if (_glintLayer == null || _glintFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < _glints.Count; index++)
		{
			Glint glint = _glints[index];
			var instance = CreateImageElement(
				_glintLayer,
				$"Glint{index + 1}",
				_glintFrames,
				GlintFrameSize,
				GlintFramesPerSecond);
			instance.XRatio = glint.XRatio;
			instance.YRatio = glint.YRatio;
			instance.Scale = glint.Radius / 10.0f;
			instance.Alpha = glint.Alpha;
			instance.Phase = glint.Phase;
			instance.Speed = glint.Speed;
			_glintInstances.Add(instance);
		}
	}

	private void BuildBubbleImageInstances()
	{
		if (_bubbleLayer == null || _bubbles.Count == 0)
		{
			return;
		}

		Texture2D? bubbleTexture = GetBubbleTexture();
		if (bubbleTexture == null)
		{
			return;
		}

		for (int index = 0; index < _bubbles.Count; index++)
		{
			_bubbleImageInstances.Add(CreateBubbleImageElement(
				_bubbleLayer,
				$"Bubble{index + 1}",
				bubbleTexture,
				_bubbles[index]));
		}
	}

	private ImageElement CreateImageElement(
		Control layer,
		string name,
		Texture2D[] frames,
		Vector2 baseSize,
		float framesPerSecond)
	{
		var node = new TextureRect
		{
			Name = name,
			Texture = frames.Length > 0 ? frames[0] : null,
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Size = baseSize,
			PivotOffset = baseSize / 2.0f,
		};
		layer.AddChild(node);
		return new ImageElement
		{
			Node = node,
			Frames = frames,
			BaseSize = baseSize,
			FramesPerSecond = framesPerSecond,
		};
	}

	private BubbleImageElement CreateBubbleImageElement(
		Control layer,
		string name,
		Texture2D bubbleTexture,
		Bubble bubble)
	{
		var node = new TextureRect
		{
			Name = name,
			Texture = bubbleTexture,
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			Size = Vector2.One * 8.0f,
			PivotOffset = Vector2.One * 4.0f,
		};
		layer.AddChild(node);
		return new BubbleImageElement
		{
			Node = node,
			Bubble = bubble,
		};
	}

	private void UpdateImageInstances()
	{
		UpdateSunbeamInstances();
		UpdateSurfaceWakeInstances();
		UpdateCurrentRibbonInstances();
		UpdateCausticRibbonInstances();
		UpdateGlintInstances();
		UpdateBubbleImageInstances();
	}

	private void UpdateSunbeamInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _sunbeamInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float widthPulse = 0.72f + Mathf.Sin(time * 0.18f + instance.Phase) * instance.Secondary;
			float scale = (Size.Y / instance.BaseSize.Y) * instance.Scale;
			Vector2 size = new(instance.BaseSize.X * scale * widthPulse, instance.BaseSize.Y * scale);
			float centerX = Size.X * instance.XRatio
				+ Mathf.Sin(time * (0.02f + instance.Speed) + instance.Phase) * Size.X * instance.Drift;
			float topY = -Size.Y * 0.015f + Mathf.Sin(time * 0.06f + instance.Phase) * 1.0f;
			float alpha = instance.Alpha * (0.58f + 0.08f * Mathf.Sin(time * 0.14f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, topY + size.Y * 0.5f), size, alpha);
		}
	}

	private void UpdateSurfaceWakeInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _surfaceWakeInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * (0.20f + instance.Speed) + instance.Phase) * 8.0f;
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * (0.24f + instance.Speed) + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.68f + 0.14f * Mathf.Sin(time * 0.28f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateCurrentRibbonInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _currentRibbonInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float xRatio = Mathf.PosMod(instance.XRatio + time * instance.Speed, 1.28f) - 0.18f;
			float centerX = Size.X * xRatio;
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * 0.50f + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.64f + 0.14f * Mathf.Sin(time * 0.38f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateCausticRibbonInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _causticRibbonInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float centerX = Size.X * (instance.XRatio + Mathf.Sin(time * instance.Speed + instance.Phase) * 0.08f);
			float centerY = Size.Y * instance.YRatio + Mathf.Sin(time * 0.42f + instance.Phase) * instance.Drift;
			float alpha = instance.Alpha * (0.78f + 0.22f * Mathf.Sin(time * 0.28f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateGlintInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _glintInstances)
		{
			instance.ElapsedTime = time;
			float pulse = Mathf.Max(0.0f, Mathf.Sin(time * instance.Speed + instance.Phase));
			if (pulse <= 0.04f)
			{
				SetImageVisible(instance, false);
				continue;
			}

			ApplyAnimatedFrame(instance);
			Vector2 size = instance.BaseSize * instance.Scale * (0.8f + pulse * 0.7f);
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * 0.18f + instance.Phase) * 12.0f;
			float centerY = Size.Y * instance.YRatio + Mathf.Cos(time * 0.22f + instance.Phase) * 8.0f;
			float alpha = instance.Alpha * pulse;
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateBubbleImageInstances()
	{
		Texture2D? bubbleTexture = GetBubbleTexture();
		if (bubbleTexture == null)
		{
			foreach (BubbleImageElement instance in _bubbleImageInstances)
			{
				SetBubbleVisible(instance, false);
			}

			return;
		}

		foreach (BubbleImageElement instance in _bubbleImageInstances)
		{
			if (instance.Node.Texture != bubbleTexture)
			{
				instance.Node.Texture = bubbleTexture;
				instance.ResetCache();
			}

			if (!TryGetBubbleLayout(instance.Bubble, out Vector2 position, out float radius, out float alpha))
			{
				SetBubbleVisible(instance, false);
				continue;
			}

			float side = Mathf.Max(radius * 3.35f, 8.0f);
			Vector2 size = new(side, side);
			ApplyBubbleLayout(instance, position - size / 2.0f, size, alpha * 1.22f);
		}
	}

	private static void ApplyAnimatedFrame(ImageElement instance)
	{
		if (instance.Frames.Length == 0)
		{
			if (instance.Node.Texture != null)
			{
				instance.Node.Texture = null;
			}
			instance.LastFrameIndex = -1;
			return;
		}

		float frameTime = instance.ElapsedTime * instance.FramesPerSecond
			+ instance.Phase * instance.Frames.Length / Mathf.Tau;
		int frameIndex = WrapFrameIndex(Mathf.FloorToInt(frameTime), instance.Frames.Length);
		Texture2D texture = instance.Frames[frameIndex];
			if (instance.LastFrameIndex != frameIndex || instance.Node.Texture != texture)
			{
				instance.Node.Texture = texture;
				instance.LastFrameIndex = frameIndex;
		}
	}

	private static int WrapFrameIndex(int frameIndex, int frameCount)
	{
		if (frameCount <= 0)
		{
			return 0;
		}

		int wrapped = frameIndex % frameCount;
		return wrapped < 0 ? wrapped + frameCount : wrapped;
	}

	private void ApplyImageLayout(ImageElement instance, Vector2 center, Vector2 size, float alpha)
	{
		instance.ElapsedTime = (float)_time;
			if (size.X <= 0.0f || size.Y <= 0.0f || alpha <= 0.001f)
			{
				SetImageVisible(instance, false);
				return;
			}

			Vector2 position = center - size / 2.0f;
			Color modulate = new(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha, 0.0f, 1.0f));
			SetImageVisible(instance, true);
			if (!instance.HasAppliedLayout || instance.LastSize.DistanceTo(size) >= 0.01f)
			{
				instance.Node.Size = size;
				instance.Node.PivotOffset = size / 2.0f;
				instance.LastSize = size;
			}
			if (!instance.HasAppliedLayout || instance.LastPosition.DistanceTo(position) >= 0.01f)
			{
				instance.Node.Position = position;
				instance.LastPosition = position;
			}
			if (!instance.HasAppliedLayout || Mathf.Abs(instance.LastModulate.A - modulate.A) >= 0.001f)
			{
				instance.Node.Modulate = modulate;
				instance.LastModulate = modulate;
			}
			instance.HasAppliedLayout = true;
		}

		private static void SetImageVisible(ImageElement instance, bool visible)
		{
			if (!instance.HasAppliedLayout || instance.LastVisible != visible)
			{
				instance.Node.Visible = visible;
				instance.LastVisible = visible;
			}
		}

		private static void SetBubbleVisible(BubbleImageElement instance, bool visible)
		{
			if (!instance.HasAppliedLayout || instance.LastVisible != visible)
			{
				instance.Node.Visible = visible;
				instance.LastVisible = visible;
			}
		}

		private static void ApplyBubbleLayout(BubbleImageElement instance, Vector2 position, Vector2 size, float alpha)
		{
			Color modulate = new(1.0f, 1.0f, 1.0f, Mathf.Clamp(alpha, 0.0f, 1.0f));
			SetBubbleVisible(instance, true);
			if (!instance.HasAppliedLayout || instance.LastSize.DistanceTo(size) >= 0.01f)
			{
				instance.Node.Size = size;
				instance.Node.PivotOffset = size / 2.0f;
				instance.LastSize = size;
			}
			if (!instance.HasAppliedLayout || instance.LastPosition.DistanceTo(position) >= 0.01f)
			{
				instance.Node.Position = position;
				instance.LastPosition = position;
			}
			if (!instance.HasAppliedLayout || Mathf.Abs(instance.LastModulate.A - modulate.A) >= 0.001f)
			{
				instance.Node.Modulate = modulate;
				instance.LastModulate = modulate;
			}
			instance.HasAppliedLayout = true;
		}

	private sealed class ImageElement
	{
		public TextureRect Node { get; set; } = default!;
		public Texture2D[] Frames { get; set; } = Array.Empty<Texture2D>();
		public Vector2 BaseSize { get; set; }
		public float FramesPerSecond { get; set; }
		public float XRatio { get; set; }
		public float YRatio { get; set; }
		public float Scale { get; set; }
		public float Alpha { get; set; }
		public float Phase { get; set; }
		public float Speed { get; set; }
		public float Drift { get; set; }
			public float Secondary { get; set; }
			public float ElapsedTime { get; set; }
			public int LastFrameIndex { get; set; } = -1;
			public bool LastVisible { get; set; }
			public Vector2 LastPosition { get; set; }
			public Vector2 LastSize { get; set; }
			public Color LastModulate { get; set; }
			public bool HasAppliedLayout { get; set; }
	}

	private sealed class BubbleImageElement
		{
			public TextureRect Node { get; set; } = default!;
			public Bubble Bubble { get; set; }
			public int LastFrameIndex { get; set; } = -1;
			public bool LastVisible { get; set; }
			public Vector2 LastPosition { get; set; }
			public Vector2 LastSize { get; set; }
			public Color LastModulate { get; set; }
			public bool HasAppliedLayout { get; set; }

			public void ResetCache()
			{
				LastFrameIndex = -1;
				LastVisible = false;
				LastPosition = Vector2.Zero;
				LastSize = Vector2.Zero;
				LastModulate = Colors.Transparent;
				HasAppliedLayout = false;
			}
		}
}
