use crate::car::navigation_adapter::NavigationAdapter;
use crate::spam_elgoog::SpamElgoogNavigate;
use crate::world::{Coordinate, Node, Road, WorldMaths};
use rand::Rng;
use std::sync::{
    atomic::{AtomicBool, Ordering},
    Arc,
};
use crate::car::drift_correction_facade::DriftCorrectionFacade;
use crate::car::internal_tools::inertial_measurement_unit::InertialMeasurementUnit;
use crate::car::internal_tools::sign_reader::SignReader;

pub struct CarDriver {
    pub current_position: Option<Coordinate>,
    pub is_active: bool,
    pub current_speed: f64,
    pub current_bearing: f64,
    pub current_road_index: usize,
    pub current_route: Option<Vec<Road>>,
    navigation: NavigationAdapter,
    rng: rand::rngs::ThreadRng,
    drift_correction_facade: DriftCorrectionFacade
}

impl CarDriver {
    pub fn new(navigation: SpamElgoogNavigate) -> Self {
        Self {
            current_position: None,
            is_active: false,
            current_speed: 0.0,
            current_bearing: 0.0,
            current_road_index: 0,
            current_route: None,
            navigation: NavigationAdapter::new(navigation),
            rng: rand::thread_rng(),
            drift_correction_facade: DriftCorrectionFacade::new(InertialMeasurementUnit::new(), SignReader::new())
        }
    }

    pub fn calculate_route(&mut self, start_id: &str, dest_id: &str) -> Option<Vec<Road>> {
        self.navigation.navigate(start_id, dest_id)
    }

    pub fn update_route(&mut self, route: Vec<Road>) {
        self.current_road_index = 0;
        self.current_route = Some(route);
    }

    /// Drives the car, calling `on_update` after every simulated step.
    /// The callback receives (position, bearing, speed, road_index, is_active).
    pub fn start_driving_with_callback<F>(
        &mut self,
        start: &Node,
        dest: &Node,
        stop_flag: Arc<AtomicBool>,
        mut on_update: F,
    ) where
        F: FnMut(Coordinate, f64, f64, usize, bool),
    {
        self.current_position = Some(start.coordinate.clone());

        self.is_active = true;

        if let Some(route) = self.navigation.navigate(&start.id, &dest.id) {
            let route_len = route.len();
            self.update_route(route);

            for _ in 0..route_len {
                if !self.travel_along_road(&stop_flag, &mut on_update) {
                    break;
                }
                self.current_road_index += 1;
            }
        }

        self.is_active = false;
        if let Some(pos) = self.current_position.clone() {
            on_update(
                pos,
                self.current_bearing,
                self.current_speed,
                self.current_road_index,
                false,
            );
        }
    }

    fn travel_along_road<F>(&mut self, stop_flag: &Arc<AtomicBool>, on_update: &mut F) -> bool
    where
        F: FnMut(Coordinate, f64, f64, usize, bool),
    {
        let mut traveled_distance = 0.0;

        self.current_speed += self
            .navigation
            .get_speed_correction(self.current_road_index, self.current_speed);
        self.current_bearing += self
            .navigation
            .get_bearing_correction(self.current_road_index, self.current_bearing);

        let distance = self.navigation.get_distance(self.current_road_index);
        let mut is_navigation_available = true;

        while traveled_distance < distance {
            if stop_flag.load(Ordering::Relaxed) {
                self.is_active = false;
                return false;
            }

            if is_navigation_available {

                self.correct_drift();
                is_navigation_available = self.check_navigation_available();
            } else {

                self.correct_drift_fallback();
                is_navigation_available = !self.check_navigation_available();
            }

            const SPEED_SCALE_FACTOR: f64 = 2400.0;
            let mut step = (self.current_speed * SPEED_SCALE_FACTOR) / 72000.0;
            let remaining = distance - traveled_distance;
            if step > remaining {
                step = remaining;
            }

            if let Some(pos) = self.current_position.clone() {
                let new_pos =
                    WorldMaths::calculate_destination_point(&pos, self.current_bearing, step);
                self.current_position = Some(new_pos.clone());
                on_update(
                    new_pos,
                    self.current_bearing,
                    self.current_speed,
                    self.current_road_index,
                    true,
                );
            }

            traveled_distance += step;
            self.drift_bearing(0.05);
            self.drift_speed(0.075);

            std::thread::sleep(std::time::Duration::from_millis(50));
        }

        true
    }
    fn correct_drift(&mut self) {
        self.current_speed += self
            .navigation
            .get_speed_correction(self.current_road_index, self.current_speed);
        self.current_bearing += self
            .navigation
            .get_bearing_correction(self.current_road_index, self.current_bearing);
    }

    fn correct_drift_fallback(&mut self) {
        if let Some(current_route) = self.current_route.clone() {
            let road = &current_route[self.current_road_index];
            let corrections = self.drift_correction_facade.get_drift_corrections(road, self.current_speed, self.current_bearing);
            self.current_speed += corrections.speed_correction;
            self.current_bearing += corrections.bearing_correction;
        }
    }

    fn drift_bearing(&mut self, pct: f64) {
        self.current_bearing *= 1.0 + self.drift_factor(pct);
    }

    fn drift_speed(&mut self, pct: f64) {
        self.current_speed *= 1.0 + self.drift_factor(pct);
    }

    fn drift_factor(&mut self, pct: f64) -> f64 {
        self.rng.gen::<f64>() * (pct * 2.0) - pct
    }

    fn check_navigation_available(&mut self) -> bool {
        let chance = self.rng.gen_range(1..6);
        if chance == 5 {
            return false;
        }

        true
    }
}
