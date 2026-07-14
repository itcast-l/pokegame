#!/usr/bin/env bash

assert_lan_full_match_result() {
	local result_path="$1"
	local player_count="$2"

	test -s "$result_path"
	local winner
	winner="$(sed -n 's/^winner=//p' "$result_path")"
	[[ "$winner" =~ ^[0-9]+$ ]]
	(( winner >= 0 && winner < player_count ))
	grep -Fq "private_leak=false" "$result_path"
	local fingerprint
	fingerprint="$(sed -n 's/^fingerprint=//p' "$result_path")"
	[[ "$fingerprint" =~ ^[0-9a-f]{64}$ ]]

	local field index value
	for field in table_count; do
		value="$(sed -n "s/^${field}=//p" "$result_path")"
		[[ "$value" =~ ^[0-9]+$ ]]
	done
	for field in score deck hand item_hand; do
		for ((index = 0; index < player_count; index++)); do
			value="$(sed -n "s/^${field}_${index}=//p" "$result_path")"
			[[ "$value" =~ ^[0-9]+$ ]]
		done
	done
}

assert_lan_full_match_public_state_equal() {
	local expected_path="$1"
	local actual_path="$2"
	local player_count="$3"
	local field index expected actual

	for field in winner table_count; do
		expected="$(sed -n "s/^${field}=//p" "$expected_path")"
		actual="$(sed -n "s/^${field}=//p" "$actual_path")"
		[[ "$expected" == "$actual" ]]
	done
	for field in score deck hand item_hand; do
		for ((index = 0; index < player_count; index++)); do
			expected="$(sed -n "s/^${field}_${index}=//p" "$expected_path")"
			actual="$(sed -n "s/^${field}_${index}=//p" "$actual_path")"
			[[ "$expected" == "$actual" ]]
		done
	done
}
