using SelfDrivingCar.SpamElgoog;
using SelfDrivingCar.World;

namespace SelfDrivingCar.Car;

public class NavigationAdapter(SpamElgoogNavigate Navigation)
{
    private double MILES_TO_KM_CONVERSION_RATE = 1.609344;
    private List<Road>? currentRoute;
    
    public List<Road>? Navigate(Node start, Node end)
    {
        var route = Navigation.Navigate(start, end);
        if (route == null)
            return null;
            
        foreach (var road in route)
        {
            road.Distance *= MILES_TO_KM_CONVERSION_RATE;
            road.SpeedLimit *= MILES_TO_KM_CONVERSION_RATE;
        }

        currentRoute = route;
        return route;
    }
    
    public double GetSpeedCorrection(int roadIndex, double currentSpeed)
    {
        if (currentRoute == null)
            return 0;
            
        return currentRoute[roadIndex].SpeedLimit - currentSpeed;
    }

    public double GetBearingCorrection(int roadIndex, double currentBearing)
    {
        return Navigation.GetBearingCorrection(roadIndex, currentBearing);
    }

    public double GetDistance(int roadIndex)
    {
        if (currentRoute == null)
            return 0;
            
        return currentRoute[roadIndex].Distance;
    }
}