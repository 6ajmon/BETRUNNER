using Godot;
using System;

public partial class MainMenuOverlay : Control
{
	[Export] public Button PlayButton { get; set; }
	[Export] public Button SettingsButton { get; set; }
	[Export] public Button LevelSelectButton { get; set; }
	[Export] public Button CreditsButton { get; set; }
	[Export] public Button ExitButton { get; set; }

	private ConfirmationDialog _exitDialog;

	public override void _Ready()
	{
		// ── Connect buttons ─────────────────────────────────────────
		PlayButton.Pressed += OnPlayButtonPressed;
		SettingsButton.Pressed += OnSettingsButtonPressed;
		CreditsButton.Pressed += OnCreditsButtonPressed;
		ExitButton.Pressed += OnExitButtonPressed;

		// ── Exit confirmation dialog ────────────────────────────────
		_exitDialog = new ConfirmationDialog();
		_exitDialog.Title = "Exit Game";
		_exitDialog.DialogText = "Are you sure you want to exit the game?";
		_exitDialog.GetOkButton().Text = "Yes";
		_exitDialog.GetCancelButton().Text = "No";
		_exitDialog.Confirmed += () => GetTree().Quit();
		AddChild(_exitDialog);

		// ── Hook into SettingsOverlay back (managed by SceneManager) ─
		var settings = SceneManager.Instance.GetSettingsOverlay();
		if (settings != null)
			settings.OnBackClicked += OnSettingsBackPressed;
	}

	public override void _ExitTree()
	{
		PlayButton.Pressed -= OnPlayButtonPressed;
		SettingsButton.Pressed -= OnSettingsButtonPressed;
		CreditsButton.Pressed -= OnCreditsButtonPressed;
		ExitButton.Pressed -= OnExitButtonPressed;

		var settings = SceneManager.Instance?.GetSettingsOverlay();
		if (settings != null)
			settings.OnBackClicked -= OnSettingsBackPressed;
	}

	private void OnPlayButtonPressed()
	{
		GameManager.Instance.EmitSignal(nameof(GameManager.PlayButtonPressed));
	}

	private void OnSettingsButtonPressed()
	{
		SceneManager.Instance.ShowSettingsOverlay();
	}

	private void OnSettingsBackPressed()
	{
		SceneManager.Instance.ShowMainMenuOverlay();
	}

	private void OnCreditsButtonPressed()
	{
		OS.ShellOpen("https://github.com/6ajmon/gmtk2026");
	}

	private void OnExitButtonPressed()
	{
		_exitDialog.PopupCentered();
	}
}
