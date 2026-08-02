#!/usr/bin/env bash
set -euo pipefail

ACTION="${1:-}"
PROFILE="${2:-}"
INTERFACE="${CODEX_NETEM_INTERFACE:-}"
TC_BIN="${TC_BIN:-$(command -v tc 2>/dev/null || true)}"

if [[ -z "$TC_BIN" ]]; then
	echo "Missing tc. Install iproute2." >&2
	exit 1
fi
if [[ -z "$INTERFACE" ]]; then
	echo "Missing CODEX_NETEM_INTERFACE." >&2
	exit 1
fi
if [[ "${CODEX_NETEM_ISOLATED_INTERFACE:-0}" != "1" ]]; then
	echo "Refusing to replace a qdisc without CODEX_NETEM_ISOLATED_INTERFACE=1." >&2
	exit 1
fi

run_tc() {
	if [[ "$(id -u)" == "0" ]]; then
		"$TC_BIN" "$@"
		return
	fi

	sudo -n "$TC_BIN" "$@"
}

clear_netem() {
	run_tc qdisc del dev "$INTERFACE" root 2>/dev/null || true
}

case "$ACTION" in
	apply)
		clear_netem
		case "$PROFILE" in
			latency)
			run_tc qdisc add dev "$INTERFACE" root netem delay 350ms 75ms distribution normal
			;;
		packet_loss)
			run_tc qdisc add dev "$INTERFACE" root netem loss random 15% 25%
			;;
		duplicate_packet)
			run_tc qdisc add dev "$INTERFACE" root netem duplicate 20%
			;;
		short_disconnect)
			run_tc qdisc add dev "$INTERFACE" root netem loss 100%
			;;
		*)
			echo "Unknown netem profile: $PROFILE" >&2
			exit 1
			;;
		esac
		"$TC_BIN" qdisc show dev "$INTERFACE"
		;;
	clear)
		clear_netem
		;;
	*)
		echo "Usage: $0 apply <profile> | clear" >&2
		exit 1
		;;
esac
