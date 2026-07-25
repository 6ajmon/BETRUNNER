using Godot;
using System;

public partial class SettingsOverlay : Control
{
	[Export] private Button _backButton;
	[Export] private Button _saveButton;
	[Export] private HSlider _difficultySlider;
	[Export] private RichTextLabel _difficultyNameLabel;

	private static readonly string[] _difficultyNames = { "Noob", "Pro", "Dev" };

	// ── Pending / saved state ─────────────────────────────────────────────
	private int _savedDifficulty;
	private int _pendingDifficulty;
	private bool _hasUnsavedChanges;

	private ConfirmationDialog _unsavedDialog;

	/// <summary>
	/// Invoked when Back is pressed AND all changes are saved or discarded.
	/// </summary>
	public Action OnBackClicked { get; set; }

	public override void _Ready()
	{
		// ── Snapshot current values ──────────────────────────────────
		_savedDifficulty = (int)CountdownManager.Instance.CurrentDifficulty;
		_pendingDifficulty = _savedDifficulty;
		_hasUnsavedChanges = false;

		// ── Slider ───────────────────────────────────────────────────
		if (_difficultySlider != null)
		{
			_difficultySlider.Value = _savedDifficulty;
			_difficultySlider.ValueChanged += OnDifficultyChanged;
		}
		UpdateDifficultyLabel(_savedDifficulty);

		// ── Save button ──────────────────────────────────────────────
		if (_saveButton != null)
		{
			_saveButton.Disabled = true;
			_saveButton.Pressed += OnSavePressed;
		}

		// ── Back button ──────────────────────────────────────────────
		if (_backButton != null)
			_backButton.Pressed += OnBackPressed;

		// ── Unsaved-changes confirmation dialog ──────────────────────
		_unsavedDialog = new ConfirmationDialog();
		_unsavedDialog.Title = "Unsaved Changes";
		_unsavedDialog.DialogText = "Do you want to save changes?";
		_unsavedDialog.GetOkButton().Text = "Yes";
		_unsavedDialog.GetCancelButton().Text = "No";
		_unsavedDialog.Confirmed += SaveAndGoBack;
		_unsavedDialog.Canceled += DiscardAndGoBack;
		_unsavedDialog.AddThemeIconOverride("close", new ImageTexture()); // Remove the default "X"
		AddChild(_unsavedDialog);
	}

	public override void _ExitTree()
	{
		if (_difficultySlider != null)
			_difficultySlider.ValueChanged -= OnDifficultyChanged;

		if (_saveButton != null)
			_saveButton.Pressed -= OnSavePressed;

		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;
	}

	// ── Slider ─────────────────────────────────────────────────────────────

	private void OnDifficultyChanged(double value)
	{
		int diff = Math.Clamp((int)Math.Round(value), 0, 2);
		_pendingDifficulty = diff;
		UpdateDifficultyLabel(diff);
		MarkChanged();

		if (_difficultySlider != null)
			_difficultySlider.Value = diff; // snap
	}

	private void UpdateDifficultyLabel(int diff)
	{
		if (_difficultyNameLabel != null)
			_difficultyNameLabel.Text = _difficultyNames[diff];
	}

	// ── Dirty tracking ────────────────────────────────────────────────────

	private void MarkChanged()
	{
		bool changed = _pendingDifficulty != _savedDifficulty;
		if (changed != _hasUnsavedChanges)
		{
			_hasUnsavedChanges = changed;
			if (_saveButton != null)
				_saveButton.Disabled = !changed;
		}
	}

	// ── Save ────────────────────────────────────────────────────────────────

	private void OnSavePressed()
	{
		ApplyPending();
	}

	private void ApplyPending()
	{
		CountdownManager.Instance.CurrentDifficulty = (CountdownManager.Difficulty)_pendingDifficulty;
		_savedDifficulty = _pendingDifficulty;
		_hasUnsavedChanges = false;

		if (_saveButton != null)
			_saveButton.Disabled = true;
	}

	// ── Back ────────────────────────────────────────────────────────────────

	private void OnBackPressed()
	{
		if (_hasUnsavedChanges)
		{
			_unsavedDialog.PopupCentered();
		}
		else
		{
			OnBackClicked?.Invoke();
		}
	}

	private void SaveAndGoBack()
	{
		ApplyPending();
		OnBackClicked?.Invoke();
	}

	private void DiscardAndGoBack()
	{
		// Reset slider back to the saved value
		_pendingDifficulty = _savedDifficulty;
		if (_difficultySlider != null)
			_difficultySlider.Value = _savedDifficulty;
		UpdateDifficultyLabel(_savedDifficulty);
		_hasUnsavedChanges = false;
		if (_saveButton != null)
			_saveButton.Disabled = true;

		OnBackClicked?.Invoke();
	}
}
