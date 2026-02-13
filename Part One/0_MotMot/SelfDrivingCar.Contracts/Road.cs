namespace SelfDrivingCar.Application.World;

public struct Road {
  public Coordinate From { get; set; }
  public Coordinate To { get; set; }
  public int SpeedLimit { get; set; }
}
