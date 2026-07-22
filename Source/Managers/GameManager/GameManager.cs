using Godot;
using System;

public partial class GameManager : Node
{
	public static GameManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameManager>("GameManager");
	[Signal] public delegate void StartCountdownEventHandler(double time);
	[Signal] public delegate void StopCountdownEventHandler();
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
