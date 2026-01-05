using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using SelfDrivingCar.TomTom;

namespace SelfDrivingCar.Application;

public sealed class RouteView : Control
{
	private static readonly Typeface LabelTypeface = Typeface.Default;
	private static readonly Pen MarkerBorderPen = new(Brushes.Black, 1.2);
	private const double Padding = 24.0;

	private readonly IReadOnlyList<Road> _roads;
	private readonly double _minLat;
	private readonly double _maxLat;
	private readonly double _minLon;
	private readonly double _maxLon;
	private readonly double _latSpan;
	private readonly double _lonSpan;
	private readonly bool _hasRoute;

	// Car position tracking
	private Coordinate? _carPosition;
	private int _currentRoadIndex = -1;
	private double _carBearing = 0;
	private double _remainingDistance = 0;
	private double _remainingTime = 0;

	public RouteView(IReadOnlyList<Road> roads)
	{
		_roads = roads;
		_hasRoute = roads.Count > 0;

		if (!_hasRoute)
		{
			_minLat = _maxLat = _minLon = _maxLon = 0;
			_latSpan = _lonSpan = 1;
			return;
		}

		var points = roads.SelectMany(r => new[] { r.From, r.To }).ToList();

		_minLat = points.Min(p => p.Latitude);
		_maxLat = points.Max(p => p.Latitude);
		_minLon = points.Min(p => p.Longitude);
		_maxLon = points.Max(p => p.Longitude);

		var rawLatSpan = Math.Max(1e-6, _maxLat - _minLat);
		var rawLonSpan = Math.Max(1e-6, _maxLon - _minLon);

		// Provide a small visual buffer around the path.
		_minLat -= rawLatSpan * 0.05;
		_maxLat += rawLatSpan * 0.05;
		_minLon -= rawLonSpan * 0.05;
		_maxLon += rawLonSpan * 0.05;

		_latSpan = Math.Max(1e-6, _maxLat - _minLat);
		_lonSpan = Math.Max(1e-6, _maxLon - _minLon);
	}

	public void UpdateCarPosition(Coordinate position, int roadIndex, double bearing, double remainingDistance, double remainingTime)
	{
		_carPosition = position;
		_currentRoadIndex = roadIndex;
		_carBearing = bearing;
		_remainingDistance = remainingDistance;
		_remainingTime = remainingTime;
		InvalidateVisual();
	}

	public void ClearCarPosition()
	{
		_carPosition = null;
		_currentRoadIndex = -1;
		InvalidateVisual();
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		var rect = Bounds;
		context.FillRectangle(Brushes.Black, rect);

		if (!_hasRoute || rect.Width <= 0 || rect.Height <= 0)
		{
			DrawStatusMessage(context, rect, "No route data available");
			return;
		}

		DrawRouteSegments(context, rect);

		// Draw the car if we have a position
		if (_carPosition != null)
		{
			DrawCar(context, rect, _carPosition, _carBearing);
			DrawStats(context, rect);
		}
	}

	private void DrawStats(DrawingContext context, Rect rect)
	{
		var statsText = $"Distance Remaining: {_remainingDistance:F2} km\nTime Remaining: {_remainingTime:F1} min";
		var statsLayout = new TextLayout(statsText, LabelTypeface, 16, Brushes.White, TextAlignment.Left, TextWrapping.NoWrap);

		// Draw stats text only, no background or border, and more top-left
		statsLayout.Draw(context, new Point(12, 8));
	}

	private void DrawRouteSegments(DrawingContext context, Rect bounds)
	{
		var palette = new[]
		{
			Color.Parse("#50BFFF"),
			Color.Parse("#7CFC92"),
			Color.Parse("#FFB74D"),
			Color.Parse("#FF6F61"),
			Color.Parse("#CE93D8"),
			Color.Parse("#80CBC4")
		};

		for (int index = 0; index < _roads.Count; index++)
		{
			var road = _roads[index];
			var from = Project(road.From, bounds);
			var to = Project(road.To, bounds);

			var color = palette[index % palette.Length];
			IBrush segmentBrush = new SolidColorBrush(color);
			var segmentPen = new Pen(segmentBrush, 3);

			context.DrawLine(segmentPen, from, to);

			var fromLabel = index == 0 ? "S" : "B";
			var toLabel = index == _roads.Count - 1 ? "E" : "F";

			IBrush fromBrush = index == 0 ? Brushes.LimeGreen : segmentBrush;
			IBrush toBrush = index == _roads.Count - 1 ? Brushes.OrangeRed : segmentBrush;

			DrawMarker(context, from, fromBrush, fromLabel, road.From);
			DrawMarker(context, to, toBrush, toLabel, road.To);
		}
	}

	private void DrawMarker(DrawingContext context, Point position, IBrush fill, string label, Coordinate coordinate)
	{
		context.DrawEllipse(fill, MarkerBorderPen, position, 6, 6);

		var labelText = new TextLayout(label, LabelTypeface, 12, Brushes.White, TextAlignment.Left, TextWrapping.NoWrap);
		var labelPosition = position + new Vector(8, -6);
		labelText.Draw(context, labelPosition);

		var coordText = $"({coordinate.Latitude:F2}, {coordinate.Longitude:F2})";
		var coordLayout = new TextLayout(coordText, LabelTypeface, 10, Brushes.White, TextAlignment.Left, TextWrapping.NoWrap);
		var coordPosition = position + new Vector(8, 6);
		coordLayout.Draw(context, coordPosition);
	}

	private void DrawStatusMessage(DrawingContext context, Rect rect, string message)
	{
		var formattedText = new FormattedText(
			message,
			System.Globalization.CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			LabelTypeface,
			14,
			Brushes.White);

		var position = new Point(
			rect.X + (rect.Width - formattedText.Width) / 2,
			rect.Y + (rect.Height - formattedText.Height) / 2);

		context.DrawText(formattedText, position);
	}

	private void DrawCar(DrawingContext context, Rect rect, Coordinate position, double bearing)
	{
		var screenPos = Project(position, rect);

		// Bearing 0° = North (up), we want the car to point in that direction
		// Car is drawn facing right by default, so subtract 90° to make it face up at 0°
		double rotationAngle = bearing - 90.0;

		// Save the current state and apply transformations
		using (context.PushTransform(Matrix.CreateTranslation(screenPos.X, screenPos.Y)))
		using (context.PushTransform(Matrix.CreateRotation(DegreesToRadians(rotationAngle))))
		{
			// Draw an emoji-style car facing right (east/90°)

			// Main car body (rounded rectangle using geometry)
			var bodyGeometry = new StreamGeometry();
			using (var ctx = bodyGeometry.Open())
			{
				// Car body outline with rounded edges
				ctx.BeginFigure(new Point(-14, 4), true);
				ctx.LineTo(new Point(14, 4));
				ctx.ArcTo(new Point(14, -4), new Size(4, 4), 0, false, SweepDirection.Clockwise);
				ctx.LineTo(new Point(10, -4));
				ctx.LineTo(new Point(8, -8));
				ctx.LineTo(new Point(2, -8));
				ctx.LineTo(new Point(-4, -8));
				ctx.LineTo(new Point(-8, -4));
				ctx.LineTo(new Point(-14, -4));
				ctx.ArcTo(new Point(-14, 4), new Size(4, 4), 0, false, SweepDirection.Clockwise);
			}
			context.DrawGeometry(Brushes.Red, new Pen(Brushes.DarkRed, 1.5), bodyGeometry);

			// Windows (rounded)
			var frontWindow = new EllipseGeometry(new Rect(4, -7, 6, 5));
			context.DrawGeometry(Brushes.LightBlue, new Pen(Brushes.Blue, 1), frontWindow);

			var rearWindow = new EllipseGeometry(new Rect(-6, -7, 6, 5));
			context.DrawGeometry(Brushes.LightBlue, new Pen(Brushes.Blue, 1), rearWindow);

			// Wheels (circles)
			context.DrawEllipse(Brushes.Black, new Pen(Brushes.DarkGray, 1.5), new Point(-8, 4), 3.5, 3.5); // Rear wheel
			context.DrawEllipse(Brushes.Black, new Pen(Brushes.DarkGray, 1.5), new Point(8, 4), 3.5, 3.5);  // Front wheel

			// Headlight
			context.DrawEllipse(Brushes.Yellow, null, new Point(14, 0), 1.5, 1.5);
		}
	}

	private double DegreesToRadians(double degrees)
	{
		return degrees * Math.PI / 180.0;
	}

	private Point Project(Coordinate coordinate, Rect rect)
	{
		double usableWidth = Math.Max(1, rect.Width - Padding * 2);
		double usableHeight = Math.Max(1, rect.Height - Padding * 2);

		double normalizedX = (_lonSpan <= double.Epsilon) ? 0.5 : (coordinate.Longitude - _minLon) / _lonSpan;
		double normalizedY = (_latSpan <= double.Epsilon) ? 0.5 : (_maxLat - coordinate.Latitude) / _latSpan;

		// Clamp normalized values to [0, 1] in case coordinate is slightly outside bounds
		normalizedX = Math.Max(0, Math.Min(1, normalizedX));
		normalizedY = Math.Max(0, Math.Min(1, normalizedY));

		double x = rect.X + Padding + normalizedX * usableWidth;
		double y = rect.Y + Padding + normalizedY * usableHeight;

		return new Point(x, y);
	}
}
