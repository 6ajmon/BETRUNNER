using Godot;
using System;
using System.Text;
using NewGameProject;

public partial class FailureOverlay : Control
{
	[Export] private Label _statsLabel;
	[Export] private Button _menuButton;

	private RichTextLabel _statsRT;

	public override void _Ready()
	{
		if (_menuButton != null)
			_menuButton.Pressed += OnMenuPressed;

		// Replace the plain Label with a RichTextLabel for BBCode coloring
		if (_statsLabel != null)
		{
			_statsRT = new RichTextLabel();
			_statsRT.Name = "StatsRT";
			_statsRT.Size = _statsLabel.Size;
			_statsRT.Position = _statsLabel.Position;
			_statsRT.BbcodeEnabled = true;
			_statsRT.FitContent = true;
			_statsRT.ScrollActive = false;

			var parent = _statsLabel.GetParent();
			int idx = _statsLabel.GetIndex();
			_statsLabel.Visible = false;
			parent.AddChild(_statsRT);
			parent.MoveChild(_statsRT, idx);
		}
	}

	public override void _ExitTree()
	{
		if (_menuButton != null)
			_menuButton.Pressed -= OnMenuPressed;
	}

	/// <summary>
	/// Fill the stats label with all level history (colored).
	/// </summary>
	public void ShowStats()
	{
		if (_statsRT == null) return;

		var history = CountdownManager.Instance.LevelHistory;
		var sb = new StringBuilder();

		for (int i = 0; i < history.Count; i++)
		{
			var s = history[i];
			sb.AppendLine($"[b]Level {i + 1} ({s.LevelId})[/b]");
			sb.AppendLine($"  Bet:     [color=#{UIColors.Bet.ToHtml()}]{s.BetTime:F1}s[/color]");
			sb.AppendLine($"  Time:    {s.ActualTime:F1}s");
			if (s.Overshoot > 0.0)
			{
				sb.AppendLine($"  Overshoot: +{s.Overshoot:F1}s");
				sb.AppendLine($"  Penalty:  [color=#{UIColors.Penalty.ToHtml()}]-{s.Penalty:F1}s[/color]");
			}
			sb.AppendLine();
		}

		_statsRT.Text = sb.ToString();
	}

	private void OnMenuPressed()
	{
		GameManager.Instance.ReturnToMainMenu();
	}
}
