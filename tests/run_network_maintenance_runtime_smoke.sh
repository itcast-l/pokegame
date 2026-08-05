#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
exec "$ROOT_DIR/tests/run_runtime_architecture_smoke_case.sh" res://tests/NetworkMaintenanceRuntimeSmoke.gd NETWORK_MAINTENANCE_RUNTIME_SMOKE_OK
