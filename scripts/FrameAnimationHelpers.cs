using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

internal static class FrameAnimationHelpers
{
	private static readonly Dictionary<string, Texture2D?> TextureCache = new();

	public static Texture2D[] LoadTextureFrames(string framePattern, int frameCount, string warningLabel, bool preferImageFile = false)
	{
		var frames = new List<Texture2D>(frameCount);
		for (int frameIndex = 1; frameIndex <= frameCount; frameIndex++)
		{
			string path = string.Format(CultureInfo.InvariantCulture, framePattern, frameIndex);
			Texture2D? texture = LoadTextureResource(path, preferImageFile);
			if (texture == null)
			{
				GD.PushWarning($"Missing {warningLabel} frame: {path}");
				continue;
			}

			frames.Add(texture);
		}

		return frames.ToArray();
	}

	public static Texture2D? LoadTextureResource(string path, bool allowEditorImageFallback = false)
	{
		if (TextureCache.TryGetValue(path, out Texture2D? cachedTexture))
		{
			return cachedTexture;
		}

		Texture2D? texture = GD.Load<Texture2D>(path);
		if (texture == null && allowEditorImageFallback && Engine.IsEditorHint())
		{
			texture = LoadTextureFromImageFile(path);
		}

		TextureCache[path] = texture;
		return texture;
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
		TweenTextureFrameSlots(
			tween,
			textureRect,
			frames,
			frameSeconds,
			frames.Count * repeatCount,
			(target, texture) => target.Texture = texture);
	}

	public static void TweenTextureFramesForDuration(
		Tween tween,
		TextureRect textureRect,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		double animationSeconds)
	{
		int totalFrameCount = Math.Max(frames.Count, Mathf.CeilToInt((float)(animationSeconds / frameSeconds)));
		TweenTextureFrameSlots(
			tween,
			textureRect,
			frames,
			frameSeconds,
			totalFrameCount,
			(target, texture) => target.Texture = texture);
	}

	public static void TweenTextureFramesForDuration(
		Tween tween,
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		double animationSeconds)
	{
		int totalFrameCount = Math.Max(frames.Count, Mathf.CeilToInt((float)(animationSeconds / frameSeconds)));
		TweenTextureFrameSlots(
			tween,
			sprite,
			frames,
			frameSeconds,
			totalFrameCount,
			(target, texture) => target.Texture = texture);
	}

	public static void TweenTextureFrames(
		Tween tween,
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int repeatCount = 1)
	{
		TweenTextureFrameSlots(
			tween,
			sprite,
			frames,
			frameSeconds,
			frames.Count * repeatCount,
			(target, texture) => target.Texture = texture);
	}

	private static void TweenTextureFrameSlots<T>(
		Tween tween,
		T target,
		IReadOnlyList<Texture2D> frames,
		double frameSeconds,
		int totalFrameCount,
		Action<T, Texture2D> applyFrame)
		where T : Node
	{
		if (frames.Count == 0
			|| frameSeconds <= 0.0
			|| totalFrameCount <= 0
			|| !GodotObject.IsInstanceValid(target))
		{
			return;
		}

		Tween frameTween = target.CreateTween();
		frameTween.SetParallel(true);
		for (int frameSlot = 0; frameSlot < totalFrameCount; frameSlot++)
		{
			Texture2D frame = frames[frameSlot % frames.Count];
			frameTween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(target))
				{
					applyFrame(target, frame);
				}
			})).SetDelay(frameSlot * frameSeconds);
		}
	}

	public static void PlayTextureKeyframes(
		Sprite2D sprite,
		IReadOnlyList<Texture2D> frames,
		IReadOnlyList<double> keyTimes)
	{
		int keyCount = Math.Min(frames.Count, keyTimes.Count);
		if (keyCount == 0 || !GodotObject.IsInstanceValid(sprite))
		{
			return;
		}

		sprite.Texture = frames[0];
		Tween tween = sprite.CreateTween();
		tween.SetParallel(true);
		for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
		{
			Texture2D frame = frames[keyIndex];
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(sprite))
				{
					sprite.Texture = frame;
				}
			})).SetDelay(Math.Max(0.0d, keyTimes[keyIndex]));
		}

	}

	public static void PlayTextureKeyframes(
		Sprite2D sprite,
		Texture2D firstFrame,
		Texture2D secondFrame,
		double secondKeyTime)
	{
		if (!GodotObject.IsInstanceValid(sprite))
		{
			return;
		}

		sprite.Texture = firstFrame;
		Tween tween = sprite.CreateTween();
		tween.TweenCallback(Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(sprite))
			{
				sprite.Texture = secondFrame;
			}
		})).SetDelay(Math.Max(0.0d, secondKeyTime));
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
