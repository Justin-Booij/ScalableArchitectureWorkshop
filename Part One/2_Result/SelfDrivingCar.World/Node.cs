namespace SelfDrivingCar.World;

public class Node
{
	public string Id { get; set; }
	public string Name { get; set; }
	public Coordinate Coordinate { get; set; }

	public Node(string id, string name, Coordinate coordinate)
	{
		Id = id;
		Name = name;
		Coordinate = coordinate;
	}

	public override string ToString()
	{
		return $"{Name} ({Id})";
	}
}
