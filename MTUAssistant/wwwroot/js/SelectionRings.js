// Texture-based selection rings for the Battlemap: segmented arcs for the active combatant
// and a HUD bezel for targets. Geometry is plain data so it can be checked in Node; the
// PixiJS drawing below is a thin layer over it.

export const RING_TEXTURE_SIZE = 512;
export const RING_TEXTURE_HALF = RING_TEXTURE_SIZE / 2;
// Visible outer diameter of a ring relative to the token's displayed size.
export const RING_DIAMETER_RATIO = 1.6;
// Degrees per second; positive is clockwise on screen.
export const RING_SPEEDS = Object.freeze({ outer: 20, inner: -35 });
export const RING_TINTS = Object.freeze({ active: 0xffffff, target: 0xffa500 });

export function segmentedArcsSpec() {
    const outer = [0, 1, 2, 3].map(i => ({ radius: .786, start: i * 90, end: i * 90 + 73, width: 11 }));
    const inner = [];
    for (let i = 0; i < 8; i++) {
        const base = i * 45;
        inner.push({ radius: .614, start: base, end: base + 19, width: 5.5 });
        inner.push({ radius: .614, start: base + 30, end: base + 34, width: 5.5 });
    }
    return { outer: { arcs: outer, ticks: [] }, inner: { arcs: inner, ticks: [] } };
}

export function hudBezelSpec() {
    const ticks = Array.from({ length: 48 }, (_, i) => {
        const major = i % 4 === 0;
        return { angle: i * 7.5, inner: .80 - (major ? .13 : .06), outer: .80, width: major ? 11 : 7, major };
    });
    const arcs = [0, 1, 2, 3].map(i => ({ radius: .571, start: 45 + i * 90, end: 45 + i * 90 + 74, width: 9 }));
    return { outer: { arcs: [], ticks }, inner: { arcs, ticks: [] } };
}

export const RING_DESIGNS = Object.freeze({ segmentedArcs: segmentedArcsSpec, hudBezel: hudBezelSpec });

// Furthest texture pixel from the centre that a ring paints (butt caps).
export function ringExtent(ring) {
    const arcs = ring.arcs.map(arc => arc.radius * RING_TEXTURE_HALF + arc.width / 2);
    const ticks = ring.ticks.map(tick => Math.hypot(tick.outer * RING_TEXTURE_HALF, tick.width / 2));
    return Math.max(0, ...arcs, ...ticks);
}

export const RING_OUTER_EXTENTS = Object.freeze(Object.fromEntries(
    Object.entries(RING_DESIGNS).map(([name, spec]) => [name, ringExtent(spec().outer)])));

// Sprite scale for a screen-space ring whose outer ring spans RING_DIAMETER_RATIO token widths.
export function ringSpriteScale(tokenDisplaySize, viewScale, outerExtent) {
    if (!(tokenDisplaySize > 0) || !(viewScale > 0) || !(outerExtent > 0)) return 0;
    return RING_DIAMETER_RATIO * tokenDisplaySize * viewScale / (2 * outerExtent);
}

// Rotation in radians after elapsedMs at degreesPerSecond; static when motion is reduced.
export function ringRotation(elapsedMs, degreesPerSecond, reducedMotion = false) {
    if (reducedMotion || !Number.isFinite(elapsedMs)) return 0;
    const degrees = (elapsedMs / 1000 * degreesPerSecond) % 360;
    return degrees * Math.PI / 180;
}

// Which tokens get rings. The active combatant gets segmented arcs unless dead; each target
// trail (including a snapped drag preview) gets a HUD bezel.
export function selectionRingPlacements({ combatMode, active, deadIds, trails }) {
    if (!combatMode) return [];
    const placements = [];
    if (active && !deadIds?.has(active.id))
        placements.push({ key: 'active', design: 'segmentedArcs', tint: RING_TINTS.active, column: active.column, row: active.row });
    for (const trail of trails ?? []) {
        if (!trail.isTarget || (trail.preview && !trail.snapped)) continue;
        placements.push({ key: trail.id, design: 'hudBezel', tint: RING_TINTS.target, column: trail.toColumn, row: trail.toRow });
    }
    return placements;
}

function drawRing(graphics, ring) {
    const c = RING_TEXTURE_HALF, rad = Math.PI / 180;
    for (const arc of ring.arcs)
        graphics.beginPath().arc(c, c, arc.radius * c, arc.start * rad, arc.end * rad)
            .stroke({ color: 0xffffff, width: arc.width, cap: 'butt' });
    for (const tick of ring.ticks) {
        const cos = Math.cos(tick.angle * rad), sin = Math.sin(tick.angle * rad);
        graphics.beginPath().moveTo(c + cos * tick.inner * c, c + sin * tick.inner * c)
            .lineTo(c + cos * tick.outer * c, c + sin * tick.outer * c)
            .stroke({ color: 0xffffff, width: tick.width, cap: 'butt' });
    }
}

// Generates each design's outer and inner textures once for a renderer.
export function createRingTextures(PIXI, renderer) {
    const textures = {};
    for (const [name, spec] of Object.entries(RING_DESIGNS)) {
        const design = spec();
        textures[name] = {};
        for (const part of ['outer', 'inner']) {
            const graphics = new PIXI.Graphics();
            drawRing(graphics, design[part]);
            textures[name][part] = renderer.generateTexture({
                target: graphics,
                frame: new PIXI.Rectangle(0, 0, RING_TEXTURE_SIZE, RING_TEXTURE_SIZE),
                resolution: 1,
                antialias: true,
                // Mipmaps keep the fine strokes from aliasing when rings are drawn small.
                textureSourceOptions: { autoGenerateMipmaps: true, mipLevelCount: Math.log2(RING_TEXTURE_SIZE) + 1, scaleMode: 'linear' },
            });
            graphics.destroy();
        }
    }
    return textures;
}

export function destroyRingTextures(textures) {
    for (const design of Object.values(textures ?? {})) {
        design.outer?.destroy(true);
        design.inner?.destroy(true);
    }
}
