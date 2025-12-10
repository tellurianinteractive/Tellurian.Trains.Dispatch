# Tellurian.Trains.Dispatch.Data

This package provides data provider implementations for the Tellurian.Trains.Dispatch library.

## Overview

The dispatch system uses two types of providers:

1. **Broker Data Provider** (`IBrokerDataProvider`) - Loads the static configuration
2. **State Provider** (`ICompositeStateProvider`) - Records and restores runtime state changes

Understanding the distinction between these providers is essential for implementing your own.

## Broker Data Provider vs State Provider

### Broker Data Provider

The `IBrokerDataProvider` loads the **initial configuration** that defines the railway layout and train schedules:

- **Operation Places** - Stations, signal-controlled places, and other locations
- **Track Stretches** - Physical track segments between places
- **Dispatch Stretches** - Logical dispatch sections between stations
- **Trains** - Train definitions with identity and properties
- **Train Station Calls** - Scheduled stops with arrival/departure times

This data is typically **read-only** and comes from:
- JSON configuration files
- A database with timetable data
- An external scheduling system

The configuration defines *what should happen* according to the timetable.

### State Provider

The `ICompositeStateProvider` records **runtime state changes** that occur during a dispatch session:

- **Train State Changes** - When trains transition between states (Planned → Manned → Running → Completed)
- **Dispatch State Changes** - Request, Accept, Depart, Arrive actions on train sections
- **Observed Times** - Actual arrival/departure times recorded by dispatchers
- **Track Changes** - When trains are assigned different tracks than planned
- **Pass Events** - When trains pass signal-controlled places

This data represents *what actually happened* during operations.

### Why Two Separate Providers?

| Aspect | Data Provider | State Provider |
|--------|--------------|----------------|
| **Data** | Static configuration | Dynamic state changes |
| **Lifetime** | Persistent across sessions | Per-session (clearable) |
| **Source** | Timetable/schedule system | Dispatch operations |
| **Pattern** | Load once at startup | Event sourcing (append-only) |

On restart, the broker:
1. Loads fresh configuration from the data provider
2. Applies saved state changes from the state provider

This separation allows the same timetable to be used across multiple sessions while preserving operational state.

## Implementing a Custom State Provider

### Interface Structure

The state provider system consists of:

```
ICompositeStateProvider
├── ITrainStateProvider      (train states, observed times, track changes)
└── IDispatchStateProvider   (dispatch states, pass events)
```

Both individual providers implement `IStateProvider`:

```csharp
public interface IStateProvider
{
    Task ApplyStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    bool HasSavedState { get; }
}
```

### Implementation Steps

#### 1. Create Recording Methods

Record state changes as they occur. Each record should capture:
- **Timestamp** - When the change occurred
- **Entity ID** - Which object changed (train, section, or call ID)
- **New State/Value** - The resulting state or value

Example for train state:
```csharp
public async Task RecordTrainStateChangeAsync(int trainId, TrainState state, CancellationToken ct)
{
    var record = new TrainStateRecord
    {
        Timestamp = DateTimeOffset.UtcNow,
        TrainId = trainId,
        State = state
    };
    await SaveRecordAsync(record, ct);
}
```

#### 2. Implement State Restoration

The `ApplyStateAsync` method receives pre-built dictionaries from the broker:

```csharp
public Task ApplyStateAsync(
    Dictionary<int, TrainSection> sections,
    Dictionary<int, Train> trains,
    Dictionary<int, TrainStationCall> calls,
    Dictionary<int, OperationPlace> operationPlaces,
    Dictionary<int, TrackStretch> trackStretches,
    CancellationToken cancellationToken)
{
    var records = await LoadRecordsAsync(cancellationToken);

    foreach (var record in records.OrderBy(r => r.Timestamp))
    {
        if (trains.TryGetValue(record.TrainId, out var train))
        {
            train.State = record.State;
        }
    }

    return Task.CompletedTask;
}
```

#### 3. Handle Track Changes

For track changes, use the `operationPlaces` dictionary to resolve the station and find the track:

```csharp
if (calls.TryGetValue(record.CallId, out var call))
{
    var stationId = call.At.Id;
    if (operationPlaces.TryGetValue(stationId, out var place))
    {
        var newTrack = place.Tracks.FirstOrDefault(t => t.Number == record.NewTrackNumber);
        if (newTrack is not null)
        {
            call.ChangeTrack(newTrack);
        }
    }
}
```

### The Importance of Deterministic IDs

For state restoration to work correctly, **entity IDs must be deterministic** across broker restarts.

#### How IDs Work

The domain objects use auto-incrementing IDs via the `OrNextId` pattern:
- If an ID > 0 is provided, that ID is used
- If ID ≤ 0, a new ID is auto-generated

#### Making IDs Deterministic

To ensure IDs are stable across restarts, your **data provider** must supply explicit IDs:

```json
{
  "stations": [
    { "id": 1, "name": "Station A", "signature": "A" },
    { "id": 2, "name": "Station B", "signature": "B" }
  ],
  "trains": [
    { "id": 101, "number": "123", "company": "SJ" }
  ],
  "calls": [
    { "id": 1001, "trainId": 101, "stationId": 1, ... }
  ]
}
```

#### What Happens Without Deterministic IDs

| Scenario | First Session | Second Session | Result |
|----------|--------------|----------------|--------|
| **With explicit IDs** | Train 101 → Running | Load Train 101 | State restored correctly |
| **Without explicit IDs** | Train 1 → Running | Train might be 5 | State applied to wrong train |

#### ID Resolution Flow

```
1. Broker loads configuration
   └── Data provider returns objects with explicit IDs

2. Broker builds dictionaries
   ├── sections[id] → TrainSection
   ├── trains[id] → Train
   ├── calls[id] → TrainStationCall
   ├── operationPlaces[id] → OperationPlace
   └── trackStretches[id] → TrackStretch

3. State provider applies saved state
   └── Uses dictionaries to find objects by their stable IDs
```

### Database Implementation Example

For a database-backed state provider:

```csharp
public class DatabaseStateProvider : ITrainStateProvider
{
    private readonly DbContext _context;

    public async Task RecordTrainStateChangeAsync(int trainId, TrainState state, CancellationToken ct)
    {
        _context.TrainStateChanges.Add(new TrainStateChange
        {
            Timestamp = DateTimeOffset.UtcNow,
            TrainId = trainId,
            State = state
        });
        await _context.SaveChangesAsync(ct);
    }

    public async Task ApplyStateAsync(
        Dictionary<int, TrainSection> sections,
        Dictionary<int, Train> trains,
        Dictionary<int, TrainStationCall> calls,
        Dictionary<int, OperationPlace> operationPlaces,
        Dictionary<int, TrackStretch> trackStretches,
        CancellationToken ct)
    {
        var records = await _context.TrainStateChanges
            .OrderBy(r => r.Timestamp)
            .ToListAsync(ct);

        foreach (var record in records)
        {
            if (trains.TryGetValue(record.TrainId, out var train))
            {
                train.State = record.State;
            }
        }
    }

    public async Task ClearAsync(CancellationToken ct)
    {
        _context.TrainStateChanges.RemoveRange(_context.TrainStateChanges);
        await _context.SaveChangesAsync(ct);
    }

    public bool HasSavedState => _context.TrainStateChanges.Any();
}
```

## Included Implementations

This package includes:

### Data Providers
- `JsonFileBrokerDataProvider` - Loads configuration from JSON files
- `AccessDataProvider` - Loads configuration from a Microsoft Access database (.accdb) using ODBC

### State Providers
- `CsvCompositeStateProvider` - CSV-based event sourcing (file per provider)
- `InMemoryCompositeStateProvider` - In-memory storage for testing

## Usage

```csharp
// Create providers
var dataProvider = new JsonFileBrokerDataProvider("layout.json", "trains.json");
var stateProvider = new CsvCompositeStateProvider("./state");

// Create and initialize broker
var broker = new Broker(dataProvider, stateProvider, timeProvider, logger);
await broker.InitAsync(isRestart: false);

// On restart, pass isRestart: true to restore saved state
await broker.InitAsync(isRestart: true);
```

## Recording State Changes

Use `ExecuteAndRecordAsync` to execute actions and record state changes:

```csharp
var action = departureActions.First(a => a.Action == DispatchAction.Manned);
await action.ExecuteAndRecordAsync(broker.StateProvider);
```

Or record manually:
```csharp
train.State = TrainState.Manned;
await stateProvider.TrainStateProvider.RecordTrainStateChangeAsync(train.Id, train.State);
```
