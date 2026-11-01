// Tiled map of charted space. World units are parsecs, so viewport zoom == screen px per parsec,
// which maps directly onto the tile scales (a scale-N image has N px per parsec).

const SCRIPTS = [
    "lib/pixi/pixi.min.js",
    "lib/pixi-viewport/pixi-viewport.umd.js",
];
const SCALES = [8, 16, 32, 64, 128];
const MAX_ZOOM = 192;
const CONCURRENCY = 6;
const MAX_DETAIL_TILES = 140;
const FADE_MS = 200;
const BACKGROUND = 0x05070d;
const VIEW_STORAGE_KEY = "mtu.map.view";

let scriptsPromise;

function loadScript(src) {
    return new Promise((resolve, reject) => {
        const el = document.createElement("script");
        el.src = src;
        el.onload = resolve;
        el.onerror = () => reject(new Error(`Failed to load ${src}`));
        document.head.appendChild(el);
    });
}

function loadLibraries() {
    scriptsPromise ??= (async () => {
        if (!globalThis.PIXI) await loadScript(SCRIPTS[0]);
        globalThis.pixi_js = globalThis.PIXI; // the viewport UMD build expects this global name
        if (!globalThis.pixi_viewport) await loadScript(SCRIPTS[1]);
    })();
    return scriptsPromise;
}

export async function createMap(host, statusEl) {
    await loadLibraries();
    const map = new TileMap(host, statusEl);
    await map.start();
    return map;
}

class TileMap {
    constructor(host, statusEl) {
        this.host = host;
        this.statusEl = statusEl;
        this.tiles = new Map();   // key -> { sprite, state, abort, scale, cx, cy }
        this.queue = [];
        this.active = 0;
        this.disposed = false;
        this.lastView = "";
        this.lastUpdate = 0;
        this.fades = new Set();
    }

    async start() {
        const { Application, Container } = PIXI;
        const { Viewport } = pixi_viewport;

        const response = await fetch("api/map/manifest");
        if (!response.ok) throw new Error(`Manifest request failed: ${response.status}`);
        this.manifest = await response.json();
        const m = this.manifest;
        this.sw = m.sectorWidth;
        this.sh = m.sectorHeight;
        this.worldW = m.columns * this.sw;
        this.worldH = m.rows * this.sh;
        this.baseUrl = m.baseUrl;

        this.app = new Application();
        await this.app.init({
            resizeTo: this.host,
            background: BACKGROUND,
            antialias: false,
            autoDensity: true,
            resolution: window.devicePixelRatio || 1,
        });
        if (this.disposed) { this.app.destroy(true, { children: true, texture: true }); return; }
        this.host.appendChild(this.app.canvas);

        this.viewport = new Viewport({
            screenWidth: this.host.clientWidth,
            screenHeight: this.host.clientHeight,
            worldWidth: this.worldW,
            worldHeight: this.worldH,
            events: this.app.renderer.events,
        });
        this.app.stage.addChild(this.viewport);

        this.detailLayer = new Container();
        this.viewport.addChild(this.detailLayer);

        this.viewport.drag().pinch().wheel({ smooth: 6 }).decelerate({ friction: 0.92 });

        this.resizeObserver = new ResizeObserver(() => this.onResize());
        this.resizeObserver.observe(this.host);
        this.onResize(true);
        if (!this.restoreView()) this.fit();

        this.app.ticker.add(() => this.onTick());
    }

    get fitZoom() {
        return Math.min(this.host.clientWidth / this.worldW, this.host.clientHeight / this.worldH);
    }

    onResize(initial = false) {
        if (!this.viewport) return;
        const w = this.host.clientWidth, h = this.host.clientHeight;
        if (w === 0 || h === 0) return;
        this.viewport.resize(w, h, this.worldW, this.worldH);
        this.viewport.clampZoom({ minScale: this.fitZoom, maxScale: MAX_ZOOM });
        this.viewport.clamp({ direction: "all", underflow: "center" });
        if (!initial) this.lastView = "";
    }

    fit() {
        this.viewport.setZoom(this.fitZoom, true);
        this.viewport.moveCenter(this.worldW / 2, this.worldH / 2);
        this.lastView = "";
    }

    zoomBy(factor) {
        const target = Math.min(MAX_ZOOM, Math.max(this.fitZoom, this.viewport.scale.x * factor));
        this.viewport.animate({ time: 250, scale: target, ease: "easeInOutSine" });
    }

    zoomIn() { this.zoomBy(2); }
    zoomOut() { this.zoomBy(0.5); }

    // ---- tile geometry -------------------------------------------------------------------

    sectorRect(s) {
        return { x: s.col * this.sw, y: s.row * this.sh, w: this.sw, h: this.sh };
    }

    subRect(s, i) {
        const w = this.sw / 4, h = this.sh / 4;
        return { x: s.col * this.sw + (i % 4) * w, y: s.row * this.sh + Math.floor(i / 4) * h, w, h };
    }

    tileUrl(scale, file, letter) {
        return `${this.baseUrl}/${scale}/${file}${letter ? "_" + letter : ""}.png`;
    }

    // ---- loading -------------------------------------------------------------------------

    async fetchTexture(url, signal) {
        const res = await fetch(url, { signal });
        if (!res.ok) throw new Error(`${res.status} ${url}`);
        const bitmap = await createImageBitmap(await res.blob());
        const source = new PIXI.ImageSource({
            resource: bitmap,
            autoGenerateMipmaps: true,
            scaleMode: "linear",
        });
        return new PIXI.Texture({ source });
    }

    chooseScale() {
        const needed = this.viewport.scale.x * (window.devicePixelRatio || 1) * 0.9;
        return SCALES.find(s => s >= needed) ?? SCALES[SCALES.length - 1];
    }

    // Tiles of the given scale that intersect the rect.
    tilesFor(scale, rect) {
        const out = [];
        const useSub = scale >= 64;
        for (const s of this.manifest.sectors) {
            if (!(useSub ? s.subsectorScales : s.sectorScales).includes(scale)) continue;
            const sr = this.sectorRect(s);
            if (!intersects(sr, rect)) continue;
            if (!useSub) {
                out.push({ key: `${scale}/${s.file}`, rect: sr, url: this.tileUrl(scale, s.file), scale });
                continue;
            }
            for (let i = 0; i < 16; i++) {
                const r = this.subRect(s, i);
                if (!intersects(r, rect)) continue;
                const letter = String.fromCharCode(65 + i);
                out.push({ key: `${scale}/${s.file}_${letter}`, rect: r, url: this.tileUrl(scale, s.file, letter), scale });
            }
        }
        return out;
    }

    update() {
        const v = this.viewport;
        const scale = this.chooseScale();
        const view = { x: v.left, y: v.top, w: v.right - v.left, h: v.bottom - v.top };
        const cx = view.x + view.w / 2, cy = view.y + view.h / 2;

        const tileW = scale >= 64 ? this.sw / 4 : this.sw;
        const tileH = scale >= 64 ? this.sh / 4 : this.sh;
        const margin = { x: view.x - tileW / 2, y: view.y - tileH / 2, w: view.w + tileW, h: view.h + tileH };

        const visible = this.tilesFor(scale, view);
        const wanted = this.tilesFor(scale, margin);
        const wantedKeys = new Set(wanted.map(t => t.key));
        const visibleKeys = new Set(visible.map(t => t.key));

        // Cancel in-flight loads that are no longer wanted.
        for (const [key, t] of this.tiles) {
            if (t.state === "loading" && !wantedKeys.has(key)) {
                t.abort.abort();
                this.tiles.delete(key);
            }
        }

        // Queue what is missing, nearest the centre first.
        const dist = r => Math.hypot(r.x + r.w / 2 - cx, r.y + r.h / 2 - cy);
        this.queue = wanted
            .filter(t => !this.tiles.has(t.key))
            .sort((a, b) => dist(a.rect) - dist(b.rect));

        // Tiles of a new scale stay hidden until every visible one is ready, then replace the old scale
        // in a single frame. Overlapping scales must never be shown together because they are screen-blended.
        const visibleReady = [...visibleKeys].every(k => this.tiles.get(k)?.state === "ready");
        for (const [key, t] of this.tiles) {
            if (t.state !== "ready") continue;
            if (t.hidden && t.scale !== scale) { this.evict(key); continue; }
            if (t.hidden && t.scale === scale && visibleReady) {
                t.hidden = false;
                t.sprite.visible = true;
                t.sprite.alpha = 1;
            }
            const stale = t.scale !== scale && visibleReady;
            const offscreen = t.scale === scale && !wantedKeys.has(key);
            if (stale || offscreen) this.evict(key);
        }

        // Hard cap: drop the farthest tiles that are not currently visible.
        if (this.tiles.size > MAX_DETAIL_TILES) {
            const candidates = [...this.tiles.entries()]
                .filter(([k, t]) => t.state === "ready" && !visibleKeys.has(k))
                .sort((a, b) => Math.hypot(b[1].cx - cx, b[1].cy - cy) - Math.hypot(a[1].cx - cx, a[1].cy - cy));
            for (const [k] of candidates) {
                if (this.tiles.size <= MAX_DETAIL_TILES) break;
                this.evict(k);
            }
        }

        this.target = { scale, visible: visibleKeys.size };
        this.pump();
        this.updateStatus();
    }

    pump() {
        while (this.active < CONCURRENCY && this.queue.length > 0 && !this.disposed) {
            const t = this.queue.shift();
            if (this.tiles.has(t.key)) continue;
            const entry = {
                state: "loading",
                abort: new AbortController(),
                scale: t.scale,
                cx: t.rect.x + t.rect.w / 2,
                cy: t.rect.y + t.rect.h / 2,
            };
            this.tiles.set(t.key, entry);
            this.active++;
            this.loadTile(t, entry).finally(() => { this.active--; this.pump(); });
        }
    }

    async loadTile(t, entry) {
        try {
            const texture = await this.fetchTexture(t.url, entry.abort.signal);
            if (this.disposed || this.tiles.get(t.key) !== entry) { texture.destroy(true); return; }
            const sprite = new PIXI.Sprite(texture);
            sprite.position.set(t.rect.x, t.rect.y);
            sprite.scale.set(1 / t.scale);
            // Screen keeps the brighter pixel where hex-grid margins overlap, so black edges never cover detail.
            sprite.blendMode = "screen";
            this.detailLayer.addChild(sprite);
            entry.sprite = sprite;
            entry.state = "ready";

            const replacing = [...this.tiles.values()].some(o => o.state === "ready" && !o.hidden && o.scale !== t.scale);
            if (replacing) {
                entry.hidden = true;
                sprite.visible = false;
            } else {
                sprite.alpha = 0;
                entry.fadeStart = performance.now();
                this.fades.add(entry);
            }
            this.lastView = ""; // re-evaluate eviction now that this tile is ready
        } catch (e) {
            if (e.name !== "AbortError") {
                console.warn(e);
                if (this.tiles.get(t.key) === entry) entry.state = "failed";
            }
        }
    }

    evict(key) {
        const t = this.tiles.get(key);
        if (!t) return;
        this.tiles.delete(key);
        this.fades.delete(t);
        if (t.sprite) t.sprite.destroy({ children: true, texture: true, textureSource: true });
    }

    // ---- frame loop ----------------------------------------------------------------------

    onTick() {
        const now = performance.now();

        for (const t of this.fades) {
            const p = Math.min(1, (now - t.fadeStart) / FADE_MS);
            t.sprite.alpha = p;
            if (p >= 1) {
                this.fades.delete(t);
                this.lastView = ""; // let update() evict superseded tiles now the fade is complete
            }
        }

        const v = this.viewport;
        const view = `${v.center.x.toFixed(2)},${v.center.y.toFixed(2)},${v.scale.x.toFixed(4)},${this.host.clientWidth}`;
        if (view !== this.lastView && now - this.lastUpdate > 80) {
            this.lastView = view;
            this.lastUpdate = now;
            this.saveView();
            this.update();
        }
    }

    updateStatus() {
        if (!this.statusEl || !this.target) return;
        const ready = [...this.tiles.values()].filter(t => t.state === "ready").length;
        const loading = this.tiles.size - ready + this.queue.length;
        const zoom = this.viewport.scale.x;
        this.statusEl.textContent =
            `${zoom.toFixed(1)} px/pc · detail ${this.target.scale} · ${ready} tiles` + (loading > 0 ? ` · loading ${loading}` : "");
    }

    saveView() {
        if (!this.viewport) return;
        const { x, y } = this.viewport.center;
        try {
            localStorage.setItem(VIEW_STORAGE_KEY, JSON.stringify({ x, y, zoom: this.viewport.scale.x }));
        } catch { /* storage unavailable; the view just won't persist */ }
    }

    restoreView() {
        try {
            const saved = JSON.parse(localStorage.getItem(VIEW_STORAGE_KEY) ?? "null");
            if (!saved || ![saved.x, saved.y, saved.zoom].every(Number.isFinite)) return false;
            this.viewport.setZoom(Math.min(MAX_ZOOM, Math.max(this.fitZoom, saved.zoom)), true);
            this.viewport.moveCenter(saved.x, saved.y);
            this.lastView = "";
            return true;
        } catch {
            return false;
        }
    }

    dispose() {
        this.saveView();
        this.disposed = true;
        this.resizeObserver?.disconnect();
        for (const t of this.tiles.values()) t.abort?.abort();
        this.tiles.clear();
        this.fades.clear();
        this.queue = [];
        this.app?.destroy(true, { children: true, texture: true, textureSource: true });
        this.app = null;
    }
}

function intersects(a, b) {
    return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}
