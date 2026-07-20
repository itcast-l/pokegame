using Godot;

public partial class GameBoardScene
{
	private const int DiceRollOverlayZIndex = 2700;
	private DiceRollOverlay? _diceRollOverlay;

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

	public bool DebugPlayDiceRoll(int result)
	{
		if (result == 0)
		{
			result = _rng.RandiRange(DiceRollPresentationRules.MinResult, DiceRollPresentationRules.MaxResult);
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
}
