using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

public readonly record struct ItemCardEffectValueRange(int Minimum, int Maximum)
{
	public bool Contains(string value)
	{
		return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
			&& parsed >= Minimum
			&& parsed <= Maximum;
	}
}

public sealed record ItemCardEffectContract(
	string EffectType,
	IReadOnlySet<string> AllowedTriggers,
	IReadOnlySet<string> RequiredValueKeys,
	IReadOnlySet<string> AllowedValueKeys,
	IReadOnlyDictionary<string, ItemCardEffectValueRange> ValueRanges,
	IReadOnlySet<RewardDeliveryMode> AllowedDeliveryModes,
	bool RequiresResolver = false);

public readonly record struct ItemCardContractValidationResult(
	bool IsValid,
	string ErrorMessage)
{
	public static ItemCardContractValidationResult Valid => new(true, string.Empty);

	public static ItemCardContractValidationResult Invalid(string message)
	{
		return new ItemCardContractValidationResult(false, message);
	}
}

public static class ItemCardEffectContractRegistry
{
	private static readonly IReadOnlyDictionary<string, ItemCardEffectContract> Contracts =
		new ReadOnlyDictionary<string, ItemCardEffectContract>(BuildContracts());

	private static readonly IReadOnlySet<string> KnownPresentations =
		new HashSet<string>(StringComparer.Ordinal)
		{
			CardPlayPresentationTypes.DirectTrigger,
			CardPlayPresentationTypes.TableOverlay,
			CardPlayPresentationTypes.TransformAreaTrigger,
			CardPlayPresentationTypes.LensSplit,
			CardPlayPresentationTypes.TargetedTrigger,
		};

	private static readonly IReadOnlySet<string> GenericPresentationTypes =
		new HashSet<string>(StringComparer.Ordinal)
		{
			CardPlayPresentationTypes.DirectTrigger,
			CardPlayPresentationTypes.TargetedTrigger,
		};

	public static IReadOnlyDictionary<string, ItemCardEffectContract> All => Contracts;

	public static IReadOnlySet<string> ResolverEffectTypes =>
		new HashSet<string>(
			Contracts.Values
				.Where(contract => contract.RequiresResolver)
				.Select(contract => contract.EffectType),
			StringComparer.Ordinal);

	public static IReadOnlySet<string> CustomPresentationTypes =>
		new HashSet<string>(KnownPresentations.Except(GenericPresentationTypes), StringComparer.Ordinal);

	public static IReadOnlySet<string> GenericPresentations => GenericPresentationTypes;

	public static bool IsKnownEffectType(string? effectType)
	{
		return !string.IsNullOrWhiteSpace(effectType)
			&& Contracts.ContainsKey(effectType.Trim());
	}

	public static bool IsKnownPresentation(string? presentation)
	{
		return !string.IsNullOrWhiteSpace(presentation)
			&& KnownPresentations.Contains(presentation.Trim());
	}

	public static IReadOnlySet<string> SupportedEffectTypes => new HashSet<string>(Contracts.Keys, StringComparer.Ordinal);

	public static bool HasExactResolverCoverage(
		IEnumerable<string> resolverEffectTypes,
		out string errorMessage)
	{
		return HasExactSetCoverage(
			ResolverEffectTypes,
			resolverEffectTypes,
			"effect resolver",
			out errorMessage);
	}

	public static bool HasExactPresentationCoverage(
		IEnumerable<string> customPresentationTypes,
		IEnumerable<string> genericPresentationTypes,
		out string errorMessage)
	{
		var actual = new HashSet<string>(customPresentationTypes ?? Array.Empty<string>(), StringComparer.Ordinal);
		foreach (string presentation in genericPresentationTypes ?? Array.Empty<string>())
		{
			actual.Add(presentation);
		}

		return HasExactSetCoverage(KnownPresentations, actual, "presentation handler", out errorMessage);
	}

	public static ItemCardContractValidationResult Validate(ItemCardDefinition definition)
	{
		if (!IsKnownPresentation(definition.PlayPresentation))
		{
			return ItemCardContractValidationResult.Invalid(
				$"Item card '{definition.Id}' has unknown presentation '{definition.PlayPresentation}'.");
		}

		ItemCardContractValidationResult rootResult = ValidateEffect(
			definition.Id,
			definition.Trigger,
			definition.EffectType,
			definition.EffectValues,
			definition.RewardDelivery,
			allowNone: true);

		if (!rootResult.IsValid)
		{
			return rootResult;
		}

		ItemCardContractValidationResult attachedResult = ValidateEffect(
			definition.Id,
			definition.AttachedTrigger,
			definition.AttachedEffectType,
			definition.AttachedEffectValues,
			definition.RewardDelivery,
			allowNone: true,
			attached: true);

		if (!attachedResult.IsValid)
		{
			return attachedResult;
		}

		if (definition.EnabledInGame
			&& definition.RewardDelivery == RewardDeliveryMode.None
			&& definition.CanBeQuestionReward)
		{
			return ItemCardContractValidationResult.Invalid(
				$"Reward item '{definition.Id}' must declare a delivery mode.");
		}

		if (definition.EnabledInGame
			&& !definition.CanBeQuestionReward
			&& definition.RewardDelivery != RewardDeliveryMode.None)
		{
			return ItemCardContractValidationResult.Invalid(
				$"Non-reward item '{definition.Id}' cannot declare delivery '{definition.RewardDelivery}'.");
		}

		if (definition.EnabledInGame && definition.CanBeQuestionReward)
		{
			if (definition.RewardDelivery == RewardDeliveryMode.Hand && !definition.PlayableFromHand)
			{
				return ItemCardContractValidationResult.Invalid(
					$"Hand reward '{definition.Id}' must be playable from hand.");
			}

			if (definition.RewardDelivery == RewardDeliveryMode.ImmediateAttachment
				&& definition.PlayableFromHand)
			{
				return ItemCardContractValidationResult.Invalid(
					$"Immediate attachment reward '{definition.Id}' cannot be playable from hand.");
			}
		}

		return ItemCardContractValidationResult.Valid;
	}

	public static ItemCardContractValidationResult ValidateEffect(
		string itemCardId,
		string trigger,
		string effectType,
		CardEffectValues values,
		RewardDeliveryMode delivery,
		bool allowNone,
		bool attached = false)
	{
		string normalizedEffectType = effectType?.Trim() ?? string.Empty;
		if (normalizedEffectType.Length == 0)
		{
			if (!allowNone || !string.IsNullOrWhiteSpace(trigger) || !values.IsEmpty)
			{
				return ItemCardContractValidationResult.Invalid(
					$"Item card '{itemCardId}' has an empty {(attached ? "attached " : string.Empty)}effect with data.");
			}

			return ItemCardContractValidationResult.Valid;
		}

		if (!Contracts.TryGetValue(normalizedEffectType, out ItemCardEffectContract? contract))
		{
			return ItemCardContractValidationResult.Invalid(
				$"Item card '{itemCardId}' has unknown {(attached ? "attached " : string.Empty)}effect '{normalizedEffectType}'.");
		}

		if (!contract.AllowedTriggers.Contains(trigger?.Trim() ?? string.Empty))
		{
			return ItemCardContractValidationResult.Invalid(
				$"Item card '{itemCardId}' uses trigger '{trigger}' for effect '{normalizedEffectType}'.");
		}

		foreach (string requiredKey in contract.RequiredValueKeys)
		{
			if (!values.All.ContainsKey(requiredKey))
			{
				return ItemCardContractValidationResult.Invalid(
					$"Item card '{itemCardId}' is missing value '{requiredKey}' for effect '{normalizedEffectType}'.");
			}
		}

		foreach (KeyValuePair<string, string> pair in values.All)
		{
			if (!contract.AllowedValueKeys.Contains(pair.Key))
			{
				return ItemCardContractValidationResult.Invalid(
					$"Item card '{itemCardId}' has unknown value '{pair.Key}' for effect '{normalizedEffectType}'.");
			}

			if (contract.ValueRanges.TryGetValue(pair.Key, out ItemCardEffectValueRange range)
				&& !range.Contains(pair.Value))
			{
				return ItemCardContractValidationResult.Invalid(
					$"Item card '{itemCardId}' value '{pair.Key}' is outside the range for effect '{normalizedEffectType}'.");
			}
		}

		if (!contract.AllowedDeliveryModes.Contains(delivery))
		{
			return ItemCardContractValidationResult.Invalid(
				$"Item card '{itemCardId}' uses delivery '{delivery}' for effect '{normalizedEffectType}'.");
		}

		return ItemCardContractValidationResult.Valid;
	}

	public static bool HasExactKeySet(
		IReadOnlyDictionary<string, string> values,
		IReadOnlySet<string> expectedKeys)
	{
		if (values.Count != expectedKeys.Count)
		{
			return false;
		}

		foreach (string key in values.Keys)
		{
			if (!expectedKeys.Contains(key))
			{
				return false;
			}
		}

		return true;
	}

	private static Dictionary<string, ItemCardEffectContract> BuildContracts()
	{
		var contracts = new Dictionary<string, ItemCardEffectContract>(StringComparer.Ordinal);
		Add(contracts, CardEffectTypes.PlaceTablePairDestroy, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.PairTag }, new[] { RewardDeliveryMode.None, RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.DrawCard, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Count }, new[] { RewardDeliveryMode.Hand }, new Dictionary<string, ItemCardEffectValueRange> { [CardEffectValueKeys.Count] = new(1, 52) }, requiresResolver: true);
		Add(contracts, CardEffectTypes.StealCard, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Target, CardEffectValueKeys.Count }, new[] { RewardDeliveryMode.None, RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.StealDeckCards, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Target, CardEffectValueKeys.Count }, new[] { RewardDeliveryMode.None, RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.CollectTable, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Target }, new[] { RewardDeliveryMode.None, RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.GrantReward, CardGameEvents.OnCollect, Array.Empty<string>(), new[] { RewardDeliveryMode.None }, requiresResolver: true);
		Add(contracts, CardEffectTypes.AttachModifier, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Modifier }, new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment }, requiresResolver: true);
		Add(contracts, CardEffectTypes.RevealCards, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Target, CardEffectValueKeys.Count }, new[] { RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.MagnetExtraCapture, CardGameEvents.OnCollect, new[] { CardEffectValueKeys.Target, CardEffectValueKeys.Count }, new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment }, new Dictionary<string, ItemCardEffectValueRange> { [CardEffectValueKeys.Count] = new(1, 52) });
		Add(contracts, CardEffectTypes.ShuffleTable, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Target }, new[] { RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.DoubleCollect, CardGameEvents.OnCollect, new[] { CardEffectValueKeys.Multiplier }, new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment }, new Dictionary<string, ItemCardEffectValueRange> { [CardEffectValueKeys.Multiplier] = new(1, 10) }, requiresResolver: true);
		Add(contracts, CardEffectTypes.DestroyCards, CardGameEvents.OnPlay, new[] { CardEffectValueKeys.Target, CardEffectValueKeys.Count }, new[] { RewardDeliveryMode.None, RewardDeliveryMode.Hand }, requiresResolver: true);
		Add(contracts, CardEffectTypes.ExtraTurn, CardGameEvents.OnCollect, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment }, requiresResolver: true);
		Add(contracts, CardEffectTypes.EndpointCrown, CardGameEvents.OnCollect, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		Add(contracts, CardEffectTypes.BorrowTide, CardGameEvents.OnPlay, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		Add(contracts, CardEffectTypes.ReverseAnchor, CardGameEvents.OnCollect, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		Add(contracts, CardEffectTypes.MidTideUpgrade, CardGameEvents.OnCollect, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		Add(contracts, CardEffectTypes.ReverseFlowAnchor, CardGameEvents.OnMiss, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		Add(contracts, CardEffectTypes.EmptyTideContinuation, CardGameEvents.OnMiss, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		Add(contracts, CardEffectTypes.SuitCollect, CardGameEvents.OnPlay, Array.Empty<string>(), new[] { RewardDeliveryMode.Hand, RewardDeliveryMode.ImmediateAttachment });
		return contracts;
	}

	private static void Add(
		IDictionary<string, ItemCardEffectContract> contracts,
		string effectType,
		string trigger,
		IReadOnlyCollection<string> requiredKeys,
		IReadOnlyCollection<RewardDeliveryMode> deliveries,
		IReadOnlyDictionary<string, ItemCardEffectValueRange>? ranges = null,
		bool requiresResolver = false)
	{
		var keys = new HashSet<string>(requiredKeys, StringComparer.Ordinal);
		contracts.Add(effectType, new ItemCardEffectContract(
			effectType,
			new HashSet<string>(new[] { trigger }, StringComparer.Ordinal),
			keys,
			keys,
			new ReadOnlyDictionary<string, ItemCardEffectValueRange>(
				ranges == null
					? new Dictionary<string, ItemCardEffectValueRange>(StringComparer.Ordinal)
					: new Dictionary<string, ItemCardEffectValueRange>(ranges, StringComparer.Ordinal)),
			new HashSet<RewardDeliveryMode>(deliveries),
			requiresResolver));
	}

	private static bool HasExactSetCoverage(
		IReadOnlySet<string> expected,
		IEnumerable<string> actualValues,
		string label,
		out string errorMessage)
	{
		var actual = new HashSet<string>(actualValues ?? Array.Empty<string>(), StringComparer.Ordinal);
		if (expected.SetEquals(actual))
		{
			errorMessage = string.Empty;
			return true;
		}

		var missing = new List<string>(expected.Except(actual, StringComparer.Ordinal));
		var extra = new List<string>(actual.Except(expected, StringComparer.Ordinal));
		missing.Sort(StringComparer.Ordinal);
		extra.Sort(StringComparer.Ordinal);
		errorMessage = $"{label} coverage mismatch; missing=[{string.Join(",", missing)}], extra=[{string.Join(",", extra)}].";
		return false;
	}
}
