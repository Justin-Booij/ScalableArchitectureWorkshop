using SelfDrivingCar.World;

namespace SelfDrivingCar.Car.InternalTools;

public class InertialMeasurementUnit
{
    public double GetTargetHeading(Road segment)
    {
        return segment.Bearing;
    }
}
