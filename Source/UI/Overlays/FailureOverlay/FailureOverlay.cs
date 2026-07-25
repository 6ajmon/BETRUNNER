using Godot;
using System;
using System.Text;
using NewGameProject;

public partial class FailureOverlay : Control
{
	[Export] private Button _menuButton;

	[Export] private VBoxContainer _levelStatsContainer;

	public override void _Ready()
	{
		if (_menuButton != null)
			_menuButton.Pressed += OnMenuPressed;

		if (_levelStatsContainer == null)
			GD.PrintErr("FailureOverlay: LevelStatsContainer not found in scene!");
	}

	public override void _ExitTree()
	{
		if (_menuButton != null)
			_menuButton.Pressed -= OnMenuPressed;
	}

	/// <summary>
	/// Dynamically populate LevelStatsContainer with per-level calculation rows.
	/// Each row: [Level N label (ratio 1)] [calculation RichTextLabel (ratio 3)]
	/// </summary>
	public void ShowStats()
	{
		if (_levelStatsContainer == null) return;

		// Clear previously added rows
		foreach (var child in _levelStatsContainer.GetChildren())
		{
			if (child is HBoxContainer)
				child.QueueFree();
		}

		var history = CountdownManager.Instance.LevelHistory;
		var cm = CountdownManager.Instance;
		double runningTotal = 0.0;
		double prevOvershoot = 0.0;

		for (int i = 0; i < history.Count; i++)
		{
			var stat = history[i];
			double baseTime = cm.GetLevelBaseTime(stat.LevelId);
			double penaltyFromPrev = prevOvershoot;

			// Net contribution of this level to the total pool
			double effectiveBase = Math.Max(0.0, baseTime - penaltyFromPrev);
			double netContribution = effectiveBase - stat.BetTime - stat.Overshoot * 2;
			runningTotal += netContribution;

			// ── Row: HBoxContainer ────────────────────────────────────────
			var hbox = new HBoxContainer();
			hbox.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
			hbox.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
			hbox.AddThemeConstantOverride("separation", 8);

			// Left: level number label (ratio 1), top-aligned
			var levelLabel = new Label();
			levelLabel.Text = $"Level {i + 1}";
			levelLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
			levelLabel.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
			levelLabel.SizeFlagsStretchRatio = 1.0f;
			levelLabel.VerticalAlignment = VerticalAlignment.Top;

			// Right: calculation RichTextLabel with BBCode (ratio 3)
			var calcLabel = new RichTextLabel();
			calcLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
			calcLabel.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
			calcLabel.SizeFlagsStretchRatio = 3.0f;
			calcLabel.BbcodeEnabled = true;
			calcLabel.FitContent = true;
			calcLabel.ScrollActive = false;
			calcLabel.MouseFilter = Control.MouseFilterEnum.Ignore;

			// Build colored formula (no running total, no separate penalty carry-over)
			var calc = new StringBuilder();
			calc.Append($"[color=#{UIColors.Bonus.ToHtml()}]{effectiveBase:F1}[/color]");
			calc.Append($" - [color=#{UIColors.Bet.ToHtml()}]{stat.BetTime:F1}[/color]");

			if (stat.Overshoot > 0.001)
				calc.Append($" - [color=#{UIColors.Penalty.ToHtml()}]{stat.Overshoot:F1}×2[/color]");

			// No trailing + on the last level before summary
			if (i < history.Count - 1)
				calc.Append("  [color=gray]+[/color]");
			else if (i == history.Count - 1)
				calc.Append("  [color=gray]=[/color]");

			calcLabel.Text = calc.ToString();

			hbox.AddChild(levelLabel);
			hbox.AddChild(calcLabel);
			_levelStatsContainer.AddChild(hbox);

			prevOvershoot = stat.Overshoot;
		}

		// ── Summary row: final = 0 (bigger text) ─────────────────────
		var summaryHbox = new HBoxContainer();
		summaryHbox.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		summaryHbox.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		summaryHbox.AddThemeConstantOverride("separation", 8);

		var summaryLevelLabel = new Label();
		summaryLevelLabel.Text = "Total";
		summaryLevelLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		summaryLevelLabel.SizeFlagsStretchRatio = 1.0f;
		summaryLevelLabel.VerticalAlignment = VerticalAlignment.Top;

		var summaryCalcLabel = new RichTextLabel();
		summaryCalcLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		summaryCalcLabel.SizeFlagsStretchRatio = 3.0f;
		summaryCalcLabel.BbcodeEnabled = true;
		summaryCalcLabel.FitContent = true;
		summaryCalcLabel.ScrollActive = false;
		summaryCalcLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		summaryCalcLabel.Text = $"[b][font_size=20][color=#{UIColors.Penalty.ToHtml()}]= 0.0s[/color][/font_size][/b]";
		summaryCalcLabel.HorizontalAlignment = HorizontalAlignment.Right;
		summaryCalcLabel.VerticalAlignment = VerticalAlignment.Center;


		summaryHbox.AddChild(summaryLevelLabel);
		summaryHbox.AddChild(summaryCalcLabel);
		_levelStatsContainer.AddChild(summaryHbox);
	}

	private void OnMenuPressed()
	{
		GameManager.Instance.ReturnToMainMenu();
	}
}
