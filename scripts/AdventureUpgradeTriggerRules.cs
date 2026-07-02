using System.Collections.Generic;

internal enum AdventureUpgradeTriggerSource
{
	None = 0,
	RunStart = 1,
	BigCapture = 2,
	Victory = 3,
}

internal static class AdventureUpgradeTriggerRules
{
	internal static IReadOnlyList<AdventureUpgradeTriggerSource> BuildTriggerOrder(
		bool runStartTriggered,
		bool bigCaptureTriggered,
		bool victoryTriggered)
	{
		var results = new List<AdventureUpgradeTriggerSource>(3);
		if (runStartTriggered)
		{
			results.Add(AdventureUpgradeTriggerSource.RunStart);
		}

		if (bigCaptureTriggered)
		{
			results.Add(AdventureUpgradeTriggerSource.BigCapture);
		}

		if (victoryTriggered)
		{
			results.Add(AdventureUpgradeTriggerSource.Victory);
		}

		return results;
	}
}
