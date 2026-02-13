namespace SelfDrivingCar.World;

public class WorldMaths
{
	private const double EarthRadiusKm = 6371.0;

	/// <summary>
	/// Calculates the distance between two coordinates using the Haversine formula
	/// </summary>
	/// <returns>Distance in kilometers</returns>
	public static double CalculateDistance(Coordinate a, Coordinate b)
	{
		var dLat = DegreesToRadians(b.Latitude - a.Latitude);
		var dLon = DegreesToRadians(b.Longitude - a.Longitude);

		var haversine = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
						Math.Cos(DegreesToRadians(a.Latitude)) * Math.Cos(DegreesToRadians(b.Latitude)) *
						Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

		var centralAngle = 2 * Math.Atan2(Math.Sqrt(haversine), Math.Sqrt(1 - haversine));
		var distance = EarthRadiusKm * centralAngle;

		return distance;
	}

	/// <summary>
	/// Calculates the bearing (direction) from one coordinate to another
	/// </summary>
	/// <returns>Bearing in degrees (0-360)</returns>
	public static double CalculateBearing(Coordinate a, Coordinate b)
	{
		var dLon = DegreesToRadians(b.Longitude - a.Longitude);
		var lat1Rad = DegreesToRadians(a.Latitude);
		var lat2Rad = DegreesToRadians(b.Latitude);

		var y = Math.Sin(dLon) * Math.Cos(lat2Rad);
		var x = Math.Cos(lat1Rad) * Math.Sin(lat2Rad) -
				Math.Sin(lat1Rad) * Math.Cos(lat2Rad) * Math.Cos(dLon);

		var bearing = Math.Atan2(y, x);
		var bearingDegrees = RadiansToDegrees(bearing);

		// Normalize to 0-360
		return (bearingDegrees + 360) % 360;
	}

	// <summary>
	/// Calculates a destination point given a starting point, bearing, and distance
	/// </summary>
	public static Coordinate CalculateDestinationPoint(Coordinate start, double bearing, double distanceKm)
	{
		var latRad = DegreesToRadians(start.Latitude);
		var lonRad = DegreesToRadians(start.Longitude);
		var bearingRad = DegreesToRadians(bearing);
		var angularDistance = distanceKm / EarthRadiusKm;

		var destLatRad = Math.Asin(
		  Math.Sin(latRad) * Math.Cos(angularDistance) +
		  Math.Cos(latRad) * Math.Sin(angularDistance) * Math.Cos(bearingRad)
		);

		var destLonRad = lonRad + Math.Atan2(
		  Math.Sin(bearingRad) * Math.Sin(angularDistance) * Math.Cos(latRad),
		  Math.Cos(angularDistance) - Math.Sin(latRad) * Math.Sin(destLatRad)
		);

		return new Coordinate(RadiansToDegrees(destLonRad), RadiansToDegrees(destLatRad));
	}

	/// <summary>
	/// Checks if a point is on a road segment (within a tolerance distance)
	/// </summary>
	/// <param name="pointToCheck">The point to check</param>
	/// <param name="roadStart">Start of the road segment</param>
	/// <param name="roadEnd">End of the road segment</param>
	/// <param name="toleranceKm">Maximum distance from road to be considered "on road" (default 0.1 km)</param>
	/// <returns>True if the point is on the road within tolerance</returns>
	public static bool IsPointOnRoad(Coordinate pointToCheck, Coordinate roadStart, Coordinate roadEnd, double toleranceKm = 0.1)
	{
		// Calculate distances
		double distanceToStart = CalculateDistance(pointToCheck, roadStart);
		double distanceToEnd = CalculateDistance(pointToCheck, roadEnd);
		double roadLength = CalculateDistance(roadStart, roadEnd);

		// If the point is beyond either end of the road by the tolerance, it's off the road
		// Using triangle inequality: if distToStart + distToEnd > roadLength + 2*tolerance, point is off road
		if (distanceToStart + distanceToEnd > roadLength + (2 * toleranceKm))
		{
			return false; // Point is beyond the endpoints
		}

		// Find closest point on the line segment using parametric projection
		// This uses the perpendicular distance calculation
		double closestDistance = CalculatePerpendicularDistance(pointToCheck, roadStart, roadEnd);

		return closestDistance <= toleranceKm;
	}

	/// <summary>
	/// Calculates the perpendicular distance from a point to a line segment
	/// </summary>
	private static double CalculatePerpendicularDistance(Coordinate point, Coordinate lineStart, Coordinate lineEnd)
	{
		double lat1 = DegreesToRadians(lineStart.Latitude);
		double lon1 = DegreesToRadians(lineStart.Longitude);
		double lat2 = DegreesToRadians(lineEnd.Latitude);
		double lon2 = DegreesToRadians(lineEnd.Longitude);
		double latP = DegreesToRadians(point.Latitude);
		double lonP = DegreesToRadians(point.Longitude);

		// Calculate the cross-track distance using haversine
		double dLon = lon2 - lon1;
		double y = Math.Sin(dLon) * Math.Cos(lat2);
		double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
		double bearing13 = Math.Atan2(y, x);

		double dLat = latP - lat1;
		double dLon13 = lonP - lon1;
		double distance13 = Math.Acos(Math.Sin(lat1) * Math.Sin(latP) + Math.Cos(lat1) * Math.Cos(latP) * Math.Cos(dLon13));

		double crossTrackDistance = Math.Asin(Math.Sin(distance13) * Math.Sin(bearing13 - Math.Atan2(y, x))) * EarthRadiusKm;

		return Math.Abs(crossTrackDistance);
	}

	private static double DegreesToRadians(double degrees)
	{
		return degrees * Math.PI / 180.0;
	}

	private static double RadiansToDegrees(double radians)
	{
		return radians * 180.0 / Math.PI;
	}
}
