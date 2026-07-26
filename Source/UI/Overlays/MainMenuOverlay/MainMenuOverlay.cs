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
	private ConfirmationDialog _firstPlayDialog;
	private DifficultyContainer _firstPlayDifficulty;
	private bool _hasPlayedBefore;

	public override void _Ready()
	{
		// ── Connect buttons ─────────────────────────────────────────
		PlayButton.Pressed += OnPlayButtonPressed;
		SettingsButton.Pressed += OnSettingsButtonPressed;
		CreditsButton.Pressed += OnCreditsButtonPressed;
		ExitButton.Pressed += OnExitButtonPressed;

		// ── Button sounds ──────────────────────────────────────────
		ButtonSoundHelper.Wire(PlayButton);
		ButtonSoundHelper.Wire(SettingsButton);
		ButtonSoundHelper.Wire(LevelSelectButton);
		ButtonSoundHelper.Wire(CreditsButton);
		ButtonSoundHelper.Wire(ExitButton);

		// ── Exit confirmation dialog ────────────────────────────────
		_exitDialog = new ConfirmationDialog();
		_exitDialog.Title = "Exit Game";
		_exitDialog.DialogText = "Are you sure you want to exit the game?";
		_exitDialog.GetOkButton().Text = "Yes";
		_exitDialog.GetCancelButton().Text = "No";
		_exitDialog.Confirmed += () => GetTree().Quit();
		ButtonSoundHelper.Wire(_exitDialog.GetOkButton());
		ButtonSoundHelper.Wire(_exitDialog.GetCancelButton());
		AddChild(_exitDialog);

		// ── First-play difficulty dialog ────────────────────────────
		BuildFirstPlayDialog();

		// ── Hook into SettingsOverlay back (managed by SceneManager) ─
		var settings = SceneManager.Instance.GetSettingsOverlay();
		if (settings != null)
			settings.OnBackClicked += OnSettingsBackPressed;

		// Re-wire when becoming visible (in case pause overwrote it)
		VisibilityChanged += OnVisibilityChanged;
	}

	public override void _ExitTree()
	{
		PlayButton.Pressed -= OnPlayButtonPressed;
		SettingsButton.Pressed -= OnSettingsButtonPressed;
		CreditsButton.Pressed -= OnCreditsButtonPressed;
		ExitButton.Pressed -= OnExitButtonPressed;

		var settings = SceneManager.Instance?.GetSettingsOverlay();
		if (settings != null)
		{
			settings.OnBackClicked -= OnSettingsBackPressed;
			VisibilityChanged -= OnVisibilityChanged;
		}
	}

	private void OnVisibilityChanged()
	{
		if (!Visible) return;

		var settings = SceneManager.Instance.GetSettingsOverlay();
		if (settings != null)
			settings.OnBackClicked = OnSettingsBackPressed;
	}

	// ── First-play dialog ─────────────────────────────────────────────────

	private void BuildFirstPlayDialog()
	{
		_firstPlayDialog = new ConfirmationDialog();
		_firstPlayDialog.Title = "";
		_firstPlayDialog.GetOkButton().Text = "Start";
		_firstPlayDialog.GetCancelButton().Text = "Cancel";
		_firstPlayDialog.Confirmed += OnFirstPlayConfirmed;

		ButtonSoundHelper.Wire(_firstPlayDialog.GetOkButton());
		ButtonSoundHelper.Wire(_firstPlayDialog.GetCancelButton());

		// Content container
		var content = new VBoxContainer();
		content.AddThemeConstantOverride("separation", 12);

		var titleLabel = new Label();
		titleLabel.Text = "Choose your difficulty level";
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.AddThemeFontSizeOverride("font_size", 20);
		content.AddChild(titleLabel);

		// Instance DifficultyContainer from its scene
		var diffScene = ResourceLoader.Load<PackedScene>(
			"res://Source/UI/Controls/DifficultyContainer/DifficultyContainer.tscn");
		if (diffScene != null)
		{
			_firstPlayDifficulty = diffScene.Instantiate<DifficultyContainer>();
			_firstPlayDifficulty.Value = (int)CountdownManager.Instance.CurrentDifficulty;
			content.AddChild(_firstPlayDifficulty);
		}

		var hintLabel = new Label();
		hintLabel.Text = "Don't worry, you can always change that in the settings";
		hintLabel.HorizontalAlignment = HorizontalAlignment.Center;
		hintLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		content.AddChild(hintLabel);

		_firstPlayDialog.AddChild(content);
		_firstPlayDialog.AddThemeIconOverride("close", new ImageTexture());
		AddChild(_firstPlayDialog);
	}

	private void OnFirstPlayConfirmed()
	{
		// Apply difficulty from the dialog before starting
		if (_firstPlayDifficulty != null)
			CountdownManager.Instance.CurrentDifficulty =
				(CountdownManager.Difficulty)_firstPlayDifficulty.Value;

		_hasPlayedBefore = true;
		GameManager.Instance.EmitSignal(nameof(GameManager.PlayButtonPressed));
	}

	// ── Play ──────────────────────────────────────────────────────────────

	private void OnPlayButtonPressed()
	{
		if (_hasPlayedBefore)
		{
			GameManager.Instance.EmitSignal(nameof(GameManager.PlayButtonPressed));
		}
		else
		{
			_firstPlayDialog.PopupCentered();
		}
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
