using Godot;
using System;

public partial class Laser : Area3D
{
	[Export] private AudioStreamPlayer3D _buzzPlayer;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnBodyEntered(object body)
	{
		GameManager.Instance.Respawn();
		// LaserEnter z SfxLibrary, fallback do pliku
		var enterStream = AudioManager.Instance.Sfx.LaserEnter;
		if (enterStream != null)
			AudioManager.Instance.PlaySFX(enterStream);
	}
}
