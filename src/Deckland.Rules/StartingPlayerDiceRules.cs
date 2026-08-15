namespace Deckland.Rules;

public static class StartingPlayerDiceRules
{
	private static readonly int[] ClockwiseTurnOrder = { 0, 2, 1, 3 };

	public const int MinDiceResult = 1;
	public const int MaxDiceResult = 6;

	public static bool TryResolveStartingTurnSeat(
		int playerCount,
		int referenceSeat,
		int diceResult,
		out int startingTurnSeat)
	{
		startingTurnSeat = 0;
		if (playerCount is not (3 or 4)
			|| referenceSeat < 0
			|| referenceSeat >= playerCount
			|| diceResult is < MinDiceResult or > MaxDiceResult)
		{
			return false;
		}

		Span<int> activeTurnOrder = stackalloc int[playerCount];
		int activeSeatCount = 0;
		int referenceOrderIndex = -1;
		foreach (int seat in ClockwiseTurnOrder)
		{
			if (seat >= playerCount)
			{
				continue;
			}

			activeTurnOrder[activeSeatCount] = seat;
			if (seat == referenceSeat)
			{
				referenceOrderIndex = activeSeatCount;
			}
			activeSeatCount++;
		}

		if (referenceOrderIndex < 0 || activeSeatCount != playerCount)
		{
			return false;
		}

		startingTurnSeat = activeTurnOrder[
			(referenceOrderIndex + diceResult - MinDiceResult) % activeSeatCount];
		return true;
	}
}
