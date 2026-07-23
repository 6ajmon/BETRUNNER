using Godot;
using System;

public partial class GameOverlay : Control
{
	[Export] private Label _countdownLabel;
	[Export] private Label _limitLabel;

	private double _remainingTime;
	private double _initialLimit;
	private bool _isRunning;
	private bool _failureTriggered;

	public override void _Ready()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.StartCountdown += StartCountdown;
			GameManager.Instance.StopCountdown += StopCountdown;
		}

		CountdownManager.Instance.BetPlaced += OnBetPlaced;
	}

	public override void _ExitTree()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.StartCountdown -= StartCountdown;
			GameManager.Instance.StopCountdown -= StopCountdown;
		}

		if (CountdownManager.Instance != null)
			CountdownManager.Instance.BetPlaced -= OnBetPlaced;
	}

	public override void _Process(double delta)
	{
		if (!_isRunning || !GodotObject.IsInstanceValid(_countdownLabel))
			return;

		_remainingTime -= delta;
		_countdownLabel.Text = $"{_remainingTime:F1}s";

		// Limit zaczyna się zmniejszać DOPIERO gdy czas główny jest na minusie
		double currentLimit = _initialLimit;
		if (_remainingTime < 0.0)
			currentLimit = _initialLimit + _remainingTime; // _remainingTime jest ujemne

		if (_limitLabel != null)
			_limitLabel.Text = $"{currentLimit:F1}s";

		if (currentLimit <= 0.0 && !_failureTriggered)
		{
			_failureTriggered = true;
			_isRunning = false;

			double actualTime = CountdownManager.Instance.CurrentBetTime - _remainingTime;
			CountdownManager.Instance.SetActualTimeUsed(actualTime);
			CountdownManager.Instance.OnLevelFinished(actualTime);

			if (GameManager.Instance != null)
				GameManager.Instance.TriggerDynamicFailure();
		}
	}

	private void OnBetPlaced(double betTime, double remainingTime)
	{
		if (!GodotObject.IsInstanceValid(this)) return;

		_remainingTime = betTime;
		_isRunning = false;
		_failureTriggered = false;
		_countdownLabel.Text = $"{betTime:F1}s";

		// Zapamiętaj początkowy limit — będzie stały aż do wejścia w ujemne wartości
		_initialLimit = CountdownManager.Instance.GetEffectiveLimit(betTime);
		if (_limitLabel != null)
			_limitLabel.Text = $"{_initialLimit:F1}s";
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
		if (_failureTriggered) return;

		_isRunning = false;

		double actualTime = CountdownManager.Instance.CurrentBetTime - _remainingTime;
		CountdownManager.Instance.SetActualTimeUsed(actualTime);

		if (GameManager.Instance != null)
			GameManager.Instance.EmitSignal(nameof(GameManager.CountdownPaused));
	}
}