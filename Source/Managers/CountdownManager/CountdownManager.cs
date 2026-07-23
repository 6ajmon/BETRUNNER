using Godot;
using System;
using System.Collections.Generic;

public partial class CountdownManager : Node
{
	public static CountdownManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<CountdownManager>("CountdownManager");

	/// <summary>
	/// Emitted when a level's time is allocated — the player now sees their total pool
	/// and the base time for this level.
	/// </summary>
	[Signal]
	public delegate void LevelTimeAllocatedEventHandler(double levelBaseTime, double totalAvailableTime);

	/// <summary>
	/// Emitted when the player confirms their bet.
	/// </summary>
	[Signal]
	public delegate void BetPlacedEventHandler(double betTime, double remainingTime);

	/// <summary>
	/// Emitted when the player exceeds their bet time — excess starts draining
	/// from the remaining pool and will penalise the next level's allocation.
	/// </summary>
	[Signal]
	public delegate void TimeExceededEventHandler(double excessTime, double remainingTime);

	// ── Level time configuration (level ID → base time in seconds) ──────────
	private Dictionary<string, double> _levelBaseTimes = new()
	{
		{ "Tutorial", 120.0 },
		{ "Level1",   60.0 },
        { "Level2",   40.0 },
        { "Level3",   30.0 },
	};

	// ── Runtime state ───────────────────────────────────────────────────────
	private string _currentLevelId = "";
	private double _totalAvailableTime = 0.0;   // player's total time pool
	private double _currentLevelBaseTime = 0.0; // this level's allocation (after penalty)
	private double _currentBetTime = 0.0;       // time the player bet
	private double _penaltyForNextLevel = 0.0;  // excess to subtract from next level
	private bool _betPlaced = false;

	// ── Public read-only properties ─────────────────────────────────────────
	public double TotalAvailableTime   => _totalAvailableTime;
	public double CurrentLevelBaseTime => _currentLevelBaseTime;
	public double CurrentBetTime       => _currentBetTime;
	public double PenaltyForNextLevel  => _penaltyForNextLevel;
	public bool   IsBetPlaced          => _betPlaced;
	public string CurrentLevelId       => _currentLevelId;

	/// <summary>
	/// The UI slider sets this before the player confirms the bet.
	/// GameManager reads it inside <see cref="PlaceBet()"/> (parameterless).
	/// </summary>
	public double PendingBet { get; set; }

	// ── Public API ──────────────────────────────────────────────────────────

	/// <summary>
	/// Call this when a level loads.  Adds the level's base time (minus any
	/// penalty from a previous overshoot) to the player's total pool and emits
	/// <see cref="LevelTimeAllocated"/> so the UI can show the slider.
	/// </summary>
	public void SetCurrentLevel(string levelId)
	{
		_currentLevelId = levelId;
		_betPlaced = false;

		if (!_levelBaseTimes.ContainsKey(levelId))
		{
			GD.PrintErr($"CountdownManager: Unknown level '{levelId}'");
			_currentLevelBaseTime = 0.0;
			EmitSignal(nameof(LevelTimeAllocated), 0.0, _totalAvailableTime);
			return;
		}

		double baseTime = _levelBaseTimes[levelId];
		double adjustedTime = Math.Max(0.0, baseTime - _penaltyForNextLevel);

		_currentLevelBaseTime = adjustedTime;
		_totalAvailableTime += adjustedTime;
		_penaltyForNextLevel = 0.0; // consumed

		EmitSignal(nameof(LevelTimeAllocated), _currentLevelBaseTime, _totalAvailableTime);
	}

	/// <summary>
	/// The player confirms a bet.  The bet amount is immediately subtracted
	/// from the total available time.  Clamped to [0, totalAvailable].
	/// </summary>
	public void PlaceBet(double betTime)
	{
		if (_betPlaced) return;

		betTime = Math.Clamp(betTime, 0.0, _totalAvailableTime);
		_currentBetTime = betTime;
		_totalAvailableTime -= betTime;
		_betPlaced = true;

		EmitSignal(SignalName.BetPlaced, _currentBetTime, _totalAvailableTime);
	}

	/// <summary>
	/// Parameterless overload — uses <see cref="PendingBet"/> as the bet amount.
	/// Called by GameManager when <c>EndBettingPhase</c> fires.
	/// </summary>
	public void PlaceBet()
	{
		PlaceBet(PendingBet);
	}

	/// <summary>
	/// Call this when the player finishes the level (success or failure).
	/// If they exceeded their bet the extra time is drained from the pool
	/// and the excess becomes a penalty on the next level's allocation.
	/// </summary>
	/// <param name="actualTimeUsed">Real time the player spent.</param>
	public void OnLevelFinished(double actualTimeUsed)
	{
		double overshoot = actualTimeUsed - _currentBetTime;

		if (overshoot > 0.0)
		{
			_totalAvailableTime -= overshoot;
			_penaltyForNextLevel = overshoot;

			EmitSignal(nameof(TimeExceeded), overshoot, Math.Max(0.0, _totalAvailableTime));
		}
		// If overshoot ≤ 0 → player finished within bet.  The unused bet
		// time is already gone (it was subtracted in PlaceBet).
	}

	/// <summary>
	/// Maximum bet the player can place right now.
	/// </summary>
	public double GetMaxBet() => _totalAvailableTime;

	/// <summary>
	/// Look up a level's configured base time (ignoring penalties).
	/// </summary>
	public double GetLevelBaseTime(string levelId) =>
		_levelBaseTimes.TryGetValue(levelId, out double time) ? time : 0.0;

	/// <summary>
	/// Override the base time for a given level at runtime.
	/// </summary>
	public void SetLevelBaseTime(string levelId, double seconds)
	{
		_levelBaseTimes[levelId] = Math.Max(0.0, seconds);
	}
}
