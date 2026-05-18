shader_type canvas_item;

uniform vec4 depth_color : source_color = vec4(0.0, 0.055, 0.09, 1.0);
uniform float left_alpha : hint_range(0.0, 1.0) = 0.26;
uniform float fade_start : hint_range(0.0, 1.0) = 0.08;
uniform float fade_end : hint_range(0.0, 1.0) = 0.96;

void fragment() {
	float depth = 1.0 - smoothstep(fade_start, fade_end, UV.x);
	float surface_softening = mix(0.86, 1.0, smoothstep(0.18, 0.42, UV.y));
	float alpha = left_alpha * depth * surface_softening;
	COLOR = vec4(depth_color.rgb, alpha);
}
