using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using System.Threading.Tasks;
using SelfDrivingCar.Application.UI;
using SelfDrivingCar.Car;
using SelfDrivingCar.World;

namespace SelfDrivingCar.Application.UI;

public partial class MainWindow : Window
{
	private MapCanvas? _mapCanvas;
	private TextBlock? _nodeCountText;
	private TextBlock? _roadCountText;
	private TextBlock? _statusText;
	private Button? _startDrivingButton;
	private CarDriver? _carDriver;
	private WorldNavigate? _worldNavigate;
	private bool _isCurrentlyDriving;
	private Node? _currentStartNode;
	private Node? _currentEndNode;
	private Map? _currentMap;
	private List<Node>? _currentRouteNodes; // Track the node sequence for crash detection
	private DateTime _offRoadStartTime = DateTime.MinValue;
	private const double OFF_ROAD_TOLERANCE_KM = 0.20; // Tolerance for being "on road" (200 meters)
	private const double OFF_ROAD_CRASH_THRESHOLD_SECONDS = 1.5; // Time before crash

	// Speed violation tracking
	private int _totalFineAmount = 0;
	private int _lastFineForRoadIndex = -1; // Track which road we fined for to avoid duplicate fines
	private const int SPEED_VIOLATION_THRESHOLD_KMH = 5; // Fine if speed is off by 5+ km/h
	private const int BASE_FINE_PER_KMH = 10; // Base fine per km/h over threshold
	private DispatcherTimer? _fineNotificationTimer;
	private TextBlock? _fineNotificationText;
	private Border? _fineNotificationPopup;
	private Coordinate? _lastSpeedCheckPosition;
	private DateTime _lastSpeedCheckTime = DateTime.MinValue;
	private int _lastCheckedRoadIndex = -1;

	public MainWindow()
	{
		InitializeComponent();

		// Find controls after initialization
		_mapCanvas = this.FindControl<MapCanvas>("MapCanvas");
		_nodeCountText = this.FindControl<TextBlock>("NodeCountText");
		_roadCountText = this.FindControl<TextBlock>("RoadCountText");
		_statusText = this.FindControl<TextBlock>("StatusText");
		_startDrivingButton = this.FindControl<Button>("StartDrivingButton");
		_fineNotificationText = this.FindControl<TextBlock>("FineNotificationText");
		_fineNotificationPopup = this.FindControl<Border>("FineNotificationPopup");

		// Subscribe to selection changes
		if (_mapCanvas != null)
		{
			_mapCanvas.SelectionChanged += OnNodeSelectionChanged;
		}

		_isCurrentlyDriving = false;
	}

	public void SetMap(Map map)
	{
		_currentMap = map;
		_mapCanvas?.SetMap(map);
		_worldNavigate = new WorldNavigate(map);
		UpdateStatistics(map);
	}

	public void SetCarDriver(CarDriver carDriver)
	{
		_carDriver = carDriver;
	}

	private void UpdateStatistics(Map map)
	{
		if (_nodeCountText != null)
		{
			_nodeCountText.Text = map.Nodes.Count.ToString();
		}

		int roadCount = 0;
		foreach (var node in map.Nodes)
		{
			roadCount += map.GetConnections(node).Count;
		}

		if (_roadCountText != null)
		{
			_roadCountText.Text = roadCount.ToString();
		}
	}

	private void OnNodeSelectionChanged(object? sender, NodeSelectionChangedEventArgs e)
	{
		// Track the selected nodes
		_currentStartNode = e.StartNode;
		_currentEndNode = e.EndNode;

		// Update button enable state
		bool hasRoute = e.StartNode != null && e.EndNode != null && !_isCurrentlyDriving;
		if (_startDrivingButton != null)
		{
			_startDrivingButton.IsEnabled = hasRoute;
		}

		// Update status display
		if (_statusText != null)
		{
			if (e.StartNode != null && e.EndNode != null)
			{
				_statusText.Text = $"Start: {e.StartNode.Name} → End: {e.EndNode.Name} | Press 'Start Driving' to begin";

				// Calculate ground truth route from World
				if (_worldNavigate != null)
				{
					var worldRoute = _worldNavigate.FindRoute(e.StartNode, e.EndNode);
					if (worldRoute != null && worldRoute.Count > 0)
					{
						// Store the route nodes for crash detection
						_currentRouteNodes = worldRoute;

						// Display the route on the map
						_mapCanvas?.SetPlannedRoute(worldRoute);

						// Internally calculate MotMot route for driving
						if (_carDriver != null)
						{
							var motMotRoute = _carDriver.CalculateRoute(e.StartNode, e.EndNode);
							if (motMotRoute != null && motMotRoute.Count > 0)
							{
								_carDriver.UpdateRoute(motMotRoute);
							}
						}
					}
					else
					{
						_statusText.Text = "No route found between selected nodes";
						_mapCanvas?.SetPlannedRoute(null);
						if (_startDrivingButton != null) _startDrivingButton.IsEnabled = false;
					}
				}
			}
			else if (e.StartNode != null)
			{
				_statusText.Text = $"Start: {e.StartNode.Name} | Click right to select end";
				_mapCanvas?.SetPlannedRoute(null);
			}
			else if (e.EndNode != null)
			{
				_statusText.Text = $"End: {e.EndNode.Name} | Click left to select start";
				_mapCanvas?.SetPlannedRoute(null);
			}
			else
			{
				_statusText.Text = "Select start (left-click) and end (right-click) nodes";
				_mapCanvas?.SetPlannedRoute(null);
			}
		}
	}

	private void OnStartDrivingClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (_carDriver == null || _mapCanvas == null || _currentStartNode == null || _currentEndNode == null)
		{
			return;
		}

		// Reset state for new drive
		_isCurrentlyDriving = true;
		_lastCheckedRoadIndex = -1; // Reset speed violation tracking
		_offRoadStartTime = DateTime.MinValue;

		if (_startDrivingButton != null)
		{
			_startDrivingButton.IsEnabled = false;
		}

		// Show car on map
		_mapCanvas.ShowCar(true);
		if (_statusText != null)
		{
			_statusText.Text = "Driving...";
		}

		Console.WriteLine("=== Starting Driving ===");
		Console.WriteLine($"Start Node: {_currentStartNode.Name}");
		Console.WriteLine($"End Node: {_currentEndNode.Name}");

		// Create a timer to update car position on the UI thread
		var timer = new DispatcherTimer();
		timer.Interval = TimeSpan.FromMilliseconds(50); // 20 FPS
		int updateCount = 0;
		var startTime = DateTime.Now;
		_offRoadStartTime = DateTime.MinValue;

		CancellationTokenSource cancellationTokenSource = new();
		CancellationToken cancellationToken = cancellationTokenSource.Token;

		timer.Tick += (s, args) =>
		{
			try
			{
				if (_carDriver != null && _mapCanvas != null)
				{
					// Update car position from CarDriver
					_mapCanvas.UpdateCarPosition(_carDriver.CurrentPosition, _carDriver.CurrentBearing);
					_mapCanvas.InvalidateVisual();
					updateCount++;

					// Check if car is on road (off-road crash detection)
					bool isOnRoad = IsCarOnCurrentRoad();

					if (!isOnRoad)
					{
						// Car went off-road
						if (_offRoadStartTime == DateTime.MinValue)
						{
							_offRoadStartTime = DateTime.Now;
							Console.WriteLine("⚠️  Car went off-road!");
						}

						double offRoadDuration = (DateTime.Now - _offRoadStartTime).TotalSeconds;
						if (offRoadDuration >= OFF_ROAD_CRASH_THRESHOLD_SECONDS)
						{
							// Crash! Stop the car
							Console.WriteLine($"💥 CRASH! Car was off-road for {offRoadDuration:F1} seconds");
							timer.Stop();
							_isCurrentlyDriving = false;

							// Trigger explosion animation at car's last position
							_mapCanvas.TriggerExplosion(_carDriver.CurrentPosition);
							_mapCanvas.ShowCar(false);
							cancellationTokenSource.Cancel();

							if (_statusText != null)
							{
								_statusText.Text = "💥 CRASH! Car went off-road!";
							}

							// Wait for explosion animation to finish, then show crash dialog
							Task.Delay(1500).ContinueWith(_ =>
							{
								Dispatcher.UIThread.Post(async () =>
								{
									await ShowCrashDialog();
									_startDrivingButton!.IsEnabled = true;
								});
							});

							return;
						}
					}
					else
					{
						// Car is back on road
						if (_offRoadStartTime != DateTime.MinValue)
						{
							Console.WriteLine("✓ Car back on road");
							_offRoadStartTime = DateTime.MinValue;
						}

						// Check for speed violations on this road
						CheckSpeedViolation();
					}

					if (updateCount % 20 == 0) // Log every second (20 updates * 50ms = 1000ms)
					{
						var elapsed = DateTime.Now - startTime;
						Console.WriteLine($"[{elapsed.TotalSeconds:F1}s] Pos=({_carDriver.CurrentPosition.Latitude:F6},{_carDriver.CurrentPosition.Longitude:F6}) Bearing={_carDriver.CurrentBearing:F1}° Speed={_carDriver.CurrentSpeed:F1}km/h Road#{_carDriver.CurrentRoadIndex}");
					}

					// Stop when driving is complete
					if (!_carDriver.IsActive)
					{
						var totalTime = DateTime.Now - startTime;
						timer.Stop();
						_isCurrentlyDriving = false;
						_mapCanvas.ShowCar(false);
						if (_statusText != null)
						{
							_statusText.Text = "Drive completed! Select new start/end nodes to plan another route";
						}
						Console.WriteLine($"=== Driving Completed === (Duration: {totalTime.TotalSeconds:F1}s, Total updates: {updateCount})");
						_startDrivingButton!.IsEnabled = true;
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"❌ EXCEPTION in timer.Tick: {ex}");
				Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
				timer.Stop();
				_isCurrentlyDriving = false;
				_mapCanvas?.ShowCar(false);
				if (_statusText != null)
				{
					_statusText.Text = $"Timer error: {ex.Message}";
				}
				_startDrivingButton!.IsEnabled = true;
			}
		};

		timer.Start();
		Console.WriteLine("=== TIMER STARTED ===");

		// Run StartDriving on a background thread
		Task.Run(() =>
		{
			try
			{
				Console.WriteLine("🚗 Calling CarDriver.StartDriving() on background thread");
				_carDriver.StartDriving(_currentStartNode, _currentEndNode, cancellationToken);
				Console.WriteLine("🚗 StartDriving completed");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error during driving: {ex}");
				Dispatcher.UIThread.Post(() =>
				{
					timer.Stop();
					_isCurrentlyDriving = false;
					_mapCanvas.ShowCar(false);
					if (_statusText != null)
					{
						_statusText.Text = $"Error: {ex.Message}";
					}
				});
			}
		});
	}

	/// <summary>
	/// Checks if the car is currently on the road it's supposed to be traveling on.
	/// Uses both position and bearing checks to be more consistent - avoids false positives
	/// when roads are close together.
	/// </summary>
	private bool IsCarOnCurrentRoad()
	{
		if (_carDriver == null || _currentMap == null || _currentRouteNodes == null || _currentRouteNodes.Count < 2)
		{
			return true; // Can't check, assume it's on road
		}

		int currentRoadIdx = _carDriver.CurrentRoadIndex;
		if (currentRoadIdx < 0 || currentRoadIdx >= _currentRouteNodes.Count - 1)
		{
			return true; // Out of bounds, assume safe
		}

		// Get the two nodes that form the current road segment
		var fromNode = _currentRouteNodes[currentRoadIdx];
		var toNode = _currentRouteNodes[currentRoadIdx + 1];
		var carPos = _carDriver.CurrentPosition;

		// Check 1: Car position must be on the road
		bool isPositionOnRoad = WorldMaths.IsPointOnRoad(carPos, fromNode.Coordinate, toNode.Coordinate, OFF_ROAD_TOLERANCE_KM);
		if (!isPositionOnRoad)
		{
			return false; // Car is definitely off the road
		}

		// Check 2: Car bearing should be roughly aligned with road bearing
		// Calculate expected bearing from start to end of current road
		double expectedBearing = WorldMaths.CalculateBearing(fromNode.Coordinate, toNode.Coordinate);
		double carBearing = _carDriver.CurrentBearing;

		// Calculate the angle difference (accounting for wraparound at 360°)
		double bearingDiff = Math.Abs(expectedBearing - carBearing);
		if (bearingDiff > 180)
		{
			bearingDiff = 360 - bearingDiff;
		}

		// Allow up to 45 degrees deviation from expected bearing
		// This prevents the car from being considered "on road" if it's traveling perpendicular to it
		const double MAX_BEARING_DEVIATION_DEGREES = 45.0;
		bool isBearingAligned = bearingDiff <= MAX_BEARING_DEVIATION_DEGREES;

		return isPositionOnRoad && isBearingAligned;
	}

	private async Task ShowCrashDialog()
	{
		// Create a crash dialog window
		var crashDialog = new Window
		{
			Title = "💥 CRASH! 💥",
			Width = 400,
			Height = 250,
			WindowStartupLocation = WindowStartupLocation.CenterOwner,
			CanResize = false
		};

		// Create the content
		var stackPanel = new StackPanel
		{
			Spacing = 20,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};

		// Title
		var titleText = new TextBlock
		{
			Text = "💥 YOU CRASHED! 💥",
			FontSize = 24,
			FontWeight = FontWeight.Bold,
			Foreground = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
			TextAlignment = TextAlignment.Center
		};

		// Message
		var messageText = new TextBlock
		{
			Text = "Your car went off the road and crashed!",
			FontSize = 14,
			TextAlignment = TextAlignment.Center,
			Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100))
		};

		// Exit button
		var exitButton = new Button
		{
			Content = "Exit",
			Padding = new Thickness(10,0,10,0),
			Width = 100,
			Height = 40,
			Foreground = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
			Background = new SolidColorBrush(Color.FromRgb(220, 53, 69)),
			FontSize = 15
		};
		var closeButton = new Button
		{
			Content = "Close",
			Padding = new Thickness(10,0,10,0),
			Width = 100,
			Height = 40,
			Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
			Background = new SolidColorBrush(Color.FromRgb(50, 150, 250)),
			FontSize = 15
		};

		exitButton.Click += (s, e) =>
		{
			Environment.Exit(0);
		};

		closeButton.Click += (s, e) =>
		{
			crashDialog.Close();
		};

		// Add controls to panel
		stackPanel.Children.Add(titleText);
		stackPanel.Children.Add(messageText);

		// Add button in a centered container
		var buttonContainer = new Border
		{
			HorizontalAlignment = HorizontalAlignment.Center
		};
		
		var buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Spacing = 10,
			VerticalAlignment = VerticalAlignment.Center,
			HorizontalAlignment = HorizontalAlignment.Center
		};
		
		buttonPanel.Children.Add(closeButton);
		buttonPanel.Children.Add(exitButton);

		buttonContainer.Child = buttonPanel;
		stackPanel.Children.Add(buttonContainer);

		// Wrap panel in border for padding
		var contentBorder = new Border
		{
			Padding = new Thickness(30, 40, 30, 30),
			Child = stackPanel
		};

		crashDialog.Content = contentBorder;

		// Show the dialog
		await crashDialog.ShowDialog(this);
	}

	private void CheckSpeedViolation()
	{
		if (_carDriver == null || _carDriver.CurrentRoadIndex < 0 || _carDriver.CurrentRoute == null || _carDriver.CurrentRoute.Count == 0)
		{
			return;
		}

		// Only check once per road at the end of the road
		if (_lastCheckedRoadIndex == _carDriver.CurrentRoadIndex)
		{
			return; // Already checked this road
		}

		int roadIdx = _carDriver.CurrentRoadIndex;

		// Check bounds BEFORE accessing arrays
		if (_currentMap == null || _currentRouteNodes == null || _carDriver.CurrentRoute == null ||
			roadIdx < 0 || roadIdx >= _currentRouteNodes.Count - 1 || roadIdx >= _carDriver.CurrentRoute.Count)
		{
			_lastCheckedRoadIndex = roadIdx;
			return;
		}

		// Get the speed limit from the navigation service (might be in mph if using SpamElgoog)
		var navigationRoad = _carDriver.CurrentRoute[roadIdx];
		int navigationSpeedLimit = navigationRoad.SpeedLimit;

		// Get the world's actual speed limit for this road segment

		var fromNode = _currentRouteNodes[roadIdx];
		var toNode = _currentRouteNodes[roadIdx + 1];

		// Get the actual speed limit from the world map
		var connections = _currentMap.GetConnections(fromNode);
		int? worldSpeedLimit = null;

		foreach (var (destination, distance, speedLimit) in connections)
		{
			if (destination.Id == toNode.Id)
			{
				worldSpeedLimit = speedLimit;
				break;
			}
		}

		if (!worldSpeedLimit.HasValue)
		{
			_lastCheckedRoadIndex = roadIdx;
			return;
		}

		// Compare: if navigation speed limit is significantly different from world speed limit,
		// it indicates a unit mismatch (e.g., SpamElgoog using mph instead of km/h)
		int expectedSpeed = worldSpeedLimit.Value;
		int reportedSpeed = navigationSpeedLimit;

		// A significant discrepancy indicates wrong units
		// e.g., 50 mph reported as 50 km/h is a ~30 km/h error
		int speedDifference = Math.Abs(expectedSpeed - reportedSpeed);

		if (speedDifference >= SPEED_VIOLATION_THRESHOLD_KMH)
		{
			// Fine based on the discrepancy
			int fine = Math.Max(speedDifference * BASE_FINE_PER_KMH, 25); // Minimum fine of $25

			string violationType = reportedSpeed > expectedSpeed ? "Speeding" : "Too Slow";
			ShowFineNotification(fine, $"{violationType} ({reportedSpeed} vs {expectedSpeed} km/h limit)");

			Console.WriteLine($"💰 Speed violation detected: Reported={reportedSpeed} km/h, Expected={expectedSpeed} km/h, Fine=${fine}");
		}

		_lastCheckedRoadIndex = roadIdx;
	}

	private void ShowFineNotification(int fineAmount, string reason)
	{
		if (_fineNotificationText == null || _fineNotificationPopup == null)
		{
			return;
		}

		_totalFineAmount += fineAmount;

		_fineNotificationText.Text = $"${fineAmount} - {reason} | Total Fines: ${_totalFineAmount}";
		_fineNotificationPopup.Opacity = 1.0;

		// Auto-hide notification after 3 seconds
		if (_fineNotificationTimer != null)
		{
			_fineNotificationTimer.Stop();
		}

		_fineNotificationTimer = new DispatcherTimer();
		_fineNotificationTimer.Interval = TimeSpan.FromSeconds(3);
		_fineNotificationTimer.Tick += (s, e) =>
		{
			_fineNotificationTimer.Stop();
			_fineNotificationPopup.Opacity = 0;
		};
		_fineNotificationTimer.Start();
	}
}
