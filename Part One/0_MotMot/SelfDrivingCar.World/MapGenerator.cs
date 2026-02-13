namespace SelfDrivingCar.World;

public class MapGenerator
{
	private static readonly int[] Speeds = [30, 50, 60, 80, 100, 120, 130];
	private const int MIN_NODES = 35;
	private const int MAX_NODES = 45;
	private const double MIN_DISTANCE = 5;
	private const double MAX_DISTANCE = 200;
	private const int MAX_CONNECTIONS_PER_NODE = 3;
	private const int TARGET_CONNECTIONS_PER_NODE = 2;

	public static Map GenerateMap()
	{
		var map = new Map();
		var random = new Random();
		var nodes = CreateNodes(random);

		AddNodesToMap(map, nodes);
		var connectionCounts = new Dictionary<string, int>();
		InitializeConnectionCounts(nodes, connectionCounts);

		CreateSparseNetwork(map, nodes, random, connectionCounts);
		EnsureAllNodesConnected(map, nodes, random, connectionCounts);
		EnsureGraphConnected(map, nodes, random, connectionCounts);


		return map;
	}

	private static List<Node> CreateNodes(Random random)
	{
		var nodes = new List<Node>();
		int nodeCount = random.Next(MIN_NODES, MAX_NODES);
		double centerLon = 50 + random.NextDouble() * 20;
		double centerLat = 50 + random.NextDouble() * 20;

		for (int i = 0; i < nodeCount; i++)
		{
			double lonOffset = (random.NextDouble() - 0.5) * 3.0;
			double latOffset = (random.NextDouble() - 0.5) * 3.0;

			var node = new Node(
			  $"N{i:D3}",
			  $"Node {i + 1}",
			  new Coordinate(centerLon + lonOffset, centerLat + latOffset)
			);
			nodes.Add(node);
		}

		return nodes;
	}


	private static void AddNodesToMap(Map map, List<Node> nodes)
	{
		foreach (var node in nodes)
		{
			map.AddNode(node);
		}
	}

	private static void InitializeConnectionCounts(List<Node> nodes, Dictionary<string, int> connectionCounts)
	{
		foreach (var node in nodes)
		{
			connectionCounts[node.Id] = 0;
		}
	}

	private static void CreateSparseNetwork(Map map, List<Node> nodes, Random random, Dictionary<string, int> connectionCounts)
	{
		for (int i = 0; i < nodes.Count; i++)
		{
			if (connectionCounts[nodes[i].Id] >= TARGET_CONNECTIONS_PER_NODE)
				continue;

			var nearestNodes = FindNearestCandidates(nodes, i, connectionCounts);
			ConnectToNearestNeighbors(map, nodes[i], nearestNodes, random, connectionCounts);
		}
	}

	private static List<(Node Node, double Distance)> FindNearestCandidates(List<Node> nodes, int currentIndex, Dictionary<string, int> connectionCounts)
	{
		return nodes
		  .Where((n, idx) => idx != currentIndex && connectionCounts[n.Id] < MAX_CONNECTIONS_PER_NODE)
		  .Select(n => (Node: n, Distance: WorldMaths.CalculateDistance(nodes[currentIndex].Coordinate, n.Coordinate)))
		  .OrderBy(x => x.Distance)
		  .Take(5)
		  .ToList();
	}


	private static void ConnectToNearestNeighbors(Map map, Node currentNode, List<(Node Node, double Distance)> candidates, Random random, Dictionary<string, int> connectionCounts)
	{
		int targetConnections = random.Next(1, MAX_CONNECTIONS_PER_NODE);
		int connectionsAdded = 0;

		foreach (var (candidate, distance) in candidates)
		{
			if (connectionsAdded >= targetConnections)
				break;

			if (distance < MIN_DISTANCE || distance > MAX_DISTANCE)
				continue;

			if (connectionCounts[currentNode.Id] >= TARGET_CONNECTIONS_PER_NODE || connectionCounts[candidate.Id] >= MAX_CONNECTIONS_PER_NODE)
				continue;

			int speedLimit = Speeds[random.Next(Speeds.Length)];
			map.AddBidirectionalConnection(currentNode, candidate, speedLimit);
			connectionCounts[currentNode.Id]++;
			connectionCounts[candidate.Id]++;
			connectionsAdded++;
		}
	}

	private static void EnsureAllNodesConnected(Map map, List<Node> nodes, Random random, Dictionary<string, int> connectionCounts)
	{
		for (int i = 0; i < nodes.Count; i++)
		{
			if (connectionCounts[nodes[i].Id] == 0)
			{
				var nearest = FindNearestNode(nodes, i);
				int speedLimit = Speeds[random.Next(Speeds.Length)];
				map.AddBidirectionalConnection(nodes[i], nearest, speedLimit);
				connectionCounts[nodes[i].Id]++;
				connectionCounts[nearest.Id]++;
			}
		}
	}

	private static Node FindNearestNode(List<Node> nodes, int currentIndex)
	{
		return nodes
		  .Where((n, idx) => idx != currentIndex)
		  .Select(n => (Node: n, Distance: WorldMaths.CalculateDistance(nodes[currentIndex].Coordinate, n.Coordinate)))
		  .OrderBy(x => x.Distance)
		  .First()
		  .Node;
	}

	private static void EnsureGraphConnected(Map map, List<Node> nodes, Random random, Dictionary<string, int> connectionCounts)
	{
		var components = FindConnectedComponents(map, nodes);

		if (components.Count > 1)
		{
			Console.WriteLine($"⚠️  Found {components.Count} disconnected components, connecting them...");
			BridgeComponents(map, components, random, connectionCounts);
		}
	}

	private static List<HashSet<string>> FindConnectedComponents(Map map, List<Node> nodes)
	{
		var visited = new HashSet<string>();
		var components = new List<HashSet<string>>();

		foreach (var node in nodes)
		{
			if (!visited.Contains(node.Id))
			{
				var component = new HashSet<string>();
				DepthFirstSearch(map, node, visited, component);
				components.Add(component);
			}
		}

		return components;
	}

	private static void BridgeComponents(Map map, List<HashSet<string>> components, Random random, Dictionary<string, int> connectionCounts)
	{
		for (int i = 0; i < components.Count - 1; i++)
		{
			var (node1, node2, distance) = FindClosestNodesBetweenComponents(map, components[i], components[i + 1]);

			if (node1 != null && node2 != null)
			{
				int speedLimit = Speeds[random.Next(Speeds.Length)];
				map.AddBidirectionalConnection(node1, node2, speedLimit);
				connectionCounts[node1.Id]++;
				connectionCounts[node2.Id]++;
				Console.WriteLine($"   Connected {node1.Name} to {node2.Name} ({distance:F1} km)");
			}
		}
	}

	private static (Node?, Node?, double) FindClosestNodesBetweenComponents(Map map, HashSet<string> component1, HashSet<string> component2)
	{
		Node? bestNode1 = null;
		Node? bestNode2 = null;
		double minDistance = double.MaxValue;

		foreach (var nodeId1 in component1)
		{
			var node1 = map.GetNodeById(nodeId1);
			if (node1 == null) continue;

			foreach (var nodeId2 in component2)
			{
				var node2 = map.GetNodeById(nodeId2);
				if (node2 == null) continue;

				double distance = WorldMaths.CalculateDistance(node1.Coordinate, node2.Coordinate);
				if (distance < minDistance)
				{
					minDistance = distance;
					bestNode1 = node1;
					bestNode2 = node2;
				}
			}
		}

		return (bestNode1, bestNode2, minDistance);
	}


	private static void DepthFirstSearch(Map map, Node node, HashSet<string> visited, HashSet<string> component)
	{
		visited.Add(node.Id);
		component.Add(node.Id);

		var connections = map.GetConnections(node);
		foreach (var (destination, _, _) in connections)
		{
			if (!visited.Contains(destination.Id))
			{
				DepthFirstSearch(map, destination, visited, component);
			}
		}
	}
}
