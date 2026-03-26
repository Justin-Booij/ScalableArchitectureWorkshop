use super::Coordinate;

#[derive(Debug, Clone)]
pub struct Node {
    pub id: String,
    pub name: String,
    pub coordinate: Coordinate,
}

impl Node {
    pub fn new(id: impl Into<String>, name: impl Into<String>, coordinate: Coordinate) -> Self {
        Self {
            id: id.into(),
            name: name.into(),
            coordinate,
        }
    }
}

impl std::fmt::Display for Node {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        write!(f, "{} ({})", self.name, self.id)
    }
}
