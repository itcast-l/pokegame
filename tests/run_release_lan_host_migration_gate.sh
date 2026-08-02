#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ "$#" -ne 3 ]]; then
	echo "Usage: $0 <release-audit.json> <target-runtime-evidence.json> <output-dir>" >&2
	exit 2
fi

DECKLAND_TEST_MUTE_AUDIO=1 python3 "$ROOT_DIR/tools/run_release_lan_host_migration_gate.py" \
	"$1" \
	"$2" \
	--output-dir "$3"
