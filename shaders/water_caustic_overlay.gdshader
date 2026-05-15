shader_type canvas_item;

uniform vec4 shimmer_color : source_color = vec4(0.45, 0.95, 1.0, 0.18);
uniform float shimmer_strength : hint_range(0.0, 1.0) = 0.28;
uniform float wave_scale : hint_range(1.0, 80.0) = 22.0;
uniform float wave_speed : hint_range(0.0, 4.0) = 0.45;
uniform float edge_boost : hint_range(0.0, 2.0) = 0.7;
uniform float center_glow : hint_range(0.0, 1.0) = 0.18;

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
	float alpha = caustic * shimmer_strength * shimmer_color.a * mask;
	COLOR = vec4(shimmer_color.rgb, alpha);
}
