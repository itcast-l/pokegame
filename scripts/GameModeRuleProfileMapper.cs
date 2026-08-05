using System.Collections.Immutable;
using Deckland.Rules;

internal static class GameModeRuleProfileMapper
{
	internal static MatchRuleProfile Create(GameModePolicy policy, bool itemRulesEnabled)
	{
		ArgumentNullException.ThrowIfNull(policy);

		MatchCapability capabilities = MatchCapability.None;
		if (itemRulesEnabled)
		{
			capabilities |= MatchCapability.ItemRules;
		}

		if (policy.IsNetworkStandard)
		{
			capabilities |= MatchCapability.NetworkAuthority;
		}
		else if (policy.IsAdventureTutorial)
		{
			capabilities |= MatchCapability.ScriptedTutorial
				| MatchCapability.LifeBattle
				| MatchCapability.SinglePlayerControl;
		}
		else if (policy.IsAdventureMain)
		{
			capabilities |= MatchCapability.PersistentProgress
				| MatchCapability.UpgradeSelection
				| MatchCapability.LifeBattle
				| MatchCapability.SinglePlayerControl;
		}
		else if (policy.IsSoloStandard)
		{
			capabilities |= MatchCapability.SinglePlayerControl;
		}

		return new MatchRuleProfile(new IRuleModule[]
		{
			new CompatibilityCapabilityModule(new MatchCapabilitySet(capabilities)),
		});
	}

	private sealed class CompatibilityCapabilityModule : IRuleModule
	{
		internal CompatibilityCapabilityModule(MatchCapabilitySet capabilities)
		{
			Capabilities = capabilities;
		}

		public string Id => "core.capabilities";
		public int Order => 0;
		public ImmutableArray<string> Dependencies => ImmutableArray<string>.Empty;
		public MatchCapabilitySet Capabilities { get; }

		public RuleModuleDecision Evaluate(MatchCommand command, MatchReadModel readModel)
		{
			return RuleModuleDecision.Allow;
		}
	}
}
