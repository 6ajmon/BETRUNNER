using Godot;
using System;
using System.Collections.Generic;
using NewGameProject;

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

	// ── Difficulty ─────────────────────────────────────────────────────────
	public enum Difficulty
	{
		Noob = 0,
		Pro  = 1,
		Dev  = 2,
	}

	/// <summary>
	/// Current difficulty setting. Updated by SettingsOverlay slider.
	/// </summary>
	public Difficulty CurrentDifficulty { get; set; } = Difficulty.Pro;

	/// <summary>
	/// Run key behaviour. false (default) = pressing Run makes you sprint,
	/// true = pressing Run makes you walk. Toggled via SettingsOverlay.
	/// </summary>
	public bool RunMakesYouWalk { get; set; } = true;

	// ── Level time configuration (per difficulty, by level index 0..N) ─────
	private static readonly double[] _baseTimesNoob  = { 60.0, 40.0, 40.0, 40.0, 50.0, 60.0, 40.0, 40.0, 60.0};
	private static readonly double[] _baseTimesPro   = { 30.0, 20.0, 20.0, 20.0, 20.0, 30.0, 20.0, 20.0, 30.0 };
	private static readonly double[] _baseTimesDev   = { 13.0, 10.0, 8.0, 10.0, 14.0, 20.0, 10.0, 12.0, 15.0 };

	// ── Runtime state ───────────────────────────────────────────────────────
	private string _currentLevelId = "";
	private double _totalAvailableTime = 0.0;   // player's total time pool
	private double _currentLevelBaseTime = 0.0; // this level's allocation (after penalty)
	private double _currentBetTime = 0.0;       // time the player betd
	private double _penaltyForNextLevel = 0.0;  // excess to subtract from next level
	private double _penaltyAppliedToCurrentLevel = 0.0; // penalty deducted when allocating this level
	private double _totalBeforeLevelAllocation = 0.0;   // total available before this level's base time was added
	private double _totalBeforeBet = 0.0;       // total available right before placing bet (for progress)
	private bool _betPlaced = false;

	// ── Public read-only properties ─────────────────────────────────────────
	public double TotalAvailableTime   => _totalAvailableTime;
	public double CurrentLevelBaseTime => _currentLevelBaseTime;
	public double CurrentBetTime       => _currentBetTime;
	public double PenaltyForNextLevel  => _penaltyForNextLevel;
	public double PenaltyAppliedToCurrentLevel => _penaltyAppliedToCurrentLevel;
	public bool   IsBetPlaced          => _betPlaced;
	public string CurrentLevelId       => _currentLevelId;
	/// <summary>Set by GameOverlay when countdown stops.</summary>
	public double ActualTimeUsed       { get; private set; }

	// ── Progress bar helpers ────────────────────────────────────────────────
	/// <summary>Total available BEFORE this level's base time was added (for summary calculation).</summary>
	public double TotalBeforeLevelAllocation => _totalBeforeLevelAllocation;
	/// <summary>Total available BEFORE the bet was placed (for betting progress bar).</summary>
	public double TotalBeforeBet => _totalBeforeBet;

	/// <summary>Ratio 0..1 of the bet amount relative to what was available before betting.</summary>
	public double BetRatio => _totalBeforeBet > 0.0 ? _currentBetTime / _totalBeforeBet : 0.0;

	/// <summary>Ratio 0..1 of remaining pool relative to the pool before this level's allocation.</summary>
	public double PoolRatio
	{
		get
		{
			double beforeLevel = _totalAvailableTime + _currentBetTime; // approximate
			return beforeLevel > 0.0 ? Math.Clamp(_totalAvailableTime / beforeLevel, 0.0, 1.0) : 0.0;
		}
	}

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
		_totalBeforeLevelAllocation = _totalAvailableTime;

		double baseTime = GetLevelBaseTime(levelId);
		if (baseTime < 0.0)
		{
			GD.PrintErr($"CountdownManager: Unknown level '{levelId}'");
			_currentLevelBaseTime = 0.0;
			EmitSignal(nameof(LevelTimeAllocated), 0.0, _totalAvailableTime);
			return;
		}

		double adjustedTime = Math.Max(0.0, baseTime - _penaltyForNextLevel);

		_currentLevelBaseTime = adjustedTime;
		_penaltyAppliedToCurrentLevel = baseTime - adjustedTime;
		_totalAvailableTime += adjustedTime;
		_penaltyForNextLevel = 0.0;

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
		_totalBeforeBet = _totalAvailableTime;
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

	/// <summary>Bankruptcy check — only considers the current remaining pool.</summary>
	public bool IsEffectivelyBankrupt => _totalAvailableTime <= 0.0;

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
	/// Effective time limit shown during gameplay.
	/// Pokazuje rzeczywistą pozostałą pulę (bez halvingu).
	/// Limit wizualnie leci 2× szybciej przez karę overshootu.
	/// When this reaches ≤ 0 the player is bankrupt mid-level.
	/// </summary>
	public double GetEffectiveLimit(double _ = 0)
	{
		return _totalAvailableTime;
	}

	/// <summary>Reset all state for a new game.</summary>
	public void Reset()
	{
		_currentLevelId = "";
		_totalAvailableTime = 0.0;
		_currentLevelBaseTime = 0.0;
		_currentBetTime = 0.0;
		_penaltyForNextLevel = 0.0;
		_penaltyAppliedToCurrentLevel = 0.0;
		_totalBeforeLevelAllocation = 0.0;
		_totalBeforeBet = 0.0;
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
	/// Look up a level's configured base time (ignoring penalties),
	/// based on the current difficulty. Returns -1 if the level index is out of range.
	/// </summary>
	public double GetLevelBaseTime(string levelId)
	{
		int index = ParseLevelIndex(levelId);
		if (index < 0) return -1.0;

		double[] table = CurrentDifficulty switch
		{
			Difficulty.Noob => _baseTimesNoob,
			Difficulty.Dev  => _baseTimesDev,
			_               => _baseTimesPro,
		};

		return index < table.Length ? table[index] : -1.0;
	}

	/// <summary>
	/// Override the base time for a given level at runtime.
	/// Modifies the current difficulty's array (only if index is in range).
	/// </summary>
	public void SetLevelBaseTime(string levelId, double seconds)
	{
		int index = ParseLevelIndex(levelId);
		if (index < 0) return;

		double[] table = CurrentDifficulty switch
		{
			Difficulty.Noob => _baseTimesNoob,
			Difficulty.Dev  => _baseTimesDev,
			_               => _baseTimesPro,
		};

		if (index < table.Length)
			table[index] = Math.Max(0.0, seconds);
	}

	/// <summary>Extract the zero-based level index from "Level1", "Level2" …</summary>
	private static int ParseLevelIndex(string levelId)
	{
		if (string.IsNullOrEmpty(levelId)) return -1;
		string numStr = levelId.Replace("Level", "");
		if (int.TryParse(numStr, out int num))
			return num - 1; // zero-based
		return -1;
	}

	// ── Time graph data ─────────────────────────────────────────────────────

	/// <summary>
	/// A single segment of the time graph with its intended draw color.
	/// </summary>
	public struct GraphSegment
	{
		public Vector2 Start;
		public Vector2 End;
		public Color Color;
	}

	/// <summary>
	/// Build colored segments for the remaining-time-over-game-time graph.
	/// Segments are colored by type:
	///   Bonus (amber)  — level allocation jump
	///   Bet (green)    — bet consumption during normal play
	///   Limit (orange) — flat / no-change
	///   Penalty (red)  — overtime 2× drain
	/// </summary>
	public (GraphSegment[] Segments, (float X, string Label)[] Markers) BuildTimeGraphData(bool isVictory = false)
	{
		if (_levelHistory.Count == 0)
			return (Array.Empty<GraphSegment>(), Array.Empty<(float, string)>());

		var segs = new List<GraphSegment>();
		var markers = new List<(float, string)>();

		double gameTime = 0.0;
		double pool = 0.0;
		double prevOvershoot = 0.0;

		bool lastLevel = false;

		for (int i = 0; i < _levelHistory.Count; i++)
		{
			var stat = _levelHistory[i];
			double baseTime = GetLevelBaseTime(stat.LevelId);
			double effectiveBase = Math.Max(0.0, baseTime - prevOvershoot);

			lastLevel = (i == _levelHistory.Count - 1);

			// ── Level allocation (amber) ─────────────────────────────────
			double poolBeforeLevel = pool;
			pool += effectiveBase;
			segs.Add(new GraphSegment
			{
				Start = new Vector2((float)gameTime, (float)poolBeforeLevel),
				End   = new Vector2((float)gameTime, (float)pool),
				Color = UIColors.Bonus,
			});

			// Level marker on x-axis
			markers.Add(((float)gameTime, $"L{i + 1}"));

			// ── Bet / time consumption ────────────────────────────────────
			double actualTime = stat.ActualTime;
			double betTime = stat.BetTime;
			double overshoot = stat.Overshoot;
			double poolAfterAllocation = pool;

			// W trybie victory ostatni poziom odejmuje tylko faktyczny czas (biały)
			bool useActualConsumption = isVictory && lastLevel;
			double consumption = useActualConsumption ? actualTime : betTime;
			Color consumptionColor = useActualConsumption
				? Colors.White
				: UIColors.Bet;

			double poolAfterConsumption = poolAfterAllocation - consumption;
			double normalPlayTime = Math.Min(consumption, actualTime);
			double normalEnd = gameTime + normalPlayTime;

			segs.Add(new GraphSegment
			{
				Start = new Vector2((float)gameTime, (float)poolAfterAllocation),
				End   = new Vector2((float)normalEnd, (float)poolAfterConsumption),
				Color = consumptionColor,
			});

			if (overshoot > 0.001)
			{
				// Overtime 2× drain (red)
				double penaltyDrain = Math.Min(overshoot * 2.0, poolAfterConsumption);
				double poolAfterPenalty = poolAfterConsumption - penaltyDrain;
				double overtimeEnd = gameTime + actualTime;
				segs.Add(new GraphSegment
				{
					Start = new Vector2((float)normalEnd, (float)poolAfterConsumption),
					End   = new Vector2((float)overtimeEnd, (float)poolAfterPenalty),
					Color = UIColors.Penalty,
				});
				pool = poolAfterPenalty;
			}
			else
			{
				// No overshoot: flat (orange)
				double levelEnd = gameTime + actualTime;
				if (actualTime > normalPlayTime)
				{
					segs.Add(new GraphSegment
					{
						Start = new Vector2((float)normalEnd, (float)poolAfterConsumption),
						End   = new Vector2((float)levelEnd, (float)poolAfterConsumption),
						Color = UIColors.Limit,
					});
				}
				pool = poolAfterConsumption;
			}

			gameTime += actualTime;
			prevOvershoot = stat.Overshoot;
		}

		return (segs.ToArray(), markers.ToArray());
	}
}
