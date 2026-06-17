using System;
using System.Collections.Generic;
using Godot;

public partial class CharacterAvatar
{
	private void LoadGeneratedActionFrames()
	{
		_actionFrames.Clear();
		AvatarFrameCache cache = LoadAvatarFrames(GetResolvedAvatarRoot());
		_idleFallbackTexture = cache.IdleFallbackTexture;
		if (!LoadActionFrames)
		{
			return;
		}

		foreach ((AvatarAction action, Texture2D[] frames) in cache.ActionFrames)
		{
			_actionFrames[action] = frames;
		}
	}

	private void LoadAttackFxFrames()
	{
		_attackFxFrames = LoadFxFrames(AttackFxRoot);
		HideAttackFx();
	}

	private void LoadFeedbackFxFrames()
	{
		_hitFeedbackFxFrames = AvatarFrameCacheService.LoadNamedFxFrames("res://art/fx/avatar_feedback_hit/run-001");
		_victoryFeedbackFxFrames = AvatarFrameCacheService.LoadNamedFxFrames("res://art/fx/avatar_feedback_victory/run-001");
		_defeatFeedbackFxFrames = AvatarFrameCacheService.LoadNamedFxFrames("res://art/fx/avatar_feedback_defeat/run-001");
		HideFeedbackFx();
	}

	private void PlayAttackFx()
	{
		if (_attackFxFrames.Length == 0)
		{
			return;
		}

		LayoutAttackFx();
		_attackFx.Visible = true;
		_attackFx.Texture = _attackFxFrames[0];
		_attackFx.Modulate = Colors.White;
		_attackFx.Scale = Vector2.One * AttackFxStartScale;

		_attackFxTween = CreateTween();
		_attackFxTween.SetParallel(true);
		_attackFxTween.SetTrans(Tween.TransitionType.Sine);
		_attackFxTween.SetEase(Tween.EaseType.Out);
		_attackFxTween.TweenProperty(_attackFx, "scale", Vector2.One * AttackFxEndScale, AttackFxFrameSeconds * AttackFxFrameCount);
		_attackFxTween.TweenProperty(_attackFx, "modulate:a", 0.0f, AttackFxFrameSeconds * AttackFxFrameCount);
		FrameAnimationHelpers.TweenTextureFrames(_attackFxTween, _attackFx, _attackFxFrames, AttackFxFrameSeconds);
		_attackFxTween.Finished += HideAttackFx;
	}

	private void HideAttackFx()
	{
		if (_attackFx == null)
		{
			return;
		}

		_attackFx.Visible = false;
		_attackFx.Texture = null;
		_attackFx.Modulate = Colors.White;
		_attackFx.Scale = Vector2.One;
	}

	private void PlayFeedbackFx(Texture2D[] frames, float seconds)
	{
		if (_feedbackFx == null || frames.Length == 0)
		{
			return;
		}

		_feedbackFxTween?.Kill();
		_feedbackFxTween = null;
		LayoutFeedbackFx();
		_feedbackFx.Visible = true;
		_feedbackFx.Texture = frames[0];
		_feedbackFx.Modulate = Colors.White;
		_feedbackFx.Scale = Vector2.One * 0.92f;

		float frameSeconds = Mathf.Max(0.04f, seconds / Mathf.Max(1, frames.Length));
		_feedbackFxTween = CreateTween();
		_feedbackFxTween.SetParallel(true);
		_feedbackFxTween.SetTrans(Tween.TransitionType.Sine);
		_feedbackFxTween.SetEase(Tween.EaseType.Out);
		_feedbackFxTween.TweenProperty(_feedbackFx, "scale", Vector2.One * 1.05f, seconds);
		_feedbackFxTween.TweenProperty(_feedbackFx, "modulate:a", 0.0f, seconds);
		FrameAnimationHelpers.TweenTextureFrames(_feedbackFxTween, _feedbackFx, frames, frameSeconds);
		_feedbackFxTween.Finished += HideFeedbackFx;
	}

	private void HideFeedbackFx()
	{
		if (_feedbackFx == null)
		{
			return;
		}

		_feedbackFx.Visible = false;
		_feedbackFx.Texture = null;
		_feedbackFx.Modulate = Colors.White;
		_feedbackFx.Scale = Vector2.One;
	}

	private void BeginActionFrames(AvatarAction action)
	{
		_frameAction = action;
		if (_actionFrames.TryGetValue(_frameAction, out Texture2D[]? frames) && frames.Length > 0)
		{
			StringName animationName = GetActionAnimationName(action);
			_portrait.Texture = frames[0];
			FrameAnimationHelpers.ConfigureTextureAnimation(
				_frameAnimationPlayer,
				animationName,
			new NodePath("PortraitPivot/Portrait:texture"),
			frames,
			GetActionFrameDuration(action),
			action == AvatarAction.Idle);
			_frameAnimationPlayer.Play(animationName);
			return;
		}

		_frameAnimationPlayer.Stop();
		if (_idleFallbackTexture != null)
		{
			_portrait.Texture = _idleFallbackTexture;
			return;
		}

		if (PortraitTexture != null)
		{
			_portrait.Texture = PortraitTexture;
		}
	}

	private void ShowActionStillFrame(AvatarAction action)
	{
		_frameAction = action;
		_frameAnimationPlayer.Stop();
		if (_actionFrames.TryGetValue(action, out Texture2D[]? frames) && frames.Length > 0)
		{
			_portrait.Texture = frames[0];
			return;
		}

		if (_idleFallbackTexture != null)
		{
			_portrait.Texture = _idleFallbackTexture;
			return;
		}

		if (PortraitTexture != null)
		{
			_portrait.Texture = PortraitTexture;
		}
	}

	private AnimationPlayer CreateFrameAnimationPlayer()
	{
		var animationPlayer = new AnimationPlayer
		{
			Name = "AnimationPlayer",
			RootNode = new NodePath(".."),
		};
		AddChild(animationPlayer);
		return animationPlayer;
	}

	private TextureRect CreateAttackFxRect()
	{
		var attackFx = new TextureRect
		{
			Name = "AttackFx",
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ZIndex = AttackFxZIndex,
			Visible = false,
		};
		AddChild(attackFx);
		return attackFx;
	}

	private TextureRect CreateFeedbackFxRect()
	{
		var feedbackFx = new TextureRect
		{
			Name = "FeedbackFx",
			MouseFilter = MouseFilterEnum.Ignore,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
			ZIndex = AttackFxZIndex + 1,
			Visible = false,
		};
		AddChild(feedbackFx);
		return feedbackFx;
	}

	private static AvatarFrameCache LoadAvatarFrames(string avatarRoot)
	{
		return AvatarFrameCacheService.LoadAvatarFrames(avatarRoot);
	}

	private static Texture2D[] LoadFxFrames(string fxRoot)
	{
		return AvatarFrameCacheService.LoadFxFrames(fxRoot);
	}

	private static StringName GetActionAnimationName(AvatarAction action)
	{
		return new StringName($"avatar_{GetActionSlug(action)}");
	}

	private static class AvatarFrameCacheService
	{
		internal static AvatarFrameCache LoadAvatarFrames(string avatarRoot)
		{
			if (ActionFrameCache.TryGetValue(avatarRoot, out AvatarFrameCache? cachedFrames))
			{
				return cachedFrames;
			}

			var actionFrames = new Dictionary<AvatarAction, Texture2D[]>();
			Texture2D? idleFallbackTexture = ResourceLoader.Load<Texture2D>($"{avatarRoot}/idle/processed/idle-1.png");
			foreach (AvatarAction action in Enum.GetValues<AvatarAction>())
			{
				string slug = GetActionSlug(action);
				Texture2D[] frames = new Texture2D[GetActionFrameCount(avatarRoot, action)];
				bool loadedAllFrames = true;

				for (int index = 0; index < frames.Length; index++)
				{
					string path = $"{avatarRoot}/{slug}/processed/{slug}-{index + 1}.png";
					Texture2D? texture = ResourceLoader.Load<Texture2D>(path);
					if (texture == null)
					{
						loadedAllFrames = false;
						break;
					}

					frames[index] = texture;
				}

				if (loadedAllFrames)
				{
					actionFrames[action] = frames;
				}
			}

			cachedFrames = new AvatarFrameCache(idleFallbackTexture, actionFrames);
			ActionFrameCache[avatarRoot] = cachedFrames;
			return cachedFrames;
		}

		internal static Texture2D[] LoadFxFrames(string fxRoot)
		{
			if (string.IsNullOrWhiteSpace(fxRoot))
			{
				return Array.Empty<Texture2D>();
			}

			if (AttackFxFrameCache.TryGetValue(fxRoot, out Texture2D[]? cachedFrames))
			{
				return cachedFrames;
			}

			Texture2D[] frames = FrameAnimationHelpers.LoadTextureFrames(
				$"{fxRoot}/processed/fx-{{0}}.png",
				AttackFxFrameCount,
				"avatar attack effect",
				preferImageFile: true);
			AttackFxFrameCache[fxRoot] = frames;
			return frames;
		}

		internal static Texture2D[] LoadNamedFxFrames(string fxRoot)
		{
			if (string.IsNullOrWhiteSpace(fxRoot))
			{
				return Array.Empty<Texture2D>();
			}

			if (FeedbackFxFrameCache.TryGetValue(fxRoot, out Texture2D[]? cachedFrames))
			{
				return cachedFrames;
			}

			var frames = new List<Texture2D>();
			for (int index = 1; index <= 8; index++)
			{
				Texture2D? frame = FrameAnimationHelpers.LoadExistingTextureResource($"{fxRoot}/processed/fx-{index}.png", allowEditorImageFallback: true);
				if (frame == null)
				{
					break;
				}

				frames.Add(frame);
			}

			Texture2D[] result = frames.ToArray();
			FeedbackFxFrameCache[fxRoot] = result;
			return result;
		}
	}

}
