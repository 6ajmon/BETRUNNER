using Godot;
using System;

public partial class GameManager : Node
{
	public static GameManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameManager>("GameManager");

	[Signal]
	public delegate void PlayButtonPressedEventHandler();
	[Signal] public delegate void StartCountdownEventHandler(double time);
	[Signal] public delegate void StopCountdownEventHandler();
	[Signal] public delegate void CountdownPausedEventHandler();
	[Signal] public delegate void EndBettingPhaseEventHandler();
	[Signal] public delegate void PreviewCameraLoadedEventHandler();
	

	public enum gameState
	{
		MainMenu,
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
		CurrentState = gameState.MainMenu;
		StartCountdown += OnStartCountdown;
		CountdownPaused += OnCountdownPaused;
		PreviewCameraLoaded += OnPreviewCameraLoaded;
		EndBettingPhase += OnEndBettingPhase;
		PlayButtonPressed += OnPlayButton_Pressed;
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		
	}

	private async void OnPlayButton_Pressed()
	{
		CurrentState = gameState.Loading;
		Callable.From(() => SceneManager.Instance.ChangeSceneByPathAsync("res://Source/LevelScenes/level_zero.tscn")).CallDeferred();
	}
	
	private void OnPreviewCameraLoaded()
	{
		CurrentState = gameState.Preview;
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToPreviewCamera));
	}

	private void OnEndBettingPhase()
	{
		CurrentState = gameState.Waiting;
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToFirstPersonCamera));
	}

	private void OnStartCountdown(double time)
	{
		CurrentState = gameState.Countdown;
	}

	private void OnCountdownPaused()
	{
		CurrentState = gameState.Loading;
		Callable.From(() => SceneManager.Instance.ChangeSceneByPathAsync("res://Source/LevelScenes/level_one.tscn")).CallDeferred();
	}

	public void MovePlayerToSpawn(Vector3 facingDirection)
	{
		PlayerCharacter.GlobalPosition = SpawnPosition;
		PlayerCharacter.LookAtDirection(facingDirection);
	}
}
