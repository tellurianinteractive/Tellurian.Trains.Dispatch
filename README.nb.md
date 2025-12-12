# Tellurian.Trains.Dispatch

Et moderne .NET-bibliotek for togledning mellom stasjoner på en modelljernbane.

## Hvorfor dette biblioteket?

Å kjøre en realistisk trafikkøkt på en modelljernbane krever koordinering mellom flere togledere. Dette biblioteket tilbyr:

- **Realistisk togledningsflyt** modellert etter virkelig jernbanedrift
- **Kapasitetshåndtering** for enkeltsporede og dobbeltsporede strekninger
- **Mellomliggende kontrollpunkter** for blokksignaler, kryssingsspor og kryss
- **Regelstyrte handlinger** som sikrer at kun gyldige operasjoner er tilgjengelige
- **Fleksibel integrasjon** med valgfritt brukergrensesnitt, datalagring og tidsstyring

Biblioteket håndterer den komplekse logikken for togledning, slik at du kan fokusere på brukeropplevelsen.

## Grunnleggende arkitektur

### Fysisk infrastruktur

**Driftssted** (Operation Place) er basen for alle steder der tog kjører:
- **Stasjon** – Bemannede steder der tog stopper for passasjerer eller gods
- **Signalstyrt sted** (Signal Controlled Place) – Blokksignaler, kryssingsspor eller kryss som styres av en togleder
- **Annet sted** (Other Place) – Enkle holdeplasser eller usignaliserte kryss

Hvert driftssted har ett eller flere **spor** (Station Track) som representerer tilgjengelige spor/plattformer, med egenskaper for visningsrekkefølge og plattformlengde (som indikerer mulighet for passasjerutveksling).

**Sporstrekning** (Track Stretch) representerer den fysiske banen mellom to tilstøtende steder, med ett eller flere spor (enkeltspor, dobbeltspor osv.). Valgfrie egenskaper inkluderer lengde (for grafisk visning og kjøretidsberegninger) og CSS-klasse (for UI-styling).

### Logiske togruter

**Togledningsstrekning** (Dispatch Stretch) definerer den logiske ruten mellom to stasjoner. Den kan strekke seg over flere sporstrekninger og inkludere mellomliggende signalstyrte steder. Hver togledningsstrekning støtter toveis drift gjennom **togledningsstrekningsretning** (Dispatch Stretch Direction). En valgfri CSS-klasseegenskap muliggjør UI-styling for å skille mellom ulike ruter.

### Tog

- **Tog** (Train) – Identifiseres av trafikkselskap og tognummer (f.eks. "NSB IC 123")
- **Togopphold** (Train Station Call) – En planlagt ankomst eller avgang ved et spesifikt sted og spor
- **Togseksjon** (Train Section) – Et togs bevegelse over en togledningsstrekning, fra avgangsanrop til ankomstanrop

## Tilstandsmaskiner

### Togtilstander
```
Planned → Manned → Running → Completed
   ↓        ↓         ↓
Canceled Canceled  Aborted
```

Et tog går fra planlagt (Planned) når toget er bemannet (Manned), og etter avgang i drift (Running), og deretter enten fullføres normalt eller avbrytes. Et tog kan innstilles før det begynner å kjøre (fra Planned eller Manned-tilstand).

**Merk:**
- Handlingene Manned og Canceled er kun tilgjengelige på den første togseksjonen av et togs reise
- På etterfølgende seksjoner er kun Aborted-handlingen tilgjengelig (når Running)
- Et tog blir automatisk **Running** når et tog avgår fra første stasjon - det finnes ingen eksplisitt "Running"-handling

#### Angre togtilstand

Togledere kan angre visse togtilstandsendringer for å korrigere feil. Angringen gjenoppretter tilstanden toget var i før endringen:

| Nåværende tilstand | Forrige tilstand | Angrer til | Visningsnavn |
|--------------------|------------------|------------|--------------|
| Manned | Planned | Planned | "Angre bemannet" |
| Canceled | Planned | Planned | "Angre innstilt" |
| Canceled | Manned | Manned | "Angre innstilt" |
| Aborted | Running | Running | "Angre avbrutt" |

Angre-handlingen er kun tilgjengelig umiddelbart etter tilstandsendringen (ett nivå av angre). Når det er angret, går toget tilbake til sin forrige tilstand, og angre er ikke lenger tilgjengelig før en ny tilstandsendring skjer.

### Togledningstilstander
```
None → Requested → Accepted → Departed → Arrived
                        ↓
                   Rejected/Revoked
```

Hver togseksjon sporer sin togledningsfremdrift uavhengig, fra første forespørsel gjennom avgang og ankomst.

### Togseksjonssekvensering

Et togs reise består av flere togseksjoner koblet via egenskapen `Previous`:

- **Første seksjon** har ingen foregående seksjon: Det er her togtilstandshandlinger (Manned, Canceled) er tilgjengelige. Et tog må være Manned før dets første avgang kan forespørres.
- **Etterfølgende seksjoner**: Togledningshandlinger er kun tilgjengelige etter at foregående seksjon har avgått. Kun Aborted-handlingen er tilgjengelig for togtilstandsendringer.

Dette sikrer at tog beveger seg gjennom sin reise i sekvens - du kan ikke forespørre avgang fra stasjon B før toget har forlatt stasjon A.

## Handlingsbasert togledning

Togledere samhandler gjennom eksplisitte handlinger. **Handlingstilstandsmaskinen** (Action State Machine) avgjør hvilke handlinger som er gyldige basert på nåværende tilstand og toglederens rolle:

| Handling | Utføres av | Beskrivelse |
|----------|------------|-------------|
| Request | Avgangstogleder | Forespør tillatelse til togavgang |
| Accept/Reject | Ankomsttogleder | Svarer på togledningsforespørsel |
| Revoke | Begge togledere | Kansellerer en akseptert forespørsel (før avgang) |
| Depart | Avgangstogleder | Toget forlater stasjonen |
| Pass | Kontrollpunktstogleder | Bekrefter at toget har passert et signal |
| Arrive | Ankomsttogleder | Toget har ankommet til neste stasjon |

Hver togleder ser kun de handlingene de er autorisert til å utføre.

## Kapasitetshåndtering

Biblioteket håndhever kapasitetsbegrensninger på sporstrekningsnivå:

- **Sportilgjengelighet** – Hvert fysisk spor kan kun oppta ett tog om gangen
- **Retningskonflikter** – På enkeltspor blokkeres motgående bevegelser
- **Blokkseksjoner** – Signalstyrte steder deler opp strekninger i blokker, noe som muliggjør flere tog med sikre avstander

Når et tog avgår, opptar det den første sporseksjonen. Når det passerer hvert kontrollpunkt, avanserer det til neste seksjon og frigjør den forrige for etterfølgende tog.

## Integrasjon

Implementer disse grensesnittene for å integrere biblioteket:

### IBrokerDataProvider
Leverer initialdata i en spesifikk rekkefølge:
1. Driftssteder (stasjoner, signaler, holdeplasser)
2. Sporstrekninger (fysisk infrastruktur)
3. Togledningsstrekninger (logiske ruter)
4. Tog
5. Togstasjonsanrop (rutetabellen)

### IBrokerStateProvider
Håndterer persistens av togledningstilstand for lagre/gjenopprett-funksjonalitet
i tilfelle applikasjonen må startes på nytt.

### ITimeProvider
Leverer nåværende tid fra en hurtigklokke.

## Kom i gang

```csharp
// Opprett broker med dine implementasjoner
var broker = new Broker(dataProvider, stateProvider, timeProvider);
await broker.InitAsync();

// Hent en stasjonstogleder (Station Dispatcher)
var dispatcher = broker.GetDispatchers()
    .First(d => d.Station.Name == "Oslo");

// Forespør tilgjengelige handlinger
var departureActions = dispatcher.DepartureActions;
var arrivalActions = dispatcher.ArrivalActions;
```

Broker er den sentrale koordinatoren. Hver stasjonstogleder presenterer ankomster og avganger relevante for den stasjonen, med handlinger filtrert til hva toglederen kan utføre.

## Krav

- .NET 10.0 eller nyere
- Microsoft.Extensions.DependencyInjection (valgfritt, for DI-integrasjon)

## Lisens

GPL-3.0
