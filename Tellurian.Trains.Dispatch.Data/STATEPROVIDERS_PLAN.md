# State Providers Implementation Plan

This document analyzes the proposed new approach in `STATEPROVIDER_NEW_APPROACH.md`, identifies gaps, and provides a detailed implementation plan.

> **Status Update (2025-12-10):** All issues and decision points have been resolved. The documents are now in sync and ready for implementation. See Part 2 for issue status and Part 5 for decisions.

## Part 1: Analysis of Current Implementation

### Current State Provider Architecture

The existing implementation uses:

- **`IBrokerStateProvider`** interface with two methods:
  - `SaveTrainsectionsAsync(IEnumerable<TrainSection>, CancellationToken)` - saves all state at once
  - `ReadTrainSections(CancellationToken)` - reads all train sections

- **`JsonFileBrokerStateProvider`** - persists state as JSON with delta encoding:
  - `JsonTrainState` - Train.Id + TrainState
  - `JsonTrainSectionState` - Section.Id + DispatchState + CurrentTrackStretchIndex
  - `JsonCallState` - Call.Id + ObservedArrivalTime + ObservedDepartureTime + NewTrackNumber

- **`InMemoryStateProvider`** - for testing

### Where State Changes Occur

State changes happen in these locations:

1. **`ActionContextExtensions.Execute()`** (`ActionContextExtensions.cs:14-37`)
   - Dispatch actions: Request, Accept, Reject, Revoke, Depart, Pass, Arrive, Clear
   - Train actions: Manned, Running, Canceled, Aborted, Completed

2. **`TrainSection.AdvanceToNextTrackStretch()`** (`TrainSection.cs:134-141`)
   - Increments `CurrentTrackStretchIndex` when passing a SignalControlledPlace

3. **`TrackStretchCapacityManager`** (`TrackStretchCapacityManager.cs`)
   - `Occupy()` - adds `TrackStretchOccupancy` entries
   - `Release()` - removes occupancy entries
   - Handles cascading occupancy for OtherPlaces (unsignalled junctions)

4. **TrainStationCall modifications** (observed times, track changes)
   - Not currently visible in action handlers - likely needs injection points

---

## Part 2: Issues and Gaps in Proposed Approach

### 2.1 Missing State Types ✅ RESOLVED

**Issue:** The document proposes three providers but doesn't fully cover all state that needs persistence.

| State Type | Document Coverage | Current Implementation |
|------------|-------------------|----------------------|
| TrainState (enum) | TrainStateProvider | JsonTrainState |
| DispatchState (section) | DispatcherStateProvider | JsonTrainSectionState |
| CurrentTrackStretchIndex | Not mentioned | JsonTrainSectionState |
| ObservedArrivalTime | TrainStateProvider | JsonCallState |
| ObservedDepartureTime | TrainStateProvider | JsonCallState |
| NewTrackNumber | Not mentioned | JsonCallState |
| TrackStretchOccupancy | TrackStateProvider (unclear) | Not persisted (transient) |

**Recommendation:** The `CurrentTrackStretchIndex` should be included in `DispatcherStateProvider` alongside `DispatchState` changes, as they are logically coupled (Pass action changes the index).

**Resolution:** NEW_APPROACH now includes:
- `TrackStretchIndex` column in dispatch-state.csv (set on Departed, updated on Pass)
- `TrackChange` ChangeType in train-state.csv with `NewTrack` column
- TrackStretchOccupancy explicitly stated as derivable from dispatch state

### 2.2 TrackStateProvider Scope Unclear ✅ RESOLVED

**Issue:** The document says "track on a track section" but doesn't clarify:
- Does this mean `StationTrack` (at stations)?
- Or `TrackStretch` between operation places?
- Or track changes for station calls (`NewTrackNumber`)?

**Current model:**
- `TrackStretch.ActiveOccupancies` - list of `TrackStretchOccupancy` records
- This is *transient state* that can be reconstructed from DispatchState
- When a train is Departed, it occupies the track stretch
- When it Arrives or passes to next stretch, the previous stretch is released

**Recommendation:**
- `TrackStateProvider` may be **unnecessary** if occupancy can be reconstructed from dispatch state
- OR, rename to `TrackStretchOccupancyStateProvider` and clarify its purpose
- Track *changes* at stations (NewTrackNumber) should go in `TrainStateProvider`

**Resolution:** NEW_APPROACH explicitly states: "A separate TrackStateProvider for track occupancy is not needed, as occupancy can be reconstructed from dispatch state." Track changes (NewTrackNumber) are handled by TrainStateProvider as `TrackChange` events.

### 2.3 Signal Controlled Place Pass Events ✅ RESOLVED

**Issue:** The document mentions "signal controlled places change state (e.g., passed)" but:
- SignalControlledPlace itself doesn't have state - it's a configuration object
- The *train's position* relative to the place changes (via `CurrentTrackStretchIndex`)
- The document conflates the place's concept with the train's progress

**Recommendation:** Clarify that Pass events track the *train's passage* through a SignalControlledPlace, not the place's own state. This belongs in `DispatcherStateProvider` as a Pass event record.

**Resolution:** NEW_APPROACH now correctly states: "Pass events track the *train's passage* through a SignalControlledPlace, not the place's own state. The pass action advances the train's `CurrentTrackStretchIndex`." The CSV format includes `Pass` as a ChangeType with `SignalPlaceId` and `TrackStretchIndex`.

### 2.4 Missing TrainState.Undo ✅ RESOLVED

**Issue:** The `TrainState` enum includes `Undo` state, but the document doesn't mention how undo operations affect the event log.

**Recommendation:** Add a section on handling state reversals. Options:
1. Log the reversal as a new event (preferred for audit trail)
2. Mark previous events as undone
3. Physical deletion of events (not recommended)

**Resolution:** NEW_APPROACH now has a detailed "Undo Train State" section that:
- Clarifies `Undo` is an action, not a state
- Explains the reversal writes the *resulting state* (e.g., Running after undoing Aborted)
- Proposes domain model changes: `Train.PreviousState` property, remove `TrainState.Undo` enum value
- This follows option 1 (log reversal as new event)

### 2.5 CSV Library Not Specified ✅ RESOLVED

**Issue:** The document says "use existing CSV libraries" but doesn't recommend one.

**Recommendation:** Use **CsvHelper** (https://joshclose.github.io/CsvHelper/). It is:
- Most popular .NET CSV library (300M+ downloads)
- Well maintained
- Supports async operations
- Handles complex type mapping

**Resolution:** NEW_APPROACH now has a "CSV Library" section specifying CsvHelper with the same rationale.

### 2.6 Thread Safety Not Addressed ✅ RESOLVED

**Issue:** The document doesn't mention concurrent access handling. The current implementation uses `SemaphoreSlim(1,1)` for thread safety.

**Recommendation:** Each state provider should use the same semaphore pattern as current implementation:
- Non-blocking write attempts (if write in progress, skip)
- Async reads with proper locking

**Resolution:** NEW_APPROACH now has a "Thread Safety and File Access" section that:
- Keeps file open in append mode during session
- Ensures writes are never skipped (different from old semaphore pattern)
- Explains reading only happens at restart when broker is not operational

### 2.7 File Format Details Missing ✅ RESOLVED

**Issue:** CSV column structure not defined.

**Recommendation:** Define explicit schemas:

**train-state.csv:**
```
Timestamp,TrainId,State
2024-01-15T10:30:00Z,1,Manned
2024-01-15T10:35:00Z,1,Running
```

**dispatch-state.csv:**
```
Timestamp,SectionId,State,TrackStretchIndex
2024-01-15T10:30:00Z,1,Requested,
2024-01-15T10:31:00Z,1,Accepted,
2024-01-15T10:35:00Z,1,Departed,0
2024-01-15T10:40:00Z,1,Departed,1
2024-01-15T10:45:00Z,1,Arrived,
```

**call-state.csv:**
```
Timestamp,CallId,ChangeType,Value
2024-01-15T10:35:00Z,1,Departure,10:35
2024-01-15T10:45:00Z,2,Arrival,10:45
2024-01-15T10:30:00Z,1,TrackChange,2A
```

**Resolution:** NEW_APPROACH now has detailed CSV schemas with:
- `ChangeType` column in both files to differentiate record types
- `train-state.csv`: Timestamp, ChangeType (State/ObservedArrival/ObservedDeparture/TrackChange), TrainId, CallId, State, Time, NewTrack
- `dispatch-state.csv`: Timestamp, ChangeType (State/Pass), SectionId, State, TrackStretchIndex, SignalPlaceId
- Complete examples with realistic data

### 2.8 Migration Strategy Missing ✅ RESOLVED (Not Needed)

**Issue:** No mention of how to migrate from current JSON-based state to new CSV-based event log.

**Recommendation:** Add migration support:
1. Detect existing JSON state file
2. Convert to event log format (with synthetic timestamps)
3. Delete or archive JSON file
4. Continue with event-sourced approach

**Resolution:** No backward compatibility is required. The existing `IBrokerStateProvider` and JSON implementation can be replaced entirely. Sessions will start fresh with the new CSV-based approach.

### 2.9 Performance Considerations ✅ RESOLVED (Not a Concern)

**Issue:** Reading all historical events could be slow for long sessions.

**Recommendation:** Consider:
1. Periodic compaction (snapshot + events since snapshot)
2. Or accept linear read time for simplicity (typical sessions may be short)
3. Document expected performance characteristics

**Resolution:** Performance is not a concern. Expected maximum is ~2000 records per CSV file (train state, call changes, dispatch state). Reading 2000 CSV records is fast enough that no compaction or snapshot mechanism is needed.

### 2.10 Injection Points Not Specified ✅ RESOLVED

**Issue:** The document mentions "injecting the state provider into various parts of the broker" but doesn't specify where.

**Current state changes happen in:**
- `ActionContextExtensions.Execute()` - needs state provider injection
- `Broker.InitAsync()` - already has state provider
- `TrackStretchCapacityManager` - may not need persistence (transient)

**Recommendation:** Options:
1. **Event dispatch pattern:** Actions raise events that state providers subscribe to
2. **Direct injection:** Pass state providers to Execute() method
3. **Broker mediator:** All actions go through Broker which handles persistence

Option 1 (event dispatch) is most aligned with event-sourcing but adds complexity.
Option 3 (broker mediator) is simpler and maintains existing patterns.

**Resolution:** Use **direct calls** (Option 2/3 hybrid). The state providers will be injected into the Broker, which will call them directly after state changes occur. This is simpler than event dispatch and sufficient for this use case. Key injection points:
- `ActionContextExtensions.Execute()` - call state provider after action succeeds
- Broker holds the composite state provider and passes it where needed

---

## Part 3: Recommended Interface Design

### 3.1 Base Interface

```csharp
/// <summary>
/// Base interface for state providers that record individual state changes.
/// </summary>
public interface IStateProvider
{
    /// <summary>
    /// Applies all recorded state changes to the provided broker objects.
    /// </summary>
    Task ApplyStateAsync(IBroker broker, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all recorded state (for session reset).
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if there is saved state to apply.
    /// </summary>
    bool HasSavedState { get; }
}
```

### 3.2 Train State Provider

```csharp
public interface ITrainStateProvider : IStateProvider
{
    /// <summary>
    /// Records a train state change.
    /// </summary>
    Task RecordTrainStateChangeAsync(int trainId, TrainState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records an observed arrival or departure time.
    /// </summary>
    Task RecordObservedTimeAsync(int callId, TimeSpan? arrivalTime, TimeSpan? departureTime, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a track change for a station call.
    /// </summary>
    Task RecordTrackChangeAsync(int callId, string newTrackNumber, CancellationToken cancellationToken = default);
}
```

### 3.3 Dispatch State Provider

```csharp
public interface IDispatchStateProvider : IStateProvider
{
    /// <summary>
    /// Records a dispatch state change for a train section.
    /// </summary>
    Task RecordDispatchStateChangeAsync(int sectionId, DispatchState state, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a Pass action (train passing a SignalControlledPlace).
    /// </summary>
    Task RecordPassAsync(int sectionId, int signalControlledPlaceId, int newTrackStretchIndex, CancellationToken cancellationToken = default);
}
```

### 3.4 Composite Provider (Optional)

```csharp
/// <summary>
/// Combines multiple state providers for simpler broker integration.
/// </summary>
public interface ICompositeStateProvider
{
    ITrainStateProvider TrainStateProvider { get; }
    IDispatchStateProvider DispatchStateProvider { get; }

    Task ApplyAllStateAsync(IBroker broker, CancellationToken cancellationToken = default);
    Task ClearAllAsync(CancellationToken cancellationToken = default);
    bool HasAnySavedState { get; }
}
```

---

## Part 4: Implementation Plan

### Phase 1: Interface Definition

**Files to create/modify:**

1. `Tellurian.Trains.Dispatch/Brokers/IStateProvider.cs` (new)
   - Base interface

2. `Tellurian.Trains.Dispatch/Brokers/ITrainStateProvider.cs` (new)
   - Train state recording interface

3. `Tellurian.Trains.Dispatch/Brokers/IDispatchStateProvider.cs` (new)
   - Dispatch state recording interface

4. `Tellurian.Trains.Dispatch/Brokers/ICompositeStateProvider.cs` (new)
   - Optional composite interface

### Phase 2: CSV Models and Extension Methods

**Files to create:**

1. `Tellurian.Trains.Dispatch.Data/Csv/CsvTrainStateRecord.cs`
   - POCO for train state CSV records

2. `Tellurian.Trains.Dispatch.Data/Csv/CsvDispatchStateRecord.cs`
   - POCO for dispatch state CSV records

3. `Tellurian.Trains.Dispatch.Data/Csv/CsvCallStateRecord.cs`
   - POCO for call state CSV records

4. `Tellurian.Trains.Dispatch.Data/Csv/CsvRecordExtensions.cs`
   - Extension methods for CSV reading/writing

### Phase 3: Provider Implementations

**Files to create:**

1. `Tellurian.Trains.Dispatch.Data/Csv/CsvTrainStateProvider.cs`
   - Implements `ITrainStateProvider`
   - Writes to `train-state.csv`

2. `Tellurian.Trains.Dispatch.Data/Csv/CsvDispatchStateProvider.cs`
   - Implements `IDispatchStateProvider`
   - Writes to `dispatch-state.csv`

3. `Tellurian.Trains.Dispatch.Data/Csv/CsvCompositeStateProvider.cs`
   - Implements `ICompositeStateProvider`
   - Coordinates multiple CSV providers

### Phase 4: Broker Integration

**Files to modify:**

1. `Tellurian.Trains.Dispatch/Brokers/Broker.cs`
   - Add `ICompositeStateProvider` dependency
   - Call state providers on initialization/restart

2. `Tellurian.Trains.Dispatch/ActionContextExtensions.cs`
   - Add optional state provider parameter to `Execute()`
   - OR raise events for state changes

3. Consider adding `IBroker.ApplyActionAsync()` method that:
   - Validates action
   - Executes action
   - Records state change
   - Returns result

### Phase 5: Testing

**Files to create:**

1. `Tellurian.Trains.Dispatch.Data.Tests/CsvTrainStateProviderTests.cs`

2. `Tellurian.Trains.Dispatch.Data.Tests/CsvDispatchStateProviderTests.cs`

3. `Tellurian.Trains.Dispatch.Data.Tests/CsvCompositeStateProviderTests.cs`

**Test scenarios:**
- Write single state change, read back
- Write multiple changes, reconstruct state
- Handle empty state file
- Handle corrupted/incomplete records
- Thread safety under concurrent access
- Restart broker, verify state restored correctly

### Phase 6: Migration Support (Optional)

**Files to create/modify:**

1. `Tellurian.Trains.Dispatch.Data/Migration/JsonToCsvMigrator.cs`
   - Converts existing JSON state to CSV events

---

## Part 5: Decision Points for Discussion

| # | Decision Point | Status |
|---|----------------|--------|
| 1 | **TrackStateProvider necessity:** Is track occupancy state truly needed, or can it be reconstructed from dispatch state? | ✅ **DECIDED:** Not needed - occupancy reconstructed from dispatch state |
| 2 | **Event dispatch vs direct calls:** Should state recording use events or direct method calls? | ✅ **DECIDED:** Direct calls - simpler and sufficient for this use case |
| 3 | **Composite vs individual providers:** Should Broker take individual providers or a composite? | ✅ **DECIDED:** Composite provider is acceptable |
| 4 | **Existing interface compatibility:** Should `IBrokerStateProvider` be kept for backward compatibility, or replaced entirely? | ✅ **DECIDED:** No backward compatibility needed - can replace entirely |
| 5 | **Performance threshold:** At what event count should compaction/snapshots be considered? | ✅ **DECIDED:** Not needed - max ~2000 records per file expected, reading is fast enough |
| 6 | **CSV library:** Confirm CsvHelper as the library choice. | ✅ **DECIDED:** CsvHelper confirmed |

> **All decision points resolved (2025-12-10)**

---

## Part 6: Suggested Additions to STATEPROVIDER_NEW_APPROACH.md

Based on the analysis above, consider adding these sections:

| # | Suggestion | Status |
|---|------------|--------|
| 1 | **CurrentTrackStretchIndex persistence** - clarify this is part of DispatcherStateProvider | ✅ Added |
| 2 | **Track change tracking** - clarify NewTrackNumber goes in TrainStateProvider | ✅ Added |
| 3 | **TrackStateProvider clarification** - define whether this is needed or can be derived | ✅ Added |
| 4 | **CSV schema definitions** - explicit column formats for each provider | ✅ Added |
| 5 | **Thread safety requirements** - semaphore pattern | ✅ Added |
| 6 | **Migration strategy** - from existing JSON state | ✅ Not needed (clean start) |
| 7 | **Injection strategy** - where state providers get called | ✅ Direct calls decided |
| 8 | **CsvHelper** as the recommended library | ✅ Added |

> **All suggestions resolved (2025-12-10)** - Documents are in sync and ready for implementation.
