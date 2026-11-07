# MTU Assistant: instructions for Claude Code

A GM Assistant for running **Traveller** (Mongoose 2nd Edition) sessions. The main user is Paul, the GM, at the table and in the middle of a session. Optimise for speed and clarity: few clicks, readable at a glance, nothing that stalls play.

The app is well advanced. Extend and refine what's here; don't redesign it. If a request conflicts with this file, the request wins. Point out the conflict and suggest updating this file.

## Ground rules

- **Never change git state.** Paul handles all commits, pushes and pulls. Don't run `commit`, `push`, `pull`, `fetch`, `merge`, `rebase`, `checkout`/`switch`, `branch`, `stash`, `reset`, `restore`, `clean` or `add`. Read-only commands (`status`, `diff`, `log`) are fine.
- **Check `git status` before starting.** If there's uncommitted work, ask whether to wait or build on top of it.
- **When a change is ready**, list the changed files and suggest a one-line commit message.
- **Don't touch Paul's real database** in `%LOCALAPPDATA%\MTU` (SQLite). The app applies EF migrations to it automatically at startup, so **don't launch the app after adding a migration** without asking first. The RuntimeChecks use their own in-memory databases, so they're safe.
- **Ask before:** migrations or other changes that could lose campaign data, deleting files or data, swapping frameworks or libraries, changing a rules interpretation, or building anything out of scope.
- **Otherwise, decide.** For routine or easily reversible choices, go with sensible defaults and list the choices you made at the end.
- **One feature or fix at a time,** so each makes a clean commit. Explain what changed, why, and how to test it.

## Scope

- **In scope:** campaigns, Travellers (PCs), Patrons and Enemies (NPCs), animals/creatures, equipment, Scenes and the Battlemap (tokens, fog, combat tracker, measuring), the Galaxy Map, trading, cargo and passengers, and the Cast view.
- **Out of scope for now:**
  - Ships and starship combat. Trading may treat "the party's ship" as a simple cargo/credits container.
  - Player interaction with the app.
  - Cloud hosting and multi-user access.
  - Rules supplements, except Central Supply Catalogue equipment, which may be added as data.
- **Not built yet:** Players as separate from Travellers, Encounter Areas, JSON export/import, homebrew world data, and an audit/session log. Raise the design before building any of these.

## Rules baseline

- Use the **MgT2 Core Rulebook** for rules, terms and stat formats:
  - Checks are 2D6 + DMs vs 8+, and Effect = roll − target.
  - Boon/Bane: roll 3D6 and keep the best or worst two.
  - Characteristic DMs follow the standard scale (0 → −3, 6–8 → 0, 15+ → +3).
  - An untrained skill is DM −3, reduced by Jack-of-All-Trades.
  - World profiles use UWP with eHex.
  - Squares are 1.5 m.
- **Setting:** Third Imperium, milieu **M1105**. Sector data comes from the Traveller Map API, stored locally. A sector refresh replaces a sector's systems in one transaction, so key campaign data attached to worlds by sector + hex.
- **Unclear rules:** say so rather than guessing, and mark the assumption in code with `// RULES-ASSUMPTION:`.
- **House rules** must be configurable and labelled, not hard-coded.
- **Copyright:** don't paste rulebook text. Write short original summaries. Keep the CC BY-NC credit for Tim Heiderich's *Trade Goods Dice Modifiers* in the README.

## Stack and layout

- **.NET 10** with nullable enabled. A **Blazor Web App** with Interactive Server rendering applied globally.
- **EF Core 10 + SQLite.** Migrations are applied automatically at startup.
- **UI:** Bootstrap 5 dark theme with no component library; use plain Razor components with scoped `.razor.css`.
- **Maps:** PixiJS + pixi-viewport in JS modules under `MTUAssistant/wwwroot/js`, called through `IJSRuntime` imports.

| Project | Purpose |
|---|---|
| `MTU.Core` | Rules, services, EF entities, `MtuDbContext`, migrations, generators, combat, trade, map projections |
| `MTU.Mapping` | Minimal-API map endpoints |
| `MTU.TravellerMap` | travellermap.com client |
| `MTUAssistant` | Blazor app: pages, layout, map components, UI state services |
| `MTU.RuntimeChecks` | Console app of deterministic checks (the test suite). **Not in the `.slnx`**, so run it explicitly. |

Use the names already in the code:
- **Travellers** are PCs and **Patrons and Enemies** are NPCs. Both share the `Characters` table, and NPCs add an `NPCs` profile.
- **Scenes** are battlemap definitions.
- The **Combat Tracker** holds the per-scene encounter roster.

## Code conventions

- **Domain logic lives in `MTU.Core`,** in plain C# with no Blazor dependencies. Keep Razor pages and JS thin. Put testable JS logic in pure exported functions.
- **Services** are registered in `ServiceCollectionExtensions.AddMtuDatabase`. Data services are scoped. The catalogue services (`SystemDataProvider`, `SearchService`, `MapDataService`, `WorldSelectionService`) are singletons publishing immutable snapshots, so never mutate them.
- **Campaign isolation:** every campaign-owned query and command is scoped by `campaignId` and rejects cross-campaign IDs.
- **Large binaries** (scene images, tokens) go through campaign-scoped endpoints, never Blazor UI state.
- **Randomness:** follow the existing approach in the class you're changing. Don't add a parallel dice abstraction without discussing it first.
- **Dependencies:** don't add NuGet or JS packages without saying why.

## Migrations

- Every schema change needs a new migration in `MTU.Core/Data/Migrations` that preserves existing data.
- **IDs:** existing migrations use hand-picked timestamps (e.g. `20261107050000_SceneFogEnabled`), some later than the real time. Rename a generated migration's ID (file names and the `[Migration("…")]` attribute) so it sorts after the latest one.
- **Model snapshot:** `MtuDbContextModelSnapshot.cs` has been hand-edited and isn't in EF's canonical order. Don't replace it with a regenerated one; add only the new lines. Then confirm with `dotnet ef migrations has-pending-model-changes --project MTU.Core --startup-project MTU.Core`.
- **`Down()`:** EF can't rebuild SQLite tables when the previous migration has no Designer file (e.g. `TrackerInstanceNames`). Use `migrationBuilder.Sql("ALTER TABLE … DROP COLUMN …;")` instead of `DropColumn`. The RuntimeChecks roll migrations back, so they'll catch this.
- **Migration-history checks** (e.g. `InstanceNameChecks`, the rename rollback in `RuntimeChecks/Program.cs`) migrate to an older schema and then insert data. Insert that data with raw SQL, not with current services or entities, or every new column breaks them.

## Formatting: CSharpier

- Format every C# file you create or change with **CSharpier, default settings**, e.g. `csharpier format <files>` or `dotnet csharpier format <files>`, depending on how it's installed. Don't add a `.csharpierrc` or `.editorconfig`.
- The whole solution is CSharpier-clean, so `csharpier check .` should report nothing. If it does, mention it to Paul rather than fixing it in an unrelated change.
- Format only the files you touched. Don't reformat the solution in a feature change.
- CSharpier skips `// <auto-generated />` files, such as migration Designers. That's fine.
- CSharpier doesn't format `.razor`, `.css` or `.js`. Match the surrounding style in those by hand.
- `.gitattributes` has `* text=auto`. Keep each file's existing line endings.

## Checks (run before saying a change is done)

There's no xUnit/NUnit project.
- **C# rules and data:** add checks in `MTU.RuntimeChecks/<Area>Checks.cs` and wire them into `Program.cs`.
- **JS logic:** add or extend `*.test.mjs` beside the components in `MTUAssistant/Components/Map/`.

```powershell
dotnet build MTUAssistant.slnx
dotnet run --project MTU.RuntimeChecks/MTU.RuntimeChecks.csproj
Get-ChildItem MTUAssistant -Recurse -Filter *.test.mjs | ForEach-Object { node $_.FullName }
csharpier check .
```

**Baseline (7 Nov 2026):** 0 warnings, 0 errors, all RuntimeChecks pass, all 16 `.mjs` tests pass. A new warning counts as a regression. If you can't run something, say so plainly. Paul checks the UI in a browser himself.

## README is the feature spec

`README.md` documents each feature's behaviour, edge cases, migrations and the checks that cover it. Update the matching section, or add one, in the same style with every feature change.

## Cast view (player display)

The GM's cast icon opens two read-only player pages for a second monitor or TV:
- `/presentation` shows the Galaxy Map and System Data panel.
- `/battlemap-presentation` shows the Battlemap, player combat tracker and actions.

Each GM tab sends snapshots over a named **BroadcastChannel** and caches them in local storage. Both windows must be in the same browser profile and origin.

- **Read-only.** The GM drives everything, and the presentation has no editing or panning.
- **No GM-only information.** This is the most important rule. Hidden or unplaced combatants, fogged squares, NPC/creature stats and HP, GM notes, and anything starting or ending in fog (e.g. measurements) must never enter the player snapshot.
- **Filter when the snapshot is built** (`fogPublicState`, the `player*` helpers in `BattlemapPresentationSync.js`), and validate in `validBattlemapSnapshot`. Never send GM data and hide it in the presentation page.
- **Every new snapshot field needs a privacy check** in the matching `.test.mjs`.
- **Display-friendly:** large, high-contrast text with no hover-only information, and it should work full-screen on a TV.
- It's a UI convenience, not a security boundary.
- **Future (don't build):** per-player cloud views. Don't deepen same-browser assumptions more than needed, and keep the GM/player split explicit in the snapshot-building code.

## Battlemap interaction (current behaviour)

- **Left-drag** moves a token or pans the map. **Wheel** zooms.
- **Fog** is switched on or off per scene by the cloud toolbar button (`Scenes.FogEnabled`, off by default). When fog is off, the map gets `fog = null`.
- **Fog editing:** Shift+drag clears fog and Ctrl+drag adds it. Shift/Ctrl+wheel changes the brush size (1, 3, 5 or 9).
- **Right-drag** targets during combatant targeting. Otherwise it measures square centre to square centre, and the measurement is cast to players when both ends are revealed.
- The browser context menu is suppressed on the GM map.
