using Godot;
using System;

public partial class MainMenu : Node
{
	
	[Export] private Camera3D _camera;
	[Export] private float _cameraSpeed;
	private bool _cameraGoingUp = true;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SceneManager.Instance.ShowMainMenuOverlay();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (_camera != null)
		{
			CameraMovement(delta);
		}
		
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
