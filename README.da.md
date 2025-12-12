# Tellurian.Trains.Dispatch

Et moderne .NET-bibliotek til togledning mellem stationer på en modeljernbane.

## Hvorfor dette bibliotek?

At køre en realistisk trafiksession på en modeljernbane kræver koordinering mellem flere togledere. Dette bibliotek tilbyder:

- **Realistisk togledningsflow** modelleret efter virkelig jernbanedrift
- **Kapacitetshåndtering** for enkeltsporede og dobbeltsporede strækninger
- **Mellemliggende kontrolpunkter** for bloksignaler, krydsningsspor og kryds
- **Regelstyrede handlinger** som sikrer, at kun gyldige operationer er tilgængelige
- **Fleksibel integration** med valgfrit brugergrænseflade, datalagring og tidsstyring

Biblioteket håndterer den komplekse logik for togledning, så du kan fokusere på brugeroplevelsen.

## Grundlæggende arkitektur

### Fysisk infrastruktur

**Driftssted** (Operation Place) er basen for alle steder, hvor tog kører:
- **Station** – Bemandede steder, hvor tog standser for passagerer eller gods
- **Signalstyret sted** (Signal Controlled Place) – Bloksignaler, krydsningsspor eller kryds, der styres af en togleder
- **Andet sted** (Other Place) – Enkle holdepladser eller usignalerede kryds

Hvert driftssted har et eller flere **spor** (Station Track), der repræsenterer tilgængelige spor/perroner, med egenskaber for visningsrækkefølge og perronlængde (som indikerer mulighed for passagerudveksling).

**Sporstrækning** (Track Stretch) repræsenterer den fysiske bane mellem to tilstødende steder, med et eller flere spor (enkeltspor, dobbeltspor osv.). Valgfrie egenskaber inkluderer længde (til grafisk visning og køretidsberegninger) og CSS-klasse (til UI-styling).

### Logiske togruter

**Togledningsstrækning** (Dispatch Stretch) definerer den logiske rute mellem to stationer. Den kan strække sig over flere sporstrækninger og inkludere mellemliggende signalstyrede steder. Hver togledningsstrækning understøtter tovejsdrift gennem **togledningsstrækningsretning** (Dispatch Stretch Direction). En valgfri CSS-klasseegenskab muliggør UI-styling for at skelne mellem forskellige ruter.

### Tog

- **Tog** (Train) – Identificeres af trafikselskab og tognummer (f.eks. "DSB IC 123")
- **Togophold** (Train Station Call) – En planlagt ankomst eller afgang ved et specifikt sted og spor
- **Togsektion** (Train Section) – Et togs bevægelse over en togledningsstrækning, fra afgangsopkald til ankomstopkald

## Tilstandsmaskiner

### Togtilstande
```
Planned → Manned → Running → Completed
   ↓        ↓         ↓
Canceled Canceled  Aborted
```

Et tog går fra planlagt (Planned) når toget er bemandet (Manned), og efter afgang i drift (Running), og derefter enten afsluttes normalt eller afbrydes. Et tog kan aflyses, inden det begynder at køre (fra Planned eller Manned-tilstand).

**Bemærk:**
- Handlingerne Manned og Canceled er kun tilgængelige på den første togsektion af et togs rejse
- På efterfølgende sektioner er kun Aborted-handlingen tilgængelig (når Running)
- Et tog bliver automatisk **Running**, når et tog afgår fra første station - der er ingen eksplicit "Running"-handling

#### Fortryd togtilstand

Togledere kan fortryde visse togtilstandsændringer for at rette fejl. Fortrydelsen gendanner den tilstand, toget var i før ændringen:

| Nuværende tilstand | Tidligere tilstand | Fortryder til | Visningsnavn |
|--------------------|--------------------|---------------|--------------|
| Manned | Planned | Planned | "Fortryd bemandet" |
| Canceled | Planned | Planned | "Fortryd aflyst" |
| Canceled | Manned | Manned | "Fortryd aflyst" |
| Aborted | Running | Running | "Fortryd afbrudt" |

Fortryd-handlingen er kun tilgængelig umiddelbart efter tilstandsændringen (ét niveau af fortryd). Når det er fortrudt, vender toget tilbage til sin tidligere tilstand, og fortryd er ikke længere tilgængelig, indtil en ny tilstandsændring sker.

### Togledningstilstande
```
None → Requested → Accepted → Departed → Arrived
                        ↓
                   Rejected/Revoked
```

Hver togsektion sporer sin togledningsfremdrift uafhængigt, fra første anmodning gennem afgang og ankomst.

### Togsektionssekvensering

Et togs rejse består af flere togsektioner forbundet via egenskaben `Previous`:

- **Første sektion** har ingen foregående sektion: Det er her togtilstandshandlinger (Manned, Canceled) er tilgængelige. Et tog skal være Manned, før dets første afgang kan anmodes.
- **Efterfølgende sektioner**: Togledningshandlinger er kun tilgængelige, efter at foregående sektion er afgået. Kun Aborted-handlingen er tilgængelig for togtilstandsændringer.

Dette sikrer, at tog bevæger sig gennem deres rejse i sekvens - du kan ikke anmode om afgang fra station B, før toget har forladt station A.

## Handlingsbaseret togledning

Togledere interagerer gennem eksplicitte handlinger. **Handlingstilstandsmaskinen** (Action State Machine) afgør, hvilke handlinger der er gyldige baseret på aktuel tilstand og toglederens rolle:

| Handling | Udføres af | Beskrivelse |
|----------|------------|-------------|
| Request | Afgangstogleder | Anmoder om tilladelse til togafgang |
| Accept/Reject | Ankomsttogleder | Svarer på togledningsanmodning |
| Revoke | Begge togledere | Annullerer en accepteret anmodning (før afgang) |
| Depart | Afgangstogleder | Toget forlader stationen |
| Pass | Kontrolpunktstogleder | Bekræfter, at toget har passeret et signal |
| Arrive | Ankomsttogleder | Toget er ankommet til næste station |

Hver togleder ser kun de handlinger, de er autoriseret til at udføre.

## Kapacitetshåndtering

Biblioteket håndhæver kapacitetsbegrænsninger på sporstrækningsniveau:

- **Sportilgængelighed** – Hvert fysisk spor kan kun optages af ét tog ad gangen
- **Retningskonflikter** – På enkeltspor blokeres modgående bevægelser
- **Bloksektioner** – Signalstyrede steder opdeler strækninger i blokke, hvilket muliggør flere tog med sikre afstande

Når et tog afgår, optager det den første sporsektion. Når det passerer hvert kontrolpunkt, avancerer det til næste sektion og frigør den foregående for efterfølgende tog.

## Integration

Implementer disse grænseflader for at integrere biblioteket:

### IBrokerDataProvider
Leverer initialdata i en specifik rækkefølge:
1. Driftssteder (stationer, signaler, holdepladser)
2. Sporstrækninger (fysisk infrastruktur)
3. Togledningsstrækninger (logiske ruter)
4. Tog
5. Togstationsopkald (køreplanen)

### IBrokerStateProvider
Håndterer persistens af togledningstilstand for gem/gendan-funktionalitet
i tilfælde af, at applikationen skal genstartes.

### ITimeProvider
Leverer aktuel tid fra et hurtigur.

## Kom i gang

```csharp
// Opret broker med dine implementeringer
var broker = new Broker(dataProvider, stateProvider, timeProvider);
await broker.InitAsync();

// Hent en stationstogleder (Station Dispatcher)
var dispatcher = broker.GetDispatchers()
    .First(d => d.Station.Name == "København");

// Forespørg tilgængelige handlinger
var departureActions = dispatcher.DepartureActions;
var arrivalActions = dispatcher.ArrivalActions;
```

Broker er den centrale koordinator. Hver stationstogleder præsenterer ankomster og afgange relevante for den station, med handlinger filtreret til, hvad toglederen kan udføre.

## Krav

- .NET 10.0 eller nyere
- Microsoft.Extensions.DependencyInjection (valgfrit, til DI-integration)

## Licens

GPL-3.0
