#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

python3 tools/audit_adventure_upgrade_visual_resources.py
wc -c scenes/upgrade_choice_popup_asset_scene.tscn
