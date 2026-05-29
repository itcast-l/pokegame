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

	private void AnimateDrawPulse(Action<float> applyPulse, float from, float to, float seconds)
	{
		applyPulse(from);
		QueueRedraw();
		_pulseTween?.Kill();
		_pulseTween = CreateTween();
		_pulseTween.SetTrans(Tween.TransitionType.Cubic);
		_pulseTween.SetEase(Tween.EaseType.Out);
		_pulseTween.TweenMethod(Callable.From<float>(value =>
		{
			applyPulse(value);
			QueueRedraw();
		}), from, to, seconds);
	}

	private static AvatarFrameCache LoadAvatarFrames(string avatarRoot)
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

	private static Texture2D[] LoadFxFrames(string fxRoot)
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

	private static StringName GetActionAnimationName(AvatarAction action)
	{
		return new StringName($"avatar_{GetActionSlug(action)}");
	}

}
