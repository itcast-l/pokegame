using System;

internal static class TurnGenerationRules
{
	public static bool TryGetNextAfterRestore(
		int localGeneration,
		int checkpointGeneration,
		out int restoredGeneration)
	{
		restoredGeneration = 0;
		if (localGeneration < 0
			|| checkpointGeneration < 0
			|| localGeneration == int.MaxValue
			|| checkpointGeneration == int.MaxValue)
		{
			return false;
		}

		restoredGeneration = Math.Max(localGeneration, checkpointGeneration) + 1;
		return true;
	}
}
