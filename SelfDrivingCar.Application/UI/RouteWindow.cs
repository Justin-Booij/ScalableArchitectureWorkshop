using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SelfDrivingCar.TomTom;

namespace SelfDrivingCar.Application;

public sealed class RouteWindow : Window
{
	private RouteView _routeView;
	private DockPanel _mainPanel;
	private StackPanel _buttonPanel;

	public Button StartButton { get; private set; }
	public Button ResetButton { get; private set; }
	public Button NewRouteButton { get; private set; }
	public Button ExitButton { get; private set; }

	public RouteView RouteView => _routeView;

	public RouteWindow(IReadOnlyList<Road> roads)
	{
		Title = "Route Visualizer";
		Width = 960;
		Height = 720;

		Background = Brushes.Black;

		_routeView = new RouteView(roads)
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Margin = new Thickness(16)
		};

		StartButton = CreateButton("Start Driving");
		ResetButton = CreateButton("Stop driving");
		NewRouteButton = CreateButton("New Journey");
		ExitButton = CreateButton("Exit");

		_buttonPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Center,
			Spacing = 10,
			Margin = new Thickness(0, 10, 0, 10),
			Background = Brushes.Black
		};

		_buttonPanel.Children.Add(StartButton);
		_buttonPanel.Children.Add(ResetButton);
		_buttonPanel.Children.Add(NewRouteButton);
		_buttonPanel.Children.Add(ExitButton);


		_mainPanel = new DockPanel();
		DockPanel.SetDock(_buttonPanel, Dock.Bottom);
		_mainPanel.Children.Add(_buttonPanel);
		_mainPanel.Children.Add(_routeView);

		Content = _mainPanel;
	}

	public void UpdateRoute(IReadOnlyList<Road> roads)
	{
		_mainPanel.Children.Remove(_routeView);
		_routeView = new RouteView(roads)
		{
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
			Margin = new Thickness(16)
		};

		_mainPanel.Children.Add(_routeView);
	}

	private Button CreateButton(string content)
	{
		var button = new Button
		{
			Content = content,
			Width = 130,
			Height = 44,
			FontSize = 14,
			Foreground = Brushes.Black,
			Background = Brushes.Azure,
			BorderBrush = Brushes.LightGray,
			BorderThickness = new Thickness(2),
			Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
			Padding = new Thickness(12, 6),
			CornerRadius = new CornerRadius(8),
		};
		return button;
	}
}
