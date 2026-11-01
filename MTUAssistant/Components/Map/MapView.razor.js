import { observeClicks } from "../../js/WorldSelection.js";
import { HEX_X, SECTOR_WIDTH, SECTOR_HEIGHT, MAP_PADDING, DETAIL_LEVELS,
    hexCenter, findHex, parseHex, detailLevel, intersects, worldPriority, worldLabelRules,
    mapAppearance, baseSymbols, isSubsectorCapital, worldName } from "../../js/MapGeometry.js";

const MAX_ZOOM = 192;
const VIEW_STORAGE_KEY = "mtu.map.view";
const MAX_CACHED_SECTORS = 24;
const MAX_REQUESTS = 4;
let scriptsPromise;

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const script = document.createElement("script");
        script.src = new URL(src, document.baseURI).href;
        script.onload = resolve;
        script.onerror = () => { script.remove(); reject(new Error(`Failed to load ${src}`)); };
        document.head.appendChild(script);
    });
}

async function loadLibraries() {
    scriptsPromise ??= (async () => {
        if (!globalThis.PIXI) await loadScript("lib/pixi/pixi.min.js");
        globalThis.pixi_js = globalThis.PIXI;
        if (!globalThis.pixi_viewport) await loadScript("lib/pixi-viewport/pixi-viewport.umd.js");
    })().catch(error => { scriptsPromise = null; throw error; });
    await scriptsPromise;
}

export async function createMap(host, statusEl, callback) {
    await loadLibraries();
    const map = new DynamicMap(host, statusEl, callback);
    try { await map.start(); return map; }
    catch (error) { map.dispose(); throw error; }
}

export class DynamicMap {
    constructor(host, statusEl, callback) {
        Object.assign(this, { host, statusEl, callback });
        this.abort = new AbortController();
        this.sectorCache = new Map();
        this.markers = [];
        this.labels = [];
        this.textures = new Map();
        this.activeRequests = 0;
        this.lastMarkerCount = this.lastLabelCount = 0;
        this.lastView = "";
        this.lastUpdate = 0;
        this.disposed = false;
    }

    async start() {
        await this.loadOverview();
        if (this.disposed) return;

        this.app = new PIXI.Application();
        await this.app.init({ resizeTo: this.host, background: 0x070b13, antialias: true,
            autoDensity: true, resolution: Math.min(window.devicePixelRatio || 1, 2) });
        if (this.disposed) { this.app.destroy(true, { children: true }); this.app = null; return; }
        this.host.appendChild(this.app.canvas);
        this.viewport = new pixi_viewport.Viewport({ screenWidth: this.host.clientWidth,
            screenHeight: this.host.clientHeight, worldWidth: this.worldW, worldHeight: this.worldH,
            events: this.app.renderer.events });
        this.app.stage.addChild(this.viewport);
        this.viewport.drag().pinch().wheel({ smooth: 6 }).decelerate({ friction: .92 });
        this.viewport.interactiveChildren = false;

        this.grid = new PIXI.Graphics();
        this.boundaries = new PIXI.Graphics();
        this.markerLayer = new PIXI.Container();
        this.labelLayer = new PIXI.Container();
        this.selection = new PIXI.Graphics();
        for (const layer of [this.grid, this.boundaries, this.markerLayer, this.labelLayer, this.selection]) {
            layer.eventMode = "none";
            this.viewport.addChild(layer);
        }
        this.removeClickObserver = observeClicks(this.app.canvas, event => this.selectWorld(event));
        this.resizeObserver = new ResizeObserver(() => this.onResize());
        this.resizeObserver.observe(this.host);
        this.onResize();
        if (!this.restoreView()) this.fit();
        this.tick = () => this.onTick();
        this.app.ticker.add(this.tick);
        this.update();
    }

    async loadOverview() {
        const response = await fetch(new URL("api/map/overview", document.baseURI), { signal: this.abort.signal });
        if (!response.ok) throw new Error(`World database request failed: ${response.status}`);
        this.overview = await response.json();
        if (this.disposed) return;
        this.worldW = this.overview.columns * SECTOR_WIDTH + MAP_PADDING * 2;
        this.worldH = this.overview.rows * SECTOR_HEIGHT + MAP_PADDING * 2 + .5;
        this.sectors = this.overview.sectors.map(sector => {
            const col = sector.x - this.overview.minSectorX, row = sector.y - this.overview.minSectorY;
            const result = { ...sector, col, row,
                rect: { x: MAP_PADDING + col * SECTOR_WIDTH, y: MAP_PADDING + row * SECTOR_HEIGHT,
                    w: SECTOR_WIDTH, h: SECTOR_HEIGHT + .5 } };
            result.points = sector.worlds.map(world => this.positionWorld(result, world)).filter(Boolean);
            return result;
        });

    }

    async reloadData() {
        if (this.disposed) return;
        const previousOverview = this.overview;
        const centre = this.viewport?.center;
        for (const entry of this.sectorCache.values()) entry.controller?.abort();
        this.sectorCache.clear();
        await this.loadOverview();
        if (this.disposed) return;
        this.selected = null;
        this.onResize();
        if (centre && previousOverview) this.viewport.moveCenter(
            centre.x + (previousOverview.minSectorX - this.overview.minSectorX) * SECTOR_WIDTH,
            centre.y + (previousOverview.minSectorY - this.overview.minSectorY) * SECTOR_HEIGHT);
        this.lastView = "";
        this.update();
    }

    positionWorld(sector, world) {
        const hex = parseHex(world.hex);
        if (!hex) return null;
        const column = sector.col * 32 + hex.column - 1, row = sector.row * 40 + hex.row - 1;
        return { ...world, ...hexCenter(column, row), column, row, sector,
            priority: worldPriority(world), key: `${sector.id}/${world.hex}` };
    }

    get fitZoom() { return Math.min(this.host.clientWidth / this.worldW, this.host.clientHeight / this.worldH); }

    onResize() {
        if (!this.viewport || !this.host.clientWidth || !this.host.clientHeight) return;
        this.viewport.resize(this.host.clientWidth, this.host.clientHeight, this.worldW, this.worldH);
        this.viewport.clampZoom({ minScale: this.fitZoom, maxScale: MAX_ZOOM });
        this.updatePanBounds();
        this.lastView = "";
    }

    updatePanBounds() {
        if (!this.allowEdgeCentering) {
            this.viewport.clamp({ direction: "all", underflow: "center" });
            return;
        }
        this.viewport.clamp({ left: -this.viewport.worldScreenWidth / 2,
            right: this.worldW + this.viewport.worldScreenWidth / 2,
            top: -this.viewport.worldScreenHeight / 2,
            bottom: this.worldH + this.viewport.worldScreenHeight / 2 });
    }

    fit() {
        this.viewport.setZoom(this.fitZoom, true);
        this.viewport.moveCenter(this.worldW / 2, this.worldH / 2);
        this.lastView = "";
    }

    zoomIn() { this.zoomBy(2); }
    zoomOut() { this.zoomBy(.5); }
    zoomBy(factor) {
        this.viewport.animate({ time: 250,
            scale: Math.min(MAX_ZOOM, Math.max(this.fitZoom, this.viewport.scale.x * factor)), ease: "easeInOutSine" });
    }

    onTick() {
        if (this.disposed) return;
        const v = this.viewport;
        const signature = `${v.left.toFixed(2)},${v.top.toFixed(2)},${v.scale.x.toFixed(4)},${this.host.clientWidth},${this.host.clientHeight}`;
        const retryDue = [...this.sectorCache.values()].some(entry => entry.state === "failed" && entry.retryAt <= Date.now());
        if ((signature !== this.lastView || retryDue) && performance.now() - this.lastUpdate > 70) {
            this.lastView = signature;
            this.lastUpdate = performance.now();
            this.update();
            this.saveView();
        }
    }

    update() {
        const v = this.viewport, zoom = v.scale.x;
        const level = detailLevel(zoom);
        const view = { x: v.left, y: v.top, w: v.worldScreenWidth, h: v.worldScreenHeight };
        const margin = 3;
        const expanded = { x: view.x - margin, y: view.y - margin, w: view.w + margin * 2, h: view.h + margin * 2 };
        const sectors = this.sectors.filter(sector => intersects(sector.rect, expanded));
        this.ensureDetails(level >= 1 ? sectors : []);
        this.markerIndex = this.labelIndex = 0;
        this.drawGrid(view, sectors, level, zoom);
        this.drawRegionLabels(sectors, view, level, zoom);
        const worlds = sectors.flatMap(sector => this.sectorCache.get(sector.id)?.worlds ?? sector.points)
            .filter(world => world.x >= expanded.x && world.x <= expanded.x + expanded.w
                && world.y >= expanded.y && world.y <= expanded.y + expanded.h);
        this.visibleWorlds = worlds;
        for (const world of worlds) {
            this.drawWorld(world, level, zoom);
            this.drawWorldLabels(world, view, level, zoom);
        }
        for (let i = this.markerIndex; i < this.lastMarkerCount; i++) this.markers[i].visible = false;
        for (let i = this.labelIndex; i < this.lastLabelCount; i++) this.labels[i].visible = false;
        this.lastMarkerCount = this.markerIndex;
        this.lastLabelCount = this.labelIndex;
        this.drawSelection(zoom);
        const pending = sectors.filter(sector => !this.sectorCache.get(sector.id)?.worlds).length;
        const failed = sectors.filter(sector => this.sectorCache.get(sector.id)?.state === "failed").length;
        this.statusEl.textContent = this.sectors.length === 0 ? "No sector data — import world data to populate the map."
            : `${DETAIL_LEVELS[level].name} · ${zoom.toFixed(1)} px/pc · ${worlds.length.toLocaleString()} worlds`
                + (level >= 1 && failed ? ` · ${failed} sectors unavailable; retrying` : level >= 1 && pending ? " · loading sector data…" : "");
    }

    ensureDetails(sectors) {
        const wanted = new Set(sectors.map(sector => sector.id));
        for (const [id, entry] of this.sectorCache) {
            if (entry.state === "failed" && !wanted.has(id)) this.sectorCache.delete(id);
            if (entry.state === "loading" && !wanted.has(id)) {
                entry.controller.abort(); this.sectorCache.delete(id);
            }
        }
        sectors.sort((a, b) => Math.hypot(a.rect.x - this.viewport.center.x, a.rect.y - this.viewport.center.y)
            - Math.hypot(b.rect.x - this.viewport.center.x, b.rect.y - this.viewport.center.y));
        for (const sector of sectors) {
            let entry = this.sectorCache.get(sector.id);
            if (entry) entry.lastUsed = performance.now();
            if (entry?.state === "ready" || entry?.state === "loading" || entry?.retryAt > Date.now()) continue;
            if (this.activeRequests >= MAX_REQUESTS) break;
            entry = { state: "loading", controller: new AbortController(), lastUsed: performance.now() };
            this.sectorCache.set(sector.id, entry);
            this.activeRequests++;
            this.loadSector(sector, entry);
        }
        if (this.sectorCache.size > MAX_CACHED_SECTORS) {
            const old = [...this.sectorCache].filter(([id, entry]) => !wanted.has(id) && entry.state !== "loading")
                .sort((a, b) => a[1].lastUsed - b[1].lastUsed);
            for (const [id] of old) {
                if (this.sectorCache.size <= MAX_CACHED_SECTORS) break;
                this.sectorCache.delete(id);
            }
        }
    }

    async loadSector(sector, entry) {
        try {
            const response = await fetch(new URL(`api/map/sectors/${sector.id}`, document.baseURI), { signal: entry.controller.signal });
            if (!response.ok) throw new Error(`Sector data request failed: ${response.status}`);
            const data = await response.json();
            if (this.disposed || this.sectorCache.get(sector.id) !== entry) return;
            entry.worlds = data.map(world => this.positionWorld(sector, world)).filter(Boolean);
            entry.state = "ready";
        } catch (error) {
            if (error.name !== "AbortError" && !this.disposed) {
                entry.state = "failed"; entry.retryAt = Date.now() + 5000;
                console.warn(error);
            }
        } finally {
            this.activeRequests--;
            this.lastView = "";
        }
    }

    drawGrid(view, sectors, level, zoom) {
        this.grid.clear(); this.boundaries.clear();
        const x1 = Math.max(0, Math.floor((view.x - MAP_PADDING) / HEX_X) - 1);
        const x2 = Math.min(this.overview.columns * 32 - 1, Math.ceil((view.x + view.w - MAP_PADDING) / HEX_X) + 1);
        const y1 = Math.max(0, Math.floor(view.y - MAP_PADDING) - 1);
        const y2 = Math.min(this.overview.rows * 40 - 1, Math.ceil(view.y + view.h - MAP_PADDING) + 1);
        if (level >= 1) {
            const present = new Set(sectors.map(sector => `${sector.col},${sector.row}`));
            for (let column = x1; column <= x2; column++) {
                for (let row = y1; row <= y2; row++) {
                    if (present.has(`${Math.floor(column / 32)},${Math.floor(row / 40)}`))
                        this.hexPath(this.grid, hexCenter(column, row));
                }
            }
            this.grid.stroke({ color: 0x344963, width: .65 / zoom, alpha: .55 });
        }
        for (const sector of sectors) {
            const { x, y, w } = sector.rect;
            this.boundaries.rect(x, y, w, SECTOR_HEIGHT);
        }
        this.boundaries.stroke({ color: 0x6881a8, width: 1 / zoom, alpha: .55 });
        {
            for (const sector of sectors) {
                const { x, y, w } = sector.rect;
                for (let i = 1; i < 4; i++) {
                    this.boundaries.moveTo(x + i * w / 4, y).lineTo(x + i * w / 4, y + SECTOR_HEIGHT);
                    this.boundaries.moveTo(x, y + i * 10).lineTo(x + w, y + i * 10);
                }
            }
            this.boundaries.stroke({ color: 0x57718e, width: .7 / zoom, alpha: .4 });
        }
    }

    hexPath(graphics, point) {
        const rx = 1 / Math.sqrt(3), x = point.x, y = point.y;
        graphics.poly([x - rx, y, x - rx / 2, y - .5, x + rx / 2, y - .5,
            x + rx, y, x + rx / 2, y + .5, x - rx / 2, y + .5], true);
    }

    drawRegionLabels(sectors, view, level, zoom) {
        for (const sector of sectors) {
            const x = sector.rect.x + SECTOR_WIDTH / 2, y = sector.rect.y + .3;
            const size = mapAppearance(zoom, level).sectorSize;
            this.placeLabel(sector.name, x, y, size, 0xa9b8cf, zoom, view);
            if (level >= 2) for (const subsector of sector.subsectors) {
                const index = subsector.letter.charCodeAt(0) - 65;
                if (index < 0 || index >= 16) continue;
                this.placeLabel(subsector.name || subsector.letter,
                    sector.rect.x + (index % 4 + .5) * SECTOR_WIDTH / 4,
                    sector.rect.y + Math.floor(index / 4) * 10 + .3,
                    mapAppearance(zoom, level).subsectorSize, 0x8fa6c4, zoom, view, true);
            }
        }
    }

    drawWorld(world, level, zoom) {
        const sizeCode = world.uwp?.[1], hydrographics = world.uwp?.[3];
        const wet = hydrographics && !["0", "?"].includes(hydrographics);
        const kind = level >= 2 && sizeCode === "0" ? "belt" : "world";
        const colour = level === 0 ? 0xa1c0da : sizeCode === "0" ? 0xd4bd96 : wet ? 0x87cde6 : 0xdcbf94;
        const { worldDiameter: size, zoneDiameter, gasDiameter, baseSize } = mapAppearance(zoom, level);
        if (level >= 2 && ["A", "R"].includes(world.zone?.trim()))
            this.marker("zone", world.x, world.y, zoneDiameter,
                world.zone.trim() === "R" ? 0xff727f : 0xf1c36c, zoom, .35);
        this.marker(kind, world.x, world.y, size, colour, zoom);
        // Halfway along a ray from the circle surface to the flat-topped hex edge.
        const worldRadius = size / zoom / 2;
        // Match the upper-left base inset on the upper-right side of the hex.
        const gasOffset = (worldRadius + .5) / 2 * HEX_X;
        const baseOffset = (worldRadius + .5) / 2;
        if (level >= 2 && world.pbg?.[2] && !["0", "?"].includes(world.pbg[2]))
            this.marker("gas", world.x + gasOffset, world.y - baseOffset / 2, gasDiameter * .8, 0xabc4e4, zoom);
        if (level >= 2) {
            const slots = { top: 0, bottom: 0 };
            for (const symbol of baseSymbols(world.bases)) {
                const index = slots[symbol.slot]++;
                this.marker(symbol.kind, world.x - baseOffset * HEX_X + (index % 2) * .13,
                    world.y + (symbol.slot === "top" ? -1 : 1) * (baseOffset / 2 + Math.floor(index / 2) * .12),
                    baseSize, symbol.slot === "top" ? 0xbccfff : 0x94dfcf, zoom);
            }
        }
    }

    drawWorldLabels(world, view, level, zoom) {
        const rules = worldLabelRules(world, zoom);
        const { nameSize, uwpSize, portSize, hexSize, worldDiameter } = mapAppearance(zoom, level);
        const place = (text, offset, size, colour) => {
            if (!text) return;
            this.placeLabel(text, world.x, world.y + offset / zoom, size, colour, zoom, view);
        };
        const radius = worldDiameter * .44;
        const nameY = level >= 3 ? zoom / 2 - nameSize - 4 : radius + 5;
        if (rules.name && world.name) place(worldName(world), nameY, nameSize,
            isSubsectorCapital(world) ? 0xff727f : world.priority >= 100 ? 0xf3d89b : 0xe3ecf7);
        if (rules.port && world.uwp?.[0] !== "?") place(world.uwp?.[0], -radius - 15, portSize, 0xb7c6db);
        if (rules.hex) place(world.hex, -zoom / 2, hexSize, 0x657e9e);
        if (rules.uwp) place(world.uwp || "Unknown UWP", -zoom / 2 + (rules.hex ? 14 : 0), uwpSize, 0xb3c7e2);
    }

    placeLabel(text, x, y, size, colour, zoom, view) {
        const width = text.length * size * .58;
        const box = { x: (x - view.x) * zoom - width / 2, y: (y - view.y) * zoom, w: width, h: size + 3 };
        if (box.x + box.w < 0 || box.x > this.host.clientWidth || box.y + box.h < 0 || box.y > this.host.clientHeight) return;
        this.label(text, x, y, size, colour, zoom);
    }

    label(text, x, y, size, colour, zoom) {
        let label = this.labels[this.labelIndex];
        if (!label) {
            label = new PIXI.BitmapText({ text: "", style: { fontFamily: "Arial", fontSize: 24, fill: 0xffffff } });
            label.anchor.set(.5, 0);
            this.labelLayer.addChild(label);
            this.labels.push(label);
        }
        this.labelIndex++;
        label.text = text;
        label.tint = colour;
        label.scale.set(size / 24 / zoom);
        label.position.set(x, y);
        label.visible = true;
    }

    marker(kind, x, y, size, tint, zoom, alpha = 1) {
        let sprite = this.markers[this.markerIndex];
        if (!sprite) {
            sprite = new PIXI.Sprite(); sprite.anchor.set(.5);
            this.markerLayer.addChild(sprite); this.markers.push(sprite);
        }
        this.markerIndex++;
        sprite.texture = this.texture(kind, kind === "zone" ? size : 0);
        sprite.position.set(x, y);
        sprite.width = sprite.height = size / zoom;
        sprite.tint = tint;
        sprite.alpha = alpha;
        sprite.visible = true;
    }

    texture(kind, screenSize = 0) {
        // Textures use 32 drawing units. Round up to keep the displayed stroke >= 2 CSS px.
        const zoneStroke = screenSize > 0 ? Math.max(1.2, Math.ceil(2 * 32 / screenSize * 4) / 4) : 1.2;
        const textureKey = kind === "zone" ? kind + ":" + zoneStroke : kind;
        if (this.textures.has(textureKey)) return this.textures.get(textureKey);
        const canvas = document.createElement("canvas");
        canvas.width = canvas.height = 128;
        const ctx = canvas.getContext("2d");
        ctx.scale(4, 4);
        ctx.fillStyle = ctx.strokeStyle = "white"; ctx.lineWidth = 2.5;
        if (kind === "zone") {
            // Rasterise at a generous radius so Pixi never tessellates a tiny circle into a diamond.
            ctx.lineWidth = zoneStroke;
            ctx.beginPath(); ctx.arc(16, 16, Math.min(14, 16 - zoneStroke / 2 - .25), 0, Math.PI * 2); ctx.stroke();
        } else if (kind.startsWith("base-")) {
            const star = points => {
                ctx.beginPath();
                for (let i = 0; i < points * 2; i++) {
                    const angle = -Math.PI / 2 + i * Math.PI / points;
                    const radius = i % 2 ? 5 : 13;
                    const x = 16 + Math.cos(angle) * radius, y = 16 + Math.sin(angle) * radius;
                    if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
                }
                ctx.closePath();
            };
            switch (kind) {
                case "base-naval": case "base-depot": case "base-tlaukhu":
                    star(kind === "base-tlaukhu" ? 8 : 5);
                    if (kind === "base-depot") ctx.stroke(); else ctx.fill();
                    break;
                case "base-scout": case "base-waystation":
                    ctx.beginPath(); ctx.moveTo(16, 3); ctx.lineTo(29, 27); ctx.lineTo(3, 27); ctx.closePath();
                    if (kind === "base-scout") ctx.fill(); else ctx.stroke();
                    break;
                case "base-military":
                    ctx.strokeRect(5, 5, 22, 22);
                    ctx.beginPath(); ctx.moveTo(5, 5); ctx.lineTo(27, 27); ctx.moveTo(27, 5); ctx.lineTo(5, 27); ctx.stroke();
                    break;
                case "base-corsair":
                    ctx.beginPath(); ctx.moveTo(5, 5); ctx.lineTo(27, 27); ctx.moveTo(27, 5); ctx.lineTo(5, 27); ctx.stroke();
                    break;
                case "base-clan":
                    ctx.beginPath(); ctx.moveTo(4, 25); ctx.lineTo(16, 5); ctx.lineTo(28, 25);
                    ctx.moveTo(9, 26); ctx.lineTo(16, 14); ctx.lineTo(23, 26); ctx.stroke();
                    break;
                default:
                    ctx.beginPath(); ctx.moveTo(16, 3); ctx.lineTo(29, 16); ctx.lineTo(16, 29); ctx.lineTo(3, 16); ctx.closePath(); ctx.stroke();
                    if (kind === "base-embassy") { ctx.beginPath(); ctx.arc(16, 16, 3, 0, Math.PI * 2); ctx.fill(); }
            }
        } else if (kind === "belt") {
            for (const [x, y, r] of [[7, 13, 3], [15, 7, 2], [22, 12, 3], [11, 23, 2], [23, 23, 2.5]]) {
                ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill();
            }
        } else {
            ctx.beginPath(); ctx.arc(16, 16, kind === "gas" ? 6 : 11, 0, Math.PI * 2); ctx.fill();
            if (kind === "gas") {
                ctx.beginPath(); ctx.ellipse(16, 16, 14, 5, -.4, 0, Math.PI * 2); ctx.stroke();
            }
        }
        const texture = PIXI.Texture.from(canvas);
        this.textures.set(textureKey, texture);
        return texture;
    }

    drawSelection(zoom) {
        this.selection.clear();
        if (!this.selected) return;
        this.hexPath(this.selection, this.selected);
        this.selection.fill({ color: 0x83e3d1, alpha: .10 }).stroke({ color: 0x83e3d1, width: 1.8 / zoom });
    }

    focusWorld(sectorId, hex) {
        if (this.disposed || !this.viewport) return false;
        const sector = this.sectors.find(sector => sector.id === sectorId);
        const location = parseHex(hex);
        if (!sector || !location) return false;
        const point = hexCenter(sector.col * 32 + location.column - 1,
            sector.row * 40 + location.row - 1);
        this.selected = { ...point, key: sectorId + "/" + hex };
        return this.panToPoint(point);
    }

    focusRegion(sectorId, letter = null) {
        if (this.disposed || !this.viewport) return false;
        const sector = this.sectors.find(sector => sector.id === sectorId);
        if (!sector || (letter !== null && !/^[A-P]$/.test(letter))) return false;
        let x = MAP_PADDING + sector.col * SECTOR_WIDTH;
        let y = MAP_PADDING + sector.row * SECTOR_HEIGHT;
        if (letter === null) {
            x += SECTOR_WIDTH / 2;
            y += SECTOR_HEIGHT / 2;
        } else {
            const index = letter.charCodeAt(0) - 65;
            x += (index % 4 + .5) * SECTOR_WIDTH / 4;
            y += (Math.floor(index / 4) + .5) * SECTOR_HEIGHT / 4;
        }
        this.selected = null;
        return this.panToPoint({ x, y });
    }

    panToPoint(point) {
        // Replace any previous flight and stop drag momentum before search navigation.
        this.viewport.plugins.remove("animate");
        this.viewport.plugins.get("decelerate")?.reset();
        // Permit edge worlds to sit at the centre even when the atlas fits the viewport.
        this.allowEdgeCentering = true;
        this.updatePanBounds();
        if (globalThis.matchMedia?.("(prefers-reduced-motion: reduce)").matches) {
            this.viewport.moveCenter(point.x, point.y);
            this.saveView();
        } else {
            this.viewport.animate({ time: 650, position: point, ease: "easeInOutSine",
                callbackOnComplete: () => {
                    if (this.disposed) return;
                    this.lastView = "";
                    this.saveView();
                } });
        }
        this.lastView = "";
        this.update();
        return true;
    }

    async selectWorld(event) {
        const bounds = this.app.canvas.getBoundingClientRect();
        let point = this.viewport.toWorld((event.clientX - bounds.left) * this.host.clientWidth / bounds.width,
            (event.clientY - bounds.top) * this.host.clientHeight / bounds.height);
        let nearest, distance = Math.max(4, this.viewport.scale.x * .13);
        for (const world of this.visibleWorlds ?? []) {
            const d = Math.hypot(point.x - world.x, point.y - world.y) * this.viewport.scale.x;
            if (d < distance) { nearest = world; distance = d; }
        }
        if (nearest) point = nearest;
        const hex = findHex(point.x, point.y);
        const column = hex.sectorColumn * 32 + hex.column - 1, row = hex.sectorRow * 40 + hex.row - 1;
        this.selected = { ...hexCenter(column, row), key: nearest?.key };
        this.lastView = "";
        try {
            await this.callback.invokeMethodAsync("SelectHex", this.overview.minSectorX + hex.sectorColumn,
                this.overview.minSectorY + hex.sectorRow, hex.column, hex.row);
        } catch (error) { if (!this.disposed) console.warn("World selection failed", error); }
    }

    saveView() {
        if (!this.viewport) return;
        try {
            const { x, y } = this.viewport.center;
            localStorage.setItem(VIEW_STORAGE_KEY, JSON.stringify({ x, y, zoom: this.viewport.scale.x }));
        } catch { /* Map navigation remains usable without browser storage. */ }
    }

    restoreView() {
        try {
            const saved = JSON.parse(localStorage.getItem(VIEW_STORAGE_KEY) ?? "null");
            if (!saved || ![saved.x, saved.y, saved.zoom].every(Number.isFinite)) return false;
            this.viewport.setZoom(Math.min(MAX_ZOOM, Math.max(this.fitZoom, saved.zoom)), true);
            this.viewport.moveCenter(Math.max(0, Math.min(this.worldW, saved.x)), Math.max(0, Math.min(this.worldH, saved.y)));
            return true;
        } catch { return false; }
    }

    dispose() {
        if (this.disposed) return;
        this.disposed = true;
        this.saveView();
        this.abort.abort();
        this.removeClickObserver?.();
        this.resizeObserver?.disconnect();
        for (const entry of this.sectorCache.values()) entry.controller.abort();
        this.sectorCache.clear();
        if (this.app?.renderer) this.app.destroy(true, { children: true });
        for (const texture of this.textures.values()) texture.destroy(true);
        this.textures.clear(); this.markers.length = this.labels.length = 0;
    }
}
