using System;

public enum MatchOutcomeKind
{
	InProgress,
	Winner,
	Draw,
}

internal readonly record struct MatchOutcome(MatchOutcomeKind Kind, int WinnerPlayerIndex)
{
	internal static MatchOutcome InProgress => new(MatchOutcomeKind.InProgress, -1);
	internal static MatchOutcome Draw => new(MatchOutcomeKind.Draw, -1);
	internal static MatchOutcome Winner(int playerIndex) => new(MatchOutcomeKind.Winner, playerIndex);
}

internal static class MatchOutcomeRules
{
	internal static MatchOutcome Evaluate(
		ReadOnlySpan<GameTurnPlayerCardState> playerStates,
		ReadOnlySpan<int> scores,
		int playerCount,
		int scoreOrLifeWinnerPlayerIndex = -1)
	{
		int checkedPlayerCount = Math.Min(playerCount, Math.Min(playerStates.Length, scores.Length));
		if (checkedPlayerCount <= 0)
		{
			return MatchOutcome.InProgress;
		}

		if (scoreOrLifeWinnerPlayerIndex >= 0
			&& scoreOrLifeWinnerPlayerIndex < checkedPlayerCount)
		{
			return MatchOutcome.Winner(scoreOrLifeWinnerPlayerIndex);
		}

		int resourceOwner = -1;
		int resourceOwnerCount = 0;
		bool anyoneCanAct = false;
		for (int playerIndex = 0; playerIndex < checkedPlayerCount; playerIndex++)
		{
			GameTurnPlayerCardState state = playerStates[playerIndex];
			if (state.HasResources)
			{
				resourceOwner = playerIndex;
				resourceOwnerCount++;
			}

			anyoneCanAct |= state.CanActNow;
		}

		if (resourceOwnerCount == 1)
		{
			return MatchOutcome.Winner(resourceOwner);
		}

		if (resourceOwnerCount == 0 || !anyoneCanAct)
		{
			return GetHighestScoreOutcome(scores[..checkedPlayerCount]);
		}

		return MatchOutcome.InProgress;
	}

	public static bool IsSerializedOutcomeValid(
		MatchOutcomeKind kind,
		int winnerPlayerIndex,
		int playerCount)
	{
		return Enum.IsDefined(kind)
			&& (kind == MatchOutcomeKind.Winner
				? winnerPlayerIndex >= 0 && winnerPlayerIndex < playerCount
				: winnerPlayerIndex == -1);
	}

	internal static MatchOutcome ResolveStalemate(ReadOnlySpan<int> scores, int playerCount)
	{
		int checkedPlayerCount = Math.Min(playerCount, scores.Length);
		return checkedPlayerCount <= 0
			? MatchOutcome.Draw
			: GetHighestScoreOutcome(scores[..checkedPlayerCount]);
	}

	private static MatchOutcome GetHighestScoreOutcome(ReadOnlySpan<int> scores)
	{
		int highestScore = int.MinValue;
		int highestScorePlayerIndex = -1;
		bool tied = false;
		for (int playerIndex = 0; playerIndex < scores.Length; playerIndex++)
		{
			int score = scores[playerIndex];
			if (score > highestScore)
			{
				highestScore = score;
				highestScorePlayerIndex = playerIndex;
				tied = false;
			}
			else if (score == highestScore)
			{
				tied = true;
			}
		}

		return tied ? MatchOutcome.Draw : MatchOutcome.Winner(highestScorePlayerIndex);
	}
}
