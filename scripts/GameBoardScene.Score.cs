using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private const int DefaultWinningScore = 100;
	private const int AdventurePlayerStartingHealth = 100;
	private const int AdventureEnemyNoAbilityStartingHealth = 50;
	private const int AdventureEnemyAbilityStartingHealth = 100;
	private const int AdventureEnemyRoyalStartingHealth = 100;
	private const int AdventureEnemyBossStartingHealth = 100;
	private const int AdventureEnemyPlayerIndex = 1;
	private static readonly Color[] AvatarScoreFillColors =
	{
		new(0.94f, 0.74f, 0.36f, 0.52f),
		new(0.36f, 0.80f, 0.94f, 0.52f),
		new(0.94f, 0.46f, 0.72f, 0.50f),
		new(0.40f, 0.88f, 0.62f, 0.50f),
	};
	private static readonly Color[] AvatarScoreGlowColors =
	{
		new(1.0f, 0.95f, 0.72f, 0.36f),
		new(0.82f, 0.98f, 1.0f, 0.36f),
		new(1.0f, 0.82f, 0.92f, 0.34f),
		new(0.82f, 1.0f, 0.90f, 0.34f),
	};

	private IReadOnlyList<int> _playerScores => CurrentRuleProjection.PlayerScores;
	private readonly ScoreAvatarMeter?[] _playerScoreMeters = new ScoreAvatarMeter?[MaxPlayers];
	private readonly ScoreEffectTarget?[] _playerScoreEffectTargets = new ScoreEffectTarget?[MaxPlayers];

	private void BindPlayerScoreMeters()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			int viewSlotIndex = GetPlayerViewSlotIndex(playerIndex);
			ScoreAvatarMeter meter = _nodeBindings.ScoreMeters[viewSlotIndex];

				BindPlayerStatusTooltipHover(meter, playerIndex);
				meter.Configure(AvatarScoreFillColors[playerIndex], AvatarScoreGlowColors[playerIndex]);
				meter.SetScore(GetStartingPlayerScore(playerIndex), GetScoreLimit(playerIndex), false);
				_playerScoreMeters[playerIndex] = meter;
				_playerScoreEffectTargets[playerIndex] = _nodeBindings.ScoreEffectTargets[viewSlotIndex];
			}
	}

	private void ResetPlayerScores()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			ScoreAvatarMeter? meter = _playerScoreMeters[playerIndex];
			meter?.SetScore(
				GetStartingPlayerScore(playerIndex),
				GetScoreLimit(playerIndex),
				false);
		}
	}

	private void RefreshPlayerScorePresentation()
	{
		for (int playerIndex = 0; playerIndex < MaxPlayers; playerIndex++)
		{
			UpdatePlayerScoreMeter(playerIndex, false);
		}

		RefreshGameplayTensionMusic();
	}

	private void AddPlayerScore(int playerIndex, int points)
	{
		if (playerIndex < 0 || playerIndex >= _playerScores.Count || points <= 0)
		{
			return;
		}

		int targetPlayerIndex = playerIndex;
		RuleScoreMutationKind mutationKind = RuleScoreMutationKind.Add;
		if (adventure_mode)
		{
			targetPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
			if (targetPlayerIndex < 0 || targetPlayerIndex >= _playerScores.Count)
			{
				return;
			}

			if (targetPlayerIndex == MainPlayerIndex && ShouldUseAdventureUpgradeSystem())
			{
				GD.PushError("Adventure player damage must be committed through the adventure save gate.");
				return;
			}
			mutationKind = RuleScoreMutationKind.Subtract;
		}

		int previousScore = _playerScores[targetPlayerIndex];
		if (!TrySubmitScoreMutationsRuleCommand(
			ImmutableArray.Create(new RuleScoreMutation(targetPlayerIndex, mutationKind, points)),
			out _))
		{
			return;
		}

		if (adventure_mode)
		{
			PresentAdventureDamageInMemory(playerIndex, targetPlayerIndex, previousScore);
			return;
		}

		UpdatePlayerScoreMeter(playerIndex, true);
		PlayPlayerAvatarAction(playerIndex, CharacterAvatar.AvatarAction.Score);
		RefreshGameplayTensionMusic();
	}

	private void ApplyAdventureDamageInMemory(
		int sourcePlayerIndex,
		int targetPlayerIndex,
		int points)
	{
		if (targetPlayerIndex < 0
			|| targetPlayerIndex >= _playerScores.Count
			|| points <= 0)
		{
			return;
		}

		int previousScore = _playerScores[targetPlayerIndex];
		if (!TrySubmitScoreMutationsRuleCommand(
			ImmutableArray.Create(new RuleScoreMutation(
				targetPlayerIndex,
				RuleScoreMutationKind.Subtract,
				points)),
			out _))
		{
			return;
		}

		PresentAdventureDamageInMemory(sourcePlayerIndex, targetPlayerIndex, previousScore);
	}

	private void PresentAdventureDamageInMemory(
		int sourcePlayerIndex,
		int targetPlayerIndex,
		int previousScore)
	{
		int damagedScore = _playerScores[targetPlayerIndex];
		PlayAvatarDamagePopup(targetPlayerIndex, Math.Max(0, previousScore - damagedScore));
		UpdatePlayerScoreMeter(targetPlayerIndex, true);
		PlayPlayerAvatarAction(sourcePlayerIndex, CharacterAvatar.AvatarAction.Score);
		if (targetPlayerIndex != sourcePlayerIndex)
		{
			PlayPlayerAvatarAction(targetPlayerIndex, CharacterAvatar.AvatarAction.Hit);
		}
		RefreshGameplayTensionMusic();
	}

	private void ApplyCommittedAdventurePlayerVitals(
		int sourcePlayerIndex,
		int rawDamage,
		int currentHealth,
		AdventureLowHealthRescueResult rescueResult,
		bool consumedHalfHealthRescue,
		bool consumedQuarterHealthRescue)
	{
		int previousHealth = _playerScores[MainPlayerIndex];
		if (!TrySubmitScoreMutationsRuleCommand(
			ImmutableArray.Create(new RuleScoreMutation(
				MainPlayerIndex,
				RuleScoreMutationKind.Set,
				currentHealth)),
			out _))
		{
			return;
		}

		PresentCommittedAdventurePlayerVitals(
			sourcePlayerIndex,
			rawDamage,
			previousHealth,
			rescueResult,
			consumedHalfHealthRescue,
			consumedQuarterHealthRescue);
	}

	private void PresentCommittedAdventurePlayerVitals(
		int sourcePlayerIndex,
		int rawDamage,
		int previousHealth,
		AdventureLowHealthRescueResult rescueResult,
		bool consumedHalfHealthRescue,
		bool consumedQuarterHealthRescue)
	{
		if (rawDamage > 0)
		{
			PlayAvatarDamagePopup(MainPlayerIndex, Math.Min(previousHealth, rawDamage));
			PlayPlayerAvatarAction(sourcePlayerIndex, CharacterAvatar.AvatarAction.Score);
			if (sourcePlayerIndex != MainPlayerIndex)
			{
				PlayPlayerAvatarAction(MainPlayerIndex, CharacterAvatar.AvatarAction.Hit);
			}
		}
		TryUpdateMatchFlowState(state => state with
		{
			HalfHealthRescueUsed = state.HalfHealthRescueUsed || consumedHalfHealthRescue,
			QuarterHealthRescueUsed =
				state.QuarterHealthRescueUsed || consumedQuarterHealthRescue,
		});
		if (rescueResult.DrawCount > 0)
		{
			DrawCardsFromDeckToHandForAdventureEffect(MainPlayerIndex, rescueResult.DrawCount);
		}
		UpdatePlayerScoreMeter(MainPlayerIndex, true);
		RefreshGameplayTensionMusic();
	}

	private bool TrySynchronizeCommittedAdventurePlayerHealth(int currentHealth)
	{
		if (!TrySubmitScoreMutationsRuleCommand(
			ImmutableArray.Create(new RuleScoreMutation(
				MainPlayerIndex,
				RuleScoreMutationKind.Set,
				currentHealth)),
			out _))
		{
			return false;
		}

		UpdatePlayerScoreMeter(MainPlayerIndex, animate: true);
		RefreshGameplayTensionMusic();
		return true;
	}

	private readonly record struct AdventureDirectDamageSceneCommit(
		bool Succeeded,
		AdventureDirectDamageCommitResult CommitResult);

	private async Task<AdventureDirectDamageSceneCommit> CommitAdventureDirectDamageAsync(
		int damageAmount)
	{
		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (!ShouldUseAdventureUpgradeSystem() || store?.HasActiveRun != true)
		{
			return new AdventureDirectDamageSceneCommit(false, default);
		}

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureDirectDamageCommitResult> operation =
			store.PrepareApplyDirectDamage(new AdventureDirectDamageCommitInput(
					damageAmount,
					CurrentMatchFlowState.HalfHealthRescueUsed,
					CurrentMatchFlowState.QuarterHealthRescueUsed));
		AdventureSaveGateResult<AdventureDirectDamageCommitResult> gate =
			await ExecutePreparedAdventureSaveGateAsync(
				operation,
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to commit adventure direct damage: {gate.Result.WriteResult}");
			return new AdventureDirectDamageSceneCommit(false, default);
		}

		return new AdventureDirectDamageSceneCommit(true, gate.Result);
	}

	private void ApplyCommittedAdventureDirectDamage(
		int sourcePlayerIndex,
		AdventureDirectDamageCommitResult commitResult)
	{
		ApplyCommittedAdventurePlayerVitals(
			sourcePlayerIndex,
			commitResult.PreviousHealth - commitResult.DamagedHealth,
			commitResult.HealingResult.CurrentHealth,
			commitResult.RescueResult,
			commitResult.RescueResult.ConsumedHalfThreshold,
			commitResult.RescueResult.ConsumedQuarterThreshold);
	}

	private void UpdatePlayerScoreMeter(int playerIndex, bool animate)
	{
		if (_networkAuthorityRuleTransactionActive)
		{
			return;
		}

		if (playerIndex < 0 || playerIndex >= _playerScoreMeters.Length)
		{
			return;
		}

		ScoreAvatarMeter? meter = _playerScoreMeters[playerIndex];
		if (meter == null || !GodotObject.IsInstanceValid(meter))
		{
			return;
		}

		meter.TooltipText = string.Empty;
		meter.SetScore(_playerScores[playerIndex], GetScoreLimit(playerIndex), animate);
		UpdatePlayerAvatarTooltip(playerIndex);
		if (_activeStatusTooltipPlayerIndex == playerIndex)
		{
			MarkActivePlayerStatusTooltipContentDirty();
		}
	}

	private int GetScoreWinnerPlayerIndex()
	{
		if (adventure_mode)
		{
			return GetAdventureWinnerPlayerIndex();
		}

		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (_playerScores[playerIndex] >= winning_score)
			{
				return playerIndex;
			}
		}

		return -1;
	}

	private int GetStartingPlayerScore(int playerIndex)
	{
#if DECKLAND_TEST_HOOKS
		if (playerIndex == MainPlayerIndex && debug_main_player_starting_score >= 0)
		{
			return debug_main_player_starting_score;
		}
#endif

		if (!adventure_mode || playerIndex >= player_count)
		{
			return 0;
		}

		if (playerIndex == MainPlayerIndex
			&& TryGetAdventureMainPlayerRunHealth(out int currentHealth, out _))
		{
			return currentHealth;
		}

		return GetAdventureStartingHealth(playerIndex);
	}

	private int GetScoreLimit(int playerIndex)
	{
		if (adventure_mode
			&& playerIndex == MainPlayerIndex
			&& TryGetAdventureMainPlayerRunHealth(out _, out int maxHealth))
		{
			return maxHealth;
		}

		return adventure_mode ? GetAdventureStartingHealth(playerIndex) : winning_score;
	}

	private int GetAdventureStartingHealth(int playerIndex)
	{
		if (playerIndex != AdventureEnemyPlayerIndex)
		{
			if (playerIndex == MainPlayerIndex
				&& TryGetAdventureMainPlayerRunHealth(out _, out int maxHealth))
			{
				return maxHealth;
			}

			return AdventurePlayerStartingHealth;
		}

		CharacterAvatarDefinition enemyDefinition = CharacterAvatarCatalog.GetById(adventure_enemy_avatar_id);
		return enemyDefinition.Id switch
		{
			"boss_k" => AdventureEnemyBossStartingHealth,
			"dragon_prince_j" or "dragon_queen_q" => AdventureEnemyRoyalStartingHealth,
			_ => enemyDefinition.AbilityIds.Count > 0
				? AdventureEnemyAbilityStartingHealth
				: AdventureEnemyNoAbilityStartingHealth,
		};
	}

	private int GetAdventureDamageTargetPlayerIndex(int sourcePlayerIndex)
	{
		if (sourcePlayerIndex < 0 || sourcePlayerIndex >= player_count || player_count <= 1)
		{
			return sourcePlayerIndex;
		}

		return MatchReadModel.GetNextPlayerIndex(
			ClockwiseTurnOrder,
			sourcePlayerIndex,
			player_count,
			MainPlayerIndex);
	}

	private int GetAdventureWinnerPlayerIndex()
	{
		for (int playerIndex = 0; playerIndex < player_count; playerIndex++)
		{
			if (_playerScores[playerIndex] > 0)
			{
				continue;
			}

			int winnerPlayerIndex = GetAdventureDamageTargetPlayerIndex(playerIndex);
			return winnerPlayerIndex >= 0 && winnerPlayerIndex < player_count ? winnerPlayerIndex : -1;
		}

		return -1;
	}

	private bool TryGetAdventureMainPlayerRunHealth(out int currentHealth, out int maxHealth)
	{
		currentHealth = 0;
		maxHealth = 0;
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureRunReadModel? runState = GetAdventureUpgradeRunState();
		if (runState?.IsActive != true)
		{
			return false;
		}

		currentHealth = runState.CurrentHealth;
		maxHealth = runState.MaxHealth;
		return true;
	}

	private async Task<bool> ApplyAdventureMainPlayerHealingAsync(
		int healAmount,
		int maxHealthDelta = 0,
		bool animate = true)
	{
		if (!ShouldUseAdventureUpgradeSystem())
		{
			return false;
		}

		AdventureRunSessionStore? store = GetAdventureRunSessionStore();
		if (store?.HasActiveRun != true)
		{
			return false;
		}

		AdventureRunSessionStore.PreparedAdventureOperation<AdventureHealingCommitResult> operation =
			store.PrepareApplyHealing(healAmount, maxHealthDelta);
		AdventureSaveGateResult<AdventureHealingCommitResult> gate =
			await ExecutePreparedAdventureSaveGateAsync(
				operation,
				result => result.WriteResult == AdventureRunWriteResult.Succeeded,
				result => result.WriteResult == AdventureRunWriteResult.SaveFailed);
		if (!gate.Succeeded)
		{
			GD.PushWarning($"Failed to commit adventure healing: {gate.Result.WriteResult}");
			return false;
		}
		AdventureHealingCommitResult commitResult = gate.Result;

			if (!TrySubmitScoreMutationsRuleCommand(
				ImmutableArray.Create(new RuleScoreMutation(
					MainPlayerIndex,
					RuleScoreMutationKind.Set,
					commitResult.HealingResult.CurrentHealth)),
				out _))
			{
				return false;
			}
		UpdatePlayerScoreMeter(MainPlayerIndex, animate);
		RefreshGameplayTensionMusic();
		return true;
	}

	private static int GetCardScoreValue(PlayingCard card)
	{
		return CardRankCatalog.GetScoreValue(card.Rank, !string.IsNullOrWhiteSpace(card.Suit));
	}

	private int GetCapturedCardScoreValueForPlayer(PlayingCard card, int scorePlayerIndex, bool hasDoubleEffect)
	{
		return ApplyCapturedCardScoreModifiers(GetCardScoreValue(card), scorePlayerIndex, hasDoubleEffect);
	}

	private int GetHalfCapturedCardScoreValueForPlayer(PlayingCard card, int scorePlayerIndex, bool hasDoubleEffect)
	{
		int points = Mathf.CeilToInt(GetCardScoreValue(card) / 2.0f);
		return ApplyCapturedCardScoreModifiers(points, scorePlayerIndex, hasDoubleEffect);
	}

	private int ApplyCapturedCardScoreModifiers(int points, int scorePlayerIndex, bool hasDoubleEffect)
	{
		return MatchReadModel.ApplyCapturedCardScoreModifiers(
			points,
			BuildCaptureScoringContext(scorePlayerIndex),
			hasDoubleEffect);
	}

	private bool ShouldApplyCrabScoreReduction(int scorePlayerIndex)
	{
		return adventure_mode
			&& scorePlayerIndex == MainPlayerIndex
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.TideGuard);
	}

	private bool ShouldPlayCrabScoreReductionAbility(
		int scorePlayerIndex,
		int captureStartIndex,
		HashSet<int>? extraTableCardIndexes = null)
	{
		if (!ShouldApplyCrabScoreReduction(scorePlayerIndex)
			|| captureStartIndex < 0
			|| captureStartIndex >= _tableCards.Count)
		{
			return false;
		}

		bool hasDoubleEffect = HasCapturedDoubleScoreEffect(captureStartIndex, extraTableCardIndexes);
		for (int tableCardIndex = 0; tableCardIndex < _tableCards.Count; tableCardIndex++)
		{
			bool isBaseScoringCard = tableCardIndex >= captureStartIndex
				&& IsCapturedTableCardScoringIndex(tableCardIndex, captureStartIndex);
			bool isExtraScoringCard = extraTableCardIndexes != null && extraTableCardIndexes.Contains(tableCardIndex);
			if (!isBaseScoringCard && !isExtraScoringCard)
			{
				continue;
			}

			TableCard tableCard = _tableCards[tableCardIndex];
			if (tableCard.IsBuriedInSand || !tableCard.Card.HasValue)
			{
				continue;
			}

			int rawPoints = GetCardScoreValue(tableCard.Card.Value);
			if (hasDoubleEffect)
			{
				rawPoints *= 2;
			}

			if (rawPoints > 0)
			{
				return true;
			}
		}

		return false;
	}
}
