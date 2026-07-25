using Godot;
using System;

public partial class MainMenu : Control
{
	
	[Export] private Button _playButton;
	[Export] private Button _creditsButton;
	[Export] private Button _exitButton;
	[Export] private Camera3D _camera;
	[Export] private float _cameraSpeed;
	private bool _cameraGoingUp = true;
	private ConfirmationDialog _exitDialog;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_playButton.Pressed += OnPlayButtonPressed;
		_creditsButton.Pressed += OnCreditsButtonPressed;
		_exitButton.Pressed += OnExitButtonPressed;
		
		_exitDialog = new ConfirmationDialog();
		_exitDialog.Title = "Exit Game";
		_exitDialog.DialogText = "Are you sure you want to exit the game?";
		_exitDialog.GetOkButton().Text = "Yes";
		_exitDialog.GetCancelButton().Text = "No";
		_exitDialog.Confirmed += () => GetTree().Quit();
		AddChild(_exitDialog);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_camera != null)
		{
			CameraMovement(delta);
		}
		
	}
	
	private void OnPlayButtonPressed()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.PlayButtonPressed));
	}
	
	private void OnCreditsButtonPressed()
	{
		OS.ShellOpen("https://github.com/6ajmon/gmtk2026");
	}
	
	private void OnExitButtonPressed()
	{
		_exitDialog.PopupCentered();
	}

	private void CameraMovement(double delta)
	{
		if (_cameraGoingUp)
		{
			if (_camera.GlobalPosition.Y <= 3)
			{
				_camera.Position += new Vector3(0, (float)(delta * _cameraSpeed), 0);
			}
			else
			{
				_cameraGoingUp = false;
			}
		}
		else
		{
			if (_camera.GlobalPosition.Y >= 1.17)
			{
				_camera.Position -= new Vector3(0, (float)(delta * _cameraSpeed), 0);
			}
			else
			{
				_cameraGoingUp = true;
			}
		}
		
	}
}
