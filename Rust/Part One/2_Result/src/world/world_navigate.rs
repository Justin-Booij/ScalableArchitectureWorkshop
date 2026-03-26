use std::collections::{HashMap, HashSet};
use super::{Map, Node};

/// Navigation service that uses the actual world map to find the ground-truth route.
/// Uses Dijkstra's algorithm on raw distances (km).
pub struct WorldNavigate<'a> {
    world_map: &'a Map,
}

impl<'a> WorldNavigate<'a> {
    pub fn new(world_map: &'a Map) -> Self {
        Self { world_map }
    }

    /// Returns the shortest path as an ordered list of nodes, or `None` if no path exists.
    pub fn find_route(&self, start: &Node, end: &Node) -> Option<Vec<&Node>> {
        let start_id = &start.id;
        let end_id = &end.id;

        let mut distances: HashMap<String, f64> = HashMap::new();
        let mut previous: HashMap<String, String> = HashMap::new();
        let mut unvisited: HashSet<String> = HashSet::new();

        for node in self.world_map.nodes() {
            distances.insert(node.id.clone(), f64::MAX);
            unvisited.insert(node.id.clone());
        }

        *distances.get_mut(start_id)? = 0.0;

        while !unvisited.is_empty() {
            let current_id = unvisited
                .iter()
                .min_by(|a, b| distances[*a].partial_cmp(&distances[*b]).unwrap())
                .cloned();

            let current_id = match current_id {
                None => break,
                Some(id) if distances[&id] == f64::MAX => break,
                Some(id) => id,
            };

            unvisited.remove(&current_id);

            if current_id == *end_id {
                break;
            }

            for (dest_id, dist, _) in self.world_map.get_connections(&current_id) {
                let alt = distances[&current_id] + dist;
                if alt < distances[&dest_id] {
                    *distances.get_mut(&dest_id).unwrap() = alt;
                    previous.insert(dest_id, current_id.clone());
                }
            }
        }

        if distances[end_id] == f64::MAX {
            return None;
        }

        let mut route: Vec<&Node> = Vec::new();
        let mut current_id = end_id.clone();

        loop {
            let node = self.world_map.get_node_by_id(&current_id)?;
            route.insert(0, node);
            if current_id == *start_id {
                break;
            }
            match previous.get(&current_id) {
                None => break,
                Some(prev) => current_id = prev.clone(),
            }
        }

        Some(route)
    }
}
