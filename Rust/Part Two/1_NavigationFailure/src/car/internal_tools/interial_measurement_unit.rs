use crate::world::Road;

pub struct InertialMeasurementUnit;


impl InertialMeasurementUnit {
    pub fn new() -> Self {
        Self
    }

    pub fn get_target_heading(&self, segment: &Road) -> f64{
        segment.bearing
    }
}