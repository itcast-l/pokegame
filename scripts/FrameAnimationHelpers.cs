using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

internal static class FrameAnimationHelpers
{
	public static Texture2D[] LoadTextureFrames(string framePattern, int frameCount, string warningLabel, bool preferImageFile = false)
	{
		var frames = new List<Texture2D>(frameCount);
		for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
		{
			string path = string.Format(CultureInfo.InvariantCulture, framePattern, frameIndex);
			Texture2D? texture = LoadTextureWithImageFallback(path, preferImageFile);
			if (texture == null)
			{
				GD.PushWarning($"Missing {warningLabel} frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames.ToArray();
	}

	private static Texture2D? LoadTextureWithImageFallback(string path, bool preferImageFile)
	{
		if (preferImageFile)
		{
			Texture2D? imageTexture = LoadTextureFromImageFile(path);
			if (imageTexture != null)
			{
				return imageTexture;
			}
		}

		Texture2D? texture = GD.Load<Texture2D>(path);
		if (texture != null)
		{
			return texture;
		}

		return LoadTextureFromImageFile(path);
	}

	private static Texture2D? LoadTextureFromImageFile(string path)
	{
		string globalPath = ProjectSettings.GlobalizePath(path);
		if (!File.Exists(globalPath))
		{
			return null;
		}

		Image image = Image.LoadFromFile(globalPath);
		return image.IsEmpty() ? null : ImageTexture.CreateFromImage(image);
	}

	public static void TweenTextureFrames(
		Tween tween,
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		PlayTextureFrames(textureRect, frames, frameSeconds, repeatCount);
	}

	public static void TweenTextureFramesForDuration(
		Tween tween,
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		double animationSeconds)
	{
		int totalFrameCount = Math.Max(frames.Count, Mathf.CeilToInt((float)(animationSeconds / frameSeconds)));
		PlayTextureFrameSlots(textureRect, frames, frameSeconds, totalFrameCount);
	}

	public static void TweenTextureFramesForDuration(
		Tween tween,
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		double animationSeconds)
	{
		int totalFrameCount = Math.Max(frames.Count, Mathf.CeilToInt((float)(animationSeconds / frameSeconds)));
		PlayTextureFrameSlots(sprite, frames, frameSeconds, totalFrameCount);
	}

	public static void TweenTextureFrames(
		Tween tween,
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		PlayTextureFrames(sprite, frames, frameSeconds, repeatCount);
	}

	public static AnimationPlayer? PlayTextureFrames(
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		return PlayTextureFrameSlots(textureRect, frames, frameSeconds, frames.Count * repeatCount);
	}

	public static AnimationPlayer? PlayTextureFrames(
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		return PlayTextureFrameSlots(sprite, frames, frameSeconds, frames.Count * repeatCount);
	}

	public static AnimationPlayer? PlayTextureFrameSlots(
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int totalFrameCount)
	{
		return PlayTextureFrameSlots(textureRect, frames, frameSeconds, totalFrameCount, new NodePath(".."), new NodePath(".:texture"));
	}

	public static AnimationPlayer? PlayTextureFrameSlots(
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int totalFrameCount)
	{
		return PlayTextureFrameSlots(sprite, frames, frameSeconds, totalFrameCount, new NodePath(".."), new NodePath(".:texture"));
	}

	public static AnimationPlayer? PlayTextureKeyframes(
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		IReadOnlyList<double> keyTimes)
	{
		if (frames.Count == 0 || keyTimes.Count == 0 || !GodotObject.IsInstanceValid(sprite))
		{
			return null;
		}

		var animationPlayer = new AnimationPlayer
		{
			Name = "FrameAnimationPlayer",
			RootNode = new NodePath(".."),
		};
		sprite.AddChild(animationPlayer);
		ConfigureTextureAnimationKeyframes(
			animationPlayer,
			new StringName("frames"),
			new NodePath(".:texture"),
			frames,
			keyTimes);
		animationPlayer.Play("frames");
		animationPlayer.AnimationFinished += _ =>
		{
			if (GodotObject.IsInstanceValid(animationPlayer))
			{
				animationPlayer.QueueFree();
			}
		};
		return animationPlayer;
	}

	public static AnimationPlayer? PlayTextureKeyframes(
		Sprite2D sprite,
		Texture2D firstFrame,
		Texture2D secondFrame,
		double secondKeyTime)
	{
		if (!GodotObject.IsInstanceValid(sprite))
		{
			return null;
		}

		var animationPlayer = new AnimationPlayer
		{
			Name = "FrameAnimationPlayer",
			RootNode = new NodePath(".."),
		};
		sprite.AddChild(animationPlayer);
		ConfigureTwoTextureKeyframes(
			animationPlayer,
			new StringName("frames"),
			new NodePath(".:texture"),
			firstFrame,
			secondFrame,
			secondKeyTime);
		animationPlayer.Play("frames");
		animationPlayer.AnimationFinished += _ =>
		{
			if (GodotObject.IsInstanceValid(animationPlayer))
			{
				animationPlayer.QueueFree();
			}
		};
		return animationPlayer;
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

	private static AnimationPlayer? PlayTextureFrameSlots(
		Node target,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int totalFrameCount,
		NodePath rootNode,
		NodePath trackPath)
	{
		if (frames.Count == 0 || frameSeconds <= 0.0 || totalFrameCount <= 0 || !GodotObject.IsInstanceValid(target))
		{
			return null;
		}

		var animationPlayer = new AnimationPlayer
		{
			Name = "FrameAnimationPlayer",
			RootNode = rootNode,
		};
		target.AddChild(animationPlayer);
		ConfigureTextureAnimationSlots(
			animationPlayer,
			new StringName("frames"),
			trackPath,
			frames,
			frameSeconds,
			totalFrameCount,
			loops: false);
		animationPlayer.Play("frames");
		animationPlayer.AnimationFinished += _ =>
		{
			if (GodotObject.IsInstanceValid(animationPlayer))
			{
				animationPlayer.QueueFree();
			}
		};
		return animationPlayer;
	}

	private static void ConfigureTextureAnimationSlots(
		AnimationPlayer animationPlayer,
		StringName animationName,
		NodePath trackPath,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int totalFrameCount,
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
			Length = (float)Math.Max(frameSeconds, totalFrameCount * frameSeconds),
			LoopMode = loops ? Animation.LoopModeEnum.Linear : Animation.LoopModeEnum.None,
		};

		int trackIndex = animation.AddTrack(Animation.TrackType.Value);
		animation.TrackSetPath(trackIndex, trackPath);
		animation.TrackSetInterpolationType(trackIndex, Animation.InterpolationType.Nearest);
		animation.ValueTrackSetUpdateMode(trackIndex, Animation.UpdateMode.Discrete);

		for (int frameSlot = 0; frameSlot < totalFrameCount; frameSlot++)
		{
			animation.TrackInsertKey(trackIndex, frameSlot * frameSeconds, frames[frameSlot % frames.Count]);
		}

		if (!loops)
		{
			animation.TrackInsertKey(trackIndex, animation.Length, frames[(totalFrameCount - 1) % frames.Count]);
		}

		library.AddAnimation(animationName, animation);
	}

	private static void ConfigureTextureAnimationKeyframes(
		AnimationPlayer animationPlayer,
		StringName animationName,
		NodePath trackPath,
		IReadOnlyList<Texture2D> frames,
		IReadOnlyList<double> keyTimes)
	{
		AnimationLibrary library = GetOrCreateDefaultLibrary(animationPlayer);
		if (library.HasAnimation(animationName))
		{
			library.RemoveAnimation(animationName);
		}

		int keyCount = Math.Min(frames.Count, keyTimes.Count);
		var animation = new Animation
		{
			Length = (float)Math.Max(0.001, keyTimes[keyCount - 1] + 0.001),
			LoopMode = Animation.LoopModeEnum.None,
		};

		int trackIndex = animation.AddTrack(Animation.TrackType.Value);
		animation.TrackSetPath(trackIndex, trackPath);
		animation.TrackSetInterpolationType(trackIndex, Animation.InterpolationType.Nearest);
		animation.ValueTrackSetUpdateMode(trackIndex, Animation.UpdateMode.Discrete);

		for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
		{
			animation.TrackInsertKey(trackIndex, keyTimes[keyIndex], frames[keyIndex]);
		}

		library.AddAnimation(animationName, animation);
	}

	private static void ConfigureTwoTextureKeyframes(
		AnimationPlayer animationPlayer,
		StringName animationName,
		NodePath trackPath,
		Texture2D firstFrame,
		Texture2D secondFrame,
		double secondKeyTime)
	{
		AnimationLibrary library = GetOrCreateDefaultLibrary(animationPlayer);
		if (library.HasAnimation(animationName))
		{
			library.RemoveAnimation(animationName);
		}

		double clampedSecondKeyTime = Math.Max(0.0d, secondKeyTime);
		var animation = new Animation
		{
			Length = (float)Math.Max(0.001d, clampedSecondKeyTime + 0.001d),
			LoopMode = Animation.LoopModeEnum.None,
		};

		int trackIndex = animation.AddTrack(Animation.TrackType.Value);
		animation.TrackSetPath(trackIndex, trackPath);
		animation.TrackSetInterpolationType(trackIndex, Animation.InterpolationType.Nearest);
		animation.ValueTrackSetUpdateMode(trackIndex, Animation.UpdateMode.Discrete);
		animation.TrackInsertKey(trackIndex, 0.0d, firstFrame);
		animation.TrackInsertKey(trackIndex, clampedSecondKeyTime, secondFrame);

		library.AddAnimation(animationName, animation);
	}

	private static AnimationLibrary GetOrCreateDefaultLibrary(AnimationPlayer animationPlayer)
	{
		var libraryName = new StringName("");
		if (animationPlayer.HasAnimationLibrary(libraryName))
		{
			return animationPlayer.GetAnimationLibrary(libraryName);
		}

		var library = new AnimationLibrary();
		animationPlayer.AddAnimationLibrary(libraryName, library);
		return library;
	}
}
