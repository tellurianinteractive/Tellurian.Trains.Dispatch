# RgZm - Computer-Assisted Train Reporting System

RgZm (Rechnergestützte Zugmeldung, literally "Computer-Assisted Train Notification") is an open-source software suite designed for model railroad operations, specifically targeting modular layout meets where multiple operators need to coordinate train movements between stations.

## Purpose

Traditional train reporting at modular railroad meets relies on telephone communication between dispatchers at different stations. This becomes time-consuming and error-prone, especially on double-track main lines operating under compressed model time (where real minutes represent model hours).

RgZm replaces voice-based train reporting with a networked, graphical system. Dispatchers can:
- Report train movements with mouse clicks instead of phone calls
- See real-time status of all trains in the network
- Coordinate departures and arrivals without verbal communication
- Track delays automatically with color-coded indicators

## Key Use Cases

### 1. Station Dispatching
Each station dispatcher runs an RgZm client showing:
- Station tracks as clickable buttons (empty tracks show "insert", occupied tracks show train numbers and times)
- Routes to neighboring stations displayed in lateral panels
- Single-track routes (purple) and double-track routes (yellow/orange with directional arrows)
- Connection status to neighbors (green = connected, red = connection lost)

### 2. Train Arrival
When a train arrives:
1. Dispatcher confirms the track assignment from the station diagram
2. Sets and secures the route
3. Monitors the train's arrival
4. Reports the track as clear in RgZm
5. Returns entry signal to stop

### 3. Train Departure
When dispatching a train:
1. Confirm route can be set safely
2. Set and secure the route
3. Pre-announce the train in RgZm (if track shows occupied)
4. Pull departure signal after track confirmation
5. Monitor departing train

### 4. Pass-Through Trains
For trains passing without stopping:
1. Set departure and entry routes sequentially
2. Pre-announce in RgZm
3. Monitor passage
4. Reset signals and routes after train clears

### 5. Synchronized Timetable Display
The visual timetable (Bildfahrplan) shows train movements graphically:
- Stations arranged vertically
- Train movements as diagonal lines
- Auto-scrolling with current model time
- Red timeline indicator for "now"

## Technical Setup

### System Requirements
- **Java Runtime Environment**: Version 1.8 or higher
- **Network**: LAN/WLAN connectivity between all operator stations
- **Operating System**: Windows or Unix/Linux

### Installation
1. Download the latest release from SourceForge (currently v4.3.0)
2. Extract the ZIP file to your preferred location
3. Ensure `JAVA_HOME` environment variable points to your Java installation
4. Launch applications from the `bin` subfolder using provided scripts

### Software Components

The RgZm suite includes seven integrated programs:

| Program | Purpose |
|---------|---------|
| **RgZm** | Core train reporting system - manages notifications between stations via network |
| **Timekeeper** | Master clock generating adjustable model time ratios, broadcast to all clients |
| **Clock** | Slave clock displaying synchronized model time (analog/digital options) |
| **Bildfahrplan** | Visual timetable synchronized with model time |
| **Frachtmatrix** | Freight planning tool for car routing optimization |
| **Datenblatteditor** | Creates/edits operating point data sheets (XML format) |
| **Zeitdienste Adapter** | Integration bridge for MRclockserver or Rocrail applications |

### Network Architecture
- TCP/IP socket-based communication
- Stateless request-response protocol
- Version-based handshake for compatibility
- Supports multiple simultaneous operator stations

### Remote Control API
RgZm provides a TCP/IP socket interface for external control, supporting:
- Track occupancy queries
- Block section management
- Train creation and acceptance
- Pre-notification to neighboring stations

## Configuration

### Station Configuration
Each station requires:
- Station name and code
- Track layout definition
- Neighboring station connections
- Route definitions (single/double track)

### Visual Timetable Configuration
The Bildfahrplan uses configuration files with:
- Station list (`zuglaufstellen`)
- Distance in kilometers for each station
- Track lists per station
- BFO data file paths
- Display canvas dimensions

Configuration files use UTF-8 encoded key-value syntax.

## Further Documentation

### Official Documentation
- **User Documentation (German)**: https://cafebahn.sourceforge.io/userdocs/de/
- **Introduction**: https://cafebahn.sourceforge.io/introduction/
- **Installation Guide**: https://cafebahn.sourceforge.io/userdocs/de/installation/
- **RgZm Operation**: https://cafebahn.sourceforge.io/userdocs/de/rgzm-bedienung/
- **Configuration**: https://cafebahn.sourceforge.io/userdocs/de/rgzm-konfiguration/
- **Remote Control API**: https://cafebahn.sourceforge.io/userdocs/de/rgzm-fernsteuerung/
- **Visual Timetable**: https://cafebahn.sourceforge.io/userdocs/de/bildfahrplan/

### FKTT Resources
- **FKTT RgZm Overview**: https://www.fktt-module.de/de/node/207
- **Step-by-Step Guide**: https://www.fktt-module.de/node/208

## Source Code

RgZm is open source software released under the **GNU General Public License v3.0 (GPLv3)**.

### Repository
- **Project Page**: https://sourceforge.net/projects/cafebahn/
- **Git Repositories**:
  - Cafebahn (main)
  - Rgzmsuite
  - Cafebahn-Maintenance

### Download
- **Latest Release**: https://sourceforge.net/projects/cafebahn/files/latest/download
- **Current Version**: 4.3.0 (October 2021)
- **File Size**: ~8.8 MB (ZIP archive)

## Contact and Community

### Development Team
The Cafebahn Development Team maintains the project. Known contributors (SourceForge usernames):
- grischan
- horsteff
- stumml

### Community
- **FKTT (Freundeskreis TT-Module)**: A German model railroad community using TT gauge (1:120 scale) that actively uses RgZm at their modular meets
- **FKTT Forum**: https://forum.fktt-module.de/

### Project History
- **Started**: July 2007
- **Active Development**: 2007-2021
- **Technology**: Java with Swing UI
- **Languages Supported**: German, English, Dutch, Polish, Czech, Danish, Hungarian, Bulgarian

## Language Support

The software interface is available in multiple languages:
- German (primary)
- English
- Dutch
- Polish
- Czech
- Danish
- Hungarian
- Bulgarian
