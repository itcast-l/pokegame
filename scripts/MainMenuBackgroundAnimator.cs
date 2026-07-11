using System;
using Godot;

public partial class MainMenuBackgroundAnimator : TextureRect
{
	private static readonly StringName BackgroundAnimationName = "main_menu_ocean_background";

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
	private AnimationPlayer? _animationPlayer;
	private bool _motionEnabled = true;

	public override void _Ready()
	{
		_staticTexture = Texture;
		_ = LoadFramesAndConfigureAnimationAsync();
		UpdateAnimationPlayback();
	}

	protected virtual System.Threading.Tasks.Task<Texture2D[]>
		LoadBackgroundFramesAsync()
	{
		return BackgroundFrameLoader.LoadTextureFramesAsync(
			this,
			frame_pattern,
			frame_count,
			"main menu ocean background");
	}

	private async System.Threading.Tasks.Task LoadFramesAndConfigureAnimationAsync()
	{
		Texture2D[] frames = await LoadBackgroundFramesAsync();

		if (!GodotObject.IsInstanceValid(this) || !IsInsideTree())
		{
			return;
		}

		_frames = frames;

		if (_frames.Length > 0)
		{
			Texture = _frames[0];
		}

		if (_frames.Length > 1 && frames_per_second > 0.0f)
		{
			_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? CreateAnimationPlayer();
			FrameAnimationHelpers.ConfigureTextureAnimation(
				_animationPlayer,
				BackgroundAnimationName,
				new NodePath(".:texture"),
				_frames,
				1.0 / frames_per_second,
				loops: true);
		}

		UpdateAnimationPlayback();
	}

	public void SetMotionEnabled(bool enabled)
	{
		_motionEnabled = enabled;
		if (!_motionEnabled)
		{
			_animationPlayer?.Stop();
			Texture = _staticTexture ?? (_frames.Length > 0 ? _frames[0] : Texture);
		}

		UpdateAnimationPlayback();
	}

	private AnimationPlayer CreateAnimationPlayer()
	{
		var animationPlayer = new AnimationPlayer
		{
			Name = "AnimationPlayer",
			RootNode = new NodePath(".."),
		};
		AddChild(animationPlayer);
		return animationPlayer;
	}

	private void UpdateAnimationPlayback()
	{
		if (_animationPlayer == null || _frames.Length <= 1 || frames_per_second <= 0.0f)
		{
			return;
		}

		_animationPlayer.SpeedScale = Mathf.Max(motion_scale, 0.0f);
		if (!_motionEnabled || Mathf.IsZeroApprox(_animationPlayer.SpeedScale))
		{
			_animationPlayer.Stop();
			Texture = _staticTexture ?? _frames[0];
			return;
		}

		if (!_animationPlayer.IsPlaying())
		{
			_animationPlayer.Play(BackgroundAnimationName);
		}
	}
}
