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
			|| effectType == "endpoint_crown"
			|| effectType == "borrow_tide"
			|| effectType == "reverse_anchor"
			|| effectType == "mid_tide_upgrade"
			|| effectType == "reverse_flow_anchor"
			|| effectType == "empty_tide_continuation"
			|| effectType == "destroy_cards"
			|| effectType == "extra_turn";
	}
}
