using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task PlayEnemySpecialAbilityAvatarFxAsync(
		int enemyPlayerIndex,
		int turnSequenceGeneration,
		string bossKBorrowedAvatarId = "")
	{
		EnsureAdventureModeResourcesLoaded();

		if (!adventure_mode
			|| enemyPlayerIndex < 0
			|| enemyPlayerIndex >= player_count
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver())
		{
			return;
		}

		Task? bossKBorrowedAvatarFxTask = ShouldPlayBossKBorrowedAbilityAvatarFx(enemyPlayerIndex, bossKBorrowedAvatarId)
			? PlayBossKBorrowedAbilityAvatarFxAsync(enemyPlayerIndex, bossKBorrowedAvatarId, turnSequenceGeneration)
			: null;

		PlayPlayerAvatarAction(enemyPlayerIndex, CharacterAvatar.AvatarAction.Score);
		if (_enemySpecialAbilityAvatarFxFrames.Count == 0 || !IsInsideTree())
		{
			if (bossKBorrowedAvatarFxTask != null)
			{
				await bossKBorrowedAvatarFxTask;
			}

			return;
		}

		Texture2D firstFrame = _enemySpecialAbilityAvatarFxFrames[0];
		Vector2 baseScale = GetSpriteScaleForVisualSize(firstFrame, GetPlayerAvatarFxVisualSize(enemyPlayerIndex));
		Sprite2D abilityFx = RentSpriteFx(
			"EnemySpecialAbilityAvatarFx",
			firstFrame,
			EnemySpecialAbilityAvatarFxZIndex,
			GetPlayerAvatarGlobalCenter(enemyPlayerIndex),
			baseScale * 0.88f,
			new Color(1.0f, 1.0f, 1.0f, 0.0f));

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			FrameAnimationHelpers.TweenTextureFramesForDuration(
				tween,
				abilityFx,
				_enemySpecialAbilityAvatarFxFrames,
				EnemySpecialAbilityAvatarFxFrameSeconds,
				EnemySpecialAbilityAvatarFxTotalSeconds);
			tween.TweenProperty(abilityFx, "modulate:a", 0.88f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.18f);
			tween.TweenProperty(abilityFx, "modulate:a", 0.0f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.22f)
				.SetDelay(EnemySpecialAbilityAvatarFxTotalSeconds * 0.78f);
			tween.TweenProperty(abilityFx, "scale", baseScale * 1.08f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.46f);
			tween.TweenProperty(abilityFx, "scale", baseScale * 0.96f, EnemySpecialAbilityAvatarFxTotalSeconds * 0.54f)
				.SetDelay(EnemySpecialAbilityAvatarFxTotalSeconds * 0.46f);
			tween.TweenMethod(
				Callable.From<float>(_ =>
				{
					if (GodotObject.IsInstanceValid(abilityFx))
					{
						abilityFx.GlobalPosition = GetPlayerAvatarGlobalCenter(enemyPlayerIndex);
					}
				}),
				0.0f,
				1.0f,
				EnemySpecialAbilityAvatarFxTotalSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
			if (bossKBorrowedAvatarFxTask != null)
			{
				await bossKBorrowedAvatarFxTask;
			}
		}
		finally
		{
			if (GodotObject.IsInstanceValid(abilityFx))
			{
				_animationPresenter.Release(abilityFx);
			}
		}
	}

	private async Task PlayEnemySpecialAbilityAvatarFxForAbilityAsync(
		int enemyPlayerIndex,
		int turnSequenceGeneration,
		string abilityId)
	{
		PlayAdventureAbilitySfx(enemyPlayerIndex, abilityId);
		string bossKBorrowedAvatarId = IsBossKEnemy(enemyPlayerIndex)
			&& CharacterAvatarCatalog.TryGetAvatarIdForAbility(abilityId, out string avatarId, requireBossBorrowedFxEligible: true)
				? avatarId
				: string.Empty;

		await PlayEnemySpecialAbilityAvatarFxAsync(enemyPlayerIndex, turnSequenceGeneration, bossKBorrowedAvatarId);
	}

	private async Task PlayBossKBorrowedAbilityAvatarFxAsync(
		int bossPlayerIndex,
		string borrowedAvatarId,
		int turnSequenceGeneration)
	{
		if (!ShouldPlayBossKBorrowedAbilityAvatarFx(bossPlayerIndex, borrowedAvatarId)
			|| !TryGetBossKBorrowedAbilityAvatarDefinition(borrowedAvatarId, out CharacterAvatarDefinition borrowedAvatarDefinition)
			|| bossPlayerIndex >= _playerAvatarAreas.Length
			|| _playerAvatarAreas[bossPlayerIndex] is not DraggableAvatarArea avatarArea
			|| !GodotObject.IsInstanceValid(avatarArea)
			|| !IsInsideTree())
		{
			return;
		}

		PackedScene? avatarScene = GD.Load<PackedScene>(CharacterAvatarScenePath);
		if (avatarScene == null)
		{
			GD.PushWarning($"Missing avatar scene for Boss K borrowed ability FX: {CharacterAvatarScenePath}");
			return;
		}

		Node avatarNode = avatarScene.Instantiate();
		if (avatarNode is not CharacterAvatar borrowedAvatar)
		{
			GD.PushWarning($"{CharacterAvatarScenePath} root must use CharacterAvatar for Boss K borrowed ability FX.");
			avatarNode.QueueFree();
			return;
		}

		borrowedAvatar.Name = $"BossKBorrowed{borrowedAvatarDefinition.Id}AvatarFx";
		borrowedAvatar.MouseFilter = MouseFilterEnum.Ignore;
		borrowedAvatar.AvatarRoot = borrowedAvatarDefinition.RootPath;
		borrowedAvatar.AudioConfig = borrowedAvatarDefinition.AudioConfig;
		borrowedAvatar.AccentColor = PlayerAvatarAccentColors[bossPlayerIndex];
		borrowedAvatar.ShowFrame = false;
		borrowedAvatar.LoadActionFrames = true;
		borrowedAvatar.PortraitScale = BossKBorrowedAbilityAvatarFxPortraitScale;
		borrowedAvatar.ZIndex = BossKBorrowedAbilityAvatarFxZIndex;
		borrowedAvatar.ZAsRelative = false;
		borrowedAvatar.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		borrowedAvatar.Scale = Vector2.One * BossKBorrowedAbilityAvatarFxStartScale;
		avatarArea.AddChild(borrowedAvatar);
		FitAvatarToArea(borrowedAvatar);
		RefreshBossKBorrowedAbilityAvatarFxLayout(borrowedAvatar, avatarArea);

		try
		{
			if (!borrowedAvatar.IsNodeReady())
			{
				await ToSignal(borrowedAvatar, Node.SignalName.Ready);
			}

			if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
			{
				return;
			}

			borrowedAvatar.PlayAction(CharacterAvatar.AvatarAction.Score);

			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Sine);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(
				borrowedAvatar,
				"modulate:a",
				BossKBorrowedAbilityAvatarFxPeakAlpha,
				BossKBorrowedAbilityAvatarFxTotalSeconds * 0.20f);
			tween.TweenProperty(
				borrowedAvatar,
				"modulate:a",
				0.0f,
				BossKBorrowedAbilityAvatarFxTotalSeconds * 0.28f)
				.SetDelay(BossKBorrowedAbilityAvatarFxTotalSeconds * 0.72f);
			tween.TweenProperty(
				borrowedAvatar,
				"scale",
				Vector2.One * BossKBorrowedAbilityAvatarFxPeakScale,
				BossKBorrowedAbilityAvatarFxTotalSeconds * 0.50f);
			tween.TweenProperty(
				borrowedAvatar,
				"scale",
				Vector2.One * BossKBorrowedAbilityAvatarFxEndScale,
				BossKBorrowedAbilityAvatarFxTotalSeconds * 0.50f)
				.SetDelay(BossKBorrowedAbilityAvatarFxTotalSeconds * 0.50f);
			tween.TweenMethod(
				Callable.From<float>(_ => RefreshBossKBorrowedAbilityAvatarFxLayout(borrowedAvatar, avatarArea)),
				0.0f,
				1.0f,
				BossKBorrowedAbilityAvatarFxTotalSeconds);

			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(borrowedAvatar))
			{
				borrowedAvatar.QueueFree();
			}
		}
	}

	private bool ShouldPlayBossKBorrowedAbilityAvatarFx(int bossPlayerIndex, string borrowedAvatarId)
	{
		return IsBossKEnemy(bossPlayerIndex)
			&& !string.IsNullOrWhiteSpace(borrowedAvatarId);
	}

	private static bool TryGetBossKBorrowedAbilityAvatarDefinition(
		string borrowedAvatarId,
		out CharacterAvatarDefinition borrowedAvatarDefinition)
	{
		foreach (CharacterAvatarDefinition avatarDefinition in CharacterAvatarCatalog.All)
		{
			if (!string.Equals(avatarDefinition.Id, borrowedAvatarId, StringComparison.Ordinal)
				&& !string.Equals(avatarDefinition.DisplayName, borrowedAvatarId, StringComparison.Ordinal))
			{
				continue;
			}

			borrowedAvatarDefinition = avatarDefinition;
			return avatarDefinition.BossBorrowedAbilityFxEligible;
		}

		borrowedAvatarDefinition = CharacterAvatarCatalog.Default;
		return false;
	}

	private static void RefreshBossKBorrowedAbilityAvatarFxLayout(
		CharacterAvatar borrowedAvatar,
		DraggableAvatarArea avatarArea)
	{
		if (!GodotObject.IsInstanceValid(borrowedAvatar) || !GodotObject.IsInstanceValid(avatarArea))
		{
			return;
		}

		borrowedAvatar.Position = Vector2.Zero;
		borrowedAvatar.Size = avatarArea.Size;
		borrowedAvatar.PivotOffset = borrowedAvatar.Size / 2.0f;
	}

}
