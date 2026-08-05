using System.Collections.Generic;

internal static class CaptureRevealOrder
{
	public static int[] BuildBaseRevealIndexes(int captureStartIndex, int tableCardCount)
	{
		if (captureStartIndex < 0 || captureStartIndex >= tableCardCount)
		{
			return System.Array.Empty<int>();
		}

		var revealIndexes = new List<int>(tableCardCount - captureStartIndex);
		int left = captureStartIndex;
		int right = tableCardCount - 1;
		while (left <= right)
		{
			revealIndexes.Add(left);
			if (right != left)
			{
				revealIndexes.Add(right);
			}

			left++;
			right--;
		}

		return revealIndexes.ToArray();
	}
}
