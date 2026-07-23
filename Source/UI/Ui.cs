using Godot;
using System;

public partial class Ui : Control
{
    private Timer _countdownTimer;
    [Export] private Label _countdownLabel;
    [Export] private Button betButton;
    
    public override void _Ready()
    {
        _countdownTimer = GetNodeOrNull<Timer>("Timer");

        // Subskrypcja zdarzeń
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartCountdown += StartCountdown;
            GameManager.Instance.StopCountdown += StopCountdown;
        }

        if (betButton != null)
        {
            betButton.Pressed += OnBetButtonPressed;
        }
    }

    public override void _ExitTree()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartCountdown -= StartCountdown;
            GameManager.Instance.StopCountdown -= StopCountdown;
        }

        if (betButton != null)
        {
            betButton.Pressed -= OnBetButtonPressed;
        }
    }

    public override void _Process(double delta)
    {
        if (GodotObject.IsInstanceValid(_countdownTimer) && _countdownLabel != null)
        {
            _countdownLabel.Text = Math.Round(_countdownTimer.TimeLeft, 1).ToString();
        }
    }
    
    private void OnBetButtonPressed()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EmitSignal(nameof(GameManager.EndBettingPhase));
        }
    }

    private void StartCountdown(double time)
    {
        if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(_countdownTimer))
        {
            return;
        }

        _countdownTimer.Paused = false;
        _countdownTimer.OneShot = true;
        _countdownTimer.Start(time);
    }
    
    private void StopCountdown()
    {
        if (GodotObject.IsInstanceValid(this) && GodotObject.IsInstanceValid(_countdownTimer))
        {
            _countdownTimer.Paused = true;
        }
       
        if (GameManager.Instance != null)
        {
            GameManager.Instance.EmitSignal(nameof(GameManager.CountdownPaused));
        }
    }
}