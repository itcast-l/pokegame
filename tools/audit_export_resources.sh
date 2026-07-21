#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

MAX_LIST_ITEMS=120
RAW_SOURCE_SIZE_THRESHOLD_BYTES=$((5 * 1024 * 1024))
FAILED_CHECKS=0

print_section() {
  printf '\n[%s]\n' "$1"
}

print_ok() {
  printf 'OK   %s\n' "$1"
}

print_risk() {
  FAILED_CHECKS=$((FAILED_CHECKS + 1))
  printf 'RISK %s\n' "$1"
}

count_non_empty_lines() {
  printf '%s\n' "$1" | sed '/^$/d' | wc -l | tr -d ' '
}

print_limited_list() {
  local matches="$1"
  local count
  count="$(count_non_empty_lines "$matches")"
  printf '%s\n' "$matches" | sed '/^$/d' | sed -n "1,${MAX_LIST_ITEMS}p"
  if [ "${count}" -gt "${MAX_LIST_ITEMS}" ]; then
    printf '... %d more\n' "$((count - MAX_LIST_ITEMS))"
  fi
}

file_size_bytes() {
  local path="$1"
  if stat -f '%z' "$path" >/dev/null 2>&1; then
    stat -f '%z' "$path"
    return
  fi

  stat -c '%s' "$path"
}

format_mib() {
  awk -v bytes="$1" 'BEGIN { printf "%.2f MiB", bytes / 1048576 }'
}

run_pathspec_check() {
  local label="$1"
  shift
  local matches
  matches="$(git ls-files -- "$@" || true)"
  if [ -n "$(printf '%s' "$matches" | sed '/^$/d')" ]; then
    print_risk "$label ($(count_non_empty_lines "$matches") tracked)"
    print_limited_list "$matches"
    return
  fi

  print_ok "$label"
}

print_category_summary() {
  python3 - <<'PY'
from pathlib import Path
import subprocess
from collections import Counter

root = Path.cwd()
files = subprocess.check_output(
    ['git', 'ls-files', 'generated-images', 'outputs', '*.debug-sse.log'],
    cwd=root,
    text=True,
).splitlines()
files = [path for path in files if (root / path).exists()]

counter = Counter()
for path in files:
    if path.startswith('generated-images/upgrade-popup-assets/'):
        if '/processed/' in path:
            key = 'upgrade_popup_processed'
        elif '/raw/' in path:
            key = 'upgrade_popup_raw'
        elif path.endswith('transparent-processing-meta.json'):
            key = 'upgrade_popup_meta'
        else:
            key = 'upgrade_popup_root_concepts'
    elif path.startswith('generated-images/board-top-extension-preview/'):
        key = 'board_top_preview'
    elif path.startswith('outputs/'):
        key = 'outputs'
    elif path.endswith('.debug-sse.log'):
        key = 'debug_log'
    else:
        key = 'generated_misc'
    counter[key] += 1

for key in (
    'upgrade_popup_processed',
    'upgrade_popup_raw',
    'upgrade_popup_meta',
    'upgrade_popup_root_concepts',
    'board_top_preview',
    'generated_misc',
    'outputs',
    'debug_log',
):
    print(f'{key}\t{counter.get(key, 0)}')
PY
}

is_source_art_extension() {
  case "$1" in
    *.psd|*.kra|*.xcf|*.clip|*.aseprite|*.ora|*.tif|*.tiff|*.bmp|*.exr) return 0 ;;
    *) return 1 ;;
  esac
}

is_upgrade_popup_source_only_asset() {
  local path="$1"
  case "$path" in
    generated-images/upgrade-popup-assets/raw/*|generated-images/upgrade-popup-assets/*/raw/*|generated-images/upgrade-popup-assets/transparent-processing-meta.json) return 0 ;;
  esac

  local prefix="generated-images/upgrade-popup-assets/"
  if [[ "$path" == "$prefix"* ]]; then
    local remainder="${path#$prefix}"
    if [[ "$remainder" != */* ]] && [[ "$remainder" == *.png || "$remainder" == *.png.import ]]; then
      return 0
    fi
  fi

  return 1
}

is_raw_or_source_path() {
  case "$1" in
    */raw/*|*/source/*|*/sources/*) return 0 ;;
    *) return 1 ;;
  esac
}

collect_tracked_generated_output_risks() {
  local path output=""
  while IFS= read -r path; do
    [ -z "$path" ] && continue
    [ -e "$path" ] || continue

    case "$path" in
      outputs/*|outputs)
        output+="$path"$'\n'
        continue
        ;;
      generated-images/*|generated-images)
        output+="$path"$'\n'
        ;;
    esac
  done < <(git ls-files -- generated-images outputs 2>/dev/null || true)

  printf '%s' "$output"
}

collect_tracked_raw_source_risks() {
  local path size output=""
  while IFS= read -r path; do
    [ -z "$path" ] && continue
    [ -f "$path" ] || continue

    if is_upgrade_popup_source_only_asset "$path"; then
      size="$(file_size_bytes "$path")"
      output+="$path ($(format_mib "$size"))"$'\n'
      continue
    fi

    if is_source_art_extension "$path"; then
      size="$(file_size_bytes "$path")"
      output+="$path ($(format_mib "$size"))"$'\n'
      continue
    fi

    if is_raw_or_source_path "$path"; then
      size="$(file_size_bytes "$path")"
      if [ "$size" -ge "$RAW_SOURCE_SIZE_THRESHOLD_BYTES" ]; then
        output+="$path ($(format_mib "$size"))"$'\n'
      fi
    fi
  done < <(git ls-files -- art assets asset generated-images resources 2>/dev/null || true)

  printf '%s' "$output"
}

load_exclude_patterns() {
  awk -F'"' '/^exclude_filter="/ { print $2 }' export_presets.cfg \
    | tr ',' '\n' \
    | sed '/^$/d'
}

declare -a EXCLUDE_PATTERNS=()
while IFS= read -r pattern; do
  EXCLUDE_PATTERNS+=("$pattern")
done < <(load_exclude_patterns)

matches_exclude_pattern() {
  local path="$1"
  local pattern
  for pattern in "${EXCLUDE_PATTERNS[@]}"; do
    if [[ "$path" == $pattern ]]; then
      return 0
    fi
  done

  return 1
}

collect_autoload_export_conflicts() {
  local line name value rel output=""
  while IFS= read -r line; do
    [ -z "$line" ] && continue
	name="${line%%=*}"
	case "$name" in
	  MCPScreenshot|MCPInputService|MCPGameInspector) continue ;;
	esac
	value="$(printf '%s\n' "$line" | awk -F'"' '{ print $2 }')"
    case "$value" in
      \*res://*) rel="${value#\*res://}" ;;
      res://*) rel="${value#res://}" ;;
      *) continue ;;
    esac

    if matches_exclude_pattern "$rel"; then
      output+="$name -> $rel"$'\n'
    fi
  done < <(awk '
    BEGIN { in_autoload = 0 }
    /^\[autoload\]/ { in_autoload = 1; next }
    /^\[/ && in_autoload { exit }
    in_autoload && /=/ { print }
  ' project.godot)

  printf '%s' "$output"
}

collect_runtime_export_excluded_resource_refs() {
  rg -n --no-heading \
    --glob '*.cs' \
    --glob '*.gd' \
    --glob '*.tscn' \
    --glob '*.tres' \
    --glob '*.godot' \
    --glob '!scripts/GameBoardScene.Debug.*.cs' \
    --glob '!scripts/dev_mcp/**' \
    'res://(generated-images/|tmp/|outputs/|current_screen[^"[:space:]]*|art/[^"[:space:]]+/(raw|generated)/|art/[^"[:space:]]+/(raw-sheet(-clean)?|sheet-transparent)\.(png|jpg|jpeg|webp|svg|bmp|tga|gif))' \
    scripts scenes resources project.godot 2>/dev/null || true
}

collect_missing_tracked_manifest_image_refs() {
  python3 - <<'PY'
import json
from pathlib import Path
import re
import subprocess

root = Path.cwd()
tracked = subprocess.check_output(
    ["git", "ls-files", "-z", "--", "art", "asset", "assets", "resources"],
    cwd=root,
).decode("utf-8").split("\0")
image_extensions = {".png", ".jpg", ".jpeg", ".webp", ".svg", ".bmp", ".tga", ".gif"}


def strings(value):
    if isinstance(value, str):
        yield value
    elif isinstance(value, list):
        for item in value:
            yield from strings(item)
    elif isinstance(value, dict):
        for item in value.values():
            yield from strings(item)


for relative in tracked:
    if not relative.endswith(".json"):
        continue

    path = root / relative
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, UnicodeDecodeError, json.JSONDecodeError):
        continue

    for value in strings(data):
        if not value.startswith("res://"):
            continue

        resource_path = value.removeprefix("res://")
        if Path(resource_path).suffix.lower() not in image_extensions:
            continue
        if "{" in resource_path and "}" in resource_path:
            glob_path = re.sub(r"\{[^{}]+\}", "*", resource_path)
            if any(root.glob(glob_path)):
                continue
        if not (root / resource_path).is_file():
            print(f"{relative} -> {value}")
PY
}

collect_missing_export_excludes() {
  local required pattern output=""
	local -a required_patterns=(
	  "addons/godot_mcp/**"
	  "scripts/dev_mcp/**"
	  "tmp/**"
	  "generated-images/**"
	  "current_screen*"
	  "addons/adventure_suit_upgrades/**"
	  "scenes/adventure_suit_upgrade_editor_scene.tscn"
	  "scripts/GameBoardScene.Debug.*.cs*"
	  "tests/**"
	  "tools/**"
	  "docs/**"
	  "samples/**"
	  "outputs/**"
	  "Library/**"
	  "**/*.debug-sse.log"
	  "art/**/raw/**"
    "art/**/generated/**"
    "art/**/raw-sheet.png"
    "art/**/raw-sheet.png.import"
    "art/**/raw-sheet-clean.png"
    "art/**/raw-sheet-clean.png.import"
    "art/**/sheet-transparent.png"
    "art/**/sheet-transparent.png.import"
	)

  for required in "${required_patterns[@]}"; do
    local found=0
    for pattern in "${EXCLUDE_PATTERNS[@]}"; do
      if [ "$pattern" = "$required" ]; then
        found=1
        break
      fi
    done

    if [ "$found" -eq 0 ]; then
      output+="$required"$'\n'
    fi
  done

  printf '%s' "$output"
}

collect_missing_gitignore_patterns() {
  local output=""
  local -a required_patterns=(
    "/art/**/raw/"
    "/art/**/generated/"
    "/art/**/raw-sheet.png"
    "/art/**/raw-sheet.png.import"
    "/art/**/raw-sheet-clean.png"
    "/art/**/raw-sheet-clean.png.import"
    "/art/**/sheet-transparent.png"
    "/art/**/sheet-transparent.png.import"
    "/generated-images/upgrade-popup-assets/*/raw/"
    "/generated-images/upgrade-popup-assets/transparent-processing-meta.json"
    "/generated-images/upgrade-popup-assets/*.png"
    "/generated-images/upgrade-popup-assets/*.png.import"
  )

  for required in "${required_patterns[@]}"; do
    if ! grep -Fqx "$required" .gitignore; then
      output+="$required"$'\n'
    fi
  done

  printf '%s' "$output"
}

print_section "Category Summary"
while IFS=$'\t' read -r category count; do
  printf '%s: %s\n' "$category" "$count"
done < <(print_category_summary)

print_section "Tracked Generated And Output Assets"
GENERATED_OUTPUT_MATCHES="$(collect_tracked_generated_output_risks)"
if [ -n "$(printf '%s' "$GENERATED_OUTPUT_MATCHES" | sed '/^$/d')" ]; then
  print_risk "git-tracked generated-images/** or outputs/** files outside runtime allowlist ($(count_non_empty_lines "$GENERATED_OUTPUT_MATCHES") tracked)"
  print_limited_list "$GENERATED_OUTPUT_MATCHES"
else
  print_ok "git-tracked generated-images/** or outputs/** files outside runtime allowlist"
fi

print_section "Tracked Debug Logs"
run_pathspec_check "git-tracked *.debug-sse.log files" "*.debug-sse.log"

print_section "Tracked Raw Or Source Art"
RAW_SOURCE_MATCHES="$(collect_tracked_raw_source_risks)"
if [ -n "$(printf '%s' "$RAW_SOURCE_MATCHES" | sed '/^$/d')" ]; then
  print_risk "git-tracked raw/source large images or source-art files ($(count_non_empty_lines "$RAW_SOURCE_MATCHES") tracked)"
  print_limited_list "$RAW_SOURCE_MATCHES"
else
  print_ok "git-tracked raw/source large images or source-art files"
fi

print_section "Runtime References Into Export-Excluded Paths"
RUNTIME_EXCLUDED_RESOURCE_REFS="$(collect_runtime_export_excluded_resource_refs)"
if [ -n "$(printf '%s' "$RUNTIME_EXCLUDED_RESOURCE_REFS" | sed '/^$/d')" ]; then
  print_risk "runtime project files reference export-excluded resources ($(count_non_empty_lines "$RUNTIME_EXCLUDED_RESOURCE_REFS") references)"
  print_limited_list "$RUNTIME_EXCLUDED_RESOURCE_REFS"
else
  print_ok "runtime project files reference export-excluded resources"
fi

print_section "Missing Tracked Manifest Image References"
MISSING_MANIFEST_IMAGE_REFS="$(collect_missing_tracked_manifest_image_refs)"
if [ -n "$(printf '%s' "$MISSING_MANIFEST_IMAGE_REFS" | sed '/^$/d')" ]; then
  print_risk "tracked JSON manifests reference missing images ($(count_non_empty_lines "$MISSING_MANIFEST_IMAGE_REFS") references)"
  print_limited_list "$MISSING_MANIFEST_IMAGE_REFS"
else
  print_ok "tracked JSON manifests reference missing images"
fi

print_section "Autoload Export Conflicts"
AUTOLOAD_CONFLICTS="$(collect_autoload_export_conflicts)"
if [ -n "$(printf '%s' "$AUTOLOAD_CONFLICTS" | sed '/^$/d')" ]; then
  print_risk "project.godot autoload points at export exclude_filter paths ($(count_non_empty_lines "$AUTOLOAD_CONFLICTS") conflicts)"
  print_limited_list "$AUTOLOAD_CONFLICTS"
else
  print_ok "project.godot autoload points at export exclude_filter paths"
fi

print_section "Export Preset Coverage"
MISSING_EXCLUDES="$(collect_missing_export_excludes)"
if [ -n "$(printf '%s' "$MISSING_EXCLUDES" | sed '/^$/d')" ]; then
  print_risk "export_presets.cfg exclude_filter is missing expected generated/debug excludes ($(count_non_empty_lines "$MISSING_EXCLUDES") missing)"
  print_limited_list "$MISSING_EXCLUDES"
else
	print_ok "export_presets.cfg exclude_filter covers generated/debug excludes"
fi

if python3 tools/release_export.py --static-audit; then
	print_ok "all release presets satisfy release-package policy"
else
	print_risk "one or more release presets violate release-package policy"
fi

print_section "Gitignore Coverage"
MISSING_GITIGNORE_PATTERNS="$(collect_missing_gitignore_patterns)"
if [ -n "$(printf '%s' "$MISSING_GITIGNORE_PATTERNS" | sed '/^$/d')" ]; then
  print_risk ".gitignore is missing source-only art ignore rules ($(count_non_empty_lines "$MISSING_GITIGNORE_PATTERNS") missing)"
  print_limited_list "$MISSING_GITIGNORE_PATTERNS"
else
  print_ok ".gitignore covers source-only art ignore rules"
fi

print_section "Summary"
if [ "$FAILED_CHECKS" -gt 0 ]; then
  printf 'Resource/export audit found %d risk check(s).\n' "$FAILED_CHECKS"
  exit 1
fi

printf 'Resource/export audit found no risk checks.\n'
