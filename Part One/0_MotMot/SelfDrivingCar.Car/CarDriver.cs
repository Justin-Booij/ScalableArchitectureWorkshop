using SelfDrivingCar.TomTom;
using SelfDrivingCar.World;
using SelfDrivingCar.Car.InternalTools;
using SelfDrivingCar.SpamElgoog;

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
	private Random random = new Random();

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
		CurrentRoute = route;
		
	}

	public void StartDriving(Node start, Node destination, CancellationToken cancellationToken = default)
	{
		Console.WriteLine("Starting self-driving car...");
		Console.WriteLine();
		CurrentPosition = start.Coordinate;

		IsActive = true;

		List<Road>? route = navigation.Navigate(start, destination);
		if (route == null) return;
		
		UpdateRoute(route);
		
		foreach (Road road in route)
		{
			if (!TravelAlongRoad(road, cancellationToken))
			{
				break;
			}

			CurrentRoadIndex++;
		}

		IsActive = false;
	}


	private bool TravelAlongRoad(Road road,
	  CancellationToken cancellationToken = default)
	{
		double traveledDistance = 0;
		CurrentSpeed += navigation.GetSpeedCorrection(road, CurrentSpeed);
		CurrentBearing += navigation.GetBearingCorrection(road, CurrentBearing);
		double distance = navigation.GetDistance(road);
		bool isNavigationAvailable = true;

		while (traveledDistance < distance)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				IsActive = false;
				return false;
			}

			if (isNavigationAvailable)
			{
				CorrectDrift();
				// For part two
				
				// isNavigationAvailable = CheckNavigationAvailability();
			}
			// else
			// {
			// 	Console.WriteLine("Navigation currently unavailable, you're on your own. Good luck");
			// 	isNavigationAvailable = !CheckNavigationAvailability();
			// }
			

			const double speedScaleFactor = 2400.0;
			double distanceToTravel = (CurrentSpeed * speedScaleFactor) / 72000.0;
			double remainingDistance = distance - traveledDistance;

			if (distanceToTravel > remainingDistance)
			{
				distanceToTravel = remainingDistance;
			}
			
			CurrentPosition = WorldMaths.CalculateDestinationPoint(CurrentPosition, CurrentBearing, distanceToTravel);
			traveledDistance += distanceToTravel;

			DriftBearing();
			DriftSpeed();

			Thread.Sleep(50);
		}

		return true;
	}

	private bool CheckNavigationAvailability()
	{
		int chance = random.Next(1, 6);
		if (chance == 5)
		{
			return false;
		}

		return true;
	}

	private void CorrectDrift()
	{
		Road road = CurrentRoute[CurrentRoadIndex];
		CurrentSpeed += navigation.GetSpeedCorrection(road, CurrentSpeed);
		CurrentBearing += navigation.GetBearingCorrection(road, CurrentBearing);
	}

	private void DriftBearing(double driftPercentage = 0.05)
	{
		CurrentBearing *= (1 + GetDriftFactor(driftPercentage));
	}

	private void DriftSpeed(double driftPercentage = 0.075)
	{
		CurrentSpeed *= (1 + GetDriftFactor(driftPercentage));
	}

	private double GetDriftFactor(double driftPercentage)
	{
		return random.NextDouble() * (driftPercentage * 2) - driftPercentage;
	}
}
