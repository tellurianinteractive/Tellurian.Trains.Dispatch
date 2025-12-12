# Tellurian.Trains.Dispatch

Ett modernt .NET-bibliotek för tågledning mellan stationer på en modelljärnväg.

## Varför detta bibliotek?

Att köra en realistisk trafiksession på en modelljärnväg kräver samordning mellan flera tågklarerare. Detta bibliotek erbjuder:

- **Realistiskt tågledningsflöde** modellerat efter verklig järnvägsdrift
- **Kapacitetshantering** för enkelspåriga och dubbelspåriga sträckor
- **Mellanliggande kontrollpunkter** för blocksignaler, mötesspår och korsningar
- **Regelstyrda åtgärder** som säkerställer att endast giltiga operationer är tillgängliga
- **Flexibel integration** med valfritt användargränssnitt, datalagring och tidshantering

Biblioteket hanterar den komplexa logiken för tågledning, så att du kan fokusera på användarupplevelsen.

## Grundläggande arkitektur

### Fysisk infrastruktur

**Driftplats** (Operation Place) är basen för alla platser där tåg trafikerar:
- **Station** – Bemannade platser där tåg stannar för passagerare eller gods
- **Signalkontrollerad plats** (Signal Controlled Place) – Blocksignaler, mötesspår eller korsningar som styrs av en tågklarerare
- **Annan plats** (Other Place) – Enkla hållplatser eller osignalerade korsningar

Varje driftplats har ett eller flera **spår** (Station Track) som representerar tillgängliga spår/plattformar, med egenskaper för visningsordning och plattformslängd (som indikerar möjlighet till passagerarutbyte).

**Spårsträcka** (Track Stretch) representerar den fysiska banan mellan två angränsande platser, med ett eller flera spår (enkelspår, dubbelspår etc.). Valfria egenskaper inkluderar längd (för grafisk visning och körtidsberäkningar) och CSS-klass (för UI-styling).

### Logiska tågvägar

**Tågledningssträcka** (Dispatch Stretch) definierar den logiska vägen mellan två stationer. Den kan sträcka sig över flera spårsträckor och inkludera mellanliggande signalkontrollerade platser. Varje tågledningssträcka stöder dubbelriktad drift genom **tågledningssträckriktning** (Dispatch Stretch Direction). En valfri CSS-klassegenskap möjliggör UI-styling för att särskilja olika vägar.

### Tåg och anrop

- **Tåg** (Train) – Identifieras av trafikföretag och tågnummer (t.ex. "SJ IC 123")
- **Tåguppehåll** (Train Station Call) – En planerad ankomst eller avgång vid en specifik plats och spår
- **Tågsektion** (Train Section) – Ett tågs förflyttning över en tågledningssträcka, från avgångsanrop till ankomstanrop

## Tillståndsmaskiner

### Tågtillstånd
```
Planned → Manned → Running → Completed
   ↓        ↓         ↓
Canceled Canceled  Aborted
```

Ett tåg går från planerat (Planned) när tåget bemannats (Manned), och efter avgång i drift (Running), och sedan antingen slutförs normalt eller avbryts. Ett tåg kan ställas in innan det börjar köra (från Planned eller Manned-tillstånd).

**Notera:**
- Åtgärderna Manned och Canceled är endast tillgängliga på den första tågsektionen av ett tågs resa
- På efterföljande sektioner är endast Aborted-åtgärden tillgänglig (när Running)
- Ett tåg blir automatisk **Running** när ett tåg avgår från första stationen - det finns ingen explicit "Running"-åtgärd

#### Ångra tågtillstånd

Tågklarerare kan ångra vissa tågtillståndsändringar för att korrigera misstag. Ångringen återställer till det tillstånd tåget var i innan ändringen:

| Nuvarande tillstånd | Föregående tillstånd | Ångrar till | Visningsnamn |
|---------------------|---------------------|-------------|--------------|
| Manned | Planned | Planned | "Återta bemannat" |
| Canceled | Planned | Planned | "Återta inställd" |
| Canceled | Manned | Manned | "Återta inställd" |
| Aborted | Running | Running | "Återta avnrotet" |

Ångraåtgärden är endast tillgänglig omedelbart efter tillståndsändringen (en nivå av ångra). När det är ångrat återgår tåget till sitt tidigare tillstånd och ångra är inte längre tillgängligt förrän en ny tillståndsändring sker.

### Tågledningstillstånd
```
None → Requested → Accepted → Departed → Arrived
                        ↓
                   Rejected/Revoked
```

Varje tågsektion spårar sin tågledningsframsteg oberoende, från första begäran genom avgång och ankomst.

### Tågsektionssekvensering

Ett tågs resa består av flera tågsektioner länkade via egenskapen `Previous`:

- **Första sektionen** har ingen föregende sektion: Det är här tågtillståndsåtgärder (Manned, Canceled) är tillgängliga. Ett tåg måste vara Manned innan dess första avgång kan begäras.
- **Efterföljande sektioner**: Tågledningsåtgärder är endast tillgängliga efter att föregående sektion har avgått. Endast Aborted-åtgärden är tillgänglig för tågtillståndsändringar.

Detta säkerställer att tåg rör sig genom sin resa i sekvens - du kan inte begära avgång från station B innan tåget har lämnat station A.

## Åtgärdsbaserad tågledning

Tågklarerare interagerar genom explicita åtgärder. **Åtgärdstillståndsmaskinen** (Action State Machine) avgör vilka åtgärder som är giltiga baserat på aktuellt tillstånd och tågklarerarens roll:

| Åtgärd | Utförs av | Beskrivning |
|--------|-----------|-------------|
| Request | Avgångstågklarerare | Begär tillstånd för tåg att avgå |
| Accept/Reject | Ankomsttågklarerare | Svarar på tågledningsbegäran |
| Revoke | Endera tågklareraren | Avbryter en accepterad begäran (före avgång) |
| Depart | Avgångstågklarerare | Tåget lämnar stationen |
| Pass | Kontrollpunktstågklarerare | Bekräftar att tåget passerat en signal |
| Arrive | Ankomsttågklarerare | Tåget har ankommit till nästa station |

Varje tågklarerare ser endast de åtgärder de är behöriga att utföra.

## Kapacitetshantering

Biblioteket upprätthåller kapacitetsbegränsningar på spårsträckenivå:

- **Spårtillgänglighet** – Varje fysiskt spår kan endast upptas av ett tåg åt gången
- **Riktningskonflikter** – På enkelspår blockeras motgående rörelser
- **Blocksektioner** – Signalkontrollerade platser delar upp sträckor i block, vilket möjliggör flera tåg med säkra avstånd

När ett tåg avgår upptar det den första spårsektionen. När det passerar varje kontrollpunkt avancerar det till nästa sektion och frigör den föregående för efterföljande tåg.

## Integration

Implementera dessa gränssnitt för att integrera biblioteket:

### IBrokerDataProvider
Tillhandahåller initialdata i en specifik ordning:
1. Driftplatser (stationer, signaler, hållplatser)
2. Spårsträckor (fysisk infrastruktur)
3. Tågledningssträckor (logiska vägar)
4. Tåg
5. Tågstationsanrop (tidtabellen)

### IBrokerStateProvider
Hanterar persistens av tågledningstillstånd för spara/återställ-funktionalitet
i den händelse av att applikationen behöver startas om.

### ITimeProvider
Tillhandahåller aktuell från en snabbklocka.

## Kom igång

```csharp
// Skapa broker med dina implementationer
var broker = new Broker(dataProvider, stateProvider, timeProvider);
await broker.InitAsync();

// Hämta en stationstågklarerare (Station Dispatcher)
var dispatcher = broker.GetDispatchers()
    .First(d => d.Station.Name == "Stockholm");

// Fråga tillgängliga åtgärder
var departureActions = dispatcher.DepartureActions;
var arrivalActions = dispatcher.ArrivalActions;
```

Broker är den centrala koordinatorn. Varje stationstågklarerare presenterar ankomster och avgångar relevanta för den stationen, med åtgärder filtrerade till vad tågklareraren kan utföra.

## Krav

- .NET 10.0 eller senare
- Microsoft.Extensions.DependencyInjection (valfritt, för DI-integration)

## Licens

GPL-3.0
