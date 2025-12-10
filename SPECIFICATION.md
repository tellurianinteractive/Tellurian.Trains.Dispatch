# Dispatcher User Interface Specification

This document provides a comprehensive analysis and specification for implementing a web-based dispatcher user interface for the Tellurian.Trains.Dispatch library.

## Table of Contents

1. [Requirements Summary](#requirements-summary)
2. [Architecture Analysis](#architecture-analysis)
3. [Recommended Architecture](#recommended-architecture)
4. [Domain Model Overview](#domain-model-overview)
5. [API Specification](#api-specification)
6. [SSE Event Specification](#sse-event-specification)
7. [External System Integration](#external-system-integration)
8. [Blazor Client Implementation](#blazor-client-implementation)
9. [Project Structure](#project-structure)
10. [Deployment Scenario](#deployment-scenario)
11. [Implementation Phases](#implementation-phases)

---

## Requirements Summary

### Core Requirements

1. **Web API Access** - Enable external systems (signaling systems, automated controls) to interact with the dispatch system through a standardized API.
2. **Real-Time Updates** - All connected dispatchers must see state changes immediately when any dispatcher performs an action.
3. **Blazor Implementation** - User interface built with Microsoft Blazor.
4. **SSE for Real-Time** - Use .NET 10's Server-Sent Events (SSE) support via `TypedResults.ServerSentEvents`.
5. **.NET 10 Minimal API** - Backend implemented using .NET 10 minimal API pattern.

### User Stories

- As a **station dispatcher**, I want to see arriving and departing trains for my station and perform dispatch actions (request, accept, reject, departed, arrived).
- As a **signal controller**, I want to mark train passages through my controlled signal-controlled places.
- As an **external system integrator**, I want to call API endpoints to automate dispatch operations.
- As any **dispatcher**, I want to see real-time updates when other dispatchers take actions affecting my view.

---

## Architecture Analysis

### Option A: Blazor Interactive Server (No Web API)

```
┌──────────────────┐     SignalR      ┌─────────────────────┐
│  Blazor Server   │◄────────────────►│    ASP.NET Core     │
│   (Browser UI)   │                  │   + Broker Service  │
└──────────────────┘                  └─────────────────────┘
```

**Pros:** Simpler architecture, direct DI access to Broker, fast initial load.

**Cons:** No API for external integrations (violates requirement #1), higher server resources per user, server restart loses all connections.

### Option B: Blazor WebAssembly + Web API + SSE (Recommended)

```
┌──────────────────┐                  ┌─────────────────────┐
│  Blazor WASM     │─────HTTP/SSE────►│   Minimal API       │
│   (Browser UI)   │                  │   + SSE Endpoint    │
└──────────────────┘                  │   + Broker Service  │
                                      └─────────────────────┘
┌──────────────────┐                           ▲
│ External Systems │───────HTTP/SSE────────────┘
│ (Signaling, etc) │
└──────────────────┘
```

**Pros:** Single unified API, stateless server, lightweight SSE connections, clear separation of concerns.

**Cons:** Larger initial download (~2-4MB WASM runtime), unidirectional SSE.

### Option C: Blazor WebAssembly + Web API + SignalR

**Pros:** Bidirectional real-time, rich features.

**Cons:** More complexity than needed, WebSocket may be blocked by proxies.

### Why We Chose Option B

1. **Unified API** - The same endpoints serve both the Blazor app and external integrations. A signaling system and a human dispatcher use identical API calls.

2. **SSE vs SignalR** - For our use case, dispatchers only need to *receive* updates (unidirectional). Actions are separate HTTP POSTs. SSE provides exactly this with less overhead than SignalR's bidirectional WebSocket connections.

3. **Scalability** - WASM runs client-side, so the server only handles API requests. No persistent server-side state per user.

4. **External Integration** - Physical signaling systems, display boards, and automation controllers need HTTP API access regardless. With Option B, there's one API to learn.

### Why Minimal API over Controllers

- **Less ceremony** - No controller classes, attributes, or conventions to learn
- **Better performance** - Slightly lower overhead than MVC
- **Modern pattern** - .NET 10's recommended approach for APIs
- **Clarity** - Each endpoint is self-contained and easy to understand

---

## Recommended Architecture

### Overview

```
┌────────────────────────────────────────────────────────────────────┐
│                        Web Server (.NET 10)                        │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                      Minimal API Layer                       │  │
│  │  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────┐   │  │
│  │  │ REST API    │  │ SSE Stream  │  │ Static Files        │   │  │
│  │  │ Endpoints   │  │ Endpoint    │  │ (Blazor WASM)       │   │  │
│  │  └──────┬──────┘  └──────┬──────┘  └─────────────────────┘   │  │
│  └─────────┼────────────────┼───────────────────────────────────┘  │
│            │                │                                      │
│  ┌─────────▼────────────────▼───────────────────────────────────┐  │
│  │                    Application Services                      │  │
│  │  ┌─────────────────┐  ┌─────────────────────────────────┐    │  │
│  │  │ Broker          │  │ DispatchEventService            │    │  │
│  │  │ (Singleton)     │  │ (Event Broadcasting)            │    │  │
│  │  └─────────────────┘  └─────────────────────────────────┘    │  │
│  └──────────────────────────────────────────────────────────────┘  │
└────────────────────────────────────────────────────────────────────┘
         ▲                           ▲
         │ HTTP + SSE                │ HTTP + SSE
         │                           │
┌────────┴────────┐         ┌────────┴────────┐
│ External Systems│         │ Blazor WASM     │
│ (Signals, etc.) │         │ (Dispatcher UI) │
└─────────────────┘         └─────────────────┘
```

### Components

| Project | Purpose |
|---------|---------|
| **Tellurian.Trains.Dispatch** | Existing domain library (unchanged) |
| **Tellurian.Trains.Dispatch.Server** | ASP.NET Core Minimal API host |
| **Tellurian.Trains.Dispatch.Client** | Blazor WebAssembly application |
| **Tellurian.Trains.Dispatch.Shared** | Shared DTOs and contracts |

---

## Domain Model Overview

The domain model separates physical infrastructure from logical dispatch routes.

### Physical Infrastructure

**OperationPlace** is the abstract base for all locations:

| Type | Description | Controlled |
|------|-------------|------------|
| **Station** | Manned/unmanned location with dispatcher | Yes |
| **SignalControlledPlace** | Block signal, passing loop, or junction | Yes |
| **OtherPlace** | Simple halt or unsignalled junction | No |

**TrackStretch** represents physical track between two adjacent OperationPlaces:
- Contains one or more `Track` objects (single-track, double-track)
- Manages `ActiveOccupancies` for capacity control
- Tracks have direction constraints (`TrackOperationDirection`)

### Logical Dispatch Routes

**DispatchStretch** defines the logical route between two stations:
- Spans one or more TrackStretches
- Supports bidirectional operation via `Forward` and `Reverse` directions
- May include intermediate SignalControlledPlaces

### Trains and Sections

- **Train** - Identified by Company and Identity (e.g., "SJ IC 123")
- **TrainStationCall** - A scheduled arrival/departure at a specific location
- **TrainSection** - Movement across a DispatchStretch, tracking:
  - Current `DispatchState`
  - Current `TrackStretchIndex` position
  - Departure and arrival calls
  - `Previous` section reference (null for first section)

#### TrainSection Sequencing

TrainSections for the same train are linked via the `Previous` property, forming a chain that represents the train's complete journey:

```
Section 1 (Previous=null) → Section 2 → Section 3 → ... → Section N
```

This linking enables important business rules:
- **First section**: Where Manned/Canceled actions are available
- **Subsequent sections**: Dispatch actions only available after Previous section has departed
- **Sequential progression**: A train must be on its way before the next TrainSection becomes actionable

### State Machines

**TrainState** - Operational lifecycle:
```
Planned → Manned → Running → Completed
   ↓         ↓         ↓
Canceled  Canceled  Aborted
```

**Note:** Train state actions (Manned, Canceled) are only available on the first TrainSection. On subsequent sections, only Aborted is available. Manned, Canceled, and Aborted states can be undone to revert to the previous state (see Undo Train State section below).

**DispatchState** - Dispatch authorization:
```
None → Requested → Accepted → Departed → [Pass*] → Arrived
                 → Rejected ↺ Requested
       Accepted  → Revoked  ↺ Requested
```

### Action System

The `ActionStateMachine` determines available actions based on:
- Current DispatchState and TrainState
- Dispatcher role (departure, arrival, or signal controller)
- Train section position (track stretch index)
- Previous section state (for non-first sections)

#### Dispatch Actions

| Action | Performed By | When Available |
|--------|--------------|----------------|
| Request | Departure dispatcher | State is None, Rejected, or Revoked; Previous section departed (or first section) |
| Accept | Arrival dispatcher | State is Requested; Previous section departed (or first section) |
| Reject | Arrival dispatcher | State is Requested; Previous section departed (or first section) |
| Revoke | Departure dispatcher | State is Requested or Accepted; Previous section departed (or first section) |
| Revoke | Arrival dispatcher | State is Accepted; Previous section departed (or first section) |
| Depart | Departure dispatcher | State is Accepted; Previous section departed (or first section) |
| Pass | Signal controller | Departed, not on last track stretch; Previous section departed (or first section) |
| Arrive | Arrival dispatcher | Departed and on last track stretch; Previous section departed (or first section) |
| Clear | Any dispatcher | Train canceled/aborted while Departed |

#### Train State Actions

| Action | Performed By | When Available |
|--------|--------------|----------------|
| Manned | Any dispatcher | First section only; TrainState is Planned |
| Canceled | Any dispatcher | First section only; TrainState is Planned or Manned |
| Running | Any dispatcher | First section only; TrainState is Manned |
| Aborted | Any dispatcher | Non-first sections only; TrainState is Running |
| UndoTrainState | Any dispatcher | TrainState is Manned, Canceled, or Aborted |

#### Undo Train State

The UndoTrainState action allows dispatchers to correct mistakes by reverting to the previous state:

| Current State | Previous State | Reverts To | Display Name |
|---------------|----------------|------------|--------------|
| Manned | Planned | Planned | "Undo Manned" |
| Canceled | Planned | Planned | "Undo Canceled" |
| Canceled | Manned | Manned | "Undo Canceled" |
| Aborted | Running | Running | "Undo Aborted" |

**Key behaviors:**
- Only one level of undo is supported (the most recent state change)
- After undo, the `PreviousState` is cleared and undo is no longer available
- The `DisplayName` property on `ActionContext` provides the appropriate UI label (e.g., "Undo Manned")
- Undo is not available for Running or Completed states

---

## API Specification

### Design Philosophy: HATEOAS

The API follows HATEOAS (Hypermedia as the Engine of Application State) principles:

**Why HATEOAS?**
1. **Clients don't construct URLs** - They follow links provided by the server, eliminating URL string manipulation bugs
2. **Server controls available actions** - The state machine logic lives only on the server
3. **Self-documenting** - Clients render what they receive without encoding business rules
4. **Evolvability** - Server can change URL structure without breaking clients

This is particularly valuable for our action-based dispatch system. Instead of clients knowing "in state X, action Y is valid," the server simply provides the actions that are valid right now.

### Base URL

```
/api/v1
```

### Dispatcher Endpoints

#### List Dispatchers
```http
GET /api/v1/dispatchers
```
Returns all station dispatchers with navigation links.

#### Get Dispatcher with Train Sections
```http
GET /api/v1/dispatchers/{id}
```
Returns dispatcher with arrivals and departures, each including:
- Train identification and schedule
- Current state and track stretch progress
- All available actions as clickable hrefs (dispatch, train state, and pass actions unified)

**Key Response Fields:**
```json
{
  "arrivals": [{
    "id": 42,
    "train": "SJ IC 456",
    "state": "Departed",
    "currentTrackStretch": 2,
    "totalTrackStretches": 3,
    "actions": [{ "name": "arrive", "displayName": "Arrive", "href": "..." }]
  }],
  "departures": [{
    "id": 38,
    "trainState": "Manned",
    "state": "None",
    "actions": [
      { "name": "request", "displayName": "Request", "href": "..." },
      { "name": "running", "displayName": "Running", "href": "..." },
      { "name": "undoTrainState", "displayName": "Undo Manned", "href": "..." }
    ]
  }]
}
```

### Action Endpoints

All actions are performed via POST to the href provided in the response:

```http
POST /api/v1/train-sections/{id}/actions/{action}
```

For pass actions that target a specific control point:
```http
POST /api/v1/train-sections/{id}/actions/pass/{controlPointIndex}
```

**Why POST for Actions?**
- Actions have side effects (state changes)
- Not idempotent - calling "depart" twice should fail
- Returns updated state with new available actions

### SSE Event Stream
```http
GET /api/v1/events
Accept: text/event-stream
```

Optional filtering: `?dispatcherId=2`

---

## SSE Event Specification

### Why SSE over SignalR?

| Aspect | SSE | SignalR |
|--------|-----|---------|
| Direction | Server → Client | Bidirectional |
| Protocol | Standard HTTP | WebSocket (with fallbacks) |
| Complexity | Simple | More complex |
| Proxy support | Excellent | May be blocked |
| Our need | Receive updates | Only receive updates |

Since dispatchers need to *receive* updates but *send* actions via separate HTTP calls, SSE's unidirectional nature is a perfect fit.

### Event Types

| Event | Description | Use Case |
|-------|-------------|----------|
| `DispatchStateChanged` | Train section state transition | Refresh affected dispatcher views |
| `TrainStateChanged` | Train operational state change | Update train status displays |
| `ControlPointPassed` | Train passed a SignalControlledPlace | Update progress indicators |
| `TrainSectionCleared` | Canceled train cleared from stretch | Remove from active views |

### Event Data

Each event includes:
- Timestamp
- Identifiers (train, section, dispatcher)
- Previous and new state
- Affected dispatchers for filtering

### Server Implementation Pattern

```csharp
// Use Channel for efficient pub/sub
var channel = Channel.CreateBounded<DispatchEvent>(100);

// SSE endpoint streams from channel
app.MapGet("/api/v1/events", (int? dispatcherId, CancellationToken ct) =>
    TypedResults.ServerSentEvents(
        GetFilteredEventsAsync(dispatcherId, ct)));
```

---

## External System Integration

### Use Cases

External systems connect via the same API as the Blazor client:

| System | SSE Events | REST API |
|--------|------------|----------|
| **Signal controller** | `DispatchStateChanged` → update aspects | - |
| **Block detector** | `ControlPointPassed` | Auto-confirm `pass` action |
| **Display board** | All events | - |
| **Automation** | Monitor state | Execute actions |

### Why This Works

1. **Single API** - No separate "machine API" vs "human API"
2. **Standard protocols** - HTTP + SSE work with any language
3. **Event filtering** - Systems subscribe only to relevant events

### Typical Integration Pattern

1. Connect to SSE stream (filtered by dispatcher if relevant)
2. React to events (update signals, displays, etc.)
3. Optionally POST actions for automation

---

## Blazor Client Implementation

### Why Blazor WebAssembly?

| Aspect | Motivation |
|--------|------------|
| **Unified API** | Uses same endpoints as external systems |
| **Client-side rendering** | Server resources independent of user count |
| **Modern C#** | Share domain knowledge with server code |
| **Progressive loading** | Initial WASM download cached by browser |

### Design Principles

#### 1. Follow HATEOAS Links
The client never constructs URLs. It renders action buttons from the hrefs provided by the server:

```csharp
// Server provides: { "name": "request", "href": "/api/v1/train-sections/38/actions/request" }
// Client just POSTs to the href when button clicked
```

**Why?** State machine logic stays on server. Client is a pure renderer.

#### 2. Simple Refresh Pattern
When any SSE event arrives, refresh the dispatcher data:

```csharp
Events.OnEventReceived += () => RefreshDispatcherDataAsync();
```

**Why?** Instead of complex local state reconciliation, just re-fetch from source of truth. Network latency is acceptable for our use case.

#### 3. Optimistic UI (Optional)
For smoother UX, immediately update after action then confirm with server response:

```csharp
async Task HandleAction(string href)
{
    // Optimistic: disable button immediately
    // Real: await API call
    // Confirm: SSE triggers full refresh
}
```

### Component Structure

| Component | Responsibility |
|-----------|----------------|
| **DispatcherPage** | Fetch and display arrivals/departures |
| **TrainSectionCard** | Single train with state and actions |
| **ActionButton** | Renders and handles action href |

### State Management

**No complex state management library needed.** The pattern is simple:
1. Page fetches dispatcher data on load
2. Components render from that data
3. Actions POST to server
4. SSE events trigger re-fetch

This works because the Broker is the single source of truth. The client is just a view.

---

## Project Structure

```
Tellurian.Trains.Dispatch.sln
├── Tellurian.Trains.Dispatch/              # Domain library
│   ├── Brokers/
│   ├── Layout/
│   └── Trains/
│
├── Tellurian.Trains.Dispatch.Server/       # Minimal API host
│   ├── Program.cs
│   ├── Endpoints/
│   │   ├── DispatcherEndpoints.cs
│   │   ├── TrainSectionEndpoints.cs
│   │   └── EventEndpoints.cs
│   └── Services/
│       └── DispatchEventService.cs
│
├── Tellurian.Trains.Dispatch.Client/       # Blazor WebAssembly
│   ├── Program.cs
│   ├── Pages/
│   ├── Components/
│   └── Services/
│
├── Tellurian.Trains.Dispatch.Shared/       # DTOs
│   ├── DispatcherDto.cs
│   ├── TrainSectionDto.cs
│   └── DispatchEvent.cs
│
└── Tellurian.Trains.Dispatch.Tests/
```

---

## Deployment Scenario

### Local Network Operation

The application is designed for **local network deployment**:

```
┌───────────────────────────────────────────────────────────┐
│                    Closed WiFi Network                    │
│                   (Password Protected)                    │
│                                                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │ Dispatcher  │  │ Dispatcher  │  │ Signal      │        │
│  │ Tablet/PC   │  │ Tablet/PC   │  │ Controller  │        │
│  └──────┬──────┘  └──────┬──────┘  └──────┬──────┘        │
│         │                │                │               │
│         └────────────────┼────────────────┘               │
│                          │                                │
│                   ┌──────▼──────┐                         │
│                   │ Server PC   │                         │
│                   │ (API Host)  │                         │
│                   └─────────────┘                         │
└───────────────────────────────────────────────────────────┘
```

### Security Model

- **Network-level security** - WiFi password protects the network
- **No application authentication** - All network clients are trusted
- **Open API** - Any device on the network can call endpoints

**Why no app-level auth?** This is for model railway operations in trusted environments (clubs, home layouts, exhibitions). Adding authentication would complicate setup without benefit.

### Input Validation

Even without authentication, validate all inputs using FluentValidation:
- Protects against malformed requests
- Clear error messages for debugging
- Testable validation logic

---

## Implementation Phases

### Phase 1: API Foundation
1. Create solution structure
2. Implement `DispatchEventService` for event broadcasting
3. Create REST endpoints for dispatchers and train sections
4. Add SSE endpoint
5. Write integration tests

### Phase 2: Blazor Client
1. Set up Blazor WebAssembly project
2. Implement API client service
3. Implement SSE event client
4. Create dispatcher page with arrivals/departures
5. Create train section card component

### Phase 3: Signal Control
1. Add pass action support
2. Implement track stretch progress tracking
3. Add control point events to SSE
4. Display progress in UI

### Phase 4: Polish
1. Error handling and retry logic
2. Connection status indicators
3. Performance optimization
4. Deployment configuration

---

## Summary

| Component | Technology | Why |
|-----------|------------|-----|
| Backend API | .NET 10 Minimal API | Modern, performant, less ceremony |
| Real-time | Server-Sent Events | Simple, fits unidirectional need |
| Client UI | Blazor WebAssembly | Unified API, client-side rendering |
| API Design | HATEOAS | Server controls actions, clients follow links |

**Key Design Decisions:**
1. **HATEOAS API** - State machine logic on server only
2. **SSE over SignalR** - Simpler for unidirectional updates
3. **Blazor WASM** - Same API for humans and machines
4. **Refresh pattern** - Re-fetch on events, not local state sync
5. **No authentication** - Trust the network for model railway use
