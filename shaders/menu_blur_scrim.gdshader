shader_type canvas_item;
render_mode unshaded;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_lod = 2.8;
uniform vec4 tint_color : source_color = vec4(0.0, 0.035, 0.07, 0.48);

void fragment() {
	vec4 blurred = textureLod(screen_texture, SCREEN_UV, blur_lod);
	vec3 tinted = mix(blurred.rgb, tint_color.rgb, tint_color.a);
	COLOR = vec4(tinted, 1.0);
}
