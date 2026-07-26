using Godot;

/// <summary>
/// Sound-effect library — assign audio clips in the Inspector under AudioManager,
/// then play them via <see cref="AudioManager"/> convenience methods.
/// </summary>
public partial class SfxLibrary : Node
{
	// ── UI ───────────────────────────────────────────────────────────────
	[ExportGroup("UI")]
	[Export] public AudioStream UIFocusChange { get; private set; }
	[Export] public AudioStream UIButtonClick { get; private set; }

	// ── Gameplay ─────────────────────────────────────────────────────────
	[ExportGroup("Gameplay")]
	[Export] public AudioStream FinishLine { get; private set; }
	[Export] public AudioStream LaserBuzz { get; private set; }
	[Export] public AudioStream LaserEnter { get; private set; }

	// ── Player Movement ──────────────────────────────────────────────────
	[ExportGroup("Player Movement")]
	[Export] public AudioStream PlayerFootstep { get; private set; }
	[Export] public AudioStream PlayerJump { get; private set; }
	[Export] public AudioStream PlayerLand { get; private set; }

	// ── Timer / Countdown ────────────────────────────────────────────────
	[ExportGroup("Timer / Countdown")]
	[Export] public AudioStream TimerBetTick { get; private set; }
	[Export] public AudioStream TimerLimitWarning { get; private set; }
	[Export] public AudioStream TimerLimitEnd { get; private set; }
}
