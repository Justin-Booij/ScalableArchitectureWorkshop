use std::collections::{HashMap, HashSet};
use std::sync::Arc;
use crate::world::{Map, Road};
use super::GeoMath;

/// Navigation system that routes using GeoMath (distances in miles, speed limits converted to mph).
/// Implements Dijkstra's algorithm on the world map.
pub struct MotMotNavigate {
    world_map: Arc<Map>,
    current_route: Vec<Road>,
}

impl MotMotNavigate {
    pub fn new(world_map: Arc<Map>) -> Self {
        Self {
            world_map,
            current_route: Vec::new(),
        }
    }

    /// Finds a route from `start_id` to `end_id`, stores it internally, and returns it.
    /// Returns `None` if no route exists.
    pub fn navigate(&mut self, start_id: &str, end_id: &str) -> Option<Vec<Road>> {
        let start_name = self.world_map.get_node_by_id(start_id)?.name.clone();
        let end_name = self.world_map.get_node_by_id(end_id)?.name.clone();

        println!("Finding route from {} to {}", start_name, end_name);

        let mut distances: HashMap<String, f64> = HashMap::new();
        let mut previous: HashMap<String, (String, i32)> = HashMap::new();
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

            if current_id == end_id {
                let path = self.reconstruct_path(&previous, start_id, end_id);
                self.current_route = path.clone();
                return Some(path);
            }

            for (dest_id, dist, speed_limit) in self.world_map.get_connections(&current_id) {
                if !unvisited.contains(&dest_id) {
                    continue;
                }
                let alt = distances[&current_id] + dist;
                if alt < distances[&dest_id] {
                    *distances.get_mut(&dest_id).unwrap() = alt;
                    previous.insert(dest_id, (current_id.clone(), speed_limit));
                }
            }
        }

        println!("No route found from {} to {}", start_name, end_name);
        None
    }

    pub fn get_speed_correction(&self, road_index: usize, current_speed: f64) -> f64 {
        self.current_route[road_index].speed_limit as f64 - current_speed
    }

    pub fn get_bearing_correction(&self, road_index: usize, current_bearing: f64) -> f64 {
        self.current_route[road_index].bearing - current_bearing
    }

    pub fn get_distance(&self, road_index: usize) -> f64 {
        self.current_route[road_index].distance
    }

    pub fn world_map(&self) -> &Arc<Map> {
        &self.world_map
    }

    fn reconstruct_path(
        &self,
        previous: &HashMap<String, (String, i32)>,
        start_id: &str,
        end_id: &str,
    ) -> Vec<Road> {
        let mut path = Vec::new();
        let mut current_id = end_id.to_string();

        loop {
            if current_id == start_id {
                break;
            }

            let (prev_id, speed_limit) = match previous.get(&current_id) {
                None => return Vec::new(),
                Some(p) => p.clone(),
            };

            let prev_node = self.world_map.get_node_by_id(&prev_id).unwrap();
            let current_node = self.world_map.get_node_by_id(&current_id).unwrap();

            path.insert(
                0,
                Road {
                    distance: GeoMath::calculate_distance(
                        &prev_node.coordinate,
                        &current_node.coordinate,
                    ),
                    bearing: GeoMath::calculate_bearing(
                        &prev_node.coordinate,
                        &current_node.coordinate,
                    ),
                    speed_limit: speed_limit,
                },
            );

            current_id = prev_id;
        }

        path
    }
}
