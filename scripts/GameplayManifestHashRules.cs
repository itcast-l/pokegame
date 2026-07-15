using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

public static class GameplayManifestHashRules
{
	public static string ComputeItemCardManifestHash(IEnumerable<ItemCardDefinition> definitions)
	{
		ArgumentNullException.ThrowIfNull(definitions);

		var lines = new List<string>();
		foreach (ItemCardDefinition definition in definitions.OrderBy(value => value.Id, StringComparer.Ordinal))
		{
			var fields = new[]
			{
				definition.Id,
				$"enabled_in_game={ToCanonicalBool(definition.EnabledInGame)}",
				$"playable_from_hand={ToCanonicalBool(definition.PlayableFromHand)}",
				$"can_be_question_reward={ToCanonicalBool(definition.CanBeQuestionReward)}",
				$"weight={definition.Weight}",
				$"rarity={definition.Rarity}",
				$"tags={definition.TagsCsv}",
				$"reward_delivery={RewardDeliveryModeRules.ToManifestValue(definition.RewardDelivery)}",
				$"play_presentation={definition.PlayPresentation}",
				$"trigger={definition.Trigger}",
				$"effect_type={definition.EffectType}",
				$"effect_values={definition.EffectValuesRaw}",
				$"priority={definition.Priority}",
				$"attached_trigger={definition.AttachedTrigger}",
				$"attached_effect_type={definition.AttachedEffectType}",
				$"attached_effect_values={definition.AttachedEffectValuesRaw}",
				$"attached_priority={definition.AttachedPriority}",
				$"description_key={definition.DescriptionKey}",
			};
			lines.Add(string.Join('\t', fields));
		}

		string canonical = string.Join('\n', lines) + "\n";
		return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
	}

	private static string ToCanonicalBool(bool value) => value ? "true" : "false";
}
