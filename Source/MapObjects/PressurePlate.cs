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
	
	private MeshInstance3D _mesh;
	private Material _originalMaterial;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		BodyEntered += OnBodyEntered;
		BodyExited += OnBodyExited;
		_mesh = GetNode<MeshInstance3D>("Hydroponics_Floor/Hydroponics Floor");
		_originalMaterial = _mesh.GetActiveMaterial(0);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}

	private void OnBodyEntered(Node body)
	{
		pressed = true;
		EmitSignalTurnOn();
		SetRed();
	}

	private void OnBodyExited(Node body)
	{
		if (_pressurePlateType == PressurePlateType.pressure)
		{
			pressed = false;
			EmitSignalTurnOff();
			RestoreColor();
		}
	}
	
	private void SetRed()
	{
		var material = new StandardMaterial3D();
		material.AlbedoColor = Colors.Red;
		_mesh.SetSurfaceOverrideMaterial(0, material);
	}
	public void RestoreColor()
	{
		_mesh.SetSurfaceOverrideMaterial(0, _originalMaterial);
	}
	
}
