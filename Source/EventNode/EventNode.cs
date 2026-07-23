using Godot;
using System;

public partial class EventNode : Area3D
{
	private RayCast3D _rayCast;
	
	private enum Event
	{
		StartCountdown,
		StopCountdown,
		BackToSpawn,
        SetSpawn
	}

	[Export] private Event _event;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_rayCast = GetNode<RayCast3D>("Direction");
		BodyEntered += OnBodyEntered;
		if (_event == Event.SetSpawn)
		{
			Call(_event.ToString());
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	private void OnBodyEntered(object body)
	{
		GD.Print("Body entered: " + body);
		if (_event != Event.SetSpawn)
		{
			Call(_event.ToString());
		}
		
	}

	private void StartCountdown()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.StartCountdown), 15d);
	}

	private void StopCountdown()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.StopCountdown));
	}

	private void BackToSpawn()
	{
		GameManager.Instance.PlayerToSpawn(_rayCast.ToGlobal(_rayCast.TargetPosition) - _rayCast.GlobalPosition);
	}

	private void SetSpawn()
	{
		GameManager.Instance.SpawnPosition = this.GlobalPosition;
	}
}
