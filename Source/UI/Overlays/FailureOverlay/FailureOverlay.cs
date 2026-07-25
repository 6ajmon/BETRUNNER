using Godot;
using System;
using System.Text;
using NewGameProject;

public partial class FailureOverlay : Control
{
	[Export] private Button _menuButton;

	[Export] private VBoxContainer _levelStatsContainer;
	[Export] private PanelContainer _curveContainer; // Container for the TimeGraph

	private TimeGraph _timeGraph;
	private float _rowsRevealProgress;
	private Tween _revealTween;
	private readonly System.Collections.Generic.List<CanvasItem> _animatedRows = new();

	public override void _Ready()
	{
		if (_menuButton != null)
			_menuButton.Pressed += OnMenuPressed;

		if (_levelStatsContainer == null)
			GD.PrintErr("FailureOverlay: LevelStatsContainer not found in scene!");

		// Create the TimeGraph and add it to the CurveContainer
		SetupTimeGraph();
	}

	public override void _ExitTree()
	{
		if (_menuButton != null)
			_menuButton.Pressed -= OnMenuPressed;
	}

	/// <summary>
	/// Find CurveContainer and add a TimeGraph control to it.
	/// </summary>
	private void SetupTimeGraph()
	{
		var curveContainer = _curveContainer;

		if (curveContainer == null)
		{
			GD.PrintErr("FailureOverlay: CurveContainer not found in scene!");
			return;
		}

		_timeGraph = new TimeGraph();
		_timeGraph.Name = "TimeGraph";
		_timeGraph.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		_timeGraph.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		_timeGraph.CustomMinimumSize = new Vector2(0, 180);

		curveContainer.AddChild(_timeGraph);
	}

	/// <summary>
	/// Tween-animated property: reveals rows progressively from top to bottom.
	/// </summary>
	private float rows_reveal_progress
	{
		get => _rowsRevealProgress;
		set
		{
			_rowsRevealProgress = Math.Clamp(value, 0f, 1f);
			int total = _animatedRows.Count;
			int visible = (int)(_rowsRevealProgress * total);

			for (int i = 0; i < total; i++)
			{
				var row = _animatedRows[i];
				if (i < visible)
				{
					row.Modulate = Colors.White;
					row.Show();
				}
				else if (i == visible && _rowsRevealProgress < 1f)
				{
					// Partially reveal the current row (fade in)
					float t = (_rowsRevealProgress * total) - visible;
					row.Modulate = new Color(1, 1, 1, t);
					if (t > 0.01f) row.Show(); else row.Hide();
				}
				else
				{
					row.Hide();
				}
			}
		}
	}

	/// <summary>
	/// Dynamically populate LevelStatsContainer with per-level calculation rows.
	/// Each row: [Level N label (ratio 1)] [calculation RichTextLabel (ratio 3)]
	/// </summary>
	public void ShowStats()
	{
		if (_levelStatsContainer == null) return;

		// Clear previous animation state
		_revealTween?.Kill();
		_animatedRows.Clear();

		// Clear previously added rows
		foreach (var child in _levelStatsContainer.GetChildren())
		{
			if (child is HBoxContainer)
				child.QueueFree();
		}

		// ── Legend row ────────────────────────────────────────────────────
		var legendHbox = new HBoxContainer();
		legendHbox.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		legendHbox.AddThemeConstantOverride("separation", 8);

		var legendLevelLabel = new Label();
		legendLevelLabel.Text = "";
		legendLevelLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		legendLevelLabel.SizeFlagsStretchRatio = 1.0f;

		var legendCalcLabel = new RichTextLabel();
		legendCalcLabel.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		legendCalcLabel.SizeFlagsStretchRatio = 4.2f;
		legendCalcLabel.BbcodeEnabled = true;
		legendCalcLabel.FitContent = true;
		legendCalcLabel.ScrollActive = false;
		legendCalcLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
		legendCalcLabel.Text = $"[color=#{UIColors.Bonus.ToHtml()}]+ bonus[/color]  [color=#{UIColors.Bet.ToHtml()}]- bet[/color]  [color=#{UIColors.Penalty.ToHtml()}]- penalty[/color]";

		legendHbox.AddChild(legendLevelLabel);
		legendHbox.AddChild(legendCalcLabel);
		_levelStatsContainer.AddChild(legendHbox);
		_animatedRows.Add(legendHbox);

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
			string prefix = i > 0 ? "+ " : "";
			calc.Append($"[color=#{UIColors.Bonus.ToHtml()}]{prefix}{effectiveBase:F1}[/color]");
			calc.Append($"[color=#{UIColors.Bet.ToHtml()}] - {stat.BetTime:F1}[/color]");

			if (stat.Overshoot > 0.001)
				calc.Append($"[color=#{UIColors.Penalty.ToHtml()}] - {stat.Overshoot:F1}×2[/color]");

			// No trailing + on the last level before summary
			if (i < history.Count - 1)
				calc.Append("  [color=gray]+[/color]");
			else if (i == history.Count - 1)
				calc.Append("  [color=gray]=[/color]");

			calcLabel.Text = calc.ToString();

			hbox.AddChild(levelLabel);
			hbox.AddChild(calcLabel);
			_levelStatsContainer.AddChild(hbox);

			_animatedRows.Add(hbox);

			prevOvershoot = stat.Overshoot;
		}

		// ── Summary row: final = 0 (bigger text) ─────────────────────
		var summaryHbox = new HBoxContainer();
		summaryHbox.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		summaryHbox.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		summaryHbox.AddThemeConstantOverride("separation", 8);

		var summaryLevelLabel = new Label();
		summaryLevelLabel.Text = "";
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
		summaryCalcLabel.Text = $"[b][font_size=24][color=#{UIColors.Penalty.ToHtml()}]= 0.0s[/color][/font_size][/b]  ";
		summaryCalcLabel.HorizontalAlignment = HorizontalAlignment.Right;
		summaryCalcLabel.VerticalAlignment = VerticalAlignment.Center;

		summaryHbox.AddChild(summaryLevelLabel);
		summaryHbox.AddChild(summaryCalcLabel);
		_levelStatsContainer.AddChild(summaryHbox);
		_animatedRows.Add(summaryHbox);

		// ── Hide all rows initially, then animate in ──────────────────
		foreach (var row in _animatedRows)
		{
			row.Modulate = Colors.Transparent;
			row.Hide();
		}

		_rowsRevealProgress = 0f;
		_revealTween = CreateTween();
		_revealTween.TweenProperty(this, "rows_reveal_progress", 1f, 3.0f)
					.SetEase(Tween.EaseType.InOut)
					.SetTrans(Tween.TransitionType.Linear);

		// ── Feed data to the TimeGraph ─────────────────────────────────
		if (_timeGraph != null)
		{
			var (segments, markers) = CountdownManager.Instance.BuildTimeGraphData();
			_timeGraph.SetData(segments, markers);
		}
	}

	private void OnMenuPressed()
	{
		GameManager.Instance.ReturnToMainMenu();
	}
}
