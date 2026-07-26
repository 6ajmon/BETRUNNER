using Godot;
using System;

/// <summary>
/// Wires up Button focus-change and click sounds for UI overlays.
/// Call <see cref="Wire(Button)"/> for each Button in an overlay's _Ready().
/// </summary>
public static class ButtonSoundHelper
{
	private const string MetaKey = "__sfx_wired";
	private const string SliderMetaKey = "__sfx_slider_wired";

	/// <summary>
	/// Connect <c>FocusEntered</c> → <c>PlayUIFocusChange()</c> and
	/// <c>Pressed</c> → <c>PlayUIButtonClick()</c> on the given button.
	/// Safe to call multiple times — the helper uses a metadata flag to
	/// avoid double-wiring.
	/// </summary>
	public static void Wire(Button button)
	{
		if (button == null) return;
		if (button.HasMeta(MetaKey)) return;
		button.SetMeta(MetaKey, true);

		button.FocusEntered += () =>
		{
			if (AudioManager.Instance != null)
				AudioManager.Instance.PlayUIFocusChange();
		};

		button.Pressed += () =>
		{
			if (AudioManager.Instance != null)
				AudioManager.Instance.PlayUIButtonClick();
		};
	}

	/// <summary>
	/// Play a tick sound on each discrete step change of a Slider/HSlider.
	/// Tracks the last step value internally so the sound only fires once
	/// per full step — not on every tiny float change during a drag.
	/// </summary>
	public static void WireSlider(Slider slider)
	{
		if (slider == null) return;
		if (slider.HasMeta(SliderMetaKey)) return;
		slider.SetMeta(SliderMetaKey, true);

		double lastIntValue = Math.Round(slider.Value);

		slider.ValueChanged += (value) =>
		{
			double newInt = Math.Round(value);
			if (Math.Abs(newInt - lastIntValue) >= 0.5)
			{
				lastIntValue = newInt;
				if (AudioManager.Instance != null)
					AudioManager.Instance.PlayUIFocusChange();
			}
		};
	}
}
