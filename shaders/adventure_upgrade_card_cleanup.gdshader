shader_type canvas_item;

void fragment() {
	vec4 texel = texture(TEXTURE, UV);
	float purple_signal = min(texel.r, texel.b) - texel.g;
	float magenta_spill = smoothstep(0.025, 0.14, purple_signal);
	COLOR = vec4(texel.rgb, texel.a * (1.0 - magenta_spill)) * COLOR;
}
