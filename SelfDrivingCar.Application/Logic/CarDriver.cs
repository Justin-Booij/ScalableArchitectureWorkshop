using SelfDrivingCar.TomTom;

namespace SelfDrivingCar.Application;

// TODO Workshop: This class is tightly coupled to the GPS Router
// Refactor using the Adapter pattern to decouple the car from GPS implementation
public class CarDriver
{
	private List<Road> _route;
	private int _currentRoadIndex;
	private double _currentSpeed;
	private Coordinate _currentPosition;
	private bool _isActive;
	private double _currentBearing;

	public Coordinate CurrentPosition => _currentPosition;
	public int CurrentRoadIndex => _currentRoadIndex;
	public bool IsActive => _isActive;
	public double CurrentBearing => _currentBearing;

	public CarDriver(List<Road> route)
	{
		_route = route;
		_currentRoadIndex = 0;
		_currentSpeed = 0;
		_isActive = false;
		if (route.Count > 0)
		{
			_currentPosition = route[0].From;
			// Set initial bearing to the first road segment
			_currentBearing = GeoMaths.CalculateBearing(route[0].From, route[0].To);
		}
		else
		{
			_currentBearing = 0;
		}
	}

	public void StartDriving()
	{
		Console.WriteLine("🚗 Starting self-driving car...");
		Console.WriteLine($"📍 Starting at ({_currentPosition.Latitude:F2}, {_currentPosition.Longitude:F2})");
		Console.WriteLine($"🛣️  Total roads in route: {_route.Count}");
		Console.WriteLine();

		_isActive = true;
	}


	public bool DriveToNextWaypoint(CancellationToken cancellationToken = default)
	{
	       if (_currentRoadIndex >= _route.Count)
	       {
		       Console.WriteLine("✅ Destination reached!");
		       _isActive = false;
		       return false;
	       }

	       var road = _route[_currentRoadIndex];
	       Coordinate from = road.From;
	       Coordinate to = road.To;
	       double distance = GeoMaths.CalculateDistance(from, to);
	       _currentSpeed = road.SpeedLimit;

	       Console.WriteLine($"🚦 Road {_currentRoadIndex + 1}/{_route.Count}");
	       Console.WriteLine($"   From: ({from.Latitude:F2}, {from.Longitude:F2})");
	       Console.WriteLine($"   To: ({to.Latitude:F2}, {to.Longitude:F2})");
	       Console.WriteLine($"   Speed limit: {road.SpeedLimit} km/h");
	       Console.WriteLine($"   Distance: {distance:F2} km");

	       double timeInHours = distance / _currentSpeed;
	       double timeInMinutes = timeInHours * 60;
	       Console.WriteLine($"   ⏱️  Estimated time: {timeInMinutes:F1} minutes");
	       Console.WriteLine();

	       if (!TravelAlongRoad(from, to, timeInMinutes, cancellationToken))
	       {
		       // Interrupted
		       return false;
	       }

	       _currentPosition = to;
	       _currentRoadIndex++;
	       return true;
	}


	private bool TravelAlongRoad(Coordinate from, Coordinate to, double timeInMinutes, CancellationToken cancellationToken = default)
	{
	       const int steps = 30;
	       int totalDurationMs = (int)Math.Min(timeInMinutes * 100, 2000);
	       int stepDelayMs = totalDurationMs / steps;
	       _currentBearing = GeoMaths.CalculateBearing(from, to);

	       for (int step = 0; step <= steps; step++)
	       {
		       if (cancellationToken.IsCancellationRequested)
		       {
			       _isActive = false;
			       return false;
		       }
		       double progress = (double)step / steps;
		       double lat = from.Latitude + (to.Latitude - from.Latitude) * progress;
		       double lon = from.Longitude + (to.Longitude - from.Longitude) * progress;
		       _currentPosition = new Coordinate(lon, lat);
		       if (step < steps)
			       Thread.Sleep(stepDelayMs);
	       }
	       return true;
	}

	public void DriveFullRoute(CancellationToken cancellationToken = default)
	{
	       StartDriving();
	       while (DriveToNextWaypoint(cancellationToken))
	       {
		       if (cancellationToken.IsCancellationRequested)
		       {
			       _isActive = false;
			       return;
		       }
	       }
	}

	public void Reset()
	{
		_isActive = false;
		_currentRoadIndex = 0;
		_currentSpeed = 0;
		if (_route.Count > 0)
		{
			_currentPosition = _route[0].From;
			_currentBearing = GeoMaths.CalculateBearing(_route[0].From, _route[0].To);
		}
		else
		{
			_currentBearing = 0;
		}
	}

	public void UpdateRoute(List<Road> newRoute)
	{
		_route = newRoute;
		Reset();
	}

	public double GetTotalDistanceRemaining()
	{
		double total = 0;
		if (_route.Count == 0)
			return 0;

		if (_currentRoadIndex < _route.Count)
		{
			var road = _route[_currentRoadIndex];
			total += GeoMaths.CalculateDistance(_currentPosition, road.To);
		}

		for (int i = _currentRoadIndex + 1; i < _route.Count; i++)
		{
			total += GeoMaths.CalculateDistance(_route[i].From, _route[i].To);
		}
		return total;
	}

	public double GetTotalTimeRemaining()
	{
		double totalTime = 0;
		if (_route.Count == 0)
			return 0;

		if (_currentRoadIndex < _route.Count)
		{
			var road = _route[_currentRoadIndex];
			double distance = GeoMaths.CalculateDistance(_currentPosition, road.To);
			totalTime += distance / road.SpeedLimit;
		}

		for (int i = _currentRoadIndex + 1; i < _route.Count; i++)
		{
			var road = _route[i];
			double distance = GeoMaths.CalculateDistance(road.From, road.To);
			totalTime += distance / road.SpeedLimit;
		}
		return totalTime * 60;
	}
}
