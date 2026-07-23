using Godot;
using System;

public partial class GameOverlay : Control
{
	[Export] private Label _countdownLabel;

	private double _remainingTime;
	private bool _isRunning;

	public override void _Ready()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.StartCountdown += StartCountdown;
			GameManager.Instance.StopCountdown += StopCountdown;
		}
	}

	public override void _ExitTree()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.StartCountdown -= StartCountdown;
			GameManager.Instance.StopCountdown -= StopCountdown;
		}
	}

	public override void _Process(double delta)
	{
		if (!_isRunning || !GodotObject.IsInstanceValid(_countdownLabel))
			return;

		_remainingTime -= delta;
		_countdownLabel.Text = $"{_remainingTime:F1}s";
	}

	private void StartCountdown(double time)
	{
		if (!GodotObject.IsInstanceValid(this))
			return;

		_remainingTime = time;
		_isRunning = true;
	}

	private void StopCountdown()
	{
		_isRunning = false;

		// Zapisz rzeczywisty czas spędzony na poziomie (może być > betTime)
		double actualTime = CountdownManager.Instance.CurrentBetTime - _remainingTime;
		CountdownManager.Instance.SetActualTimeUsed(actualTime);

		if (GameManager.Instance != null)
			GameManager.Instance.EmitSignal(nameof(GameManager.CountdownPaused));
	}
}