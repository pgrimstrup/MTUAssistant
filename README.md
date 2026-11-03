# MTUAssistant

The home-page atlas is drawn directly from the M1105 sector/world database using PixiJS and pixi-viewport. Imported political borders and routes render above the grid using their stored colours. Downloaded PNG tiles are not required by the map. Refresh Sector Data downloads system data and TravellerMap metadata instead.

## Map detail

Detail switches according to CSS pixels per parsec (not device pixel ratio):

| Zoom | Detail |
| --- | --- |
| Below 10 | World circles and borders; no routes, world names or hex grid |
| 10–below 25 | World circles and hex grid; subsector capital (Cp) names only |
| 25–below 55 | World circles, hex grid, gas giants, bases and travel-zone rings; starport class; names for capitals and population 9+ |
| 55–below 95 | World circles, hex grid, gas giants, bases and travel-zone rings; full UWP and every world name |
| 95 and above | All details, including hex coordinates |

Map label fonts are 20% smaller at all zoom levels below 95 px/pc; full-detail fonts at 95+ are unchanged. Sector and subsector boundaries draw at every zoom. Label eligibility follows fixed bands in `wwwroot/js/MapGeometry.js`; no collision rejection, text budget, or zoom history suppresses eligible labels. Only off-screen text is culled. Subsector capital names are red; population 9+ names are uppercase (high-population eligibility uses 9+). Sector names remain visible, with subsector region names from 25 px/pc.

At close zoom, UWP sits at the top of the hex, below coordinates when shown; names have a four-pixel bottom margin. World circles grow with zoom. Zone rings have a minimum 2 CSS pixel stroke at every zoom level where they are displayed. Amber/red zone rings are fully opaque and do not affect symbol positions. Naval and scout symbols sit at upper/lower left; the gas giant is inset at upper right, aligned with the naval symbol. Click a marker or hex to select it; drag to pan. The viewport is remembered between visits. Selecting a system in the search dropdown (mouse or Enter) opens the map, pans smoothly to and highlights its hex at the current zoom (650 ms, with reduced-motion support), and loads the same details panel as a map click. Selecting a sector or subsector pans to its centre with the same animation, clears the world highlight and details, and preserves zoom. Search links use a stable target ID and a request token so selecting the same world again recentres it. Requests made while the map is loading are applied when it is ready; systems outside M1105 show an availability message.

`GET /api/map/overview` supplies sector metadata, world positions and compact world name/UWP/remarks data so zoomed-out capital labels are available immediately. The browser fetches detailed map attributes from `GET /api/map/sectors/{id}` for nearby sectors, with bounded concurrent requests and a client-side sector cache; these endpoints read cached server projections, not the database. Map descriptions and data projections live in MTU.Core; rendering geometry and detail thresholds live in `MTUAssistant/wwwroot/js/MapGeometry.js`.

## Shared system catalogue

After database migration, application startup awaits the singleton `SystemDataProvider.InitializeAsync()` before accepting requests. It loads sectors, subsectors, allegiances and systems once, using a short-lived database scope, and publishes a complete immutable `SystemCatalogue`. Concurrent initialization callers share the same task; a failed load prevents startup rather than exposing partial data. After a refresh, the provider reloads and atomically publishes a complete replacement snapshot; a failed reload leaves the prior snapshot available.

`SearchService`, `MapDataService` and `WorldSelectionService` share that catalogue across requests and Blazor circuits. Search retains its existing all-milieu scope, result grouping/limits and subsector context. Map and selection retain the M1105 filter. Map overview and per-sector DTOs are rebuilt when the catalogue changes, preserving existing API response shapes. Search returns fresh UI result objects so edits cannot affect shared data.

`CatalogueSystem` owns a stable system ID, location, `MainWorldData` and an immutable `Bodies` collection. Main-world data retains all currently stored system attributes. Bodies are currently empty; future `SystemBodyData` record types can supply stars, planets, moons and orbital information for an orrery without changing search or map contracts. No bodies or orbital positions are inferred from summary counts. Add future body-table loading to the provider's startup load and publish it as part of the same snapshot.

The runtime checks initialize concurrently, verify one read per source table, then close the test database and exercise search, map and selection. They also cover empty catalogues, milieu filtering, cancellation and protection from mutable search results.

## Refreshing sector data

Refresh Sector Data updates every sector in the configured M1105 area. For each sector it retrieves system data and TravellerMap metadata before replacing the sector's names, subsectors, allegiances, systems, borders and routes in a single database transaction. Existing sector IDs are retained. No sector or subsector image tiles are downloaded. A failed download leaves that sector's previous data intact and is counted in the progress dialog; cancellation retains already committed sectors.

Border paths and route endpoints (including cross-sector offsets) are preserved. Explicit colours take precedence over matching TravellerMap stylesheet rules; the original stylesheet and metadata fields are retained for future rendering. After a completed, cancelled, or interrupted refresh, the shared catalogue reloads any committed sector updates; open maps reload their data and clear their selection.

## Trading

Select a world on the map, then open Trading. Broker and Streetwise generate and save independent markets for each sector and hex. Common and matching goods use their listed lot sizes; extra D66 rolls add lots to existing entries. Broker rerolls illegal goods; Streetwise marks randomly rolled legal goods as stolen. Exotic goods have referee-determined tonnage. Each purchase price per ton uses a 3D roll, the saved skill DM, the strongest matching purchase DM and the strongest matching sale DM subtracted. The supplier DM is entered as a signed modifier and defaults to −2. Existing priced markets retain their original prices until rerolled. The resulting percentage and prices are saved with the market. Existing markets receive prices once on first opening without changing their goods or quantities. Markets survive system-row replacement during sector refresh.

Buying and Selling each have an independent active Traveller dropdown followed by mode controls. Each displays the selected Traveller total DM: Broker or Streetwise skill level plus the higher INT or EDU DM of the same Traveller. Missing skill uses the untrained -3 penalty reduced by the same Traveller's Jack-of-All-Trades level, to a minimum penalty of zero; skill zero is trained and receives no Jack-of-All-Trades bonus. This also applies to untrained Steward passenger modifiers. Jack-of-All-Trades does not grant Steward training or enable High passage. Unknown trading INT or EDU defaults to score 7 (DM 0), equivalent to UPP 777777 when the whole profile is unknown; the stored UPP is unchanged. Select a Traveller separately for each panel; Skill DM is blank and rolling is disabled until an active Traveller is selected. Buying and Selling Traveller and mode selections are remembered independently per campaign during the current user session, including navigation away from Trading. Inactive or deleted selections are cleared on reload. Modifiers refresh before each roll; saved markets retain the DM used when rolled. Current and destination worlds display UWP badges on the left and trade codes and travel zone badges on the right, matching Freight. Selling (Imports) can use the current world or an alphabetically sorted destination within six parsecs. Remote worlds load or generate their own saved selling market and show a red seven-day-old price badge. Selling lists goods with a positive matching Sale DM. Broker also includes common goods and excludes illegal goods; Streetwise excludes common goods and can include illegal goods. Roll calculates the sale price per ton from 3D plus the Selling section’s selected skill DM and strongest Sale DM, minus the strongest Purchase DM, plus the signed Buyer DM (default −2). Demand in tons is rolled using each commodity’s listed tonnage dice. Older saved Imports receive demand once without changing their prices. Sale results are saved independently by world and mode and marked outdated when reopened. They are sorted from highest to lowest price percentage.

The D66 goods, availability codes and lot sizes are based on [Trade Goods Dice Modifiers](https://github.com/timtoon/trade-goods) by Tim Heiderich (Mongoose Traveller 2nd edition, 2016); the aid identifies itself as CC BY-NC 4.0. Its ice-world availability codes have been normalized to TravellerMap's `Ic` code.

## Checks

```powershell
dotnet build MTUAssistant.slnx
dotnet run --project MTU.RuntimeChecks/MTU.RuntimeChecks.csproj
node MTUAssistant/Components/Map/MapGeometry.test.mjs
node MTUAssistant/Components/Map/WorldSelection.test.mjs
```

## Equipment

Equipment contains a shared database-backed catalogue of Armour and Weapons. Add, Edit and Delete use the same native modal dialog available from Traveller equipment. Armour and weapon notes appear in a muted Notes column. Range is numeric, nonnegative metres and accepts fractional values; tables display it as `<Range> m`. Damage, magazine, required skill and traits are text fields to allow rulebook notation; weights and credit costs accept decimals.

Open a Traveller's Equipment panel to select catalogue items and add or remove them. New Equipment opens the shared dialog and selects the newly saved item, ready for Add Equipment. Each catalogue item can be assigned once to each Traveller. The Traveller table shows comma-separated summaries: `Name (protection)` for armour and `Name (rangem, damage)` (compact metres in Traveller summaries) for weapons. Editing an assigned item updates the shared catalogue for all Travellers; removing it from a Traveller leaves the catalogue intact. Deleting a catalogue item confirms that it will also remove all assignments. Deleting a Traveller or campaign removes its assignments and preserves the catalogue.

The Equipment migration runs at application startup and retains existing campaigns, Travellers, skills and equipment-independent data. Runtime checks cover field persistence, summary formatting, assignment isolation, duplicate and invalid input handling, shared edits, removal, and cascading deletion.

### Traveller equipment options

Traveller equipment assignments store Quantity (minimum 1) and optional Scope, Targeting System, Laser Sight, Holographic Sight, FOF, PHUD and Smart Tracker flags. Existing assignments migrate to quantity 1 with no attachments. These settings are independent for each Traveller and do not modify the shared catalogue. Add Equipment opens a second dialog before the assignment is saved; Options reopens it for an existing assignment. Cancel leaves the saved assignment unchanged.

Attachments are available only for weapons whose numeric Range is greater than zero; range zero is treated as melee. Smart Tracker implies Intelligent Weapon. Quantity appears in both Traveller equipment tables. Weapon attachment summaries appear in secondary text beneath catalogue Traits, using labels such as `Scope, Laser Sight, PHUD`. Weight and credit columns retain their catalogue values per item.

## Sector Data view

The Refresh Sector Data navigation item opens `/sector-data` without starting a refresh. The page reads database counts for sectors, sector names, subsectors, systems, allegiances, borders and routes, and lists loaded milieus. Refresh Sector Data Now runs the original refresh action and progress dialog. While a refresh is running the button can reopen its progress dialog. Statistics reload after completion, cancellation or failure so any committed partial updates are reflected.

A completed refresh stores its real UTC completion timestamp in the database. The Last Refresh badge displays that date in the browser's local time as yyyy-MM-dd, with the full local timestamp on hover. Cancelled or faulted refreshes retain the previous completed timestamp. A completed run may include individual sector failures, as reported by the existing progress dialog. Until a refresh is recorded, the badge displays Not recorded. Counts use an independent database scope and a read transaction to avoid sharing the refresh worker's DbContext.

### Population statistics

Sector Data includes total known main-world population for loaded charted space and the ten largest populations by major polity, followed by Others. Rows show exact whole-person totals and percentage shares. The first two letters of allegiance codes define the major polity, case-insensitively, across sectors within one milieu. All Im* allegiances aggregate as Third Imperium (Im). Other names use two-letter metadata when available, a common recorded family name, or the prefix as a fallback. Unknown allegiance is retained as Unspecified polity. If several milieus are loaded, a selector keeps their populations separate and defaults to M1105 when available.

Population follows Traveller Map Second Survey: PBG population multiplier times 10 raised to the UWP population exponent. A zero multiplier with a positive exponent uses the documented legacy multiplier of one. Unknown or invalid population data is excluded and reported as a system count; no population is inferred for other system bodies. Decimal arithmetic avoids aggregate 64-bit integer overflow. The top ten plus Others reconcile exactly with the displayed total, and refresh completion recalculates the figures from the database. Source: https://travellermap.com/doc/secondsurvey#population-exponent
