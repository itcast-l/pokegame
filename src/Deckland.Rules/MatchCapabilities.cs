using System.Collections.Immutable;

namespace Deckland.Rules;

[Flags]
public enum MatchCapability : ulong
{
	None = 0,
	PersistentProgress = 1UL << 0,
	UpgradeSelection = 1UL << 1,
	LifeBattle = 1UL << 2,
	ScriptedTutorial = 1UL << 3,
	ItemRules = 1UL << 4,
	NetworkAuthority = 1UL << 5,
	SinglePlayerControl = 1UL << 6,
}

public readonly record struct MatchCapabilitySet
{
	private readonly MatchCapability _capabilities;

	public MatchCapabilitySet(MatchCapability capabilities)
	{
		_capabilities = capabilities;
	}

	public static MatchCapabilitySet Empty => new(MatchCapability.None);

	public bool Contains(MatchCapability capability)
	{
		return capability != MatchCapability.None
			&& (_capabilities & capability) == capability;
	}

	public MatchCapabilitySet Add(MatchCapability capability)
	{
		return new MatchCapabilitySet(_capabilities | capability);
	}

	public MatchCapabilitySet Union(MatchCapabilitySet other)
	{
		return new MatchCapabilitySet(_capabilities | other._capabilities);
	}
}

public interface IRuleModule
{
	string Id { get; }
	int Order { get; }
	ImmutableArray<string> Dependencies { get; }
	MatchCapabilitySet Capabilities { get; }
	RuleModuleDecision Evaluate(MatchCommand command, MatchReadModel readModel);
}

public readonly record struct RuleModuleDecision(
	bool Allowed,
	RuleRejection Rejection,
	ImmutableArray<PresentationIntent> PresentationIntents)
{
	public static RuleModuleDecision Allow => new(
		true,
		RuleRejection.None,
		ImmutableArray<PresentationIntent>.Empty);

	public static RuleModuleDecision Reject(RuleRejectionCode code, string detail = "")
	{
		return new RuleModuleDecision(
			false,
			new RuleRejection(code, detail),
			ImmutableArray<PresentationIntent>.Empty);
	}
}

public sealed class MatchRuleProfile
{
	public MatchRuleProfile(IEnumerable<IRuleModule> modules)
	{
		ArgumentNullException.ThrowIfNull(modules);

		IRuleModule[] orderedModules = modules.OrderBy(module => module.Order).ToArray();
		var ids = new HashSet<string>(StringComparer.Ordinal);
		var orders = new HashSet<int>();
		var capabilities = MatchCapabilitySet.Empty;

		foreach (IRuleModule module in orderedModules)
		{
			if (module == null)
			{
				throw new ArgumentException("Rule module lists cannot contain null entries.", nameof(modules));
			}

			if (string.IsNullOrWhiteSpace(module.Id))
			{
				throw new ArgumentException("Rule module IDs must be non-empty.", nameof(modules));
			}

			if (!ids.Add(module.Id))
			{
				throw new ArgumentException($"Duplicate rule module ID '{module.Id}'.", nameof(modules));
			}

			if (!orders.Add(module.Order))
			{
				throw new ArgumentException($"Rule module order {module.Order} is assigned more than once.", nameof(modules));
			}

			ImmutableArray<string> dependencies = module.Dependencies.IsDefault
				? ImmutableArray<string>.Empty
				: module.Dependencies;
			foreach (string dependency in dependencies)
			{
				if (string.IsNullOrWhiteSpace(dependency) || !ids.Contains(dependency))
				{
					throw new ArgumentException(
						$"Rule module '{module.Id}' requires missing or later module '{dependency}'.",
						nameof(modules));
				}
			}

			capabilities = capabilities.Union(module.Capabilities);
		}

		Modules = orderedModules.ToImmutableArray();
		Capabilities = capabilities;
	}

	public static MatchRuleProfile Empty { get; } = new(Array.Empty<IRuleModule>());

	public ImmutableArray<IRuleModule> Modules { get; }
	public MatchCapabilitySet Capabilities { get; }
}
