using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Godot;

public partial class DiceRollOverlay : Control
{
	private static readonly Vector2 SourceFrameSize = new(512.0f, 320.0f);
	private static readonly Vector2 MinDisplaySize = new(260.0f, 162.5f);
	private const float MaxViewportWidthRatio = 0.38f;
	private const float MaxViewportHeightRatio = 0.28f;
	private const float VerticalCenterRatio = 0.46f;

	private readonly List<Texture2D> _rollingFrames = new();
	private readonly Dictionary<int, Texture2D> _resultFrames = new();
	private TextureRect _textureRect = default!;
	private TaskCompletionSource<int>? _playbackCompletion;
	private double _elapsedSeconds;
	private int _targetResult;
	private bool _isPlaying;

	public bool IsPlaying => _isPlaying;
	public int LastResult { get; private set; }

	public override void _Ready()
	{
		MouseFilter = MouseFilterEnum.Ignore;
		Visible = false;
		SetProcess(false);
		EnsureTextureRect();
		LoadTextures();
		RefreshLayout();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
		{
			RefreshLayout();
		}
	}

	public override void _Process(double delta)
	{
		if (!_isPlaying)
		{
			return;
		}

		_elapsedSeconds += delta;
		double rollingSeconds = DiceRollPresentationRules.RollingPathFrameCount * DiceRollPresentationRules.RollingPathFrameSeconds;
		if (_elapsedSeconds < rollingSeconds)
		{
			int frameIndex = Math.Clamp(
				(int)Math.Floor(_elapsedSeconds / DiceRollPresentationRules.RollingPathFrameSeconds),
				0,
				_rollingFrames.Count - 1);
			_textureRect.Texture = _rollingFrames[frameIndex];
			return;
		}

		if (_resultFrames.TryGetValue(_targetResult, out Texture2D? resultTexture))
		{
			_textureRect.Texture = resultTexture;
		}

		if (_elapsedSeconds < rollingSeconds + DiceRollPresentationRules.ResultHoldSeconds)
		{
			return;
		}

		FinishPlayback();
	}

	public bool HasRequiredTexturesForResult(int result)
	{
		return DiceRollPresentationRules.IsValidResult(result)
			&& _rollingFrames.Count == DiceRollPresentationRules.RollingPathFrameCount
			&& _resultFrames.ContainsKey(result);
	}

	public Task<int> PlayRollAsync(int result)
	{
		if (!HasRequiredTexturesForResult(result))
		{
			return Task.FromResult(0);
		}

		if (_playbackCompletion != null && !_playbackCompletion.Task.IsCompleted)
		{
			_playbackCompletion.TrySetResult(LastResult);
		}

		_playbackCompletion = new TaskCompletionSource<int>();
		_targetResult = result;
		LastResult = result;
		_elapsedSeconds = 0.0;
		_isPlaying = true;
		Visible = true;
		Modulate = Colors.White;
		_textureRect.Texture = _rollingFrames[0];
		RefreshLayout();
		SetProcess(true);
		return _playbackCompletion.Task;
	}

	private void FinishPlayback()
	{
		_isPlaying = false;
		SetProcess(false);
		_playbackCompletion?.TrySetResult(_targetResult);
	}

	private void EnsureTextureRect()
	{
		if (_textureRect != null && GodotObject.IsInstanceValid(_textureRect))
		{
			return;
		}

		_textureRect = new TextureRect
		{
			Name = "DiceRollTexture",
			MouseFilter = MouseFilterEnum.Ignore,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
		};
		AddChild(_textureRect);
	}

	private void LoadTextures()
	{
		if (_rollingFrames.Count == 0)
		{
			for (int frameIndex = 1; frameIndex <= DiceRollPresentationRules.RollingPathFrameCount; frameIndex++)
			{
				Texture2D? texture = LoadTexture(DiceRollPresentationRules.GetRollingPathFramePath(frameIndex));
				if (texture != null)
				{
					_rollingFrames.Add(texture);
				}
			}
		}

		for (int result = DiceRollPresentationRules.MinResult; result <= DiceRollPresentationRules.MaxResult; result++)
		{
			if (_resultFrames.ContainsKey(result)
				|| !DiceRollPresentationRules.TryGetResultMotionFramePath(result, out string path))
			{
				continue;
			}

			Texture2D? texture = LoadTexture(path);
			if (texture != null)
			{
				_resultFrames[result] = texture;
			}
		}
	}

	private static Texture2D? LoadTexture(string resPath)
	{
		if (string.IsNullOrWhiteSpace(resPath))
		{
			return null;
		}

		Texture2D? texture = GD.Load<Texture2D>(resPath);
		if (texture != null)
		{
			return texture;
		}

		string globalPath = ProjectSettings.GlobalizePath(resPath);
		if (!File.Exists(globalPath))
		{
			GD.PushWarning($"Missing dice roll texture: {resPath}");
			return null;
		}

		Image image = Image.LoadFromFile(globalPath);
		if (image.IsEmpty())
		{
			GD.PushWarning($"Failed to load dice roll texture image: {resPath}");
			return null;
		}

		return ImageTexture.CreateFromImage(image);
	}

	private void RefreshLayout()
	{
		if (_textureRect == null || !GodotObject.IsInstanceValid(_textureRect))
		{
			return;
		}

		Vector2 viewportSize = Size == Vector2.Zero ? GetViewportRect().Size : Size;
		float maxWidth = Mathf.Max(MinDisplaySize.X, viewportSize.X * MaxViewportWidthRatio);
		float maxHeight = Mathf.Max(MinDisplaySize.Y, viewportSize.Y * MaxViewportHeightRatio);
		float scale = Mathf.Min(maxWidth / SourceFrameSize.X, maxHeight / SourceFrameSize.Y);
		Vector2 displaySize = SourceFrameSize * scale;
		_textureRect.Size = displaySize;
		_textureRect.Position = new Vector2(
			(viewportSize.X - displaySize.X) * 0.5f,
			viewportSize.Y * VerticalCenterRatio - displaySize.Y * 0.5f);
	}
}
