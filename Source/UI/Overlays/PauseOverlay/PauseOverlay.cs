using Godot;
using System;

public partial class PauseOverlay : Control
{
	[Export] private Button _resumeButton;
	[Export] private Button _settingsButton;
	[Export] private Button _mainMenuButton;

	public override void _Ready()
	{
		if (_resumeButton != null)
		{
			_resumeButton.Pressed += ResumeGame;
			ButtonSoundHelper.Wire(_resumeButton);
		}

		if (_settingsButton != null)
		{
			ButtonSoundHelper.Wire(_settingsButton);
			_settingsButton.Pressed += () =>
			{
				var settings = SceneManager.Instance.GetSettingsOverlay();
				if (settings != null)
				{
					settings.DifficultyEnabled = false;
					settings.OnBackClicked = () => SceneManager.Instance.ShowPauseOverlay();
				}
				SceneManager.Instance.ShowSettingsOverlay();
			};
		}

		if (_mainMenuButton != null)
		{
			ButtonSoundHelper.Wire(_mainMenuButton);
			_mainMenuButton.Pressed += () =>
			{
				GetTree().Paused = false;
				GameManager.Instance.ReturnToMainMenu();
			};
		}
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
				// Jeśli settings overlay jest otwarte (przyszliśmy z pauzy),
				// wróć do pause overlaya zamiast przełączać
				var settings = SceneManager.Instance.GetSettingsOverlay();
				if (settings != null && settings.Visible)
				{
					// Settings back powinien wrócić do pauzy — symulujemy back
					settings.GoBack();
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
		AudioManager.Instance.ApplyPauseEffect();
		AudioManager.Instance.StopLoopingSFX(); // zatrzymaj tykanie stopera
	}

	/// <summary>
	/// Hide the pause overlay and resume the game scene tree.
	/// </summary>
	public void HidePause()
	{
		Visible = false;
		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
		AudioManager.Instance.RemovePauseEffect();

		// Restore game overlay (SceneManager.ShowPauseOverlay() hides it)
		SceneManager.Instance.ShowGameOverlay();
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
