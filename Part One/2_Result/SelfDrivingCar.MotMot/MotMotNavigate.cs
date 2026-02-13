using SelfDrivingCar.World;

namespace SelfDrivingCar.TomTom;

public class MotMotNavigate(Map worldMap) {
  public List<Road>? Navigate(Node start, Node end) {
    Console.WriteLine($"Finding route from {start.Name} to {end.Name}");

    var distances = new Dictionary<string, double>();
    var previous = new Dictionary<string, (Node node, int speedLimit)>();
    var unvisited = new HashSet<string>();

    foreach (var node in worldMap.Nodes)
    {
      distances[node.Id] = double.MaxValue;
      unvisited.Add(node.Id);
    }

    distances[start.Id] = 0;

    while (unvisited.Count > 0)
    {
      string? currentId = null;
      double minDistance = double.MaxValue;
      foreach (var id in unvisited)
      {
        if (distances[id] < minDistance)
        {
          minDistance = distances[id];
          currentId = id;
        }
      }

      if (currentId == null || minDistance == double.MaxValue)
        break; // No path found

      unvisited.Remove(currentId);
      var current = worldMap.GetNodeById(currentId);

      if (current == null)
        continue;

      if (currentId == end.Id)
        return ReconstructPath(previous, start, end);

      var connectedRoads = worldMap.GetConnections(current);
      foreach (var (destination, distance, speedLimit) in connectedRoads)
      {
        if (!unvisited.Contains(destination.Id))
          continue;

        double altDistance = distances[currentId] + distance;
        if (altDistance < distances[destination.Id])
        {
          distances[destination.Id] = altDistance;
          previous[destination.Id] = (current, speedLimit);
        }
      }
    }

    Console.WriteLine($"No route found from {start.Name} to {end.Name}");
    return null;
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

  private List<Road> ReconstructPath(
    Dictionary<string, (Node node, int speedLimit)> previous,
    Node start,
    Node end) {
    var path = new List<Road>();
    var current = end;

    while (current.Id != start.Id)
    {
      if (!previous.ContainsKey(current.Id))
        return new List<Road>();

      var (previousNode, speedLimit) = previous[current.Id];
      path.Insert(0, new Road {
        Distance = GeoMaths.CalculateDistance(previousNode.Coordinate, current.Coordinate),
        Bearing = GeoMaths.CalculateBearing(previousNode.Coordinate, current.Coordinate),
        SpeedLimit = speedLimit
      });
      current = previousNode;
    }

    return path;
  }
}
