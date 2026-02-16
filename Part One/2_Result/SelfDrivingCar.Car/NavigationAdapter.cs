using SelfDrivingCar.SpamElgoog;
using SelfDrivingCar.World;

namespace SelfDrivingCar.Car;

public class NavigationAdapter(SpamElgoogNavigate Navigation)
{
    private double MILES_TO_KM_CONVERSION_RATE = 1.609344;
    public List<Road>? Navigate(Node start, Node end)
    {
        var route = Navigation.Navigate(start, end);
        foreach (var road in route)
        {
            road.Distance *= MILES_TO_KM_CONVERSION_RATE;
        }
    }
    
    public double GetSpeedCorrection(Road road, double currentSpeed)
    {
        return road.SpeedLimit - currentSpeed;
    }

    public double GetBearingCorrection(Road road, double currentBearing)
    {
        return road.Bearing - currentBearing;
    }

    public double GetDistance(Road road)
    {
        return road.Distance;
    }
}