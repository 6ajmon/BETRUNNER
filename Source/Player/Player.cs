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

	[Export] private Camera3D _camera;
	[Export] private PlayerMovement _movement;
	private float _pitch;
	private float _currentFov;
	private float _cameraBump;

	public override void _Ready()
	{
		if (_camera == null)
			_camera = GetNode<Camera3D>("Camera3D");
		if (_movement == null)
			_movement = GetNode<PlayerMovement>("Movement");

		_currentFov = DefaultFov;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	public float CameraPitch => _pitch;

	public void BumpCamera(float amount)
	{
		_cameraBump += amount;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseBtn && mouseBtn.ButtonIndex == MouseButton.Left)
		{
			if (Input.MouseMode == Input.MouseModeEnum.Visible)
				Input.MouseMode = Input.MouseModeEnum.Captured;
		}

		if (@event is InputEventMouseMotion motion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			RotateY(-motion.Relative.X * MouseSensitivity);

			_pitch -= motion.Relative.Y * MouseSensitivity;
			_pitch = Mathf.Clamp(_pitch, Mathf.DegToRad(-90.0f), Mathf.DegToRad(90.0f));
		}

		if (@event is InputEventKey key && key.Keycode == Key.Escape && key.Pressed)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;

		_movement?.HandleMovement(dt);

		// Apply camera pitch + bump (bump decays over time)
		_camera.Rotation = new Vector3(_pitch + _cameraBump, 0, 0);
		_cameraBump = Mathf.Lerp(_cameraBump, 0.0f, CameraBumpRecovery * dt);
		if (Mathf.Abs(_cameraBump) < 0.0005f)
			_cameraBump = 0.0f;

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
}
