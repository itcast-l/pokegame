extends Node

signal steam_ready

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
	_initialize_steam()


func _process(_delta: float) -> void:
	if not is_initialized:
		return

	var steam := _get_steam()
	if steam == null:
		return

	steam.run_callbacks()
	if steam.has_method("runNetworkingCallbacks"):
		steam.runNetworkingCallbacks()


func _exit_tree() -> void:
	if is_initialized:
		var steam := _get_steam()
		if steam != null and steam.has_method("steamShutdown"):
			steam.steamShutdown()


func refresh_languages() -> void:
	if not is_initialized:
		return

	var steam := _get_steam()
	if steam == null or not is_initialized:
		return

	if steam.has_method("isSteamRunning") and not bool(steam.isSteamRunning()):
		return

	current_game_language = str(steam.getCurrentGameLanguage()).strip_edges() if steam.has_method("getCurrentGameLanguage") else ""
	steam_ui_language = str(steam.getSteamUILanguage()).strip_edges() if steam.has_method("getSteamUILanguage") else ""
	available_game_languages = str(steam.getAvailableGameLanguages()).strip_edges() if steam.has_method("getAvailableGameLanguages") else ""
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
	var steam := _get_steam()
	if steam == null:
		init_message = "GodotSteam singleton is not available."
		return

	is_available = true
	var result = steam.steamInitEx()
	if typeof(result) == TYPE_DICTIONARY:
		init_status = int(result.get("status", -1))
		init_message = str(result.get("verbal", ""))
		is_initialized = init_status == 0
	else:
		is_initialized = bool(result)
		init_status = 0 if is_initialized else -1
		init_message = "" if is_initialized else "Steam initialization failed."

	if not is_initialized:
		return

	steam_id = int(steam.getSteamID())
	persona_name = str(steam.getPersonaName())
	refresh_languages()
	if steam.has_method("initRelayNetworkAccess"):
		steam.initRelayNetworkAccess()

	steam_ready.emit()


func _get_steam() -> Object:
	if not Engine.has_singleton("Steam"):
		return null
	return Engine.get_singleton("Steam")


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
