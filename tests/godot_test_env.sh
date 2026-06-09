#!/usr/bin/env bash

codex_setup_godot_test_home() {
	local base_dir="$1"
	local home_dir="$base_dir/home"
	mkdir -p "$home_dir/Library/Application Support" "$home_dir/Library/Logs"
	export HOME="$home_dir"
}
