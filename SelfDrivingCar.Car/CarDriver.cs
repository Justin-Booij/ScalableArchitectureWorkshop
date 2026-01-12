using SelfDrivingCar.SpamElgoog;
using SelfDrivingCar.TomTom;
using SelfDrivingCar.World;
using Road = SelfDrivingCar.World.Road;

namespace SelfDrivingCar.Car;

public class CarDriver
{
	public Coordinate CurrentPosition;
	public bool IsActive = false;
	public double CurrentSpeed = 0;
	public double CurrentBearing = 0;
	public int CurrentRoadIndex = 0;
	public List<Road>? CurrentRoute = null;

	private MotMotNavigate navigation;

	public CarDriver(MotMotNavigate navigation)
	{
		this.navigation = navigation;
	}

	public List<Road>? CalculateRoute(Node start, Node destination)
	{
		return navigation.Navigate(start, destination);
	}

	public void UpdateRoute(List<Road> route)
	{
		CurrentRoadIndex = 0;
	}

	public void StartDriving(Node start, Node destination, CancellationToken cancellationToken = default)
	{
		Console.WriteLine("Starting self-driving car...");
		Console.WriteLine();
		CurrentPosition = start.Coordinate;

		IsActive = true;

		List<Road>? route = navigation.Navigate(start, destination);
		CurrentRoute = route;
		CurrentRoadIndex = 0;
		int roadNum = 0;
		foreach (Road road in route)
		{
			if (!TravelAlongRoad(road, cancellationToken))
			{
				break;
			}

			CurrentRoadIndex++;
			roadNum++;
		}

		IsActive = false;
	}


	private bool TravelAlongRoad(Road road,
	  CancellationToken cancellationToken = default)
	{
		double traveledDistance = 0;
		CurrentSpeed = road.SpeedLimit;
		double distance = road.Distance;
		double bearing = road.Bearing;

		CurrentBearing = bearing;

		while (traveledDistance < distance)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				IsActive = false;
				return false;
			}

			const double speedScaleFactor = 2400.0;
			double distanceToTravel = (CurrentSpeed * speedScaleFactor) / 72000.0;
			double remainingDistance = distance - traveledDistance;

			if (distanceToTravel > remainingDistance)
			{
				distanceToTravel = remainingDistance;
			}

			CurrentPosition = WorldMaths.CalculateDestinationPoint(CurrentPosition, bearing, distanceToTravel);
			traveledDistance += distanceToTravel;
			Thread.Sleep(50);
		}

		return true;
	}
}
