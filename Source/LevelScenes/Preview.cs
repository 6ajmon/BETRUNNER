using Godot;
using System;

public partial class Preview : PathFollow3D
{
	[Export] private double _speed = 0.2;
	private Camera3D _previewCamera;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_previewCamera = GetNode<Camera3D>("Camera3D");
		CameraManager.Instance.PreviewCamera = _previewCamera;
		GameManager.Instance.EmitSignal(nameof(GameManager.PreviewCameraLoaded)); 
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		ProgressRatio += (float)(_speed * delta);
	}
}
