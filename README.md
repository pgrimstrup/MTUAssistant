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

Buying and Selling have independent mode and skill DM controls. Selling (Imports) can use the current world or an alphabetically sorted destination within six parsecs. Remote worlds load or generate their own saved selling market and show a red seven-day-old price badge. Selling lists goods with a positive matching Sale DM. Broker also includes common goods and excludes illegal goods; Streetwise excludes common goods and can include illegal goods. Roll calculates the sale price per ton from 3D plus the Selling section’s selected skill DM and strongest Sale DM, minus the strongest Purchase DM, plus the signed Buyer DM (default −2). Demand in tons is rolled using each commodity’s listed tonnage dice. Older saved Imports receive demand once without changing their prices. Sale results are saved independently by world and mode and marked outdated when reopened. They are sorted from highest to lowest price percentage.

The D66 goods, availability codes and lot sizes are based on [Trade Goods Dice Modifiers](https://github.com/timtoon/trade-goods) by Tim Heiderich (Mongoose Traveller 2nd edition, 2016); the aid identifies itself as CC BY-NC 4.0. Its ice-world availability codes have been normalized to TravellerMap's `Ic` code.

## Checks

```powershell
dotnet build MTUAssistant.slnx
dotnet run --project MTU.RuntimeChecks/MTU.RuntimeChecks.csproj
node MTUAssistant/Components/Map/MapGeometry.test.mjs
node MTUAssistant/Components/Map/WorldSelection.test.mjs
```
