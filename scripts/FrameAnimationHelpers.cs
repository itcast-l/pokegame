using System;
using System.Collections.Generic;
using Godot;

internal static class FrameAnimationHelpers
{
	public static Texture2D[] LoadTextureFrames(string framePattern, int frameCount, string warningLabel)
	{
		var frames = new List<Texture2D>(frameCount);
		for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
		{
			string path = string.Format(framePattern, frameIndex);
			Texture2D? texture = GD.Load<Texture2D>(path);
			if (texture == null)
			{
				GD.PushWarning($"Missing {warningLabel} frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames.ToArray();
	}

	public static void TweenTextureFrames(
		Tween tween,
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		TweenTextureFrames(tween, frames, frameSeconds, repeatCount, frame =>
		{
			if (GodotObject.IsInstanceValid(textureRect))
			{
				textureRect.Texture = frame;
			}
		});
	}

	public static void TweenTextureFramesForDuration(
		Tween tween,
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		double animationSeconds)
	{
		int totalFrameCount = Math.Max(frames.Count, Mathf.CeilToInt((float)(animationSeconds / frameSeconds)));
		TweenTextureFrameSlots(tween, frames, frameSeconds, totalFrameCount, frame =>
		{
			if (GodotObject.IsInstanceValid(textureRect))
			{
				textureRect.Texture = frame;
			}
		});
	}

	public static void TweenTextureFramesForDuration(
		Tween tween,
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		double animationSeconds)
	{
		int totalFrameCount = Math.Max(frames.Count, Mathf.CeilToInt((float)(animationSeconds / frameSeconds)));
		TweenTextureFrameSlots(tween, frames, frameSeconds, totalFrameCount, frame =>
		{
			if (GodotObject.IsInstanceValid(sprite))
			{
				sprite.Texture = frame;
			}
		});
	}

	public static void TweenTextureFrames(
		Tween tween,
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		TweenTextureFrames(tween, frames, frameSeconds, repeatCount, frame =>
		{
			if (GodotObject.IsInstanceValid(sprite))
			{
				sprite.Texture = frame;
			}
		});
	}

	public static void ConfigureTextureAnimation(
		AnimationPlayer animationPlayer,
		StringName animationName,
		NodePath trackPath,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		bool loops)
	{
		if (frames.Count == 0)
		{
			return;
		}

		AnimationLibrary library = GetOrCreateDefaultLibrary(animationPlayer);
		if (library.HasAnimation(animationName))
		{
			library.RemoveAnimation(animationName);
		}

		var animation = new Animation
		{
			Length = (float)Math.Max(frameSeconds, frames.Count * frameSeconds),
			LoopMode = loops ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None,
		};

		int trackIndex = animation.AddTrack(Animation.TrackType.Value);
		animation.TrackSetPath(trackIndex, trackPath);
		animation.TrackSetInterpolationType(trackIndex, Animation.InterpolationType.Nearest);
		animation.ValueTrackSetUpdateMode(trackIndex, Animation.UpdateMode.Discrete);

		for (int frameIndex = 0; frameIndex < frames.Count; frameIndex++)
		{
			animation.TrackInsertKey(trackIndex, frameIndex * frameSeconds, frames[frameIndex]);
		}

		if (!loops && frames.Count > 0)
		{
			animation.TrackInsertKey(trackIndex, animation.Length, frames[^1]);
		}

		library.AddAnimation(animationName, animation);
	}

	private static void TweenTextureFrames(
		Tween tween,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount,
		Action<Texture2D> applyFrame)
	{
		if (frames.Count == 0 || frameSeconds <= 0.0 || repeatCount <= 0)
		{
			return;
		}

		TweenTextureFrameSlots(tween, frames, frameSeconds, frames.Count * repeatCount, applyFrame);
	}

	private static void TweenTextureFrameSlots(
		Tween tween,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int totalFrameCount,
		Action<Texture2D> applyFrame)
	{
		if (frames.Count == 0 || frameSeconds <= 0.0 || totalFrameCount <= 0)
		{
			return;
		}

		for (int frameSlot = 0; frameSlot < totalFrameCount; frameSlot++)
		{
			Texture2D frame = frames[frameSlot % frames.Count];
			tween.TweenCallback(Callable.From(() => applyFrame(frame))).SetDelay(frameSlot * frameSeconds);
		}
	}

	private static AnimationLibrary GetOrCreateDefaultLibrary(AnimationPlayer animationPlayer)
	{
		var libraryName = new StringName("");
		AnimationLibrary? library = animationPlayer.GetAnimationLibrary(libraryName);
		if (library != null)
		{
			return library;
		}

		library = new AnimationLibrary();
		animationPlayer.AddAnimationLibrary(libraryName, library);
		return library;
	}
}
