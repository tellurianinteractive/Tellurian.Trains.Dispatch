# About this project
IMPORTANT! Ignore this project.

## Project Purpose
This projectaims to implement data providers:
- **IBrokerConfiguration**
- **ITimeProvider**

### IBrokerConfiguration
The project aims to contain the following concrete implementations:
- **AccessBrokerConfiguration**, read configuratio from a Microsoft Access database using the ODBC data provider.

### ITimeProvider
The project aims to contain the following concrete implementations:
- TellurianFastclockTimeProvider, fetch current fast clock time from the Tellurian Fastclock,
  either from the cloud instance or from a locally hosted clock server. 
  Both are using https or http Web API calls.

