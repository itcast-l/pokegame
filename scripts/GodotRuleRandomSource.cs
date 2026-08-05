using Deckland.Rules;
using Godot;

internal sealed class GodotRuleRandomSource : IRuleRandomSource
{
	private readonly RandomNumberGenerator _random;

	internal GodotRuleRandomSource(RandomNumberGenerator random)
	{
		_random = random ?? throw new ArgumentNullException(nameof(random));
	}

	public RuleRandomState CaptureState()
	{
		return new RuleRandomState(_random.Seed, _random.State);
	}

	public void RestoreState(RuleRandomState state)
	{
		_random.Seed = state.Seed;
		_random.State = state.State;
	}

	public int NextIntInclusive(int minimum, int maximum)
	{
		if (minimum > maximum)
		{
			throw new ArgumentOutOfRangeException(nameof(minimum));
		}

		return _random.RandiRange(minimum, maximum);
	}
}
