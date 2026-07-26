using Godot;

namespace BETRUNNER;

/// <summary>
/// Shared color constants for UI consistency across the game.
/// </summary>
public static class UIColors
{
	/// <summary>Bet amount — green (#69f0ae).</summary>
	public static readonly Color Bet     = new Color(0.4117647f, 0.9411765f, 0.68235296f);

	/// <summary>Limit / remaining pool — orange (#ff6e40).</summary>
	public static readonly Color Limit   = new Color(1f, 0.43137255f, 0.2509804f);

	/// <summary>Penalty (same as limit).</summary>
	public static readonly Color Penalty = new Color(0.588f, 0.0f, 0.106f);

	/// <summary>Bonus time from levels — amber/yellow (#ffd740).</summary>
	public static readonly Color Bonus   = new Color(1f, 0.84313726f, 0.2509804f);
}
