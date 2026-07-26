using Godot;
using System;

/// <summary>
/// Okrągły progress bar w stylu stopera — dwa koncentryczne pierścienie:
///   - Zewnętrzny (grubszy): zielony — czas z betu
///   - Wewnętrzny (cieńszy):  czerwony — limit (pula zapasowa)
/// Tło: czarne wypełnienie koła z obramowaniem.
/// W środku wyświetlane są liczniki betu i limitu z animacją skalowania.
/// </summary>
public partial class StoperProgressBar : Control
{
	// ── Eksporty ────────────────────────────────────────────────────────────
	[Export] private Color _betColor       = new Color(0.4117647f, 0.9411765f, 0.68235296f);
	[Export] private Color _limitColor     = new Color(1f, 0.43137255f, 0.2509804f);
	[Export] private Color _bgColor        = new Color(0.0f, 0.0f, 0.0f);
	[Export] private Color _borderColor    = new Color(0.5f, 0.5f, 0.5f);
	[Export] private float _outerBorderWidth = 3f;
	[Export] private float _ringBorderWidth  = 2f;
	[Export] private float _outerRingWidth = 14f;
	[Export] private float _innerRingWidth = 8f;
	[Export] private float _gap = 4f;
	[Export] private int   _arcSegments = 120;

	// ── Znaczniki (jak na zegarku) ──────────────────────────────────────────
	[Export] private Color _tickColor      = new Color(0.6f, 0.6f, 0.6f, 0.5f);
	[Export] private float _largeTickLen   = 18f;
	[Export] private float _smallTickLen   = 10f;
	[Export] private int   _largeTicks     = 12;
	[Export] private int   _smallPerLarge   = 4; // ile małych między dużymi

	// ── Czcionka ────────────────────────────────────────────────────────────
	[Export] private int _activeFontSize   = 28;
	[Export] private int _inactiveFontSize = 18;

	public enum ActiveTimerEnum { Bet, Limit }

	// ── Wartości do rysowania ───────────────────────────────────────────────
	public float BetRatio   { get; set; } = 1f;
	public float LimitRatio { get; set; } = 1f;
	public string BetTimerText   { get; set; } = "";
	public string LimitTimerText { get; set; } = "";
	public ActiveTimerEnum ActiveTimer { get; set; } = ActiveTimerEnum.Bet;

	private Label _betLabel;
	private Label _limitLabel;
	private Label _multiplierLabel;
	private Tween _tween;
	private Tween _pulseTween;
	private ActiveTimerEnum _lastActiveTimer = ActiveTimerEnum.Bet;

	// ── Timer sound state ──────────────────────────────────────────────────
	/// <summary>Ustaw na true gdy stoper faktycznie odlicza (czas leci).</summary>
	public bool TimerFlowing { get; set; } = false;
	private ActiveTimerEnum _lastActiveTimerForSound = ActiveTimerEnum.Bet;
	private float _lastLimitRatioForSound = 1f;
	private bool _limitEndPlayedAtHalf;

	/// <summary>Mnożnik jasności dla pulsującego limitu (0..1).</summary>
	public float LimitPulseFactor { get; set; } = 1f;
	/// <summary>Wartość 0..1 sterująca pulsacją (animowana przez tween).</summary>
	public float PulseValue { get; set; } = 0f;

	// ── Lifecycle ───────────────────────────────────────────────────────────

	public override void _Ready()
	{
		// Górny label — bet (zielony)
		_betLabel = new Label();
		_betLabel.Name = "BetTimerLabel";
		_betLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_betLabel.VerticalAlignment   = VerticalAlignment.Bottom;
		_betLabel.AddThemeColorOverride("font_color", _betColor);
		_betLabel.AddThemeFontSizeOverride("font_size", _activeFontSize);
		AddChild(_betLabel);

		// Dolny label — limit (czerwony)
		_limitLabel = new Label();
		_limitLabel.Name = "LimitTimerLabel";
		_limitLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_limitLabel.VerticalAlignment   = VerticalAlignment.Top;
		_limitLabel.AddThemeColorOverride("font_color", _limitColor);
		_limitLabel.AddThemeFontSizeOverride("font_size", _inactiveFontSize);
		AddChild(_limitLabel);

		// Label "2x" — pod limitem, pokazuje że czas leci w przyśpieszeniu
		_multiplierLabel = new Label();
		_multiplierLabel.Name = "MultiplierLabel";
		_multiplierLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_multiplierLabel.VerticalAlignment   = VerticalAlignment.Top;
		_multiplierLabel.AddThemeColorOverride("font_color", _limitColor);
		_multiplierLabel.AddThemeFontSizeOverride("font_size", 12);
		_multiplierLabel.Text = "2×";
		_multiplierLabel.Scale = Vector2.Zero; // schowany na starcie
		AddChild(_multiplierLabel);

		UpdateLabelLayout();
	}

	public override void _Notification(int what)
	{
		if (what == NotificationResized)
			UpdateLabelLayout();
	}

	// ── Drawing ─────────────────────────────────────────────────────────────

	public override void _Draw()
	{
		float halfW = Size.X * 0.5f;
		float halfH = Size.Y * 0.5f;
		float radius = Mathf.Min(halfW, halfH) - 2f;
		Vector2 center = new Vector2(halfW, halfH);

		// 0. Zewnętrzne obramowanie całego stopera
		DrawCircle(center, radius + _outerBorderWidth, _borderColor);

		// 1. Tło — wypełnione koło
		DrawCircle(center, radius, _bgColor);

		// 2. Czerwony pierścień (limit) — wewnętrzny, rysowany pierwszy = pod spodem
		float innerRadius = radius - _outerRingWidth - _gap;
		if (innerRadius > 0f && LimitRatio > 0.001f)
		{
			float limitAngle = Mathf.Tau * LimitRatio;
			float from = -Mathf.Pi * 0.5f;
			float to   = from + limitAngle;

			// Obramowanie pierścienia (szersze)
			DrawArc(center, innerRadius, from, to,
				_arcSegments, _borderColor, _innerRingWidth + _ringBorderWidth * 2f, true);
			// Wypełnienie (z uwzględnieniem pulsacji)
			Color pulsedLimit = new Color(
				Mathf.Min(_limitColor.R * LimitPulseFactor, 1f),
				Mathf.Min(_limitColor.G * LimitPulseFactor, 1f),
				Mathf.Min(_limitColor.B * LimitPulseFactor, 1f),
				_limitColor.A);
			DrawArc(center, innerRadius, from, to,
				_arcSegments, pulsedLimit, _innerRingWidth, true);
		}

		// 3. Zielony pierścień (bet) — zewnętrzny, rysowany później = na wierzchu
		if (BetRatio > 0.001f)
		{
			float betAngle = Mathf.Tau * BetRatio;
			float betRadius = radius - _outerRingWidth * 0.5f;
			float from = -Mathf.Pi * 0.5f;
			float to   = from + betAngle;

			// Obramowanie pierścienia
			DrawArc(center, betRadius, from, to,
				_arcSegments, _borderColor, _outerRingWidth + _ringBorderWidth * 2f, true);
			// Wypełnienie
			DrawArc(center, betRadius, from, to,
				_arcSegments, _betColor, _outerRingWidth, true);
		}

		// 4. Znaczniki jak na zegarku — na wierzchu, nad pierścieniami
		DrawTicks(center, radius);
	}

	/// <summary>
	/// Rysuje znaczniki jak na tarczy zegarka — duże (12) i małe (4 między każdą parą).
	/// </summary>
	private void DrawTicks(Vector2 center, float radius)
	{
		if (_tickColor.A < 0.01f) return;

		float outerR = radius - 3f;
		float step = Mathf.Tau / _largeTicks;
		float subStep = step / (_smallPerLarge + 1);

		// Małe kreski — 4 między każdą parą dużych
		for (int i = 0; i < _largeTicks; i++)
		{
			for (int j = 0; j < _smallPerLarge; j++)
			{
				float angle = -Mathf.Pi * 0.5f + step * i + subStep * (j + 1);
				Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
				DrawLine(center + dir * outerR, center + dir * (outerR - _smallTickLen), _tickColor, 1f);
			}
		}

		// Duże kreski — 12
		for (int i = 0; i < _largeTicks; i++)
		{
			float angle = -Mathf.Pi * 0.5f + step * i;
			Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
			DrawLine(center + dir * outerR, center + dir * (outerR - _largeTickLen), _tickColor, 2f);
		}
	}

	// ── Public API ──────────────────────────────────────────────────────────

	/// <summary>Odświeża rysunek i aktualizuje teksty / animację.</summary>
	public void UpdateProgress()
	{
		QueueRedraw();

		if (_betLabel != null)
		{
			_betLabel.Text = BetTimerText;
			_betLabel.AddThemeColorOverride("font_color", _betColor);
		}
		if (_limitLabel != null)
		{
			_limitLabel.Text = LimitTimerText;
			_limitLabel.AddThemeColorOverride("font_color", _limitColor);
		}

		// Animacja powiększania aktywnego timera
		if (ActiveTimer != _lastActiveTimer)
		{
			AnimateActiveSwitch();
			_lastActiveTimer = ActiveTimer;
		}

		// Pulsacja gdy limit spadnie poniżej połowy
		bool warning = ActiveTimer == ActiveTimerEnum.Limit && LimitRatio < 0.5f;
		SetLimitWarning(warning);
		ApplyPulse();

		// Dźwięki stopera
		UpdateTimerSound();
	}

	// ── Pulsacja limitu ─────────────────────────────────────────────────────

	private void SetLimitWarning(bool warning)
	{
		if (warning)
		{
			if (_pulseTween != null && _pulseTween.IsValid())
				return;

			PulseValue = 0f;
			_pulseTween = CreateTween().SetLoops();
			// Sekwencja: 0 → 1 → 0 → 1 → ...
			_pulseTween.TweenProperty(this, "PulseValue", 1f, 0.5f)
				.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
			_pulseTween.TweenProperty(this, "PulseValue", 0f, 0.5f)
				.SetEase(Tween.EaseType.InOut).SetTrans(Tween.TransitionType.Sine);
		}
		else
		{
			if (_pulseTween == null || !_pulseTween.IsValid())
				return;

			_pulseTween.Kill();
			_pulseTween = null;
			PulseValue = 0f;
			LimitPulseFactor = 1f;
			if (_limitLabel != null)
				_limitLabel.Scale = Vector2.One;
			QueueRedraw();
		}
	}

	/// <summary>Stosuje pulsację do koloru i skali — wołane z UpdateProgress().</summary>
	private void ApplyPulse()
	{
		if (PulseValue < 0.001f && LimitPulseFactor >= 0.999f)
		{
			// Brak aktywnej pulsacji — upewnij się że skala = 1
			if (_limitLabel != null && _limitLabel.Scale != Vector2.One)
				_limitLabel.Scale = Vector2.One;
			return;
		}

		// Mapa 0..1 → jasność 1.0..0.6
		LimitPulseFactor = 1f - PulseValue * 0.4f;

		// Mapa 0..1 → skala 1.0..1.12
		if (_limitLabel != null)
		{
			float s = 1f + PulseValue * 0.12f;
			_limitLabel.Scale = new Vector2(s, s);
		}

		QueueRedraw();
	}

	// ── Timer sounds ────────────────────────────────────────────────────────

	/// <summary>
	/// Zarządza ciągłymi dźwiękami stopera (ticking).
	/// Dźwięk leci TYLKO gdy <see cref="TimerFlowing"/> jest true
	/// (czyli czas faktycznie odlicza — nie na pauzie, nie przed startem).
	/// </summary>
	private void UpdateTimerSound()
	{
		var audio = AudioManager.Instance;
		if (audio?.Sfx == null) return;

		if (!TimerFlowing)
		{
			// Czas nie leci — zatrzymaj tykanie (pauza, menu, przed startem)
			audio.StopLoopingSFX();
			return;
		}

		// Reset flagi przy powrocie do betu
		if (ActiveTimer == ActiveTimerEnum.Bet)
			_limitEndPlayedAtHalf = false;

		// Zmiana trybu → przełącz dźwięk ciągły przez AudioManager
		if (ActiveTimer == ActiveTimerEnum.Bet)
			audio.StartLoopingSFX(audio.Sfx.TimerBetTick, 1f);
		else // Limit
			audio.StartLoopingSFX(audio.Sfx.TimerLimitWarning, 2f);

		// Gdy limit spadnie poniżej połowy → dodatkowo one-shot TimerLimitEnd
		if (ActiveTimer == ActiveTimerEnum.Limit
			&& _lastLimitRatioForSound >= 0.5f && LimitRatio < 0.5f
			&& !_limitEndPlayedAtHalf)
		{
			audio.PlayTimerLimitEnd();
			_limitEndPlayedAtHalf = true;
		}

		_lastActiveTimerForSound = ActiveTimer;
		_lastLimitRatioForSound = LimitRatio;
	}

	// ── Layout ──────────────────────────────────────────────────────────────

	private void UpdateLabelLayout()
	{
		float halfH = Size.Y * 0.5f;
		float halfW = Size.X * 0.5f;
		float labelHeight = _activeFontSize * 1.2f;

		if (_betLabel != null)
		{
			_betLabel.Size       = new Vector2(Size.X, labelHeight);
			_betLabel.Position   = new Vector2(0, halfH - labelHeight - 4f);
			_betLabel.PivotOffset = new Vector2(halfW, labelHeight * 0.5f);
		}
		if (_limitLabel != null)
		{
			_limitLabel.Size       = new Vector2(Size.X, labelHeight);
			_limitLabel.Position   = new Vector2(0, halfH + 4f);
			_limitLabel.PivotOffset = new Vector2(halfW, labelHeight * 0.5f);
		}
		if (_multiplierLabel != null)
		{
			_multiplierLabel.Size       = new Vector2(Size.X, 16f);
			_multiplierLabel.Position   = new Vector2(0, halfH + labelHeight + 8f);
			_multiplierLabel.PivotOffset = new Vector2(halfW, 8f);
		}
	}

	// ── Animacja ────────────────────────────────────────────────────────────

	private void AnimateActiveSwitch()
	{
		if (_tween != null && _tween.IsValid())
			_tween.Kill();

		// Zatrzymaj pulsację — przełącznik dostaje priorytet
		if (_pulseTween != null && _pulseTween.IsValid())
		{
			_pulseTween.Kill();
			_pulseTween = null;
			PulseValue = 0f;
			LimitPulseFactor = 1f;
		}

		_tween = CreateTween();
		_tween.SetParallel(true);

		bool betActive  = ActiveTimer == ActiveTimerEnum.Bet;
		float betScale  = betActive  ? 1.3f : 0.75f;
		float limitScale = betActive ? 0.75f : 1.3f;

		// Zmień też font size dla płynniejszego efektu
		if (_betLabel != null)
		{
			int targetBetSize = betActive ? _activeFontSize : _inactiveFontSize;
			_betLabel.AddThemeFontSizeOverride("font_size", targetBetSize);
		}
		if (_limitLabel != null)
		{
			int targetLimitSize = betActive ? _inactiveFontSize : _activeFontSize;
			_limitLabel.AddThemeFontSizeOverride("font_size", targetLimitSize);
		}

		_tween.TweenProperty(_betLabel, "scale",
				new Vector2(betScale, betScale), 0.35f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
		_tween.TweenProperty(_limitLabel, "scale",
				new Vector2(limitScale, limitScale), 0.35f)
			.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);

		if (_multiplierLabel != null)
		{
			if (!betActive)
			{
				// Wpada z "pop" — skala od 0 do 1
				_multiplierLabel.Scale = Vector2.Zero;
				_tween.TweenProperty(_multiplierLabel, "scale",
						Vector2.One, 0.35f)
					.SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
			}
			else
			{
				// Znika
				_tween.TweenProperty(_multiplierLabel, "scale",
						Vector2.Zero, 0.2f)
					.SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
			}
		}
	}
}
