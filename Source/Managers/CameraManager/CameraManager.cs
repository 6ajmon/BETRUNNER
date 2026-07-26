using Godot;
using System;

public partial class CameraManager : Node3D
{
	public static CameraManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<CameraManager>("CameraManager");
	[Signal] public delegate void SwitchToPreviewCameraEventHandler();
	[Signal] public delegate void SwitchToFirstPersonCameraEventHandler();
	public Camera3D PreviewCamera { get; set; }
	public Camera3D PlayerCamera { get; set; }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		SwitchToPreviewCamera += OnSwitchToPreviewCamera;
		SwitchToFirstPersonCamera += OnSwitchToFirstPersonCamera;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void OnSwitchToPreviewCamera()
	{
		PreviewCamera.MakeCurrent();
		GameManager.Instance.PlayerCharacter?.SetModelVisible(false);
	}

	public void OnSwitchToFirstPersonCamera()
	{
		PlayerCamera.MakeCurrent();
		GameManager.Instance.PlayerCharacter?.SetModelVisible(true);
	}
}