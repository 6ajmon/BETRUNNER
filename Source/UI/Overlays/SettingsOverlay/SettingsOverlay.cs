using Godot;
using System;

public partial class SettingsOverlay : Control
{
	[Export] private Button _backButton;
	[Export] private Button _saveButton;
	[Export] private DifficultyContainer _difficultyContainer;
	[Export] private HSlider _musicSlider;
	[Export] private HSlider _sfxSlider;
	[Export] private CheckButton _runToggle;

	// ── Pending / saved state ─────────────────────────────────────────────
	private int _savedDifficulty;
	private int _pendingDifficulty;
	private float _savedMusicVolume = -1f;   // -1 = uninitialised sentinel
	private float _savedSfxVolume = -1f;      // -1 = uninitialised sentinel
	private bool _savedRunToggle;
	private bool _pendingRunToggle;
	private bool _hasUnsavedChanges;

	private ConfirmationDialog _unsavedDialog;

	/// <summary>
	/// Invoked when Back is pressed AND all changes are saved or discarded.
	/// </summary>
	public Action OnBackClicked { get; set; }

	/// <summary>
	/// Whether the difficulty slider is interactive.
	/// Set to false when opening settings from the pause overlay (in-game).
	/// </summary>
	public bool DifficultyEnabled
	{
		get => _difficultyEnabled;
		set
		{
			_difficultyEnabled = value;
			if (_difficultyContainer != null)
				_difficultyContainer.SetDifficultyInteractive(value);
			if (_saveButton != null)
				_saveButton.Disabled = !value;
		}
	}
	private bool _difficultyEnabled = true;

	public override void _Ready()
	{
		// ── Difficulty container ─────────────────────────────────────
		if (_difficultyContainer != null)
			_difficultyContainer.DifficultyChanged += OnDifficultyChanged;

		// ── Save button ──────────────────────────────────────────────
		if (_saveButton != null)
		{
			_saveButton.Pressed += OnSavePressed;
			ButtonSoundHelper.Wire(_saveButton);
		}

		// ── Back button ──────────────────────────────────────────────
		if (_backButton != null)
		{
			_backButton.Pressed += OnBackPressed;
			ButtonSoundHelper.Wire(_backButton);
		}

		// ── Volume sliders ───────────────────────────────────────────
		if (_musicSlider != null)
		{
			_musicSlider.ValueChanged += OnMusicVolumeChanged;
			ButtonSoundHelper.WireSlider(_musicSlider);
		}
		if (_sfxSlider != null)
		{
			_sfxSlider.ValueChanged += OnSfxVolumeChanged;
			ButtonSoundHelper.WireSlider(_sfxSlider);
		}

		// ── Run toggle ──────────────────────────────────────────────
		if (_runToggle != null)
		{
			_runToggle.Toggled += OnRunToggleChanged;
			ButtonSoundHelper.Wire(_runToggle);
		}

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
		ButtonSoundHelper.Wire(_unsavedDialog.GetOkButton());
		ButtonSoundHelper.Wire(_unsavedDialog.GetCancelButton());
		_unsavedDialog.AddThemeIconOverride("close", new ImageTexture());
		AddChild(_unsavedDialog);

		// Initial load
		ReloadSavedState();
	}

	private void ReloadSavedState()
	{
		if (!Visible) return;

		// Initialise saved volumes from current bus state (first time only)
		if (_savedMusicVolume < 0f)
			_savedMusicVolume = DbToSlider(AudioManager.Instance.GetMusicVolumeDb());
		if (_savedSfxVolume < 0f)
			_savedSfxVolume = DbToSlider(AudioManager.Instance.GetSFXVolumeDb());

		_savedDifficulty = (int)CountdownManager.Instance.CurrentDifficulty;
		_pendingDifficulty = _savedDifficulty;
		_savedRunToggle = CountdownManager.Instance.RunMakesYouWalk;
		_pendingRunToggle = _savedRunToggle;
		_hasUnsavedChanges = false;

		if (_difficultyContainer != null)
			_difficultyContainer.Value = _savedDifficulty;

		if (_runToggle != null)
			_runToggle.ButtonPressed = _savedRunToggle;

		if (_saveButton != null)
			_saveButton.Disabled = !_difficultyEnabled;

		// Reset sliders + buses to the last saved values
		if (_musicSlider != null)
		{
			AudioManager.Instance.SetAllMusicVolumeDb(SliderToDb(_savedMusicVolume));
			_musicSlider.Value = _savedMusicVolume;
		}
		if (_sfxSlider != null)
		{
			float db = SliderToDb(_savedSfxVolume);
			AudioManager.Instance.SetSFXVolumeDb(db);
			AudioManager.Instance.SetSFX3DVolumeDb(db);
			_sfxSlider.Value = _savedSfxVolume;
		}
	}

	public override void _ExitTree()
	{
		if (_difficultyContainer != null)
			_difficultyContainer.DifficultyChanged -= OnDifficultyChanged;

		if (_saveButton != null)
			_saveButton.Pressed -= OnSavePressed;

		if (_backButton != null)
			_backButton.Pressed -= OnBackPressed;

		if (_musicSlider != null)
			_musicSlider.ValueChanged -= OnMusicVolumeChanged;
		if (_sfxSlider != null)
			_sfxSlider.ValueChanged -= OnSfxVolumeChanged;

		if (_runToggle != null)
			_runToggle.Toggled -= OnRunToggleChanged;
	}

	// ── Difficulty ─────────────────────────────────────────────────────────

	private void OnDifficultyChanged(int diff)
	{
		_pendingDifficulty = diff;
		MarkChanged();
	}

	private void OnRunToggleChanged(bool toggledOn)
	{
		_pendingRunToggle = toggledOn;
		MarkChanged();
	}

	// ── Dirty tracking ────────────────────────────────────────────────────

	private void MarkChanged()
	{
		bool changed = _pendingDifficulty != _savedDifficulty
			|| _pendingRunToggle != _savedRunToggle
			|| (_musicSlider != null && (float)_musicSlider.Value != _savedMusicVolume)
			|| (_sfxSlider != null && (float)_sfxSlider.Value != _savedSfxVolume);

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

		CountdownManager.Instance.RunMakesYouWalk = _pendingRunToggle;
		_savedRunToggle = _pendingRunToggle;

		// Commit current slider values as saved
		if (_musicSlider != null)
			_savedMusicVolume = (float)_musicSlider.Value;
		if (_sfxSlider != null)
			_savedSfxVolume = (float)_sfxSlider.Value;

		_hasUnsavedChanges = false;
		if (_saveButton != null)
			_saveButton.Disabled = true;
	}

	// ── Volume sliders (immediate feedback, committed only on Save) ───────

	private void OnMusicVolumeChanged(double value)
	{
		AudioManager.Instance.SetAllMusicVolumeDb(SliderToDb((float)value));
		MarkChanged();
	}

	private void OnSfxVolumeChanged(double value)
	{
		float db = SliderToDb((float)value);
		AudioManager.Instance.SetSFXVolumeDb(db);
		AudioManager.Instance.SetSFX3DVolumeDb(db);
		MarkChanged();
	}

	/// <summary>
	/// Converts a slider value (0-100) to decibels.
	/// </summary>
	private static float SliderToDb(float sliderValue)
	{
		if (sliderValue <= 0f) return -80f;
		return Mathf.LinearToDb(sliderValue / 100f);
	}

	/// <summary>
	/// Converts decibels to a slider value (0-100).
	/// </summary>
	private static float DbToSlider(float db)
	{
		if (db <= -80f) return 0f;
		return Mathf.DbToLinear(db) * 100f;
	}

	// ── Back ────────────────────────────────────────────────────────────────

	/// <summary>
	/// Programmatically trigger the Back action.
	/// Public so PauseOverlay can call it when PauseToggle is pressed
	/// while the settings overlay is open.
	/// </summary>
	public void GoBack()
	{
		OnBackPressed();
	}

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
		// Revert difficulty
		_pendingDifficulty = _savedDifficulty;
		if (_difficultyContainer != null)
			_difficultyContainer.Value = _savedDifficulty;

		// Revert run toggle
		_pendingRunToggle = _savedRunToggle;
		if (_runToggle != null)
			_runToggle.ButtonPressed = _savedRunToggle;

		// Revert volume sliders + buses to saved values
		if (_musicSlider != null)
		{
			AudioManager.Instance.SetAllMusicVolumeDb(SliderToDb(_savedMusicVolume));
			_musicSlider.Value = _savedMusicVolume;
		}
		if (_sfxSlider != null)
		{
			float db = SliderToDb(_savedSfxVolume);
			AudioManager.Instance.SetSFXVolumeDb(db);
			AudioManager.Instance.SetSFX3DVolumeDb(db);
			_sfxSlider.Value = _savedSfxVolume;
		}

		_hasUnsavedChanges = false;
		if (_saveButton != null)
			_saveButton.Disabled = true;

		OnBackClicked?.Invoke();
	}
}
