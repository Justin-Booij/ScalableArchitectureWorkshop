use crate::world::Road;

pub struct SignReader;

impl SignReader {
    pub fn new() -> Self {
        Self
    }

    pub fn get_speed_for_current_road(&self, segment: &Road) -> i32 {
        segment.speed_limit
    }
}