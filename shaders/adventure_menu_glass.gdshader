shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float corner_radius : hint_range(0.0, 32.0) = 8.0;
uniform float blur_lod : hint_range(0.0, 6.0) = 2.8;
uniform vec4 tint_color : source_color = vec4(0.004, 0.12, 0.15, 0.68);
uniform vec4 edge_light_color : source_color = vec4(0.52, 1.0, 0.94, 0.12);
uniform float surface_opacity : hint_range(0.0, 1.0) = 0.92;

float rounded_panel_mask(vec2 uv) {
	vec2 local_size = 1.0 / max(fwidth(uv), vec2(0.000001));
	vec2 half_size = local_size * 0.5;
	vec2 pixel = uv * local_size;
	vec2 inset = max(half_size - vec2(corner_radius), vec2(0.0));
	vec2 distance_to_edge = abs(pixel - half_size) - inset;
	float distance = length(max(distance_to_edge, vec2(0.0)))
		+ min(max(distance_to_edge.x, distance_to_edge.y), 0.0)
		- corner_radius;
	float antialias = max(fwidth(distance), 0.75);
	return 1.0 - smoothstep(-antialias, antialias, distance);
}

void fragment() {
	vec3 blurred = textureLod(screen_texture, SCREEN_UV, blur_lod).rgb;
	vec3 glass = mix(blurred, tint_color.rgb, tint_color.a);
	float top_edge = pow(1.0 - UV.y, 5.0);
	glass += edge_light_color.rgb * edge_light_color.a * top_edge;
	COLOR = vec4(glass, surface_opacity * rounded_panel_mask(UV));
}
