using Godot;
using System;

public partial class GameManager : Node
{
	public static GameManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameManager>("GameManager");
	[Signal] public delegate void StartCountdownEventHandler(double time);
	[Signal] public delegate void StopCountdownEventHandler();
	[Signal] public delegate void EndBettingPhaseEventHandler();
	[Signal] public delegate void PreviewCameraLoadedEventHandler();

	public enum gameState
	{
		Loading,
		Preview,
		Waiting,
		Countdown
	}

	public gameState CurrentState { get; set; }
	
	public Vector3 SpawnPosition { get; set; }
	public Player PlayerCharacter { get; set; }
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		CurrentState = gameState.Loading;
		StartCountdown += onStartCountdown;
		PreviewCameraLoaded += onPreviewCameraLoaded;
		EndBettingPhase += onEndBettingPhase;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}
	
	private void onPreviewCameraLoaded()
	{
		CurrentState = gameState.Preview;
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToPreviewCamera));
	}

	private void onEndBettingPhase()
	{
		CurrentState = gameState.Waiting;
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToFirstPersonCamera));
	}

	private void onStartCountdown(double time)
	{
		CurrentState = gameState.Countdown;
	}

	public void movePlayerToSpawn(Vector3 facingDirection)
	{
		PlayerCharacter.GlobalPosition = SpawnPosition;
		PlayerCharacter.LookAtDirection(facingDirection);
	}
}
