#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
bash "$ROOT_DIR/tests/run_adventure_upgrade_interaction_smoke_case.sh" \
	res://tests/AdventureUpgradeSnapshotV5MigrationSmoke.gd \
	ADVENTURE_UPGRADE_SNAPSHOT_V5_MIGRATION_SMOKE_OK \
	adventure-upgrade-snapshot-v5-migration
