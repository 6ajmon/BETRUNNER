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
	[Export] private Color _betColor       = new Color(0.2f, 0.9f, 0.2f);
	[Export] private Color _limitColor     = new Color(0.9f, 0.2f, 0.2f);
	[Export] private Color _bgColor        = new Color(0.0f, 0.0f, 0.0f);
	[Export] private Color _borderColor    = new Color(0.5f, 0.5f, 0.5f);
	[Export] private float _outerBorderWidth = 3f;
	[Export] private float _ringBorderWidth  = 2f;
	[Export] private float _outerRingWidth = 14f;
	[Export] private float _innerRingWidth = 8f;
	[Export] private float _gap = 4f;
	[Export] private int   _arcSegments = 120;

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
	private ActiveTimerEnum _lastActiveTimer = ActiveTimerEnum.Bet;

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

		// 1. Tło — czarne wypełnione koło
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
			// Wypełnienie
			DrawArc(center, innerRadius, from, to,
				_arcSegments, _limitColor, _innerRingWidth, true);
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
