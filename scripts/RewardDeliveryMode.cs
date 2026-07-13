using System;

public enum RewardDeliveryMode
{
	None = 0,
	Hand = 1,
	ImmediateAttachment = 2,
}

public static class RewardDeliveryModeRules
{
	public static bool TryParse(string? rawValue, out RewardDeliveryMode mode)
	{
		mode = rawValue?.Trim().ToLowerInvariant() switch
		{
			"none" or "" => RewardDeliveryMode.None,
			"hand" => RewardDeliveryMode.Hand,
			"immediate_attachment" or "immediateattachment" => RewardDeliveryMode.ImmediateAttachment,
			_ => (RewardDeliveryMode)(-1),
		};

		return mode is RewardDeliveryMode.None
			or RewardDeliveryMode.Hand
			or RewardDeliveryMode.ImmediateAttachment;
	}

	public static RewardDeliveryMode ParseOrNone(string? rawValue)
	{
		return TryParse(rawValue, out RewardDeliveryMode mode)
			? mode
			: RewardDeliveryMode.None;
	}

	public static string ToManifestValue(RewardDeliveryMode mode)
	{
		return mode switch
		{
			RewardDeliveryMode.Hand => "hand",
			RewardDeliveryMode.ImmediateAttachment => "immediate_attachment",
			_ => "none",
		};
	}
}
