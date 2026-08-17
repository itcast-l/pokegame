#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
"$ROOT_DIR/tests/run_runtime_architecture_smoke_case.sh" \
	res://tests/NormalShutdownBarrierSmoke.gd \
	NORMAL_SHUTDOWN_BARRIER_SMOKE_OK
"$ROOT_DIR/tests/run_runtime_architecture_smoke_case.sh" \
	res://tests/NonMainShutdownRoutingSmoke.gd \
	NON_MAIN_SHUTDOWN_ROUTING_SMOKE_OK
