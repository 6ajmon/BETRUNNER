using Godot;

/// <summary>
/// Controls the speed lines shader effect based on player velocity.
/// Attach this script to a CanvasLayer node in the scene.
/// It creates its own full-screen ColorRect with the shader.
/// </summary>
public partial class MotionShaderController : CanvasLayer
{
	[ExportGroup("Speed")]
	[Export] public float SpeedThreshold { get; set; } = 7.0f;
	[Export] public float MaxEffectSpeed { get; set; } = 15.0f;

	[ExportGroup("Smoothing")]
	[Export] public float SmoothSpeed { get; set; } = 4.0f;

	[ExportGroup("Rendering")]
	[Export] public int RenderLayer { get; set; } = 1;

	[Export] private ShaderMaterial _material;

	private ColorRect _overlay;
	private float _currentPower;
	private Player _player;

	public override void _Ready()
	{
		Layer = RenderLayer;

		// Create full-screen ColorRect for the shader
		_overlay = new ColorRect();
		_overlay.Name = "SpeedLinesOverlay";
		_overlay.Color = Colors.Transparent;
		_overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
		_overlay.AnchorRight = 1.0f;
		_overlay.AnchorBottom = 1.0f;
		AddChild(_overlay);

		if (_material == null)
		{
			GD.PrintErr("[SpeedLines] ShaderMaterial not assigned in inspector.");
			return;
		}

		_overlay.Material = _material;

		_material.SetShaderParameter("effect_power", 0.0f);
	}

	public override void _Process(double delta)
	{
		if (_material == null)
			return;

		if (_player == null)
		{
			_player = GameManager.Instance?.PlayerCharacter;
			if (_player == null)
				return;
		}

		// Ensure overlay fills viewport
		Vector2 vp = GetViewport().GetVisibleRect().Size;
		if (_overlay.Size != vp)
			_overlay.Size = vp;

		// Horizontal speed
		Vector3 hVel = new Vector3(_player.Velocity.X, 0, _player.Velocity.Z);
		float speed = hVel.Length();

		// Direction factor: only show lines when looking where you're moving
		float dirFactor = 0.0f;
		if (speed > 0.1f)
		{
			Vector3 moveDir = hVel / speed;
			Vector3 camForward = -_player.GetNode<Camera3D>("Camera3D").GlobalTransform.Basis.Z;
			camForward.Y = 0;
			camForward = camForward.Normalized();
			dirFactor = Mathf.Clamp(moveDir.Dot(camForward), 0.0f, 1.0f);
		}

		// Map speed × direction to effect_power (0..1)
		float target = speed > SpeedThreshold
			? Mathf.Clamp((speed - SpeedThreshold) / (MaxEffectSpeed - SpeedThreshold), 0.0f, 1.0f)
			: 0.0f;
		target *= dirFactor;

		_currentPower = Mathf.Lerp(_currentPower, target, (float)delta * SmoothSpeed);

		_material.SetShaderParameter("effect_power", _currentPower);
	}
}
