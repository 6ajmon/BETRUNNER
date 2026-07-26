using Godot;
using System;
using NewGameProject;

public partial class GameOverlay : Control
{
	[Export] private Label _countdownLabel;
	[Export] private Label _limitLabel;
	[Export] private StoperProgressBar _stoperBar;

	private double _remainingTime;
	private double _initialLimit;
	private bool _isRunning;
	private bool _finishTriggered;

	public override void _Ready()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.StartCountdown += StartCountdown;
			GameManager.Instance.StopCountdown += StopCountdown;
		}

		CountdownManager.Instance.BetPlaced += OnBetPlaced;
	}

	public void Stop()
	{
		_isRunning = false;
		_finishTriggered = true;
		if (AudioManager.Instance != null)
			AudioManager.Instance.StopLoopingSFX();
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

		// Safety: zatrzymaj tykanie gdy overlay opuszcza drzewo (zmiana sceny itp.)
		if (AudioManager.Instance != null)
			AudioManager.Instance.StopLoopingSFX();
	}

	public override void _Process(double delta)
	{
		if (!_isRunning || !GodotObject.IsInstanceValid(_countdownLabel))
			return;

		_remainingTime -= delta;
		_countdownLabel.Text = $"{_remainingTime:F1}s";

		// Limit zaczyna się zmniejszać DOPIERO gdy czas główny jest na minusie
		// I leci 2× szybciej — kara za overshoot
		double currentLimit = _initialLimit;
		if (_remainingTime < 0.0)
			currentLimit = _initialLimit + _remainingTime * 2.0; // _remainingTime jest ujemne

		// Aktualizuj stoper (okrągłe progress bary + tekst w środku)
		if (_stoperBar != null)
		{
			_stoperBar.TimerFlowing = true; // czas leci → dźwięki grają

			double betRemaining = Math.Max(0.0, _remainingTime);
			double limitRemaining = Math.Max(0.0, currentLimit);
			double betRatio = CountdownManager.Instance.CurrentBetTime > 0.0
				? betRemaining / CountdownManager.Instance.CurrentBetTime
				: 0.0;
			double limitRatio = _initialLimit > 0.0
				? limitRemaining / _initialLimit
				: 0.0;
			_stoperBar.BetRatio   = (float)Math.Clamp(betRatio, 0.0, 1.0);
			_stoperBar.LimitRatio = (float)Math.Clamp(limitRatio, 0.0, 1.0);
			_stoperBar.BetTimerText   = $"{betRemaining:F1}s";
			_stoperBar.LimitTimerText = $"{limitRemaining:F1}s";
			_stoperBar.ActiveTimer = _remainingTime > 0.0
				? StoperProgressBar.ActiveTimerEnum.Bet
				: StoperProgressBar.ActiveTimerEnum.Limit;
			_stoperBar.UpdateProgress();
		}

		if (_limitLabel != null)
		{
			_limitLabel.Text = $"{currentLimit:F1}s";
			_limitLabel.AddThemeColorOverride("font_color", UIColors.Limit);
		}

		if (currentLimit <= 0.0 && _remainingTime < 0.0 && !_finishTriggered)
		{
			_finishTriggered = true;
			_isRunning = false;

			// Zatrzymaj ciągły dźwięk stopera
			if (AudioManager.Instance != null)
				AudioManager.Instance.StopLoopingSFX();

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
		_finishTriggered = false;
		_countdownLabel.Text = $"{betTime:F1}s";
		_countdownLabel.AddThemeColorOverride("font_color", UIColors.Bet);

		// Zapamiętaj początkowy limit — będzie stały aż do wejścia w ujemne wartości
		_initialLimit = CountdownManager.Instance.GetEffectiveLimit(betTime);

		// Stoper startuje z pełnymi wartościami (dźwięk NIE gra — TimerFlowing = false)
		if (_stoperBar != null)
		{
			_stoperBar.TimerFlowing = false;
			_stoperBar.BetRatio = 1f;
			_stoperBar.LimitRatio = 1f;
			_stoperBar.BetTimerText = $"{betTime:F1}s";
			_stoperBar.LimitTimerText = $"{_initialLimit:F1}s";
			_stoperBar.ActiveTimer = StoperProgressBar.ActiveTimerEnum.Bet;
			_stoperBar.UpdateProgress();
		}

		if (_limitLabel != null)
		{
			_limitLabel.Text = $"{_initialLimit:F1}s";
			_limitLabel.AddThemeColorOverride("font_color", UIColors.Limit);
		}
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
		if (_finishTriggered) return;

		_isRunning = false;

		// Zatrzymaj ciągły dźwięk stopera
		if (AudioManager.Instance != null)
			AudioManager.Instance.StopLoopingSFX();

		double actualTime = CountdownManager.Instance.CurrentBetTime - _remainingTime;
		CountdownManager.Instance.SetActualTimeUsed(actualTime);

		if (GameManager.Instance != null)
			GameManager.Instance.EmitSignal(nameof(GameManager.CountdownPaused));
	}
}