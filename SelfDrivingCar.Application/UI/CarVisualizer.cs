using Avalonia.Threading;
using SelfDrivingCar.TomTom;

namespace SelfDrivingCar.Application;

/// <summary>
/// Polls the car's position and updates the visual display.
/// Completely decoupled from the car controller - the car doesn't know about visualization.
/// </summary>
public class CarVisualizer
{
	private readonly RouteView _routeView;
	private readonly CarDriver _carController;
	private readonly DispatcherTimer _timer;

	public CarVisualizer(RouteView routeView, CarDriver carController)
	{
		_routeView = routeView;
		_carController = carController;

		// Set up a timer to poll the car's position at 60 FPS
		_timer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(16) // ~60 FPS
		};
		_timer.Tick += OnTimerTick;
	}

	public void Start()
	{
		_timer.Start();
	}

	public void Stop()
	{
		_timer.Stop();
	}

	public void ShowCarAtCurrentPosition()
	{
		// Show the car at its current position without clearing it
		_routeView.UpdateCarPosition(
			_carController.CurrentPosition,
			_carController.CurrentRoadIndex,
			_carController.CurrentBearing,
			_carController.GetTotalDistanceRemaining(),
			_carController.GetTotalTimeRemaining());
	}

	private void OnTimerTick(object? sender, EventArgs e)
	{
		// Always update the car position, whether active or not
		_routeView.UpdateCarPosition(
			_carController.CurrentPosition,
			_carController.CurrentRoadIndex,
			_carController.CurrentBearing,
			_carController.GetTotalDistanceRemaining(),
			_carController.GetTotalTimeRemaining());
	}
}
