namespace SelfDrivingCar.TomTom;

public class Coordinate(double lon, double lat) {
  public double Longitude { get; set; } = lon;
  public double Latitude { get; set; } = lat;
}
