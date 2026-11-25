# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET library for dispatching trains between stations on a model railway.
- The **Broker** is the central singleton component holding state of each train's calls at specific stations.
- A **Dispatcher** presents state of arriving and departing trains. The **StationDispatcher** presents state for a named station.

## Domain Model Concepts

### Core Entities
- **Train** - Identified by Company and Identity (e.g., "SJ IC 123"). Has a collection of TrainSection sections.
- **Station** - A named location with a signature. Can be manned or unmanned.
- **StationTrack** - A specific track/platform at a station.
- **DispatchStretch** - The track section between two adjacent stations. Has capacity (single/double track) and optional block signals.
- **TrainSection** - A train's scheduled movement over a DispatchStretch, with departure and arrival calls.
- **TrainStationCall** - A scheduled stop: train, station, track, and call times.
- **BlockSignal** - Intermediate signal point on a DispatchStretch, enabling finer capacity control.

### State Machines
- **TrainState**: Planned → Manned → Running → [Completed | Canceled | Aborted]
- **DispatchState**: None → Requested → [Accepted | Rejected] → Departed → [PassBlockSignal*] → Arrived (or Accepted → Revoked)
- **BlockPassageState**: Expected → Passed (or Canceled)

### Block Signal Handling
A DispatchStretch can be divided into blocks by intermediate block signals:
- **N block signals** create **N+1 blocks** (segments)
- Each block can hold only **one train** at a time
- Trains must pass block signals **in sequence**
- A train's current block index = count of passed BlockSignalPassages
- Arrival is only allowed after **all block signals have been passed**

Block occupancy determines capacity:
- When a train departs, it occupies Block 0 (station to first signal)
- When a dispatcher marks the train as passed a signal, it moves to the next block
- The previous block is then free for the next train

The dispatcher who controls a block signal (`BlockSignal.ControlledBy`) is presented with the action to mark the passage.

### Rules come from the timetable model
Plan rules belong in `Tellurian.Trains.Schedules` (Model / Model.Layouts), as extension blocks on the existing types,
and are only *consumed* here. Dispatch adds only rules that are solely about dispatching. Rules taken from the model:
- who has a dispatcher (`HasDispatcher`), who works a place from afar (`Dispatcher`, `IsDispatchEndpoint`);
- which sessions/days a train runs on, including the first session's start (`plan.SessionNumbersOf(train)`):
  with `RunsOverMidnight`, session 1 excludes trains of driver duties starting before `FirstSessionStartTime`;
- who clears each part of a train's run (`train.DispatchLegs`: BetweenDispatchers, OneDispatcher, DriverCalls);
- the train's company (`EffectiveCompany`) and the session start (`SessionStart`).

### Operating Day
`IBrokerDataProvider.GetScheduleStartTimeAsync` gives the fast clock time the operating day starts at (default 00:00).
Dispatchers' arrivals and departures are ordered by scheduled time within that day (`InOperatingDayStartingAt`):
times before the start count as after midnight, so with a 05:00 start, 00:30 sorts after 23:30.
A train running on several sessions/days is one `Train` per session (`Train.OperatingDay`), each with its own
train and dispatch state; scheduled times are within the whole period, so day 2 at 05:20 is `1.05:20`.

### Train Section Kinds
A data provider may give the broker `TrainSectionDefinition`s (`GetTrainSectionsAsync`); otherwise the broker creates
Normal sections between consecutive calls at the stations of a `DispatchStretch`. A section's `TrainSectionKind` and its
`FromStation`/`ToStation` decide the roles (`RequestingDispatcher`, `AcceptingDispatcher`, `DepartingDispatcher`,
`ArrivingDispatcher`, and which lists it is in):
- **Normal** - between two dispatchers at the places the train departs from and arrives at.
- **Leaving / Entering** - the train ends/starts or leaves/joins the line at a place along it without a dispatcher;
  still cleared between the dispatchers at both ends: the train's dispatcher requests, departs and marks the
  arrival; the other end accepts/rejects.
- **OneDispatcherOutbound / OneDispatcherInbound** - cleared by one dispatcher (a branch, an end where the driver works
  the points, a place the dispatcher works alone). Outbound: done when departed. Inbound: the driver phones;
  accepting is the go (state Departed), then arrive.

Only signals a dispatcher controls divide a section into blocks; places without (industrial areas, unmanned stations,
driver-worked signals) are passed automatically. The data provider presents a leg a dispatcher clears alone up to a
junction they work from afar together with the leg beyond it (e.g. Gårdsjö → Tjärby → Rotebro is one Normal section).

## Architecture Patterns

- **Extension Methods** - Behavior is organized into extension method classes for separation of concerns.
- **Option<T>** - Generic error handling for operations that can fail with messages.
- **Dependency Injection Ready** - Core interfaces (IBrokerConfiguration, IBrokerStateProvider, ITimeProvider) allow flexible integration.
- **Thread-Safe ID Generation** - Auto-incrementing IDs use Interlocked for safety.

## Component Relationships

Broker (singleton) manages:
- Collection of DispatchStretch (track sections)
- Collection of StationDispatcher (one per station)
- TrainSection instances representing active dispatching

StationDispatcher exposes arrivals and departures for its station by querying the Broker.

## .NET Version
This project targets .NET 11.0 and the .NET 11 SDK. This is only defined globally in Directory.Build.props.
Test projects should use MSTest.Sdk, the Microsoft Testing Platform and NOT VS Test.

## Documentation
There is a README in the project, outlining the details and intended use.
