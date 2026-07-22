using Godot;

public enum PlayerMovementState
{
	OnGround,
	InAir,
	WallClimbing,
	ClimbingLedge,
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

	[ExportGroup("Jump")]
	[Export] public float JumpVelocity { get; set; } = 9.0f;
	[Export] public float Gravity { get; set; } = 28.0f;
	[Export] public float CoyoteTime { get; set; } = 0.08f;
	[Export] public float JumpBufferTime { get; set; } = 0.1f;
	[Export] public float JumpCamBump { get; set; } = 0.03f;
	[Export] public float LandCamBump { get; set; } = -0.04f;

	[ExportGroup("Wall Jump")]
	[Export] public float WallJumpHorizontalForce { get; set; } = 18.0f;
	[Export] public float WallJumpVerticalForce { get; set; } = 10.0f;
	[Export] public float WallDetectionDistance { get; set; } = 0.6f;

	[ExportGroup("Ledge Climb")]
	[Export] public float LedgeReachDistance { get; set; } = 1.2f;
	[Export] public float LedgeClimbDuration { get; set; } = 0.3f;
	[Export] public float LedgeMaxHeightAboveHead { get; set; } = 1.5f;
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
	private bool _wallJumpUsedThisAir;
	private bool _wasOnFloor;

	// Ledge climb
	private float _ledgeClimbProgress;
	private Vector3 _ledgeStartPos;
	private Vector3 _ledgeEndPos;
	private Vector3 _wallClimbForward;

	// Jump input tracking (Space key) — reset when entering WallClimbing
	private bool _spacePrevPressed;

	public override void _Ready()
	{
		_player = GetParent<Player>();
		_state = PlayerMovementState.OnGround;
		_wasOnFloor = true;
	}

	public void HandleMovement(float delta)
	{
		// --- Detect landing (floor transition) ---
		bool isOnFloor = _player.IsOnFloor();
		if (!_wasOnFloor && isOnFloor)
		{
			// Landed — reset air-only flags
			_wallJumpUsedThisAir = false;
			_player.BumpCamera(LandCamBump);
		}
		_wasOnFloor = isOnFloor;

		// --- State dispatch ---
		switch (_state)
		{
			case PlayerMovementState.OnGround:
				UpdateOnGround(delta);
				break;
			case PlayerMovementState.InAir:
				UpdateInAir(delta);
				break;
			case PlayerMovementState.WallClimbing:
				UpdateWallClimbing(delta);
				break;
			case PlayerMovementState.ClimbingLedge:
				UpdateClimbingLedge(delta);
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

		// Jump from ground
		if (wantJump)
		{
			_velocity.Y = JumpVelocity;
			_jumpBufferTimer = 0.0f;
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

		// Wall jump — only once per airtime
		if (wantJump && !_wallJumpUsedThisAir)
		{
			Vector3 wallNormal = FindWallNormal();
			if (wallNormal != Vector3.Zero)
			{
				// Push strongly away from the wall
				_velocity = wallNormal * WallJumpHorizontalForce;
				_velocity.Y = WallJumpVerticalForce;
				_wallJumpUsedThisAir = true;
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

		// Ledge grab check (only when falling)
		if (_velocity.Y <= 0.0f)
		{
			TryLedgeGrab();
			if (_state == PlayerMovementState.WallClimbing)
				return;
		}

		// Air movement
		ApplyAirMovement(delta);
		ApplyVelocityAndMove(delta);

		// Landed?
		if (_player.IsOnFloor())
		{
			TransitionTo(PlayerMovementState.OnGround);
		}
	}

	// ===================================================================
	//  STATE: WallClimbing (hanging on ledge)
	// ===================================================================
	private void UpdateWallClimbing(float delta)
	{
		// Zero velocity while hanging
		_velocity = Vector3.Zero;
		_player.Velocity = Vector3.Zero;

		// Climb up with Space
		bool spacePressed = Input.IsKeyPressed(Key.Space);
		bool jumpJustPressed = spacePressed && !_spacePrevPressed;
		_spacePrevPressed = spacePressed;

		if (jumpJustPressed)
		{
			// Start climb animation
			_ledgeStartPos = _player.GlobalPosition;
			_ledgeEndPos += _wallClimbForward * 0.3f;
			_ledgeClimbProgress = 0.0f;
			TransitionTo(PlayerMovementState.ClimbingLedge);
			return;
		}

		// Let go with S / backward
		if (Input.IsActionPressed("MoveBackwards") || Input.IsActionJustPressed("MoveLeft") || Input.IsActionJustPressed("MoveRight"))
		{
			_velocity = -_wallClimbForward * 2.0f;
			TransitionTo(PlayerMovementState.InAir);
			return;
		}
	}

	// ===================================================================
	//  STATE: ClimbingLedge (locked animation upward)
	// ===================================================================
	private void UpdateClimbingLedge(float delta)
	{
		_ledgeClimbProgress += delta / LedgeClimbDuration;

		if (_ledgeClimbProgress >= 1.0f)
		{
			_player.GlobalPosition = _ledgeEndPos;
			_velocity = Vector3.Zero;
			_player.Velocity = Vector3.Zero;
			TransitionTo(PlayerMovementState.OnGround);
			return;
		}

		float t = Mathf.SmoothStep(0.0f, 1.0f, _ledgeClimbProgress);
		_player.GlobalPosition = _ledgeStartPos.Lerp(_ledgeEndPos, t);
		_player.Velocity = Vector3.Zero;
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
		Vector3 inputDir = GetInputDirection();

		if (inputDir != Vector3.Zero)
		{
			Vector3 targetVel = inputDir * SprintSpeed; // use sprint speed as max in air too
			_velocity.X = Mathf.Lerp(_velocity.X, targetVel.X, AirAcceleration * delta);
			_velocity.Z = Mathf.Lerp(_velocity.Z, targetVel.Z, AirAcceleration * delta);
		}
		else
		{
			_velocity.X = Mathf.Lerp(_velocity.X, 0.0f, AirFriction * delta);
			_velocity.Z = Mathf.Lerp(_velocity.Z, 0.0f, AirFriction * delta);
		}
	}

	private void ApplyVelocityAndMove(float delta)
	{
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

	/// <summary>Try to grab a ledge. Sets state to WallClimbing on success.</summary>
	private void TryLedgeGrab()
	{
		var spaceState = _player.GetWorld3D().DirectSpaceState;
		var pos = _player.GlobalPosition;
		Vector3 forward = Vector3.Forward.Rotated(Vector3.Up, _player.Rotation.Y);

		// Head height — approximate top of the capsule
		float headHeight = PlayerHeight * 0.85f;

		// 1) Cast forward from head height to detect a wall
		var wallQuery = PhysicsRayQueryParameters3D.Create(
			pos + Vector3.Up * headHeight,
			pos + forward * LedgeReachDistance + Vector3.Up * headHeight,
			(uint)1
		);
		var wallResult = spaceState.IntersectRay(wallQuery);
		if (wallResult.Count == 0)
			return;

		Vector3 wallHit = (Vector3)wallResult["position"];
		Vector3 wallNormal = (Vector3)wallResult["normal"];

		// 2) Cast down from far enough above the wall to find the top edge
		float searchTop = headHeight + LedgeMaxHeightAboveHead + 0.5f;
		var downQuery = PhysicsRayQueryParameters3D.Create(
			wallHit + Vector3.Up * searchTop + wallNormal * 0.2f,
			wallHit + Vector3.Up * (headHeight - 0.1f) + wallNormal * 0.2f,
			(uint)1
		);
		var downResult = spaceState.IntersectRay(downQuery);
		if (downResult.Count == 0)
			return;

		Vector3 wallTop = (Vector3)downResult["position"];

		// Check that the ledge is within range (head to head + maxHeightAboveHead)
		float ledgeHeight = wallTop.Y - (pos.Y + headHeight);
		if (ledgeHeight < -0.1f || ledgeHeight > LedgeMaxHeightAboveHead)
			return;

		// 3) Check there's room above the ledge to stand
		var clearQuery = PhysicsRayQueryParameters3D.Create(
			wallTop + Vector3.Up * PlayerHeight,
			wallTop + Vector3.Up * PlayerHeight - forward * 0.4f,
			(uint)1
		);
		var clearResult = spaceState.IntersectRay(clearQuery);
		if (clearResult.Count > 0)
			return; // blocked above

		// ── Grab the ledge! ──
		_wallClimbForward = forward;
		_ledgeEndPos = wallTop + forward * 0.3f;

		_velocity = Vector3.Zero;
		_player.Velocity = Vector3.Zero;
		_spacePrevPressed = false;
		_state = PlayerMovementState.WallClimbing;
	}
}
