# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET library for dispatching trains between stations on a model railway.
- The **Broker** is the central singleton component holding state of each train's calls at specific stations.
- A **Dispatcher** presents state of arriving and departing trains. The **StationDispatcher** presents state for a named station.

## Domain Model Concepts

### Core Entities
- **Train** - Identified by Company and Identity (e.g., "SJ IC 123"). Has a collection of TrainStretch sections.
- **Station** - A named location with a signature. Can be manned or unmanned.
- **StationTrack** - A specific track/platform at a station.
- **DispatchStretch** - The track section between two adjacent stations. Has capacity (single/double track) and optional block signals.
- **TrainStretch** - A train's scheduled movement over a DispatchStretch, with departure and arrival calls.
- **TrainStationCall** - A scheduled stop: train, station, track, and call times.
- **BlockSignal** - Intermediate signal point on a DispatchStretch, enabling finer capacity control.

### State Machines
- **TrainState**: Planned → Manned → Running → [Completed | Canceled | Aborted]
- **DispatchState**: None → Requested → [Accepted | Rejected] → Departed → Arrived (or Accepted → Revoked)

## Architecture Patterns

- **Extension Methods** - Behavior is organized into extension method classes for separation of concerns.
- **Option<T>** - Generic error handling for operations that can fail with messages.
- **Dependency Injection Ready** - Core interfaces (IBrokerConfiguration, IBrokerStateProvider, ITimeProvider) allow flexible integration.
- **Thread-Safe ID Generation** - Auto-incrementing IDs use Interlocked for safety.

## Component Relationships

Broker (singleton) manages:
- Collection of DispatchStretch (track sections)
- Collection of StationDispatcher (one per station)
- TrainStretch instances representing active dispatching

StationDispatcher exposes arrivals and departures for its station by querying the Broker.

## .NET Version
This project targets .NET 10.0 and the .NET 10 SDK. This is only defined globally in Directory.Build.props.
Test projects should use MSTest.Sdk, the Microsoft Testing Platform and NOT VS Test.

## Documentation
There is a README in the project, outlining the details and intended use.
