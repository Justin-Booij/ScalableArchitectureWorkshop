using SelfDrivingCar.World;
using SelfDrivingCar.MotMot;

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
			if (!TravelAlongRoad(cancellationToken))
			{
				break;
			}

			CurrentRoadIndex++;
		}

		IsActive = false;
	}


	private bool TravelAlongRoad(
	  CancellationToken cancellationToken = default)
	{
		double traveledDistance = 0;
		CurrentSpeed += navigation.GetSpeedCorrection(CurrentRoadIndex, CurrentSpeed);
		CurrentBearing += navigation.GetBearingCorrection(CurrentRoadIndex, CurrentBearing);
		double distance = navigation.GetDistance(CurrentRoadIndex);

		while (traveledDistance < distance)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				IsActive = false;
				return false;
			}
			
			
			CorrectDrift();
			
			
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

	private void CorrectDrift()
	{
		CurrentSpeed += navigation.GetSpeedCorrection(CurrentRoadIndex, CurrentSpeed);
		CurrentBearing += navigation.GetBearingCorrection(CurrentRoadIndex, CurrentBearing);
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
