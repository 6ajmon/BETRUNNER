using Godot;

public enum PlayerAnimState
{
	Idle,
	Walk,
	Run,
	Jump,
	Fall,
	WallJump,
	Climb,
}

public partial class PlayerAnimation : Node
{
	[ExportGroup("Animation Names (from Arms.glb)")]
	[Export] public string AnimIdle { get; set; } = "mixamo_com_003";
	[Export] public string AnimWalk { get; set; } = "Walk";
	[Export] public string AnimRun { get; set; } = "mixamo_com_006";
	[Export] public string AnimJump { get; set; } = "mixamo_com_005";
	[Export] public string AnimFall { get; set; } = "mixamo_com_002";
	[Export] public string AnimWallJump { get; set; } = "mixamo_com_004";
	[Export] public string AnimClimb { get; set; } = "mixamo_com_005";

	[ExportGroup("Settings")]
	[Export] public float CrossfadeTime { get; set; } = 0.1f;
	[Export] public float MoveThreshold { get; set; } = 0.5f;

	[ExportGroup("Fall Animation (mixamo_com_002)")]
	[Export] public float FallYOffset { get; set; } = -0.4f;
	[Export] public float FallZOffset { get; set; } = 0.1f;

	[ExportGroup("WallJump Animation (mixamo_com_004)")]
	[Export] public float WallJumpClipLength { get; set; } = 0.7f;
	[Export] public float WallJumpSpeedScale { get; set; } = 1.8f;
	[Export] public float WallJumpYOffset { get; set; } = -1.4f;

	private Player _player;
	private PlayerMovement _movement;
	private AnimationPlayer _animPlayer;
	private Node3D _arms;
	private Vector3 _armsOriginalPosition;
	private Vector3 _armsOriginalRotation;

	private PlayerAnimState _currentState = PlayerAnimState.Idle;
	private PlayerAnimState _previousState = PlayerAnimState.Idle;

	public bool IsLocked => _lockedState.HasValue;
	public PlayerAnimState CurrentAnimState => _currentState;

	// Non-interruptible state lock (e.g. WallJump, Climb)
	private PlayerAnimState? _lockedState;
	private string _lockedAnimName;
	private float _lockedClipLength;
	private float _lockedSpeedScale;
	private float _lockedYOffset;

	public override void _Ready()
	{
		_player = GetParent<Player>();
		_movement = _player.GetNode<PlayerMovement>("Movement");
		_animPlayer = _player.GetNode<AnimationPlayer>("Arms/AnimationPlayer");
		_arms = _player.GetNode<Node3D>("Arms");
		_armsOriginalPosition = _arms.Position;
		_armsOriginalRotation = _arms.Rotation;
		// Model faces backward (+Z) by default — offset by PI (yaw bias is per-frame in Player.cs)
		_armsOriginalRotation.Y += Mathf.Pi;

		if (_animPlayer == null)
		{
			GD.PrintErr("PlayerAnimation: AnimationPlayer not found in Arms!");
			SetProcess(false);
			return;
		}

		// Play idle by default
		PlayAnimation(PlayerAnimState.Idle, 0.0f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_animPlayer == null)
			return;

		// Check if locked animation has finished
		if (_lockedState.HasValue)
		{
			bool animFinished = !_animPlayer.IsPlaying()
				|| _animPlayer.CurrentAnimationPosition >= _lockedClipLength - 0.05f;

			if (animFinished)
			{
				// Unlock — restore position, rotation, normal speed
				_arms.Position = _armsOriginalPosition;
				_arms.Rotation = _armsOriginalRotation;
				_lockedState = null;
				_lockedAnimName = null;
				_animPlayer.SpeedScale = 1.0f;
				_previousState = PlayerAnimState.Idle; // force re-eval next frame
			}
			else
			{
				// Still locked — keep the locked animation playing
				return;
			}
		}

		PlayerAnimState newState = DetermineState();
		_currentState = newState;

		if (newState != _previousState)
		{
			// Restore position when leaving Fall
			if (_previousState == PlayerAnimState.Fall)
				_arms.Position = _armsOriginalPosition;

			if (newState == PlayerAnimState.WallJump)
			{
				StartLockedAnimation(newState, WallJumpClipLength, WallJumpSpeedScale, WallJumpYOffset);
			}
			else if (newState == PlayerAnimState.Fall)
			{
				PlayAnimation(newState, CrossfadeTime);
				_arms.Position = _armsOriginalPosition + new Vector3(0, FallYOffset, FallZOffset);
				_previousState = newState;
			}
			else
			{
				PlayAnimation(newState, CrossfadeTime);
				_previousState = newState;
			}
		}
	}

	private void StartLockedAnimation(PlayerAnimState state, float clipLength, float speedScale, float yOffset)
	{
		string animName = GetAnimationName(state);
		PlayAnimationDirect(animName, CrossfadeTime);

		// Apply Y offset to arms for the duration
		_arms.Position = _armsOriginalPosition + new Vector3(0, yOffset, 0);

		// For wall jump: rotate arms to face the wall (forward = -Z, look toward wall normal)
		if (state == PlayerAnimState.WallJump)
		{
			Vector3 wallNormal = _movement.LastWallJumpNormal;
			if (wallNormal.LengthSquared() > 0.001f)
			{
				Vector3 wallDir = new Vector3(wallNormal.X, 0, wallNormal.Z).Normalized();
				_arms.LookAt(_arms.GlobalPosition + wallDir, Vector3.Up);
			}
		}

		_lockedState = state;
		_lockedAnimName = animName;
		_lockedClipLength = clipLength;
		_lockedSpeedScale = speedScale;
		_lockedYOffset = yOffset;
		_animPlayer.SpeedScale = speedScale;
		_previousState = state;
	}

	private PlayerAnimState DetermineState()
	{
		// Priority: one-shot / interrupt states first
		if (_movement.IsClimbing)
			return PlayerAnimState.Climb;

		if (_movement.IsWallJumping)
			return PlayerAnimState.WallJump;

		if (_movement.CurrentState == PlayerMovementState.InAir)
		{
			// Rising = jump (includes wall jump if the flag just fired this frame,
			// but we already caught WallJump above — this covers normal jumps)
			if (_player.Velocity.Y > 0.0f && !_player.IsOnFloor())
				return PlayerAnimState.Jump;

			// Falling = fall
			return PlayerAnimState.Fall;
		}

		// OnGround states
		bool isMoving = _player.Velocity.Length() > MoveThreshold;

		if (!isMoving)
			return PlayerAnimState.Idle;

		if (_movement.IsSprinting)
			return PlayerAnimState.Run;

		return PlayerAnimState.Walk;
	}

	private void PlayAnimation(PlayerAnimState state, float blend)
	{
		string animName = GetAnimationName(state);
		PlayAnimationDirect(animName, blend);
	}

	private void PlayAnimationDirect(string animName, float blend)
	{
		if (string.IsNullOrEmpty(animName))
		{
			GD.PrintErr("PlayerAnimation: No animation name provided");
			return;
		}

		if (!_animPlayer.HasAnimation(animName))
		{
			GD.PrintErr($"PlayerAnimation: Animation '{animName}' not found in AnimationPlayer");
			return;
		}

		if (blend > 0.0f)
			_animPlayer.Play(animName, customBlend: blend);
		else
			_animPlayer.Play(animName);
	}

	private string GetAnimationName(PlayerAnimState state) => state switch
	{
		PlayerAnimState.Idle => AnimIdle,
		PlayerAnimState.Walk => AnimWalk,
		PlayerAnimState.Run => AnimRun,
		PlayerAnimState.Jump => AnimJump,
		PlayerAnimState.Fall => AnimFall,
		PlayerAnimState.WallJump => AnimWallJump,
		PlayerAnimState.Climb => AnimClimb,
		_ => AnimIdle,
	};
}
