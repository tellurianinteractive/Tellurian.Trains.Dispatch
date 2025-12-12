# Tellurian.Trains.Dispatch

Eine moderne .NET-Bibliothek für die Zugdisposition zwischen Bahnhöfen auf einer Modelleisenbahn.

## Warum diese Bibliothek?

Der Betrieb einer realistischen Fahrsession auf einer Modelleisenbahn erfordert die Koordination zwischen mehreren Fahrdienstleitern. Diese Bibliothek bietet:

- **Realistischer Dispositionsablauf** nach dem Vorbild des echten Eisenbahnbetriebs
- **Kapazitätsmanagement** für eingleisige und zweigleisige Streckenabschnitte
- **Zwischenkontrollpunkte** für Blocksignale, Ausweichgleise und Weichen
- **Regelbasierte Aktionen** die sicherstellen, dass nur gültige Operationen verfügbar sind
- **Flexible Integration** mit Ihrer Wahl an Benutzeroberfläche, Persistenz und Zeitmanagement

Die Bibliothek übernimmt die komplexe Logik der Zugdisposition, sodass Sie sich auf die Benutzererfahrung konzentrieren können.

## Grundarchitektur

### Physische Infrastruktur

**Betriebsstelle** (Operation Place) ist die Basis für alle Orte, an denen Züge verkehren:
- **Station** – Besetzte Orte, an denen Züge für Passagiere oder Fracht halten
- **Signalgesteuerte Stelle** (Signal Controlled Place) – Blocksignale, Ausweichgleise oder Weichen, die von einem Fahrdienstleiter gesteuert werden
- **Andere Stelle** (Other Place) – Einfache Haltepunkte oder unsignalisierte Kreuzungen

Jede Betriebsstelle hat **Bahnhofsgleise** (Station Track), die verfügbare Gleise/Bahnsteige darstellen, mit Eigenschaften für die Anzeigereihenfolge und Bahnsteiglänge (die die Möglichkeit zum Fahrgastwechsel anzeigt).

**Streckenabschnitt** (Track Stretch) repräsentiert die physische Strecke zwischen zwei benachbarten Orten, mit einem oder mehreren Gleisen (eingleisig, zweigleisig usw.). Optionale Eigenschaften umfassen Länge (für grafische Darstellung und Fahrzeitberechnungen) und CSS-Klasse (für UI-Styling).

### Logische Dispositionsrouten

**Dispositionsstrecke** (Dispatch Stretch) definiert die logische Route zwischen zwei Bahnhöfen. Sie kann mehrere Streckenabschnitte umfassen und zwischenliegende signalgesteuerte Stellen einschließen. Jede Dispositionsstrecke unterstützt bidirektionalen Betrieb durch **Dispositionsstreckenrichtung** (Dispatch Stretch Direction). Eine optionale CSS-Klasseneigenschaft ermöglicht die UI-Styling-Differenzierung zwischen Routen.

### Züge

- **Zug** (Train) – Identifiziert durch Eisenbahnverkehrsunternehmen und Zugnummer (z.B. "DB ICE 123")
- **Zughaltestelle** (Train Station Call) – Eine geplante Ankunft oder Abfahrt an einem bestimmten Ort und Gleis
- **Zugabschnitt** (Train Section) – Die Fahrt eines Zuges über eine Dispositionsstrecke, vom Abfahrtseintrag zum Ankunftseintrag

## Zustandsmaschinen

### Zugzustände
```
Planned → Manned → Running → Completed
   ↓         ↓         ↓
Canceled  Canceled  Aborted
```

Ein Zug schreitet von geplant (Planned) über Personalbesetzung (Manned) zum aktiven Betrieb (Running) voran und wird dann entweder normal abgeschlossen oder abgebrochen. Ein Zug kann storniert werden, bevor er zu fahren beginnt (aus dem Planned- oder Manned-Zustand).

**Hinweis:**
- Die Aktionen Manned und Canceled sind nur im ersten Zugabschnitt der Zugreise verfügbar
- In nachfolgenden Abschnitten ist nur die Aborted-Aktion verfügbar (wenn Running)
- **Der Running-Zustand wird implizit gesetzt**, wenn ein Zug abfährt - es gibt keine explizite "Running"-Aktion

#### Zugzustand rückgängig machen

Fahrdienstleiter können bestimmte Zugzustandsänderungen rückgängig machen, um Fehler zu korrigieren. Das Rückgängigmachen kehrt zu dem Zustand zurück, in dem sich der Zug vor der Änderung befand:

| Aktueller Zustand | Vorheriger Zustand | Rückgängig macht | Anzeigename |
|-------------------|-------------------|------------------|-------------|
| Manned | Planned | Planned | "Undo Manned" |
| Canceled | Planned | Planned | "Undo Canceled" |
| Canceled | Manned | Manned | "Undo Canceled" |
| Aborted | Running | Running | "Undo Aborted" |

Die Rückgängig-Aktion ist nur unmittelbar nach der Zustandsänderung verfügbar (eine Ebene des Rückgängigmachens). Nach dem Rückgängigmachen kehrt der Zug in seinen vorherigen Zustand zurück, und Rückgängig ist nicht mehr verfügbar, bis eine weitere Zustandsänderung eintritt.

### Dispositionszustände
```
None → Requested → Accepted → Departed → Arrived
                        ↓
                   Rejected/Revoked
```

Jeder Zugabschnitt verfolgt seinen Dispositionsfortschritt unabhängig, von der ersten Anfrage über Abfahrt bis zur Ankunft.

### Zugabschnittssequenzierung

Die Reise eines Zuges besteht aus mehreren Zugabschnitten, die über die Eigenschaft `Previous` verknüpft sind:

- **Erster Abschnitt** (`Previous` ist null): Hier sind Zugzustandsaktionen (Manned, Canceled) verfügbar. Ein Zug muss Manned sein, bevor seine erste Abfahrt angefordert werden kann.
- **Nachfolgende Abschnitte**: Dispositionsaktionen sind erst verfügbar, nachdem der vorherige Abschnitt abgefahren ist. Nur die Aborted-Aktion ist für Zugzustandsänderungen verfügbar.

Dies stellt sicher, dass Züge ihre Reise in Reihenfolge durchlaufen - Sie können keine Abfahrt von Bahnhof B anfordern, bevor der Zug Bahnhof A verlassen hat.

## Aktionsbasierte Disposition

Fahrdienstleiter interagieren durch explizite Aktionen. Die **Aktionszustandsmaschine** (Action State Machine) bestimmt, welche Aktionen basierend auf dem aktuellen Zustand und der Rolle des Fahrdienstleiters gültig sind:

| Aktion | Ausgeführt von | Beschreibung |
|--------|----------------|--------------|
| Request | Abfahrts-Fahrdienstleiter | Anfrage zur Abfahrt von Zügen |
| Accept/Reject | Ankunfts-Fahrdienstleiter | Antwort auf die Anfrage zur Abfahrt des Zuges |
| Revoke | Beide Fahrdienstleiter | Eine akzeptierte Anfrage stornieren (vor der Abfahrt) |
| Depart | Abfahrts-Fahrdienstleiter | Zug verlässt den Bahnhof |
| Pass | Kontrollpunkt-Fahrdienstleiter | Bestätigt, dass der Zug ein Signal passiert hat |
| Arrive | Ankunfts-Fahrdienstleiter | Der Zug erreicht den nächsten Bahnhof |

Jeder Fahrdienstleiter sieht nur die Aktionen, zu deren Ausführung er berechtigt ist.

## Kapazitätsmanagement

Die Bibliothek erzwingt Kapazitätsbeschränkungen auf Streckenabschnittsebene:

- **Gleisverfügbarkeit** – Jedes physische Gleis kann nur von einem Zug gleichzeitig belegt werden
- **Richtungskonflikte** – Auf eingleisigen Strecken werden Gegenfahrten blockiert
- **Blockabschnitte** – Signalgesteuerte Stellen unterteilen Strecken in Blöcke, was mehrere Züge mit sicherem Abstand ermöglicht

Wenn ein Zug abfährt, belegt er den ersten Streckenabschnitt. Wenn er jeden Kontrollpunkt passiert, rückt er zum nächsten Abschnitt vor und gibt den vorherigen für nachfolgende Züge frei.

## Integration

Implementieren Sie diese Schnittstellen zur Integration der Bibliothek:

### IBrokerDataProvider
Liefert Initialdaten in einer bestimmten Reihenfolge:
1. Betriebsstellen (Bahnhöfe, Signale, Haltepunkte)
2. Streckenabschnitte (physische Infrastruktur)
3. Dispositionsstrecken (logische Routen)
4. Züge
5. Zugfahrplaneinträge (der Fahrplan)

### IBrokerStateProvider
Gewährleistet die Persistenz des Dispatch-Status für die Speicher-/Wiederherstellungsfunktion
im Falle eines Neustarts oder Absturzes der Anwendung.

### ITimeProvider
Liefert die aktuelle Zeit, typischerweise von einer Schnelluhr für beschleunigten Betrieb.

## Erste Schritte

```csharp
// Erstellen Sie den Broker mit Ihren Implementierungen
var broker = new Broker(dataProvider, stateProvider, timeProvider);
await broker.InitAsync();

// Holen Sie einen Bahnhofs-Fahrdienstleiter (Station Dispatcher)
var dispatcher = broker.GetDispatchers()
    .First(d => d.Station.Name == "München");

// Verfügbare Aktionen abfragen
var departureActions = dispatcher.DepartureActions;
var arrivalActions = dispatcher.ArrivalActions;
```

Der Broker ist der zentrale Koordinator. Jeder Bahnhofs-Fahrdienstleiter präsentiert die für diesen Bahnhof relevanten Ankünfte und Abfahrten, mit Aktionen gefiltert auf das, was der Fahrdienstleiter ausführen kann.

## Anforderungen

- .NET 10.0 oder höher
- Microsoft.Extensions.DependencyInjection (optional, für DI-Integration)

## Lizenz

GPL-3.0
