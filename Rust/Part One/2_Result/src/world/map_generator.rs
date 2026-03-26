use std::collections::{HashMap, HashSet};
use rand::Rng;
use super::{Map, Node, Coordinate, world_maths::WorldMaths};

const SPEEDS: [i32; 7] = [30, 50, 60, 80, 100, 120, 130];
const MIN_NODES: usize = 35;
const MAX_NODES: usize = 45;
const MIN_DISTANCE: f64 = 5.0;
const MAX_DISTANCE: f64 = 200.0;
const MAX_CONNECTIONS_PER_NODE: usize = 3;
const TARGET_CONNECTIONS_PER_NODE: usize = 2;

pub struct MapGenerator;

impl MapGenerator {
    pub fn generate_map() -> Map {
        let mut rng = rand::thread_rng();
        let mut map = Map::new();
        let nodes = Self::create_nodes(&mut rng);

        Self::add_nodes_to_map(&mut map, &nodes);

        let mut connection_counts: HashMap<String, usize> =
            nodes.iter().map(|n| (n.id.clone(), 0)).collect();

        Self::create_sparse_network(&mut map, &nodes, &mut rng, &mut connection_counts);
        Self::ensure_all_nodes_connected(&mut map, &nodes, &mut rng, &mut connection_counts);
        Self::ensure_graph_connected(&mut map, &nodes, &mut rng, &mut connection_counts);

        map
    }

    fn create_nodes(rng: &mut impl Rng) -> Vec<Node> {
        let node_count = rng.gen_range(MIN_NODES..MAX_NODES);
        let center_lon = 50.0 + rng.gen::<f64>() * 20.0;
        let center_lat = 50.0 + rng.gen::<f64>() * 20.0;

        (0..node_count)
            .map(|i| {
                let lon_offset = (rng.gen::<f64>() - 0.5) * 3.0;
                let lat_offset = (rng.gen::<f64>() - 0.5) * 3.0;
                Node::new(
                    format!("N{:03}", i),
                    format!("Node {}", i + 1),
                    Coordinate::new(center_lon + lon_offset, center_lat + lat_offset),
                )
            })
            .collect()
    }

    fn add_nodes_to_map(map: &mut Map, nodes: &[Node]) {
        for node in nodes {
            map.add_node(node.clone());
        }
    }

    fn create_sparse_network(
        map: &mut Map,
        nodes: &[Node],
        rng: &mut impl Rng,
        connection_counts: &mut HashMap<String, usize>,
    ) {
        for i in 0..nodes.len() {
            if *connection_counts.get(&nodes[i].id).unwrap_or(&0) >= TARGET_CONNECTIONS_PER_NODE {
                continue;
            }
            let candidates = Self::find_nearest_candidates(nodes, i, connection_counts);
            Self::connect_to_nearest_neighbors(map, &nodes[i].id, &candidates, rng, connection_counts);
        }
    }

    fn find_nearest_candidates(
        nodes: &[Node],
        current_index: usize,
        connection_counts: &HashMap<String, usize>,
    ) -> Vec<(String, f64)> {
        let current = &nodes[current_index];
        let mut candidates: Vec<(String, f64)> = nodes
            .iter()
            .enumerate()
            .filter(|(idx, n)| {
                *idx != current_index
                    && *connection_counts.get(&n.id).unwrap_or(&0) < MAX_CONNECTIONS_PER_NODE
            })
            .map(|(_, n)| {
                (
                    n.id.clone(),
                    WorldMaths::calculate_distance(&current.coordinate, &n.coordinate),
                )
            })
            .collect();

        candidates.sort_by(|a, b| a.1.partial_cmp(&b.1).unwrap());
        candidates.truncate(5);
        candidates
    }

    fn connect_to_nearest_neighbors(
        map: &mut Map,
        current_id: &str,
        candidates: &[(String, f64)],
        rng: &mut impl Rng,
        connection_counts: &mut HashMap<String, usize>,
    ) {
        let target_connections = rng.gen_range(1..MAX_CONNECTIONS_PER_NODE);
        let mut connections_added = 0;

        for (candidate_id, distance) in candidates {
            if connections_added >= target_connections {
                break;
            }
            if *distance < MIN_DISTANCE || *distance > MAX_DISTANCE {
                continue;
            }

            let current_count = *connection_counts.get(current_id).unwrap_or(&0);
            let candidate_count = *connection_counts.get(candidate_id.as_str()).unwrap_or(&0);

            if current_count >= TARGET_CONNECTIONS_PER_NODE
                || candidate_count >= MAX_CONNECTIONS_PER_NODE
            {
                continue;
            }

            let speed_limit = SPEEDS[rng.gen_range(0..SPEEDS.len())];
            map.add_bidirectional_connection(current_id, candidate_id, speed_limit);
            *connection_counts.entry(current_id.to_string()).or_insert(0) += 1;
            *connection_counts.entry(candidate_id.clone()).or_insert(0) += 1;
            connections_added += 1;
        }
    }

    fn ensure_all_nodes_connected(
        map: &mut Map,
        nodes: &[Node],
        rng: &mut impl Rng,
        connection_counts: &mut HashMap<String, usize>,
    ) {
        for i in 0..nodes.len() {
            if *connection_counts.get(&nodes[i].id).unwrap_or(&0) == 0 {
                let nearest_id = Self::find_nearest_node(nodes, i);
                let speed_limit = SPEEDS[rng.gen_range(0..SPEEDS.len())];
                map.add_bidirectional_connection(&nodes[i].id, &nearest_id, speed_limit);
                *connection_counts.entry(nodes[i].id.clone()).or_insert(0) += 1;
                *connection_counts.entry(nearest_id).or_insert(0) += 1;
            }
        }
    }

    fn find_nearest_node(nodes: &[Node], current_index: usize) -> String {
        let current = &nodes[current_index];
        nodes
            .iter()
            .enumerate()
            .filter(|(idx, _)| *idx != current_index)
            .map(|(_, n)| {
                (
                    n.id.clone(),
                    WorldMaths::calculate_distance(&current.coordinate, &n.coordinate),
                )
            })
            .min_by(|a, b| a.1.partial_cmp(&b.1).unwrap())
            .unwrap()
            .0
    }

    fn ensure_graph_connected(
        map: &mut Map,
        nodes: &[Node],
        rng: &mut impl Rng,
        connection_counts: &mut HashMap<String, usize>,
    ) {
        let components = Self::find_connected_components(map, nodes);
        if components.len() > 1 {
            println!(
                "⚠️  Found {} disconnected components, connecting them...",
                components.len()
            );
            Self::bridge_components(map, &components, rng, connection_counts);
        }
    }

    fn find_connected_components(map: &Map, nodes: &[Node]) -> Vec<HashSet<String>> {
        let mut visited: HashSet<String> = HashSet::new();
        let mut components = Vec::new();

        for node in nodes {
            if !visited.contains(&node.id) {
                let mut component = HashSet::new();
                Self::depth_first_search(map, &node.id, &mut visited, &mut component);
                components.push(component);
            }
        }

        components
    }

    fn bridge_components(
        map: &mut Map,
        components: &[HashSet<String>],
        rng: &mut impl Rng,
        connection_counts: &mut HashMap<String, usize>,
    ) {
        for i in 0..components.len() - 1 {
            if let Some((id1, id2, dist)) =
                Self::find_closest_nodes_between_components(map, &components[i], &components[i + 1])
            {
                let speed_limit = SPEEDS[rng.gen_range(0..SPEEDS.len())];
                let name1 = map
                    .get_node_by_id(&id1)
                    .map(|n| n.name.clone())
                    .unwrap_or_default();
                let name2 = map
                    .get_node_by_id(&id2)
                    .map(|n| n.name.clone())
                    .unwrap_or_default();
                println!("   Connected {} to {} ({:.1} km)", name1, name2, dist);
                map.add_bidirectional_connection(&id1, &id2, speed_limit);
                *connection_counts.entry(id1).or_insert(0) += 1;
                *connection_counts.entry(id2).or_insert(0) += 1;
            }
        }
    }

    fn find_closest_nodes_between_components(
        map: &Map,
        comp1: &HashSet<String>,
        comp2: &HashSet<String>,
    ) -> Option<(String, String, f64)> {
        let mut best: Option<(String, String, f64)> = None;

        for id1 in comp1 {
            let node1 = match map.get_node_by_id(id1) {
                None => continue,
                Some(n) => n,
            };
            for id2 in comp2 {
                let node2 = match map.get_node_by_id(id2) {
                    None => continue,
                    Some(n) => n,
                };
                let dist = WorldMaths::calculate_distance(&node1.coordinate, &node2.coordinate);
                if best.as_ref().map_or(true, |b| dist < b.2) {
                    best = Some((id1.clone(), id2.clone(), dist));
                }
            }
        }

        best
    }

    fn depth_first_search(
        map: &Map,
        node_id: &str,
        visited: &mut HashSet<String>,
        component: &mut HashSet<String>,
    ) {
        visited.insert(node_id.to_string());
        component.insert(node_id.to_string());

        for (dest_id, _, _) in map.get_connections(node_id) {
            if !visited.contains(&dest_id) {
                Self::depth_first_search(map, &dest_id, visited, component);
            }
        }
    }
}
