#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export DECKLAND_DISABLE_STEAM=1
exec "$ROOT_DIR/tests/run_runtime_architecture_smoke_case.sh" res://tests/NoSteamPlatformSmoke.gd NO_STEAM_PLATFORM_SMOKE_OK
