# About this project
This project should contain data providers impementing 
**IBrokerDataProvider**,
**IBrokerStateProvider and 
**ITimeProvider**
for Tellurian Trains Dispatch.

## Project Purpose
This projectaims to implement data providers:
- **IBrokerConfiguration**
- **ITimeProvider**

### IBrokerConfiguration
The project aims to contain the following concrete implementations:
- **JsonFileBrokerDataProvider**, read configuration from a JSON file.
- **IBrokerDataProvider**, read configuratio from a Microsoft Access database using the ODBC data provider.

### IBrokerStateProvider
The project aims to contain the following concrete implementations:
- **JsonFileBrokerStateProvider**, read broker state from a JSON file.
- 
### ITimeProvider
The project aims to contain the following concrete implementations:
- **TellurianFastclockTimeProvider**, fetch current fast clock time from the Tellurian Fastclock,
  either from the cloud instance or from a locally hosted clock server. 
  Both are using https or http Web API calls.

