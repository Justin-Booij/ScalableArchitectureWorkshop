# Self-Driving Car Design Pattern Playground

A lightweight .NET 8 console application you can use during workshops to explore software design patterns in the context of a self-driving car. The playground replays mocked GPS data, feeds it through a navigation strategy, translates waypoints into commands, and publishes telemetry to the console so you can experiment with patterns such as Strategy, Command, Mediator, Observer, and more.

## Project layout

```
ScalableArchitectureWorkshop/
├── ScalableArchitectureWorkshop.sln
├── README.md
└── src/
    ├── SelfDrivingCar.Gps/          # Standalone GPS simulation library
    └── SelfDrivingCar.Playground/
        ├── Commands/                # Command pattern primitives
        ├── Domain/                  # Vehicle aggregate state
        ├── Navigation/              # Route planner strategies
        ├── Simulation/              # Orchestrator wiring everything together
        └── Telemetry/               # Console telemetry sink (Observer pattern hook)
```

## GPS simulation toolkit

The `SelfDrivingCar.Gps` class library exposes `IGpsDataFeed`, `GpsSnapshot`, and two configurable simulators:

- `ProceduralGpsSimulator` – endlessly loops through randomized city-style blocks with gentle + sharp turns. Handy for showing behavior over time.
- `RouteFollowingGpsSimulator` – the default in `Program.cs`. It accepts an origin/destination pair, then stitches together randomized "roads" (each with their own heading and speed limit) while still making forward progress toward the target. Speed limits and headings change after a few samples to mimic real routing decisions.

Every `GpsSnapshot` contains:

- Current location (latitude/longitude)
- Heading the car is currently facing
- Desired heading (where it should be pointing as upcoming turns appear)
- Legal speed limit for the current road segment
- Timestamp so you can derive velocity/acceleration on your own

```csharp
using SelfDrivingCar.Gps;

var options = new RouteFollowingGpsOptions
{
    Origin = new GpsCoordinate(37.774868, -122.419519),
    Destination = new GpsCoordinate(37.793015, -122.392996),
    MinSpeedLimitKph = 30,
    MaxSpeedLimitKph = 60
};

IGpsDataFeed gpsFeed = new RouteFollowingGpsSimulator(options, seed: 2025);
await foreach (var snapshot in gpsFeed.StreamAsync())
{
    Console.WriteLine(snapshot);
}
```

## How to run

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download) if you don't have it yet.
2. Restore and run the simulation (the optional numeric argument limits how many GPS samples are processed):

```bash
dotnet run --project src/SelfDrivingCar.Playground -- --origin=37.774868,-122.419519 --destination=37.793015,-122.392996 --samples=80
```

- `--origin` / `--destination` accept `lat,lon` pairs (defaults cover downtown San Francisco to the Embarcadero if omitted).
- `--samples` limits how many GPS readings flow through the pipeline (defaults to 60).

You should see timestamped GPS snapshots with the current heading, desired heading, speed limit, and smoothed speed printed to the console as the simulated vehicle follows varied routes to its destination. Press `Ctrl+C` to stop early.

## Customize the workshop

- Tweak `RouteFollowingGpsOptions` (origin, destination, deviation, speed limits) or swap in another `IGpsDataFeed` implementation to illustrate different environments.
- Implement additional planners in `Navigation/` (e.g., a `StatePatternRoutePlanner` or `BehaviorTreePlanner`) to demonstrate alternative strategies.
- Introduce new command types (lane changes, stop-at-signals) plus handlers registered inside `Program.cs` to walk through the Command or Mediator patterns.
- Replace `ConsoleTelemetrySink` with a pub/sub based implementation to explore Observer or Event Sourcing ideas.

## Next ideas

- Add unit tests that lock down specific navigation behaviors.
- Record simulation output to JSON for later visualization.
- Layer in more sensors (Lidar, IMU) behind interfaces similar to `IGpsProvider` to show how to scale the architecture.
