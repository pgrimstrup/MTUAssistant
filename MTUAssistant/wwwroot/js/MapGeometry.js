export const HEX_X = Math.cos(Math.PI / 6);
export const SECTOR_WIDTH = 32 * HEX_X;
export const SECTOR_HEIGHT = 40;
export const MAP_PADDING = 1;
// Fixed detail bands in CSS pixels per parsec.
export const DETAIL_LEVELS = [
    { name: "Overview", minimumZoom: 0 },
    { name: "Sector", minimumZoom: 10 },
    { name: "Subsector", minimumZoom: 25 },
    { name: "Close", minimumZoom: 55 },
    { name: "Full", minimumZoom: 95 },
];

export function worldLabelRules(world, zoom) {
    const codes = new Set((world.remarks ?? "").split(/\s+/));
    const capital = codes.has("Cp") || codes.has("Cs") || codes.has("Cx");
    const highPopulation = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".indexOf(world.uwp?.[4] ?? "?") >= 8;
    return {
        name: zoom < 10 ? capital : zoom < 25 ? codes.has("Cp")
            : zoom < 55 ? capital || highPopulation : true,
        port: zoom >= 25 && zoom < 55,
        uwp: zoom >= 55,
        hex: zoom >= 95,
    };
}

export function detailLevel(zoom) {
    return DETAIL_LEVELS.findLastIndex(level => zoom >= level.minimumZoom);
}

export function mapAppearance(zoom, level = detailLevel(zoom)) {
    const worldDiameter = level === 0 ? Math.min(1.8, Math.max(.55, zoom * .55)) : Math.max(4, zoom * (level >= 3 ? .32 : .24));
    const fontScale = zoom >= 95 ? 1 : .8;
    return { worldDiameter, zoneDiameter: worldDiameter * 1.8,
        gasDiameter: Math.max(9, zoom * .28), baseSize: Math.max(9, zoom * .14),
        nameSize: (level >= 3 ? Math.min(19, 15 + zoom * .02) : level >= 2 ? 14 : 12) * fontScale,
        uwpSize: Math.min(16, 13 + zoom * .015) * fontScale,
        portSize: 12 * fontScale, hexSize: 9 * fontScale,
        sectorSize: (level === 0 ? 12 : 20) * fontScale,
        subsectorSize: 16 * fontScale };
}

// T5 base codes. Naval/military symbols occupy the upper left; scout/civilian symbols the lower left.
// N/K = naval, S/V = scout/exploration, D = depot, W = way station.
export function baseSymbols(codes) {
    const types = { N: ["naval", "top"], K: ["naval", "top"], D: ["depot", "top"],
        S: ["scout", "bottom"], V: ["scout", "bottom"], W: ["waystation", "bottom"],
        M: ["military", "top"], C: ["corsair", "top"], E: ["embassy", "bottom"],
        R: ["clan", "top"], T: ["tlaukhu", "top"] };
    const seen = new Set();
    const result = [];
    for (const code of (codes ?? "").toUpperCase()) {
        if (!/[A-Z]/.test(code)) continue;
        const [kind, slot] = types[code] ?? ["otherbase", "bottom"];
        if (!seen.has(kind)) { seen.add(kind); result.push({ kind: `base-${kind}`, slot }); }
    }
    return result;
}

export function parseHex(hex) {
    if (!/^\d{4}$/.test(hex)) return null;
    const column = Number(hex.slice(0, 2)), row = Number(hex.slice(2));
    return column >= 1 && column <= 32 && row >= 1 && row <= 40 ? { column, row } : null;
}

// Global zero-based columns/rows. Flat-topped hexes; alternating columns are staggered.
export function hexCenter(column, row) {
    return { x: MAP_PADDING + (column + .5) * HEX_X,
        y: MAP_PADDING + row + (column % 2 === 0 ? .5 : 1) };
}

export function findHex(x, y) {
    x -= MAP_PADDING;
    y -= MAP_PADDING;
    const approximateColumn = Math.floor(x / HEX_X);
    let best, distance = Infinity;
    for (let column = approximateColumn - 1; column <= approximateColumn + 1; column++) {
        const offset = column % 2 === 0 ? .5 : 1;
        const row = Math.round(y - offset);
        const d = Math.hypot(x - (column + .5) * HEX_X, y - row - offset);
        if (d < distance) {
            distance = d;
            best = { sectorColumn: Math.floor(column / 32), sectorRow: Math.floor(row / 40),
                column: ((column % 32) + 32) % 32 + 1, row: ((row % 40) + 40) % 40 + 1 };
        }
    }
    return best;
}

export function intersects(a, b) {
    return a.x < b.x + b.w && a.x + a.w > b.x && a.y < b.y + b.h && a.y + a.h > b.y;
}

// Screen-space buckets keep label placement near linear even in crowded sectors.
export class LabelLayout {
    constructor(cellSize = 64) { this.cellSize = cellSize; this.cells = new Map(); }
    *keys(box) {
        for (let x = Math.floor(box.x / this.cellSize); x <= Math.floor((box.x + box.w) / this.cellSize); x++)
            for (let y = Math.floor(box.y / this.cellSize); y <= Math.floor((box.y + box.h) / this.cellSize); y++)
                yield `${x},${y}`;
    }
    reserve(box) {
        for (const key of this.keys(box)) {
            const cell = this.cells.get(key) ?? [];
            cell.push(box);
            this.cells.set(key, cell);
        }
    }
    canPlace(box, maxOverlapFraction = 0) {
        for (const key of this.keys(box))
            if (this.cells.get(key)?.some(other => {
                if (!intersects(box, other)) return false;
                const area = (Math.min(box.x + box.w, other.x + other.w) - Math.max(box.x, other.x))
                    * (Math.min(box.y + box.h, other.y + other.h) - Math.max(box.y, other.y));
                return area > Math.min(box.w * box.h, other.w * other.h) * maxOverlapFraction;
            })) return false;
        return true;
    }
    place(box, maxOverlapFraction = 0) {
        if (!this.canPlace(box, maxOverlapFraction)) return false;
        this.reserve(box);
        return true;
    }
}

export function worldPriority(world) {
    const codes = new Set((world.remarks ?? "").split(/\s+/));
    const population = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".indexOf(world.uwp?.[4] ?? "?");
    return (codes.has("Cx") ? 300 : codes.has("Cs") ? 200 : codes.has("Cp") ? 100 : 0) + Math.max(0, population);
}

export function isSubsectorCapital(world) {
    return (world.remarks ?? "").split(/\s+/).includes("Cp");
}

export function worldName(world) {
    const population = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ".indexOf(world.uwp?.[4] ?? "?");
    return population >= 8 ? world.name.toUpperCase() : world.name;
}
