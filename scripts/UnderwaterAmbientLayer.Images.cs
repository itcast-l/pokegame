using System;
using System.Collections.Generic;
using Godot;

public partial class UnderwaterAmbientLayer
{
	private void EnsureImageAssetsLoaded(bool retryIfAllMissing = false)
	{
		if (_imageAssetState != DynamicImageAssetState.Uninitialized)
		{
			return;
		}

		_missingCategoryWarnings.Clear();
		_resourceLoadCount++;
		if (debug_force_missing_image_assets)
		{
			_seagrassFrames = Array.Empty<Texture2D>();
			_centerCausticFrames = Array.Empty<Texture2D>();
			_centerGlintFrames = Array.Empty<Texture2D>();
			_sunbeamFrames = Array.Empty<Texture2D>();
			_waterCurrentFrames = Array.Empty<Texture2D>();
			_moteFrames = Array.Empty<Texture2D>();
		}
		else
		{
			_seagrassFrames = LoadProfileFrames("seagrass", SeagrassFrameCount, "underwater seagrass");
			_centerCausticFrames = LoadProfileFrames("center_caustic", CenterCausticFrameCount, "underwater center caustic");
			_centerGlintFrames = LoadProfileFrames("center_glint", CenterGlintFrameCount, "underwater center glint");
			_sunbeamFrames = LoadProfileFrames("sunbeam", SunbeamFrameCount, "underwater sunbeam");
			_waterCurrentFrames = LoadProfileFrames("water_current", WaterCurrentFrameCount, "underwater water current");
			_moteFrames = LoadProfileFrames("mote", MoteFrameCount, "underwater mote");
		}

		_imageAssetState = AllImageFramesMissing()
			? DynamicImageAssetState.PermanentFallback
			: DynamicImageAssetState.Ready;
		if (_imageAssetState == DynamicImageAssetState.PermanentFallback)
		{
			GD.PushWarning($"Dynamic background image assets unavailable: UnderwaterAmbientLayer/{_activeVisualProfile}. Using permanent procedural fallback.");
			return;
		}

		WarnMissingProfileCategories();
	}

	private Texture2D[] LoadProfileFrames(string category, int frameCount, string warningLabel)
	{
		string profileSlug = GetProfileSlug(_activeVisualProfile);
		string framePattern = $"res://art/fx/environment/underwater_ambient/{profileSlug}/{category}/run-001/processed/fx-{{0}}.png";
		return FrameAnimationHelpers.LoadTextureFrames(
			framePattern,
			frameCount,
			warningLabel,
			preferImageFile: true,
			warnMissingFrames: false);
	}

	private void WarnMissingProfileCategories()
	{
		string profileSlug = GetProfileSlug(_activeVisualProfile);
		WarnMissingCategoryIfEmpty("seagrass", _seagrassFrames, profileSlug);
		WarnMissingCategoryIfEmpty("center_caustic", _centerCausticFrames, profileSlug);
		WarnMissingCategoryIfEmpty("center_glint", _centerGlintFrames, profileSlug);
		WarnMissingCategoryIfEmpty("sunbeam", _sunbeamFrames, profileSlug);
		WarnMissingCategoryIfEmpty("water_current", _waterCurrentFrames, profileSlug);
		WarnMissingCategoryIfEmpty("mote", _moteFrames, profileSlug);
	}

	private void WarnMissingCategoryIfEmpty(string category, Texture2D[] frames, string profileSlug)
	{
		if (frames.Length == 0)
		{
			WarnMissingCategory(category, $"res://art/fx/environment/underwater_ambient/{profileSlug}/{category}/run-001/processed/fx-{{0}}.png");
		}
	}

	public void ReloadImageAssets()
	{
		if (!Engine.IsEditorHint() && !OS.IsDebugBuild())
		{
			return;
		}

		_imageAssetState = DynamicImageAssetState.Uninitialized;
		_seagrassFrames = Array.Empty<Texture2D>();
		_centerCausticFrames = Array.Empty<Texture2D>();
		_centerGlintFrames = Array.Empty<Texture2D>();
		_sunbeamFrames = Array.Empty<Texture2D>();
		_waterCurrentFrames = Array.Empty<Texture2D>();
		_moteFrames = Array.Empty<Texture2D>();
		ClearImageInstances();
		EnsureImageAssetsLoaded();
		RebuildAmbient();
	}

	public int DebugGetResourceLoadCount() => _resourceLoadCount;
	public int DebugGetRebuildCount() => _rebuildCount;
	public string DebugGetImageAssetStateName() => _imageAssetState.ToString();

	private void WarnMissingCategory(string category, string path)
	{
		if (_missingCategoryWarnings.ContainsKey(category))
		{
			return;
		}

		_missingCategoryWarnings[category] = true;
		GD.PushWarning($"UnderwaterAmbientLayer missing {category} frames for {_activeVisualProfile}: {path}. Falling back to procedural draw for that category.");
	}

	private bool AllImageFramesMissing()
	{
		return _seagrassFrames.Length == 0
			&& _centerCausticFrames.Length == 0
			&& _centerGlintFrames.Length == 0
			&& _sunbeamFrames.Length == 0
			&& _waterCurrentFrames.Length == 0
			&& _moteFrames.Length == 0;
	}

	private bool HasNoImageInstances()
	{
		return _seagrassInstances.Count == 0
			&& _centerCausticInstances.Count == 0
			&& _centerGlintInstances.Count == 0
			&& _sunbeamInstances.Count == 0
			&& _waterCurrentInstances.Count == 0
			&& _moteInstances.Count == 0;
	}

	private void EnsureImageLayers()
	{
		_seagrassLayer = GetOrCreateLayer(ref _seagrassLayer, "SeagrassLayer", zIndex: -1);
		_sunbeamLayer = GetOrCreateLayer(ref _sunbeamLayer, "SunbeamLayer", zIndex: 0);
		_waterCurrentLayer = GetOrCreateLayer(ref _waterCurrentLayer, "WaterCurrentLayer", zIndex: 0);
		_centerCausticLayer = GetOrCreateLayer(ref _centerCausticLayer, "CenterCausticLayer", zIndex: 1);
		_moteLayer = GetOrCreateLayer(ref _moteLayer, "MoteLayer", zIndex: 1);
		_centerGlintLayer = GetOrCreateLayer(ref _centerGlintLayer, "CenterGlintLayer", zIndex: 1);
	}

	private Control GetOrCreateLayer(ref Control? layer, string name, int zIndex)
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
			GrowHorizontal = GrowDirection.Both,
			GrowVertical = GrowDirection.Both,
			ZAsRelative = false,
			ZIndex = zIndex,
		};
		layer.SetAnchorsPreset(LayoutPreset.FullRect);
		layer.OffsetRight = 0.0f;
		layer.OffsetBottom = 0.0f;
		AddChild(layer);
		return layer;
	}

	private void SetImageLayersVisible(bool visible)
	{
		SetLayerVisible(_seagrassLayer, visible);
		SetLayerVisible(_sunbeamLayer, visible);
		SetLayerVisible(_waterCurrentLayer, visible);
		SetLayerVisible(_centerCausticLayer, visible);
		SetLayerVisible(_moteLayer, visible);
		SetLayerVisible(_centerGlintLayer, visible);
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
		BuildSeagrassInstances();
		BuildWaterCurrentInstances();
		BuildCenterCausticInstances();
		BuildMoteInstances();
		BuildCenterGlintInstances();
	}

	private void ClearImageInstances()
	{
		ClearImageLayer(_seagrassLayer, _seagrassInstances);
		ClearImageLayer(_sunbeamLayer, _sunbeamInstances);
		ClearImageLayer(_waterCurrentLayer, _waterCurrentInstances);
		ClearImageLayer(_centerCausticLayer, _centerCausticInstances);
		ClearImageLayer(_moteLayer, _moteInstances);
		ClearImageLayer(_centerGlintLayer, _centerGlintInstances);
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

	private void BuildSeagrassInstances()
	{
		if (_seagrassLayer == null || _seagrassFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < _seagrassBlades.Count; index++)
		{
			SeagrassBlade blade = _seagrassBlades[index];
			var instance = CreateImageElement(
				_seagrassLayer,
				$"Seagrass{index + 1}",
				_seagrassFrames,
				SeagrassFrameSize,
				SeagrassFramesPerSecond);
			instance.Position = blade.Base;
			instance.Scale = Mathf.Clamp(blade.Height / SeagrassFrameSize.Y, 0.22f, 0.62f);
			instance.Alpha = Mathf.Clamp(blade.Color.A * 1.35f, 0.08f, 0.55f);
			instance.Phase = blade.Phase;
			instance.Speed = blade.Speed;
			instance.Drift = blade.Sway;
			instance.Secondary = blade.Flip;
			instance.GrowsDown = blade.GrowsDown;
			_seagrassInstances.Add(instance);
		}
	}

	private void BuildSunbeamInstances()
	{
		if (_sunbeamLayer == null || _sunbeamFrames.Length == 0 || sunbeam_intensity <= 0.0f)
		{
			return;
		}

		for (int side = 0; side < 2; side++)
		{
			for (int index = 0; index < 5; index++)
			{
				var instance = CreateImageElement(
					_sunbeamLayer,
					$"Sunbeam{side + 1}_{index + 1}",
					_sunbeamFrames,
					SunbeamFrameSize,
					SunbeamFramesPerSecond);
				instance.XRatio = side == 0
					? 0.04f + index * 0.045f
					: 0.96f - index * 0.045f;
				instance.YRatio = 0.0f;
				instance.Scale = 0.18f + index * 0.03f;
				instance.Alpha = (0.04f + index * 0.006f) * sunbeam_intensity;
				instance.Phase = index * 1.27f + side * 2.4f;
				instance.Speed = 0.02f + index * 0.004f;
				instance.Drift = 0.008f + index * 0.002f;
				instance.Secondary = side == 0 ? 1.0f : -1.0f;
				_sunbeamInstances.Add(instance);
			}
		}
	}

	private void BuildWaterCurrentInstances()
	{
		if (_waterCurrentLayer == null || _waterCurrentFrames.Length == 0)
		{
			return;
		}

		for (int edge = 0; edge < 2; edge++)
		{
			float yRatio = edge == 0 ? 0.075f : 0.925f;
			for (int index = 0; index < 10; index++)
			{
				var instance = CreateImageElement(
					_waterCurrentLayer,
					$"WaterCurrent{edge + 1}_{index + 1}",
					_waterCurrentFrames,
					WaterCurrentFrameSize,
					WaterCurrentFramesPerSecond);
				instance.XRatio = index / 9.0f;
				instance.YRatio = yRatio;
				instance.Scale = 0.22f;
				instance.Alpha = 0.10f;
				instance.Phase = index * 0.7f + edge * 1.9f;
				instance.Speed = 0.38f + index * 0.02f;
				instance.Drift = 14.0f;
				instance.Secondary = edge == 0 ? 1.0f : -1.0f;
				_waterCurrentInstances.Add(instance);
			}
		}
	}

	private void BuildCenterCausticInstances()
	{
		if (_centerCausticLayer == null || _centerCausticFrames.Length == 0 || center_caustic_intensity <= 0.0f)
		{
			return;
		}

		int count = GetCenterCausticInstanceCount();
		for (int index = 0; index < count; index++)
		{
			var instance = CreateImageElement(
				_centerCausticLayer,
				$"CenterCaustic{index + 1}",
				_centerCausticFrames,
				CenterCausticFrameSize,
				CenterCausticFramesPerSecond);
			instance.XRatio = 0.34f + (index % 4) * 0.11f + _rng.RandfRange(-0.025f, 0.025f);
			instance.YRatio = 0.34f + (index / 4) * 0.12f + _rng.RandfRange(-0.03f, 0.03f);
			instance.Scale = GetCenterCausticBaseScale() + _rng.RandfRange(-0.05f, 0.08f);
			instance.Alpha = GetCenterCausticBaseAlpha() * _rng.RandfRange(0.82f, 1.14f);
			instance.Phase = _rng.RandfRange(0.0f, Mathf.Tau);
			instance.Speed = _rng.RandfRange(0.18f, 0.34f);
			instance.Drift = _rng.RandfRange(8.0f, 24.0f);
			_centerCausticInstances.Add(instance);
		}
	}

	private void BuildCenterGlintInstances()
	{
		if (_centerGlintLayer == null || _centerGlintFrames.Length == 0 || center_caustic_intensity <= 0.0f)
		{
			return;
		}

		for (int index = 0; index < _centerGlints.Count; index++)
		{
			CenterGlint glint = _centerGlints[index];
			var instance = CreateImageElement(
				_centerGlintLayer,
				$"CenterGlint{index + 1}",
				_centerGlintFrames,
				CenterGlintFrameSize,
				CenterGlintFramesPerSecond);
			instance.XRatio = glint.XRatio;
			instance.YRatio = glint.YRatio;
			instance.Scale = Mathf.Clamp(glint.Radius / 8.5f, 0.18f, 0.75f);
			instance.Alpha = glint.Alpha;
			instance.Phase = glint.Phase;
			instance.Speed = glint.Speed;
			_centerGlintInstances.Add(instance);
		}
	}

	private void BuildMoteInstances()
	{
		if (_moteLayer == null || _moteFrames.Length == 0)
		{
			return;
		}

		for (int index = 0; index < _motes.Count; index++)
		{
			Mote mote = _motes[index];
			var instance = CreateImageElement(
				_moteLayer,
				$"Mote{index + 1}",
				_moteFrames,
				MoteFrameSize,
				MoteFramesPerSecond);
			instance.Position = mote.Base;
			instance.Scale = Mathf.Clamp(mote.Radius / 8.0f, 0.14f, 0.36f);
			instance.Alpha = Mathf.Clamp(mote.Alpha * 1.4f, 0.08f, 0.42f);
			instance.Phase = mote.Phase;
			instance.Speed = mote.Speed;
			instance.Drift = mote.Drift;
			instance.Secondary = 0.45f;
			_moteInstances.Add(instance);
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
			TextureFilter = TextureFilterEnum.Linear,
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

	private void UpdateImageInstances()
	{
		UpdateSunbeamInstances();
		UpdateSeagrassInstances();
		UpdateWaterCurrentInstances();
		UpdateCenterCausticInstances();
		UpdateMoteInstances();
		UpdateCenterGlintInstances();
	}

	private void UpdateSunbeamInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _sunbeamInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float widthPulse = 0.78f + Mathf.Sin(time * 0.18f + instance.Phase) * 0.08f;
			float scale = (Size.Y / instance.BaseSize.Y) * instance.Scale;
			Vector2 size = new(instance.BaseSize.X * scale * widthPulse, instance.BaseSize.Y * scale);
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * instance.Speed + instance.Phase) * Size.X * instance.Drift;
			float centerY = size.Y * 0.5f + Mathf.Sin(time * 0.06f + instance.Phase) * 1.0f;
			float alpha = instance.Alpha * (0.58f + 0.08f * Mathf.Sin(time * 0.14f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateSeagrassInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _seagrassInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float sizePulse = 0.97f + 0.03f * Mathf.Sin(time * instance.Speed + instance.Phase);
			float scale = instance.Scale * sizePulse;
			Vector2 size = instance.BaseSize * scale;
			float direction = instance.GrowsDown ? 1.0f : -1.0f;
			float centerX = instance.Position.X
				+ Mathf.Sin(time * instance.Speed + instance.Phase) * instance.Drift * 0.14f
				+ instance.Secondary * Mathf.Sin(time * instance.Speed * 0.7f + instance.Phase) * instance.Drift * 0.08f;
			float centerY = instance.Position.Y - direction * size.Y * 0.5f;
			float alpha = instance.Alpha * (0.92f + 0.08f * Mathf.Sin(time * 0.46f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateCenterCausticInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _centerCausticInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			Vector2 size = instance.BaseSize * instance.Scale;
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * instance.Speed + instance.Phase) * instance.Drift;
			float centerY = Size.Y * instance.YRatio + Mathf.Cos(time * 0.42f + instance.Phase) * instance.Drift * 0.35f;
			float alpha = instance.Alpha
				* center_caustic_intensity
				* (0.78f + 0.22f * Mathf.Sin(time * 0.28f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateWaterCurrentInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _waterCurrentInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float width = Size.X * instance.Scale;
			float scale = width / instance.BaseSize.X;
			Vector2 size = instance.BaseSize * scale;
			float sway = Mathf.Sin(time * 0.45f + instance.Phase) * instance.Drift;
			float centerX = Size.X * instance.XRatio;
			float centerY = Size.Y * instance.YRatio + sway * instance.Secondary;
			float alpha = instance.Alpha * (0.82f + 0.18f * Mathf.Sin(time * 0.26f + instance.Phase));
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateCenterGlintInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _centerGlintInstances)
		{
			instance.ElapsedTime = time;
			float pulse = Mathf.Max(0.0f, Mathf.Sin(time * instance.Speed + instance.Phase));
				if (pulse <= 0.08f)
				{
					SetImageVisible(instance, false);
				continue;
			}

			ApplyAnimatedFrame(instance);
			Vector2 size = instance.BaseSize * instance.Scale * (0.72f + pulse * 0.95f);
			float centerX = Size.X * instance.XRatio + Mathf.Sin(time * 0.18f + instance.Phase) * 10.0f;
			float centerY = Size.Y * instance.YRatio + Mathf.Cos(time * 0.24f + instance.Phase) * 7.0f;
			float alpha = instance.Alpha * pulse * center_caustic_intensity;
			ApplyImageLayout(instance, new Vector2(centerX, centerY), size, alpha);
		}
	}

	private void UpdateMoteInstances()
	{
		float time = (float)_time;
		foreach (ImageElement instance in _moteInstances)
		{
			instance.ElapsedTime = time;
			ApplyAnimatedFrame(instance);
			float pulse = 0.86f + 0.14f * Mathf.Sin(time * instance.Speed * 1.2f + instance.Phase);
			Vector2 size = instance.BaseSize * instance.Scale * pulse;
			Vector2 center = instance.Position + new Vector2(
				Mathf.Sin(time * instance.Speed + instance.Phase) * instance.Drift,
				Mathf.Cos(time * instance.Speed * 0.8f + instance.Phase) * instance.Drift * instance.Secondary);
			float alpha = instance.Alpha * (0.82f + 0.18f * Mathf.Sin(time * 0.34f + instance.Phase));
			ApplyImageLayout(instance, center, size, alpha);
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

	private static string GetProfileSlug(AmbientVisualProfile profile)
	{
		return profile switch
		{
			AmbientVisualProfile.MainMenu => "main_menu",
			AmbientVisualProfile.GameBoard => "game_board",
			AmbientVisualProfile.AdventureMap => "adventure_map",
			_ => "game_board",
		};
	}

	private int GetCenterCausticInstanceCount()
	{
		return _activeVisualProfile switch
		{
			AmbientVisualProfile.MainMenu => 5,
			AmbientVisualProfile.GameBoard => 7,
			AmbientVisualProfile.AdventureMap => 9,
			_ => 7,
		};
	}

	private float GetCenterCausticBaseScale()
	{
		return _activeVisualProfile switch
		{
			AmbientVisualProfile.MainMenu => 0.26f,
			AmbientVisualProfile.GameBoard => 0.30f,
			AmbientVisualProfile.AdventureMap => 0.36f,
			_ => 0.30f,
		};
	}

	private float GetCenterCausticBaseAlpha()
	{
		return _activeVisualProfile switch
		{
			AmbientVisualProfile.MainMenu => 0.08f,
			AmbientVisualProfile.GameBoard => 0.10f,
			AmbientVisualProfile.AdventureMap => 0.12f,
			_ => 0.10f,
		};
	}

	private sealed class ImageElement
	{
		public TextureRect Node { get; set; } = default!;
		public Texture2D[] Frames { get; set; } = Array.Empty<Texture2D>();
		public Vector2 BaseSize { get; set; }
		public float FramesPerSecond { get; set; }
		public Vector2 Position { get; set; }
		public float XRatio { get; set; }
		public float YRatio { get; set; }
		public float Scale { get; set; }
		public float Alpha { get; set; }
		public float Phase { get; set; }
		public float Speed { get; set; }
		public float Drift { get; set; }
		public float Secondary { get; set; }
			public bool GrowsDown { get; set; }
			public float ElapsedTime { get; set; }
			public int LastFrameIndex { get; set; } = -1;
			public bool LastVisible { get; set; }
			public Vector2 LastPosition { get; set; }
			public Vector2 LastSize { get; set; }
			public Color LastModulate { get; set; }
			public bool HasAppliedLayout { get; set; }
		}
}
