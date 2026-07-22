using Godot;

public enum PlayerMovementState
{
	OnGround,
	InAir,
}

public partial class PlayerMovement : Node
{
	[ExportGroup("Movement")]
	[Export] public float WalkSpeed { get; set; } = 5.0f;
	[Export] public float SprintSpeed { get; set; } = 10.0f;
	[Export] public float Acceleration { get; set; } = 14.0f;
	[Export] public float AirAcceleration { get; set; } = 8.0f;
	[Export] public float Friction { get; set; } = 12.0f;
	[Export] public float AirFriction { get; set; } = 0.8f;
	[Export] public float AirTurnPenalty { get; set; } = 0.03f;

	[ExportGroup("Jump")]
	[Export] public float JumpVelocity { get; set; } = 7.0f;
	[Export] public float Gravity { get; set; } = 18.0f;
	[Export] public float CoyoteTime { get; set; } = 0.08f;
	[Export] public float JumpBufferTime { get; set; } = 0.1f;
	[Export] public float JumpCamBump { get; set; } = 0.03f;
	[Export] public float LandCamBump { get; set; } = -0.04f;

	[ExportGroup("Wall Jump")]
	[Export] public float WallJumpHorizontalForce { get; set; } = 9.0f;
	[Export] public float WallJumpSpeedMultiplier { get; set; } = 2.5f;
	[Export] public float WallJumpVerticalForce { get; set; } = 7.0f;
	[Export] public float WallJumpCooldownTime { get; set; } = 0.01f;
	[Export] public float WallJumpAirPenaltyDuration { get; set; } = 0.1f;
	[Export] public float WallDetectionDistance { get; set; } = 0.8f;

	[ExportGroup("Climb Boost")]
	[Export] public float ClimbBoostVertical { get; set; } = 6.5f;
	[Export] public float ClimbBoostHorizontal { get; set; } = 0.0f;
	[Export] public float ClimbStartDelay { get; set; } = 0.16f;
	[Export] public float ClimbCooldownTime { get; set; } = 0.6f;
    [Export] public float ClimbClearanceHeight { get; set; } = 1.0f;
    [Export] public float PlayerHeight { get; set; } = 1.8f;

	public float MaxSprintSpeed => SprintSpeed;
	public PlayerMovementState CurrentState => _state;

	private Player _player;
	private Vector3 _velocity;
	private PlayerMovementState _state;

	// Timers
	private float _coyoteTimer;
	private float _jumpBufferTimer;

	// Air tracking
	private float _wallJumpCooldown;
	private bool _wasOnFloor;
	private float _airControlPenalty;
	private float _jumpTimer;

	// Hold-Space jump-on-landing flag
	private bool _holdJumpOnLanding;

	// Climb boost state
	private bool _climbReady;
	private float _climbCooldown;

	// Jump input tracking (Space key)
	private bool _spacePrevPressed;

	// Velocity before MoveAndSlide collision snapping
	private Vector3 _preCollisionVelocity;

	public override void _Ready()
	{
		_player = GetParent<Player>();
		_state = PlayerMovementState.OnGround;
		_wasOnFloor = true;
		_climbReady = true;
	}

	public void HandleMovement(float delta)
	{
		// --- Detect landing (floor transition) ---
		bool isOnFloor = _player.IsOnFloor();
		if (!_wasOnFloor && isOnFloor)
		{
			// Landed — reset air-only flags
			_wallJumpCooldown = 0.0f;
			_airControlPenalty = 0.0f;
			_climbReady = true;
			_player.BumpCamera(LandCamBump);

			// Hold-Space: jump immediately on landing
			if (Input.IsKeyPressed(Key.Space))
				_holdJumpOnLanding = true;
		}
		_wasOnFloor = isOnFloor;

		// Timer decay
		if (_climbCooldown > 0.0f) _climbCooldown -= delta;
		if (_wallJumpCooldown > 0.0f) _wallJumpCooldown -= delta;
		if (_jumpTimer < 99.0f) _jumpTimer += delta;

		// --- State dispatch ---
		switch (_state)
		{
			case PlayerMovementState.OnGround:
				UpdateOnGround(delta);
				break;
			case PlayerMovementState.InAir:
				UpdateInAir(delta);
				break;
		}
	}

	// ===================================================================
	//  STATE: OnGround
	// ===================================================================
	private void UpdateOnGround(float delta)
	{
		ApplyGravity(delta);
		UpdateCoyoteAndJumpBuffer(delta);

		bool wantJump = _jumpBufferTimer > 0.0f;

		// Jump from ground (normal buffer or hold-Space-on-landing)
		if (wantJump || _holdJumpOnLanding)
		{
			_holdJumpOnLanding = false;
			_velocity.Y = JumpVelocity;
			_jumpBufferTimer = 0.0f;
			_jumpTimer = 0.0f;
			_player.BumpCamera(JumpCamBump);
			TransitionTo(PlayerMovementState.InAir);
			return;
		}

		// Ground movement
		ApplyGroundMovement(delta);

		// Lose ground (walked off edge)
		if (!_player.IsOnFloor())
		{
			TransitionTo(PlayerMovementState.InAir);
			return;
		}

		ApplyVelocityAndMove(delta);
	}

	// ===================================================================
	//  STATE: InAir
	// ===================================================================
	private void UpdateInAir(float delta)
	{
		ApplyGravity(delta);
		UpdateCoyoteAndJumpBuffer(delta);

		bool wantJump = _jumpBufferTimer > 0.0f;

		// Wall jump — chainable with cooldown
		if (wantJump && _wallJumpCooldown <= 0.0f)
		{
			Vector3 wallNormal = FindWallNormal();
			if (wallNormal != Vector3.Zero)
			{
				// Speed towards the wall — use pre-collision velocity so wall hit doesn't eat it
				float speedIntoWall = Mathf.Max(0.0f, -_preCollisionVelocity.Dot(wallNormal));
				float pushStrength = WallJumpHorizontalForce + WallJumpSpeedMultiplier * Mathf.Log(1.0f + speedIntoWall);
				_velocity = wallNormal * pushStrength;

				// Add input direction along the wall — boost when running parallel to it
				Vector3 inputDir = GetInputDirection();
				if (inputDir != Vector3.Zero)
				{
					float inputIntoWall = Mathf.Max(0.0f, -inputDir.Dot(wallNormal));
					float dot = inputDir.Dot(wallNormal);
					Vector3 alongWall = inputDir - wallNormal * dot;
					if (alongWall.LengthSquared() > 0.001f)
					{
						// Less sideways boost the more you're running INTO the wall
						float alongFactor = 1.0f - inputIntoWall;
						_velocity += alongWall.Normalized() * SprintSpeed * alongFactor;
					}
				}

				_velocity.Y = WallJumpVerticalForce;
				_wallJumpCooldown = WallJumpCooldownTime;
				_airControlPenalty = WallJumpAirPenaltyDuration;
				_jumpBufferTimer = 0.0f;
				_player.BumpCamera(JumpCamBump * 1.5f);
				ApplyVelocityAndMove(delta);
				return;
			}
		}

		// Coyote-time jump (normal in-air jump from edge)
		if (wantJump && _coyoteTimer > 0.0f)
		{
			_velocity.Y = JumpVelocity;
			_coyoteTimer = 0.0f;
			_jumpBufferTimer = 0.0f;
			_player.BumpCamera(JumpCamBump);
			ApplyVelocityAndMove(delta);
			return;
		}

		// Air movement
		ApplyAirMovement(delta);
		ApplyVelocityAndMove(delta);

		// Climb boost: touching wall + ascending + ready + delay elapsed + pressing W + headroom
		if (_player.IsOnWall() && _velocity.Y > 0.0f && _climbReady && _jumpTimer >= ClimbStartDelay && _climbCooldown <= 0.0f && Input.IsActionPressed("MoveForward") && HasClimbClearance())
		{
			DoClimbBoost();
		}

		// Landed?
		if (_player.IsOnFloor())
		{
			TransitionTo(PlayerMovementState.OnGround);
		}
	}





	// ===================================================================
	//  HELPERS
	// ===================================================================

	private void TransitionTo(PlayerMovementState newState)
	{
		_state = newState;
	}

	private void ApplyGravity(float delta)
	{
		if (!_player.IsOnFloor())
		{
			_velocity.Y -= Gravity * delta;
		}
		_velocity.Y = Mathf.Max(_velocity.Y, -50.0f);
	}

	private void UpdateCoyoteAndJumpBuffer(float delta)
	{
		if (_player.IsOnFloor())
			_coyoteTimer = CoyoteTime;
		else
			_coyoteTimer -= delta;

		bool spacePressed = Input.IsKeyPressed(Key.Space);
		bool jumpJustPressed = spacePressed && !_spacePrevPressed;
		_spacePrevPressed = spacePressed;

		if (jumpJustPressed)
			_jumpBufferTimer = JumpBufferTime;
		else
			_jumpBufferTimer -= delta;
	}

	private void ApplyGroundMovement(float delta)
	{
		Vector3 inputDir = GetInputDirection();
		bool isSprinting = Input.IsActionPressed("Run");
		float targetSpeed = isSprinting ? SprintSpeed : WalkSpeed;

		if (inputDir != Vector3.Zero)
		{
			Vector3 targetVel = inputDir * targetSpeed;
			_velocity.X = Mathf.Lerp(_velocity.X, targetVel.X, Acceleration * delta);
			_velocity.Z = Mathf.Lerp(_velocity.Z, targetVel.Z, Acceleration * delta);
		}
		else
		{
			_velocity.X = Mathf.Lerp(_velocity.X, 0.0f, Friction * delta);
			_velocity.Z = Mathf.Lerp(_velocity.Z, 0.0f, Friction * delta);
		}
	}

	private void ApplyAirMovement(float delta)
	{
		// Decay air control penalty (smooth recovery)
		if (_airControlPenalty > 0.0f)
		{
			_airControlPenalty -= delta;
			if (_airControlPenalty < 0.0f)
				_airControlPenalty = 0.0f;
		}

		// Smooth factor: 0.15 at start → 1.0 at end
		float penaltyDuration = WallJumpAirPenaltyDuration > 0.001f ? WallJumpAirPenaltyDuration : 0.35f;
		float penaltyT = 1.0f - Mathf.Clamp(_airControlPenalty / penaltyDuration, 0.0f, 1.0f);
		float airAccelFactor = Mathf.SmoothStep(0.15f, 1.0f, penaltyT);

		Vector3 inputDir = GetInputDirection();

		if (inputDir != Vector3.Zero)
		{
			// Direction reversal penalty: harder to turn around in air
			float turnPenalty = 1.0f;
			Vector3 currentH = new Vector3(_velocity.X, 0.0f, _velocity.Z);
			float currentSpeed = currentH.Length();
			if (currentSpeed > 0.1f)
			{
				float alignment = inputDir.Dot(currentH / currentSpeed);
				if (alignment < 0.0f)
				{
					// Input opposes current velocity — scale acceleration down
					float t = 1.0f + alignment; // 0 at full opposite, 1 at perpendicular
					turnPenalty = Mathf.Lerp(AirTurnPenalty, 1.0f, t);
				}
			}

			Vector3 targetVel = inputDir * SprintSpeed;
			float effectiveAccel = AirAcceleration * airAccelFactor * turnPenalty;
			_velocity.X = Mathf.Lerp(_velocity.X, targetVel.X, effectiveAccel * delta);
			_velocity.Z = Mathf.Lerp(_velocity.Z, targetVel.Z, effectiveAccel * delta);
		}
		else
		{
			_velocity.X = Mathf.Lerp(_velocity.X, 0.0f, AirFriction * delta);
			_velocity.Z = Mathf.Lerp(_velocity.Z, 0.0f, AirFriction * delta);
		}
	}

	private void ApplyVelocityAndMove(float delta)
	{
		_preCollisionVelocity = _velocity;
		_player.Velocity = _velocity;
		_player.MoveAndSlide();
		_velocity = _player.Velocity;

		if (_player.IsOnFloor() && _velocity.Y < 0.0f)
			_velocity.Y = -0.5f;
	}

	private Vector3 GetInputDirection()
	{
		Vector3 dir = Vector3.Zero;
		if (Input.IsActionPressed("MoveForward"))    dir += Vector3.Forward;
		if (Input.IsActionPressed("MoveBackwards"))  dir += Vector3.Back;
		if (Input.IsActionPressed("MoveLeft"))       dir += Vector3.Left;
		if (Input.IsActionPressed("MoveRight"))      dir += Vector3.Right;

		dir = dir.Rotated(Vector3.Up, _player.Rotation.Y);
		if (dir.LengthSquared() > 0.0f)
			dir = dir.Normalized();
		return dir;
	}

	/// <summary>Check for walls around the player. Returns the wall normal, or zero if none.</summary>
	private Vector3 FindWallNormal()
	{
		var spaceState = _player.GetWorld3D().DirectSpaceState;
		var pos = _player.GlobalPosition;

		Vector3[] directions =
		{
			Vector3.Forward.Rotated(Vector3.Up, _player.Rotation.Y),
			Vector3.Back.Rotated(Vector3.Up, _player.Rotation.Y),
			Vector3.Left.Rotated(Vector3.Up, _player.Rotation.Y),
			Vector3.Right.Rotated(Vector3.Up, _player.Rotation.Y),
		};

		foreach (Vector3 dir in directions)
		{
			var query = PhysicsRayQueryParameters3D.Create(
				pos + Vector3.Up * 0.6f,
				pos + dir * WallDetectionDistance + Vector3.Up * 0.6f,
				(uint)1
			);
			var result = spaceState.IntersectRay(query);
			if (result.Count > 0)
			{
				Vector3 normal = (Vector3)result["normal"];
				float angleUp = normal.AngleTo(Vector3.Up);
				if (angleUp > Mathf.DegToRad(70.0f) && angleUp < Mathf.DegToRad(110.0f))
					return normal;
			}
		}
		return Vector3.Zero;
	}

	/// <summary>Check if there's enough headroom above the wall for the player to stand on top.</summary>
	private bool HasClimbClearance()
	{
		if (!_player.IsOnWall())
			return false;

		// Use the actual wall normal from the last MoveAndSlide collision
		Vector3 wallNormal = _player.GetWallNormal();
		float checkHeight = PlayerHeight + ClimbClearanceHeight;

		// Cast from slightly in front of the wall towards it, at head height.
		// If we hit something (wall continues up or another block on top),
		// there's no room to stand. Exclude the player's own body.
		var spaceState = _player.GetWorld3D().DirectSpaceState;
		Vector3 origin = _player.GlobalPosition + Vector3.Up * checkHeight + wallNormal * 0.2f;
		var query = PhysicsRayQueryParameters3D.Create(origin, origin - wallNormal * 1.0f, (uint)1);
		query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };
		var result = spaceState.IntersectRay(query);

		return result.Count <= 0;
	}

	/// <summary>Boost player up and slightly forward — like a wall-assisted second jump.</summary>
	private void DoClimbBoost()
	{
		Vector3 boostDir = Vector3.Forward.Rotated(Vector3.Up, _player.Rotation.Y);
		_velocity = boostDir * ClimbBoostHorizontal + Vector3.Up * ClimbBoostVertical;
		_player.Velocity = _velocity;
		_climbReady = false;
		_climbCooldown = ClimbCooldownTime;
		_player.BumpCamera(JumpCamBump * 1.2f);
	}
}
