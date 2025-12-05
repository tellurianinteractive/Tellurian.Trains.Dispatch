# Dispatcher User Interface Specification

This document provides a comprehensive analysis and specification for implementing a web-based dispatcher user interface for the Tellurian.Trains.Dispatch library.

## Table of Contents

1. [Requirements Summary](#requirements-summary)
2. [Architecture Analysis](#architecture-analysis)
3. [Real-Time Communication Options](#real-time-communication-options)
4. [Blazor Hosting Model Comparison](#blazor-hosting-model-comparison)
5. [Recommended Architecture](#recommended-architecture)
6. [API Specification](#api-specification)
7. [SSE Event Specification](#sse-event-specification)
8. [External System Integration](#external-system-integration)
9. [Blazor Client Implementation](#blazor-client-implementation)
10. [Project Structure](#project-structure)
11. [Deployment Scenario](#deployment-scenario)
12. [Implementation Phases](#implementation-phases)

---

## Requirements Summary

### Core Requirements

1. **Web API Access** - Enable external systems (signaling systems, block signals, automated controls) to interact with the dispatch system through a standardized API.
2. **Real-Time Updates** - All connected dispatchers must see state changes immediately when any dispatcher performs an action.
3. **Blazor Implementation** - User interface built with Microsoft Blazor.
4. **SSE for Real-Time** - Use .NET 10's new Server-Sent Events (SSE) support via `TypedResults.ServerSentEvents`.
5. **.NET 10 Minimal API** - Backend implemented using .NET 10 minimal API pattern.

### User Stories

- As a **station dispatcher**, I want to see arriving and departing trains for my station and perform dispatch actions (request, accept, reject, departed, arrived).
- As a **block signal operator**, I want to mark train passages through my controlled signals.
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

**Pros:**
- Simpler architecture - single project
- Direct access to Broker through DI
- Built-in real-time via SignalR circuit
- Smaller initial download (no WASM runtime)
- Full .NET API compatibility

**Cons:**
- No API for external integrations (violates requirement #1)
- Server affinity - each user holds a SignalR connection
- Higher server resource usage per user
- No offline capability
- If server restarts, all connections lost

### Option B: Blazor WebAssembly + Web API + SSE

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

**Note:** External systems can use both REST API (for actions) and SSE (for real-time updates).

**Pros:**
- Single API serves both Blazor app and external integrations
- Stateless API - better scalability
- SSE is lightweight (single HTTP connection per client)
- Works with CDN/static hosting for WASM assets
- Browser can cache WASM runtime
- Clear separation of concerns

**Cons:**
- Larger initial download (WASM runtime ~2-4MB)
- Slightly more complex architecture (two projects)
- SSE is unidirectional (server → client only)

### Option C: Blazor WebAssembly + Web API + SignalR

```
┌──────────────────┐     HTTP + WS     ┌─────────────────────┐
│  Blazor WASM     │◄─────────────────►│   Minimal API       │
│   (Browser UI)   │    (SignalR)      │   + SignalR Hub     │
└──────────────────┘                   │   + Broker Service  │
                                       └─────────────────────┘
┌──────────────────┐                            ▲
│ External Systems │───────────HTTP─────────────┘
└──────────────────┘
```

**Pros:**
- Bidirectional real-time communication
- Rich SignalR features (groups, backpressure)
- API for external integrations

**Cons:**
- SignalR is more complex than SSE
- WebSocket connections may be blocked by proxies
- Heavier protocol than SSE
- Not requested in requirements (SSE was specified)

---

## Real-Time Communication Options

### Server-Sent Events (SSE) - **Recommended**

.NET 10 introduces native SSE support via `TypedResults.ServerSentEvents`:

```csharp
app.MapGet("/api/events", (CancellationToken ct) =>
{
    return TypedResults.ServerSentEvents(
        GetDispatchEventsAsync(ct),
        eventType: "dispatch"
    );
});
```

**Characteristics:**
- Unidirectional: Server → Client only
- Uses standard HTTP (works through most proxies)
- Automatic reconnection built into browser `EventSource` API
- Lightweight protocol (text/event-stream)
- Perfect for broadcasting state changes

**For our use case:** SSE is ideal because:
- Dispatchers primarily need to *receive* updates
- Actions are performed via separate HTTP POST calls
- Simple to implement and debug
- Native .NET 10 support

### SignalR (Alternative)

- Bidirectional WebSocket-based communication
- More features but more complexity
- Not specified in requirements

### Polling (Not Recommended)

- Simple but inefficient
- High latency for updates
- Wastes bandwidth

---

## Blazor Hosting Model Comparison

### Blazor WebAssembly (Recommended)

| Aspect | Details |
|--------|---------|
| **Execution** | Runs in browser via WebAssembly |
| **Server Dependency** | Only for API calls |
| **Initial Load** | Slower (download WASM runtime) |
| **API Access** | Via HTTP calls to Web API |
| **Scalability** | Excellent (stateless server) |
| **Offline** | Possible with service worker |
| **External Integration** | Same API used by WASM app |

### Blazor Interactive Server

| Aspect | Details |
|--------|---------|
| **Execution** | Runs on server, UI via SignalR |
| **Server Dependency** | Constant SignalR connection |
| **Initial Load** | Fast (small initial payload) |
| **API Access** | Direct DI, no HTTP needed |
| **Scalability** | Limited by server connections |
| **Offline** | Not possible |
| **External Integration** | Requires separate API |

### Decision: Blazor WebAssembly

**Rationale:**
1. **Unified API** - Both the Blazor app and external integrations use the same Web API endpoints. This eliminates code duplication and ensures consistent behavior.
2. **Scalability** - WASM offloads rendering to clients; server only handles API requests.
3. **External Integration** - Signaling systems and automated controls need HTTP API access regardless.
4. **Requirement Alignment** - Matches the user's stated preference for "everything goes through the WEB API."

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

1. **Tellurian.Trains.Dispatch.Server** - ASP.NET Core host with Minimal API
2. **Tellurian.Trains.Dispatch.Client** - Blazor WebAssembly application
3. **Tellurian.Trains.Dispatch.Shared** - Shared DTOs and contracts
4. **Tellurian.Trains.Dispatch** - Existing domain library (unchanged)

---

## API Specification

### Base URL

```
/api/v1
```

### HATEOAS Design

The API uses a simple HATEOAS approach where each resource includes links to available actions. This means:

- **Clients don't construct URLs** - they follow links provided by the server
- **Server controls available actions** - based on current state
- **Self-documenting** - clients just render what they receive
- **SSE events trigger refresh** - client re-fetches dispatcher data to get updated actions

### Dispatcher Endpoints

#### List Dispatchers
```http
GET /api/v1/dispatchers
```
Returns all station dispatchers with self links.

**Response:**
```json
[
  {
    "id": 1,
    "name": "Göteborg C",
    "signature": "G",
    "links": {
      "self": "/api/v1/dispatchers/1",
      "arrivals": "/api/v1/dispatchers/1/arrivals",
      "departures": "/api/v1/dispatchers/1/departures",
      "events": "/api/v1/events?dispatcherId=1"
    }
  }
]
```

#### Get Dispatcher with Train Sections
```http
GET /api/v1/dispatchers/{id}
```
Returns dispatcher with arrivals and departures, each including available actions.

**Response:**
```json
{
  "id": 1,
  "name": "Göteborg C",
  "signature": "G",
  "arrivals": [
    {
      "id": 42,
      "train": "SJ IC 456",
      "from": "Alingsås",
      "scheduledTime": "10:45",
      "track": "2",
      "state": "Departed",
      "actions": [
        { "name": "arrived", "href": "/api/v1/train-sections/42/actions/arrived" }
      ],
      "blockSignalActions": [],
      "links": {
        "self": "/api/v1/train-sections/42"
      }
    }
  ],
  "departures": [
    {
      "id": 38,
      "train": "SJ IC 123",
      "to": "Alingsås",
      "scheduledTime": "10:30",
      "track": "1",
      "state": "None",
      "actions": [
        { "name": "request", "href": "/api/v1/train-sections/38/actions/request" }
      ],
      "trainActions": [
        { "name": "manned", "href": "/api/v1/train-sections/38/train-actions/manned" },
        { "name": "cancel", "href": "/api/v1/train-sections/38/train-actions/cancel" }
      ],
      "blockSignalActions": [],
      "links": {
        "self": "/api/v1/train-sections/38"
      }
    },
    {
      "id": 39,
      "train": "GC G 4501",
      "to": "Sävenäs",
      "scheduledTime": "10:35",
      "track": "3",
      "state": "Accepted",
      "actions": [
        { "name": "departed", "href": "/api/v1/train-sections/39/actions/departed" },
        { "name": "revoke", "href": "/api/v1/train-sections/39/actions/revoke" }
      ],
      "trainActions": [],
      "blockSignalActions": [],
      "links": {
        "self": "/api/v1/train-sections/39"
      }
    }
  ],
  "links": {
    "self": "/api/v1/dispatchers/1",
    "events": "/api/v1/events?dispatcherId=1"
  }
}
```

#### Train Section with Block Signal Actions
When a dispatcher controls intermediate block signals, those actions appear:

```json
{
  "id": 55,
  "train": "SJ Rc 891",
  "from": "Partille",
  "to": "Göteborg C",
  "scheduledTime": "11:15",
  "state": "Departed",
  "currentBlock": 1,
  "totalBlocks": 3,
  "actions": [],
  "blockSignalActions": [
    {
      "name": "pass",
      "signalName": "Block 42A",
      "href": "/api/v1/train-sections/55/pass-block-signal/0"
    }
  ],
  "links": {
    "self": "/api/v1/train-sections/55"
  }
}
```

### Action Endpoints

All actions are performed via POST to the href provided in the response:

```http
POST /api/v1/train-sections/{id}/actions/{action}
```

**Response** (returns updated train section with new available actions):
```json
{
  "success": true,
  "trainSection": {
    "id": 38,
    "train": "SJ IC 123",
    "state": "Requested",
    "actions": [
      { "name": "revoke", "href": "/api/v1/train-sections/38/actions/revoke" }
    ],
    "links": {
      "self": "/api/v1/train-sections/38"
    }
  }
}
```

**Error Response:**
```json
{
  "success": false,
  "error": "Action 'departed' is not available in state 'None'"
}
```

### Client Workflow

1. **Initial Load**: `GET /api/v1/dispatchers/{id}` → render train sections with action buttons
2. **User Action**: `POST` to action href → update UI with response
3. **SSE Event**: Receive event → `GET /api/v1/dispatchers/{id}` → refresh entire view

```
┌────────────────────────────────────────────────────────────┐
│  Client receives:                                          │
│  { "actions": [{ "name": "request", "href": "..." }] }     │
│                                                            │
│  Client renders:  ┌─────────┐                              │
│                   │ Request │  ← Button enabled            │
│                   └─────────┘                              │
│                                                            │
│  User clicks → POST to href → Server returns new state     │
│  OR SSE event arrives → Client refreshes dispatcher data   │
└────────────────────────────────────────────────────────────┘
```

### Real-Time Events Endpoint

#### SSE Event Stream
```http
GET /api/v1/events
Accept: text/event-stream
```

Optional query parameters:
- `dispatcherId` - Filter events for a specific dispatcher
- `lastEventId` - Resume from a specific event ID

---

## SSE Event Specification

### Event Types

All events are JSON-serialized and include an event type field.

#### DispatchStateChanged
Fired when a train section's dispatch state changes.

```json
{
  "type": "DispatchStateChanged",
  "timestamp": "2025-12-05T10:30:00Z",
  "trainSectionId": 123,
  "previousState": "Requested",
  "newState": "Accepted",
  "trainIdentity": "SJ IC 456",
  "fromStation": "Göteborg C",
  "toStation": "Alingsås",
  "affectedDispatchers": [1, 2]
}
```

#### TrainStateChanged
Fired when a train's overall state changes.

```json
{
  "type": "TrainStateChanged",
  "timestamp": "2025-12-05T10:30:00Z",
  "trainId": 45,
  "trainIdentity": "SJ IC 456",
  "previousState": "Planned",
  "newState": "Manned"
}
```

#### BlockSignalPassed
Fired when a train passes a block signal.

```json
{
  "type": "BlockSignalPassed",
  "timestamp": "2025-12-05T10:30:00Z",
  "trainSectionId": 123,
  "blockSignalName": "Block 42A",
  "blockIndex": 1,
  "totalBlocks": 3,
  "controlledByDispatcherId": 2
}
```

#### TrainSectionCleared
Fired when a canceled/aborted train is cleared from a stretch.

```json
{
  "type": "TrainSectionCleared",
  "timestamp": "2025-12-05T10:30:00Z",
  "trainSectionId": 123,
  "stretchFrom": "Göteborg C",
  "stretchTo": "Alingsås"
}
```

### SSE Wire Format

```
event: DispatchStateChanged
id: 1001
data: {"type":"DispatchStateChanged","trainSectionId":123,...}

event: TrainStateChanged
id: 1002
data: {"type":"TrainStateChanged","trainId":45,...}
```

### Server Implementation

```csharp
public class DispatchEventService
{
    private readonly Channel<DispatchEvent> _channel =
        Channel.CreateBounded<DispatchEvent>(100);

    public async IAsyncEnumerable<SseItem<DispatchEvent>> GetEventsAsync(
        int? dispatcherId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var eventId = 0;
        await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
        {
            if (dispatcherId == null || evt.AffectsDispatcher(dispatcherId.Value))
            {
                yield return new SseItem<DispatchEvent>(evt, eventType: evt.Type)
                {
                    EventId = (++eventId).ToString()
                };
            }
        }
    }

    public ValueTask PublishAsync(DispatchEvent evt) =>
        _channel.Writer.WriteAsync(evt);
}
```

### API Endpoint

```csharp
app.MapGet("/api/v1/events", (
    [FromQuery] int? dispatcherId,
    [FromServices] DispatchEventService eventService,
    CancellationToken ct) =>
{
    return TypedResults.ServerSentEvents(
        eventService.GetEventsAsync(dispatcherId, ct));
});
```

---

## External System Integration

External systems (signaling controllers, block signal hardware, automation systems) can consume SSE events just like the Blazor client. SSE is standard HTTP - any HTTP client can connect.

### Use Cases

| System | Consumes SSE For | Calls REST API For |
|--------|------------------|-------------------|
| Signal controller | `DispatchStateChanged` → update signal aspects | - |
| Block detector | `BlockSignalPassed` → verify passage | `POST /pass-block-signal` (auto-confirm) |
| Display board | All events → show train movements | - |
| Logging system | All events → record operations | - |

### C# External Client Example

```csharp
// External system using .NET HttpClient + SseParser
public class SignalController
{
    private readonly HttpClient _http;

    public async Task MonitorDispatchEventsAsync(CancellationToken ct)
    {
        await using var stream = await _http.GetStreamAsync(
            "http://dispatch-server:5000/api/v1/events", ct);

        await foreach (var item in SseParser.Create(stream).EnumerateAsync(ct))
        {
            var evt = JsonSerializer.Deserialize<DispatchEvent>(item.Data.ToString());

            switch (evt)
            {
                case { Type: "DispatchStateChanged", NewState: "Departed" }:
                    SetSignalAspect(evt.FromStation, SignalAspect.Stop);
                    break;
                case { Type: "DispatchStateChanged", NewState: "Arrived" }:
                    SetSignalAspect(evt.ToStation, SignalAspect.Clear);
                    break;
            }
        }
    }
}
```

### Python External Client Example

```python
# External system using Python sseclient
import sseclient
import requests
import json

def monitor_dispatch_events():
    response = requests.get(
        'http://dispatch-server:5000/api/v1/events',
        stream=True
    )
    client = sseclient.SSEClient(response)

    for event in client.events():
        data = json.loads(event.data)

        if data['type'] == 'DispatchStateChanged':
            if data['newState'] == 'Departed':
                set_signal_red(data['fromStation'])
            elif data['newState'] == 'Arrived':
                set_signal_green(data['toStation'])
```

### Arduino/ESP32 Example (Conceptual)

```cpp
// ESP32 with WiFi - simplified example
#include <WiFi.h>
#include <HTTPClient.h>

void loop() {
    HTTPClient http;
    http.begin("http://dispatch-server:5000/api/v1/events");
    http.addHeader("Accept", "text/event-stream");

    int httpCode = http.GET();
    if (httpCode == HTTP_CODE_OK) {
        WiFiClient* stream = http.getStreamPtr();
        while (stream->available()) {
            String line = stream->readStringUntil('\n');
            if (line.startsWith("data:")) {
                // Parse JSON and control signals
                handleEvent(line.substring(5));
            }
        }
    }
}
```

### Filtering Events

External systems can filter events server-side to reduce traffic:

```http
GET /api/v1/events?dispatcherId=2
```

Only receives events affecting dispatcher 2 (useful for a block signal controller that only manages signals on one stretch).

---

## Blazor Client Implementation

### HATEOAS-Based Design

The Blazor client is simplified by the HATEOAS API:
- **No URL construction** - just follow hrefs from the API
- **No state machine logic** - server provides available actions
- **Simple refresh pattern** - SSE event → re-fetch dispatcher data

### API Client Service

```csharp
public class DispatcherApiClient(HttpClient http)
{
    public Task<DispatcherDto?> GetDispatcherAsync(int id) =>
        http.GetFromJsonAsync<DispatcherDto>($"/api/v1/dispatchers/{id}");

    public Task<ActionResultDto?> PerformActionAsync(string actionHref) =>
        http.PostAsync(actionHref, null)
            .ContinueWith(t => t.Result.Content.ReadFromJsonAsync<ActionResultDto>())
            .Unwrap();
}
```

### SSE Event Client

```csharp
public class DispatchEventClient : IAsyncDisposable
{
    private readonly HttpClient _http;
    private CancellationTokenSource? _cts;

    public event Action? OnEventReceived;

    public async Task StartListeningAsync(string eventsHref)
    {
        _cts = new CancellationTokenSource();

        await using var stream = await _http.GetStreamAsync(eventsHref, _cts.Token);

        await foreach (var _ in SseParser.Create(stream).EnumerateAsync(_cts.Token))
        {
            // Any event triggers a refresh - we don't need event details
            OnEventReceived?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
```

### Dispatcher Page Component

```csharp
@page "/dispatcher/{Id:int}"
@implements IAsyncDisposable
@inject DispatcherApiClient Api
@inject DispatchEventClient Events

<h1>@dispatcher?.Name (@dispatcher?.Signature)</h1>

<section class="departures">
    <h2>Departures</h2>
    @foreach (var section in dispatcher?.Departures ?? [])
    {
        <TrainSectionCard Section="section" OnActionClicked="HandleAction" />
    }
</section>

<section class="arrivals">
    <h2>Arrivals</h2>
    @foreach (var section in dispatcher?.Arrivals ?? [])
    {
        <TrainSectionCard Section="section" OnActionClicked="HandleAction" />
    }
</section>

@code {
    [Parameter] public int Id { get; set; }

    private DispatcherDto? dispatcher;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();

        Events.OnEventReceived += async () => await InvokeAsync(RefreshAsync);

        if (dispatcher?.Links.Events is { } eventsHref)
        {
            _ = Events.StartListeningAsync(eventsHref);
        }
    }

    private async Task RefreshAsync()
    {
        dispatcher = await Api.GetDispatcherAsync(Id);
        StateHasChanged();
    }

    private async Task HandleAction(string actionHref)
    {
        await Api.PerformActionAsync(actionHref);
        // SSE event will trigger refresh, but we can also refresh immediately
        await RefreshAsync();
    }

    public async ValueTask DisposeAsync() => await Events.DisposeAsync();
}
```

### Train Section Card Component

```csharp
<div class="train-section @Section.State.ToLower()">
    <div class="train-info">
        <span class="train-name">@Section.Train</span>
        <span class="time">@Section.ScheduledTime</span>
        <span class="track">Track @Section.Track</span>
        <span class="destination">@(Section.To ?? Section.From)</span>
    </div>

    <div class="state">@Section.State</div>

    <div class="actions">
        @foreach (var action in Section.Actions)
        {
            <button class="action-btn @action.Name"
                    @onclick="() => OnActionClicked.InvokeAsync(action.Href)">
                @action.Name
            </button>
        }

        @foreach (var action in Section.TrainActions)
        {
            <button class="train-action-btn @action.Name"
                    @onclick="() => OnActionClicked.InvokeAsync(action.Href)">
                @action.Name
            </button>
        }

        @foreach (var action in Section.BlockSignalActions)
        {
            <button class="signal-btn"
                    @onclick="() => OnActionClicked.InvokeAsync(action.Href)">
                Pass @action.SignalName
            </button>
        }
    </div>
</div>

@code {
    [Parameter] public required TrainSectionDto Section { get; set; }
    [Parameter] public EventCallback<string> OnActionClicked { get; set; }
}
```

### Key Simplifications from HATEOAS

| Without HATEOAS | With HATEOAS |
|-----------------|--------------|
| Client builds URLs from patterns | Client follows hrefs |
| Client checks state to show buttons | Server provides available actions |
| Client knows state machine rules | Server controls what's possible |
| Multiple endpoint patterns to learn | Just POST to provided href |

---

## Project Structure

```
Tellurian.Trains.Dispatch.sln
├── Tellurian.Trains.Dispatch/              # Existing domain library
│   ├── Brokers/
│   ├── Layout/
│   ├── Trains/
│   └── ...
│
├── Tellurian.Trains.Dispatch.Server/       # ASP.NET Core API host
│   ├── Program.cs
│   ├── Endpoints/
│   │   ├── DispatcherEndpoints.cs
│   │   ├── TrainSectionEndpoints.cs
│   │   └── EventEndpoints.cs
│   ├── Services/
│   │   └── DispatchEventService.cs
│   └── appsettings.json
│
├── Tellurian.Trains.Dispatch.Client/       # Blazor WebAssembly
│   ├── Program.cs
│   ├── Pages/
│   │   ├── Index.razor
│   │   ├── Dispatcher.razor
│   │   └── TrainSection.razor
│   ├── Components/
│   │   ├── TrainSectionCard.razor
│   │   └── ActionButton.razor
│   ├── Services/
│   │   ├── DispatcherApiClient.cs
│   │   └── DispatchEventClient.cs
│   └── wwwroot/
│
├── Tellurian.Trains.Dispatch.Shared/       # Shared DTOs
│   ├── DispatcherDto.cs
│   ├── TrainSectionDto.cs
│   ├── DispatchEvent.cs
│   └── ActionResult.cs
│
└── Tellurian.Trains.Dispatch.Tests/        # Existing tests
```

---

## Deployment Scenario

### Local Network Operation

The application is designed for **local network deployment** in a model railway operations environment:

```
┌───────────────────────────────────────────────────────────┐
│                    Closed WiFi Network                    │
│                   (Password Protected)                    │
│                                                           │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│  │ Dispatcher  │  │ Dispatcher  │  │ Block Signal│        │
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

- **Network-level security** - WiFi password protects access to the network
- **No application authentication** - All clients on the network are trusted
- **Open API access** - Any device on the network can call API endpoints

This simplifies the architecture and is appropriate for:
- Model railway club operations
- Home layouts with private networks
- Exhibition setups with dedicated WiFi

### Server Configuration

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Allow connections from any device on the local network
app.Urls.Add("http://0.0.0.0:5000");

// No authentication middleware needed
app.MapDispatcherEndpoints();
app.MapTrainSectionEndpoints();
app.MapEventEndpoints();

app.Run();
```

### Input Validation with FluentValidation

API inputs are validated using [FluentValidation](https://docs.fluentvalidation.net/) for expressive, testable validation rules.

#### Validator Example

```csharp
public record DispatchActionRequest(int TrainSectionId, string Action);

public class DispatchActionRequestValidator : AbstractValidator<DispatchActionRequest>
{
    private static readonly string[] ValidActions =
        ["request", "accept", "reject", "revoke", "departed", "arrived"];

    public DispatchActionRequestValidator()
    {
        RuleFor(x => x.TrainSectionId)
            .GreaterThan(0)
            .WithMessage("TrainSectionId must be a positive integer");

        RuleFor(x => x.Action)
            .NotEmpty()
            .Must(action => ValidActions.Contains(action.ToLowerInvariant()))
            .WithMessage($"Action must be one of: {string.Join(", ", ValidActions)}");
    }
}
```

#### Registration

```csharp
builder.Services.AddValidatorsFromAssemblyContaining<DispatchActionRequestValidator>();
```

#### Manual Validation in Endpoints

```csharp
app.MapPost("/api/v1/train-sections/{id}/actions/{action}",
    async (int id, string action, IValidator<DispatchActionRequest> validator, IBroker broker) =>
{
    var request = new DispatchActionRequest(id, action);
    var result = await validator.ValidateAsync(request);

    if (!result.IsValid)
    {
        return Results.ValidationProblem(result.ToDictionary());
    }

    // Proceed with action...
});
```

#### Automatic Validation with Endpoint Filter

For cleaner endpoints, use [SharpGrip.FluentValidation.AutoValidation](https://github.com/SharpGrip/FluentValidation.AutoValidation):

```csharp
builder.Services.AddFluentValidationAutoValidation();

// Endpoints are automatically validated - no manual code needed
app.MapPost("/api/v1/train-sections/{id}/actions/{action}",
    (int id, string action, IBroker broker) =>
{
    // Validation already passed if we reach here
});
```

#### Benefits over DataAnnotations

| DataAnnotations | FluentValidation |
|-----------------|------------------|
| Attributes on properties | Separate validator classes |
| Limited expressions | Rich fluent API |
| Hard to test | Easy to unit test |
| Mixed concerns | Clean separation |
| Basic rules only | Complex conditional rules |

---

## Implementation Phases

### Phase 1: API Foundation

1. Create solution structure with new projects
2. Implement `DispatchEventService` for event broadcasting
3. Create REST API endpoints for dispatchers and train sections
4. Add SSE endpoint for real-time events
5. Write API integration tests

### Phase 2: Blazor Client

1. Set up Blazor WebAssembly project
2. Implement API client services
3. Implement SSE event client
4. Create dispatcher page with arrivals/departures
5. Create action components for dispatch operations

### Phase 3: Block Signal Support

1. Add block signal display components
2. Implement pass block signal action
3. Add block signal passage events to SSE stream

### Phase 4: Polish & Production

1. Add error handling and retry logic
2. Implement connection status indicators
3. Add loading states and optimistic updates
4. Performance testing and optimization
5. Local network deployment configuration

---

## Summary

The recommended architecture is:

| Component | Technology | Purpose |
|-----------|------------|---------|
| Backend API | .NET 10 Minimal API | REST endpoints + SSE streaming |
| Real-time | Server-Sent Events | Push state changes to clients |
| Client UI | Blazor WebAssembly | Dispatcher interface |
| Shared | Class Library | DTOs and contracts |

**Key Benefits:**
1. **Unified API** - Single entry point for Blazor app and external integrations
2. **Modern Stack** - .NET 10 native SSE support, minimal API
3. **Scalable** - Stateless API, client-side rendering
4. **Real-Time** - Immediate updates via SSE
5. **Maintainable** - Clear separation of concerns

This architecture fulfills all stated requirements while leveraging the latest .NET 10 capabilities.
