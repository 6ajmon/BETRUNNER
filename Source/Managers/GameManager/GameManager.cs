using Godot;
using System;
using System.Collections.Generic;

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

	// ── Level progression ───────────────────────────────────────────────────
	private int _currentLevelIndex = -1;

	/// <summary>
	/// Przeciągnij sceny poziomów w kolejności (0, 1, 2…).
	/// ID poziomu generowane automatycznie: "Level1", "Level2" …
	/// </summary>
	[Export] private PackedScene[] _levelScenes;

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

	// ── Level progression helpers ───────────────────────────────────────────

	private string GetLevelIdForIndex(int index) => $"Level{index + 1}";

	private bool TryAdvanceToNextLevel()
	{
		if (_levelScenes == null || _levelScenes.Length == 0)
		{
			GD.PrintErr("[GameManager] _levelScenes not configured in Inspector!");
			return false;
		}

		int nextIndex = _currentLevelIndex + 1;
		if (nextIndex >= _levelScenes.Length)
		{
			GD.Print("[GameManager] All levels completed!");
			return false;
		}

		_currentLevelIndex = nextIndex;
		string levelId = GetLevelIdForIndex(_currentLevelIndex);

		// Tell CountdownManager which level we're entering
		CountdownManager.Instance.SetCurrentLevel(levelId);

		// Load the scene from the PackedScene reference
		string path = _levelScenes[_currentLevelIndex].ResourcePath;
		Callable.From(() => SceneManager.Instance.ChangeSceneByPathAsync(path)).CallDeferred();
		return true;
	}

	// ── Signal handlers ─────────────────────────────────────────────────────

	private async void OnPlayButton_Pressed()
	{
		CurrentState = gameState.Loading;

		// Initialise the first level's time allocation
		if (_currentLevelIndex < 0)
		{
			TryAdvanceToNextLevel(); // sets index to 0, calls SetCurrentLevel + loads scene
		}
	}
	
	private void OnPreviewCameraLoaded()
	{
		CurrentState = gameState.Preview;
		SceneManager.Instance.ShowBettingOverlay();
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToPreviewCamera));

		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void OnEndBettingPhase()
	{
		// 1. Consume the bet from the player's time pool
		CountdownManager.Instance.PlaceBet();

		// 2. Start the countdown with the time the player bet
		double betTime = CountdownManager.Instance.CurrentBetTime;
		EmitSignal(nameof(StartCountdown), betTime);

		// 3. Switch to first-person view and enable controls
		CurrentState = gameState.Waiting;
		SceneManager.Instance.ShowGameOverlay();
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToFirstPersonCamera));
		if (PlayerCharacter != null)
			PlayerCharacter.EnablePlayerControls();
	}

	private void OnStartCountdown(double time)
	{
		CurrentState = gameState.Countdown;
	}

	private void OnCountdownPaused()
	{
		// Timer ran out — the player used all of their bet time
		double betTime = CountdownManager.Instance.CurrentBetTime;
		CountdownManager.Instance.OnLevelFinished(betTime);

		// Advance to the next level
		CurrentState = gameState.Loading;
		TryAdvanceToNextLevel();
	}

	/// <summary>
	/// Call this when the player successfully completes the current level
	/// (e.g. reaching the exit trigger) before the timer runs out.
	/// </summary>
	public void CompleteCurrentLevel(double actualTimeUsed)
	{
		CountdownManager.Instance.OnLevelFinished(actualTimeUsed);
		CurrentState = gameState.Loading;
		TryAdvanceToNextLevel();
	}

	public void MovePlayerToSpawn(Vector3 facingDirection)
	{
		PlayerCharacter.GlobalPosition = SpawnPosition;
		PlayerCharacter.LookAtDirection(facingDirection);
	}
}
