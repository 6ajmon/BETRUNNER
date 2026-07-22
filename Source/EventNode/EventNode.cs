using Godot;
using System;

public partial class EventNode : Area3D
{
	private enum Event
	{
		StartCountdown,
		StopCountdown,
	}

	[Export] private Event _event;
	
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
		GD.Print("Body entered: " + body);
		Call(_event.ToString());
	}

	private void StartCountdown()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.StartCountdown), 15d);
	}

	private void StopCountdown()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.StopCountdown));
	}

}
