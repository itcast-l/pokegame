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

	private async Task<PlayCardResult> ResolveDragonPrinceJackTurnStartAsync(int playerIndex, int turnSequenceGeneration)
	{
		if (!ShouldResolveDragonPrinceJack(playerIndex))
		{
			return new PlayCardResult(false, false);
		}

		_dragonPrinceJackUsed = true;
		await PlayEnemySpecialAbilityAvatarFxForAbilityAsync(playerIndex, turnSequenceGeneration, AdventureEnemyAbilities.DragonPrince);
		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new PlayCardResult(false, false);
		}

		int specialJackTableIndex = await PlayDragonPrinceSpecialJackToTableAsync(playerIndex, turnSequenceGeneration);
		if (specialJackTableIndex < 0)
		{
			return new PlayCardResult(false, false);
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new PlayCardResult(false, false);
		}

		PlayCardResult result = await ResolveDragonPrinceJackCollectTableAsync(playerIndex, turnSequenceGeneration, specialJackTableIndex);
		MarkBossKActiveControlUsedIfNeeded(playerIndex);
		RenderPlayerCardPiles();
		RenderPlayerHands();
		RenderTableCards();
		return result with { CompletesTurn = true };
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

		var spawnCard = new TextureRect
		{
			Name = "DragonPrinceSpecialJackSkillSpawn",
			Texture = _dragonPrinceSpecialJackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + 860,
			ZAsRelative = false,
			Scale = Vector2.One * 0.78f,
			Modulate = new Color(1.0f, 1.0f, 1.0f, 0.0f),
		};
		AddChild(spawnCard);
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
				spawnCard.QueueFree();
			}
		}
	}

	private async Task<PlayCardResult> ResolveDragonPrinceJackCollectTableAsync(
		int playerIndex,
		int turnSequenceGeneration,
		int specialJackTableIndex)
	{
		if (playerIndex < 0 || playerIndex >= _playerDecks.Count)
		{
			return new PlayCardResult(false, false);
		}

		bool captured = _tableCards.Count > 0;
		PairCaptureEdgeFloatHold? specialJackFloat = null;
		bool specialJackFloatHandedToDeck = false;
		if (captured)
		{
			try
			{
				specialJackFloat = await PlayDragonPrinceSpecialJackTableFloatHoldAsync(specialJackTableIndex);
				if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
				{
					return new PlayCardResult(false, false);
				}

				List<CapturedTableCardVisual> capturedCardVisuals = BuildCapturedTableCardVisuals(
					0,
					false,
					flightStartOverrides: specialJackFloat);
				if (capturedCardVisuals.Count > 0)
				{
					specialJackFloat?.QueueFreeOverlays();
					specialJackFloatHandedToDeck = specialJackFloat != null;
					await PlayCapturedCardsToDeckAnimationAsync(
						playerIndex,
						0,
						capturedCardVisuals,
						turnSequenceGeneration);
				}
			}
			finally
			{
				if (!specialJackFloatHandedToDeck && specialJackFloat != null)
				{
					RestorePairCaptureEdgeFloatHold(specialJackFloat);
				}
			}
		}

		if (turnSequenceGeneration != _turnSequenceGeneration || IsGameOver())
		{
			return new PlayCardResult(false, false);
		}

		CaptureTableCardsResult captureResult = await CaptureTableCardsAsync(
			playerIndex,
			0,
			turnSequenceGeneration: turnSequenceGeneration);
		if (!IsGameOver())
		{
			Shuffle(_playerDecks[playerIndex]);
			Cut(_playerDecks[playerIndex]);
		}

		return new PlayCardResult(true, captured, captureResult.CollectedCardCount > 0, captureResult.QuestionRewardAnimation);
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

		var overlay = new TextureRect
		{
			Name = "DragonPrinceSpecialJackTableFloat",
			Texture = _dragonPrinceSpecialJackTexture,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
			TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
			MouseFilter = MouseFilterEnum.Ignore,
			Size = CardVisualSize,
			PivotOffset = CardVisualSize / 2.0f,
			ZIndex = PlayedCardFlightZIndex + tableCardIndex * 2 + 260,
			ZAsRelative = false,
		};
		AddChild(overlay);
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
