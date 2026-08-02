#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

python3 tests/test_release_export.py
python3 tests/test_release_target_runtime.py
python3 tests/test_release_pack_harness.py
python3 tests/test_host_migration_release_evidence.py
