#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
SCREENSHOT_DIR="${CODEX_LOCALIZATION_SCREENSHOT_DIR:-$ROOT_DIR/outputs/localization_screenshots/matrix-$TIMESTAMP}"
TIMEOUT_SECONDS="${CODEX_LOCALIZATION_SCREENSHOT_TIMEOUT_SECONDS:-20}"
SKIP_EXISTING="${CODEX_LOCALIZATION_SCREENSHOT_SKIP_EXISTING:-0}"
MATRIX_LOG="$SCREENSHOT_DIR/matrix.tsv"

scene_slug() {
	local scene="$1"
	scene="${scene#res://}"
	scene="${scene%.tscn}"
	scene="${scene//\//__}"
	scene="${scene//:/_}"
	printf '%s' "$scene"
}

normalize_scene() {
	local scene="$1"
	if [[ "$scene" == res://* ]]; then
		printf '%s' "$scene"
	elif [[ "$scene" == scenes/* ]]; then
		printf 'res://%s' "$scene"
	elif [[ -f "$ROOT_DIR/scenes/$scene.tscn" ]]; then
		printf 'res://scenes/%s.tscn' "$scene"
	else
		printf '%s' "$scene"
	fi
}

split_csv_filter() {
	local value="$1"
	if [[ -z "$value" ]]; then
		return 0
	fi
	local old_ifs="$IFS"
	IFS=','
	for item in $value; do
		item="${item#"${item%%[![:space:]]*}"}"
		item="${item%"${item##*[![:space:]]}"}"
		if [[ -n "$item" ]]; then
			printf '%s\n' "$item"
		fi
	done
	IFS="$old_ifs"
}

run_with_timeout() {
	local timeout="$1"
	shift
	local pid
	"$@" &
	pid=$!
	local deadline=$((SECONDS + timeout))
	while kill -0 "$pid" 2>/dev/null; do
		if (( SECONDS >= deadline )); then
			kill "$pid" 2>/dev/null || true
			sleep 1
			kill -9 "$pid" 2>/dev/null || true
			wait "$pid" 2>/dev/null || true
			return 124
		fi
		sleep 1
	done
	wait "$pid"
}

mkdir -p "$SCREENSHOT_DIR/_logs"

if [[ -n "${CODEX_LOCALIZATION_SCREENSHOT_SCENES:-}" ]]; then
	SCENES=()
	while IFS= read -r scene; do
		SCENES+=("$scene")
	done < <(split_csv_filter "$CODEX_LOCALIZATION_SCREENSHOT_SCENES")
else
	SCENES=()
	while IFS= read -r scene; do
		SCENES+=("$scene")
	done < <(find "$ROOT_DIR/scenes" -name '*.tscn' -type f | sort | sed "s#^$ROOT_DIR/#res://#")
fi

if [[ -n "${CODEX_LOCALIZATION_SCREENSHOT_LOCALES:-}" ]]; then
	LOCALES=()
	while IFS= read -r locale; do
		LOCALES+=("$locale")
	done < <(split_csv_filter "$CODEX_LOCALIZATION_SCREENSHOT_LOCALES")
else
	IFS=',' read -r -a CSV_HEADER < "$ROOT_DIR/localization/ui.csv"
	LOCALES=("${CSV_HEADER[@]:1}")
fi

printf 'scene\tlocale\tstatus\tscreenshot\tlog\n' >"$MATRIX_LOG"

total=$(( ${#SCENES[@]} * ${#LOCALES[@]} ))
done_count=0
failures=0

for scene in "${SCENES[@]}"; do
	scene="$(normalize_scene "$scene")"
	slug="$(scene_slug "$scene")"
	for locale in "${LOCALES[@]}"; do
		done_count=$((done_count + 1))
		target="$SCREENSHOT_DIR/$slug/$locale.png"
		log_dir="$SCREENSHOT_DIR/_logs/$slug"
		log_file="$log_dir/$locale.log"
		mkdir -p "$log_dir"

		if [[ "$SKIP_EXISTING" == "1" && -s "$target" ]]; then
			printf '%s\t%s\tSKIP_EXISTING\t%s\t%s\n' "$scene" "$locale" "$target" "$log_file" >>"$MATRIX_LOG"
			printf 'LOCALIZATION_SCREENSHOT_MATRIX_PROGRESS %d/%d SKIP %s %s\n' "$done_count" "$total" "$scene" "$locale"
			continue
		fi

		printf 'LOCALIZATION_SCREENSHOT_MATRIX_PROGRESS %d/%d RUN %s %s\n' "$done_count" "$total" "$scene" "$locale"
		set +e
		CODEX_LOCALIZATION_SCREENSHOT_SCENES="$scene" \
		CODEX_LOCALIZATION_SCREENSHOT_LOCALES="$locale" \
		CODEX_LOCALIZATION_SCREENSHOT_DIR="$SCREENSHOT_DIR" \
			run_with_timeout "$TIMEOUT_SECONDS" bash "$ROOT_DIR/tests/run_localization_screenshot_audit.sh" >"$log_file" 2>&1
		status=$?
		set -e

		if [[ "$status" -eq 0 && -s "$target" ]]; then
			printf '%s\t%s\tOK\t%s\t%s\n' "$scene" "$locale" "$target" "$log_file" >>"$MATRIX_LOG"
		elif [[ "$status" -eq 134 && -s "$target" ]] && grep -Fq "LOCALIZATION_SCREENSHOT_AUDIT_OK" "$log_file"; then
			printf '%s\t%s\tOK_AFTER_ABORT\t%s\t%s\n' "$scene" "$locale" "$target" "$log_file" >>"$MATRIX_LOG"
		else
			failures=$((failures + 1))
			printf '%s\t%s\tFAIL_%s\t%s\t%s\n' "$scene" "$locale" "$status" "$target" "$log_file" >>"$MATRIX_LOG"
		fi
	done
done

captured="$(find "$SCREENSHOT_DIR" -path "$SCREENSHOT_DIR/_logs" -prune -o -name '*.png' -type f -print | wc -l | tr -d ' ')"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_OUTPUT %s\n' "$SCREENSHOT_DIR"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_SCENES %d\n' "${#SCENES[@]}"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_LOCALES %d\n' "${#LOCALES[@]}"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_EXPECTED %d\n' "$total"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_CAPTURED %s\n' "$captured"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_FAILURES %d\n' "$failures"

if [[ "$failures" -ne 0 ]]; then
	exit 1
fi

printf 'LOCALIZATION_SCREENSHOT_MATRIX_OK\n'
