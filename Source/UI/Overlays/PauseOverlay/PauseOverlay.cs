using Godot;
using System;

public partial class PauseOverlay : Control
{
	[Export] private Button _resumeButton;
	[Export] private Button _settingsButton;
	[Export] private Button _mainMenuButton;

	/// <summary>Invoked when Settings is pressed.</summary>
	public Action OnSettingsClicked { get; set; }

	/// <summary>Invoked when Main Menu is pressed.</summary>
	public Action OnMainMenuClicked { get; set; }

	public override void _Ready()
	{
		if (_resumeButton != null)
			_resumeButton.Pressed += ResumeGame;

		if (_settingsButton != null)
			_settingsButton.Pressed += () => OnSettingsClicked?.Invoke();

		if (_mainMenuButton != null)
			_mainMenuButton.Pressed += () => OnMainMenuClicked?.Invoke();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("PauseToggle"))
		{
			if (Visible)
			{
				// Pause overlay is visible — hide and resume the game
				HidePause();
			}
			else
			{
				// Only allow pausing during active gameplay
				var state = GameManager.Instance.CurrentState;
				if (state == GameManager.gameState.Waiting
				    || state == GameManager.gameState.Countdown)
				{
					ShowPause();
				}
			}
			GetViewport().SetInputAsHandled();
		}
	}

	/// <summary>
	/// Show the pause overlay and pause the game scene tree.
	/// </summary>
	public void ShowPause()
	{
		Visible = true;
		GetTree().Paused = true;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	/// <summary>
	/// Hide the pause overlay and resume the game scene tree.
	/// </summary>
	public void HidePause()
	{
		Visible = false;
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}

	private void ResumeGame()
	{
		HidePause();
	}

	public override void _ExitTree()
	{
		// Signal connections are cleaned up automatically by Godot
		// when the node exits the tree.
	}
}
