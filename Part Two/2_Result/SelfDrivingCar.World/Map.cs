namespace SelfDrivingCar.World;

public class Map
{
	private List<Node> _nodes;
	private Dictionary<string, List<(Node destination, double distance, int speedLimit)>> _adjacencyList;

	public IReadOnlyList<Node> Nodes => _nodes.AsReadOnly();

	public Map()
	{
		_nodes = new List<Node>();
		_adjacencyList = new Dictionary<string, List<(Node, double, int)>>();
	}

	public void AddNode(Node node)
	{
		if (!_nodes.Contains(node))
		{
			_nodes.Add(node);
			_adjacencyList[node.Id] = new List<(Node, double, int)>();
		}
	}

	public void AddConnection(Node from, Node to, int speedLimit)
	{
		if (!_nodes.Contains(from) || !_nodes.Contains(to))
		{
			throw new ArgumentException("Both nodes must be added to the map before creating a connection.");
		}

		double distance = WorldMaths.CalculateDistance(from.Coordinate, to.Coordinate);
		_adjacencyList[from.Id].Add((to, distance, speedLimit));
	}

	public void AddBidirectionalConnection(Node node1, Node node2, int speedLimit)
	{
		AddConnection(node1, node2, speedLimit);
		AddConnection(node2, node1, speedLimit);
	}

	public List<(Node destination, double distance, int speedLimit)> GetConnections(Node node)
	{
		if (_adjacencyList.TryGetValue(node.Id, out var connections))
		{
			return connections;
		}
		return new List<(Node, double, int)>();
	}

	public Node? GetNodeById(string id)
	{
		return _nodes.FirstOrDefault(n => n.Id == id);
	}
}
