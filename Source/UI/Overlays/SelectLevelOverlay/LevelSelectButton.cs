using Godot;
using System;

public partial class LevelSelectButton : Button
{
	[Export] public int LevelId = 0; 
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		ButtonSoundHelper.Wire(this);
		Pressed += OnPressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnPressed()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.LevelSelected), LevelId);
	}
}
