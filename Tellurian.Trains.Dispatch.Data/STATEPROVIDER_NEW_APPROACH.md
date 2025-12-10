# State Providers - New Approach

This document outlines a new approach to implementing
state providers in the Tellurian Trains Dispatch system.
The goal is to enhance modularity, maintainability, and
scalability of state management within the application.
The new approach also makes it easier to add new state providers
and keep track of historical state data.

## Key Concepts

The main concepts of the new approach are:
- Write each small state change to the state provider as it happens.
- Each state change is timestamped.
- Each state change is logged for historical tracking.
- Each object type with state is handled by a dedicated state provider.
- Reloading state involves reading all historical state changes and reconstructing the current state,
  unless otherwise specified for a given state provider.

## State Providers

The following state providers will be implemented:

- **TrainStateProvider**: Records changes in train state, observed arrival and departure times,
  and station track changes.
- **DispatchStateProvider**: Records changes in dispatch state for train sections,
  including pass events through signal-controlled places.

Note: A separate TrackStateProvider for track occupancy is not needed, as occupancy can be
reconstructed from dispatch state (a train occupies a track stretch when Departed,
and releases it when Arrived or passed to the next stretch).

## Implementation Details

Each state provider will implement a separate interface derived from a common
`IStateProvider` interface. This interface will define methods for:
- Writing state changes.
- Reloading state from data using the current configuration of the broker.

Interfaces should be placed in the `Tellurian.Trains.Dispatch` project's `Brokers`
folder. Implementations should be placed in the `Tellurian.Trains.Dispatch.Data` project.

Methods for writing and reading should be asynchronous to ensure non-blocking operations.

The first implementation should write state changes to CSV files for simplicity.
Each state provider will have its own CSV file to log changes.

References to objects (e.g., trains, sections, calls) will be done using Id properties to ensure consistency.

Because each state provider can write different types of state changes,
the type of state change must be included in the CSV records, in order to differentiate
between them when reading the records back.

Reading and writing CSV records should be handled using extension methods.

### CSV Library

Use **CsvHelper** (https://joshclose.github.io/CsvHelper/) for reading and writing CSV files.
CsvHelper is:
- The most popular .NET CSV library with a large user base.
- Well-maintained and actively developed.
- Flexible in writing CSV files that can be read by Excel and LibreOffice Calc.
- Supports async operations.

### Thread Safety and File Access

Each CSV file must be protected to permit only one write at a time.
This is achieved by keeping the file open in append mode during the session.

Important considerations:
- Writing must never be skipped, as this would cause missed state changes.
- Each state provider opens its CSV file for append when initialized.
- Writes append new rows to the open file stream.
- Reading is only performed when the Broker is restarting.
- During restart, the entire file is read sequentially; no thread safety is needed for reading
  since the broker is not yet operational.

### Injecting State Providers

The state provider must be able to write state changes as they happen.
This requires injecting the state provider into the broker where state changes occur.

**Injection Strategy: Direct Calls**

Use direct method calls (not events) for simplicity:
- The Broker holds a composite state provider that combines TrainStateProvider and DispatchStateProvider.
- After an action succeeds in `ActionContextExtensions.Execute()`, call the appropriate state provider method.
- The composite provider coordinates writing to both CSV files as needed.

**Key Injection Points:**
- `ActionContextExtensions.Execute()` - After dispatch actions (Request, Accept, Depart, Pass, Arrive, etc.)
  and train actions (Manned, Running, Canceled, Aborted, Completed, Undo).
- Where observed times are set on TrainStationCall.
- Where track changes are applied to TrainStationCall.

### Composite State Provider

A composite interface combines TrainStateProvider and DispatchStateProvider for easier broker integration:

```csharp
public interface ICompositeStateProvider
{
    ITrainStateProvider TrainStateProvider { get; }
    IDispatchStateProvider DispatchStateProvider { get; }

    Task ApplyAllStateAsync(IBroker broker, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    bool HasAnySavedState { get; }
}
```

### Performance Expectations

Expected maximum is approximately 2000 records per CSV file per session.
At this scale, reading all records sequentially during broker restart is fast enough.
No compaction, snapshots, or other optimization mechanisms are needed.

### Migration from Previous Implementation

No migration from the previous JSON-based `IBrokerStateProvider` is required.
The existing implementation can be replaced entirely. Sessions will start fresh
with the new CSV-based event-sourced approach.

## Train State Provider

The TrainStateProvider will handle all state changes related to trains.
This includes:
- Recording changes in train state (e.g., Planned, Canceled, Manned, Running, Aborted, Completed).
- Recording observed times when a train arrives at or departs from a station.
  This needs to reference the call Id.
- Recording station track changes when a train is assigned a different track than planned.
  This needs to reference the call Id and new track number.

When the train's final state is Completed, Canceled, or Aborted,
the train should be excluded when re-establishing the current state.
Note that cancellations and abortions of trains may be undone (reverted),
so only the final state should be examined when deciding whether to
include a train in the current state.

### Undo Train State

The `Undo` action is not a train state itself. Instead, it is an action that reverts
the train's state back to its `PreviousState` property. This allows dispatchers
to correct mistakes (e.g., accidentally canceling or aborting a train).

When an undo action is performed:
1. The train's `State` is set back to its `PreviousState` value.
2. The resulting state (the reverted state) is written to the state provider.
3. For example, a train can go from Aborted back to Running.

This requires a change in the domain model:
- The `Train` class needs a `PreviousState` property.
- The `TrainState.Undo` enum value should be removed (it's an action, not a state).
- The action state machine needs to support the Undo action for appropriate states.

### Train State CSV Format

File: `train-state.csv`

Columns:
- `Timestamp` - ISO 8601 format timestamp of when the change occurred
- `ChangeType` - Type of change: `State`, `ObservedArrival`, `ObservedDeparture`, `TrackChange`
- `TrainId` - Train identifier (for all change types)
- `CallId` - Call identifier (for ObservedArrival, ObservedDeparture, and TrackChange)
- `State` - New train state (for State changes)
- `Time` - Observed time as TimeSpan (for ObservedArrival and ObservedDeparture)
- `NewTrack` - New track number (for TrackChange)

Example:
```csv
Timestamp,ChangeType,TrainId,CallId,State,Time,NewTrack
2024-01-15T10:30:00Z,State,1,,Manned,,
2024-01-15T10:32:00Z,State,1,,Running,,
2024-01-15T10:35:00Z,ObservedDeparture,1,1,,10:35:00,
2024-01-15T10:36:00Z,TrackChange,1,2,,,2A
2024-01-15T10:45:00Z,ObservedArrival,1,2,,10:45:00,
2024-01-15T11:00:00Z,State,1,,Completed,,
```

## Dispatch State Provider

The DispatchStateProvider will handle all state changes related to
the train sections' dispatch state. This includes:

- Recording when a train section's dispatch state changes
  (e.g., None, Requested, Accepted, Rejected, Revoked, Departed, Arrived, Canceled).
  This needs to reference the section Id.
- Recording when a train passes a signal-controlled place.
  Pass events track the *train's passage* through a SignalControlledPlace,
  not the place's own state. The pass action advances the train's `CurrentTrackStretchIndex`.
  This needs to reference the section Id, the signal-controlled place Id,
  and the new track stretch index.

### Dispatch State CSV Format

File: `dispatch-state.csv`

Columns:
- `Timestamp` - ISO 8601 format timestamp of when the change occurred
- `ChangeType` - Type of change: `State`, `Pass`
- `SectionId` - Train section identifier
- `State` - New dispatch state (for State changes)
- `TrackStretchIndex` - Current track stretch index (set when Departed, updated on Pass)
- `SignalPlaceId` - Signal-controlled place Id (for Pass changes)

Example:
```csv
Timestamp,ChangeType,SectionId,State,TrackStretchIndex,SignalPlaceId
2024-01-15T10:30:00Z,State,1,Requested,,
2024-01-15T10:31:00Z,State,1,Accepted,,
2024-01-15T10:35:00Z,State,1,Departed,0,
2024-01-15T10:40:00Z,Pass,1,,1,5
2024-01-15T10:43:00Z,Pass,1,,2,6
2024-01-15T10:45:00Z,State,1,Arrived,,
2024-01-15T10:32:00Z,State,2,Requested,,
2024-01-15T10:33:00Z,State,2,Rejected,,
```

## Reloading State

When reloading state, each state provider will read its respective CSV file.
The current state will be reconstructed by applying all logged state changes in order.

When calling the reload method on a state provider,
it should pass in the current configuration of the broker.
This allows the state provider to reference the correct objects
when reconstructing the state.

The configuration of the broker will be built the same way
as when loading state for the first time. The task of the reload
method is to read all historical state changes and apply them to the objects
passed from the broker.

### State Reconstruction Logic

**For TrainStateProvider:**
1. Read all records from `train-state.csv` ordered by timestamp.
2. For each `State` record, find the train by TrainId and set its State.
3. For each `ObservedArrival` record, find the call by CallId and set the observed arrival time.
4. For each `ObservedDeparture` record, find the call by CallId and set the observed departure time.
5. For each `TrackChange` record, find the call by CallId and set the new track.
6. After processing, trains with final state Completed, Canceled, or Aborted
   can be filtered out from active dispatching.

**For DispatchStateProvider:**
1. Read all records from `dispatch-state.csv` ordered by timestamp.
2. For each `State` record, find the section by SectionId and set its State.
   If the state is `Departed`, also set `CurrentTrackStretchIndex` to 0.
3. For each `Pass` record, find the section by SectionId and set its `CurrentTrackStretchIndex`.

## Test Strategy

Each state provider should have a comprehensive set of unit tests
covering:
- Writing state changes.
- Reloading state from written data.

Tests should ensure that the state providers correctly handle
various scenarios, including edge cases and error conditions.

From an initial state after loading configuration of the broker,
apply a series of state changes that will be written to the state provider.

Then restart an instance of a new broker in restart mode (to compare broker states).
Compare the state of the new broker with the expected state of the original broker.

### Test Data

Test data for unit tests can be stored in a dedicated test data folder.
Test data is JSON files representing the initial configuration of the broker.
