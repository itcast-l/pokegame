using System;
using Godot;

public partial class MainMenuBackgroundAnimator : TextureRect
{
	[Export]
	public string frame_pattern = "res://art/menu/main_menu_ocean_frames/frame-{0:000}.png";

	[Export(PropertyHint.Range, "1,60,1")]
	public int frame_count = 24;

	[Export(PropertyHint.Range, "1.0,24.0,0.5")]
	public float frames_per_second = 16.0f;

	[Export(PropertyHint.Range, "0.0,2.0,0.05")]
	public float motion_scale = 1.0f;

	private Texture2D[] _frames = Array.Empty<Texture2D>();
	private Texture2D? _staticTexture;
	private bool _motionEnabled = true;
	private double _elapsed;
	private int _frameIndex;

	public override void _Ready()
	{
		_staticTexture = Texture;
		_frames = FrameAnimationHelpers.LoadTextureFrames(
			frame_pattern,
			frame_count,
			"main menu ocean background",
			preferImageFile: true);

		if (_frames.Length > 0)
		{
			Texture = _frames[0];
		}

		UpdateProcessingState();
	}

	public override void _Process(double delta)
	{
		if (!_motionEnabled || _frames.Length <= 1 || frames_per_second <= 0.0f)
		{
			return;
		}

		_elapsed += delta * Mathf.Max(motion_scale, 0.0f);
		int nextFrameIndex = Mathf.PosMod((int)Math.Floor(_elapsed * frames_per_second), _frames.Length);
		if (nextFrameIndex == _frameIndex)
		{
			return;
		}

		_frameIndex = nextFrameIndex;
		Texture = _frames[_frameIndex];
	}

	public void SetMotionEnabled(bool enabled)
	{
		_motionEnabled = enabled;
		if (!_motionEnabled)
		{
			_elapsed = 0.0;
			_frameIndex = 0;
			Texture = _staticTexture ?? (_frames.Length > 0 ? _frames[0] : Texture);
		}
		else if (_frames.Length > 0)
		{
			Texture = _frames[_frameIndex];
		}

		UpdateProcessingState();
	}

	private void UpdateProcessingState()
	{
		SetProcess(_motionEnabled && _frames.Length > 1 && frames_per_second > 0.0f);
	}
}
