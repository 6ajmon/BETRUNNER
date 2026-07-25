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
		if (_event != Event.StopCountdown)
		{
			Visible = false;
		}
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
		if (_event != Event.SetSpawn)
		{
			Call(_event.ToString());
		}
		
	}

	private void StartCountdown()
	{
		double betTime = CountdownManager.Instance.CurrentBetTime;
		GameManager.Instance.EmitSignal(nameof(GameManager.StartCountdown), betTime);
		this.QueueFree();
	}

	private void StopCountdown()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.StopCountdown));
		Visible = false;
	}

	private void BackToSpawn()
	{
		GameManager.Instance.Respawn();
	}

	private void SetSpawn()
	{
		GameManager.Instance.SpawnPosition = this.GlobalPosition;
		GameManager.Instance.FaceDirectionAfterRespawn =
			_rayCast.ToGlobal(_rayCast.TargetPosition) - _rayCast.GlobalPosition;
	}
}
