using Godot;
using System;

public partial class AudioManager : Node
{
	public static AudioManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<AudioManager>("AudioManager");

	// ── Exports ──────────────────────────────────────────────────────────

	[ExportGroup("Music Tracks")]
	[Export] private AudioStream _mainMenuMusic;
	[Export] private AudioStream _gameplayMusic;
	[Export] private AudioStream _countdownMusic;
	[Export] private AudioStream _previewMusic;
	[Export] private AudioStream _victoryMusic;
	[Export] private AudioStream _gameOverMusic;

	[ExportGroup("Track Volume Offsets (dB)")]
	[Export] private float _mainMenuVolumeDb = 0f;
	[Export] private float _gameplayVolumeDb = -10f;
	[Export] private float _countdownVolumeDb = -10f;
	[Export] private float _previewVolumeDb = -10f;
	[Export] private float _victoryVolumeDb = -10f;
	[Export] private float _gameOverVolumeDb = -10f;

	[ExportGroup("Settings")]
	[Export] private float _defaultFadeDuration = 1.0f;
	[Export] private int _sfxPoolSize = 16;
	[Export] private int _sfx3DPoolSize = 16;

	// ── Audio buses ──────────────────────────────────────────────────────
	/// <summary>Normal music bus (MainMenu, Victory).</summary>
	private const string MusicBus = "Music";
	/// <summary>Muffled music bus with low-pass + reverb (Preview, Waiting).</summary>
	private const string MusicMuffledBus = "Music_Muffled";
	/// <summary>Countdown music bus with subtle filter (Countdown).</summary>
	private const string MusicCountdownBus = "Music_Countdown";
	/// <summary>Game-over music bus, heavily muffled (GameOver).</summary>
	private const string MusicGameOverBus = "Music_GameOver";
	private const string SfxBus = "SFX";
	private const string Sfx3DBus = "SFX3D";

	// ── Music state ──────────────────────────────────────────────────────
	private AudioStreamPlayer _musicPlayer;
	private AudioStream _currentMusic;
	private Tween _musicFadeTween;
	private bool _musicPaused;
	/// <summary>Current track's local volume offset (applied on top of bus volume).</summary>
	private float _currentTrackVolumeDb;

	// ── SFX pools ────────────────────────────────────────────────────────
	private AudioStreamPlayer[] _sfxPool;
	private int _sfxIndex;

	private AudioStreamPlayer3D[] _sfx3DPool;
	private int _sfx3DIndex;

	private Node _sfxContainer;
	private Node _sfx3DContainer;

	// ── Dedicated looping SFX player (timer, etc.) ──────────────────────
	private AudioStreamPlayer _loopingSFXPlayer;

	// ── SFX library (assign clips in Inspector) ─────────────────────────
	[Export] private SfxLibrary _sfxLibrary;

	/// <summary>
	/// Provides access to all assigned sound-effect clips.
	/// Used by convenience Play methods; can also be accessed directly.
	/// </summary>
	public SfxLibrary Sfx => _sfxLibrary;

	// ══════════════════════════════════════════════════════════════════════
	//  INITIALIZATION
	// ══════════════════════════════════════════════════════════════════════

	public override void _Ready()
	{
		// ── Audio buses ──────────────────────────────────────────────
		// Create music buses if they don't exist yet (so volume sliders work
		// even before the user sets them up in the Audio tab).
		CreateMusicBuses();

		// Set initial bus volumes to 20% (all music buses + SFX buses)
		SetAllMusicVolumeDb(Mathf.LinearToDb(0.2f));
		SetSFXVolumeDb(Mathf.LinearToDb(0.2f));
		SetSFX3DVolumeDb(Mathf.LinearToDb(0.2f));

		// ── Music player ─────────────────────────────────────────────
		_musicPlayer = new AudioStreamPlayer();
		_musicPlayer.Bus = MusicBus;
		_musicPlayer.Name = "MusicPlayer";
		_musicPlayer.ProcessMode = ProcessModeEnum.Always;
		AddChild(_musicPlayer);

		// ── SFX pool containers (for scene tree organization) ────────
		_sfxContainer = new Node();
		_sfxContainer.Name = "SFXPool";
		AddChild(_sfxContainer);

		_sfx3DContainer = new Node();
		_sfx3DContainer.Name = "SFX3DPool";
		AddChild(_sfx3DContainer);

		// Pre-allocate 2D SFX players
		_sfxPool = new AudioStreamPlayer[_sfxPoolSize];
		for (int i = 0; i < _sfxPoolSize; i++)
		{
			var player = new AudioStreamPlayer();
			player.Bus = SfxBus;
			player.Name = $"SFX_{i}";
			_sfxContainer.AddChild(player);
			_sfxPool[i] = player;
		}

		// Pre-allocate 3D SFX players
		_sfx3DPool = new AudioStreamPlayer3D[_sfx3DPoolSize];
		for (int i = 0; i < _sfx3DPoolSize; i++)
		{
			var player = new AudioStreamPlayer3D();
			player.Bus = Sfx3DBus;
			player.Name = $"SFX3D_{i}";
			_sfx3DContainer.AddChild(player);
			_sfx3DPool[i] = player;
		}
	}

	// ── Bus creation ──────────────────────────────────────────────────────

	/// <summary>
	/// Creates all music-related audio buses if they don't already exist.
	/// This makes volume sliders work without requiring manual bus setup.
	/// </summary>
	private void CreateMusicBuses()
	{
		EnsureBusExists(MusicBus);
		EnsureBusExists(MusicMuffledBus);
		EnsureBusExists(MusicCountdownBus);
		EnsureBusExists(MusicGameOverBus);
	}

	/// <summary>
	/// Adds a bus with the given name as a child of Master, if it doesn't
	/// already exist.
	/// </summary>
	private static void EnsureBusExists(string busName)
	{
		if (AudioServer.GetBusIndex(busName) >= 0) return;

		AudioServer.AddBus();
		int idx = AudioServer.GetBusCount() - 1;
		AudioServer.SetBusName(idx, busName);
		AudioServer.SetBusSend(idx, "Master");
	}

	// ══════════════════════════════════════════════════════════════════════
	//  MUSIC — playback
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Plays a music track. If another track is playing, crossfades.
	/// If the same track is already playing, only switches the audio bus
	/// (changes effects without restarting playback).
	/// </summary>
	/// <param name="track">Audio stream to play.</param>
	/// <param name="fadeInDuration">
	/// Fade-in duration in seconds. -1 uses the default from Inspector. 0 = instant.
	/// </param>
	/// <param name="targetBus">
	/// Audio bus to use for this track. If null, keeps the current bus.
	/// </param>
	public void PlayMusic(AudioStream track, float fadeInDuration = -1f, string targetBus = null)
	{
		if (track == null) return;

		// Same track already playing — just switch bus + track volume, don't restart
		if (track == _currentMusic && _musicPlayer.Playing)
		{
			if (targetBus != null && _musicPlayer.Bus != targetBus)
				_musicPlayer.Bus = targetBus;
			_musicPlayer.VolumeDb = _currentTrackVolumeDb;
			return;
		}

		if (fadeInDuration < 0) fadeInDuration = _defaultFadeDuration;

		// If something else is already playing — crossfade
		if (_musicPlayer.Playing)
		{
			CrossfadeTo(track, fadeInDuration, targetBus);
			return;
		}

		_currentMusic = track;
		_musicPlayer.Stream = track;
		if (targetBus != null) _musicPlayer.Bus = targetBus;

		if (fadeInDuration > 0f)
		{
			_musicPlayer.VolumeDb = -80f;
			_musicPlayer.Play();

			_musicFadeTween?.Kill();
			_musicFadeTween = CreateTween();
			_musicFadeTween.TweenProperty(_musicPlayer, "volume_db", _currentTrackVolumeDb, fadeInDuration)
				.SetEase(Tween.EaseType.In)
				.SetTrans(Tween.TransitionType.Quad);
		}
		else
		{
			_musicPlayer.VolumeDb = _currentTrackVolumeDb;
			_musicPlayer.Play();
		}
	}

	/// <summary>
	/// Crossfades from the current track to a new one.
	/// </summary>
	public void CrossfadeTo(AudioStream newTrack, float fadeDuration = -1f, string targetBus = null)
	{
		if (newTrack == null) return;
		if (fadeDuration < 0) fadeDuration = _defaultFadeDuration;

		_musicFadeTween?.Kill();
		_musicFadeTween = CreateTween().SetParallel(true);

		// Fade out current
		_musicFadeTween.TweenProperty(_musicPlayer, "volume_db", -80f, fadeDuration)
			.SetEase(Tween.EaseType.Out)
			.SetTrans(Tween.TransitionType.Quad);

		// Switch track, apply target bus, and fade in to track offset
		_musicFadeTween.TweenCallback(Callable.From(() =>
		{
			_currentMusic = newTrack;
			_musicPlayer.Stream = newTrack;
			_musicPlayer.Play();
			if (targetBus != null) _musicPlayer.Bus = targetBus;
			_musicPlayer.VolumeDb = -80f;

			var fadeInTween = CreateTween();
			fadeInTween.TweenProperty(_musicPlayer, "volume_db", _currentTrackVolumeDb, fadeDuration * 0.8f)
				.SetEase(Tween.EaseType.In)
				.SetTrans(Tween.TransitionType.Quad);
		})).SetDelay(fadeDuration * 0.7f);
	}

	/// <summary>
	/// Stops the current music with optional fade-out.
	/// </summary>
	public void StopMusic(float fadeOutDuration = -1f)
	{
		if (fadeOutDuration < 0) fadeOutDuration = _defaultFadeDuration;

		_musicFadeTween?.Kill();

		if (fadeOutDuration > 0f && _musicPlayer.Playing)
		{
			_musicFadeTween = CreateTween();
			_musicFadeTween.TweenProperty(_musicPlayer, "volume_db", -80f, fadeOutDuration)
				.SetEase(Tween.EaseType.Out)
				.SetTrans(Tween.TransitionType.Quad);
			_musicFadeTween.TweenCallback(Callable.From(() =>
			{
				_musicPlayer.Stop();
				_currentMusic = null;
			}));
		}
		else
		{
			_musicPlayer.Stop();
			_currentMusic = null;
		}
	}

	/// <summary>
	/// Pauses the music (keeps the track loaded).
	/// </summary>
	public void PauseMusic()
	{
		if (_musicPlayer.Playing)
		{
			_musicPlayer.StreamPaused = true;
			_musicPaused = true;
		}
	}

	/// <summary>
	/// Resumes paused music.
	/// </summary>
	public void ResumeMusic()
	{
		if (_musicPaused)
		{
			_musicPlayer.StreamPaused = false;
			_musicPaused = false;
		}
	}

	/// <summary>
	/// Store the bus the music player was on before pause, so we can restore it.
	/// </summary>
	private string _busBeforePause;

	/// <summary>
	/// Switch the music to the muffled bus (low-pass + reverb) during pause.
	/// </summary>
	public void ApplyPauseEffect()
	{
		if (!_musicPlayer.Playing) return;
		_busBeforePause = _musicPlayer.Bus;
		_musicPlayer.Bus = MusicMuffledBus;
	}

	/// <summary>
	/// Restore the music to its pre-pause bus when unpausing.
	/// </summary>
	public void RemovePauseEffect()
	{
		if (!_musicPlayer.Playing) return;
		if (_busBeforePause != null)
			_musicPlayer.Bus = _busBeforePause;
	}

	// ══════════════════════════════════════════════════════════════════════
	//  MUSIC — game-state awareness
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Automatically picks the right music track and audio-bus (effects)
	/// for the current <see cref="GameManager.gameState"/>.
	/// Call this from <c>GameManager</c> whenever the state changes.
	/// </summary>
	/// <remarks>
	/// If the track is the same as the current one, only the bus (effects)
	/// are switched — playback continues seamlessly.
	/// </remarks>
	public void UpdateMusicForGameState()
	{
		AudioStream track = GetMusicForCurrentState();
		string bus = GetBusForCurrentState();
		_currentTrackVolumeDb = GetVolumeForCurrentState();
		PlayMusic(track, -1f, bus);
	}

	private AudioStream GetMusicForCurrentState()
	{
		return GameManager.Instance.CurrentState switch
		{
			GameManager.gameState.MainMenu  => _mainMenuMusic,
			GameManager.gameState.Preview   => _previewMusic,
			GameManager.gameState.Countdown => _countdownMusic,
			GameManager.gameState.Waiting   => _gameplayMusic,
			GameManager.gameState.Loading   => _currentMusic, // keep current during load
			_                               => _gameplayMusic,
		};
	}

	private string GetBusForCurrentState()
	{
		return GameManager.Instance.CurrentState switch
		{
			GameManager.gameState.MainMenu  => MusicBus,
			GameManager.gameState.Preview   => MusicMuffledBus,
			GameManager.gameState.Waiting   => MusicMuffledBus,
			GameManager.gameState.Countdown => MusicCountdownBus,
			GameManager.gameState.Loading   => _musicPlayer.Bus, // keep current during load
			_                               => MusicBus,
		};
	}

	private float GetVolumeForCurrentState()
	{
		return GameManager.Instance.CurrentState switch
		{
			GameManager.gameState.MainMenu  => _mainMenuVolumeDb,
			GameManager.gameState.Preview   => _previewVolumeDb,
			GameManager.gameState.Countdown => _countdownVolumeDb,
			GameManager.gameState.Waiting   => _gameplayVolumeDb,
			GameManager.gameState.Loading   => _currentTrackVolumeDb, // keep current during load
			_                               => _gameplayVolumeDb,
		};
	}

	/// <summary>
	/// Plays the victory music track on the normal Music bus.
	/// If the same track is already playing, only switches the bus + volume.
	/// </summary>
	public void PlayVictoryMusic()
	{
		_currentTrackVolumeDb = _victoryVolumeDb;
		if (_victoryMusic == _currentMusic && _musicPlayer.Playing)
		{
			if (_musicPlayer.Bus != MusicBus)
				_musicPlayer.Bus = MusicBus;
			_musicPlayer.VolumeDb = _currentTrackVolumeDb;
			return;
		}
		PlayMusic(_victoryMusic, 0.5f, MusicBus);
	}

	/// <summary>
	/// Plays the game-over / failure music track on the heavily-muffled bus.
	/// If the same track is already playing, only switches the bus + volume.
	/// </summary>
	public void PlayGameOverMusic()
	{
		_currentTrackVolumeDb = _gameOverVolumeDb;
		if (_gameOverMusic == _currentMusic && _musicPlayer.Playing)
		{
			if (_musicPlayer.Bus != MusicGameOverBus)
				_musicPlayer.Bus = MusicGameOverBus;
			_musicPlayer.VolumeDb = _currentTrackVolumeDb;
			return;
		}
		PlayMusic(_gameOverMusic, 0.5f, MusicGameOverBus);
	}

	// ══════════════════════════════════════════════════════════════════════
	//  2D SFX
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Plays a 2D (non-spatial) sound effect from the shared pool.
	/// </summary>
	/// <param name="sound">Audio stream to play.</param>
	/// <param name="volumeDb">Volume offset in dB (0 = default bus volume).</param>
	/// <param name="pitchScale">Pitch multiplier (1.0 = normal).</param>
	public void PlaySFX(AudioStream sound, float volumeDb = 0f, float pitchScale = 1f)
	{
		if (sound == null) return;

		var player = RentSFXPlayer();
		if (player == null) return;

		player.Stream = sound;
		player.VolumeDb = volumeDb;
		player.PitchScale = pitchScale;
		player.Play();
	}

	/// <summary>
	/// Plays a 2D SFX with randomised pitch (reduces repetition fatigue).
	/// </summary>
	public void PlaySFXVaried(AudioStream sound, float volumeDb = 0f,
		float minPitch = 0.9f, float maxPitch = 1.1f)
	{
		float pitch = (float)GD.RandRange(minPitch, maxPitch);
		PlaySFX(sound, volumeDb, pitch);
	}

	// ══════════════════════════════════════════════════════════════════════
	//  3D SFX  (spatial)
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Plays a spatial 3D sound effect at a world position (pooled player).
	/// </summary>
	public void PlaySFX3D(AudioStream sound, Vector3 position,
		float volumeDb = 0f, float pitchScale = 1f)
	{
		if (sound == null) return;

		var player = RentSFX3DPlayer();
		if (player == null) return;

		player.Stream = sound;
		player.VolumeDb = volumeDb;
		player.PitchScale = pitchScale;
		player.GlobalPosition = position;
		player.Play();
	}

	/// <summary>
	/// Plays a spatial 3D SFX at a world position with randomised pitch.
	/// </summary>
	public void PlaySFX3DVaried(AudioStream sound, Vector3 position, float volumeDb = 0f,
		float minPitch = 0.9f, float maxPitch = 1.1f)
	{
		float pitch = (float)GD.RandRange(minPitch, maxPitch);
		PlaySFX3D(sound, position, volumeDb, pitch);
	}

	/// <summary>
	/// Plays a spatial 3D SFX that follows a <c>Node3D</c> until it finishes.
	/// A dedicated <c>AudioStreamPlayer3D</c> is spawned as a child of the
	/// target node and freed automatically on completion.
	/// </summary>
	public void PlaySFX3DAttached(AudioStream sound, Node3D targetNode,
		float volumeDb = 0f, float pitchScale = 1f)
	{
		if (sound == null || targetNode == null) return;
		if (!IsInstanceValid(targetNode)) return;

		var player = new AudioStreamPlayer3D();
		player.Bus = Sfx3DBus;
		player.Stream = sound;
		player.VolumeDb = volumeDb;
		player.PitchScale = pitchScale;

		targetNode.AddChild(player);
		player.Play();

		player.Finished += () =>
		{
			if (IsInstanceValid(player))
				player.QueueFree();
		};
	}

	// ══════════════════════════════════════════════════════════════════════
	//  VOLUME CONTROL  (via audio buses)
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Sets volume on ALL music-related buses so the slider works
	/// regardless of which bus the current track is using.
	/// </summary>
	public void SetAllMusicVolumeDb(float volumeDb)
	{
		SetSingleBusVolume(MusicBus, volumeDb);
		SetSingleBusVolume(MusicMuffledBus, volumeDb);
		SetSingleBusVolume(MusicCountdownBus, volumeDb);
		SetSingleBusVolume(MusicGameOverBus, volumeDb);
	}

	public void SetSFXVolumeDb(float volumeDb)
	{
		int busIndex = AudioServer.GetBusIndex(SfxBus);
		if (busIndex >= 0) AudioServer.SetBusVolumeDb(busIndex, volumeDb);
	}

	public void SetSFX3DVolumeDb(float volumeDb)
	{
		int busIndex = AudioServer.GetBusIndex(Sfx3DBus);
		if (busIndex >= 0) AudioServer.SetBusVolumeDb(busIndex, volumeDb);
	}

	public float GetMusicVolumeDb()
	{
		int busIndex = AudioServer.GetBusIndex(MusicBus);
		return busIndex >= 0 ? AudioServer.GetBusVolumeDb(busIndex) : 0f;
	}

	public float GetSFXVolumeDb()
	{
		int busIndex = AudioServer.GetBusIndex(SfxBus);
		return busIndex >= 0 ? AudioServer.GetBusVolumeDb(busIndex) : 0f;
	}

	public float GetSFX3DVolumeDb()
	{
		int busIndex = AudioServer.GetBusIndex(Sfx3DBus);
		return busIndex >= 0 ? AudioServer.GetBusVolumeDb(busIndex) : 0f;
	}

	private static void SetSingleBusVolume(string busName, float volumeDb)
	{
		int busIndex = AudioServer.GetBusIndex(busName);
		if (busIndex >= 0) AudioServer.SetBusVolumeDb(busIndex, volumeDb);
	}

	// ══════════════════════════════════════════════════════════════════════
	//  SFX LIBRARY CONVENIENCE METHODS
	// ══════════════════════════════════════════════════════════════════════

	// ── UI ───────────────────────────────────────────────────────────────

	/// <summary>Play the UI focus-change sound.</summary>
	public void PlayUIFocusChange() =>
		PlaySFX(_sfxLibrary?.UIFocusChange);

	/// <summary>Play the UI button-click sound.</summary>
	public void PlayUIButtonClick() =>
		PlaySFX(_sfxLibrary?.UIButtonClick);

	// ── Gameplay ─────────────────────────────────────────────────────────

	/// <summary>Play the finish-line / level-complete sound.</summary>
	public void PlayFinishLine() =>
		PlaySFX(_sfxLibrary?.FinishLine);

	/// <summary>Play a laser-fire sound.</summary>
	public void PlayLaserFire() =>
		PlaySFX(_sfxLibrary?.LaserFire);

	/// <summary>Play the sound of the player entering / touching a laser.</summary>
	public void PlayLaserEnter() =>
		PlaySFX(_sfxLibrary?.LaserEnter);

	// ── Player Movement ──────────────────────────────────────────────────

	/// <summary>Play a footstep sound.</summary>
	public void PlayPlayerFootstep() =>
		PlaySFX(_sfxLibrary?.PlayerFootstep);

	/// <summary>Play a jump sound.</summary>
	public void PlayPlayerJump() =>
		PlaySFX(_sfxLibrary?.PlayerJump);

	/// <summary>Play a landing sound.</summary>
	public void PlayPlayerLand() =>
		PlaySFX(_sfxLibrary?.PlayerLand);

	// ── Timer / Countdown ────────────────────────────────────────────────

	/// <summary>Play the bet / countdown tick sound.</summary>
	public void PlayTimerBetTick() =>
		PlaySFX(_sfxLibrary?.TimerBetTick);

	/// <summary>Play the limit-time warning sound (plays when time is almost up).</summary>
	public void PlayTimerLimitWarning() =>
		PlaySFX(_sfxLibrary?.TimerLimitWarning);

	/// <summary>
	/// Play the limit-time ended sound (one-shot — nie zapętla się,
	/// nawet jeśli plik audio ma loop=true w imporcie).
	/// </summary>
	public void PlayTimerLimitEnd()
	{
		var sound = _sfxLibrary?.TimerLimitEnd;
		if (sound == null) return;

		// Dedicated player — zatrzymuje się po naturalnej długości streamu
		var player = new AudioStreamPlayer();
		player.Bus = SfxBus;
		player.Stream = sound;
		AddChild(player);
		player.Play();

		double length = sound.GetLength();
		if (length > 0.0)
		{
			var timer = GetTree().CreateTimer(length);
			timer.Timeout += () =>
			{
				if (IsInstanceValid(player))
				{
					player.Stop();
					player.QueueFree();
				}
			};
		}
		else
		{
			// Fallback: jeśli długość nieznana, zatrzymaj po 1 sekundzie
			var timer = GetTree().CreateTimer(1.0);
			timer.Timeout += () =>
			{
				if (IsInstanceValid(player))
				{
					player.Stop();
					player.QueueFree();
				}
			};
		}
	}

	// ══════════════════════════════════════════════════════════════════════
	//  LOOPING / CONTINUOUS SFX
	// ══════════════════════════════════════════════════════════════════════

	/// <summary>
	/// Starts (or switches) a looping sound that plays continuously until
	/// <see cref="StopLoopingSFX"/> is called. Only one looping SFX can play
	/// at a time — calling this again replaces the current loop.
	/// </summary>
	/// <param name="sound">Audio stream to loop (should have loop=true in import).</param>
	/// <param name="pitchScale">Pitch multiplier (1.0 = normal, 2.0 = double speed).</param>
	/// <param name="volumeDb">Volume offset in dB (0 = default bus).</param>
	public void StartLoopingSFX(AudioStream sound, float pitchScale = 1f, float volumeDb = 0f)
	{
		if (sound == null) return;

		if (_loopingSFXPlayer == null)
		{
			_loopingSFXPlayer = new AudioStreamPlayer();
			_loopingSFXPlayer.Name = "LoopingSFX";
			_loopingSFXPlayer.Bus = SfxBus;
			AddChild(_loopingSFXPlayer);
		}

		// Already playing this exact stream at the same pitch — no-op
		if (_loopingSFXPlayer.Playing
			&& _loopingSFXPlayer.Stream == sound
			&& Mathf.Abs(_loopingSFXPlayer.PitchScale - pitchScale) < 0.01f)
			return;

		_loopingSFXPlayer.Stop();
		_loopingSFXPlayer.Stream = sound;
		_loopingSFXPlayer.PitchScale = pitchScale;
		_loopingSFXPlayer.VolumeDb = volumeDb;
		_loopingSFXPlayer.Play();
	}

	/// <summary>
	/// Stops the currently looping SFX, if any.
	/// </summary>
	public void StopLoopingSFX()
	{
		if (_loopingSFXPlayer != null && _loopingSFXPlayer.Playing)
			_loopingSFXPlayer.Stop();
	}

	// ══════════════════════════════════════════════════════════════════════
	//  POOL HELPERS
	// ══════════════════════════════════════════════════════════════════════

	private AudioStreamPlayer RentSFXPlayer()
	{
		for (int i = 0; i < _sfxPoolSize; i++)
		{
			_sfxIndex = (_sfxIndex + 1) % _sfxPoolSize;
			var player = _sfxPool[_sfxIndex];
			if (!player.Playing)
				return player;
		}

		// All busy — recycle the next one
		_sfxIndex = (_sfxIndex + 1) % _sfxPoolSize;
		var recycled = _sfxPool[_sfxIndex];
		recycled.Stop();
		return recycled;
	}

	private AudioStreamPlayer3D RentSFX3DPlayer()
	{
		for (int i = 0; i < _sfx3DPoolSize; i++)
		{
			_sfx3DIndex = (_sfx3DIndex + 1) % _sfx3DPoolSize;
			var player = _sfx3DPool[_sfx3DIndex];
			if (!player.Playing)
				return player;
		}

		// All busy — recycle the next one
		_sfx3DIndex = (_sfx3DIndex + 1) % _sfx3DPoolSize;
		var recycled = _sfx3DPool[_sfx3DIndex];
		recycled.Stop();
		return recycled;
	}
}

