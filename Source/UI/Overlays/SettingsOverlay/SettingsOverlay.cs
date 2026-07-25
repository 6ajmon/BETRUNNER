using Godot;
using System;

public partial class SettingsOverlay : Control
{
	[Export] private Button _backButton;
	[Export] private Button _saveButton;
	[Export] private DifficultyContainer _difficultyContainer;

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
		// ── Difficulty container ─────────────────────────────────────
		if (_difficultyContainer != null)
			_difficultyContainer.DifficultyChanged += OnDifficultyChanged;

		// ── Save button ──────────────────────────────────────────────
		if (_saveButton != null)
			_saveButton.Pressed += OnSavePressed;

		// ── Back button ──────────────────────────────────────────────
		if (_backButton != null)
			_backButton.Pressed += OnBackPressed;

		// ── Reload whenever we become visible ────────────────────────
		VisibilityChanged += ReloadSavedState;

		// ── Unsaved-changes confirmation dialog ──────────────────────
		_unsavedDialog = new ConfirmationDialog();
		_unsavedDialog.Title = "Unsaved Changes";
		_unsavedDialog.DialogText = "Do you want to save changes?";
		_unsavedDialog.GetOkButton().Text = "Yes";
		_unsavedDialog.GetCancelButton().Text = "No";
		_unsavedDialog.Confirmed += SaveAndGoBack;
		_unsavedDialog.Canceled += DiscardAndGoBack;
		_unsavedDialog.AddThemeIconOverride("close", new ImageTexture());
		AddChild(_unsavedDialog);

		// Initial load
		ReloadSavedState();
	}

	private void ReloadSavedState()
	{
		if (!Visible) return;

		_savedDifficulty = (int)CountdownManager.Instance.CurrentDifficulty;
		_pendingDifficulty = _savedDifficulty;
		_hasUnsavedChanges = false;

		if (_difficultyContainer != null)
			_difficultyContainer.Value = _savedDifficulty;

		if (_saveButton != null)
			_saveButton.Disabled = true;
	}

	public override void _ExitTree()
	{
		if (_difficultyContainer != null)
			_difficultyContainer.DifficultyChanged -= OnDifficultyChanged;

		if (_saveButton != null)
			_saveButton.Pressed -= OnSavePressed;

		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;
	}

	// ── Difficulty ─────────────────────────────────────────────────────────

	private void OnDifficultyChanged(int diff)
	{
		_pendingDifficulty = diff;
		MarkChanged();
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
		_pendingDifficulty = _savedDifficulty;
		if (_difficultyContainer != null)
			_difficultyContainer.Value = _savedDifficulty;
		_hasUnsavedChanges = false;
		if (_saveButton != null)
			_saveButton.Disabled = true;

		OnBackClicked?.Invoke();
	}
}
