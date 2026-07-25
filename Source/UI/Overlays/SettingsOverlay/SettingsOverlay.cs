using Godot;
using System;

public partial class SettingsOverlay : Control
{
	[Export] private Button _backButton;

    [Export] private HSlider DifficultySlider;
    [Export] private RichTextLabel DifficultyNameLabel;

	/// <summary>
	/// Invoked when the Back button is pressed. MainMenuOverlay hooks into this
	/// to show itself and hide SettingsOverlay.
	/// </summary>
	public Action OnBackClicked { get; set; }

	public override void _Ready()
	{
		if (_backButton != null)
			_backButton.Pressed += OnBackPressed;
	}

	public override void _ExitTree()
	{
		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;
	}

	private void OnBackPressed()
	{
		OnBackClicked?.Invoke();
	}
}
