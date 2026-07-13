public static class ItemCardEffectSupport
{
	public static bool CanFulfillDrawCardEffect(
		int deckCardCount,
		int totalHandCardCountBeforeUse,
		int maxTotalHandCards,
		int requestedDrawCount)
	{
		if (deckCardCount < 0
			|| totalHandCardCountBeforeUse <= 0
			|| maxTotalHandCards <= 0)
		{
			return false;
		}

		int drawCount = Math.Max(1, requestedDrawCount);
		int postUseHandCardCount = Math.Max(
			0,
			totalHandCardCountBeforeUse - 1);
		int availableHandSlots = Math.Max(
			0,
			maxTotalHandCards - postUseHandCardCount);
		return deckCardCount >= drawCount && availableHandSlots >= drawCount;
	}

	public static bool IsSupportedEffectType(string effectType)
	{
		return ItemCardEffectContractRegistry.IsKnownEffectType(effectType);
	}
}
