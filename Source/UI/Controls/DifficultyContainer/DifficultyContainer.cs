using Godot;
using System;

public partial class DifficultyContainer : VBoxContainer
{
	[Export] private RichTextLabel _difficultyLabel;
	[Export] private HSlider _difficultySlider;

	private static readonly string[] _difficultyNames = { "Noob", "Pro", "Dev" };

	/// <summary>Fired when the slider value changes.</summary>
	public event Action<int> DifficultyChanged;

	public override void _Ready()
	{
		if (_difficultySlider != null)
			_difficultySlider.ValueChanged += OnSliderChanged;
	}

	public override void _ExitTree()
	{
		if (_difficultySlider != null)
			_difficultySlider.ValueChanged -= OnSliderChanged;
	}

	/// <summary>Current difficulty index (0 = Noob, 1 = Pro, 2 = Dev).</summary>
	public int Value
	{
		get => _difficultySlider != null ? Math.Clamp((int)Math.Round(_difficultySlider.Value), 0, 2) : 1;
		set
		{
			int clamped = Math.Clamp(value, 0, 2);
			if (_difficultySlider != null)
				_difficultySlider.Value = clamped;
			UpdateLabel(clamped);
		}
	}

	private void OnSliderChanged(double raw)
	{
		int diff = Math.Clamp((int)Math.Round(raw), 0, 2);
		UpdateLabel(diff);
		DifficultyChanged?.Invoke(diff);
	}

	private void UpdateLabel(int diff)
	{
		if (_difficultyLabel != null)
			_difficultyLabel.Text = _difficultyNames[diff];
	}
}
