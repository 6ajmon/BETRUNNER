using Godot;
using System;

public partial class MovingPlatform : Node3D
{
	[Export]
	private Vector3 _target = Vector3.Zero;
	[Export]
	private float _speed = 0f;
	private Vector3 _startPosition;
	private bool _reverseDirection = false;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_startPosition = this.Position;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		float step = _speed * (float)delta;
		
		if (GameManager.Instance.CurrentState == GameManager.gameState.Preview ||
		    GameManager.Instance.CurrentState == GameManager.gameState.Countdown)
		{
			if (!_reverseDirection && Position.DistanceTo(_startPosition + _target) > step)
			{
				Position += _target.Normalized() * step;
			}
			else
			{
				_reverseDirection = true;
			}

			if (_reverseDirection && Position.DistanceTo(_startPosition) > step)
			{
				Position -= _target.Normalized() * step;
			}
			else
			{
				_reverseDirection = false;
			}
		}
		else
		{
			Position = _startPosition;
		}
		
	}
	
}
