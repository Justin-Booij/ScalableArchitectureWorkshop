use crate::spam_elgoog::SpamElgoogNavigate;
use crate::world::{Node, Road};



pub struct NavigationAdapter {
     adaptee: SpamElgoogNavigate,
    current_route: Vec<Road>

}

const MILES_TO_KM_CONVERSION_RATE: f64 = 1.609344;

impl NavigationAdapter {
    pub fn new(adaptee: SpamElgoogNavigate) -> Self {
        Self { adaptee, current_route: Vec::new()}
    }

    pub fn navigate(&mut self, start: &str, end: &str) -> Option<Vec<Road>> {


        let maybe_route = self.adaptee.navigate(start, end);

        if let Some(route) = maybe_route {
            for road in route {
                let mut new_road = road.clone();
                new_road.distance *= MILES_TO_KM_CONVERSION_RATE;
                let speed_limit = (new_road.speed_limit as f64) * MILES_TO_KM_CONVERSION_RATE;
                new_road.speed_limit = speed_limit as i32;

                self.current_route.push(new_road)
            }

            return Some(self.current_route.clone())
        }

        None
    }

    pub fn get_speed_correction(&self, road_index: usize, current_speed: f64) -> f64{
        if self.current_route.len() == 0 {
            return 0.0;
        }

        self.current_route[road_index].speed_limit as f64 - current_speed
    }

    pub fn get_bearing_correction(&self, road_index: usize, current_bearing: f64) -> f64 {
        if self.current_route.len() == 0 {
            return 0.0;
        }

        self.adaptee.get_bearing_correction(road_index, current_bearing)
    }

    pub fn get_distance(&self, road_index: usize) -> f64 {
        if self.current_route.len() == 0 {
            return 0.0;
        }

        self.current_route[road_index].distance
    }
}