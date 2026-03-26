#[derive(Debug, Clone)]
pub struct Coordinate {
    pub longitude: f64,
    pub latitude: f64,
}

impl Coordinate {
    pub fn new(lon: f64, lat: f64) -> Self {
        Self {
            longitude: lon,
            latitude: lat,
        }
    }
}
