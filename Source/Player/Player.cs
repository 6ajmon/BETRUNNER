using Godot;

public partial class Player : CharacterBody3D
{
	[ExportGroup("Mouse")]
	[Export] public float MouseSensitivity { get; set; } = 0.002f;

	[ExportGroup("Camera")]
	[Export] public float DefaultFov { get; set; } = 75.0f;
	[Export] public float MaxFov { get; set; } = 100.0f;
	[Export] public float FovLerpSpeed { get; set; } = 8.0f;
	[Export] public float CameraBumpRecovery { get; set; } = 10.0f;

	[ExportGroup("Arms")]
	[Export] public float DefaultArmYaw { get; set; } = Mathf.DegToRad(30.0f);
	[Export] public float ArmYawSpeed { get; set; } = 8.0f;

	[Export] private Camera3D _camera;
	[Export] private PlayerMovement _movement;
	[Export] private PlayerAnimation _animation;

	private Node3D _arms;
	private float _pitch;
	private float _smoothedArmGlobalYaw;
	private float _previousBodyYaw;
	private float _currentFov;
	private float _cameraBump;

	public override void _Ready()
	{
		if (_camera == null)
			_camera = GetNode<Camera3D>("Camera3D");
		if (_movement == null)
			_movement = GetNode<PlayerMovement>("Movement");
		if (_animation == null)
			_animation = GetNode<PlayerAnimation>("Animation");

		_arms = GetNode<Node3D>("Arms");
		_smoothedArmGlobalYaw = Rotation.Y + Mathf.Pi;
		_previousBodyYaw = Rotation.Y;
		_currentFov = DefaultFov;
		GameManager.Instance.PlayerCharacter = this;
		CameraManager.Instance.PlayerCamera = _camera;
	}

	public float CameraPitch => _pitch;

	public void BumpCamera(float amount)
	{
		_cameraBump += amount;
	}
	
	public void LookAtDirection(Vector3 direction)
{
    direction = direction.Normalized();
    
    Rotation = new Vector3(
        Rotation.X,
        Mathf.Atan2(-direction.X, -direction.Z),
        Rotation.Z
    );
    
    _pitch = Mathf.Asin(direction.Y);
}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			if (Input.MouseMode == Input.MouseModeEnum.Visible
				&& GameManager.Instance.CurrentState != GameManager.gameState.Preview
				&& GameManager.Instance.CurrentState != GameManager.gameState.Loading
				&& GameManager.Instance.CurrentState != GameManager.gameState.MainMenu)
			{
				Input.MouseMode = Input.MouseModeEnum.Captured;
			}
		}

		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			// Body rotates instantly with mouse — no gameplay impact
			RotateY(-motion.Relative.X * MouseSensitivity);

			// Vertical: camera pitch
			_pitch -= motion.Relative.Y * MouseSensitivity;
			_pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-90.0f), Mathf.DegToRad(90.0f));
		}

		if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	/// <summary>
	/// Called when betting ends — enables mouse look and movement.
	/// </summary>
	public void EnablePlayerControls()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	/// <summary>
	/// Show/hide the player's 3D visuals (body mesh + arms).
	/// Hide when preview camera is active, show for first-person.
	/// </summary>
	public void SetVisible(bool visible)
	{
		var mesh = GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		if (mesh != null) mesh.Visible = visible;

		var arms = GetNodeOrNull<Node3D>("Arms");
		if (arms != null) arms.Visible = visible;

		// Speed lines overlay — hide during preview
		var speedLines = GetNodeOrNull<ColorRect>("Camera3D/SpeedLines");
		if (speedLines != null) speedLines.Visible = visible;
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		_movement?.HandleMovement(dt);

		// Camera follows body instantly + pitch
		_camera.Rotation = new Vector3(_pitch + _cameraBump, 0, 0);
		_cameraBump = Mathf.Lerp(_cameraBump, 0.0f, CameraBumpRecovery * dt);
		if (Mathf.Abs(_cameraBump) < 0.0005f)
			_cameraBump = 0.0f;

		// Arms: purely cosmetic smooth follow (does NOT affect movement or camera)
		if (_arms != null && (_animation == null || !_animation.IsLocked))
		{
			PlayerAnimState animState = _animation?.CurrentAnimState ?? PlayerAnimState.Idle;
			bool useBias = animState == PlayerAnimState.Idle || animState == PlayerAnimState.Fall;
			float bias = useBias ? DefaultArmYaw : 0.0f;

			// Target = body's current global Y + PI (model flip) + bias
			float targetGlobalYaw = Rotation.Y + Mathf.Pi + bias;

			// Smoothly catch up — purely visual lag
			_smoothedArmGlobalYaw = Mathf.LerpAngle(_smoothedArmGlobalYaw, targetGlobalYaw, ArmYawSpeed * dt);

			// Convert smoothed global to local (relative to body)
			_arms.Rotation = new Vector3(0, _smoothedArmGlobalYaw - Rotation.Y, 0);
		}

		// Dynamic FOV — direction-based
		Vector3 horizontalVel = new Vector3(Velocity.X, 0, Velocity.Z);
		float speed = horizontalVel.Length();
		float maxSpeed = _movement?.MaxSprintSpeed ?? 10.0f;
		float speedRatio = Mathf.Clamp(speed / maxSpeed, 0.0f, 1.0f);

		// Direction factor: dot(moveDir, cameraForward) → 1 = full FOV, 0 = no FOV
		float dirFactor = 0.0f;
		if (speed > 0.1f)
		{
			Vector3 moveDir = horizontalVel / speed;
			Vector3 camForward = -_camera.GlobalTransform.Basis.Z;
			camForward.Y = 0;
			camForward = camForward.Normalized();
			dirFactor = Mathf.Clamp(moveDir.Dot(camForward), 0.0f, 1.0f);
		}

		float t = speedRatio * dirFactor;
		float targetFov = Mathf.Lerp(DefaultFov, MaxFov, t);
		_currentFov = Mathf.Lerp(_currentFov, targetFov, FovLerpSpeed * dt);
		_camera.Fov = _currentFov;
	}

	public Vector3 GetFacingDirection()
	{
		return _camera.GlobalTransform.Basis.Z.Normalized();
	}
}
