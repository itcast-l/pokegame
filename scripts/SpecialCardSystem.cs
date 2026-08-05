public static class CardGameEvents
{
	public const string OnPlay = "on_play";
	public const string OnMatch = "on_match";
	public const string OnCollect = "on_collect";
	public const string OnMiss = "on_miss";
	public const string OnRoundStart = "on_round_start";
	public const string OnRoundEnd = "on_round_end";
	public const string OnDraw = "on_draw";
	public const string OnShuffle = "on_shuffle";
	public const string OnDiscard = "on_discard";
	public const string OnEmptyDeck = "on_empty_deck";
}

public static class CardEffectTypes
{
	public const string None = "";
	public const string PlaceTablePairDestroy = "place_table_pair_destroy";
	public const string DrawCard = "draw_card";
	public const string StealCard = "steal_card";
	public const string StealDeckCards = "steal_deck_cards";
	public const string CollectTable = "collect_table";
	public const string GrantReward = "grant_reward";
	public const string AttachModifier = "attach_modifier";
	public const string RevealCards = "reveal_cards";
	public const string MagnetExtraCapture = "magnet_extra_capture";
	public const string ShuffleTable = "shuffle_table";
	public const string DoubleCollect = "double_collect";
	public const string ReverseCollect = "reverse_collect";
	public const string DestroyCards = "destroy_cards";
	public const string ExtraTurn = "extra_turn";
	public const string EndpointCrown = "endpoint_crown";
	public const string BorrowTide = "borrow_tide";
	public const string ReverseAnchor = "reverse_anchor";
	public const string MidTideUpgrade = "mid_tide_upgrade";
	public const string ReverseFlowAnchor = "reverse_flow_anchor";
	public const string EmptyTideContinuation = "empty_tide_continuation";
	public const string SuitCollect = "suit_collect";
	public const string BlockMatch = "block_match";
}

public static class CardEffectValueKeys
{
	public const string Modifier = "modifier";
	public const string Target = "target";
	public const string Count = "count";
	public const string PairTag = "pair_tag";
	public const string Multiplier = "multiplier";
}

public static class CardEffectTargets
{
	public const string PlayerHand = "player_hand";
	public const string PlayerDeck = "player_deck";
	public const string Table = "table";
	public const string OpponentTableFaceDown = "opponent_table_face_down";
}

public static class CardPlayPresentationTypes
{
	public const string DirectTrigger = "direct_trigger";
	public const string TableOverlay = "table_overlay";
	public const string TransformAreaTrigger = "transform_area_trigger";
	public const string LensSplit = "lens_split";
}

public static class CardTags
{
	public const string Fixed = "fixed";
	public const string Bomb = "bomb";
	public const string Reward = "reward";
	public const string Attachment = "attachment";
}
