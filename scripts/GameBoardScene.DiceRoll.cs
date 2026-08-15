using System;
using System.Threading.Tasks;
using Deckland.Rules;
using Godot;

public partial class GameBoardScene
{
	private const int DiceRollOverlayZIndex = 2700;
	private DiceRollOverlay? _diceRollOverlay;
	private int _debugForcedOpeningDiceRollResult;
	private int _openingDiceRollResult;
	private int _openingStartingTurnSeat = -1;
	private bool _openingPresentationSequenceRunning;
	private int _openingPresentationSequenceTurnGeneration = -1;
	private int _openingPresentationCompletedTurnGeneration = -1;
	private bool _networkOpeningDealCompleted = true;

	private void CreateDiceRollOverlay()
	{
		EnsureDiceRollOverlay();
	}

	private void EnsureDiceRollOverlay()
	{
		if (_diceRollOverlay != null && GodotObject.IsInstanceValid(_diceRollOverlay))
		{
			return;
		}

		_diceRollOverlay = new DiceRollOverlay
		{
			Name = "DiceRollOverlay",
			ZIndex = DiceRollOverlayZIndex,
			MouseFilter = MouseFilterEnum.Ignore,
		};
		_diceRollOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(_diceRollOverlay);
	}

	private bool TryPlayDiceRoll(int result)
	{
		if (!DiceRollPresentationRules.IsValidResult(result))
		{
			return false;
		}

		EnsureDiceRollOverlay();
		if (_diceRollOverlay == null || !_diceRollOverlay.HasRequiredTexturesForResult(result))
		{
			return false;
		}

		TrackBoardTask(
			_diceRollOverlay.PlayRollAsync(result),
			nameof(DiceRollOverlay.PlayRollAsync));
		return true;
	}

	private bool TryCreateOpeningDiceOutcome(
		out int diceRollResult,
		out int startingTurnSeat)
	{
		diceRollResult = 0;
		int referenceSeat = IsNetworkMultiplayerMode()
			? GetLocalHandPlayerIndex()
			: MainPlayerIndex;
		startingTurnSeat = referenceSeat;
		if (player_count < 3)
		{
			return true;
		}

		int forcedResult = _debugForcedOpeningDiceRollResult;
		if (forcedResult == 0)
		{
			string forcedEnvironmentValue = OS.GetEnvironment(
				"CODEX_OPENING_DICE_ROLL_RESULT");
			if (int.TryParse(forcedEnvironmentValue, out int environmentResult)
				&& DiceRollPresentationRules.IsValidResult(environmentResult))
			{
				forcedResult = environmentResult;
			}
		}

		diceRollResult = DiceRollPresentationRules.IsValidResult(forcedResult)
			? forcedResult
			: _rng.RandiRange(
				StartingPlayerDiceRules.MinDiceResult,
				StartingPlayerDiceRules.MaxDiceResult);
		if (StartingPlayerDiceRules.TryResolveStartingTurnSeat(
			player_count,
			referenceSeat,
			diceRollResult,
			out startingTurnSeat))
		{
			return true;
		}

		GD.PushError(
			$"Could not resolve opening dice result {diceRollResult} "
			+ $"for {player_count} players from seat {referenceSeat}.");
		return false;
	}

	private void CommitOpeningDiceOutcome(int diceRollResult, int startingTurnSeat)
	{
		_openingDiceRollResult = diceRollResult;
		_openingStartingTurnSeat = startingTurnSeat;
		_openingPresentationCompletedTurnGeneration = -1;
		_networkOpeningDealCompleted = !IsNetworkMultiplayerMode() || IsNetworkHost();
		_debugForcedOpeningDiceRollResult = 0;
	}

	private async Task PlayOpeningDiceRollAsync(int turnSequenceGeneration)
	{
		if (player_count < 3
			|| !DiceRollPresentationRules.IsValidResult(_openingDiceRollResult)
			|| turnSequenceGeneration != _turnSequenceGeneration
			|| IsGameOver()
			|| !IsInsideTree())
		{
			return;
		}

		EnsureDiceRollOverlay();
		if (_diceRollOverlay == null
			|| !_diceRollOverlay.HasRequiredTexturesForResult(_openingDiceRollResult))
		{
			return;
		}

		await _diceRollOverlay.PlayRollAsync(_openingDiceRollResult);
	}

	public bool DebugPlayDiceRoll(int result)
	{
		if (result == 0)
		{
			result = _presentationRng.RandiRange(
				DiceRollPresentationRules.MinResult,
				DiceRollPresentationRules.MaxResult);
		}

		return TryPlayDiceRoll(result);
	}

	public bool DebugIsDiceRollPlaying()
	{
		return _diceRollOverlay?.IsPlaying == true;
	}

	public int DebugGetLastDiceRollResult()
	{
		return _diceRollOverlay?.LastResult ?? 0;
	}

	public bool DebugForceNextOpeningDiceRollResult(int result)
	{
		if (!DiceRollPresentationRules.IsValidResult(result))
		{
			return false;
		}

		_debugForcedOpeningDiceRollResult = result;
		return true;
	}

	public int DebugGetOpeningDiceRollResult()
	{
		return _openingDiceRollResult;
	}

	public int DebugGetOpeningStartingTurnSeat()
	{
		return _openingStartingTurnSeat;
	}

	public int DebugGetTurnSequenceGeneration()
	{
		return _turnSequenceGeneration;
	}

	public bool DebugAreOpeningCardsFullyRevealed()
	{
		if (_openingVisibleHandCardCounts.Count < player_count
			|| _openingVisiblePileCardCounts.Count < player_count)
		{
			return false;
		}

		for (int seat = 0; seat < player_count; seat++)
		{
			if (_openingVisibleHandCardCounts[seat] < _playerHands[seat].Count
				|| _openingVisiblePileCardCounts[seat] < _playerDecks[seat].Count)
			{
				return false;
			}
		}

		return true;
	}

	public bool DebugStartOpeningDiceMatch(int testPlayerCount, int forcedResult)
	{
		if (!IsInsideTree()
			|| testPlayerCount is < MinPlayers or > MaxPlayers
			|| _isOpeningDealAnimationPlaying
			|| _isProcessingCardPlay
			|| _isResolvingTurnSequence
			|| _playerCardPiles.Count != MaxPlayers
			|| _playerHandAreas.Count != MaxPlayers
			|| (testPlayerCount < 3 && forcedResult != 0)
			|| (testPlayerCount > 2
				&& !DebugForceNextOpeningDiceRollResult(forcedResult)))
		{
			return false;
		}

		_playerCount = testPlayerCount;
		_networkWaitForInitialize = false;
		_networkBoardInitialized = true;
		UpdatePlayerVisibility();
		DealCardsToPlayers(playOpeningDealAnimation: true);
		return _isOpeningDealAnimationPlaying;
	}
}
