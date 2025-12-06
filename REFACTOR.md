# Refactoring Plan: Train Dispatch Domain Model

## Executive Summary

This document analyzes the identified design flaws in the current dispatch system and proposes a comprehensive refactoring plan based on the suggested improvements. The refactoring introduces a cleaner separation between physical infrastructure (TrackStretch), logical routing (DispatchStretch), and operational concepts (OperationalPlace, ControlledPlace).

---

## Part 1: Analysis of Current Design Issues

### Issue 1: BlockSignal/DispatchStretch Coupling Problem

**Current Design:**
```
DispatchStretch (A → B)
    └── [BlockSignal S1, BlockSignal S2]
            └── Each BlockSignal belongs exclusively to this DispatchStretch
```

**The Problem:**
The current model assumes a BlockSignal belongs to exactly one DispatchStretch. However, in real railway operations, a track segment between Station A and Block Signal S1 might be shared by multiple routes:

```
                ┌─────────────────┐
Station A ──────│ Shared Segment  │────── Signal S1 ───── Station B
                │                 │
                └─────────────────┘────── Signal S2 ───── Station C
```

In this scenario:
- DispatchStretch A→B uses segment (A to S1)
- DispatchStretch A→C also uses segment (A to S1)
- When a train occupies (A to S1) on route A→B, it should block trains on route A→C

The current model cannot represent this because:
1. BlockSignals are instantiated per DispatchStretch (`DispatchStretch.cs:34-35`)
2. Capacity is computed per DispatchStretch, not per physical segment (`DispatchStretchExtensions.cs:22-34`)
3. There's no concept of shared infrastructure between stretches

**Evidence from Code:**
```csharp
// DispatchStretch.cs:31-36
public DispatchStretch(Station from, Station to, int numberOfTracks = 1,
    IList<BlockSignal>? intermediateBlockSignals = null)
{
    NumberOfTracks = numberOfTracks;
    Forward = new(this, from, to, StretchDirection.Forward, intermediateBlockSignals);
    Reverse = new(this, to, from, StretchDirection.Reverse, intermediateBlockSignals?.Reversed);
}
```

Block signals are **copied** (with reversal) for each direction, creating separate instances rather than referencing shared physical locations.

### Issue 2: DispatchState Conflating Current State and Available Actions

**Current Design:**
```csharp
public enum DispatchState
{
    None, Canceled, Requested, Rejected,
    Accepted, Revoked, Departed, Passed, Arrived
}
```

This enum serves dual purposes:
1. **Current state** of the TrainSection (stored in `TrainSection.State`)
2. **Available actions** to transition to (returned by `ArrivalStates`/`DepartureStates`)

**The Problem:**
- `Passed` is awkward as a "state" - a train doesn't stay in "Passed" state; it immediately becomes "Departed at block N+1"
- The state machine logic must filter states by dispatcher role, creating complex expressions
- Adding new action types (like intermediate stops, speed restrictions) becomes difficult
- UI code must interpret states as actions (via `ActionResourceName`)

**Evidence from Code:**
```csharp
// TrainSectionStateExtensions.cs:83-90
public DispatchState[] ArrivalStates => trainSection.State switch
{
    _ when trainSection.Train.IsCanceledOrAborted => [],
    DispatchState.Requested => [DispatchState.Accepted, DispatchState.Rejected],
    DispatchState.Departed when trainSection.ArrivalDispatcherCanPassNextBlockSignal => [DispatchState.Passed],
    DispatchState.Departed when trainSection.HasPassedAllBlockSignals => [DispatchState.Arrived],
    _ => []
};
```

The switch expression mixes:
- State queries (`trainSection.State`)
- Business rules (`ArrivalDispatcherCanPassNextBlockSignal`)
- Action generation (`[DispatchState.Passed]`)

---

## Part 2: Evaluation of Proposed Solutions

### Proposal 1: TrackStretch as Physical Infrastructure

**Concept:**
```
TrackStretch (physical)
    ├── Between: OperationalPlace A ↔ OperationalPlace B
    └── Tracks: [Track 1, Track 2, ...]
            └── Track: Number, Direction (Forward/Backward/DoubleDirected/Closed)
```

**Assessment: EXCELLENT**

This correctly models the **physical reality**:
- A TrackStretch is a piece of railway between two points
- It exists independently of how trains are routed over it
- Multiple Tracks allow for capacity modeling
- Direction per track handles:
  - Swedish model: Both tracks are DoubleDirected (trains can use either)
  - German/Traditional model: One track Forward, one Backward
  - Maintenance: A track can be Closed

**Benefits:**
1. Capacity is calculated at the correct level (physical infrastructure)
2. A TrackStretch can be shared by multiple DispatchStretches
3. Realistic modeling of double-track, multi-track, and single-track lines

### Proposal 2: DispatchStretch as Sequence of TrackStretches

**Concept:**
```
DispatchStretch (logical route)
    └── Sequence: [TrackStretch1, TrackStretch2, ...]
```

**Assessment: GOOD with refinement**

A DispatchStretch becomes a **route** composed of physical segments:
- Train from A to C via junction J: [TrackStretch(A→J), TrackStretch(J→C)]
- Occupancy is tracked per TrackStretch, not per DispatchStretch
- When a train occupies TrackStretch(A→J), all DispatchStretches using it are affected

**Refinement needed:**
The proposal says "a train moves from TrackStretch to next TrackStretch by issuing actions" - this needs clarification. The action triggers a state change; the movement is implicit. Suggest: Actions are `Request`, `Depart`, `Pass(SignalControlledPlace)`, `Arrive`.

### Proposal 3: OperationalPlace and ControlledPlace Hierarchy

**Concept:**
```
OperationalPlace (abstract)
    ├── Station (IsManned, has Dispatcher, can have multiple StationTracks)
    ├── SignalControlledPlace (ControlledBy: IDispatcher, can have StationTracks)
    └── OtherPlace (no control, e.g., simple halt)
```

**Assessment: EXCELLENT**

This properly separates concerns:
- **OperationalPlace**: Where times are recorded (all types)
- **ControlledPlace**: Where dispatch actions are required (Station, SignalControlledPlace)
- **Capacity points**: Places with StationTracks where trains can meet

The current BlockSignal becomes an unmanned SignalControlledPlace:
- It has a name and is controlled by a dispatcher
- It can optionally have tracks for meets (e.g., at a passing loop)
- Multiple SignalControlledPlaces can share a physical location with different names (for multi-aspect signaling)

### Proposal 4: Track Direction Model

**Concept:**
```csharp
enum TrackOperationDirection { ForwardOnly, BackwardOnly, DoubleDirected, Closed }
```

**Assessment: EXCELLENT**

This correctly models:
- **ForwardOnly**: Track is for "up" direction only (e.g., traditional double-track)
- **BackwardOnly**: Track is for "down" direction only
- **DoubleDirected**: Track can be used in either direction (Swedish model, also used for meets on single-track)
- **Closed**: Track is out of service

Combined with Up/Down designation on double-track:
- `Up` track is preferred for trains going From→To
- `Down` track is preferred for trains going To→From

### Proposal 5: MaxLength for Train Meets

**Concept:**
- `Train.MaxLength` (meters)
- `StationTrack.MaxLength` (meters)
- Trains can only meet if at least one fits in the longest StationTrack

**Assessment: GOOD**

This adds realistic constraints:
- Long freight trains can't meet at short sidings
- Helps with automatic meet planning
- `MaxLength = 0` means "ignore constraint" (backward compatible)

**Implementation consideration:**
Should also consider whether BOTH trains need to fit (one on main, one on siding) or just one. In most cases, both should fit somewhere.

### Proposal 6: Separate DispatchState and DispatchAction

**Concept:**
```csharp
enum DispatchState { None, Requested, Rejected, Revoked, Passed, Arrived, Completed, Canceled }
enum DispatchAction { Request, Accept, Reject, Revoke, Depart, Pass, Arrive, Clear }
```

**Assessment: EXCELLENT**

This is the key architectural improvement:
- **DispatchState**: Current condition of the dispatch (stable states)
- **DispatchAction**: Operations that can be performed (transitions)

**Benefits:**
1. State machine logic becomes clearer: `(CurrentState, Conditions) → [AvailableActions]`
2. Actions are explicitly named and documented
3. `Pass(SignalControlledPlace)` becomes a proper action with context
4. Special handling for BlockSignalPassages can be unified under `Pass` action
5. TrainState actions (`Manned`, `Running`, `Completed`) follow the same pattern

---

## Part 3: Recommended Refactoring Plan

### Phase 0: Preparation (Non-Breaking)

**Goal:** Add new structures alongside existing ones; no behavioral changes.

#### Step 0.1: Create TrackOperationDirection enum
```csharp
// Layout/TrackOperationDirection.cs
public enum TrackOperationDirection
{
    ForwardOnly,
    BackwardOnly,
    DoubleDirected,
    Closed
}
```

#### Step 0.2: Create Track class
```csharp
// Layout/Track.cs
public record Track(string? Number, TrackOperationDirection Direction)
{
    public bool IsUpTrack { get; init; }  // Preferred for From→To direction
    public int? MaxLength { get; init; }  // Meters, null = no constraint
}
```

#### Step 0.3: Create OperationalPlace hierarchy
```csharp
// Layout/OperationalPlace.cs
public abstract record OperationalPlace(string Name, string Signature)
{
    public int Id { get; set { field = value.OrNextId; } }
    public IList<StationTrack> Tracks { get; init; } = [];
}

// Layout/SignalControlledPlace.cs
public record SignalControlledPlace(string Name, string Signature, IDispatcher ControlledBy)
    : OperationalPlace(Name, Signature)
{
    public bool IsJunction { get; init; }
}

// Layout/OtherPlace.cs
public record OtherPlace(string Name, string Signature) : OperationalPlace(Name, Signature);
```

#### Step 0.4: Update Station to inherit from OperationalPlace
```csharp
// Trains/Station.cs (modified)
public record Station(string Name, string Signature, bool IsManned = true)
    : OperationalPlace(Name, Signature)
{
    public IDispatcher Dispatcher { get; internal set; } = default!;
}
```

#### Step 0.5: Create TrackStretch class
```csharp
// Layout/TrackStretch.cs
public class TrackStretch
{
    public int Id { get; set { field = value.OrNextId; } }
    public OperationalPlace From { get; }
    public OperationalPlace To { get; }
    public IList<Track> Tracks { get; }

    // Current occupancy tracking
    public IList<TrackStretchOccupancy> ActiveOccupancies { get; } = [];

    public TrackStretch(OperationalPlace from, OperationalPlace to, IList<Track>? tracks = null)
    {
        From = from;
        To = to;
        Tracks = tracks ?? [new Track(null, TrackOperationDirection.DoubleDirected)];
    }

    public bool HasCapacityFor(Train train, TrackOperationDirection requiredDirection) { ... }
}

public record TrackStretchOccupancy(TrainSection Section, Track Track, DateTimeOffset EnteredAt);
```

#### Step 0.6: Create DispatchAction enum
```csharp
// DispatchAction.cs
public enum DispatchAction
{
    Request,
    Accept,
    Reject,
    Revoke,
    Depart,
    Pass,       // Generic pass action - target specified separately
    Arrive,
    Clear,      // Clear canceled/aborted train from stretch
    Manned,     // Train action: crew assigned
    Running,    // Train action: train starts operating
    Canceled,   // Train action: cancel before running
    Aborted,    // Train action: abort during operation
    Completed   // Train action: journey complete
}
```

#### Step 0.7: Add MaxLength to Train and StationTrack
```csharp
// Trains/Train.cs - add property
public int? MaxLength { get; init; }  // Meters, null = no constraint

// Trains/StationTrack.cs - add property
public int? MaxLength { get; init; }  // Meters, null = no constraint
```

### Phase 1: Parallel Implementation (New APIs)

**Goal:** Build new dispatch logic using new structures; keep old structures working.

#### Step 1.1: Create ActionContext for unified action handling
```csharp
// ActionContext.cs
public record ActionContext(
    TrainSection Section,
    DispatchAction Action,
    IDispatcher Dispatcher,
    SignalControlledPlace? PassTarget = null  // For Pass actions
);
```

#### Step 1.2: Create IActionProvider interface
```csharp
// IActionProvider.cs
public interface IActionProvider
{
    IEnumerable<ActionContext> GetAvailableActions(TrainSection section, IDispatcher dispatcher);
}
```

#### Step 1.3: Implement new action state machine
```csharp
// ActionStateMachine.cs
public class ActionStateMachine : IActionProvider
{
    public IEnumerable<ActionContext> GetAvailableActions(TrainSection section, IDispatcher dispatcher)
    {
        // Unified logic combining:
        // - DepartureStates
        // - ArrivalStates
        // - NextTrainStates
        // - BlockSignalPassageActions
        // Returns ActionContext objects with explicit action and target
    }
}
```

#### Step 1.4: Update DispatchStretch to use TrackStretch sequence
```csharp
// Layout/DispatchStretch.cs (new version)
public class DispatchStretch
{
    public int Id { get; set { field = value.OrNextId; } }
    public IList<TrackStretch> Segments { get; }

    // Derived properties
    public OperationalPlace Origin => Segments.First().From;
    public OperationalPlace Destination => Segments.Last().To;
    public IEnumerable<SignalControlledPlace> IntermediateControlPoints =>
        Segments.Skip(1).Select(s => s.From).OfType<SignalControlledPlace>();
}
```

#### Step 1.5: Create TrackStretchCapacityManager
```csharp
// Layout/TrackStretchCapacityManager.cs
public class TrackStretchCapacityManager
{
    private readonly Dictionary<int, TrackStretch> _stretches;

    public bool CanOccupy(TrainSection section, TrackStretch stretch, Track track) { ... }
    public void Occupy(TrainSection section, TrackStretch stretch, Track track) { ... }
    public void Release(TrainSection section, TrackStretch stretch) { ... }

    // This handles shared capacity across multiple DispatchStretches
}
```

### Phase 2: Migration (Gradual Transition)

**Goal:** Move TrainSection to use new structures; deprecate old APIs.

#### Step 2.1: Update TrainSection to track segment position
```csharp
// TrainSection.cs additions
public class TrainSection
{
    // Existing
    public DispatchStretchDirection StretchDirection { get; }

    // New - segment-based tracking
    public int CurrentSegmentIndex { get; internal set; }
    public TrackStretch? CurrentSegment =>
        CurrentSegmentIndex < DispatchStretch.Segments.Count
            ? DispatchStretch.Segments[CurrentSegmentIndex]
            : null;
    public OperationalPlace? NextControlPoint =>
        CurrentSegment?.To as SignalControlledPlace ?? CurrentSegment?.To;
}
```

#### Step 2.2: Create adapter for BlockSignalPassage → Pass action
```csharp
// BlockSignalPassageAdapter.cs
internal static class BlockSignalPassageAdapter
{
    internal static ActionContext? ToPassAction(this BlockSignalPassage passage, IDispatcher dispatcher)
    {
        if (passage.IsExpected && passage.BlockSignal.ControlledBy.Id == dispatcher.Id)
        {
            var target = new SignalControlledPlace(
                passage.BlockSignal.Name,
                passage.BlockSignal.Name,
                passage.BlockSignal.ControlledBy)
            { IsJunction = passage.BlockSignal.IsJunction };

            return new ActionContext(passage.TrainSection, DispatchAction.Pass, dispatcher, target);
        }
        return null;
    }
}
```

#### Step 2.3: Update Broker to support both old and new patterns
- Add `IActionProvider` injection
- Keep existing `GetArrivalsFor`/`GetDeparturesFor` working
- Add new `GetActionsFor(IDispatcher)` method

#### Step 2.4: Update StationDispatcher
```csharp
// StationDispatcher.cs additions
public record StationDispatcher(Station Station, IBroker Broker) : IDispatcher
{
    // Existing
    public IEnumerable<TrainSection> Arrivals => Broker.GetArrivalsFor(Station, 10);
    public IEnumerable<TrainSection> Departures => Broker.GetDeparturesFor(Station, 10);

    // New - unified action view
    public IEnumerable<ActionContext> AvailableActions => Broker.GetActionsFor(this);
}
```

### Phase 3: Cleanup (Remove Old Code)

**Goal:** Remove deprecated structures and APIs.

#### Step 3.1: Remove BlockSignal (replaced by SignalControlledPlace)
- Delete `Layout/BlockSignal.cs`
- Update configurations to use SignalControlledPlace

#### Step 3.2: Remove BlockSignalPassage (replaced by segment tracking)
- Delete `BlockSignalPassage.cs`
- Delete `BlockSignalPassageState.cs`
- Remove `BlockSignalPassages` from TrainSection

#### Step 3.3: Simplify DispatchState
```csharp
// DispatchState.cs (simplified)
public enum DispatchState
{
    None,           // Not yet started
    Requested,      // Departure requested
    Accepted,       // Request accepted by arrival dispatcher
    InProgress,     // Train is on the stretch (was: Departed)
    Completed,      // Arrived at destination (was: Arrived)
    Canceled        // Canceled or aborted
}
// Removed: Rejected, Revoked, Passed (these are now actions or covered by returning to prior state)
```

#### Step 3.4: Remove dual-purpose state extensions
- Remove `NextArrivalStates`, `NextDepartureStates` from DispatchStateExtensions
- Remove `ArrivalStates`, `DepartureStates` from TrainSectionStateExtensions
- Use `IActionProvider.GetAvailableActions()` exclusively

#### Step 3.5: Update DispatchStretchDirection
- Remove direct BlockSignal references
- Use segment-based traversal

---

## Part 4: New Domain Model Summary

```
┌─────────────────────────────────────────────────────────────────────┐
│                        PHYSICAL LAYER                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  OperationalPlace (abstract)                                         │
│      ├── Station (Name, Signature, IsManned, Dispatcher, Tracks)    │
│      ├── SignalControlledPlace (Name, Signature, ControlledBy,      │
│      │                          IsJunction, Tracks)                  │
│      └── OtherPlace (Name, Signature)                               │
│                                                                      │
│  TrackStretch                                                        │
│      ├── From: OperationalPlace                                      │
│      ├── To: OperationalPlace                                        │
│      ├── Tracks: [Track]                                             │
│      │       └── Track (Number?, Direction, IsUpTrack, MaxLength?)  │
│      └── ActiveOccupancies: [TrackStretchOccupancy]                 │
│                                                                      │
│  StationTrack (Number, MaxLength?)                                   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                        LOGICAL LAYER                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  DispatchStretch                                                     │
│      └── Segments: [TrackStretch] (ordered sequence)                │
│          ├── Origin = Segments.First().From                         │
│          └── Destination = Segments.Last().To                       │
│                                                                      │
│  TrainSection                                                        │
│      ├── DispatchStretch                                            │
│      ├── Departure: TrainStationCall                                │
│      ├── Arrival: TrainStationCall                                  │
│      ├── State: DispatchState                                       │
│      └── CurrentSegmentIndex: int                                   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      OPERATIONAL LAYER                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  DispatchState                                                       │
│      None → Requested → Accepted → InProgress → Completed           │
│                 │                      │                             │
│                 └── Canceled ──────────┘                            │
│                                                                      │
│  DispatchAction                                                      │
│      Request, Accept, Reject, Revoke, Depart, Pass, Arrive, Clear  │
│      Manned, Running, Canceled, Aborted, Completed                  │
│                                                                      │
│  ActionContext                                                       │
│      └── (Section, Action, Dispatcher, PassTarget?)                 │
│                                                                      │
│  IActionProvider                                                     │
│      └── GetAvailableActions(section, dispatcher) → [ActionContext] │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────────┐
│                      CAPACITY MANAGEMENT                             │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  TrackStretchCapacityManager (singleton per Broker)                 │
│      ├── Tracks occupancy per TrackStretch                          │
│      ├── Cross-DispatchStretch blocking for shared segments         │
│      └── Direction validation (ForwardOnly, BackwardOnly, etc.)     │
│                                                                      │
│  Train meeting rules:                                                │
│      ├── At Station or SignalControlledPlace with multiple Tracks   │
│      ├── MaxLength check: Train.MaxLength ≤ StationTrack.MaxLength  │
│      └── Both trains must fit (each on a separate track)            │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Part 5: Key Behavioral Changes

### 1. Capacity is per TrackStretch, not per DispatchStretch

**Before:**
```csharp
dispatchStretch.HasFreeTrackFor(trainSection)
// Only checks this stretch's ActiveTrains
```

**After:**
```csharp
capacityManager.CanOccupy(trainSection, trackStretch, track)
// Checks ALL DispatchStretches that share this TrackStretch
```

### 2. Pass action is unified

**Before:**
```csharp
// Special handling in TrainSection
Dictionary<int, Func<bool>> BlockSignalPassageActions;
trainSection.GetBlockSignalActionsFor(dispatcher);
```

**After:**
```csharp
// Unified action model
actionProvider.GetAvailableActions(section, dispatcher)
    .Where(a => a.Action == DispatchAction.Pass)
    .Select(a => (a.PassTarget!.Name, a.Execute));
```

### 3. State and actions are decoupled

**Before:**
```csharp
// Same enum for state and available transitions
DispatchState[] ArrivalStates => trainSection.State switch { ... };
```

**After:**
```csharp
// Separate concepts
DispatchState currentState = section.State;
IEnumerable<DispatchAction> availableActions =
    actionProvider.GetAvailableActions(section, dispatcher)
        .Select(a => a.Action);
```

### 4. Double-track direction is explicit

**Before:**
```csharp
// Implicit: Forward direction and Reverse direction on same stretch
dispatchStretch.Forward  // A → B
dispatchStretch.Reverse  // B → A
```

**After:**
```csharp
// Explicit per track
trackStretch.Tracks = [
    new Track("1", TrackOperationDirection.ForwardOnly, IsUpTrack: true),   // For A→B
    new Track("2", TrackOperationDirection.BackwardOnly, IsUpTrack: false)  // For B→A
];
// Or Swedish model:
trackStretch.Tracks = [
    new Track("1", TrackOperationDirection.DoubleDirected, IsUpTrack: true),
    new Track("2", TrackOperationDirection.DoubleDirected, IsUpTrack: false)
];
```

### 5. SignalControlledPlace has two operational modes

A SignalControlledPlace operates differently depending on whether a train meet is scheduled:

**Mode A: Pass-through (no meeting)**
```
Train 101 ──► [Signal S1] ──►
                  │
                  └── Action: Pass
                      State: InProgress (unchanged)
```
- Single action: `Pass`
- Train does not stop
- Dispatcher confirms passage, train continues to next segment

**Mode B: Meeting point (trains crossing)**
```
Train 101 ──► [Signal S1] ◄── Train 202
                  │
         ┌───────┴───────┐
         │   Track 1     │   ← Train 101 arrives here
         │   Track 2     │   ← Train 202 arrives here
         └───────────────┘
```

State machine for meeting:
```
Train 101: InProgress → Arrive → [Wait] → Depart → InProgress
Train 202: InProgress → Arrive → [Wait] → Depart → InProgress
                              ↓
                    Both must Arrive before
                    either can Depart
```

**Implementation:**

```csharp
// SignalControlledPlace with meeting capability
public record SignalControlledPlace(string Name, string Signature, IDispatcher ControlledBy)
    : OperationalPlace(Name, Signature)
{
    public bool IsJunction { get; init; }

    // Tracks for meeting (inherited from OperationalPlace)
    // If Tracks.Count > 1, meeting is possible
    public bool CanHostMeeting => Tracks.Count > 1;
}

// Action determination based on meeting status
public IEnumerable<ActionContext> GetActionsForSignalControlledPlace(
    TrainSection section,
    SignalControlledPlace place,
    IDispatcher dispatcher)
{
    var otherTrainWaiting = GetOpposingTrainAt(place, section);

    if (place.CanHostMeeting && otherTrainWaiting != null)
    {
        // Meeting mode: use Arrive/Depart cycle
        if (section.IsApproaching(place))
            yield return new ActionContext(section, DispatchAction.Arrive, dispatcher, place);

        if (section.IsArrivedAt(place) && otherTrainWaiting.IsArrivedAt(place))
            yield return new ActionContext(section, DispatchAction.Depart, dispatcher, place);
    }
    else
    {
        // Pass-through mode: simple Pass action
        if (section.IsApproaching(place))
            yield return new ActionContext(section, DispatchAction.Pass, dispatcher, place);
    }
}
```

**Meeting constraints:**
- Both trains must `Arrive` at the SignalControlledPlace before either can `Depart`
- Each train occupies a separate StationTrack
- MaxLength validation applies: `Train.MaxLength ≤ StationTrack.MaxLength`
- The dispatcher controlling the SignalControlledPlace manages both arrivals and departures

This unifies the handling of:
- Station stops (always Arrive/Depart)
- Signal passages (Pass when no meet)
- Meeting points at signals (Arrive/Depart like a mini-station)

---

## Part 6: Migration Strategy

### Recommended Approach: Strangler Fig Pattern

1. **Phase 0** (1-2 days): Add new types alongside existing ones
2. **Phase 1** (3-5 days): Implement new action system in parallel
3. **Phase 2** (2-3 days): Migrate TrainSection to use new structures
4. **Phase 3** (1-2 days): Remove deprecated code

### Testing Strategy

1. Keep all existing tests passing during Phase 0-2
2. Add new tests for:
   - TrackStretch shared capacity scenarios
   - SignalControlledPlace with tracks (meeting point)
   - Track direction validation
   - MaxLength meeting constraints
   - ActionProvider returning correct actions
3. Phase 3: Update tests to remove deprecated API usage

### Breaking Changes

- `BlockSignal` → `SignalControlledPlace` (different constructor)
- `DispatchStretch` constructor (segments instead of stations + signals)
- `TrainSection.BlockSignalPassages` removed
- `ArrivalStates`/`DepartureStates` replaced by `GetAvailableActions()`

---

## Appendix: Open Questions

1. ~~**Meeting constraints**: Should both trains be required to fit at a meet point, or just one?~~
   **RESOLVED**: Yes, both trains must `Arrive` at the meeting point before either can `Depart`. Each train occupies a separate StationTrack. This applies to both Stations and SignalControlledPlaces with multiple tracks. See "Part 5, Section 5: SignalControlledPlace has two operational modes".

2. **Track selection**: When a TrackStretch has multiple tracks, how is track assignment determined?
   **PARTIALLY RESOLVED**:
   - **ForwardOnly track**: Automatically assigned to trains traveling in the TrackStretch's From→To direction
   - **BackwardOnly track**: Automatically assigned to trains traveling in the TrackStretch's To→From direction
   - **DoubleDirected tracks (bi-directional)**: Deferred for later implementation after other refactorings are in place. This covers the Swedish model where either track can be used in either direction.

3. ~~**OtherPlace usage**: Are there real scenarios where OtherPlace (non-controlled stops) is needed?~~
   **RESOLVED**: Yes, `OtherPlace` is needed for two scenarios:

   **A. Simple halt (IsJunction = false)**
   A passenger stop without signalling. Trains CAN stop here (scheduled stop for passengers), but there is no dispatch control. No special occupancy rules - train occupies only the current TrackStretch.

   **B. Unsignalled junction (IsJunction = true)**
   A junction point without signals. Trains CANNOT be stopped here, so cascading occupancy applies:

   **Cascading Occupancy Rule** (junctions only): When a train departs from a Station or SignalControlledPlace and the TrackStretch ends at an OtherPlace with `IsJunction = true`, ALL TrackStretches starting from that junction are also occupied. This applies recursively until reaching a Station or SignalControlledPlace.

   **Example: Unsignalled junction**
   ```
                        ┌───► B (Station)
   A (Station) ───► J (OtherPlace, IsJunction=true)
                        └───► C (Station)
   ```
   When train departs A toward B (Accepted), ALL stretches are occupied:
   - A→J (train's path)
   - J→B (train's path)
   - J→C (junction protection - no signals to stop conflicting moves)

   All remain occupied until Arrived at B.

   **Implementation:**
   ```csharp
   public void OccupyWithCascade(TrainSection section, TrackStretch initialStretch)
   {
       Occupy(section, initialStretch);

       if (initialStretch.To is OtherPlace { IsJunction: true } junction)
       {
           // Recursively occupy all stretches from this junction
           var connectedStretches = GetStretchesStartingFrom(junction);
           foreach (var stretch in connectedStretches)
           {
               OccupyWithCascade(section, stretch);
           }
       }
       // Stop recursion at Station, SignalControlledPlace, or non-junction OtherPlace
   }
   ```

4. ~~**Backward compatibility**: Should the Broker support both old and new configuration formats during transition?~~
   **RESOLVED**: No dual format support needed. An existing database already has the new structure:
   - `OperationalPlaces` (may reference controlling Station)
   - `TrackStretches` (reference Start and End OperationalPlace)
   - `DispatchStretches` (reference From and To Station)

   **Import rules:**
   - Each object must be a single instance (no duplicates)
   - OperationalPlaces are loaded first (no dependencies)
   - TrackStretches are loaded next (depend on OperationalPlaces)
   - DispatchStretches are loaded last (depend on Stations)

   **Control relationship:**
   - In data/storage: `OperationalPlace.ControlledByStation` → optional reference to the Station that controls it
   - At runtime: `IDispatcher.ControlledPlaces` → derived list of OperationalPlaces controlled by this dispatcher

   ```csharp
   // Data model (for import)
   public abstract record OperationalPlace(string Name, string Signature)
   {
       public Station? ControlledByStation { get; init; }  // Set during import
   }

   // Runtime (derived during Broker init)
   public record StationDispatcher(Station Station, IBroker Broker) : IDispatcher
   {
       public IEnumerable<OperationalPlace> ControlledPlaces =>
           Broker.GetPlacesControlledBy(this);
   }
   ```

   **TrackStretch sequence discovery:**
   Use a **shortest path algorithm** to automatically find the ordered sequence of TrackStretches for a DispatchStretch, given only the From and To stations.

   ```csharp
   public IList<TrackStretch> FindPath(Station from, Station to, IEnumerable<TrackStretch> allStretches)
   {
       // Build graph from TrackStretches
       // Use Dijkstra or BFS to find shortest path from 'from' to 'to'
       // Return ordered list of TrackStretches
   }
   ```

   This eliminates the need to explicitly configure the segment sequence - it's derived from the graph topology.

5. ~~**Rejected/Revoked states**: The simplified DispatchState removes these as explicit states.~~
   **RESOLVED**: Keep `Rejected` and `Revoked` as distinct states. They provide important information:
   - `Rejected`: Arrival dispatcher declined the request (capacity issue, conflict, etc.)
   - `Revoked`: Departure dispatcher withdrew after acceptance (train delayed, canceled, etc.)

   **Updated DispatchState:**
   ```csharp
   public enum DispatchState
   {
       None,           // Not yet started
       Requested,      // Departure requested
       Accepted,       // Request accepted by arrival dispatcher
       Rejected,       // Request rejected by arrival dispatcher
       Revoked,        // Accepted request revoked by departure dispatcher
       InProgress,     // Train is on the stretch (departed)
       Completed,      // Arrived at destination
       Canceled        // Train canceled or aborted
   }
   ```
