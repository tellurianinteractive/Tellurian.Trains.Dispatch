# About this project

This project contains data providers implementing interfaces for Tellurian Trains Dispatch.

## Data Providers

### IBrokerDataProvider
Provides initial layout and train configuration to the Broker.

**Implementation:**
- **TimetableBrokerDataProvider** - Converts a plan of `Tellurian.Trains.Schedules.Model` to Dispatch domain types.
  - Plan rules are the model's and only consumed here (see root CLAUDE.md): `HasDispatcher`, `plan.SessionNumbersOf(train)`, `train.DispatchLegs`, `EffectiveCompany`, `SessionStart`. Never re-implement them here; add missing ones to the model as extension blocks.
  - Dispatch-only additions: one `Train` per session it runs (times offset by whole days), and merging a one-dispatcher leg at a junction worked from afar with the leg beyond it.
- **ScheduleDatabase** (`Database/`) - Holds one complete `Plan` in any EF Core database via `Tellurian.Trains.Schedules.Model.Databases`.
  - `ImportPlanFileAsync` imports a Timetable Planner plan file (JSON); `CreateBrokerDataProvider` runs the broker from the database.
  - Test data: `Tellurian.Trains.Dispatch.Data.Tests/TestData/Varnamo-Moduldagar.json` is a real Planner plan file (schema 5).

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
