using Avalonia.Controls.ApplicationLifetimes;
using SelfDrivingCar.Application.UI;
using SelfDrivingCar.Car;
using SelfDrivingCar.SpamElgoog;
using SelfDrivingCar.TomTom;
using SelfDrivingCar.World;

namespace SelfDrivingCar.Application;

public sealed class App : Avalonia.Application
{
	private CarDriver carController;
	private MotMotNavigate motMotNavigate;
    private SpamElgoogNavigate spamElgoogNavigate;
	private Map currentMap;
	private MainWindow mainWindow;

	public override void Initialize()
	{
		currentMap = MapGenerator.GenerateMap();
		motMotNavigate = new MotMotNavigate(currentMap);
        spamElgoogNavigate = new SpamElgoogNavigate(currentMap);
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
      carController = new CarDriver(spamElgoogNavigate);
	  mainWindow = new MainWindow();
	  
		if (currentMap != null)
		{
			mainWindow.SetMap(currentMap);
		}

		mainWindow.SetCarDriver(carController);

		desktop.MainWindow = mainWindow;
	}
}
