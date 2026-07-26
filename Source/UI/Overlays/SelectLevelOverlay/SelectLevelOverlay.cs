using Godot;
using System;

public partial class SelectLevelOverlay : Control
{
	[Export] private Button _returnButton;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ButtonSoundHelper.Wire(_returnButton);
		_returnButton.Pressed += OnReturnButtonPressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnReturnButtonPressed()
	{
		SceneManager.Instance.ShowMainMenuOverlay();
	}
	
}
