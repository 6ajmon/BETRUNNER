using Godot;
using System;
using NewGameProject;

public partial class SummaryOverlay : Control
{
	[Export] private RichTextLabel _bonusValueLabel;
	[Export] private RichTextLabel _betLabel;
	[Export] private RichTextLabel _penaltyLabel;
	[Export] private RichTextLabel _timeLabel;
	[Export] private RichTextLabel _RemainingLabel; // RemainingTimeLabel — shows colored formula
	[Export] private Button _continueButton;

	public override void _Ready()
	{
		if (_continueButton != null)
		{
			_continueButton.Pressed += OnContinuePressed;
			ButtonSoundHelper.Wire(_continueButton);
		}

		// Enable BBCode on all value labels (in case not set in scene)
		foreach (var rt in new[] { _bonusValueLabel, _betLabel, _timeLabel, _penaltyLabel, _RemainingLabel })
		{
			if (rt != null)
			{
				rt.BbcodeEnabled = true;
				rt.MouseFilter = MouseFilterEnum.Ignore;
			}
		}
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
		double bonusTime = cm.CurrentLevelBaseTime; // time actually added from this level

		// ── Colored value labels (BBCode) ────────────────────────────────
		if (_betLabel != null)
			_betLabel.Text = $"[color=#{UIColors.Bet.ToHtml()}]{bet:F1}s[/color]";

		// ── Finished-in: bet + overshoot = actual ────────────────────────
		if (_timeLabel != null)
		{
			_timeLabel.Text = overshoot > 0.001
				? $"[color=#{UIColors.Bet.ToHtml()}]{bet:F1}[/color] + [color=#{UIColors.Penalty.ToHtml()}]{overshoot:F1}[/color] = {actual:F1}s"
				: $"{actual:F1}s";
		}

		if (_penaltyLabel != null)
		{
			_penaltyLabel.Text = overshoot > 0.001
				? $"[color=#{UIColors.Penalty.ToHtml()}]-{overshoot:F1}s[/color]"
				: "[color=gray]None[/color]";
		}

		if (_bonusValueLabel != null)
			_bonusValueLabel.Text = $"[color=#{UIColors.Bonus.ToHtml()}]+{bonusTime:F1}s[/color]";

		// Also color the description labels to match
		ColorLabelsInGrid();

		// ── Colored calculation formula in the RemainingTime cell ────────
		if (_RemainingLabel != null)
		{
			double prevRemaining = cm.TotalBeforeLevelAllocation;

			var parts = new System.Collections.Generic.List<string>();

			// Previous remaining (limit color)
			if (prevRemaining > 0.001)
			{
				parts.Add($"[color=#{UIColors.Limit.ToHtml()}]{prevRemaining:F1}[/color]");
				parts.Add(" + ");
			}

			// Bonus base time (bonus color) — już uwzględnia karę z poprzedniego poziomu
			parts.Add($"[color=#{UIColors.Bonus.ToHtml()}]{bonusTime:F1}[/color]");

			// Bet (bet color)
			parts.Add(" - ");
			parts.Add($"[color=#{UIColors.Bet.ToHtml()}]{bet:F1}[/color]");

			// Overshoot penalty (penalty color)
			if (overshoot > 0.001)
			{
				parts.Add(" - ");
				parts.Add($"[color=#{UIColors.Penalty.ToHtml()}]{overshoot:F1}×2[/color]");
			}

			string formula = string.Join("", parts);
			_RemainingLabel.Text = $"{formula} = [color=#{UIColors.Limit.ToHtml()}]{remaining:F1}s[/color]";
		}
	}

	/// <summary>
	/// Apply matching colors to the description labels in the grid.
	/// </summary>
	private void ColorLabelsInGrid()
	{
		var grid = _betLabel?.GetParent() as GridContainer;
		if (grid == null) return;

		int childCount = grid.GetChildCount();
		for (int i = 0; i < childCount; i += 2)
		{
			var descLabel = grid.GetChild<Label>(i);
			if (descLabel == null) continue;

			string text = descLabel.Text.ToLowerInvariant();

			if (text.Contains("bet"))
				descLabel.AddThemeColorOverride("font_color", UIColors.Bet);
			else if (text.Contains("penalty"))
				descLabel.AddThemeColorOverride("font_color", UIColors.Penalty);
			else if (text.Contains("remaining"))
				descLabel.AddThemeColorOverride("font_color", UIColors.Limit);
			else if (text.Contains("level"))
				descLabel.AddThemeColorOverride("font_color", UIColors.Bonus);
		}
	}

	private void OnContinuePressed()
	{
		// Proceed to next level (or end game)
		GameManager.Instance.ContinueAfterSummary();
	}
}
