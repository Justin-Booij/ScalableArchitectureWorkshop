using SelfDrivingCar.Application.World;

namespace SelfDrivingCar.Application;

/// <summary>
/// Service interface for navigation operations.
/// Provides route planning and navigation guidance.
/// This interface should be implemented by navigation libraries like MotMot.
/// </summary>
public interface INavigationService
{
	/// <summary>
	/// Sets the route for navigation
	/// </summary>
	void SetRoute(List<Road> route);

	/// <summary>
	/// Gets the next road from the current position in the route
	/// </summary>
	Road? GetNextRoad();

	/// <summary>
	/// Gets the recommended speed for the current road
	/// </summary>
	double GetCurrentSpeed();

	/// <summary>
	/// Gets the bearing (direction) to travel from current position
	/// </summary>
	double GetBearing(Coordinate from, Coordinate to);

	/// <summary>
	/// Gets the distance to the destination
	/// </summary>
	double GetDistanceToDestination(Coordinate currentPosition);

	/// <summary>
	/// Calculates the distance between two coordinates
	/// </summary>
	double CalculateDistance(Coordinate from, Coordinate to);

	/// <summary>
	/// Advances to the next road segment in the route
	/// </summary>
	bool MoveToNextRoad();

	/// <summary>
	/// Checks if we've reached the destination
	/// </summary>
	bool HasReachedDestination();

	/// <summary>
	/// Gets the total number of roads in the current route
	/// </summary>
	int GetTotalRoads();

	/// <summary>
	/// Gets the current road index
	/// </summary>
	int GetCurrentRoadIndex();

	/// <summary>
	/// Gets the start position of the route
	/// </summary>
	Coordinate? GetStartPosition();

	/// <summary>
	/// Resets the navigation service to the beginning of the route
	/// </summary>
	void Reset();
}
