use crate::car::internal_tools::inertial_measurement_unit::InertialMeasurementUnit;
use crate::car::internal_tools::sign_reader::SignReader;
use crate::world::Road;

pub struct DriftCorrectionFacade {
    imu: InertialMeasurementUnit,
    sign_reader: SignReader,
}

pub struct Corrections {
    pub speed_correction: f64,
    pub bearing_correction: f64
}



impl DriftCorrectionFacade {
    pub fn new(imu: InertialMeasurementUnit, sign_reader: SignReader) -> Self {
        Self {imu, sign_reader}
    }

    pub fn get_drift_corrections(&self, road: &Road, current_speed: f64, current_bearing: f64) -> Corrections {
        let speed_limit = self.sign_reader.get_speed_for_current_road(road) as f64;
        let target_heading = self.imu.get_target_heading(road);

        Corrections::new(speed_limit - current_speed, target_heading - current_bearing)
    }
}

impl Corrections {
    pub fn new(speed_correction: f64, bearing_correction: f64) -> Self {
        Self {speed_correction, bearing_correction}
    }
}