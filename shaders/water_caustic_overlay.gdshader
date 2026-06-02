shader_type canvas_item;

uniform vec4 shimmer_color : source_color = vec4(0.45, 0.95, 1.0, 0.18);
uniform float shimmer_strength : hint_range(0.0, 1.0) = 0.28;
uniform float wave_scale : hint_range(1.0, 80.0) = 22.0;
uniform float wave_speed : hint_range(0.0, 4.0) = 0.45;
uniform float edge_boost : hint_range(0.0, 2.0) = 0.7;
uniform float center_glow : hint_range(0.0, 1.0) = 0.18;
uniform vec4 sun_color : source_color = vec4(1.0, 0.92, 0.68, 0.22);
uniform float sun_pulse_strength : hint_range(0.0, 1.0) = 0.0;
uniform float sun_shadow_strength : hint_range(0.0, 0.5) = 0.0;
uniform float sun_pulse_speed : hint_range(0.0, 2.0) = 0.42;
uniform float center_motion_strength : hint_range(0.0, 1.0) = 0.0;
uniform float center_motion_speed : hint_range(0.0, 3.0) = 0.55;

float sunbeam_mask(vec2 uv, float side) {
	float x = side < 0.0 ? uv.x : 1.0 - uv.x;
	float top_fade = 1.0 - smoothstep(0.04, 0.36, uv.y);
	float corner_fade = 1.0 - smoothstep(0.08, 0.43, x);
	float beam_a = 1.0 - smoothstep(0.020, 0.105, abs(x - (0.055 + uv.y * 0.26)));
	float beam_b = 1.0 - smoothstep(0.018, 0.092, abs(x - (0.125 + uv.y * 0.19)));
	float beam_c = 1.0 - smoothstep(0.016, 0.080, abs(x - (0.190 + uv.y * 0.12)));
	float corner_glow = 1.0 - smoothstep(0.0, 1.0, length(vec2(x / 0.28, uv.y / 0.24)));
	return clamp((beam_a * 0.55 + beam_b * 0.38 + beam_c * 0.24 + corner_glow * 0.32) * top_fade * corner_fade, 0.0, 1.0);
}

void fragment() {
	vec2 uv = UV;
	float t = TIME * wave_speed;

	vec2 drift_a = uv + vec2(sin(uv.y * 8.0 + t) * 0.018, cos(uv.x * 7.0 - t * 0.8) * 0.014);
	vec2 drift_b = uv + vec2(cos(uv.y * 5.5 - t * 1.2) * 0.015, sin(uv.x * 6.5 + t) * 0.012);
	float wave_a = sin((drift_a.x * wave_scale + t * 1.15) + sin(drift_a.y * 12.0 + t * 0.7));
	float wave_b = sin((drift_b.y * wave_scale * 0.82 - t * 1.35) + sin(drift_b.x * 9.0 - t));
	float wave_c = sin((drift_a.x + drift_b.y) * wave_scale * 0.55 + t * 0.62);
	float caustic = pow(max((wave_a + wave_b + wave_c) * 0.34, 0.0), 2.65);

	float edge_distance = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
	float edge_mask = 1.0 - smoothstep(0.04, 0.32, edge_distance);
	float center_mask = smoothstep(0.08, 0.22, edge_distance);
	float mask = clamp(center_mask * center_glow + edge_mask * edge_boost, 0.0, 1.0);
	float caustic_alpha = caustic * shimmer_strength * shimmer_color.a * mask;

	float center_t = TIME * center_motion_speed;
	vec2 center_uv = uv - vec2(0.5, 0.5);
	vec2 center_shape = center_uv / vec2(0.34, 0.27);
	float center_area = 1.0 - smoothstep(0.58, 1.03, length(center_shape));
	vec2 flow_uv = uv + vec2(
		sin(uv.y * 8.0 + center_t) * 0.026,
		cos(uv.x * 7.0 - center_t * 0.9) * 0.020);
	float fine_a = sin((flow_uv.x * 58.0 + flow_uv.y * 23.0) + center_t * 1.65);
	float fine_b = sin((flow_uv.y * 64.0 - flow_uv.x * 19.0) - center_t * 1.32);
	float center_flow = pow(max(fine_a * fine_b, 0.0), 3.2);
	float center_alpha = center_flow * center_area * center_motion_strength * shimmer_color.a * 0.62;

	float sun_mask = clamp(sunbeam_mask(uv, -1.0) + sunbeam_mask(uv, 1.0), 0.0, 1.0);
	float sun_pulse = 0.5 + 0.5 * sin(TIME * sun_pulse_speed + sin(TIME * 0.19) * 0.45);
	float sun_alpha = sun_mask * sun_pulse_strength * sun_color.a * (0.42 + sun_pulse * 0.58);
	float shade_alpha = sun_mask * sun_shadow_strength * pow(1.0 - sun_pulse, 1.4);

	float alpha = clamp(caustic_alpha + center_alpha + sun_alpha + shade_alpha, 0.0, 0.78);
	vec3 weighted_color =
		shimmer_color.rgb * caustic_alpha
		+ shimmer_color.rgb * center_alpha
		+ sun_color.rgb * sun_alpha
		+ vec3(0.02, 0.16, 0.21) * shade_alpha;
	vec3 color = alpha > 0.0 ? weighted_color / alpha : shimmer_color.rgb;
	COLOR = vec4(color, alpha);
}
