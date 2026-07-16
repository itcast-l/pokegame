shader_type canvas_item;

uniform float vertical_compression : hint_range(0.5, 1.0) = 0.96;
uniform float top_width_ratio : hint_range(0.5, 1.0) = 0.97;
uniform float bottom_width_ratio : hint_range(0.5, 1.2) = 1.0;
uniform float plane_rotation_degrees : hint_range(-180.0, 180.0) = 0.0;
uniform vec2 rect_size = vec2(264.0, 402.0);

void vertex() {
	vec2 safe_size = max(rect_size, vec2(1.0));
	float vertical_ratio = clamp(VERTEX.y / safe_size.y, 0.0, 1.0);
	float width_ratio = mix(top_width_ratio, bottom_width_ratio, vertical_ratio);
	vec2 centered = VERTEX - safe_size * 0.5;
	centered.x *= width_ratio;
	centered.y *= vertical_compression;

	float angle = radians(plane_rotation_degrees);
	mat2 rotation = mat2(vec2(cos(angle), -sin(angle)), vec2(sin(angle), cos(angle)));
	VERTEX = rotation * centered + safe_size * 0.5;
}

void fragment() {
	vec4 vertex_color = COLOR;
	vec4 texel = texture(TEXTURE, UV);
	float purple_signal = min(texel.r, texel.b) - texel.g;
	float red_blue_balance = 1.0 - abs(texel.r - texel.b);
	float magenta_spill = smoothstep(0.08, 0.24, purple_signal)
		* smoothstep(0.5, 0.88, red_blue_balance);
	texel.a *= 1.0 - magenta_spill;
	COLOR = texel * vertex_color;
}
