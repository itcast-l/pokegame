extends Node

signal steam_ready
signal lobby_created(connect: int, lobby_id: int)
signal lobby_joined(lobby_id: int, permissions: int, locked: bool, response: int)
signal lobby_match_list(lobbies: Array)
signal join_requested(lobby_id: int, friend_id: int)
signal lobby_data_update(success: int, lobby_id: int, member_id: int)

var is_available := false
var is_initialized := false
var init_status := -1
var init_message := ""
var steam_id := 0
var persona_name := ""
var current_game_language := ""
var steam_ui_language := ""
var available_game_languages := ""
var preferred_godot_locale := ""
var singleton_lookup_count := 0
var capability_query_count := 0
var process_call_count := 0

var _steam: Object
var _capabilities: Dictionary = {}
var _run_networking_callbacks_available := false

const _STEAM_LANGUAGE_FALLBACKS := {
	"arabic": "ar",
	"bulgarian": "bg",
	"schinese": "zh_CN",
	"tchinese": "zh_TW",
	"czech": "cs",
	"danish": "da",
	"dutch": "nl",
	"english": "en",
	"finnish": "fi",
	"french": "fr",
	"german": "de",
	"greek": "el",
	"hungarian": "hu",
	"indonesian": "id",
	"italian": "it",
	"japanese": "ja",
	"koreana": "ko",
	"norwegian": "no",
	"polish": "pl",
	"portuguese": "pt",
	"brazilian": "pt_BR",
	"romanian": "ro",
	"russian": "ru",
	"spanish": "es",
	"latam": "es_419",
	"swedish": "sv",
	"thai": "th",
	"turkish": "tr",
	"ukrainian": "uk",
	"vietnamese": "vi",
}


func _ready() -> void:
	set_process(false)
	_initialize_steam()


func _process(_delta: float) -> void:
	process_call_count += 1
	if not is_initialized:
		return

	if _steam == null:
		return

	_steam.run_callbacks()
	if _run_networking_callbacks_available:
		_steam.runNetworkingCallbacks()


func _exit_tree() -> void:
	if is_initialized:
		if _steam != null and _has_capability("steamShutdown"):
			_steam.steamShutdown()
	set_process(false)


func refresh_languages() -> void:
	if not is_initialized:
		return

	if _steam == null or not is_initialized:
		return

	if _has_capability("isSteamRunning") and not bool(_steam.isSteamRunning()):
		return

	current_game_language = str(_steam.getCurrentGameLanguage()).strip_edges() if _has_capability("getCurrentGameLanguage") else ""
	steam_ui_language = str(_steam.getSteamUILanguage()).strip_edges() if _has_capability("getSteamUILanguage") else ""
	available_game_languages = str(_steam.getAvailableGameLanguages()).strip_edges() if _has_capability("getAvailableGameLanguages") else ""
	preferred_godot_locale = _map_steam_language_to_locale(current_game_language)
	if preferred_godot_locale.is_empty():
		preferred_godot_locale = _map_steam_language_to_locale(steam_ui_language)


func get_preferred_godot_locale() -> String:
	if preferred_godot_locale.is_empty():
		refresh_languages()
	return preferred_godot_locale


func get_steam_language() -> String:
	return current_game_language if not current_game_language.is_empty() else steam_ui_language


func _initialize_steam() -> void:
	singleton_lookup_count += 1
	if OS.get_environment("DECKLAND_DISABLE_STEAM") == "1":
		init_message = "Steam is disabled by the runtime environment."
		set_process(false)
		return
	_steam = Engine.get_singleton("Steam") if Engine.has_singleton("Steam") else null
	if _steam == null:
		init_message = "GodotSteam singleton is not available."
		set_process(false)
		return

	_cache_capabilities()
	is_available = true
	if not _has_capability("steamInitEx"):
		init_message = "GodotSteam initialization method is not available."
		set_process(false)
		return
	var result = _steam.steamInitEx()
	if typeof(result) == TYPE_DICTIONARY:
		init_status = int(result.get("status", -1))
		init_message = str(result.get("verbal", ""))
		is_initialized = init_status == 0
	else:
		is_initialized = bool(result)
		init_status = 0 if is_initialized else -1
		init_message = "" if is_initialized else "Steam initialization failed."

	if not is_initialized:
		set_process(false)
		return

	steam_id = int(_steam.getSteamID()) if _has_capability("getSteamID") else 0
	persona_name = str(_steam.getPersonaName()) if _has_capability("getPersonaName") else ""
	refresh_languages()
	if _has_capability("initRelayNetworkAccess"):
		_steam.initRelayNetworkAccess()

	_connect_forwarded_signals()
	set_process(true)
	steam_ready.emit()


func _cache_capabilities() -> void:
	for method_name in [
		"steamInitEx",
		"run_callbacks",
		"runNetworkingCallbacks",
		"steamShutdown",
		"isSteamRunning",
		"getSteamID",
		"getPersonaName",
		"getCurrentGameLanguage",
		"getSteamUILanguage",
		"getAvailableGameLanguages",
		"initRelayNetworkAccess",
		"requestLobbyList",
		"inviteUserToLobby",
		"activateGameOverlayInviteDialog",
		"createLobby",
		"joinLobby",
		"getLobbyData",
		"getLobbyOwner",
		"setLobbyJoinable",
		"setLobbyData",
		"setLobbyOwner",
		"leaveLobby",
	]:
		_capabilities[method_name] = _steam.has_method(method_name)
		capability_query_count += 1
	_run_networking_callbacks_available = bool(_capabilities.get("runNetworkingCallbacks", false))


func _has_capability(method_name: String) -> bool:
	return bool(_capabilities.get(method_name, false))


func _connect_forwarded_signals() -> void:
	_connect_signal_if_available("lobby_created", _on_lobby_created)
	_connect_signal_if_available("lobby_joined", _on_lobby_joined)
	_connect_signal_if_available("lobby_match_list", _on_lobby_match_list)
	_connect_signal_if_available("join_requested", _on_join_requested)
	_connect_signal_if_available("lobby_data_update", _on_lobby_data_update)


func _connect_signal_if_available(signal_name: String, callback: Callable) -> void:
	if _steam.has_signal(signal_name) and not _steam.is_connected(signal_name, callback):
		_steam.connect(signal_name, callback)


func _on_lobby_created(connect: int, lobby_id: int) -> void:
	lobby_created.emit(connect, lobby_id)


func _on_lobby_joined(lobby_id: int, permissions: int, locked: bool, response: int) -> void:
	lobby_joined.emit(lobby_id, permissions, locked, response)


func _on_lobby_match_list(lobbies: Array) -> void:
	lobby_match_list.emit(lobbies)


func _on_join_requested(lobby_id: int, friend_id: int) -> void:
	join_requested.emit(lobby_id, friend_id)


func _on_lobby_data_update(success: int, lobby_id: int, member_id: int) -> void:
	lobby_data_update.emit(success, lobby_id, member_id)


func request_lobby_list() -> bool:
	if not is_initialized or not _has_capability("requestLobbyList"):
		return false
	_steam.requestLobbyList()
	return true


func invite_user_to_lobby(lobby_id: int, user_id: int) -> bool:
	return bool(_steam.inviteUserToLobby(lobby_id, user_id)) \
		if is_initialized and _has_capability("inviteUserToLobby") else false


func activate_lobby_invite_dialog(lobby_id: int) -> bool:
	if not is_initialized or not _has_capability("activateGameOverlayInviteDialog"):
		return false
	_steam.activateGameOverlayInviteDialog(lobby_id)
	return true


func create_lobby(lobby_type: int, player_count: int) -> bool:
	if not is_initialized or not _has_capability("createLobby"):
		return false
	_steam.createLobby(lobby_type, player_count)
	return true


func join_lobby(lobby_id: int) -> bool:
	if not is_initialized or not _has_capability("joinLobby"):
		return false
	_steam.joinLobby(lobby_id)
	return true


func get_lobby_data(lobby_id: int, key: String) -> String:
	return str(_steam.getLobbyData(lobby_id, key)) \
		if is_initialized and _has_capability("getLobbyData") else ""


func get_lobby_owner(lobby_id: int) -> int:
	return int(_steam.getLobbyOwner(lobby_id)) \
		if is_initialized and _has_capability("getLobbyOwner") else 0


func set_lobby_joinable(lobby_id: int, joinable: bool) -> bool:
	return bool(_steam.setLobbyJoinable(lobby_id, joinable)) \
		if is_initialized and _has_capability("setLobbyJoinable") else false


func set_lobby_data(lobby_id: int, key: String, value: String) -> bool:
	return bool(_steam.setLobbyData(lobby_id, key, value)) \
		if is_initialized and _has_capability("setLobbyData") else false


func set_lobby_owner(lobby_id: int, owner_id: int) -> bool:
	return bool(_steam.setLobbyOwner(lobby_id, owner_id)) \
		if is_initialized and _has_capability("setLobbyOwner") else false


func leave_lobby(lobby_id: int) -> bool:
	if not is_initialized or not _has_capability("leaveLobby"):
		return false
	_steam.leaveLobby(lobby_id)
	return true


func get_steam_id_for_peer(peer: MultiplayerPeer, peer_id: int) -> int:
	if peer == null or peer_id <= 0 or not peer.has_method("get_steam_id_for_peer_id"):
		return 0
	return int(peer.call("get_steam_id_for_peer_id", peer_id))


func _map_steam_language_to_locale(language_key: String) -> String:
	var normalized := language_key.strip_edges().to_lower()
	if normalized.is_empty():
		return ""

	if OS.has_feature("editor"):
		var mapper = load("res://addons/SteamLocaleMapper/SteamLocaleMapper.gd")
		if mapper != null and mapper.has_method("mapSteamLanguageKeyToLocale"):
			var mapped = mapper.mapSteamLanguageKeyToLocale(normalized)
			var normalized_mapped := _normalize_locale(str(mapped))
			if not normalized_mapped.is_empty():
				return normalized_mapped

	return _STEAM_LANGUAGE_FALLBACKS.get(normalized, "")


func _normalize_locale(locale: String) -> String:
	var normalized := locale.strip_edges().replace("-", "_")
	if normalized in ["zh_Hans", "zh_CN"]:
		return "zh_CN"
	if normalized in ["zh_Hant", "zh_TW"]:
		return "zh_TW"
	if normalized in ["pt_BR", "es_419"]:
		return normalized
	if normalized == "es" and current_game_language.strip_edges().to_lower() == "latam":
		return "es_419"
	return normalized if normalized in _STEAM_LANGUAGE_FALLBACKS.values() else ""
