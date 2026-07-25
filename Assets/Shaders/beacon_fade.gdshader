shader_type spatial;
render_mode blend_mix, depth_draw_opaque;

/// Beacon Fade Shader
/// Fades the top and bottom of a tall pillar/beacon mesh to transparency,
/// so only the middle portion is visible — making it appear shorter
/// without actually modifying the mesh geometry.
///
/// Supports albedo texture + emission (self-illumination glow).
/// Assign as the main material on MeshInstance3D — works standalone.
///
/// Controls:
/// - texture_albedo:     base color texture (assign same as original material)
/// - texture_emission:   emission map (assign same as original material)
/// - emission_energy:    emission brightness multiplier
/// - bottom_fade:        fraction of height to fade from bottom (0.0–1.0)
/// - top_fade:           fraction of height to fade from top (0.0–1.0)
/// - softness:           smoothness of the transition edges
/// - use_vertex_y:       switch to VERTEX.y mode if UV doesn't follow height
///   (also set mesh_height to the mesh's actual Y extent)

// ── Textures ──────────────────────────────────────────────────────────────
uniform sampler2D texture_albedo : source_color;
uniform sampler2D texture_emission : source_color;

// ── Emission ──────────────────────────────────────────────────────────────
uniform float emission_energy : hint_range(0.0, 10.0) = 1.0;
uniform vec4 emission_color : source_color = vec4(1.0);

// ── Fade controls ─────────────────────────────────────────────────────────
uniform float bottom_fade : hint_range(0.0, 1.0) = 0.3;
uniform float top_fade : hint_range(0.0, 1.0) = 0.3;
uniform float softness : hint_range(0.0, 0.5) = 0.1;

// ── Vertex mode fallback ──────────────────────────────────────────────────
uniform bool use_vertex_y = false;
uniform float mesh_height : hint_range(0.1, 100.0) = 10.0;

varying float height_pos;

void vertex() {
	if (use_vertex_y) {
		height_pos = (VERTEX.y + mesh_height * 0.5) / mesh_height;
	} else {
		height_pos = UV.y;
	}
}

void fragment() {
	float h = height_pos;

	// ── Alpha fade ───────────────────────────────────────────────────────
	float bottom_end = bottom_fade + softness;
	float bottom_alpha = smoothstep(0.0, bottom_end, h);
	float top_start = 1.0 - top_fade - softness;
	float top_alpha = 1.0 - smoothstep(top_start, 1.0, h);
	float alpha = min(bottom_alpha, top_alpha);

	// ── Albedo ───────────────────────────────────────────────────────────
	vec4 albedo_tex = texture(texture_albedo, UV);
	ALBEDO = albedo_tex.rgb * alpha;

	// ── Emission ─────────────────────────────────────────────────────────
	vec4 emission_tex = texture(texture_emission, UV);
	EMISSION = emission_tex.rgb * emission_color.rgb * emission_energy;

	// ── Alpha ────────────────────────────────────────────────────────────
	ALPHA = alpha;
}
