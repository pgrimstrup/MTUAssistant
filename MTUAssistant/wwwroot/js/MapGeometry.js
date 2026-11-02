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
        name: zoom < 10 ? false : zoom < 25 ? codes.has("Cp")
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

// MSEC perimeter traversal follows TravellerMap PathUtil.ComputeBorderPath
// (https://github.com/inexorabletash/travellermap/blob/main/server/RenderUtil.cs).
// Perimeter hexes are traversed clockwise; edges, not centre-to-centre
// lines, form the border. Buffer hexes outside the sector guide the traversal.
export function borderSegments(path, sectorColumn, sectorRow) {
    const hexes = (path ?? '').trim().split(/\s+/).filter(h => /^\d{4}$/.test(h))
        .map(h => ({x:Number(h.slice(0,2)),y:Number(h.slice(2))}));
    if (!hexes.length) return [];
    const present = new Set(hexes.map(h => h.x+','+h.y));
    const edgeX = [-1/Math.sqrt(3)/2,-1/Math.sqrt(3),-1/Math.sqrt(3)/2,1/Math.sqrt(3)/2,1/Math.sqrt(3),1/Math.sqrt(3)/2];
    const edgeY = [.5,0,-.5,-.5,0,.5];
    const vertex = (h,i) => { const c=hexCenter(sectorColumn*32+h.x-1,sectorRow*40+h.y-1); return {x:c.x+edgeX[i],y:c.y+edgeY[i]}; };
    const neighbor = (h,i) => { let x=h.x,y=h.y, odd=((x%2)+2)%2;
        switch(i) {case 0:x--;y+=1-odd;break;case 1:x--;y-=odd;break;case 2:y--;break;case 3:x++;y-=odd;break;case 4:x++;y+=1-odd;break;case 5:y++;}
        return x+','+y; };
    let first=0,start=hexes[0],previous=vertex(start,0),visited=false; const segments=[];
    for (const h of hexes) {
        let last=first+5;
        if (visited && h.x===start.x && h.y===start.y) { last=5; if(first<3) break; }
        visited=true;
        let i=first;
        for(let check=first;check<=last;check++) {
            i=check;
            if(present.has(neighbor(h,i%6))) break;
            const next=vertex(h,(i+1)%6);
            if(h.x>=1 && h.x<=32 && h.y>=1 && h.y<=40) segments.push([previous,next]);
            previous=next;
        }
        first=(i%6+4)%6;
    }
    return segments;
}
export function routePoints(route, sectorColumn, sectorRow) {
    const point=(hex,dx,dy)=>{
        if(!/^\d{4}$/.test(hex ?? '')) return null;
        return hexCenter((sectorColumn+dx)*32+Number(hex.slice(0,2))-1,
            (sectorRow+dy)*40+Number(hex.slice(2))-1);
    };
    const start=point(route.start,route.startOffsetX ?? 0,route.startOffsetY ?? 0);
    const end=point(route.end,route.endOffsetX ?? 0,route.endOffsetY ?? 0);
    return start && end ? [start,end] : null;
}

// Clockwise perimeter segments have their interior on the right in screen coordinates.
export function insetBorderSegment(a, b, zoom) {
    const dx=b.x-a.x, dy=b.y-a.y, length=Math.hypot(dx,dy);
    if (!length || zoom <= 0) return [a,b];
    const inset=Math.min(2 / zoom, .2);
    const nx=-dy/length*inset, ny=dx/length*inset;
    // Extend joins so inset edges meet cleanly at the 120-degree hex corners.
    const extension=Math.min(inset/Math.sqrt(3), length/4);
    const tx=dx/length*extension, ty=dy/length*extension;
    return [{x:a.x+nx-tx,y:a.y+ny-ty},{x:b.x+nx+tx,y:b.y+ny+ty}];
}
