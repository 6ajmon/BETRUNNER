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

	// ── Overlay visibility control ─────────────────────────────────────────

	public void ShowBettingOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = true;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
	}

	public void ShowGameOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = true;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
	}

	public void ShowSummaryOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = true;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
	}

	public void ShowFinishOverlay()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = true;
	}

	public void HideAllOverlays()
	{
		if (_bettingOverlay != null) _bettingOverlay.Visible = false;
		if (_gameOverlay != null)    _gameOverlay.Visible = false;
		if (_summaryOverlay != null) _summaryOverlay.Visible = false;
		if (_finishOverlay != null) _finishOverlay.Visible = false;
	}

	// ── Overlay accessors for code-behind ──────────────────────────────────
	public SummaryOverlay GetSummaryOverlay() => _summaryOverlay as SummaryOverlay;
	public FinishOverlay GetFailureOverlay() => _finishOverlay as FinishOverlay;
	
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
