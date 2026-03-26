use super::Coordinate;

const EARTH_RADIUS_KM: f64 = 6371.0;

pub struct WorldMaths;

impl WorldMaths {
    /// Calculates the distance between two coordinates using the Haversine formula.
    /// Returns distance in kilometers.
    pub fn calculate_distance(a: &Coordinate, b: &Coordinate) -> f64 {
        let d_lat = Self::degrees_to_radians(b.latitude - a.latitude);
        let d_lon = Self::degrees_to_radians(b.longitude - a.longitude);

        let haversine = (d_lat / 2.0).sin().powi(2)
            + Self::degrees_to_radians(a.latitude).cos()
                * Self::degrees_to_radians(b.latitude).cos()
                * (d_lon / 2.0).sin().powi(2);

        let central_angle = 2.0 * haversine.sqrt().atan2((1.0 - haversine).sqrt());
        EARTH_RADIUS_KM * central_angle
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

    /// Calculates a destination point given a starting point, bearing, and distance.
    pub fn calculate_destination_point(start: &Coordinate, bearing: f64, distance_km: f64) -> Coordinate {
        let lat_rad = Self::degrees_to_radians(start.latitude);
        let lon_rad = Self::degrees_to_radians(start.longitude);
        let bearing_rad = Self::degrees_to_radians(bearing);
        let angular_distance = distance_km / EARTH_RADIUS_KM;

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

    /// Checks if a point is on a road segment within a tolerance distance.
    pub fn is_point_on_road(
        point: &Coordinate,
        road_start: &Coordinate,
        road_end: &Coordinate,
        tolerance_km: f64,
    ) -> bool {
        let dist_to_start = Self::calculate_distance(point, road_start);
        let dist_to_end = Self::calculate_distance(point, road_end);
        let road_length = Self::calculate_distance(road_start, road_end);

        if dist_to_start + dist_to_end > road_length + 2.0 * tolerance_km {
            return false;
        }

        Self::calculate_perpendicular_distance(point, road_start, road_end) <= tolerance_km
    }

    fn calculate_perpendicular_distance(
        point: &Coordinate,
        line_start: &Coordinate,
        line_end: &Coordinate,
    ) -> f64 {
        let lat1 = Self::degrees_to_radians(line_start.latitude);
        let lon1 = Self::degrees_to_radians(line_start.longitude);
        let lat2 = Self::degrees_to_radians(line_end.latitude);
        let lon2 = Self::degrees_to_radians(line_end.longitude);
        let lat_p = Self::degrees_to_radians(point.latitude);
        let lon_p = Self::degrees_to_radians(point.longitude);

        let d_lon = lon2 - lon1;
        let y = d_lon.sin() * lat2.cos();
        let x = lat1.cos() * lat2.sin() - lat1.sin() * lat2.cos() * d_lon.cos();
        let bearing13 = y.atan2(x);

        let d_lon13 = lon_p - lon1;
        let distance13 =
            (lat1.sin() * lat_p.sin() + lat1.cos() * lat_p.cos() * d_lon13.cos()).acos();

        let cross_track =
            (distance13.sin() * (bearing13 - y.atan2(x)).sin()).asin() * EARTH_RADIUS_KM;
        cross_track.abs()
    }

    pub fn degrees_to_radians(degrees: f64) -> f64 {
        degrees * std::f64::consts::PI / 180.0
    }

    pub fn radians_to_degrees(radians: f64) -> f64 {
        radians * 180.0 / std::f64::consts::PI
    }
}
