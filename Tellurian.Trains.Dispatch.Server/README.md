# Tellurian.Trains.Dispatch.Api

A .NET 11 Minimal API for train dispatching on model railways. Provides REST endpoints for dispatcher operations and Server-Sent Events (SSE) for real-time updates.

## Design Philosophy

### HATEOAS (Hypermedia as the Engine of Application State)

This API follows HATEOAS principles. Clients discover available actions through hyperlinks in responses rather than constructing URLs themselves. Each response contains `href` links that clients follow to perform actions.

Benefits:
- **Decoupled clients** - URL structure can change without breaking clients
- **Self-documenting** - Responses tell clients what they can do next
- **Reduced client logic** - No URL templating or construction needed

Example response:
```json
{
  "id": 1,
  "train": "SJ IC 456",
  "actions": [
    { "name": "request", "displayName": "Request departure", "href": "/api/v1/train-sections/1/actions/request" },
    { "name": "depart", "displayName": "Depart", "href": "/api/v1/train-sections/1/actions/depart" }
  ]
}
```

### Server-Sent Events (SSE)

Real-time updates are delivered via SSE rather than WebSockets or polling. SSE provides:
- **Simplicity** - Standard HTTP, works through proxies and firewalls
- **Automatic reconnection** - Built into the browser EventSource API
- **Efficient** - Server pushes only when state changes occur
- **Filtering** - Subscribe to events for a specific dispatcher

## Endpoints

### Dispatchers

#### List all dispatchers
```
GET /api/v1/dispatchers
```

Returns a list of all station dispatchers with links to their detailed views.

**Response:** `200 OK`
```json
[
  {
    "id": 1,
    "signature": "A",
    "stationName": "Ankeborg",
    "href": "/api/v1/dispatchers/1"
  }
]
```

#### Get dispatcher details
```
GET /api/v1/dispatchers/{id}
```

Returns a dispatcher with arrivals, departures, and available actions for each train section.

**Response:** `200 OK`
```json
{
  "id": 1,
  "signature": "A",
  "stationName": "Ankeborg",
  "preferredLanguage": "sv",
  "self": "/api/v1/dispatchers/1",
  "events": "/api/v1/events?dispatcherId=1",
  "arrivals": [...],
  "departures": [
    {
      "id": 42,
      "train": "SJ IC 456",
      "trainState": "Running",
      "state": "Accepted",
      "from": "A",
      "to": "B",
      "departureTrack": "2",
      "arrivalTrack": "1",
      "scheduledDeparture": "08:15",
      "scheduledArrival": "08:32",
      "currentTrackStretch": 0,
      "totalTrackStretches": 3,
      "actions": [
        { "name": "depart", "displayName": "Depart", "href": "/api/v1/train-sections/42/actions/depart" }
      ]
    }
  ]
}
```

**Response:** `404 Not Found` if dispatcher does not exist.

### Train Section Actions

#### Execute action
```
POST /api/v1/train-sections/{id}/actions/{action}
```

Executes a dispatch or train action. Available actions depend on current state.

**Actions:**
| Action | Description |
|--------|-------------|
| `request` | Request permission to depart |
| `accept` | Accept departure request |
| `reject` | Reject departure request |
| `revoke` | Revoke accepted permission |
| `depart` | Mark train as departed |
| `arrive` | Mark train as arrived |
| `assigndriver` | Assign driver/crew to train |
| `start` | Start train journey |
| `cancel` | Cancel train before running |
| `abort` | Abort train while running |
| `complete` | Complete train journey |

**Response:** `200 OK`
```json
{
  "success": true,
  "trainSection": { ... }
}
```

**Response:** `400 Bad Request` if action is not available.
**Response:** `404 Not Found` if train section does not exist.

#### Execute pass action
```
POST /api/v1/train-sections/{id}/actions/pass/{controlPointIndex}
```

Marks a train as having passed a signal-controlled place (block signal) along the route.

**Response:** `200 OK`
```json
{
  "success": true,
  "trainSection": { ... }
}
```

### Events (SSE)

#### Subscribe to events
```
GET /api/v1/events
GET /api/v1/events?dispatcherId={id}
```

Opens a Server-Sent Events stream. Optionally filter events by dispatcher ID.

**Content-Type:** `text/event-stream`

**Usage with JavaScript:**
```javascript
const events = new EventSource('/api/v1/events?dispatcherId=1');

events.onmessage = (e) => {
  const event = JSON.parse(e.data);
  console.log(event.eventType, event.train);
};
```

## SSE Event Types

### DispatchStateChanged
Fired when dispatch state changes (request, accept, depart, arrive, etc.).

```json
{
  "eventType": "DispatchStateChanged",
  "timestamp": "2024-01-15T10:30:00Z",
  "trainSectionId": 42,
  "train": "SJ IC 456",
  "action": "Depart",
  "affectedDispatcherIds": [1, 2],
  "previousState": "Accepted",
  "newState": "Departed"
}
```

### TrainStateChanged
Fired when train operational state changes (manned, running, canceled, etc.).

```json
{
  "eventType": "TrainStateChanged",
  "timestamp": "2024-01-15T10:25:00Z",
  "trainSectionId": 42,
  "train": "SJ IC 456",
  "action": "AssignDriver",
  "affectedDispatcherIds": [1, 2],
  "previousState": "Planned",
  "newState": "Manned"
}
```

### ControlPointPassed
Fired when a train passes a signal-controlled place (block signal).

```json
{
  "eventType": "ControlPointPassed",
  "timestamp": "2024-01-15T10:35:00Z",
  "trainSectionId": 42,
  "train": "SJ IC 456",
  "action": "Pass",
  "affectedDispatcherIds": [1, 2, 3],
  "controlPointName": "Signal 42A",
  "newTrackStretchIndex": 2
}
```

## Configuration

Configure the API in `appsettings.json`:

```json
{
  "TimeProvider": {
    "Type": "Fastclock",
    "Fastclock": {
      "ServerUrl": "https://fastclock.azurewebsites.net",
      "ClockName": "Demo"
    }
  },
  "DataProvider": {
    "Source": "Xpln",
    "Xpln": {
      "FilePath": "Data/schedule.ods"
    },
    "Json": {
      "FilePath": "Data/schedule.json"
    },
    "Database": {
      "ConnectionString": "Data Source=schedule.db",
      "ScheduleName": "MySchedule"
    }
  }
}
```

### Time Provider

**Supported types:**
- `Default` (or empty) - Uses system wall clock time
- `Fastclock` - Fetches time from a Tellurian Fastclock server

### Data Provider

**Supported sources:**
- `Xpln` - Load from XPLN spreadsheet files (.ods)
- `Json` - Load from JSON schedule files
- `Database` - Load from SQLite database

All data providers use the `Tellurian.Trains.Schedules.Model` ecosystem.

#### XPLN Provider
```json
"DataProvider": {
  "Source": "Xpln",
  "Xpln": { "FilePath": "Data/schedule.ods" }
}
```

#### JSON Provider
```json
"DataProvider": {
  "Source": "Json",
  "Json": { "FilePath": "Data/schedule.json" }
}
```

#### Database Provider
```json
"DataProvider": {
  "Source": "Database",
  "Database": {
    "ConnectionString": "Data Source=schedule.db",
    "ScheduleName": "MySchedule"
  }
}
```

If no `Source` is configured, an empty provider is used (for testing).

## Running the API

```bash
dotnet run --project Tellurian.Trains.Dispatch.Api
```

The API starts on `http://localhost:5000` by default.

### Health Check
```
GET /health
```

Returns `{ "status": "healthy" }` when the API is running.
