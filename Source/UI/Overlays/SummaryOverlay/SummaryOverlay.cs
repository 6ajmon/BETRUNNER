using Godot;
using System;

public partial class SummaryOverlay : Control
{
	[Export] private Label _betLabel;
	[Export] private Label _timeLabel;
	[Export] private Label _penaltyLabel;
	[Export] private Label _remainingLabel;
	[Export] private Label _calculationLabel;
	[Export] private Button _continueButton;

	public override void _Ready()
	{
		if (_continueButton != null)
			_continueButton.Pressed += OnContinuePressed;
	}

	public override void _ExitTree()
	{
		if (_continueButton != null)
			_continueButton.Pressed -= OnContinuePressed;
	}

	/// <summary>
	/// Fill the labels from CountdownManager after a level finishes.
	/// </summary>
	public void ShowStats()
	{
		var cm = CountdownManager.Instance;
		double bet = cm.CurrentBetTime;
		double actual = cm.ActualTimeUsed;
		double overshoot = Math.Max(0.0, actual - bet);
		double remaining = cm.TotalAvailableTime;

		if (_betLabel != null)
			_betLabel.Text = $"{bet:F1}s";

		if (_timeLabel != null)
			_timeLabel.Text = $"{actual:F1}s";

		if (_penaltyLabel != null)
			_penaltyLabel.Text = overshoot > 0.0 ? $"-{overshoot:F1}s" : "None";

		if (_remainingLabel != null)
			_remainingLabel.Text = $"{remaining:F1}s";

		// ── Calculation formula ───────────────────────────────────────────
		if (_calculationLabel != null)
		{
			double baseTime = cm.GetLevelBaseTime(cm.CurrentLevelId);
			double penaltyFromPrev = cm.PenaltyAppliedToCurrentLevel;
			double prevRemaining = cm.TotalBeforeLevelAllocation;

			// Build: prevRemaining + baseTime - penaltyFromPrev - bet [- overshoot×2] = remaining
			var parts = new System.Collections.Generic.List<string>();

			// Show previous remaining only if > 0
			if (prevRemaining > 0.001)
				parts.Add($"{prevRemaining:F1}");

			parts.Add($"{baseTime:F1}");

			if (penaltyFromPrev > 0.001)
				parts.Add($"-{penaltyFromPrev:F1}");

			parts.Add($"-{bet:F1}");

			if (overshoot > 0.001)
				parts.Add($"-{overshoot:F1}×2");

			string formula = string.Join(" ", parts);
			_calculationLabel.Text = $"{formula} = {remaining:F1}s";
		}
	}

	private void OnContinuePressed()
	{
		// Proceed to next level (or end game)
		GameManager.Instance.ContinueAfterSummary();
	}
}
