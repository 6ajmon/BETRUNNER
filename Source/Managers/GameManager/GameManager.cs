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
	[Export] private PackedScene _mainMenuScene;

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

		// 2. Switch to first-person view and enable controls
		//    (countdown starts later when player walks into EventNode)
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
		// Timer został zatrzymany — weź rzeczywisty czas spędzony na poziomie
		double actualTime = CountdownManager.Instance.ActualTimeUsed;
		double overshoot = Math.Max(0.0, actualTime - CountdownManager.Instance.CurrentBetTime);
		CountdownManager.Instance.OnLevelFinished(actualTime);

		CurrentState = gameState.Loading;

		// Zwolnij myszkę i włącz kamerę podglądu
		Input.MouseMode = Input.MouseModeEnum.Visible;
		CameraManager.Instance.EmitSignal(nameof(CameraManager.SwitchToPreviewCamera));

		// Pokaż odpowiedni ekran podsumowania — przegrana tylko gdy przekroczono zakład i pula jest pusta
		if (overshoot > 0.0 && CountdownManager.Instance.IsEffectivelyBankrupt)
		{
			SceneManager.Instance.ShowFinishOverlay();
			var fail = SceneManager.Instance.GetFailureOverlay();
			if (fail != null) fail.ShowStats();
		}
		else if (_currentLevelIndex >= _levelScenes.Length - 1)
		{
			// Ostatni poziom ukończony — finish overlay z podsumowaniem całej gry
			SceneManager.Instance.ShowFinishOverlay();
			var finish = SceneManager.Instance.GetFailureOverlay();
			if (finish != null) finish.ShowVictoryStats();
		}
		else
		{
			SceneManager.Instance.ShowSummaryOverlay();
			var summary = SceneManager.Instance.GetSummaryOverlay();
			if (summary != null) summary.ShowStats();
		}
	}

	/// <summary>
	/// Called by SummaryOverlay's Continue button.
	/// Advances to the next level or ends the game.
	/// </summary>
	public void ContinueAfterSummary()
	{
		if (_currentLevelIndex >= _levelScenes.Length - 1)
		{
			// Wszystkie poziomy ukończone — wróć do menu
			ReturnToMainMenu();
			return;
		}

		CurrentState = gameState.Loading;
		TryAdvanceToNextLevel();
	}

	/// <summary>
	/// Called by FinishOverlay's Menu button (or from summary on last level).
	/// </summary>
	public void ReturnToMainMenu()
	{
		CurrentState = gameState.MainMenu;
		_currentLevelIndex = -1;
		CountdownManager.Instance.Reset();
		SceneManager.Instance.HideAllOverlays();

		if (_mainMenuScene != null)
		{
			string path = _mainMenuScene.ResourcePath;
			Callable.From(() => SceneManager.Instance.ChangeSceneByPathAsync(path)).CallDeferred();
		}
	}

	/// <summary>
	/// Called by GameOverlay when the effective limit reaches 0 mid-level.
	/// Shows the failure screen immediately.
	/// </summary>
	public void TriggerDynamicFailure()
	{
		CurrentState = gameState.Loading;
		Input.MouseMode = Input.MouseModeEnum.Visible;
		SceneManager.Instance.ShowFinishOverlay();
		var fail = SceneManager.Instance.GetFailureOverlay();
		if (fail != null) fail.ShowStats();
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
