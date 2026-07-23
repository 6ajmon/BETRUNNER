using Godot;
using System;

public partial class GameOverlay : Control
{
	private Timer _countdownTimer;
	[Export] private Label _countdownLabel;

	public override void _Ready()
	{
		_countdownTimer = GetNodeOrNull<Timer>("Timer");

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
		if (GodotObject.IsInstanceValid(_countdownTimer) && _countdownLabel != null)
		{
			_countdownLabel.Text = Math.Round(_countdownTimer.TimeLeft, 1).ToString();
		}
	}

	private void StartCountdown(double time)
	{
		if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_countdownTimer))
			return;

		_countdownTimer.Paused = false;
		_countdownTimer.OneShot = true;
		_countdownTimer.Start(time);
	}

	private void StopCountdown()
	{
		if (GodotObject.IsInstanceValid(this) && GodotObject.IsInstanceValid(_countdownTimer))
			_countdownTimer.Paused = true;

		if (GameManager.Instance != null)
			GameManager.Instance.EmitSignal(nameof(GameManager.CountdownPaused));
	}
}