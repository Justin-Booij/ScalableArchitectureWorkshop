using SelfDrivingCar.World;

namespace SelfDrivingCar.TomTom;

public class GeoMaths {
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

    return new Coordinate(RadiansToDegrees(destLonRad),RadiansToDegrees(destLatRad));
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
