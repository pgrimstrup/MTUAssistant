# MTUAssistant

The home-page atlas is drawn directly from the M1105 sector/world database using PixiJS and pixi-viewport. Political borders and routes are not rendered. Downloaded PNG tiles are not required by the map; the existing tile-download utility remains available.

## Map detail

Detail switches according to CSS pixels per parsec (not device pixel ratio):

| Zoom | Detail |
| --- | --- |
| Below 10 | World circles; capital names (Cp, Cs, Cx); no hex grid |
| 10–below 25 | World circles and hex grid; subsector capital (Cp) names only |
| 25–below 55 | World circles, hex grid, gas giants, bases and travel-zone rings; starport class; names for capitals and population 8+ |
| 55–below 95 | World circles, hex grid, gas giants, bases and travel-zone rings; full UWP and every world name |
| 95 and above | All details, including hex coordinates |

Map label fonts are 20% smaller at all zoom levels below 95 px/pc; full-detail fonts at 95+ are unchanged. Sector and subsector boundaries draw at every zoom. Label eligibility follows fixed bands in `wwwroot/js/MapGeometry.js`; no collision rejection, text budget, or zoom history suppresses eligible labels. Only off-screen text is culled. Subsector capital names are red; population 8+ names are uppercase (high-population eligibility uses 8+). Sector names remain visible, with subsector region names from 25 px/pc.

At close zoom, UWP sits at the top of the hex, below coordinates when shown; names have a four-pixel bottom margin. World circles grow with zoom. Zone rings have a minimum 2 CSS pixel stroke at every zoom level where they are displayed. Amber/red zone rings use 35% opacity and do not affect symbol positions. Naval and scout symbols sit at upper/lower left; the gas giant is inset at upper right, aligned with the naval symbol. Click a marker or hex to select it; drag to pan. The viewport is remembered between visits.

`GET /api/map/overview` supplies sector metadata, world positions and compact world name/UWP/remarks data so zoomed-out capital labels are available immediately. The browser fetches detailed map attributes from `GET /api/map/sectors/{id}` for nearby sectors, with bounded concurrent requests and a client-side sector cache; these endpoints read cached server projections, not the database. Map descriptions and data projections live in MTU.Core; rendering geometry and detail thresholds live in `MTUAssistant/wwwroot/js/MapGeometry.js`.

## Shared system catalogue

After database migration, application startup awaits the singleton `SystemDataProvider.InitializeAsync()` before accepting requests. It loads sectors, subsectors, allegiances and systems once, using a short-lived database scope, and publishes a complete immutable `SystemCatalogue`. Concurrent initialization callers share the same task; a failed load prevents startup rather than exposing partial data. Database imports or edits become visible after restarting the application.

`SearchService`, `MapDataService` and `WorldSelectionService` share that catalogue across requests and Blazor circuits. Search retains its existing all-milieu scope, result grouping/limits and subsector context. Map and selection retain the M1105 filter. Map overview and per-sector DTOs are prepared once, preserving existing API response shapes. Search returns fresh UI result objects so edits cannot affect shared data.

`CatalogueSystem` owns a stable system ID, location, `MainWorldData` and an immutable `Bodies` collection. Main-world data retains all currently stored system attributes. Bodies are currently empty; future `SystemBodyData` record types can supply stars, planets, moons and orbital information for an orrery without changing search or map contracts. No bodies or orbital positions are inferred from summary counts. Add future body-table loading to the provider's startup load and publish it as part of the same snapshot.

The runtime checks initialize concurrently, verify one read per source table, then close the test database and exercise search, map and selection. They also cover empty catalogues, milieu filtering, cancellation and protection from mutable search results.

## Checks

```powershell
dotnet build MTUAssistant.slnx
dotnet run --project MTU.RuntimeChecks/MTU.RuntimeChecks.csproj
node MTUAssistant/Components/Map/MapGeometry.test.mjs
node MTUAssistant/Components/Map/WorldSelection.test.mjs
```
