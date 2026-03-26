use super::{Node, world_maths::WorldMaths};

/// An adjacency-list road network. Connections store the destination node ID,
/// the straight-line distance (km), and the speed limit (km/h).
pub struct Map {
    nodes: Vec<Node>,
    adjacency_list: std::collections::HashMap<String, Vec<(String, f64, i32)>>,
}

impl Map {
    pub fn new() -> Self {
        Self {
            nodes: Vec::new(),
            adjacency_list: std::collections::HashMap::new(),
        }
    }

    pub fn nodes(&self) -> &[Node] {
        &self.nodes
    }

    pub fn add_node(&mut self, node: Node) {
        if !self.nodes.iter().any(|n| n.id == node.id) {
            let id = node.id.clone();
            self.nodes.push(node);
            self.adjacency_list.insert(id, Vec::new());
        }
    }

    pub fn add_connection(&mut self, from_id: &str, to_id: &str, speed_limit: i32) {
        let (from_coord, to_coord) = {
            let from = self
                .nodes
                .iter()
                .find(|n| n.id == from_id)
                .expect("from node must be in the map");
            let to = self
                .nodes
                .iter()
                .find(|n| n.id == to_id)
                .expect("to node must be in the map");
            (from.coordinate.clone(), to.coordinate.clone())
        };

        let distance = WorldMaths::calculate_distance(&from_coord, &to_coord);
        self.adjacency_list
            .entry(from_id.to_string())
            .or_default()
            .push((to_id.to_string(), distance, speed_limit));
    }

    pub fn add_bidirectional_connection(&mut self, node1_id: &str, node2_id: &str, speed_limit: i32) {
        self.add_connection(node1_id, node2_id, speed_limit);
        self.add_connection(node2_id, node1_id, speed_limit);
    }

    /// Returns all connections from a node as `(destination_id, distance_km, speed_limit)`.
    pub fn get_connections(&self, node_id: &str) -> Vec<(String, f64, i32)> {
        self.adjacency_list
            .get(node_id)
            .cloned()
            .unwrap_or_default()
    }

    pub fn get_node_by_id(&self, id: &str) -> Option<&Node> {
        self.nodes.iter().find(|n| n.id == id)
    }
}

impl Default for Map {
    fn default() -> Self {
        Self::new()
    }
}
