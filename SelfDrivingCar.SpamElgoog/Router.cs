namespace SelfDrivingCar.GoogleMaps;

public class Router
{
	private int MIN_STEP_PERCENT = 5;
	private int MAX_STAP_PERCENT = 10;
	private double MAX_BEARING_DEVIATION = 45.0;
	private int[] speeds = [20, 30, 35, 50, 60, 75, 80];



	public List<Road> GenerateRoads(Coordinate start, Coordinate destination)
	{
		Random random = new Random();
		int amountOfRoads = random.Next(MIN_STEP_PERCENT, MAX_STAP_PERCENT);
		Coordinate currentLocation = start;
		List<Road> result = new List<Road>();
		double totalMiles = GeoMaths.CalculateDistance(start, destination);
		double distanceToGo = totalMiles;


		for (int i = 0; i < amountOfRoads - 1; i++)
		{

			double distance = random.NextDouble() * ((distanceToGo / 2) - 1.0) + 1.0;
			double bearing = GetRandomizedBearing(GeoMaths.CalculateBearing(currentLocation, destination));

			Coordinate currentTarget = GeoMaths.CalculateDestinationPoint(currentLocation, bearing, distance);

			Road road = new()
			{
				From = currentLocation,
				SpeedLimit = speeds[random.Next(speeds.Length - 1)],
				To = currentTarget
			};
			result.Add(road);
			currentLocation = currentTarget;
			distanceToGo = GeoMaths.CalculateDistance(currentTarget, destination);

		}

		result.Add(new()
		{
			From = currentLocation,
			SpeedLimit = speeds[random.Next(speeds.Length - 1)],
			To = destination
		});

		return result;
	}

	private double GetRandomizedBearing(double bearing)
	{
		double offset = (Random.Shared.NextDouble() * 2 - 1) * MAX_BEARING_DEVIATION;
		double newBearing = bearing + offset;

		newBearing = (newBearing % 360 + 360) % 360;
		return newBearing;
	}
}
