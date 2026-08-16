extends SceneTree

const MENU_SCENE_PATH := "res://scenes/adventure_avatar_menu_scene.tscn"
const PROFILE_SLOT := &"profile_0"
const MAX_WAIT_FRAMES := 3600

const EXPECTED_V8_KEYS := [
	"SchemaVersion",
	"CampaignId",
	"MainStageIndex",
	"EnemyAvatarId",
	"ShouldGrantRunStartUpgrade",
	"ShouldGrantVictoryUpgrade",
	"AcquiredUpgradeIds",
	"RetiredUpgradeIds",
	"CurrentHealth",
	"MaxHealth",
	"PermanentCaptureDamageBonus",
	"QuestionRewardCount",
	"HealingOverflowRemainder",
	"HasUsedRevive",
]
const LEGACY_ONLY_KEYS := [
	"CurrentRunStageIndex",
	"FinalDoubleCaptureDamageActive",
	"NextCaptureDamageBonus",
	"NextTurnHealAmount",
	"NextTurnDrawCount",
	"ClubShortBranchReturnUsedThisTurn",
	"ClubMissClimbGrowthStacks",
	"ClubBorrowSuitSetArmed",
	"ClubGapRootingUsedThisTurn",
	"FinalDoubleQuestionRewardCountThisBattle",
	"ClubFlourishStacks",
]
const EXPECTED_UPGRADE_IDS := [
	"diamonds_final_double",
	"diamonds_question_mark_interest",
	"hearts_overflow_growth",
]

var _failed := false
var _profile_save_started_count := 0
var _profile_save_completed_count := 0


func _initialize() -> void:
	call_deferred("_run")


func _run() -> void:
	await process_frame
	await process_frame
	var save_manager := root.get_node_or_null("SaveManager")
	if save_manager == null:
		_fail("SaveManager autoload is missing")
		return
	for method in [
		"ensure_slot_for_file_base",
		"set_value",
		"persist",
		"save_to_slot_sync",
		"load_from_slot_sync",
	]:
		if not save_manager.has_method(method):
			_fail("SaveManager is missing migration smoke method %s" % method)
			return

	save_manager.call("ensure_slot_for_file_base", PROFILE_SLOT, String(PROFILE_SLOT))
	save_manager.call("set_value", &"progress.active_profile_slot", String(PROFILE_SLOT))
	if int(save_manager.call("persist")) != OK:
		_fail("Failed to persist the active migration-smoke profile")
		return

	var legacy_snapshot := {
		"SchemaVersion": 4,
		"CurrentAvatarStageNumber": 3,
		"CurrentRunStageIndex": 99,
		"ShouldGrantRunStartUpgrade": false,
		"ShouldGrantVictoryUpgrade": true,
		"AcquiredUpgradeIds": [
			"hearts_overflow_growth",
			"diamonds_question_mark_interest",
			"diamonds_final_double",
			"diamonds_final_double",
			"hearts_blood_tide_resonance",
			"diamonds_all_gains_converge",
			"spades_black_tide_dominion",
			"clubs_lush_growth",
			"hearts_forged_unknown",
			"",
		],
		"CurrentHealth": -20,
		"MaxHealth": 999,
		"PermanentCaptureDamageBonus": 99,
		"QuestionRewardCount": 99,
		"FinalDoubleCaptureDamageActive": true,
		"HealingOverflowRemainder": 99,
		"NextCaptureDamageBonus": 9,
		"NextTurnHealAmount": 8,
		"NextTurnDrawCount": 7,
		"ClubShortBranchReturnUsedThisTurn": true,
		"ClubMissClimbGrowthStacks": 6,
		"ClubBorrowSuitSetArmed": true,
		"ClubGapRootingUsedThisTurn": true,
		"FinalDoubleQuestionRewardCountThisBattle": 10,
		"ClubFlourishStacks": 5,
	}
	var now := int(Time.get_unix_time_from_system())
	var profile := {
		"schema_version": 3,
		"highest_completed_main_adventure_stage": 0,
		"completed_adventure_tutorial_ids_csv": "",
		"selected_avatar_id": "shrimp",
		"player_name": "Migration Smoke",
		"pending_avatar_unlock_queue": "",
		"pending_adventure_run_json": JSON.stringify(legacy_snapshot),
		"unlocked_skill_codex_upgrade_ids_csv": "",
		"playtime_seconds": 0.0,
		"created_unix_time": now,
		"updated_unix_time": now,
	}
	if int(save_manager.call("save_to_slot_sync", PROFILE_SLOT, profile)) != OK:
		_fail("Failed to write the v4 adventure run fixture")
		return
	if int(save_manager.connect(
		&"save_started",
		Callable(self, "_on_save_started")
	)) != OK \
		or int(save_manager.connect(
			&"save_completed",
			Callable(self, "_on_save_completed")
		)) != OK:
		_fail("Failed to observe migration profile writes")
		return

	var packed_menu := load(MENU_SCENE_PATH) as PackedScene
	if packed_menu == null:
		_fail("Failed to load scene: %s" % MENU_SCENE_PATH)
		return
	var menu := packed_menu.instantiate()
	if menu == null:
		_fail("Failed to instantiate the adventure menu")
		return
	menu.set("selected_avatar_id", "shrimp")
	root.add_child(menu)
	await process_frame
	await process_frame

	var migrated_profile := save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary
	var migrated_json := String(migrated_profile.get("pending_adventure_run_json", ""))
	var migrated_value: Variant = JSON.parse_string(migrated_json)
	if typeof(migrated_value) != TYPE_DICTIONARY:
		_fail("Migrated adventure run payload is not valid JSON: %s" % migrated_json)
		return
	var migrated := migrated_value as Dictionary
	if not _assert_canonical_v8(migrated):
		return
	if _profile_save_started_count != 1 or _profile_save_completed_count != 1:
		_fail(
			"v4 migration did not perform exactly one profile rewrite " \
			+ "(started=%d completed=%d)" \
			% [_profile_save_started_count, _profile_save_completed_count]
		)
		return

	if not menu.has_method("DebugIsAdventureRunResumeDialogVisible"):
		_fail("Adventure menu is missing the resume-dialog debug hook")
		return
	var start_button := menu.get_node_or_null("%StartButton") as Button
	if start_button == null:
		_fail("Adventure menu is missing StartButton")
		return
	start_button.emit_signal("pressed")
	if not await _wait_for_resume_dialog(menu):
		return

	var reloaded_profile := save_manager.call("load_from_slot_sync", PROFILE_SLOT) as Dictionary
	var reloaded_json := String(reloaded_profile.get("pending_adventure_run_json", ""))
	if reloaded_json != migrated_json:
		_fail("Reloading canonical v8 rewrote the already migrated payload")
		return
	if _profile_save_started_count != 1 or _profile_save_completed_count != 1:
		_fail(
				"Reloading canonical v8 performed a redundant profile rewrite " \
			+ "(started=%d completed=%d)" \
			% [_profile_save_started_count, _profile_save_completed_count]
		)
		return

	print("ADVENTURE_UPGRADE_SNAPSHOT_V5_MIGRATION_SMOKE_OK")
	await _dispose_audio_before_quit()
	quit(0)


func _assert_canonical_v8(snapshot: Dictionary) -> bool:
	if snapshot.size() != EXPECTED_V8_KEYS.size():
		_fail("Canonical v8 property count mismatch: %s" % str(snapshot.keys()))
		return false
	for key in EXPECTED_V8_KEYS:
		if not snapshot.has(key):
			_fail("Canonical v8 payload is missing %s" % key)
			return false
	for key in LEGACY_ONLY_KEYS:
		if snapshot.has(key):
			_fail("Canonical v8 payload retained transient/legacy field %s" % key)
			return false

	var acquired_ids := snapshot.get("AcquiredUpgradeIds", []) as Array
	if acquired_ids != EXPECTED_UPGRADE_IDS:
		_fail("v4 migration did not filter and sort upgrade IDs: %s" % str(acquired_ids))
		return false
	var retired_ids := snapshot.get("RetiredUpgradeIds", []) as Array
	if not retired_ids.is_empty():
		_fail("v4 migration unexpectedly produced retired upgrade IDs: %s" % str(retired_ids))
		return false
	if int(snapshot.get("SchemaVersion", 0)) != 8 \
		or String(snapshot.get("CampaignId", "")) != "deep_tide_throne" \
		or int(snapshot.get("MainStageIndex", 0)) != 1 \
		or String(snapshot.get("EnemyAvatarId", "")) != "shrimp" \
		or int(snapshot.get("CurrentHealth", -1)) != 0 \
		or int(snapshot.get("MaxHealth", -1)) != 150 \
		or int(snapshot.get("PermanentCaptureDamageBonus", -1)) != 5 \
		or int(snapshot.get("QuestionRewardCount", -1)) != 10 \
		or int(snapshot.get("HealingOverflowRemainder", -1)) != 3:
		_fail("v4 migration did not clamp canonical v8 values: %s" % str(snapshot))
		return false
	if bool(snapshot.get("ShouldGrantRunStartUpgrade", true)) \
		or not bool(snapshot.get("ShouldGrantVictoryUpgrade", false)) \
		or bool(snapshot.get("HasUsedRevive", true)):
		_fail("v4 migration changed persisted upgrade grant flags")
		return false
	return true


func _wait_for_resume_dialog(menu: Node) -> bool:
	for _frame_index in range(MAX_WAIT_FRAMES):
		await process_frame
		if not is_instance_valid(menu):
			_fail("Adventure menu freed while waiting for the migrated run dialog")
			return false
		if bool(menu.call("DebugIsAdventureRunResumeDialogVisible")):
			return true
	_fail("Exceeded %d process frames waiting for the migrated run dialog" % MAX_WAIT_FRAMES)
	return false


func _on_save_started(slot_id: StringName) -> void:
	if slot_id == PROFILE_SLOT:
		_profile_save_started_count += 1


func _on_save_completed(slot_id: StringName) -> void:
	if slot_id == PROFILE_SLOT:
		_profile_save_completed_count += 1


func _dispose_audio_before_quit() -> void:
	var game_audio := root.get_node_or_null("GameAudio")
	if game_audio == null or not game_audio.has_method("DisposeAllAudioImmediately"):
		return
	game_audio.call("DisposeAllAudioImmediately")
	await process_frame
	await process_frame


func _fail(message: String) -> void:
	if _failed:
		return
	_failed = true
	push_error(message)
	print("ADVENTURE_UPGRADE_SNAPSHOT_V5_MIGRATION_SMOKE_FAIL %s" % message)
	quit(1)
