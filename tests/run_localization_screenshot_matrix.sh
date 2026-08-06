#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TIMESTAMP="$(date +%Y%m%d-%H%M%S)"
export CODEX_LOCALIZATION_SCREENSHOT_DIR="${CODEX_LOCALIZATION_SCREENSHOT_DIR:-$ROOT_DIR/outputs/localization_screenshots/matrix-$TIMESTAMP}"

bash "$ROOT_DIR/tests/run_localization_screenshot_audit.sh"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_OUTPUT %s\n' "$CODEX_LOCALIZATION_SCREENSHOT_DIR"
printf 'LOCALIZATION_SCREENSHOT_MATRIX_OK\n'
