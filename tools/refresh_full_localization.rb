#!/usr/bin/env ruby

require "csv"
require "json"
require "net/http"
require "open3"
require "uri"

ROOT = File.expand_path("..", __dir__)
CSV_PATH = File.join(ROOT, "localization", "ui.csv")

GOOGLE_LOCALES = {
	"ar" => "ar",
	"bg" => "bg",
	"zh_CN" => "zh-CN",
	"zh_TW" => "zh-TW",
	"cs" => "cs",
	"da" => "da",
	"nl" => "nl",
	"fi" => "fi",
	"fr" => "fr",
	"de" => "de",
	"el" => "el",
	"hu" => "hu",
	"id" => "id",
	"it" => "it",
	"ja" => "ja",
	"ko" => "ko",
	"no" => "no",
	"pl" => "pl",
	"pt" => "pt-PT",
	"pt_BR" => "pt-BR",
	"ro" => "ro",
	"ru" => "ru",
	"es" => "es",
	"es_419" => "es-419",
	"sv" => "sv",
	"th" => "th",
	"tr" => "tr",
	"uk" => "uk",
	"vi" => "vi",
}.freeze

FALLBACK_KEYS = %w[
	ITEM_CARD_CONFIG_ERROR_TITLE
	ITEM_CARD_CONFIG_ERROR_BODY
	GAME_ADVENTURE_SAVING
	GAME_ADVENTURE_SAVE_RETRYING
	GAME_ADVENTURE_SAVE_FAILED_REPLAY
	GAME_ADVENTURE_RETURNING_TO_MENU
	ADVENTURE_RECOVERY_TITLE
	ADVENTURE_RECOVERY_BODY
	ADVENTURE_RECOVERY_RESTART
	ADVENTURE_RECOVERY_SAVE_FAILED
	NETWORK_STATE_LEAVING
].freeze

CRITICAL_UPGRADE_KEYS = %w[
	UPGRADE_DESC_HEARTS_WARM_CURRENT_SUPPLY
	UPGRADE_DESC_HEARTS_MISS_BANDAGE
	UPGRADE_DESC_HEARTS_THICK_BLOOD
	UPGRADE_DESC_HEARTS_CHAIN_TIDE_BREATH
	UPGRADE_DESC_HEARTS_RED_TIDE_RETURN
	UPGRADE_DESC_HEARTS_ENCHANTED_RECOVERY
	UPGRADE_DESC_HEARTS_RETURN_TIDE_HEART
	UPGRADE_DESC_HEARTS_REVIVAL_DRAW
	UPGRADE_DESC_HEARTS_OVERFLOW_GROWTH
	UPGRADE_DESC_DIAMONDS_SMALL_INSURANCE
	UPGRADE_DESC_DIAMONDS_REJUDGE_ONCE
	UPGRADE_DESC_DIAMONDS_QUESTION_MARK_WINDFALL
	UPGRADE_DESC_DIAMONDS_QUESTION_MARK_ECHO
	UPGRADE_DESC_DIAMONDS_QUESTION_MARK_INTEREST
	UPGRADE_DESC_DIAMONDS_TOP_PRIZE_REPEAT
	UPGRADE_DESC_DIAMONDS_FULL_PAYOUT
	UPGRADE_DESC_DIAMONDS_FINAL_DOUBLE
	UPGRADE_DESC_SPADES_DARK_HARBOR_REVEAL
	UPGRADE_DESC_SPADES_WRONG_SHORE_LINEUP
	UPGRADE_DESC_SPADES_REVERSE_CAPTURE_TAX
	UPGRADE_DESC_SPADES_EXACTION
	UPGRADE_DESC_SPADES_HUNT_ORDER
	UPGRADE_DESC_SPADES_INTERCEPT_BEAT
	UPGRADE_DESC_SPADES_BLACK_LEDGER
	UPGRADE_DESC_CLUBS_SHORT_BRANCH_RETURN
	UPGRADE_DESC_CLUBS_GAP_ROOTING
	UPGRADE_DESC_CLUBS_PRIMARY_ROOT_RETURN
	UPGRADE_DESC_CLUBS_MISS_CLIMB_GROWTH
	UPGRADE_DESC_CLUBS_BORROW_SUIT_SET
	UPGRADE_DESC_CLUBS_CONTINUE_BRANCH
	UPGRADE_DESC_CLUBS_LINKED_STEMS
	UPGRADE_DESC_CLUBS_CROSS_GRAFT
	UPGRADE_DESC_CLUBS_CROWN_CANOPY
].freeze

NEW_ROWS = {
	"SETTINGS_INPUT_GAMEPAD_A" => "A",
	"SETTINGS_INPUT_GAMEPAD_B" => "B",
	"SETTINGS_INPUT_GAMEPAD_X" => "X",
	"SETTINGS_INPUT_GAMEPAD_Y" => "Y",
	"SETTINGS_INPUT_GAMEPAD_BACK" => "Back",
	"SETTINGS_INPUT_GAMEPAD_GUIDE" => "Guide",
	"SETTINGS_INPUT_GAMEPAD_START" => "Start",
	"SETTINGS_INPUT_GAMEPAD_LEFT_STICK" => "Left Stick",
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK" => "Right Stick",
	"SETTINGS_INPUT_GAMEPAD_LB" => "LB",
	"SETTINGS_INPUT_GAMEPAD_RB" => "RB",
	"SETTINGS_INPUT_GAMEPAD_DPAD_UP" => "D-Pad Up",
	"SETTINGS_INPUT_GAMEPAD_DPAD_DOWN" => "D-Pad Down",
	"SETTINGS_INPUT_GAMEPAD_DPAD_LEFT" => "D-Pad Left",
	"SETTINGS_INPUT_GAMEPAD_DPAD_RIGHT" => "D-Pad Right",
	"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_RIGHT" => "Left Stick Right",
	"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_LEFT" => "Left Stick Left",
	"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_DOWN" => "Left Stick Down",
	"SETTINGS_INPUT_GAMEPAD_LEFT_STICK_UP" => "Left Stick Up",
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_RIGHT" => "Right Stick Right",
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_LEFT" => "Right Stick Left",
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_DOWN" => "Right Stick Down",
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_UP" => "Right Stick Up",
	"SETTINGS_INPUT_GAMEPAD_LT" => "LT",
	"SETTINGS_INPUT_GAMEPAD_RT" => "RT",
	"NETWORK_ERROR_CONNECTION_FAILED" => "Connection failed. Check your network and try again.",
	"NETWORK_ERROR_LAN_UNAVAILABLE" => "LAN connection failed. Check the local network and try again.",
	"NETWORK_ERROR_STEAM_UNAVAILABLE" => "Steam multiplayer is unavailable. Check Steam and try again.",
	"NETWORK_ERROR_ROOM_AUTHENTICATION_FAILED" => "Room authentication failed. Check the password and try again.",
	"NETWORK_ERROR_ROOM_UNAVAILABLE" => "This room is no longer available to join.",
	"NETWORK_ERROR_ROOM_FULL" => "This room is full.",
	"NETWORK_ERROR_SCENE_LOAD_FAILED" => "The match could not start because scene loading failed.",
	"NETWORK_ERROR_RECONNECT_FAILED" => "Could not reconnect to the match.",
	"NETWORK_ERROR_HOST_DISCONNECTED" => "The host disconnected. Returning to the multiplayer menu.",
	"NETWORK_ERROR_HOST_MIGRATION_FAILED" => "Host migration failed. Returning to the multiplayer menu.",
	"NETWORK_ERROR_CHECKPOINT_FAILED" => "The latest match checkpoint could not be saved.",
	"NETWORK_STATE_AI_TAKEOVER" => "Disconnected - AI takeover",
	"NETWORK_STATE_CONTROL_PENDING" => "Reconnected - control returns next turn",
	"NETWORK_STATE_SYNCING_MATCH" => "Syncing the latest match...",
	"NETWORK_STATE_RECONNECTED_NOTICE" => "Reconnected. Control returns on your next turn.",
	"NETWORK_STATE_MIGRATING_HOST" => "Migrating the host and restoring the match...",
	"NETWORK_STATE_MIGRATION_FAILED_RETURNING" => "Host migration failed. Returning to the multiplayer menu...",
}.freeze

INVARIANT_NEW_ROW_KEYS = %w[
	SETTINGS_INPUT_GAMEPAD_A
	SETTINGS_INPUT_GAMEPAD_B
	SETTINGS_INPUT_GAMEPAD_X
	SETTINGS_INPUT_GAMEPAD_Y
	SETTINGS_INPUT_GAMEPAD_LB
	SETTINGS_INPUT_GAMEPAD_RB
	SETTINGS_INPUT_GAMEPAD_LT
	SETTINGS_INPUT_GAMEPAD_RT
].freeze

TRANSLATION_OVERRIDES = {
	"SETTINGS_INPUT_GAMEPAD_DPAD_UP" => {
		"el" => "Σταυρός κατεύθυνσης επάνω",
	},
	"SETTINGS_INPUT_GAMEPAD_DPAD_DOWN" => {
		"el" => "Σταυρός κατεύθυνσης κάτω",
	},
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_RIGHT" => {
		"bg" => "Десен стик надясно",
		"uk" => "Правий стік праворуч",
	},
	"SETTINGS_INPUT_GAMEPAD_RIGHT_STICK_UP" => {
		"el" => "Δεξιός μοχλός επάνω",
	},
}.freeze

full_refresh = ARGV.delete("--full")
raise "Usage: #{$PROGRAM_NAME} [--full]" unless ARGV.empty?

def read_head_table
	text, error, status = Open3.capture3("git", "show", "HEAD:localization/ui.csv", chdir: ROOT)
	raise "Cannot read HEAD localization table: #{error}" unless status.success?

	CSV.parse(text, headers: true)
end

def freeze_placeholders(text)
	placeholders = text.scan(/\{[A-Za-z0-9_]+\}/).uniq
	frozen = text.dup
	placeholders.each_with_index do |placeholder, index|
		frozen.gsub!(placeholder, "ZXQPH#{index}QXZ")
	end
	[frozen, placeholders]
end

def restore_placeholders(text, placeholders)
	restored = text.dup
	placeholders.each_with_index do |placeholder, index|
		restored.gsub!("ZXQPH#{index}QXZ", placeholder)
	end
	missing = placeholders.reject { |placeholder| restored.include?(placeholder) }
	restored = "#{restored} (#{missing.join(", ")})" unless missing.empty?
	restored
end

def request_translation(target_locale, texts)
	body = texts.each_with_index.map { |text, index| "ZXQROW#{index}QXZ\n#{text}" }.join("\n")
	uri = URI("https://translate.googleapis.com/translate_a/single")
	uri.query = URI.encode_www_form(client: "gtx", sl: "en", tl: target_locale, dt: "t", q: body)

	response = Net::HTTP.get_response(uri)
	raise "Translation request failed for #{target_locale}: HTTP #{response.code}" unless response.is_a?(Net::HTTPSuccess)

	payload = JSON.parse(response.body)
	translated = payload.fetch(0).map { |segment| segment.fetch(0) }.join
	parts = translated.split(/ZXQROW(\d+)QXZ\s*/)
	result = Array.new(texts.length)
	(1...parts.length).step(2) do |index|
		result[Integer(parts[index], 10)] = parts[index + 1].to_s.strip
	end
	raise "Translation response lost row markers for #{target_locale}" if result.any?(&:nil?)

	result
end

def translate_rows(rows_by_key, keys, headers)
	GOOGLE_LOCALES.each do |locale, target_locale|
		raise "Missing locale column #{locale}" unless headers.include?(locale)

		keys.each_slice(12) do |key_slice|
			frozen_texts = []
			placeholder_sets = []
			key_slice.each do |key|
				frozen, placeholders = freeze_placeholders(rows_by_key.fetch(key)["en"])
				frozen_texts << frozen
				placeholder_sets << placeholders
			end

			translated = nil
			3.times do |attempt|
				begin
					translated = request_translation(target_locale, frozen_texts)
					break
				rescue StandardError => error
					raise if attempt == 2
					warn "Retrying #{target_locale} after #{error.message}"
					sleep(1 + attempt)
				end
			end

			key_slice.each_with_index do |key, index|
				rows_by_key.fetch(key)[locale] = restore_placeholders(translated.fetch(index), placeholder_sets.fetch(index))
			end
		end
		puts "Translated #{keys.length} rows for #{locale}"
	end
end

table = CSV.read(CSV_PATH, headers: true)
headers = table.headers
raise "Localization table must contain 30 locale columns" unless headers.length == 31 && headers.first == "key"

rows = table.map(&:dup)
rows_by_key = rows.each_with_object({}) { |row, result| result[row["key"]] = row }

if full_refresh
	head_table = read_head_table
	head_by_key = head_table.each_with_object({}) { |row, result| result[row["key"]] = row }
	rows_by_key.each do |key, row|
		next unless key.start_with?("UPGRADE_DESC_")

		base_row = head_by_key.fetch(key)
		head_table.headers.each { |header| row[header] = base_row[header] }
	end
end

insertion_index = rows.index { |row| row["key"].start_with?("UPGRADE_DESC_") } || rows.length
NEW_ROWS.each do |key, english|
	next if rows_by_key.key?(key)

	row = CSV::Row.new(headers, Array.new(headers.length, ""))
	row["key"] = key
	row["en"] = english
	if INVARIANT_NEW_ROW_KEYS.include?(key)
		headers.drop(1).each { |locale| row[locale] = english }
	end
	rows.insert(insertion_index, row)
	insertion_index += 1
	rows_by_key[key] = row
end

broad_english_fallback_keys = rows.each_with_object([]) do |row, result|
	key = row["key"]
	next if key == "APP_NAME" || key.start_with?("LANG_")

	matching_locales = headers.drop(1).count do |locale|
		locale != "en" && row[locale].to_s == row["en"].to_s && row["en"].to_s.match?(/[A-Za-z]{3}/)
	end
	result << key if matching_locales >= 10
end
keys_to_translate = if full_refresh
	(FALLBACK_KEYS + CRITICAL_UPGRADE_KEYS + NEW_ROWS.keys + broad_english_fallback_keys).uniq
else
	NEW_ROWS.keys.reject do |key|
		INVARIANT_NEW_ROW_KEYS.include?(key) || headers.drop(1).all? { |locale| !rows_by_key.fetch(key)[locale].to_s.strip.empty? }
	end
end
missing_keys = keys_to_translate.reject { |key| rows_by_key.key?(key) }
raise "Missing source rows: #{missing_keys.join(", ")}" unless missing_keys.empty?

translate_rows(rows_by_key, keys_to_translate, headers) unless keys_to_translate.empty?

TRANSLATION_OVERRIDES.each do |key, values_by_locale|
	row = rows_by_key.fetch(key)
	values_by_locale.each { |locale, value| row[locale] = value }
end

rows.each do |row|
	english_tokens = row["en"].to_s.scan(/\{[A-Za-z0-9_]+\}/).sort
	headers.drop(1).each do |locale|
		text = row[locale].to_s
		raise "Empty localization cell key=#{row["key"]} locale=#{locale}" if text.strip.empty?

		tokens = text.scan(/\{[A-Za-z0-9_]+\}/).sort
		raise "Placeholder mismatch key=#{row["key"]} locale=#{locale}" unless tokens == english_tokens
	end
end

File.open(CSV_PATH, "w:utf-8") do |file|
	file << CSV.generate_line(headers)
	rows.each { |row| file << CSV.generate_line(headers.map { |header| row[header] }) }
end

puts "Updated #{CSV_PATH} with #{rows.length} rows"
