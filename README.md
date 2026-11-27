# MTUAssistant

MTUAssistant is a browser-based GM assistant for Mongoose Traveller 2nd Edition. Use it to explore Charted Space, prepare campaign characters and encounters, and run trading, travel and combat at the table.

## What you can do

- **Galaxy Map** — Explore the M1105 atlas, search for worlds, sectors and subsectors, and inspect world profiles, trade codes and travel zones. Right-click and drag to measure jump distance in parsecs and mark a jump destination. The map remembers your position, zoom, selected world and destination in the same browser.
- **Trading, Passengers and Cargo** — Generate Broker and Streetwise markets, roll purchase and sale prices, and determine available passengers and freight between worlds. Passengers and Cargo starts with the Galaxy Map jump destination selected when it is within range.
- **Campaign characters** — Manage Travellers, generate patrons and enemies, and prepare animals and creatures. Record each character's career term by term, with ranks and age worked out for you, and track skills and equipment using the shared Supply Catalog. Travellers and NPCs open as a character sheet showing their characteristics with DMs, the current STR, DEX and END after damage, and tabbed career, skills, armour and weapons records.
- **Scenes and Battlemap** — Upload scene images, configure a grid, place tokens, and run encounters with the Combat Tracker, fog of war and cover protection. An experimental Polygon mode lets the GM draw solid areas on the map, snapped to the grid and saved with the scene, and click them open or closed like doors. With fog on, an experimental line-of-sight option clears fog from what the selected Traveller can see, as far as the map's Ambient Light or the Traveller's own Light, whichever is higher, with the closed polygons blocking the view.
- **Player presentation** — Cast the Galaxy Map or Battlemap to a separate read-only window in the same browser. On the Battlemap, players see revealed areas and visible tokens.
- **Sector Data** — Refresh Traveller Map data and review loaded sectors, systems and population statistics.

## Run locally

With the .NET 11 SDK installed, run this command from the repository root and open the local URL printed in the terminal.

```powershell
dotnet run --project MTUAssistant/MTUAssistant.csproj
```

See [Architecture](ARCHITECTURE.md) for implementation decisions, persistence, detailed feature rules and verification commands.
