shader_type canvas_item;

uniform float sun_pulse_speed : hint_range(0.0, 2.0) = 0.48;
uniform float board_exposure : hint_range(0.75, 1.35) = 1.0;
uniform float board_midtone_lift : hint_range(0.0, 0.6) = 0.0;
uniform float sun_brightness : hint_range(0.0, 0.8) = 0.32;
uniform float sun_shadow : hint_range(0.0, 0.6) = 0.22;
uniform float sun_drift : hint_range(0.0, 0.08) = 0.018;
uniform float center_line_strength : hint_range(0.0, 1.0) = 0.34;
uniform float center_line_speed : hint_range(0.0, 3.0) = 0.72;
uniform float center_warp_strength : hint_range(0.0, 0.02) = 0.004;
uniform vec4 center_light_color : source_color = vec4(0.64, 1.0, 0.86, 1.0);

float top_sun_mask(vec2 uv, float side, float t) {
	float x = side < 0.0 ? uv.x : 1.0 - uv.x;
	float drift = sin(t * 0.17 + side * 2.2) * sun_drift;
	float top_fade = 1.0 - smoothstep(0.03, 0.34, uv.y);
	float corner_fade = 1.0 - smoothstep(0.06, 0.42, x);
	float broad = 1.0 - smoothstep(0.0, 1.0, length(vec2(x / 0.30, uv.y / 0.24)));
	float beam_a = 1.0 - smoothstep(0.018, 0.095, abs(x - (0.050 + uv.y * 0.33 + drift)));
	float beam_b = 1.0 - smoothstep(0.016, 0.078, abs(x - (0.115 + uv.y * 0.24 - drift * 0.7)));
	float beam_c = 1.0 - smoothstep(0.014, 0.066, abs(x - (0.180 + uv.y * 0.15 + drift * 0.45)));
	return clamp((beam_a * 0.60 + beam_b * 0.46 + beam_c * 0.32 + broad * 0.42) * top_fade * corner_fade, 0.0, 1.0);
}

float center_mask(vec2 uv) {
	vec2 center_shape = (uv - vec2(0.50, 0.455)) / vec2(0.34, 0.255);
	return 1.0 - smoothstep(0.62, 1.04, length(center_shape));
}

float organic_center_caustics(vec2 uv, float t) {
	vec2 p = (uv - vec2(0.50, 0.455)) / vec2(0.34, 0.255);
	p += vec2(
		sin(p.y * 3.8 + t * 0.75) * 0.075 + sin((p.x + p.y) * 2.1 - t * 0.42) * 0.045,
		cos(p.x * 3.4 - t * 0.68) * 0.070 + sin((p.x - p.y) * 2.6 + t * 0.36) * 0.040);

	float a = sin(p.x * 14.0 + sin(p.y * 8.0 + t * 0.95) * 1.9 + t * 0.88);
	float b = sin(p.y * 13.0 + cos(p.x * 7.0 - t * 0.80) * 1.7 - t * 0.78);
	float c = sin((p.x + p.y) * 10.5 + sin((p.x - p.y) * 6.0 + t * 0.55) * 1.6 + t * 0.44);
	float d = sin(length(p + vec2(sin(t * 0.31), cos(t * 0.27)) * 0.18) * 18.0 - t * 1.10);
	float caustic = pow(max((a + b + c + d) * 0.24, 0.0), 2.7);
	float soft_lace = pow(max((a * b + c * 0.55) * 0.34, 0.0), 2.2);
	return clamp(caustic * 0.80 + soft_lace * 0.45, 0.0, 1.0);
}

void fragment() {
	float t = TIME;
	vec2 uv = UV;
	float center = center_mask(uv);
	vec2 warped_uv = uv + center * center_warp_strength * vec2(
		sin(uv.y * 16.0 + t * center_line_speed),
		cos(uv.x * 14.0 - t * center_line_speed * 0.82));
	vec4 base = texture(TEXTURE, clamp(warped_uv, vec2(0.0), vec2(1.0))) * COLOR;
	base.rgb *= board_exposure;
	base.rgb = mix(base.rgb, sqrt(clamp(base.rgb, vec3(0.0), vec3(1.0))), board_midtone_lift);

	float sun_mask = clamp(top_sun_mask(uv, -1.0, t) + top_sun_mask(uv, 1.0, t), 0.0, 1.0);
	float sun_pulse = 0.5 + 0.5 * sin(t * sun_pulse_speed + sin(t * 0.21) * 0.55);
	float sun_lift = sun_mask * sun_brightness * (0.30 + sun_pulse * 0.70);
	float sun_dim = sun_mask * sun_shadow * pow(1.0 - sun_pulse, 1.35);
	base.rgb = base.rgb * (1.0 + sun_lift) * (1.0 - sun_dim);

	float center_lines = organic_center_caustics(uv, t * center_line_speed) * center;
	float center_pulse = 0.72 + 0.28 * sin(t * 0.74);
	base.rgb += center_light_color.rgb * center_lines * center_line_strength * center_pulse;

	COLOR = vec4(clamp(base.rgb, vec3(0.0), vec3(1.0)), base.a);
}
