using Godot;
using System;
using System.Text;
using BETRUNNER;

public partial class FinishOverlay : Control
{
	[Export] private Button _menuButton;

	[Export] private VBoxContainer _levelStatsContainer;
	[Export] private PanelContainer _curveContainer; // Container for the TimeGraph
	[Export] private Control _legendContainer;       // Container with a RichTextLabel for the legend
	[Export] private Control _resultContainer;       // Container with a RichTextLabel for the final result
	[Export] private Label _titleLabel;              // "GAME OVER" — zmieniane na "VICTORY" przy wygranej

	private RichTextLabel _legendLabel;
	private RichTextLabel _resultLabel;
	private TimeGraph _timeGraph;
	private float _rowsRevealProgress;
	private Tween _revealTween;
	private readonly System.Collections.Generic.List<CanvasItem> _animatedRows = new();

	public override void _Ready()
	{
		if (_menuButton != null)
		{
			_menuButton.Pressed += OnMenuPressed;
			ButtonSoundHelper.Wire(_menuButton);
		}

		if (_levelStatsContainer == null)
			GD.PrintErr("FailureOverlay: LevelStatsContainer not found in scene!");

		// Find RichTextLabel children in the exported containers
		if (_legendContainer != null)
			_legendLabel = _legendContainer.GetChild<RichTextLabel>(0);
		if (_resultContainer != null)
			_resultLabel = _resultContainer.GetChild<RichTextLabel>(0);

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

		// ── Legend ────────────────────────────────────────────────────────
		if (_legendLabel != null)
		{
			_legendLabel.BbcodeEnabled = true;
			_legendLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			_legendLabel.Text = $"[color=#{UIColors.Bonus.ToHtml()}]+ bonus[/color]  [color=#{UIColors.Bet.ToHtml()}]- bet[/color]  [color=#{UIColors.Penalty.ToHtml()}]- penalty[/color]";
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
			string prefix = i > 0 ? "+ " : "  ";
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

		// ── Result: final = 0 (bigger text) ───────────────────────────
		if (_resultLabel != null)
		{
			_resultLabel.BbcodeEnabled = true;
			_resultLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			_resultLabel.Text = $"[b][font_size=24][color=#{UIColors.Penalty.ToHtml()}]= 0.0s[/color][/font_size][/b]";
		}

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

	/// <summary>
	/// Victory variant — dla ekranu końcowego po przejściu wszystkich poziomów.
	/// Zmienia tytuł na "VICTORY", pokazuje dodatnią pozostałą pulę,
	/// a na wykresie ostatni poziom odejmuje tylko faktyczny czas (biały kolor).
	/// </summary>
	public void ShowVictoryStats()
	{
		if (_titleLabel != null)
			_titleLabel.Text = "VICTORY";

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

		// ── Legend ────────────────────────────────────────────────────────
		if (_legendLabel != null)
		{
			_legendLabel.BbcodeEnabled = true;
			_legendLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			_legendLabel.Text = $"[color=#{UIColors.Bonus.ToHtml()}]+ bonus[/color]  [color=#{UIColors.Bet.ToHtml()}]- bet[/color]  [color=#{UIColors.Penalty.ToHtml()}]- penalty[/color]";
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
			string prefix = i > 0 ? "+ " : "  ";
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

		// ── Result: positive remaining pool ────────────────────────────
		if (_resultLabel != null)
		{
			_resultLabel.BbcodeEnabled = true;
			_resultLabel.MouseFilter = Control.MouseFilterEnum.Ignore;
			_resultLabel.Text = $"[b][font_size=24][color=#{UIColors.Bonus.ToHtml()}]= {Math.Max(0.0, cm.TotalAvailableTime):F1}s[/color][/font_size][/b]";
		}

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

		// ── Feed data to the TimeGraph (victory mode: last level uses actual time) ──
		if (_timeGraph != null)
		{
			var (segments, markers) = CountdownManager.Instance.BuildTimeGraphData(isVictory: true);
			_timeGraph.SetData(segments, markers);
		}
	}

	private void OnMenuPressed()
	{
		GameManager.Instance.ReturnToMainMenu();
	}
}
