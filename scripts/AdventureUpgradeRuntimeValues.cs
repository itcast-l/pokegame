using System;
using System.Collections.Generic;

internal static class AdventureUpgradeRuntimeValues
{
	private static AdventureUpgradeManifest? _activeManifest;

	internal static void SetManifest(AdventureUpgradeManifest? manifest)
	{
		_activeManifest = manifest?.IsReady == true ? manifest : null;
	}

	internal static int GetInt(string upgradeId, string key, int fallback = 0)
	{
		string normalizedId = upgradeId?.Trim() ?? string.Empty;
		string normalizedKey = key?.Trim() ?? string.Empty;
		if (_activeManifest?.TryGetEnabledEntry(normalizedId, out AdventureUpgradeManifestEntry entry) != true
			|| entry.EffectValues == null)
		{
			// A missing manifest is a disabled gameplay contract. Callers must not
			// silently revive the old code constant in that state.
			return 0;
		}

		return entry.EffectValues.TryGetValue(normalizedKey, out int configuredValue)
			? configuredValue
			: 0;
	}

	internal static int GetPositiveInt(string upgradeId, string key, int fallback = 1)
	{
		return _activeManifest == null ? 0 : Math.Max(1, GetInt(upgradeId, key, 0));
	}

	internal static int GetNonNegativeInt(string upgradeId, string key, int fallback = 0)
	{
		return _activeManifest == null ? 0 : Math.Max(0, GetInt(upgradeId, key, 0));
	}

	internal static IReadOnlyList<string> GetDescriptionValueKeys(string upgradeId)
	{
		return _activeManifest?.TryGetEnabledEntry(upgradeId, out AdventureUpgradeManifestEntry entry) == true
			? entry.DescriptionValueKeys ?? Array.Empty<string>()
			: Array.Empty<string>();
	}
}
