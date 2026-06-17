using System;
using System.Collections.Generic;
using Godot;

public partial class CharacterAvatar
{
	private void LayoutPortrait()
	{
		if (_portraitPivot == null)
		{
			return;
		}

		float side = Mathf.Max(1.0f, Mathf.Min(Size.X, Size.Y) * Mathf.Clamp(PortraitScale, 0.55f, 0.95f));
		_portraitPivot.Size = new Vector2(side, side);
		_portraitPivot.PivotOffset = _portraitPivot.Size / 2.0f;
		_basePivotPosition = (Size - _portraitPivot.Size) / 2.0f;

		if (_currentAction == AvatarAction.Idle)
		{
			_portraitPivot.Position = _basePivotPosition;
			_portraitPivot.Scale = Vector2.One;
		}

		LayoutFrameTextures();
		LayoutAttackFx();
		LayoutFeedbackFx();
		ApplyFrameTextures();
	}

	private void LayoutFrameTextures()
	{
		if (_frameBase == null || _frameAccent == null)
		{
			return;
		}

		float side = Mathf.Max(1.0f, Mathf.Min(Size.X, Size.Y) * 0.96f);
		Vector2 textureSize = new(side, side);
		Vector2 position = (Size - textureSize) / 2.0f;

		_frameBase.Size = textureSize;
		_frameBase.Position = position;
		_frameBase.PivotOffset = textureSize / 2.0f;

		_frameAccent.Size = textureSize;
		_frameAccent.Position = position;
		_frameAccent.PivotOffset = textureSize / 2.0f;
	}

	private void LayoutAttackFx()
	{
		if (_attackFx == null)
		{
			return;
		}

		float side = Mathf.Max(1.0f, Mathf.Min(Size.X, Size.Y) * AttackFxVisualScale);
		_attackFx.Size = new Vector2(side, side);
		_attackFx.PivotOffset = _attackFx.Size / 2.0f;
		_attackFx.Position = (Size - _attackFx.Size) / 2.0f;
	}

	private void LayoutFeedbackFx()
	{
		if (_feedbackFx == null)
		{
			return;
		}

		float side = Mathf.Max(1.0f, Mathf.Min(Size.X, Size.Y) * FeedbackFxVisualScale);
		_feedbackFx.Size = new Vector2(side, side);
		_feedbackFx.PivotOffset = _feedbackFx.Size / 2.0f;
		_feedbackFx.Position = (Size - _feedbackFx.Size) / 2.0f;
	}

	private void UpdateAttackFxRootForAvatarRoot(string previousRoot)
	{
		string previousDefaultFxRoot = GetDefaultAttackFxRoot(previousRoot);
		if (!string.IsNullOrWhiteSpace(_attackFxRoot)
			&& !string.Equals(_attackFxRoot, previousDefaultFxRoot, StringComparison.Ordinal))
		{
			return;
		}

		_attackFxRoot = GetDefaultAttackFxRoot(GetResolvedAvatarRoot());
	}

	private static string GetDefaultAttackFxRoot(string avatarRoot)
	{
		if (string.IsNullOrWhiteSpace(avatarRoot))
		{
			return string.Empty;
		}

		return CharacterAvatarCatalog.GetAttackFxRoot(avatarRoot);
	}

	private string GetResolvedAvatarRoot()
	{
		return string.IsNullOrWhiteSpace(_avatarRoot)
			? CharacterAvatarCatalog.Default.RootPath
			: _avatarRoot;
	}

	private static bool TryParseAction(string actionName, out AvatarAction action)
	{
		string normalized = actionName.Trim().ToLowerInvariant();
		switch (normalized)
		{
			case "idle":
			case "待机":
				action = AvatarAction.Idle;
				return true;
			case "score":
			case "scored":
			case "得分":
				action = AvatarAction.Score;
				return true;
			case "attack":
			case "攻击":
				action = AvatarAction.Attack;
				return true;
			case "hit":
			case "hurt":
			case "damaged":
			case "受到攻击":
			case "受击":
				action = AvatarAction.Hit;
				return true;
			case "victory":
			case "win":
			case "胜利":
				action = AvatarAction.Victory;
				return true;
			case "defeat":
			case "lose":
			case "loss":
			case "失败":
				action = AvatarAction.Defeat;
				return true;
			default:
				action = AvatarAction.Idle;
				return false;
		}
	}

	private static string GetActionSlug(AvatarAction action)
	{
		return action switch
		{
			AvatarAction.Idle => "idle",
			AvatarAction.Score => "score",
			AvatarAction.Attack => "attack",
			AvatarAction.Hit => "hit",
			AvatarAction.Victory => "victory",
			AvatarAction.Defeat => "defeat",
			_ => "idle",
		};
	}

	private float GetActionFrameDuration(AvatarAction action)
	{
		return action switch
		{
			AvatarAction.Idle => GetIdleFrameDuration(),
			AvatarAction.Defeat => 0.06f,
			_ => 0.08f,
		};
	}

	private float GetIdleFrameDuration()
	{
		float configured = CharacterAvatarCatalog.GetIdleFrameSeconds(GetResolvedAvatarRoot());
		return configured > 0.0f ? configured : 0.12f;
	}

	private int GetActionFrameCount(AvatarAction action)
	{
		return GetActionFrameCount(GetResolvedAvatarRoot(), action);
	}

	private static int GetActionFrameCount(string avatarRoot, AvatarAction action)
	{
		return action == AvatarAction.Idle
			? CharacterAvatarCatalog.GetIdleFrameCount(avatarRoot)
			: CharacterAvatarCatalog.GetActionFrameCount(avatarRoot);
	}

	private sealed record AvatarFrameCache(Texture2D? IdleFallbackTexture, Dictionary<AvatarAction, Texture2D[]> ActionFrames);}
