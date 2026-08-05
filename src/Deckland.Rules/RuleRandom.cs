namespace Deckland.Rules;

public readonly record struct RuleRandomState(ulong Seed, ulong State);

public interface IRuleRandomSource
{
	RuleRandomState CaptureState();
	void RestoreState(RuleRandomState state);
	int NextIntInclusive(int minimum, int maximum);
}
