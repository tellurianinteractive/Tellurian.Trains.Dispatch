# Tellurian.Trains.Dispatch

A .NET library for dispatching trains between stations on a model railway.

## Overview

This library provides the core domain model and business logic for managing train dispatching operations. It tracks trains as they move between stations, manages dispatch requests and approvals, and handles capacity constraints on track sections.

The library is designed to be integrated into larger applications that provide the user interface, persistence, and time management.

## Core Concepts

### Broker
The central singleton component that maintains the state of all trains and their station calls. It manages track sections (DispatchStretch) and station dispatchers, providing a unified view of the railway network.

### Station and StationDispatcher
A Station represents a named location on the railway with one or more tracks. Each station has an associated StationDispatcher that presents arrivals and departures for that station.

### Train
A train is identified by its operating company and identity (train number). Each train has a sequence of TrainSection sections representing its journey across the network.

### DispatchStretch
The track section between two adjacent stations. A DispatchStretch has:
- Capacity defined by number of tracks (single, double, etc.)
- Optional intermediate block signals for finer capacity control
- Support for bidirectional operation

### TrainSection
Represents a train's scheduled movement over a DispatchStretch, connecting a departure call at one station to an arrival call at the next. This is the primary unit for dispatch operations.

### Block Signals
Intermediate signal points on a DispatchStretch that divide the stretch into blocks (segments). When present, they allow multiple trains to occupy the same stretch simultaneously, but only one train per block:
- N block signals create N+1 blocks (e.g., 2 signals = 3 blocks)
- A train occupies one block at a time, tracked by its current block index
- When a train passes a block signal, it moves to the next block, freeing the previous one
- Each block signal is controlled by a dispatcher who confirms when a train passes
- Block signals can be located at junctions where tracks diverge

### Block Occupancy
The library tracks which block each train currently occupies:
- **Block 0**: From the departure station to the first block signal
- **Block 1...N-1**: Between consecutive block signals
- **Block N**: From the last block signal to the arrival station

A train's current block index is derived from the count of passed block signal passages. This enables:
- Capacity enforcement: only one train per block in the same direction
- Progress tracking: dispatchers see which block each train occupies
- Safe following: the next train can only enter a block after the previous train clears it

## Train Lifecycle

A train progresses through these states:

1. **Planned** - Initial state when the train is scheduled
2. **Manned** - Crew has been assigned and train is ready
3. **Running** - Train is actively operating
4. **Completed** - Train has finished its journey

Alternative endings: **Canceled** (before running) or **Aborted** (during operation)

## Dispatch Workflow

Each TrainSection follows this dispatch workflow:

1. **Requested** - Departure dispatcher requests permission to dispatch
2. **Accepted** or **Rejected** - Arrival dispatcher responds based on capacity and conditions
3. **Departed** - Train has left the departure station (occupies Block 0)
4. **Block Signal Passages** - If block signals exist, the controlling dispatcher marks each passage in sequence
5. **Arrived** - Train has arrived at the destination station (only available after all block signals passed)

An accepted request can be **Revoked** before departure if circumstances change.

### Dispatcher Actions

The library uses a state machine pattern where each dispatcher is presented only the actions valid for the current state:

**Departure Dispatcher**:
- Request dispatch when state is None, Rejected, or Revoked
- Mark as Departed when Accepted
- Revoke a request when Requested or Accepted

**Arrival Dispatcher**:
- Accept or Reject when Requested
- Mark as Arrived when Departed and all block signals passed

**Block Signal Dispatcher** (for signals they control):
- Mark passage when train is in the block before this signal (current block index matches signal index)

### Capacity Rules

Before accepting a dispatch request, the system checks capacity:
- **Single track**: No trains in the opposite direction allowed
- **All tracks**: Block 0 must be free (no train in the first segment moving in the same direction)

### Handling Canceled/Aborted Trains

If a train is canceled or aborted while on the stretch (state is Departed):
- The train must be manually cleared using the ClearFromStretch action
- Clearing removes the train from active trains and frees the block it occupies
- All expected block signal passages are marked as Canceled

## Integration Interfaces

To integrate this library, implement these interfaces:

### IBrokerConfiguration
Provides initial data loading:
- Station definitions
- Track stretch definitions
- Block signal definitions
- Scheduled train station calls

### IBrokerStateProvider
Handles persistence:
- Save current dispatch state
- Restore state on restart

### ITimeProvider
Supplies the current time (typically from a fast clock for model railway operation).

## License

This library is licensed under GPL-3.0.
