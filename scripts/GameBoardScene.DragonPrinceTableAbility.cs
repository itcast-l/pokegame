using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

public partial class GameBoardScene
{
	private async Task ResolveAdventureEnemyTurnEndAbilityAsync(int playerIndex, PlayCardResult result, int turnSequenceGeneration)
	{
		await ResolveLeafySeadragonBladeBreakAsync(playerIndex, result, turnSequenceGeneration);
	}

	private async Task<PlayCardResult>
	ResolveDragonPrinceJackTurnStartAsync(
		int playerIndex,
		int turnSequenceGeneration)
{
	EnsureAdventureModeResourcesLoaded();

	if (!ShouldResolveDragonPrinceJack(playerIndex))
	{
		return new PlayCardResult(false, false);
	}

	_dragonPrinceJackUsed = true;
	await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(
		playerIndex,
		turnSequenceGeneration,
		AdventureEnemyAbilities.DragonPrince);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return new PlayCardResult(false, false);
	}

	int specialJackTableIndex =
		await PlayDragonPrinceSpecialJackToTableAsync(
			playerIndex,
			turnSequenceGeneration);
	if (specialJackTableIndex < 0
		|| turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return new PlayCardResult(false, false);
	}

	PlayCardResult result =
		await ResolveDragonPrinceJackCollectTableAsync(
			playerIndex,
			turnSequenceGeneration,
			specialJackTableIndex);
	PlayCardResult completedResult = result with
	{
		CompletesTurn = true,
	};
	if (!result.Played
		|| turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return completedResult;
	}

	RenderPlayerCardPiles();
	if (!QuestionRewardPresentationRules
		.ShouldDelayTableRenderForQuestionReward(
			result.QuestionRewardAnimation.HasValue,
			IsLocalHandPlayer(playerIndex)))
	{
		RenderTableCards();
	}
	SendQuestionRewardVisualEventIfNeeded(
		playerIndex,
		result.QuestionRewardAnimation,
		turnSequenceGeneration);
	await PlayQuestionCardRewardAnimationAsync(
		playerIndex,
		result.QuestionRewardAnimation,
		turnSequenceGeneration);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return completedResult;
	}

	await ApplyAdventureQuestionRewardEffects(
		playerIndex,
		result.AdventureQuestionRewardEffects,
		turnSequenceGeneration);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return completedResult;
	}

	await ApplyAdventurePostCaptureEffectsAsync(
		playerIndex,
		result.AdventurePostCaptureEffects,
		turnSequenceGeneration);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return completedResult;
	}

	await MaybeResolveAppliedAdventureQuestionRewardRejudgeAsync(
		playerIndex,
		result.QuestionRewardAnimation,
		turnSequenceGeneration);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return completedResult;
	}

	await ResolvePendingMagnetAttachmentsAsync(
		turnSequenceGeneration);
	if (turnSequenceGeneration != _turnSequenceGeneration
		|| _isLeavingGame
		|| IsGameOver())
	{
		return completedResult;
	}

	MarkBossKActiveControlUsedIfNeeded(playerIndex);
	RenderPlayerCardPiles();
	RenderPlayerHands();
	RenderTableCards();
	return completedResult;
}

	private async Task<int> PlayDragonPrinceSpecialJackToTableAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (_dragonPrinceSpecialJackTexture == null || string.IsNullOrWhiteSpace(_dragonPrinceSpecialJackRank))
		{
			return -1;
		}

		Vector2 skillGlobalPosition = GetDragonPrinceSpecialJackSkillGlobalPosition();
		await PlayDragonPrinceSpecialJackSkillSpawnAsync(skillGlobalPosition);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return -1;
		}

		PlayingCard specialJack = new(string.Empty, _dragonPrinceSpecialJackRank, _dragonPrinceSpecialJackTexture);
		_tableCards.Add(new TableCard(specialJack, true, cardOwnerPlayerIndex: playerIndex, removeCardOnCapture: true));
		int tableCardIndex = _tableCards.Count - 1;
		await PlayCardToTableAnimationAsync(
			playerIndex,
			specialJack,
			skillGlobalPosition,
			tableCardIndex,
			true,
			_dragonPrinceSpecialJackTexture);
		RenderTableCards();
		return tableCardIndex;
	}

	private Vector2 GetDragonPrinceSpecialJackSkillGlobalPosition()
	{
		return GetQuestionCardTransformAreaCardGlobalPosition(
			_centerArena.GlobalPosition + (_centerArena.Size - CardVisualSize) / 2.0f);
	}

	private async Task PlayDragonPrinceSpecialJackSkillSpawnAsync(Vector2 skillGlobalPosition)
	{
		if (!IsInsideTree() || _dragonPrinceSpecialJackTexture == null)
		{
			return;
		}

		TextureRect spawnCard = _animationPresenter.RentTextureRect(
			this,
			"DragonPrinceSpecialJackSkillSpawn",
			_dragonPrinceSpecialJackTexture,
			CardVisualSize,
			PlayedCardFlightZIndex + 860,
			CanvasItem.TextureFilterEnum.Nearest,
			zAsRelative: false);
		spawnCard.Scale = Vector2.One * 0.78f;
		spawnCard.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f);
		spawnCard.GlobalPosition = skillGlobalPosition;

		try
		{
			Tween tween = CreateTween();
			tween.SetParallel(true);
			tween.SetTrans(Tween.TransitionType.Cubic);
			tween.SetEase(Tween.EaseType.Out);
			tween.TweenProperty(spawnCard, "modulate:a", 1.0f, DragonPrinceSpecialJackSpawnSeconds * 0.55f);
			tween.TweenProperty(spawnCard, "scale", Vector2.One * 1.10f, DragonPrinceSpecialJackSpawnSeconds);
			tween.Chain().SetParallel(true);
			tween.TweenProperty(spawnCard, "scale", Vector2.One, DragonPrinceSpecialJackSpawnSettleSeconds);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(spawnCard))
			{
				_animationPresenter.Release(spawnCard);
			}
		}
	}

	private async Task<PlayCardResult>
	ResolveDragonPrinceJackCollectTableAsync(
		int playerIndex,
		int turnSequenceGeneration,
		int specialJackTableIndex)
{
	if (playerIndex < 0
		|| playerIndex >= _playerDecks.Count)
	{
		return new PlayCardResult(false, false);
	}

	const int captureStartIndex = 0;
	var captureTableReferenceSnapshot =
		new List<TableCard>(_tableCards);
	if (captureTableReferenceSnapshot.Count == 0
		|| specialJackTableIndex < 0
		|| specialJackTableIndex
			>= captureTableReferenceSnapshot.Count)
	{
		return new PlayCardResult(false, false);
	}

	bool OwnsDragonCapture()
	{
		return !_isLeavingGame
			&& !IsGameOver()
			&& IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot);
	}

	int scorePlayerIndex =
		GetCapturedTableScorePlayerIndex(
			captureStartIndex,
			playerIndex);
	bool capturedMiddleCards =
		HasCapturedMiddleTableCards(captureStartIndex);
	int collectedPoints = capturedMiddleCards
		? GetCapturedTablePoints(
			captureStartIndex,
			scorePlayerIndex)
		: 0;
	PairCaptureEdgeFloatHold? specialJackFloat = null;
	bool specialJackFloatHandedToDeck = false;
	try
	{
		specialJackFloat =
			await PlayDragonPrinceSpecialJackTableFloatHoldAsync(
				specialJackTableIndex);
		if (!OwnsDragonCapture())
		{
			return new PlayCardResult(false, false);
		}

		if (capturedMiddleCards
			&& ShouldPlayCrabScoreReductionAbility(
				scorePlayerIndex,
				captureStartIndex,
				null))
		{
			await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(
				1,
				turnSequenceGeneration,
				AdventureEnemyAbilities.TideGuard);
			if (!OwnsDragonCapture())
			{
				return new PlayCardResult(false, false);
			}
		}

		if (collectedPoints > 0)
		{
			List<CapturedTableCardVisual>
				scoringCardVisuals =
					BuildCapturedTableCardVisuals(
						captureStartIndex,
						false,
						true,
						scorePlayerIndex:
							scorePlayerIndex);
			await PlayScoreFillCollectionAsync(
				scorePlayerIndex,
				scoringCardVisuals,
				turnSequenceGeneration,
				() => { });
			if (!OwnsDragonCapture())
			{
				return new PlayCardResult(false, false);
			}
		}

		List<CapturedTableCardVisual> capturedCardVisuals =
			BuildCapturedTableCardVisuals(
				captureStartIndex,
				false,
				flightStartOverrides:
					specialJackFloat);
		if (capturedCardVisuals.Count > 0)
		{
			specialJackFloat?.QueueFreeOverlays(
				_animationPresenter);
			specialJackFloatHandedToDeck =
				specialJackFloat != null;
			await PlayCapturedCardsToDeckAnimationAsync(
				playerIndex,
				captureStartIndex,
				capturedCardVisuals,
				turnSequenceGeneration);
			if (!OwnsDragonCapture())
			{
				return new PlayCardResult(false, false);
			}
		}

		CaptureTableCardsResult captureResult =
			await CaptureTableCardsAsync(
				playerIndex,
				captureStartIndex,
				awardScore: false,
				turnSequenceGeneration:
					turnSequenceGeneration,
				expectedTableCardReferences:
					captureTableReferenceSnapshot,
				commitImmediatelyBeforeApply: () =>
				{
					if (collectedPoints > 0)
					{
						AddPlayerScore(
							scorePlayerIndex,
							collectedPoints);
					}
				});
		if (!captureResult.Applied
			|| captureResult.Aborted)
		{
			return new PlayCardResult(false, false);
		}

		if (!IsGameOver())
		{
			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
		}

		return new PlayCardResult(
			true,
			true,
			captureResult.CollectedCardCount > 0,
			captureResult.QuestionRewardAnimation,
			AdventureQuestionRewardEffects:
				captureResult
					.AdventureQuestionRewardEffects,
			AdventurePostCaptureEffects:
				captureResult
					.AdventurePostCaptureEffects);
	}
	finally
	{
		if (!specialJackFloatHandedToDeck
			&& specialJackFloat != null)
		{
			if (IsCaptureTableReferenceSnapshotCurrent(
				turnSequenceGeneration,
				captureTableReferenceSnapshot))
			{
				RestorePairCaptureEdgeFloatHold(
					specialJackFloat);
			}
			else
			{
				specialJackFloat.QueueFreeOverlays(
					_animationPresenter);
			}
		}
	}
}

	private async Task<PairCaptureEdgeFloatHold?> PlayDragonPrinceSpecialJackTableFloatHoldAsync(int tableCardIndex)
	{
		if (!IsInsideTree()
			|| tableCardIndex < 0
			|| tableCardIndex >= _tableCards.Count
			|| _dragonPrinceSpecialJackTexture == null)
		{
			return null;
		}

		if (!TryGetTableCardPosition(tableCardIndex, out Vector2 cardPosition))
		{
			return null;
		}

		TextureRect overlay = _animationPresenter.RentTextureRect(
			this,
			"DragonPrinceSpecialJackTableFloat",
			_dragonPrinceSpecialJackTexture,
			CardVisualSize,
			PlayedCardFlightZIndex + tableCardIndex * 2 + 260,
			CanvasItem.TextureFilterEnum.Nearest,
			zAsRelative: false);
		overlay.GlobalPosition = _centerArena.GlobalPosition + cardPosition;
		_tableCards[tableCardIndex].IsHiddenDuringCapture = true;
		RenderTableCards();

		Tween tween = CreateTween();
		tween.SetParallel(true);
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);
		tween.TweenProperty(
			overlay,
			"global_position",
			overlay.GlobalPosition + new Vector2(0.0f, -DragonPrinceSpecialJackFloatRise),
			DragonPrinceSpecialJackFloatSeconds);
		tween.TweenProperty(
			overlay,
			"scale",
			Vector2.One * DragonPrinceSpecialJackFloatScale,
			DragonPrinceSpecialJackFloatSeconds);
		await ToSignal(tween, Tween.SignalName.Finished);
		return new PairCaptureEdgeFloatHold(tableCardIndex, overlay);
	}

	private bool ShouldResolveDragonPrinceJack(int playerIndex)
	{
		return adventure_mode
			&& AdventureEnemyHasAbility(AdventureEnemyAbilities.DragonPrince)
			&& playerIndex == 1
			&& !_dragonPrinceJackUsed
			&& _tableCards.Count >= GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5)
			&& (!IsBossKEnemy(playerIndex) || ShouldResolveBossKDragonPrinceJack(playerIndex));
	}

	private bool ShouldResolveBossKDragonPrinceJack(int playerIndex)
	{
		return BossKCanUseActiveControl(playerIndex)
			&& GetBestPotentialNormalCaptureCount(playerIndex, _tableCards) < GetAdventureEnemyAbilityValue(AdventureEnemyAbilityValueKeys.RoyalCaptureThreshold, 5);
	}

}
