#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESOURCE_PATH="$ROOT_DIR/art/maps/underwater_card_table_vertical_backdrop_1920x1080.png"

if [[ ! -f "$RESOURCE_PATH" ]]; then
	echo "Missing resource: $RESOURCE_PATH" >&2
	exit 1
fi

IMAGE_INFO="$(file "$RESOURCE_PATH")"
echo "$IMAGE_INFO"
grep -Fq "1920 x 1080" <<<"$IMAGE_INFO" || {
	echo "Vertical backdrop resource must be an actual 1920x1080 PNG." >&2
	exit 1
}
