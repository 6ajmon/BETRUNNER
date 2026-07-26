using Godot;
using System;
using System.Threading.Tasks;

public partial class SceneManager : Node
{
	
	public static SceneManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<SceneManager>("SceneManager");

	// ── Overlay references (assign in Godot editor) ────────────────────────
	[Export] private Control _bettingOverlay;
	[Export] private Control _gameOverlay;
	[Export] private Control _summaryOverlay;
	[Export] private Control _finishOverlay;
	[Export] private Control _mainMenuOverlay;
	[Export] private Control _settingsOverlay;
	[Export] private Control _pauseOverlay;

	// ── Overlay visibility control ─────────────────────────────────────────

	public void ShowBettingOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = true;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
	}

	public void ShowGameOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = true;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
	}

	public void ShowSummaryOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = true;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
	}

	public void ShowFinishOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = true;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
	}

	public void ShowMainMenuOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = true;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
	}

	public void ShowSettingsOverlay()
	{
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = true;
	}

	public void ShowPauseOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)
		{
			var pause = _pauseOverlay as PauseOverlay;
			pause?.ShowPause();
		}
	}

	public void HideAllOverlays()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
		if (_mainMenuOverlay != null) _mainMenuOverlay.Visible = false;
		if (_settingsOverlay != null) _settingsOverlay.Visible = false;
		if (_pauseOverlay != null)    _pauseOverlay.Visible = false;
	}

	// ── Overlay accessors ──────────────────────────────────────────────────
	public SettingsOverlay GetSettingsOverlay() => _settingsOverlay as SettingsOverlay;

	// ── Overlay accessors for code-behind ──────────────────────────────────
	public SummaryOverlay GetSummaryOverlay() => _summaryOverlay as SummaryOverlay;
	public FinishOverlay GetFailureOverlay() => _finishOverlay as FinishOverlay;
	public PauseOverlay GetPauseOverlay() => _pauseOverlay as PauseOverlay;
	public GameOverlay GetGameOverlay() => _gameOverlay as GameOverlay;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public async void ChangeSceneByPathAsync(string scenePath)
	{
		if (!ResourceLoader.Exists(scenePath))
		{
			GD.PrintErr($"[SceneManager] Plik sceny nie istnieje pod ścieżką: {scenePath}");
			return;
		}

		SceneTree tree = GetTree();
		Node currentScene = tree.CurrentScene;

		if (currentScene != null)
		{
			tree.Root.RemoveChild(currentScene);
			currentScene.QueueFree();
			tree.CurrentScene = null;
		}
		await ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		PackedScene newSceneResource = ResourceLoader.Load<PackedScene>(scenePath);
		if (newSceneResource == null)
		{
			GD.PrintErr($"[SceneManager] Nie udało się załadować zasobu: {scenePath}");
			return;
		}
		Node newScene = newSceneResource.Instantiate();
		tree.Root.AddChild(newScene);
		tree.CurrentScene = newScene;
	}
}
