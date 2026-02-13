using SelfDrivingCar.World;

namespace SelfDrivingCar.Car.InternalTools;

public class SignReader
{
    public int GetSpeedForCurrentRoad(Road road)
    {
        return road.SpeedLimit;
    }
}