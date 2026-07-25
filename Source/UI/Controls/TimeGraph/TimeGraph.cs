using Godot;
using System;
using System.Collections.Generic;
using NewGameProject;

/// <summary>
/// A custom Control that draws a colored segmented graph of remaining time (y)
/// over game time (x). Supports left-to-right draw animation via Tween.
/// Each segment is drawn in its own color (Bonus=amber, Bet=green, Penalty=red, Limit=orange).
/// </summary>
public partial class TimeGraph : Control
{
	// ── Data ────────────────────────────────────────────────────────────────
	private CountdownManager.GraphSegment[] _segments = Array.Empty<CountdownManager.GraphSegment>();
	private (float X, string Label)[] _levelMarkers = Array.Empty<(float, string)>();

	/// <summary>How much of the graph is drawn (0..1), animated via Tween.</summary>
	private float _drawProgress = 1f;

	// ── Exported appearance ─────────────────────────────────────────────────
	[Export] private Color _gridColor        = new Color(0.3f, 0.3f, 0.3f, 0.3f);
	[Export] private Color _textColor        = new Color(0.8f, 0.8f, 0.8f);
	[Export] private Color _bgFillColor      = new Color(1f, 0.43137255f, 0.2509804f, 0.05f);
	[Export] private Color _levelMarkerColor = new Color(1f, 0.84313726f, 0.2509804f);
	[Export] private float _lineWidth        = 2.5f;
	[Export] private int   _yTicks           = 5;
	[Export] private float _padding          = 8f;
	[Export] private float _animDuration     = 3.0f;

	// ── Internal state ──────────────────────────────────────────────────────
	private float _dataMinX, _dataMaxX, _dataMinY, _dataMaxY;
	private float _plotLeft, _plotRight, _plotTop, _plotBottom;
	private Tween _tween;

	// ── Public API ──────────────────────────────────────────────────────────

	/// <summary>
	/// Set the graph data (colored segments + level markers) and start animation.
	/// </summary>
	public void SetData(CountdownManager.GraphSegment[] segments,
	                    (float X, string Label)[] levelMarkers)
	{
		_segments = segments ?? Array.Empty<CountdownManager.GraphSegment>();
		_levelMarkers = levelMarkers ?? Array.Empty<(float, string)>();

		ComputeBounds();
		StartAnimation();
	}

	/// <summary>
	/// Start the left-to-right reveal animation (slower, linear).
	/// </summary>
	private void StartAnimation()
	{
		_drawProgress = 0f;
		QueueRedraw();

		_tween?.Kill();
		_tween = CreateTween();
		_tween.TweenProperty(this, "draw_progress", 1f, _animDuration)
			  .SetEase(Tween.EaseType.InOut)
			  .SetTrans(Tween.TransitionType.Linear);
		_tween.Finished += QueueRedraw;
	}

	/// <summary>Property used by Tween.</summary>
	private float draw_progress
	{
		get => _drawProgress;
		set
		{
			_drawProgress = Math.Clamp(value, 0f, 1f);
			QueueRedraw();
		}
	}

	// ── Bounds computation ──────────────────────────────────────────────────

	private void ComputeBounds()
	{
		if (_segments.Length == 0)
		{
			_dataMinX = _dataMaxX = _dataMinY = _dataMaxY = 0f;
			return;
		}

		_dataMinX = _dataMaxX = _segments[0].Start.X;
		_dataMinY = _dataMaxY = _segments[0].Start.Y;

		foreach (var seg in _segments)
		{
			foreach (var v in new[] { seg.Start, seg.End })
			{
				if (v.X < _dataMinX) _dataMinX = v.X;
				if (v.X > _dataMaxX) _dataMaxX = v.X;
				if (v.Y < _dataMinY) _dataMinY = v.Y;
				if (v.Y > _dataMaxY) _dataMaxY = v.Y;
			}
		}

		float yRange = Math.Max(_dataMaxY - _dataMinY, 1f);
		_dataMaxY += yRange * 0.1f;
		_dataMinY = Math.Max(0f, _dataMinY - yRange * 0.05f);
	}

	// ── Drawing ─────────────────────────────────────────────────────────────

	public override void _Draw()
	{
		if (_segments.Length == 0) return;

		float w = Size.X;
		float h = Size.Y;
		if (w <= 0 || h <= 0) return;

		_plotLeft   = _padding + 20f; // space for y-axis labels
		_plotRight  = w - _padding;
		_plotTop    = _padding;
		_plotBottom = h - _padding * 2f - 16f;

		// ── Grid / axes ─────────────────────────────────────────────────
		float yRange = _dataMaxY - _dataMinY;
		for (int i = 0; i <= _yTicks; i++)
		{
			float t = (float)i / _yTicks;
			float dataY = _dataMaxY - t * yRange;
			float py = MapY(dataY);
			DrawLine(new Vector2(_plotLeft, py), new Vector2(_plotRight, py), _gridColor, 1f);
			DrawString(ThemeDB.FallbackFont, new Vector2(2, py + 4),
			           dataY.ToString("F0"), HorizontalAlignment.Left, -1, 12, _textColor);
		}

		// ── Level markers ──────────────────────────────────────────────
		foreach (var (x, label) in _levelMarkers)
		{
			float px = MapX(x);
			if (px < _plotLeft || px > _plotRight) continue;
			DrawLine(new Vector2(px, _plotTop), new Vector2(px, _plotBottom),
			         _levelMarkerColor * new Color(1, 1, 1, 0.3f), 1f, true);
			DrawString(ThemeDB.FallbackFont, new Vector2(px - 8, _plotBottom + 14),
			           label, HorizontalAlignment.Left, -1, 14, _levelMarkerColor);
		}

		// ── Draw each segment up to _drawProgress ─────────────────────
		float totalDist = ComputeTotalDistance();
		float drawnDist = totalDist * _drawProgress;

		DrawFilledArea(drawnDist);
		DrawSegments(drawnDist);
	}

	// ── Coordinate mapping ──────────────────────────────────────────────────

	private float MapX(float dataX)
	{
		float range = _dataMaxX - _dataMinX;
		if (range <= 0f) return _plotLeft;
		return _plotLeft + (dataX - _dataMinX) / range * (_plotRight - _plotLeft);
	}

	private float MapY(float dataY)
	{
		float range = _dataMaxY - _dataMinY;
		if (range <= 0f) return _plotTop;
		return _plotBottom - (dataY - _dataMinY) / range * (_plotBottom - _plotTop);
	}

	private Vector2 ToScreen(Vector2 dataPt)
	{
		return new Vector2(MapX(dataPt.X), MapY(dataPt.Y));
	}

	// ── Segment drawing ─────────────────────────────────────────────────────

	private float ComputeTotalDistance()
	{
		float dist = 0f;
		foreach (var seg in _segments)
			dist += (seg.End - seg.Start).Length();
		return dist;
	}

	/// <summary>
	/// Walk segments in order and find the point at the given distance.
	/// Returns (segIndex, t) where t is 0..1 within that segment.
	/// </summary>
	private (int SegIndex, float T) WalkToDistance(float maxDist)
	{
		float accumulated = 0f;
		for (int i = 0; i < _segments.Length; i++)
		{
			float segLen = (_segments[i].End - _segments[i].Start).Length();
			if (accumulated + segLen > maxDist)
			{
				float remaining = maxDist - accumulated;
				float t = segLen > 0f ? remaining / segLen : 0f;
				return (i, t);
			}
			accumulated += segLen;
		}
		return (_segments.Length - 1, 1f);
	}

	/// <summary>
	/// Draw each fully-visible segment with its own color, plus a partial last segment.
	/// </summary>
	private void DrawSegments(float maxDist)
	{
		if (_segments.Length == 0) return;

		var (segIdx, t) = WalkToDistance(maxDist);

		// Draw full segments up to segIdx-1
		for (int i = 0; i < segIdx; i++)
		{
			var seg = _segments[i];
			DrawLine(ToScreen(seg.Start), ToScreen(seg.End), seg.Color, _lineWidth, true);
		}

		// Draw partial segment at segIdx
		if (segIdx < _segments.Length && t > 0f)
		{
			var seg = _segments[segIdx];
			Vector2 endPt = seg.Start.Lerp(seg.End, t);
			DrawLine(ToScreen(seg.Start), ToScreen(endPt), seg.Color, _lineWidth, true);
		}
	}

	/// <summary>
	/// Draw a filled polygon under all visible segments.
	/// </summary>
	private void DrawFilledArea(float maxDist)
	{
		if (_segments.Length == 0 || _drawProgress <= 0f) return;

		var (segIdx, t) = WalkToDistance(maxDist);
		var fillPoints = new List<Vector2>();

		// Start at bottom-left below first segment's start
		fillPoints.Add(ToScreen(new Vector2(_segments[0].Start.X, _dataMinY)));

		// Walk full segments + partial
		for (int i = 0; i <= segIdx && i < _segments.Length; i++)
		{
			if (i < segIdx)
			{
				fillPoints.Add(ToScreen(_segments[i].Start));
				fillPoints.Add(ToScreen(_segments[i].End));
			}
			else
			{
				fillPoints.Add(ToScreen(_segments[i].Start));
				Vector2 partialEnd = _segments[i].Start.Lerp(_segments[i].End, t);
				fillPoints.Add(ToScreen(partialEnd));
				fillPoints.Add(ToScreen(new Vector2(partialEnd.X, _dataMinY)));
			}
		}

		if (fillPoints.Count >= 3)
			DrawColoredPolygon(fillPoints.ToArray(), _bgFillColor);
	}
}
