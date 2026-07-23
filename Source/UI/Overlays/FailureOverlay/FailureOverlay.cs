using Godot;
using System;
using System.Text;

public partial class FailureOverlay : Control
{
	[Export] private Label _statsLabel;
	[Export] private Button _menuButton;

	public override void _Ready()
	{
		if (_menuButton != null)
			_menuButton.Pressed += OnMenuPressed;
	}

	public override void _ExitTree()
	{
		if (_menuButton != null)
			_menuButton.Pressed -= OnMenuPressed;
	}

	/// <summary>
	/// Fill the stats label with all level history.
	/// </summary>
	public void ShowStats()
	{
		if (_statsLabel == null) return;

		var history = CountdownManager.Instance.LevelHistory;
		var sb = new StringBuilder();

		for (int i = 0; i < history.Count; i++)
		{
			var s = history[i];
			sb.AppendLine($"Level {i + 1} ({s.LevelId})");
			sb.AppendLine($"  Bet:     {s.BetTime:F1}s");
			sb.AppendLine($"  Time:    {s.ActualTime:F1}s");
			if (s.Overshoot > 0.0)
			{
				sb.AppendLine($"  Overshoot: +{s.Overshoot:F1}s");
				sb.AppendLine($"  Penalty:  -{s.Penalty:F1}s");
			}
			sb.AppendLine();
		}

		_statsLabel.Text = sb.ToString();
	}

	private void OnMenuPressed()
	{
		GameManager.Instance.ReturnToMainMenu();
	}
}
