namespace SelfDrivingCar.World;

/// <summary>
/// Navigation service that uses the actual world map data to calculate the ground truth route.
/// This is independent of any navigation library like MotMot and represents what the route
/// "should be" based on the world structure.
/// </summary>
public class WorldNavigate
{
	private readonly Map _worldMap;

	public WorldNavigate(Map worldMap)
	{
		_worldMap = worldMap;
	}

	/// <summary>
	/// Calculate the shortest path using Dijkstra's algorithm based on actual world distances.
	/// Returns a list of nodes representing the optimal route from start to end.
	/// </summary>
	public List<Node>? FindRoute(Node start, Node end)
	{
		if (start == null || end == null) return null;

		var distances = new Dictionary<string, double>();
		var previous = new Dictionary<string, Node>();
		var unvisited = new HashSet<string>();
		
		foreach (var node in _worldMap.Nodes)
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
				break; // No path exists

			unvisited.Remove(currentId);

			var currentNode = _worldMap.GetNodeById(currentId);
			if (currentNode == null) continue;

			if (currentNode.Id == end.Id)
				break;
			
			var connections = _worldMap.GetConnections(currentNode);
			foreach (var (destination, distance, speedLimit) in connections)
			{
				double alt = distances[currentId] + distance;
				if (alt < distances[destination.Id])
				{
					distances[destination.Id] = alt;
					previous[destination.Id] = currentNode;
				}
			}
		}
		
		if (distances[end.Id] == double.MaxValue)
			return null; // No path found

		var route = new List<Node>();
		var current = end;

		while (current != null)
		{
			route.Insert(0, current);
			if (current.Id == start.Id)
				break;

			if (!previous.TryGetValue(current.Id, out var prev))
				break;

			current = prev;
		}

		return route;
	}
}
