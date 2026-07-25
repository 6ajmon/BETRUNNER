using Godot;
using System;

public partial class MovingPlatform : Node3D
{
	private bool _active = true;
	[ExportGroup ("Movement")]
	[Export] private Vector3 _target = Vector3.Zero;
	[Export] private float _speed = 0f;
	[Export] private Vector3 _rotation = Vector3.Zero;
	[Export] private float _rotationSpeed = 0f;
	[Export] private bool _comeback = true;
	[ExportGroup ("Signal")]
	[Export] private PressurePlate _pressurePlate;
	[Export] private bool _waitingRoomObject = false;
	
	private Vector3 _startPosition;
	private bool _reverseDirection = false;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		if (_pressurePlate != null)
		{
			_active = false;
			_pressurePlate.TurnOn += TurnedOn;
			_pressurePlate.TurnOff += TurnedOff;
		}
		_startPosition = this.Position;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		float step = _speed * (float)delta;

		if (GameManager.Instance.CurrentState == GameManager.gameState.Preview ||
		    GameManager.Instance.CurrentState == GameManager.gameState.Countdown || 
		    _waitingRoomObject)
		{
			if(_active)
			{
				Move(step);
				Rotate(delta);
			}
		
		}
		
		else
		{
			Position = _startPosition;
		}	

	}

	private void Move(float step)
	{
		
		//Forward
		if (!_reverseDirection && Position.DistanceTo(_startPosition + _target) > step)
		{
			Position += _target.Normalized() * step;
		}
		else
		{
			_reverseDirection = true;
		}
		//Reverse
		if (_reverseDirection &&  _comeback &&Position.DistanceTo(_startPosition) > step)
		{
			Position -= _target.Normalized() * step;
		}
		else
		{
			_reverseDirection = false;
		}
		
		
		
	}

	private void Rotate(double delta)
	{
		//Rotation
		if (_rotation != Vector3.Zero)
		{
			Rotation += _rotation.Normalized() * (float)delta * _rotationSpeed;
		}
	}
	
	private void TurnedOn()
	{
		_active = true;
	}
	
	private void TurnedOff()
	{
		_active = false;
	}
	
}

