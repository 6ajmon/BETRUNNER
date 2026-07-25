using Godot;
using System;

public partial class SettingsOverlay : Control
{
	[Export] private Button _backButton;
	[Export] private HSlider _difficultySlider;
	[Export] private RichTextLabel _difficultyNameLabel;

	private static readonly string[] _difficultyNames = { "Noob", "Pro", "Dev" };

	/// <summary>
	/// Invoked when the Back button is pressed. MainMenuOverlay hooks into this
	/// to show itself and hide SettingsOverlay.
	/// </summary>
	public Action OnBackClicked { get; set; }

	public override void _Ready()
	{
		if (_backButton != null)
			_backButton.Pressed += OnBackPressed;

		// ── Load saved difficulty into slider ───────────────────────
		int saved = (int)CountdownManager.Instance.CurrentDifficulty;
		if (_difficultySlider != null)
		{
			_difficultySlider.Value = saved;
			_difficultySlider.ValueChanged += OnDifficultyChanged;
		}

		UpdateDifficultyLabel(saved);
	}

	public override void _ExitTree()
	{
		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;

		if (_difficultySlider != null)
			_difficultySlider.ValueChanged -= OnDifficultyChanged;
	}

	private void OnDifficultyChanged(double value)
	{
		int diff = Math.Clamp((int)Math.Round(value), 0, 2);
		CountdownManager.Instance.CurrentDifficulty = (CountdownManager.Difficulty)diff;
		UpdateDifficultyLabel(diff);

		// Snap slider to integer
		if (_difficultySlider != null)
			_difficultySlider.Value = diff;
	}

	private void UpdateDifficultyLabel(int diff)
	{
		if (_difficultyNameLabel != null)
			_difficultyNameLabel.Text = _difficultyNames[diff];
	}

	private void OnBackPressed()
	{
		OnBackClicked?.Invoke();
	}
}
