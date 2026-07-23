using Godot;
using System;
using System.Threading.Tasks;

public partial class SceneManager : Node
{
	
	public static SceneManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<SceneManager>("SceneManager");
	
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
