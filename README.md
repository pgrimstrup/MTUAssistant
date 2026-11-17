# MTUAssistant

MTUAssistant is a browser-based GM assistant for Mongoose Traveller 2nd Edition. Use it to explore Charted Space, prepare campaign characters and encounters, and run trading, travel and combat at the table.

## What you can do

- **Galaxy Map** — Explore the M1105 atlas, search for worlds, sectors and subsectors, and inspect world profiles, trade codes and travel zones. Right-click and drag to measure jump distance in parsecs and mark a jump destination. The map remembers your position, zoom, selected world and destination in the same browser.
- **Trading, Passengers and Cargo** — Generate Broker and Streetwise markets, roll purchase and sale prices, and determine available passengers and freight between worlds. Passengers and Cargo starts with the Galaxy Map jump destination selected when it is within range.
- **Campaign characters** — Manage Travellers, generate patrons and enemies, and prepare animals and creatures. Track skills and equipment using the shared Supply Catalog.
- **Scenes and Battlemap** — Upload scene images, configure a grid, place tokens, and run encounters with the Combat Tracker, fog of war and cover protection.
- **Player presentation** — Cast the Galaxy Map or Battlemap to a separate read-only window in the same browser. On the Battlemap, players see revealed areas and visible tokens.
- **Sector Data** — Refresh Traveller Map data and review loaded sectors, systems and population statistics.

## Run locally

With the .NET 11 SDK installed, run this command from the repository root and open the local URL printed in the terminal.

```powershell
dotnet run --project MTUAssistant/MTUAssistant.csproj
```

See [Architecture](ARCHITECTURE.md) for implementation decisions, persistence, detailed feature rules and verification commands.
