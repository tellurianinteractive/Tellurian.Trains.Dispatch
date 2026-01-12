# About this project

This project contains data providers implementing interfaces for Tellurian Trains Dispatch.

## Data Providers

### IBrokerDataProvider
Provides initial layout and train configuration to the Broker.

**Implementation:**
- **TimetableBrokerDataProvider** - Converts data from `Tellurian.Trains.Schedules.Model` to Dispatch domain types.
  - Supports XPLN .ODS files via `XplnBrokerDataProviderFactory`
  - Can be extended to support .XLSX and relational databases via the Schedules.Model ecosystem

### ICompositeStateProvider / IStateProvider
Persists and restores dispatch and train state changes.

**Implementations:**
- **InMemoryCompositeStateProvider** - In-memory state for testing (no persistence)
- **CsvCompositeStateProvider** - Event-sourced persistence using CSV files
  - `CsvTrainStateProvider` - Records train state changes
  - `CsvDispatchStateProvider` - Records dispatch state changes and signal passages

### ITimeProvider
Provides the current time for dispatch operations.

**Implementation:**
- **TellurianFastclockTimeProvider** - Fetches current fast clock time from the Tellurian Fastclock service (cloud or local).
