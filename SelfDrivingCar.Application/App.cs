using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SelfDrivingCar.TomTom;

namespace SelfDrivingCar.Application;

public sealed class App : Avalonia.Application
{
	private CarVisualizer? _visualizer;
	private CancellationTokenSource? _drivingCancellation;
	private CarDriver? _carController;
	private Router? _router;
	private RouteWindow? _window;

	public override void Initialize()
	{
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			CreateAndSetupWindow(desktop);
		}

		base.OnFrameworkInitializationCompleted();
	}

	private void CreateAndSetupWindow(IClassicDesktopStyleApplicationLifetime desktop)
	{
		_router = new Router();
		var route = GenerateNewRoute(_router);
		_carController = new CarDriver(route);
		_window = new RouteWindow(route);
		desktop.MainWindow = _window;
		_visualizer = new CarVisualizer(_window.RouteView, _carController);


		_window.StartButton.Click += OnStartButtonClick;
		_window.ResetButton.Click += OnResetButtonClick;
		_window.NewRouteButton.Click += OnNewRouteButtonClick;
		_window.ExitButton.Click += OnExitButtonClick;

		_window.Opened += (sender, args) =>
		{
			_visualizer.Start();
			_visualizer.ShowCarAtCurrentPosition();
		};
	}

	private async void OnStartButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (_carController != null && !_carController.IsActive)
		{
			_drivingCancellation?.Cancel();
			_drivingCancellation = new CancellationTokenSource();

			try
			{
				await Task.Run(() => _carController.DriveFullRoute(_drivingCancellation.Token), _drivingCancellation.Token);
			}
			catch (OperationCanceledException)
			{
				// Expected when cancellation is requested
			}
		}
	}

	private async void OnResetButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		_drivingCancellation?.Cancel();
		await Task.Delay(50);
		_carController?.Reset();

		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			_visualizer?.ShowCarAtCurrentPosition();
		});
	}

	private async void OnNewRouteButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		if (_router == null || _carController == null || _window == null || _visualizer == null)
			return;

		_drivingCancellation?.Cancel();
		await Task.Delay(50);

		var newRoute = GenerateNewRoute(_router);
		_carController.UpdateRoute(newRoute);


		await Dispatcher.UIThread.InvokeAsync(() =>
		{
			_window.UpdateRoute(newRoute);
			_visualizer = new CarVisualizer(_window.RouteView, _carController);
			_visualizer.Start();
			_visualizer.ShowCarAtCurrentPosition();
		});
	}

	private void OnExitButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
	{
		_drivingCancellation?.Cancel();
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.Shutdown();
		}
	}

	private List<Road> GenerateNewRoute(Router router)
	{
		var random = new Random();
		var startLon = 30 + random.NextDouble() * 60;
		var startLat = 30 + random.NextDouble() * 60;
		var endLon = 30 + random.NextDouble() * 60;
		var endLat = 30 + random.NextDouble() * 60;
		return router.GenerateRoads(new Coordinate(startLon, startLat), new Coordinate(endLon, endLat));
	}
}
