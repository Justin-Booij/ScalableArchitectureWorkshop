mod world;
mod spam_elgoog;
mod car;

use std::sync::{Arc, Mutex, atomic::{AtomicBool, Ordering}};
use std::time::{Duration, Instant};

use eframe::egui::{self, Color32, Painter, Pos2, Rect, Sense, Stroke, Vec2};
use rand::Rng;

use world::{Coordinate, Map, MapGenerator, Node, WorldMaths, WorldNavigate};
use spam_elgoog::SpamElgoogNavigate;
use car::CarDriver;

fn main() -> eframe::Result<()> {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_title("Self-Driving Car Workshop")
            .with_inner_size([1200.0, 800.0]),
        ..Default::default()
    };
    eframe::run_native(
        "Self-Driving Car Workshop",
        options,
        Box::new(|_cc| Ok(Box::new(SelfDrivingApp::new()))),
    )
}

// ─── shared driving state ────────────────────────────────────────────────────

#[derive(Clone)]
struct CarState {
    position: Option<Coordinate>,
    bearing: f64,
    speed: f64,
    road_index: usize,
    is_active: bool,
}

struct DriveHandle {
    state: Arc<Mutex<CarState>>,
    stop: Arc<AtomicBool>,
}

// ─── App ─────────────────────────────────────────────────────────────────────

enum AppScreen {
    Driving,
    Crashed,
    Done,
}

struct SpeedFine {
    amount: i32,
    reason: String,
    shown_at: Instant,
}

struct SelfDrivingApp {
    map: Arc<Map>,
    // node screen positions are computed lazily per frame
    selected_start: Option<Node>,
    selected_end: Option<Node>,
    // ground-truth route from WorldNavigate (node ids)
    planned_route: Option<Vec<String>>,
    // driving
    drive: Option<DriveHandle>,
    screen: AppScreen,
    is_driving: bool,
    // off-road crash tracking
    off_road_since: Option<Instant>,
    // speed fines
    total_fines: i32,
    active_fines: Vec<SpeedFine>,
    last_fined_road: Option<usize>,
    // status bar text
    status_text: String,
}

impl SelfDrivingApp {
    const OFF_ROAD_CRASH_SECS: f64 = 1.5;
    const OFF_ROAD_TOLERANCE_KM: f64 = 0.20;
    const SPEED_VIOLATION_THRESHOLD: i32 = 5;
    const BASE_FINE_PER_KMH: i32 = 10;
    const PADDING: f32 = 50.0;
    const NODE_RADIUS: f32 = 6.0;
    const CLICK_TOLERANCE: f32 = 12.0;

    fn new() -> Self {
        let map = Arc::new(MapGenerator::generate_map());
        let node_count = map.nodes().len();
        let road_count: usize = map.nodes().iter().map(|n| map.get_connections(&n.id).len()).sum();
        Self {
            map,
            selected_start: None,
            selected_end: None,
            planned_route: None,
            drive: None,
            screen: AppScreen::Driving,
            is_driving: false,
            off_road_since: None,
            total_fines: 0,
            active_fines: Vec::new(),
            last_fined_road: None,
            status_text: format!(
                "Map ready ({} nodes, {} roads). Left-click = start, right-click = end.",
                node_count, road_count
            ),
        }
    }

    // ─── coordinate helpers ────────────────────────────────────────────────

    fn map_bounds(&self) -> (f64, f64, f64, f64) {
        let nodes = self.map.nodes();
        let mut min_lon = f64::MAX;
        let mut max_lon = f64::MIN;
        let mut min_lat = f64::MAX;
        let mut max_lat = f64::MIN;
        for n in nodes {
            min_lon = min_lon.min(n.coordinate.longitude);
            max_lon = max_lon.max(n.coordinate.longitude);
            min_lat = min_lat.min(n.coordinate.latitude);
            max_lat = max_lat.max(n.coordinate.latitude);
        }
        if (max_lon - min_lon).abs() < 1e-9 { max_lon += 1.0; }
        if (max_lat - min_lat).abs() < 1e-9 { max_lat += 1.0; }
        (min_lon, max_lon, min_lat, max_lat)
    }

    fn coord_to_screen(&self, coord: &Coordinate, rect: Rect, bounds: (f64, f64, f64, f64)) -> Pos2 {
        let (min_lon, max_lon, min_lat, max_lat) = bounds;
        let lon_range = max_lon - min_lon;
        let lat_range = max_lat - min_lat;
        let w = (rect.width() - 2.0 * Self::PADDING) as f64;
        let h = (rect.height() - 2.0 * Self::PADDING) as f64;
        let x = rect.left() + Self::PADDING + ((coord.longitude - min_lon) / lon_range * w) as f32;
        let y = rect.top() + Self::PADDING + ((max_lat - coord.latitude) / lat_range * h) as f32;
        Pos2::new(x, y)
    }

    fn node_screen_pos(&self, node_id: &str, rect: Rect, bounds: (f64, f64, f64, f64)) -> Option<Pos2> {
        self.map.get_node_by_id(node_id)
            .map(|n| self.coord_to_screen(&n.coordinate, rect, bounds))
    }

    // ─── rendering ─────────────────────────────────────────────────────────

    fn draw_grid(painter: &Painter, rect: Rect) {
        let color = Color32::from_rgb(230, 230, 230);
        let stroke = Stroke::new(1.0, color);
        let mut x = rect.left();
        while x < rect.right() {
            painter.line_segment([Pos2::new(x, rect.top()), Pos2::new(x, rect.bottom())], stroke);
            x += 50.0;
        }
        let mut y = rect.top();
        while y < rect.bottom() {
            painter.line_segment([Pos2::new(rect.left(), y), Pos2::new(rect.right(), y)], stroke);
            y += 50.0;
        }
    }

    fn draw_roads(&self, painter: &Painter, rect: Rect, bounds: (f64, f64, f64, f64)) {
        let mut drawn = std::collections::HashSet::new();
        for node in self.map.nodes() {
            for (dest_id, _dist, speed_limit) in self.map.get_connections(&node.id) {
                let key = if node.id < dest_id {
                    format!("{}-{}", node.id, dest_id)
                } else {
                    format!("{}-{}", dest_id, node.id)
                };
                if drawn.contains(&key) { continue; }
                drawn.insert(key);

                let Some(a) = self.node_screen_pos(&node.id, rect, bounds) else { continue };
                let Some(b) = self.node_screen_pos(&dest_id, rect, bounds) else { continue };

                // Shadow
                painter.line_segment([a, b], Stroke::new(10.0, Color32::from_rgb(20, 20, 20)));
                // Asphalt
                painter.line_segment([a, b], Stroke::new(6.0, Color32::from_rgb(40, 40, 40)));
                // Centre dashes
                Self::draw_dashed_line(painter, a, b, 2.5, Color32::WHITE, 8.0);

                // Speed sign at midpoint
                let mid = Pos2::new((a.x + b.x) / 2.0, (a.y + b.y) / 2.0);
                let dx = b.x - a.x;
                let dy = b.y - a.y;
                let len = (dx * dx + dy * dy).sqrt();
                if len > 0.0 {
                    let perp_x = -dy / len * 14.0;
                    let perp_y = dx / len * 14.0;
                    let sign_pos = Pos2::new(mid.x + perp_x, mid.y + perp_y);
                    painter.circle_filled(sign_pos, 8.0, Color32::WHITE);
                    painter.circle_stroke(sign_pos, 8.0, Stroke::new(1.5, Color32::from_rgb(180, 0, 0)));
                    painter.text(
                        sign_pos,
                        egui::Align2::CENTER_CENTER,
                        speed_limit.to_string(),
                        egui::FontId::proportional(7.0),
                        Color32::BLACK,
                    );
                }
            }
        }
    }

    fn draw_dashed_line(painter: &Painter, a: Pos2, b: Pos2, width: f32, color: Color32, dash_len: f32) {
        let dx = b.x - a.x;
        let dy = b.y - a.y;
        let total = (dx * dx + dy * dy).sqrt();
        if total < 1.0 { return; }
        let ux = dx / total;
        let uy = dy / total;
        let mut t = 0.0f32;
        let mut drawing = true;
        while t < total {
            let end = (t + dash_len).min(total);
            if drawing {
                let p1 = Pos2::new(a.x + ux * t, a.y + uy * t);
                let p2 = Pos2::new(a.x + ux * end, a.y + uy * end);
                painter.line_segment([p1, p2], Stroke::new(width, color));
            }
            t += dash_len;
            drawing = !drawing;
        }
    }

    fn draw_planned_route(&self, painter: &Painter, rect: Rect, bounds: (f64, f64, f64, f64)) {
        let route = match &self.planned_route {
            None => return,
            Some(r) if r.len() < 2 => return,
            Some(r) => r,
        };
        for w in route.windows(2) {
            let Some(a) = self.node_screen_pos(&w[0], rect, bounds) else { continue };
            let Some(b) = self.node_screen_pos(&w[1], rect, bounds) else { continue };
            painter.line_segment([a, b], Stroke::new(6.0, Color32::from_rgb(200, 150, 0)));
            painter.line_segment([a, b], Stroke::new(4.0, Color32::from_rgb(255, 193, 7)));
            // Arrow at midpoint
            let mid = Pos2::new((a.x + b.x) / 2.0, (a.y + b.y) / 2.0);
            let angle = (b.y - a.y).atan2(b.x - a.x);
            let size = 8.0f32;
            let offset = std::f32::consts::FRAC_PI_6;
            let tl = Pos2::new(mid.x - size * (angle - offset).cos(), mid.y - size * (angle - offset).sin());
            let tr = Pos2::new(mid.x - size * (angle + offset).cos(), mid.y - size * (angle + offset).sin());
            let arrow_stroke = Stroke::new(2.0, Color32::from_rgb(255, 193, 7));
            painter.line_segment([mid, tl], arrow_stroke);
            painter.line_segment([mid, tr], arrow_stroke);
        }
    }

    fn draw_nodes(&self, painter: &Painter, rect: Rect, bounds: (f64, f64, f64, f64)) {
        for node in self.map.nodes() {
            let pos = self.coord_to_screen(&node.coordinate, rect, bounds);
            let is_start = self.selected_start.as_ref().is_some_and(|n| n.id == node.id);
            let is_end = self.selected_end.as_ref().is_some_and(|n| n.id == node.id);

            let fill = if is_start {
                Color32::from_rgb(46, 204, 113)
            } else if is_end {
                Color32::from_rgb(231, 76, 60)
            } else {
                Color32::from_rgb(220, 53, 69)
            };

            painter.circle_filled(pos, Self::NODE_RADIUS, fill);
            painter.circle_stroke(pos, Self::NODE_RADIUS, Stroke::new(1.5, Color32::from_rgb(80, 0, 0)));

            if is_start || is_end {
                painter.text(
                    Pos2::new(pos.x, pos.y - Self::NODE_RADIUS - 10.0),
                    egui::Align2::CENTER_BOTTOM,
                    if is_start { "START" } else { "END" },
                    egui::FontId::proportional(10.0),
                    fill,
                );
            }
        }
    }

    fn draw_car(painter: &Painter, pos: Pos2, bearing: f64) {
        let radians = (bearing as f32 - 90.0).to_radians();
        let cos_r = radians.cos();
        let sin_r = radians.sin();

        let car_len = 12.0f32;
        let car_w = 6.0f32;

        let front = Pos2::new(pos.x + (car_len / 2.0) * cos_r, pos.y + (car_len / 2.0) * sin_r);
        let rear  = Pos2::new(pos.x - (car_len / 2.0) * cos_r, pos.y - (car_len / 2.0) * sin_r);

        let lx = -(car_w / 2.0) * sin_r;
        let ly =  (car_w / 2.0) * cos_r;
        let rx =  (car_w / 2.0) * sin_r;
        let ry = -(car_w / 2.0) * cos_r;

        let fl = Pos2::new(front.x + lx, front.y + ly);
        let fr = Pos2::new(front.x + rx, front.y + ry);
        let rl = Pos2::new(rear.x  + lx, rear.y  + ly);
        let rr = Pos2::new(rear.x  + rx, rear.y  + ry);

        let car_color = Color32::from_rgb(220, 53, 69);
        let outline   = Stroke::new(1.5, Color32::from_rgb(139, 0, 0));

        painter.add(egui::Shape::convex_polygon(
            vec![fl, fr, rr, rl],
            car_color,
            outline,
        ));
    }

    // ─── interaction ───────────────────────────────────────────────────────

    fn handle_click(&mut self, pos: Pos2, rect: Rect, bounds: (f64, f64, f64, f64), right: bool) {
        for node in self.map.nodes() {
            let screen = self.coord_to_screen(&node.coordinate, rect, bounds);
            let dx = pos.x - screen.x;
            let dy = pos.y - screen.y;
            if (dx * dx + dy * dy).sqrt() <= Self::CLICK_TOLERANCE {
                if right {
                    self.selected_end = Some(node.clone());
                } else {
                    self.selected_start = Some(node.clone());
                }
                self.refresh_planned_route();
                return;
            }
        }
    }

    fn refresh_planned_route(&mut self) {
        let (Some(start), Some(end)) = (&self.selected_start, &self.selected_end) else {
            self.planned_route = None;
            self.update_status();
            return;
        };
        let nav = WorldNavigate::new(&self.map);
        self.planned_route = nav
            .find_route(start, end)
            .map(|r| r.iter().map(|n| n.id.clone()).collect());
        self.update_status();
    }

    fn update_status(&mut self) {
        let start_name = self.selected_start.as_ref().map(|n| n.name.clone());
        let end_name = self.selected_end.as_ref().map(|n| n.name.clone());

        self.status_text = match (&start_name, &end_name) {
            (Some(s), Some(e)) => {
                if self.planned_route.is_some() {
                    format!("Start: {}  →  End: {}  |  Press 'Start Driving' to begin", s, e)
                } else {
                    "No route found between selected nodes.".into()
                }
            }
            (Some(s), None) => format!("Start: {}  |  Right-click to select end", s),
            (None, Some(e)) => format!("End: {}  |  Left-click to select start", e),
            _ => "Left-click = start node, right-click = end node".into(),
        };
    }

    fn start_driving(&mut self) {
        let (Some(start_node), Some(end_node)) = (self.selected_start.clone(), self.selected_end.clone()) else { return };
        if self.planned_route.is_none() || self.is_driving { return; }

        let navigate = SpamElgoogNavigate::new(Arc::clone(&self.map));

        let state = Arc::new(Mutex::new(CarState {
            position: Some(start_node.coordinate.clone()),
            bearing:  0.0,
            speed:    0.0,
            road_index: 0,
            is_active: true,
        }));
        let stop = Arc::new(AtomicBool::new(false));

        let state_clone = Arc::clone(&state);
        let stop_clone  = Arc::clone(&stop);


        std::thread::spawn(move || {
            let mut car = CarDriver::new(navigate);
            car.start_driving_with_callback(&start_node, &end_node, stop_clone, move |position, bearing, speed, road_index, is_active| {
                let mut s = state_clone.lock().unwrap();
                s.position   = Some(position);
                s.bearing    = bearing;
                s.speed      = speed;
                s.road_index = road_index;
                s.is_active  = is_active;
            });
        });

        self.drive = Some(DriveHandle { state, stop });
        self.is_driving = true;
        self.off_road_since = None;
        self.last_fined_road = None;
        self.status_text = "Driving...".into();
    }

    fn stop_driving(&mut self) {
        if let Some(h) = &self.drive {
            h.stop.store(true, Ordering::Relaxed);
        }
        self.drive = None;
        self.is_driving = false;
    }

    // ─── per-frame logic ───────────────────────────────────────────────────

    fn tick(&mut self, ctx: &egui::Context) {
        let car_state = self.drive.as_ref().map(|h| h.state.lock().unwrap().clone());

        if let Some(cs) = &car_state {
            if !cs.is_active {
                self.screen = AppScreen::Done;
                self.is_driving = false;
                self.status_text = "Drive completed! Select new nodes to drive again.".into();
                return;
            }

            // Off-road crash check
            if let Some(pos) = &cs.position {
                let on_road = self.is_on_road(pos, cs.road_index, cs.bearing);
                if !on_road {
                    if self.off_road_since.is_none() {
                        self.off_road_since = Some(Instant::now());
                    } else if self.off_road_since.unwrap().elapsed().as_secs_f64()
                        >= Self::OFF_ROAD_CRASH_SECS
                    {
                        self.stop_driving();
                        self.screen = AppScreen::Crashed;
                        return;
                    }
                } else {
                    self.off_road_since = None;
                    self.check_speed_violation(cs.road_index, cs.speed);
                }
            }

            // Expire old fine popups (3 s)
            self.active_fines.retain(|f| f.shown_at.elapsed() < Duration::from_secs(3));

            ctx.request_repaint_after(Duration::from_millis(50));
        }
    }

    fn is_on_road(&self, pos: &Coordinate, road_index: usize, bearing: f64) -> bool {
        let route = match &self.planned_route {
            None => return true,
            Some(r) => r,
        };
        if route.len() < 2 || road_index + 1 >= route.len() {
            return true;
        }
        let from = match self.map.get_node_by_id(&route[road_index]) {
            None => return true,
            Some(n) => n,
        };
        let to = match self.map.get_node_by_id(&route[road_index + 1]) {
            None => return true,
            Some(n) => n,
        };

        let on_road = WorldMaths::is_point_on_road(pos, &from.coordinate, &to.coordinate, Self::OFF_ROAD_TOLERANCE_KM);
        if !on_road { return false; }

        let expected = WorldMaths::calculate_bearing(&from.coordinate, &to.coordinate);
        let diff = (expected - bearing).abs();
        let diff = if diff > 180.0 { 360.0 - diff } else { diff };
        diff <= 45.0
    }

    fn check_speed_violation(&mut self, road_index: usize, car_speed: f64) {
        if self.last_fined_road == Some(road_index) { return; }
        let route = match &self.planned_route {
            None => return,
            Some(r) => r,
        };
        if road_index + 1 >= route.len() { return; }
        let from_id = &route[road_index];
        let to_id   = &route[road_index + 1];

        // World speed limit for this segment (km/h)
        let world_limit = self.map.get_connections(from_id)
            .into_iter()
            .find(|(dest, _, _)| dest == to_id)
            .map(|(_, _, sl)| sl);

        let Some(world_limit) = world_limit else {
            self.last_fined_road = Some(road_index);
            return;
        };

        // Car speed is in km/h — compare directly against the km/h world limit
        let actual_speed = car_speed as i32;

        let diff = (world_limit - actual_speed).abs();
        if diff >= Self::SPEED_VIOLATION_THRESHOLD {
            let fine = (diff * Self::BASE_FINE_PER_KMH).max(25);
            self.total_fines += fine;
            let reason = if actual_speed > world_limit { "Speeding" } else { "Too Slow" };
            self.active_fines.push(SpeedFine {
                amount: fine,
                reason: format!("{} ({} vs {} km/h limit)", reason, actual_speed, world_limit),
                shown_at: Instant::now(),
            });
        }

        self.last_fined_road = Some(road_index);
    }
}

impl eframe::App for SelfDrivingApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        self.tick(ctx);

        match self.screen {
            AppScreen::Crashed => {
                egui::CentralPanel::default().show(ctx, |ui| {
                    ui.vertical_centered(|ui| {
                        ui.add_space(80.0);
                        ui.heading(egui::RichText::new("💥 YOU CRASHED! 💥").size(32.0).color(Color32::from_rgb(220, 53, 69)));
                        ui.add_space(12.0);
                        ui.label("Your car went off the road and crashed!");
                        ui.add_space(24.0);
                        ui.horizontal(|ui| {
                            if ui.button(egui::RichText::new("Close").size(15.0)).clicked() {
                                self.screen = AppScreen::Driving;
                                self.status_text = "Select start (left-click) and end (right-click) nodes".into();
                            }
                            if ui.button(egui::RichText::new("Exit").size(15.0).color(Color32::WHITE)).clicked() {
                                std::process::exit(0);
                            }
                        });
                    });
                });
                return;
            }
            AppScreen::Done | AppScreen::Driving => {}
        }

        // ── top panel ────────────────────────────────────────────────────────
        egui::TopBottomPanel::top("header").show(ctx, |ui| {
            ui.add_space(8.0);
            ui.vertical_centered(|ui| {
                ui.heading(egui::RichText::new("Self-Driving Car Workshop").size(22.0).color(Color32::WHITE));
                ui.label(egui::RichText::new("World Map Visualization").color(Color32::from_rgb(189, 195, 199)));
            });
            ui.add_space(8.0);
        });

        // ── bottom panel ─────────────────────────────────────────────────────
        egui::TopBottomPanel::bottom("footer").show(ctx, |ui| {
            ui.horizontal(|ui| {
                let node_count = self.map.nodes().len();
                let road_count: usize = self.map.nodes().iter()
                    .map(|n| self.map.get_connections(&n.id).len())
                    .sum();
                ui.label(format!("Nodes: {}  |  Roads: {}", node_count, road_count));
                ui.separator();
                ui.label(&self.status_text);
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let can_start = self.selected_start.is_some()
                        && self.selected_end.is_some()
                        && self.planned_route.is_some()
                        && !self.is_driving;
                    ui.add_enabled_ui(can_start, |ui| {
                        if ui.button(egui::RichText::new("Start Driving").color(Color32::WHITE)).clicked() {
                            self.start_driving();
                        }
                    });
                    ui.separator();
                    ui.label(format!("Total fines: ${}", self.total_fines));
                });
            });
        });

        // ── map canvas ───────────────────────────────────────────────────────
        egui::CentralPanel::default().show(ctx, |ui| {
            let (response, painter) = ui.allocate_painter(ui.available_size(), Sense::click());
            let rect = response.rect;
            let bounds = self.map_bounds();

            Self::draw_grid(&painter, rect);
            self.draw_roads(&painter, rect, bounds);
            self.draw_planned_route(&painter, rect, bounds);
            self.draw_nodes(&painter, rect, bounds);

            // Draw car
            if self.is_driving {
                if let Some(h) = &self.drive {
                    let cs = h.state.lock().unwrap();
                    if let Some(pos) = &cs.position {
                        let screen = self.coord_to_screen(pos, rect, bounds);
                        Self::draw_car(&painter, screen, cs.bearing);
                    }
                }
            }

            // Speed fine popups
            for fine in &self.active_fines {
                let msg = format!("⚠ SPEED VIOLATION  ${} — {}  |  Total: ${}", fine.amount, fine.reason, self.total_fines);
                let fine_rect = Rect::from_min_size(
                    Pos2::new(rect.center().x - 220.0, rect.top() + 10.0),
                    Vec2::new(440.0, 40.0),
                );
                painter.rect_filled(fine_rect, 6.0, Color32::from_rgb(255, 107, 107));
                painter.rect_stroke(fine_rect, 6.0, Stroke::new(2.0, Color32::from_rgb(204, 0, 0)), egui::StrokeKind::Middle);
                painter.text(
                    fine_rect.center(),
                    egui::Align2::CENTER_CENTER,
                    &msg,
                    egui::FontId::proportional(11.0),
                    Color32::WHITE,
                );
            }

            // Click handling
            if response.clicked() {
                if let Some(pos) = response.interact_pointer_pos() {
                    self.handle_click(pos, rect, bounds, false);
                }
            }
            if response.secondary_clicked() {
                if let Some(pos) = response.interact_pointer_pos() {
                    self.handle_click(pos, rect, bounds, true);
                }
            }
        });
    }
}
