using SelfDrivingCar.World;

namespace SelfDrivingCar.Car.InternalTools;

public class SignReader
{
    public double GetSpeedForCurrentRoad(Road road)
    {
        return road.SpeedLimit;
    }
}