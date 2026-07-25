shader_type canvas_item;

uniform float effect_power : hint_range(0.0, 1.0) = 0.0;
uniform float barrel_strength : hint_range(0.0, 1.0) = 0.35;
uniform float blur_intensity : hint_range(0.0, 1.0) = 0.5;
uniform float vignette_strength : hint_range(0.0, 1.0) = 0.4;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void fragment() {
	vec2 center = vec2(0.5);
	vec2 dir = UV - center;
	float dist = length(dir);
	float dist_sq = dist * dist;

	// Smooth intensity curve — łagodne narastanie
	float intensity = smoothstep(0.0, 0.8, effect_power);
	intensity = intensity * intensity; // krzywa kwadratowa dla płynności

	// ── 1. Barrel distortion ────────────────────────────────────────
	float barrel = 1.0 + dist_sq * barrel_strength * intensity * 0.8;
	vec2 barrel_uv = center + dir * barrel;

	// ── 2. Radial blur (okrągły, rozmycie wzdłuż promienia) ─────────
	vec4 color = vec4(0.0);
	int samples = 10;
	float blur_amount = blur_intensity * intensity * 0.015;

	for (int i = 0; i < samples; i++) {
		float fi = float(i) / float(samples - 1);
		vec2 blur_uv = barrel_uv + dir * fi * blur_amount;
		color += texture(SCREEN_TEXTURE, blur_uv);
	}
	color /= float(samples);

	// ── 3. Vignette (okrągły) ───────────────────────────────────────
	float vig = 1.0 - pow(dist, 3.0) * vignette_strength * intensity;
	color.rgb *= clamp(vig, 0.0, 1.0);

	COLOR = color;
}