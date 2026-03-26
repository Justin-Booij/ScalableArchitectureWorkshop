use crate::world::Coordinate;

const EARTH_RADIUS_MILES: f64 = 3958.8;

pub struct GeoMath;

impl GeoMath {
    /// Calculates the distance between two coordinates using the Haversine formula.
    /// Returns distance in miles.
    pub fn calculate_distance(a: &Coordinate, b: &Coordinate) -> f64 {
        let d_lat = Self::degrees_to_radians(b.latitude - a.latitude);
        let d_lon = Self::degrees_to_radians(b.longitude - a.longitude);

        let haversine = (d_lat / 2.0).sin().powi(2)
            + Self::degrees_to_radians(a.latitude).cos()
                * Self::degrees_to_radians(b.latitude).cos()
                * (d_lon / 2.0).sin().powi(2);

        let central_angle = 2.0 * haversine.sqrt().atan2((1.0 - haversine).sqrt());
        EARTH_RADIUS_MILES * central_angle
    }

    /// Calculates the bearing (direction) from one coordinate to another.
    /// Returns bearing in degrees (0-360).
    pub fn calculate_bearing(a: &Coordinate, b: &Coordinate) -> f64 {
        let d_lon = Self::degrees_to_radians(b.longitude - a.longitude);
        let lat1_rad = Self::degrees_to_radians(a.latitude);
        let lat2_rad = Self::degrees_to_radians(b.latitude);

        let y = d_lon.sin() * lat2_rad.cos();
        let x = lat1_rad.cos() * lat2_rad.sin() - lat1_rad.sin() * lat2_rad.cos() * d_lon.cos();

        let bearing_degrees = Self::radians_to_degrees(y.atan2(x));
        (bearing_degrees + 360.0) % 360.0
    }

    /// Calculates a destination point given a starting point, bearing, and distance in miles.
    pub fn calculate_destination_point(
        start: &Coordinate,
        bearing: f64,
        distance_miles: f64,
    ) -> Coordinate {
        let lat_rad = Self::degrees_to_radians(start.latitude);
        let lon_rad = Self::degrees_to_radians(start.longitude);
        let bearing_rad = Self::degrees_to_radians(bearing);
        let angular_distance = distance_miles / EARTH_RADIUS_MILES;

        let dest_lat_rad = (lat_rad.sin() * angular_distance.cos()
            + lat_rad.cos() * angular_distance.sin() * bearing_rad.cos())
        .asin();

        let dest_lon_rad = lon_rad
            + (bearing_rad.sin() * angular_distance.sin() * lat_rad.cos()).atan2(
                angular_distance.cos() - lat_rad.sin() * dest_lat_rad.sin(),
            );

        Coordinate::new(
            Self::radians_to_degrees(dest_lon_rad),
            Self::radians_to_degrees(dest_lat_rad),
        )
    }

    fn degrees_to_radians(degrees: f64) -> f64 {
        degrees * std::f64::consts::PI / 180.0
    }

    fn radians_to_degrees(radians: f64) -> f64 {
        radians * 180.0 / std::f64::consts::PI
    }
}
