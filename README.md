# Tellurian.Trains.Dispatch

A modern .NET library for dispatching trains between stations on a model railway.

## Why This Library?

Running a realistic train operations session on a model railway requires coordination between multiple dispatchers. This library provides:

- **Realistic dispatch workflow** modeled after prototype railway operations
- **Capacity management** for single and double track sections
- **Intermediate control points** for block signals, passing loops, and junctions
- **State machine-driven actions** ensuring only valid operations are available
- **Flexible integration** with your choice of UI, persistence, and time management

The library handles the complex logic of train dispatching, letting you focus on the user experience.

## Core Architecture

### Physical Infrastructure

**OperationPlace** is the base for all locations where trains operate:
- **Station** – Manned locations where trains stop for passengers or freight
- **SignalControlledPlace** – Block signals, passing loops, or junctions controlled by a dispatcher
- **OtherPlace** – Simple halts or unsignalled junctions

**TrackStretch** represents the physical track between two adjacent places, with one or more tracks (single-track, double-track, etc.).

### Logical Dispatch Routes

**DispatchStretch** defines the logical route between two stations. It may span multiple TrackStretches and include intermediate SignalControlledPlaces. Each DispatchStretch supports bidirectional operation through **DispatchStretchDirection**.

### Trains and Calls

- **Train** – Identified by operating company and train number (e.g., "SJ IC 123")
- **TrainStationCall** – A scheduled arrival or departure at a specific location and track
- **TrainSection** – A train's movement across a DispatchStretch, from departure call to arrival call

## State Machines

### Train States
```
Planned → Manned → Running → Completed
    ↓        ↓         ↓
Canceled  Canceled  Aborted
```

A train progresses from scheduled (Planned) through crew assignment (Manned) to active operation (Running), then either completes normally or is canceled/aborted.

### Dispatch States
```
None → Requested → Accepted → Departed → Arrived
                        ↓
                   Rejected/Revoked
```

Each TrainSection tracks its dispatch progress independently, from initial request through departure and arrival.

## Action-Based Dispatch

Dispatchers interact through explicit actions. The **ActionStateMachine** determines which actions are valid based on current state and the dispatcher's role:

| Action | Performed By | Description |
|--------|--------------|-------------|
| Request | Departure dispatcher | Request permission to dispatch |
| Accept/Reject | Arrival dispatcher | Respond to dispatch request |
| Revoke | Departure dispatcher | Cancel an accepted request |
| Depart | Departure dispatcher | Train leaves the station |
| Pass | Control point dispatcher | Confirm train passed a signal |
| Arrive | Arrival dispatcher | Train reached destination |

Each dispatcher sees only the actions they are authorized to perform.

## Capacity Management

The library enforces capacity constraints at the TrackStretch level:

- **Track availability** – Each physical track can be occupied by only one train at a time
- **Direction conflicts** – On single track, opposing movements are blocked
- **Block sections** – SignalControlledPlaces divide stretches into blocks, allowing multiple trains with safe spacing

When a train departs, it occupies the first track section. As it passes each control point, it advances to the next section, freeing the previous one for following trains.

## Integration

Implement these interfaces to integrate the library:

### IBrokerDataProvider
Supplies initial data in a specific order:
1. OperationPlaces (stations, signals, halts)
2. TrackStretches (physical infrastructure)
3. DispatchStretches (logical routes)
4. Trains
5. TrainStationCalls (the timetable)

### IBrokerStateProvider
Handles persistence of dispatch state for save/restore functionality.

### ITimeProvider
Supplies current time, typically from a fast clock for accelerated operation.

## Getting Started

```csharp
// Create the broker with your implementations
var broker = new Broker(dataProvider, stateProvider, timeProvider);
await broker.InitAsync();

// Get a station dispatcher
var dispatcher = broker.GetDispatchers()
    .First(d => d.Station.Name == "Stockholm");

// Query available actions
var departureActions = dispatcher.DepartureActions;
var arrivalActions = dispatcher.ArrivalActions;
```

The Broker is the central coordinator. Each StationDispatcher presents the arrivals and departures relevant to that station, with actions filtered to what the dispatcher can perform.

## Requirements

- .NET 10.0 or later
- Microsoft.Extensions.DependencyInjection (optional, for DI integration)

## License

GPL-3.0
