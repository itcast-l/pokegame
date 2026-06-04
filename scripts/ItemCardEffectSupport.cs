public static class ItemCardEffectSupport
{
	public static bool IsSupportedEffectType(string effectType)
	{
		if (string.IsNullOrWhiteSpace(effectType))
		{
			return false;
		}

		// Keep this list aligned with CardEffectTypes in SpecialCardSystem.cs.
		return effectType == "place_table_pair_destroy"
			|| effectType == "draw_card"
			|| effectType == "steal_card"
			|| effectType == "steal_deck_cards"
			|| effectType == "collect_table"
			|| effectType == "grant_reward"
			|| effectType == "attach_modifier"
			|| effectType == "reveal_cards"
			|| effectType == "shuffle_table"
			|| effectType == "double_collect"
			|| effectType == "destroy_cards"
			|| effectType == "extra_turn";
	}
}
