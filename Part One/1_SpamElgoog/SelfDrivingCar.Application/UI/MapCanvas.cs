using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using SelfDrivingCar.World;

namespace SelfDrivingCar.Application.UI;

public class MapCanvas : Control
{
	private Map? _map;
	private readonly Dictionary<string, Point> _nodePositions = new();
	private const double NodeRadius = 6;
	private const double Padding = 50;

	// Cached bounds for coordinate transformation
	private double _cachedMinLon;
	private double _cachedMaxLon;
	private double _cachedMinLat;
	private double _cachedMaxLat;
	private double _cachedLonRange;
	private double _cachedLatRange;
	private double _cachedAvailableWidth;
	private double _cachedAvailableHeight;

	// Selection tracking
	private Node? _selectedStartNode;
	private Node? _selectedEndNode;
	private List<Node>? _plannedRoute; // The ground truth route from WorldNavigate

	// Car visualization
	private Coordinate? _carPosition;
	private double _carBearing; // in degrees
	private bool _showCar;

	// Explosion animation
	private Coordinate? _explosionPosition;
	private DateTime _explosionStartTime = DateTime.MinValue;
	private const double EXPLOSION_DURATION_SECONDS = 1.5;
	private DispatcherTimer? _explosionAnimationTimer;

	// Events
	public event EventHandler<NodeSelectionChangedEventArgs>? SelectionChanged;

	public Node? SelectedStartNode => _selectedStartNode;
	public Node? SelectedEndNode => _selectedEndNode;

	public void SetMap(Map map)
	{
		_map = map;
		CalculateNodePositions();
		InvalidateVisual();
	}

	public void SetPlannedRoute(List<Node>? route)
	{
		_plannedRoute = route;
		InvalidateVisual();
	}

	public void UpdateCarPosition(Coordinate position, double bearing)
	{
		_carPosition = position;
		_carBearing = bearing;
	}

	public void ShowCar(bool show)
	{
		_showCar = show;
		InvalidateVisual();
	}

	public void TriggerExplosion(Coordinate position)
	{
		_explosionPosition = position;
		_explosionStartTime = DateTime.Now;

		// Start animation timer to update the display
		if (_explosionAnimationTimer == null)
		{
			_explosionAnimationTimer = new DispatcherTimer();
			_explosionAnimationTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
			_explosionAnimationTimer.Tick += (s, e) =>
			{
				if (IsExplosionAnimationPlaying())
				{
					InvalidateVisual();
				}
				else
				{
					_explosionAnimationTimer.Stop();
					_explosionPosition = null;
					_explosionStartTime = DateTime.MinValue;
				}
			};
		}

		_explosionAnimationTimer.Start();
		InvalidateVisual();
	}

	public bool IsExplosionAnimationPlaying()
	{
		if (_explosionStartTime == DateTime.MinValue) return false;
		double elapsed = (DateTime.Now - _explosionStartTime).TotalSeconds;
		return elapsed < EXPLOSION_DURATION_SECONDS;
	}

	protected override Size MeasureOverride(Size availableSize)
	{
		// Take all available space
		return availableSize;
	}

	protected override Size ArrangeOverride(Size finalSize)
	{
		CalculateNodePositions();
		return finalSize;
	}

	private void CalculateNodePositions()
	{
		if (_map == null || _map.Nodes.Count == 0) return;

		_nodePositions.Clear();

		// Find bounds of the map
		double minLon = double.MaxValue;
		double maxLon = double.MinValue;
		double minLat = double.MaxValue;
		double maxLat = double.MinValue;

		foreach (var node in _map.Nodes)
		{
			minLon = Math.Min(minLon, node.Coordinate.Longitude);
			maxLon = Math.Max(maxLon, node.Coordinate.Longitude);
			minLat = Math.Min(minLat, node.Coordinate.Latitude);
			maxLat = Math.Max(maxLat, node.Coordinate.Latitude);
		}

		// Cache bounds for use in car positioning
		_cachedMinLon = minLon;
		_cachedMaxLon = maxLon;
		_cachedMinLat = minLat;
		_cachedMaxLat = maxLat;

		// Calculate scale factors
		double lonRange = maxLon - minLon;
		double latRange = maxLat - minLat;

		if (lonRange == 0) lonRange = 1;
		if (latRange == 0) latRange = 1;

		_cachedLonRange = lonRange;
		_cachedLatRange = latRange;

		// Cache available dimensions for consistent coordinate transformation across all drawing operations
		_cachedAvailableWidth = Bounds.Width - (2 * Padding);
		_cachedAvailableHeight = Bounds.Height - (2 * Padding);

		// Convert lat/lon to screen coordinates
		foreach (var node in _map.Nodes)
		{
			double x = Padding + ((node.Coordinate.Longitude - minLon) / lonRange) * _cachedAvailableWidth;
			// Invert Y axis (lat increases northward, but screen Y increases downward)
			double y = Padding + ((maxLat - node.Coordinate.Latitude) / latRange) * _cachedAvailableHeight;
			_nodePositions[node.Id] = new Point(x, y);
		}
	}

	public override void Render(DrawingContext context)
	{
		base.Render(context);

		if (_map == null) return;

		// Draw background grid
		DrawGrid(context);

		// Draw roads first (so they appear behind nodes)
		DrawRoads(context);

		// Draw planned route (if any)
		if (_plannedRoute != null && _plannedRoute.Count > 1)
		{
			DrawPlannedRoute(context);
		}

		// Draw nodes on top
		DrawNodes(context);

		// Draw car if it should be shown
		if (_showCar && _carPosition != null)
		{
			DrawCar(context);
		}

		// Draw explosion animation if playing
		if (IsExplosionAnimationPlaying() && _explosionPosition != null)
		{
			DrawExplosion(context);
		}
	}

	protected override void OnPointerPressed(PointerPressedEventArgs e)
	{
		base.OnPointerPressed(e);

		var point = e.GetPosition(this);
		var node = GetNodeAtPosition(point);

		if (node != null)
		{
			if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
			{
				// Left click = set start node
				_selectedStartNode = node;
			}
			else if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
			{
				// Right click = set end node
				_selectedEndNode = node;
			}

			// Notify listeners of selection change
			SelectionChanged?.Invoke(this, new NodeSelectionChangedEventArgs(_selectedStartNode, _selectedEndNode));

			// Redraw to show selected nodes
			InvalidateVisual();
		}
	}

	private Node? GetNodeAtPosition(Point point)
	{
		if (_map == null) return null;

		const double clickTolerance = 12; // pixels

		foreach (var node in _map.Nodes)
		{
			if (_nodePositions.TryGetValue(node.Id, out var nodePos))
			{
				double distance = Math.Sqrt(
					Math.Pow(point.X - nodePos.X, 2) +
					Math.Pow(point.Y - nodePos.Y, 2)
				);

				if (distance <= clickTolerance)
				{
					return node;
				}
			}
		}

		return null;
	}

	private void DrawGrid(DrawingContext context)
	{
		var gridPen = new Pen(new SolidColorBrush(Color.FromRgb(230, 230, 230)), 1);
		double gridSpacing = 50;

		for (double x = 0; x < Bounds.Width; x += gridSpacing)
		{
			context.DrawLine(gridPen, new Point(x, 0), new Point(x, Bounds.Height));
		}

		for (double y = 0; y < Bounds.Height; y += gridSpacing)
		{
			context.DrawLine(gridPen, new Point(0, y), new Point(Bounds.Width, y));
		}
	}

	private void DrawRoads(DrawingContext context)
	{
		if (_map == null) return;

		var drawnConnections = new HashSet<string>();

		foreach (var node in _map.Nodes)
		{
			var connections = _map.GetConnections(node);
			foreach (var (destination, distance, speedLimit) in connections)
			{
				// Create a unique key for this connection (bidirectional)
				string connectionKey = string.Compare(node.Id, destination.Id, StringComparison.Ordinal) < 0
					? $"{node.Id}-{destination.Id}"
					: $"{destination.Id}-{node.Id}";

				// Skip if we've already drawn this road
				if (drawnConnections.Contains(connectionKey))
					continue;

				drawnConnections.Add(connectionKey);

				if (_nodePositions.TryGetValue(node.Id, out var startPos) &&
					_nodePositions.TryGetValue(destination.Id, out var endPos))
				{
					// Draw realistic road with black asphalt and white center line

					// Layer 1: Road shadow/edge (darkest, widest)
					var shadowPen = new Pen(new SolidColorBrush(Color.FromRgb(20, 20, 20)), 10);
					context.DrawLine(shadowPen, startPos, endPos);

					// Layer 2: Road asphalt (black)
					var roadPen = new Pen(new SolidColorBrush(Color.FromRgb(40, 40, 40)), 6);
					context.DrawLine(roadPen, startPos, endPos);

					// Layer 3: Road edge/border (slight highlight on one side)
					var edgePen = new Pen(new SolidColorBrush(Color.FromRgb(80, 80, 80)), 1);
					// Draw edge offset slightly
					double dx = endPos.X - startPos.X;
					double dy = endPos.Y - startPos.Y;
					double length = Math.Sqrt(dx * dx + dy * dy);
					if (length > 0)
					{
						double perpX = -dy / length * 2;
						double perpY = dx / length * 2;
						var edgeStart = new Point(startPos.X + perpX, startPos.Y + perpY);
						var edgeEnd = new Point(endPos.X + perpX, endPos.Y + perpY);
						context.DrawLine(edgePen, edgeStart, edgeEnd);
					}

					// Layer 4: Center line (white dashed)
					var centerPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 255, 255)), 2.5);
					DrawDashedLine(context, centerPen, startPos, endPos, 8);

					// Calculate midpoint and perpendicular offset for sign
					var midPoint = new Point(
						(startPos.X + endPos.X) / 2,
						(startPos.Y + endPos.Y) / 2
					);

					// Calculate road direction and perpendicular vector
					double roadDx = endPos.X - startPos.X;
					double roadDy = endPos.Y - startPos.Y;
					double roadLength = Math.Sqrt(roadDx * roadDx + roadDy * roadDy);

					if (roadLength > 0)
					{
						// Normalize direction vector
						double dirX = roadDx / roadLength;
						double dirY = roadDy / roadLength;

						// Perpendicular vector (rotated 90 degrees)
						double perpX = -dirY;
						double perpY = dirX;

						// Offset distance for the sign (perpendicular to road)
						double offsetDistance = 20;

						// Position sign perpendicular to road
						var signPosition = new Point(
							midPoint.X + perpX * offsetDistance,
							midPoint.Y + perpY * offsetDistance
						);

						DrawSpeedSignLabel(context, midPoint, signPosition, speedLimit);
					}
				}
			}
		}
	}

	private void DrawPlannedRoute(DrawingContext context)
	{
		if (_plannedRoute == null || _plannedRoute.Count < 2) return;

		// Draw the planned route as highlighted path
		var routePen = new Pen(new SolidColorBrush(Color.FromRgb(255, 193, 7)), 4); // Golden/yellow
		var routeOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(200, 150, 0)), 6); // Darker golden

		for (int i = 0; i < _plannedRoute.Count - 1; i++)
		{
			var current = _plannedRoute[i];
			var next = _plannedRoute[i + 1];

			if (_nodePositions.TryGetValue(current.Id, out var startPos) &&
				_nodePositions.TryGetValue(next.Id, out var endPos))
			{
				// Draw outline first
				context.DrawLine(routeOutlinePen, startPos, endPos);

				// Draw route line on top
				context.DrawLine(routePen, startPos, endPos);

				// Draw arrow markers to show direction
				DrawRouteArrow(context, startPos, endPos);
			}
		}
	}

	private void DrawRouteArrow(DrawingContext context, Point from, Point to)
	{
		double dx = to.X - from.X;
		double dy = to.Y - from.Y;
		double distance = Math.Sqrt(dx * dx + dy * dy);

		if (distance < 10) return; // Too short for arrow

		// Calculate arrow position at midpoint
		double mid = distance / 2;
		double arrowX = from.X + (dx / distance) * mid;
		double arrowY = from.Y + (dy / distance) * mid;

		// Arrow direction
		double arrowAngle = Math.Atan2(dy, dx);
		double arrowSize = 8;
		double arrowAngleOffset = Math.PI / 6; // 30 degrees

		// Arrow point
		var arrowPoint = new Point(arrowX, arrowY);

		// Arrow tail points
		var tailLeft = new Point(
			arrowX - arrowSize * Math.Cos(arrowAngle - arrowAngleOffset),
			arrowY - arrowSize * Math.Sin(arrowAngle - arrowAngleOffset)
		);

		var tailRight = new Point(
			arrowX - arrowSize * Math.Cos(arrowAngle + arrowAngleOffset),
			arrowY - arrowSize * Math.Sin(arrowAngle + arrowAngleOffset)
		);

		// Draw arrow as lines
		var arrowPen = new Pen(new SolidColorBrush(Color.FromRgb(255, 193, 7)), 2);
		context.DrawLine(arrowPen, arrowPoint, tailLeft);
		context.DrawLine(arrowPen, arrowPoint, tailRight);
	}

	private void DrawCar(DrawingContext context)
	{
		if (_carPosition == null) return;

		// Use cached dimensions that were calculated in CalculateNodePositions for consistent coordinate transformation
		// This ensures the car position matches the road positions, especially when window is resized or moved to different monitor
		double screenX = Padding + ((_carPosition.Longitude - _cachedMinLon) / _cachedLonRange) * _cachedAvailableWidth;
		double screenY = Padding + ((_cachedMaxLat - _carPosition.Latitude) / _cachedLatRange) * _cachedAvailableHeight;
		var carScreenPos = new Point(screenX, screenY);

		// Draw realistic top-down car shape
		double carLength = 12;  // Overall car length
		double carWidth = 6;    // Overall car width (narrower to fit on roads)

		// Convert bearing to radians (bearing is in degrees, 0 = East, increases counter-clockwise in math, but typically 0=North)
		// For screen coords: 0 degrees = right, 90 = down, 180 = left, 270 = up
		double radians = (_carBearing - 90) * Math.PI / 180.0;
		double cosRad = Math.Cos(radians);
		double sinRad = Math.Sin(radians);

		// Calculate car body corners (top-down view)
		// Front (nose) of car
		var carFront = new Point(
			carScreenPos.X + (carLength / 2) * cosRad,
			carScreenPos.Y + (carLength / 2) * sinRad
		);

		// Rear of car
		var carRear = new Point(
			carScreenPos.X - (carLength / 2) * cosRad,
			carScreenPos.Y - (carLength / 2) * sinRad
		);

		// Left side (perpendicular to direction)
		double leftOffsetX = -(carWidth / 2) * sinRad;
		double leftOffsetY = (carWidth / 2) * cosRad;

		// Right side (perpendicular to direction)
		double rightOffsetX = (carWidth / 2) * sinRad;
		double rightOffsetY = -(carWidth / 2) * cosRad;

		var frontLeft = new Point(carFront.X + leftOffsetX, carFront.Y + leftOffsetY);
		var frontRight = new Point(carFront.X + rightOffsetX, carFront.Y + rightOffsetY);
		var rearLeft = new Point(carRear.X + leftOffsetX, carRear.Y + leftOffsetY);
		var rearRight = new Point(carRear.X + rightOffsetX, carRear.Y + rightOffsetY);

		// Draw main car body (rectangle)
		var carBrush = new SolidColorBrush(Color.FromRgb(220, 53, 69)); // Red color
		var carOutline = new Pen(new SolidColorBrush(Color.FromRgb(139, 0, 0)), 1.5); // Dark red outline

		var carBodyGeometry = new StreamGeometry();
		using (var ctx = carBodyGeometry.Open())
		{
			ctx.BeginFigure(frontLeft, true);
			ctx.LineTo(frontRight);
			ctx.LineTo(rearRight);
			ctx.LineTo(rearLeft);
			ctx.EndFigure(true);
		}

		context.DrawGeometry(carBrush, carOutline, carBodyGeometry);

		// Draw windshield (front window area - light blue tint)
		double windshieldLength = carLength * 0.35;
		var windshieldFront = carFront;
		var windshieldRear = new Point(
			carScreenPos.X + (windshieldLength / 2) * cosRad,
			carScreenPos.Y + (windshieldLength / 2) * sinRad
		);

		double windshieldWidth = carWidth * 0.7;
		double windshieldLeftOffsetX = -(windshieldWidth / 2) * sinRad;
		double windshieldLeftOffsetY = (windshieldWidth / 2) * cosRad;
		double windshieldRightOffsetX = (windshieldWidth / 2) * sinRad;
		double windshieldRightOffsetY = -(windshieldWidth / 2) * cosRad;

		var windshieldTopLeft = new Point(windshieldFront.X + windshieldLeftOffsetX, windshieldFront.Y + windshieldLeftOffsetY);
		var windshieldTopRight = new Point(windshieldFront.X + windshieldRightOffsetX, windshieldFront.Y + windshieldRightOffsetY);
		var windshieldBottomLeft = new Point(windshieldRear.X + windshieldLeftOffsetX, windshieldRear.Y + windshieldLeftOffsetY);
		var windshieldBottomRight = new Point(windshieldRear.X + windshieldRightOffsetX, windshieldRear.Y + windshieldRightOffsetY);

		var windshieldBrush = new SolidColorBrush(Color.FromArgb(100, 135, 206, 250)); // Semi-transparent light blue
		var windshieldGeometry = new StreamGeometry();
		using (var ctx = windshieldGeometry.Open())
		{
			ctx.BeginFigure(windshieldTopLeft, true);
			ctx.LineTo(windshieldTopRight);
			ctx.LineTo(windshieldBottomRight);
			ctx.LineTo(windshieldBottomLeft);
			ctx.EndFigure(true);
		}

		context.DrawGeometry(windshieldBrush, null, windshieldGeometry);

		// Draw headlights (small circles at front corners)
		var headlightColor = new SolidColorBrush(Color.FromRgb(255, 255, 100)); // Yellow
		context.DrawEllipse(headlightColor, null, frontLeft, 2, 2);
		context.DrawEllipse(headlightColor, null, frontRight, 2, 2);
	}

	private void DrawDashedLine(DrawingContext context, Pen pen, Point start, Point end, double dashLength)
	{
		double dx = end.X - start.X;
		double dy = end.Y - start.Y;
		double distance = Math.Sqrt(dx * dx + dy * dy);

		if (distance == 0) return;

		double unitX = dx / distance;
		double unitY = dy / distance;

		for (double d = 0; d < distance; d += dashLength * 2)
		{
			double dashEnd = Math.Min(d + dashLength, distance);
			var dashStart = new Point(start.X + unitX * d, start.Y + unitY * d);
			var dashEndPoint = new Point(start.X + unitX * dashEnd, start.Y + unitY * dashEnd);
			context.DrawLine(pen, dashStart, dashEndPoint);
		}
	}

	private Color GetRoadColor(int speedLimit)
	{
		// Color-code roads by speed limit
		return speedLimit switch
		{
			<= 50 => Color.FromRgb(76, 175, 80),    // Green - slow
			<= 80 => Color.FromRgb(33, 150, 243),   // Blue - medium
			<= 100 => Color.FromRgb(255, 193, 7),   // Yellow - fast
			_ => Color.FromRgb(244, 67, 54)         // Red - very fast
		};
	}

	private void DrawSpeedSignLabel(DrawingContext context, Point roadPosition, Point signPosition, int speedLimit)
	{
		// Draw circular speed limit sign (like real speed signs)
		double signRadius = 11;

		// Draw a thin connecting line from road to sign (like a sign post)
		var connectPen = new Pen(new SolidColorBrush(Color.FromRgb(120, 120, 120)), 1);
		context.DrawLine(connectPen, roadPosition, signPosition);

		// Draw white background circle
		var whiteBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));
		context.DrawEllipse(whiteBrush, null, signPosition, signRadius, signRadius);

		// Draw red border circle
		var redPen = new Pen(new SolidColorBrush(Color.FromRgb(220, 20, 60)), 1.5);
		context.DrawEllipse(null, redPen, signPosition, signRadius, signRadius);

		// Draw speed limit text
		var text = new FormattedText(
			$"{speedLimit}",
			System.Globalization.CultureInfo.CurrentCulture,
			FlowDirection.LeftToRight,
			new Typeface("Arial", FontStyle.Normal, FontWeight.Bold),
			10,
			new SolidColorBrush(Color.FromRgb(220, 20, 60))
		);

		context.DrawText(
			text,
			new Point(signPosition.X - text.Width / 2, signPosition.Y - text.Height / 2)
		);
	}
	private void DrawNodes(DrawingContext context)
	{
		var nodeBrush = new SolidColorBrush(Color.FromRgb(231, 76, 60));
		var nodeOutlinePen = new Pen(new SolidColorBrush(Color.FromRgb(192, 57, 43)), 2);

		foreach (var node in _map!.Nodes)
		{
			if (_nodePositions.TryGetValue(node.Id, out var position))
			{
				// Determine colors based on selection
				SolidColorBrush brush;
				Pen outlinePen;

				if (node == _selectedStartNode)
				{
					// Start node = green
					brush = new SolidColorBrush(Color.FromRgb(76, 175, 80));
					outlinePen = new Pen(new SolidColorBrush(Color.FromRgb(56, 142, 60)), 3);
				}
				else if (node == _selectedEndNode)
				{
					// End node = blue
					brush = new SolidColorBrush(Color.FromRgb(33, 150, 243));
					outlinePen = new Pen(new SolidColorBrush(Color.FromRgb(13, 71, 161)), 3);
				}
				else
				{
					// Normal node = red
					brush = nodeBrush;
					outlinePen = nodeOutlinePen;
				}

				// Draw node circle
				context.DrawEllipse(
					brush, outlinePen,
					position,
					NodeRadius,
					NodeRadius
				);
			}
		}
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);

		if (change.Property == BoundsProperty)
		{
			CalculateNodePositions();
		}
	}

	private void DrawExplosion(DrawingContext context)
	{
		if (_explosionPosition == null) return;

		double elapsed = (DateTime.Now - _explosionStartTime).TotalSeconds;
		double progress = Math.Min(elapsed / EXPLOSION_DURATION_SECONDS, 1.0);

		// Convert car position to screen coordinates
		double availableWidth = Bounds.Width - (2 * Padding);
		double availableHeight = Bounds.Height - (2 * Padding);
		double screenX = Padding + ((_explosionPosition.Longitude - _cachedMinLon) / _cachedLonRange) * availableWidth;
		double screenY = Padding + ((_cachedMaxLat - _explosionPosition.Latitude) / _cachedLatRange) * availableHeight;

		var explosionCenter = new Point(screenX, screenY);

		// Draw multiple explosion rings expanding outward
		int numRings = 4;
		for (int i = 0; i < numRings; i++)
		{
			double ringProgress = (progress + (i * 0.15)) % 1.0;
			if (ringProgress < 0.8) // Only draw if still visible
			{
				double ringRadius = 10 + (ringProgress * 40);
				double opacity = 1.0 - ringProgress;

				// Outer ring (yellow/orange)
				var ringBrush = new SolidColorBrush(Color.FromRgb(
					(byte)(255 * opacity),
					(byte)(150 * opacity),
					0
				));
				var ringPen = new Pen(ringBrush, 2);
				context.DrawEllipse(null, ringPen, explosionCenter, ringRadius, ringRadius);
			}
		}

		// Draw explosion particles (expanding circles)
		int numParticles = 12;
		for (int i = 0; i < numParticles; i++)
		{
			double angle = (i / (double)numParticles) * Math.PI * 2;
			double particleProgress = progress;
			double particleDistance = particleProgress * 60;

			double particleX = explosionCenter.X + Math.Cos(angle) * particleDistance;
			double particleY = explosionCenter.Y + Math.Sin(angle) * particleDistance;

			double particleSize = (1.0 - particleProgress) * 6;
			if (particleSize > 0.5)
			{
				var particleBrush = new SolidColorBrush(Color.FromRgb(
					255,
					(byte)(100 * (1.0 - particleProgress)),
					0
				));
				context.DrawEllipse(particleBrush, null, new Point(particleX, particleY), particleSize, particleSize);
			}
		}

		// Draw center burst (bright white that fades)
		double centerOpacity = Math.Max(0, 1.0 - (progress * 1.5));
		if (centerOpacity > 0)
		{
			var centerBrush = new SolidColorBrush(Color.FromRgb(
				255,
				255,
				(byte)(150 * centerOpacity)
			));
			context.DrawEllipse(centerBrush, null, explosionCenter, 15, 15);
		}
	}
}

public class NodeSelectionChangedEventArgs : EventArgs
{
	public Node? StartNode { get; }
	public Node? EndNode { get; }

	public NodeSelectionChangedEventArgs(Node? startNode, Node? endNode)
	{
		StartNode = startNode;
		EndNode = endNode;
	}
}
