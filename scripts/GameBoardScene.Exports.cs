using System;
using Godot;

public partial class GameBoardScene
{
	// These read-only projections keep diagnostics and existing scene queries from owning startup state.
	// Construction is performed exclusively through GameBoardSceneFactory.
	public int player_count => _runtimeConfiguration?.PlayerCount ?? MinPlayers;
	public bool item_mode => _runtimeConfiguration?.ItemMode ?? false;
	public int winning_score => _runtimeConfiguration?.WinningScore ?? NetworkSessionDefaults.DefaultWinningScore;
	public bool adventure_mode => _runtimeConfiguration?.IsAdventure ?? false;
	public bool adventure_tutorial_mode => _runtimeConfiguration?.IsAdventureTutorial ?? false;
	public string adventure_tutorial_flow => _runtimeConfiguration?.TutorialFlow?.ToStableId() ?? string.Empty;
	public bool solo_mode => _runtimeConfiguration?.IsSolo ?? false;
	public int solo_ai_difficulty => (int)(_runtimeConfiguration?.SoloAiDifficulty ?? SoloAiDifficulty.Medium);
	public int local_player_index => _runtimeConfiguration?.LocalPlayerIndex ?? 0;
	public string network_session_id => _runtimeConfiguration?.NetworkSessionId ?? string.Empty;
	public string network_match_id => _runtimeConfiguration?.NetworkMatchId ?? string.Empty;
	public long match_seed => _runtimeConfiguration?.MatchSeed ?? 0;
	public string player_names_csv => _runtimeConfiguration == null
		? string.Empty
		: string.Join(',', _runtimeConfiguration.PlayerNames);
	public string player_avatar_ids_csv => _runtimeConfiguration == null
		? string.Empty
		: string.Join(',', _runtimeConfiguration.PlayerAvatarIds);
	public bool network_wait_for_initialize => _runtimeConfiguration?.NetworkWaitForInitialize ?? false;
	public string adventure_enemy_avatar_id => _runtimeConfiguration?.AdventureEnemyAvatarId ?? string.Empty;

#if DECKLAND_TEST_HOOKS
	[Export]
	public bool debug_opponent_has_one_card = false;

	[Export]
	public bool debug_main_player_has_one_card = false;

	[Export]
	public string debug_recording_setup_id = string.Empty;

	[Export(PropertyHint.Range, "-1,200,1")]
	public int debug_main_player_starting_score = -1;

	[Export]
	public bool debug_performance_trace = false;

	[Export(PropertyHint.Range, "0,50,0.5")]
	public float debug_performance_trace_min_ms = 4.0f;
#endif
}
