using Godot;
using System;

public partial class BettingOverlay : Control
{
	[Export] private Slider _betSlider;
	[Export] private Label _levelTimeLabel;
	[Export] private Label _totalTimeLabel;
	[Export] private Label _sliderMinLabel;
	[Export] private Label _sliderMaxLabel;
	[Export] private Label _sliderValueLabel;

	public override void _Ready()
	{
		// Aktualizuj etykiety gdy CountdownManager przydzieli czas
		CountdownManager.Instance.LevelTimeAllocated += OnLevelTimeAllocated;

		if (_betSlider != null)
		{
			_betSlider.ValueChanged += OnBetSliderValueChanged;
		}
	}

	public override void _ExitTree()
	{
		if (CountdownManager.Instance != null)
		{
			CountdownManager.Instance.LevelTimeAllocated -= OnLevelTimeAllocated;
		}

		if (_betSlider != null)
		{
			_betSlider.ValueChanged -= OnBetSliderValueChanged;
		}
	}

	private void OnLevelTimeAllocated(double levelBaseTime, double totalAvailableTime)
	{
		if (_levelTimeLabel != null)
			_levelTimeLabel.Text = $"Level time: {levelBaseTime:F1}s";

		if (_totalTimeLabel != null)
			_totalTimeLabel.Text = $"Total pool: {totalAvailableTime:F1}s";

		// Ustaw zakres suwaka
		if (_betSlider != null)
		{
			_betSlider.MinValue = 0;
			_betSlider.MaxValue = totalAvailableTime;
			_betSlider.Value = totalAvailableTime * 0.5; // domyślnie połowa
			CountdownManager.Instance.PendingBet = _betSlider.Value;

			// Aktualizuj etykiety zakresu
			UpdateSliderLabels(_betSlider.Value);
		}
	}

	private void OnBetSliderValueChanged(double value)
	{
		CountdownManager.Instance.PendingBet = value;
		UpdateSliderLabels(value);
	}

	private void UpdateSliderLabels(double value)
	{
		if (_sliderMinLabel != null && _betSlider != null)
			_sliderMinLabel.Text = $"{_betSlider.MinValue:F0}s";

		if (_sliderMaxLabel != null && _betSlider != null)
			_sliderMaxLabel.Text = $"{_betSlider.MaxValue:F0}s";

		if (_sliderValueLabel != null)
			_sliderValueLabel.Text = $"{value:F1}s";
	}

	// ── Progress bar helpers ────────────────────────────────────────────────
	/// <summary>Current bet value (for ProgressBar.Value).</summary>
	public double BetProgress => _betSlider?.Value ?? 0.0;
	/// <summary>Maximum bet value (for ProgressBar.MaxValue).</summary>
	public double MaxBetProgress => _betSlider?.MaxValue ?? 0.0;
	/// <summary>Ratio 0..1 of the current bet relative to max available.</summary>
	public double BetRatio => MaxBetProgress > 0.0 ? BetProgress / MaxBetProgress : 0.0;

	/// <summary>
	/// Wywoływane przez przycisk Bet w edytorze (lub z .tscn).
	/// </summary>
	public void _on_bet_button_pressed()
	{
		if (_betSlider != null)
		{
			CountdownManager.Instance.PendingBet = _betSlider.Value;
		}
		GameManager.Instance.EmitSignal(nameof(GameManager.EndBettingPhase));
	}
}
