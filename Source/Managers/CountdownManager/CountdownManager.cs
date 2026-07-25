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

	// ── Level stats ────────────────────────────────────────────────────────
	public struct LevelStat
	{
		public string LevelId;
		public double BetTime;
		public double ActualTime;
		public double Overshoot;
		public double Penalty;
	}

	private List<LevelStat> _levelHistory = new();
	public IReadOnlyList<LevelStat> LevelHistory => _levelHistory.AsReadOnly();

	// ── Level time configuration (level ID → base time in seconds) ──────────
	private Dictionary<string, double> _levelBaseTimes = new()
	{
		{ "Level1",   30.0 },
		{ "Level2",   20.0 },
		{ "Level3",   20.0 },
		{ "Level4",   20.0 },
		{ "level5",   20.0 },
		{ "Level6", 20.0 },
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
	/// <summary>Set by GameOverlay when countdown stops.</summary>
	public double ActualTimeUsed       { get; private set; }

	/// <summary>
	/// The UI slider sets this before the player confirms the bet.
	/// GameManager reads it inside <see cref="PlaceBet()"/> (parameterless).
	/// </summary>
	public double PendingBet { get; set; }

	// ── Public API ──────────────────────────────────────────────────────────

	/// <summary>
	/// Store the actual elapsed time (set by GameOverlay on stop).
	/// </summary>
	public void SetActualTimeUsed(double time) => ActualTimeUsed = time;

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
	/// If overshoot > 0:
	///   - time is deducted as if the player had bet the actual time used,
	///   - then an ADDITIONAL penalty equal to the overshoot is applied,
	///   - the overshoot also reduces the next level's time allocation.
	/// Returns true if the player still has time left, false if bankrupt.
	/// </summary>
	public bool OnLevelFinished(double actualTimeUsed)
	{
		double overshoot = Math.Max(0.0, actualTimeUsed - _currentBetTime);
		double penalty = 0.0;

		if (overshoot > 0.0)
		{
			penalty = overshoot; // additional penalty = overshoot amount
			_totalAvailableTime -= overshoot; // correction
			_totalAvailableTime -= overshoot; // penalty
			_penaltyForNextLevel = overshoot;
		}

		// Save level stats
		_levelHistory.Add(new LevelStat
		{
			LevelId = _currentLevelId,
			BetTime = _currentBetTime,
			ActualTime = actualTimeUsed,
			Overshoot = overshoot,
			Penalty = penalty,
		});

		EmitSignal(nameof(TimeExceeded), overshoot, Math.Max(0.0, _totalAvailableTime));

		return _totalAvailableTime > 0.0;
	}

	/// <summary>True if the player has run out of total time.</summary>
	public bool IsBankrupt => _totalAvailableTime <= 0.0;

	/// <summary>Bankruptcy check that also considers the next level's bonus time.</summary>
	public bool IsEffectivelyBankrupt
	{
		get
		{
			double nextBonus = GetNextLevelBaseTime();
			double adjustedNext = Math.Max(0.0, nextBonus - _penaltyForNextLevel);
			return _totalAvailableTime + adjustedNext <= 0.0;
		}
	}

	/// <summary>
	/// Base time of the level that follows the current one, or 0 if unknown.
	/// </summary>
	public double GetNextLevelBaseTime()
	{
		if (string.IsNullOrEmpty(_currentLevelId)) return 0.0;
		string numStr = _currentLevelId.Replace("Level", "");
		if (int.TryParse(numStr, out int num))
		{
			string nextId = $"Level{num + 1}";
			return GetLevelBaseTime(nextId);
		}
		return 0.0;
	}

	/// <summary>
	/// Effective time limit shown during gameplay:
	/// (totalAvailablePool + nextLevelBonus) / 2
	/// The bet is NOT included — it counts down separately in the main timer.
	/// When this reaches ≤ 0 the player is bankrupt mid-level.
	/// </summary>
	public double GetEffectiveLimit(double _ = 0)
	{
		return (_totalAvailableTime + GetNextLevelBaseTime()) * 0.5;
	}

	/// <summary>Reset all state for a new game.</summary>
	public void Reset()
	{
		_currentLevelId = "";
		_totalAvailableTime = 0.0;
		_currentLevelBaseTime = 0.0;
		_currentBetTime = 0.0;
		_penaltyForNextLevel = 0.0;
		_betPlaced = false;
		ActualTimeUsed = 0.0;
		PendingBet = 0.0;
		_levelHistory.Clear();
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
