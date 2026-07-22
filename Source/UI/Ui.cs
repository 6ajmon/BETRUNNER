using Godot;
using System;

public partial class Ui : Control
{
	[Export] private Timer _countdownTimer;
	[Export]
	private Label _countdownLabel;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		GameManager.Instance.StartCountdown += StartCountdown;
		GameManager.Instance.StopCountdown += StopCountdown;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_countdownLabel.Text = Math.Round(_countdownTimer.TimeLeft, 1).ToString();
	}

	private void StartCountdown(double time)
	{
		GD.Print("Start Countdown");
		_countdownTimer.OneShot = true;
		_countdownTimer.Start(time);
	}
	
	private void StopCountdown()
	{
		_countdownTimer.Stop();
	}
}
