using Godot;
using System;

public partial class MainMenu : Control
{
	
	[Export] private Button _playButton;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_playButton.Pressed += OnPlayButton_Pressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
	
	private void OnPlayButton_Pressed()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.PlayButtonPressed));
	}
}
