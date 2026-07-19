#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GODOT_CMD="${GODOT_BIN:-/Volumes/KIOXIA11/Applications/Godot_mono.app/Contents/MacOS/Godot}"
TMP_DIR="$(mktemp -d)"
trap 'rm -rf "$TMP_DIR"' EXIT

HOME="$TMP_DIR/home" "$GODOT_CMD" --headless --path "$ROOT_DIR" \
	--script res://tests/PandoraCompressionUtf8Smoke.gd >"$TMP_DIR/smoke.log" 2>&1

cat "$TMP_DIR/smoke.log"
rg -q "PANDORA_COMPRESSION_UTF8_SMOKE_OK" "$TMP_DIR/smoke.log"
if rg -q "Unicode parsing error|SCRIPT ERROR|ERROR:" "$TMP_DIR/smoke.log"; then
	echo "Pandora UTF-8 smoke emitted a blocking error" >&2
	exit 1
fi
