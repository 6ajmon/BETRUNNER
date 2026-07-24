using Godot;
using System;

public partial class PressurePlate : Area3D
{
	public enum PressurePlateType
	{
		one_time,
		pressure,
	}
	
	[Export] PressurePlateType _pressurePlateType = PressurePlateType.one_time;
	bool pressed = false;
	
	[Signal] public delegate void TurnOnEventHandler();
	[Signal] public delegate void TurnOffEventHandler();
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (pressed)
		{
			GD.Print("pressed");
		}
	}

	private void OnBodyEntered(Node body)
	{
		pressed = true;
		EmitSignalTurnOn();
	}

	private void OnBodyExited(Node body)
	{
		if (_pressurePlateType == PressurePlateType.pressure)
		{
			pressed = false;
			EmitSignalTurnOff();
		}
		GD.Print("stop");
	}
}
