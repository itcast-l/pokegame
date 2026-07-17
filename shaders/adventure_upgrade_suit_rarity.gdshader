shader_type canvas_item;

uniform vec4 rarity_color : source_color = vec4(0.94, 0.96, 1.0, 1.0);

void fragment() {
	vec4 texel = texture(TEXTURE, UV);
	float channel_max = max(texel.r, max(texel.g, texel.b));
	float channel_min = min(texel.r, min(texel.g, texel.b));
	float saturation = (channel_max - channel_min) / max(channel_max, 0.001);
	float enamel_mask = smoothstep(0.16, 0.34, saturation) * smoothstep(0.08, 0.24, channel_max);
	float luminance = dot(texel.rgb, vec3(0.299, 0.587, 0.114));
	float enamel_value = mix(0.42, 1.08, smoothstep(0.08, 0.92, luminance));
	vec3 tinted_enamel = rarity_color.rgb * enamel_value;

	float purple_signal = min(texel.r, texel.b) - texel.g;
	float magenta_spill = smoothstep(0.025, 0.14, purple_signal);

	COLOR = vec4(
		mix(texel.rgb, tinted_enamel, enamel_mask),
		texel.a * (1.0 - magenta_spill) * COLOR.a);
}
