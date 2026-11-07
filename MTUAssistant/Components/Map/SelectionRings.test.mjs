import assert from 'node:assert/strict';
import {
    segmentedArcsSpec, hudBezelSpec, ringExtent, ringSpriteScale, ringRotation, selectionRingPlacements, createRingTextures, destroyRingTextures,
    RING_TEXTURE_SIZE, RING_TEXTURE_HALF, RING_DIAMETER_RATIO, RING_OUTER_EXTENTS, RING_SPEEDS, RING_TINTS
} from '../../wwwroot/js/SelectionRings.js';

const close = (actual, expected, message) => assert.ok(Math.abs(actual - expected) < 1e-9, `${message}: ${actual} ≠ ${expected}`);
const length = arc => arc.end - arc.start;

// Segmented arcs: 4 × 73° outer arcs with 17° gaps from 0°.
const segmented = segmentedArcsSpec();
assert.equal(segmented.outer.arcs.length, 4);
segmented.outer.arcs.forEach((arc, i) => {
    assert.equal(arc.start, i * 90, 'outer arcs start at 0° and repeat every 90°');
    assert.equal(length(arc), 73);
    assert.equal(arc.radius, .786); assert.equal(arc.width, 11);
    const next = segmented.outer.arcs[(i + 1) % 4];
    assert.equal((next.start + 360 - arc.end) % 360, 17, 'outer gaps are 17°');
});
// Inner: 8 repeats of long 19°, gap 11°, short 4°, gap 11°.
assert.equal(segmented.inner.arcs.length, 16);
for (let i = 0; i < 8; i++) {
    const long = segmented.inner.arcs[i * 2], short = segmented.inner.arcs[i * 2 + 1], next = segmented.inner.arcs[(i * 2 + 2) % 16];
    assert.equal(long.start, i * 45, 'inner pattern repeats every 45°');
    assert.equal(length(long), 19); assert.equal(short.start - long.end, 11);
    assert.equal(length(short), 4); assert.equal((next.start + 360 - short.end) % 360, 11);
    for (const arc of [long, short]) { assert.equal(arc.radius, .614); assert.equal(arc.width, 5.5); }
}
assert.equal(segmented.outer.ticks.length + segmented.inner.ticks.length, 0);
console.log('Segmented arc counts, lengths, gaps and offsets passed.');

// HUD bezel: 48 ticks every 7.5°, 12 major.
const hud = hudBezelSpec();
assert.equal(hud.outer.ticks.length, 48);
assert.equal(hud.outer.ticks.filter(t => t.major).length, 12, 'exactly 12 major ticks');
hud.outer.ticks.forEach((tick, i) => {
    assert.equal(tick.angle, i * 7.5); assert.equal(tick.outer, .80);
    assert.equal(tick.major, i % 4 === 0, 'every fourth tick is major');
    close(tick.outer - tick.inner, tick.major ? .13 : .06, 'tick length');
    assert.equal(tick.width, tick.major ? 11 : 7);
});
const firstMajor = hud.outer.ticks.find(t => t.major).angle;
assert.equal(hud.inner.arcs.length, 4);
hud.inner.arcs.forEach((arc, i) => {
    assert.equal(arc.start, firstMajor + 45 + i * 90, 'inner arcs are offset 45° from the first major tick');
    assert.equal(length(arc), 74); assert.equal(arc.radius, .571); assert.equal(arc.width, 9);
    const next = hud.inner.arcs[(i + 1) % 4];
    assert.equal((next.start + 360 - arc.end) % 360, 16, 'inner gaps are 16°');
});
console.log('HUD bezel ticks, major ticks, arc lengths, gaps and offset passed.');

// Every stroke stays inside the 512 px texture and minor tick spacing stays open.
assert.equal(RING_TEXTURE_SIZE, 512); assert.equal(RING_TEXTURE_HALF, 256);
for (const [name, spec] of [['segmented', segmented], ['hud', hud]]) for (const part of ['outer', 'inner']) {
    const ring = spec[part];
    assert.ok(ringExtent(ring) < RING_TEXTURE_HALF, `${name} ${part} ring is not clipped`);
    for (const arc of ring.arcs) assert.ok(arc.radius * RING_TEXTURE_HALF - arc.width / 2 > 0);
    for (const tick of ring.ticks) assert.ok(tick.inner * RING_TEXTURE_HALF > tick.width / 2);
}
for (const spec of [segmented, hud]) assert.ok(ringExtent(spec.inner) < ringExtent(spec.outer), 'inner ring sits inside the outer ring');
// Neighbouring ticks only overlap radially from the inner end of the shorter one outwards.
hud.outer.ticks.forEach((tick, i) => {
    const next = hud.outer.ticks[(i + 1) % 48];
    const spacing = 2 * Math.PI * Math.max(tick.inner, next.inner) * RING_TEXTURE_HALF / 48;
    assert.ok(spacing - (tick.width + next.width) / 2 > 10, 'tick gaps are wide enough not to alias closed');
});
assert.equal(RING_OUTER_EXTENTS.segmentedArcs, ringExtent(segmented.outer));
assert.equal(RING_OUTER_EXTENTS.hudBezel, ringExtent(hud.outer));
console.log('Ring radii and strokes stay inside the texture.');

// Sizing scales with token size and zoom; the outer ring spans 1.6 token widths on screen.
assert.equal(RING_DIAMETER_RATIO, 1.6);
for (const extent of Object.values(RING_OUTER_EXTENTS)) for (const tokenSize of [36, 100, 250]) for (const zoom of [.125, .5, 1, 4]) {
    const scale = ringSpriteScale(tokenSize, zoom, extent);
    close(2 * extent * scale, RING_DIAMETER_RATIO * tokenSize * zoom, 'outer ring screen diameter');
}
close(ringSpriteScale(200, 1, 200), 2 * ringSpriteScale(100, 1, 200), 'doubling the token doubles the ring');
close(ringSpriteScale(100, 2, 200), 2 * ringSpriteScale(100, 1, 200), 'doubling the zoom doubles the ring');
assert.equal(ringSpriteScale(0, 1, 200), 0); assert.equal(ringSpriteScale(100, 0, 200), 0);
console.log('Ring sizing follows token size and zoom.');

// Rotation is time-based: +20°/s outer, −35°/s inner, static with reduced motion.
assert.deepEqual(RING_SPEEDS, { outer: 20, inner: -35 });
close(ringRotation(1000, RING_SPEEDS.outer), 20 * Math.PI / 180, 'outer ring turns 20° per second');
close(ringRotation(1000, RING_SPEEDS.inner), -35 * Math.PI / 180, 'inner ring turns −35° per second');
close(ringRotation(500, RING_SPEEDS.outer), 10 * Math.PI / 180, 'rotation follows elapsed time, not frames');
close(ringRotation(18000, RING_SPEEDS.outer), 0, 'rotation wraps after a full turn');
assert.equal(ringRotation(1000, RING_SPEEDS.outer, true), 0); assert.equal(ringRotation(1000, RING_SPEEDS.inner, true), 0);
console.log('Ring rotation speeds and reduced motion passed.');

// Placement: active combatant (white, never dead) and every target trail (orange).
const active = { id: 1, column: 2, row: 3 };
const trails = [
    { id: 'target', isTarget: true, toColumn: 4, toRow: 5 },
    { id: 'target:3', isTarget: true, toColumn: 6, toRow: 1 },
    { id: 7, toColumn: 1, toRow: 1 },
    { id: 'target-preview', isTarget: true, preview: true, snapped: false, toColumn: 9.2, toRow: 9.7 },
];
let rings = selectionRingPlacements({ combatMode: true, active, deadIds: new Set(), trails });
assert.deepEqual(rings.map(r => [r.key, r.design, r.tint]), [
    ['active', 'segmentedArcs', RING_TINTS.active], ['target', 'hudBezel', RING_TINTS.target], ['target:3', 'hudBezel', RING_TINTS.target]]);
assert.equal(RING_TINTS.active, 0xffffff); assert.equal(RING_TINTS.target, 0xffa500);
rings = selectionRingPlacements({ combatMode: true, active, deadIds: new Set([1]), trails: [] });
assert.equal(rings.length, 0, 'dead combatants get no ring');
rings = selectionRingPlacements({ combatMode: true, active, trails: [{ ...trails[3], snapped: true, toColumn: 9, toRow: 9 }] });
assert.deepEqual(rings.at(-1), { key: 'target-preview', design: 'hudBezel', tint: 0xffa500, column: 9, row: 9 }, 'snapped preview gets a ring');
assert.equal(selectionRingPlacements({ combatMode: false, active, trails }).length, 0, 'rings are combat-only');
console.log('Ring placement, tints and dead combatant exclusion passed.');

// Texture generation creates two 512 px textures per design and cleans them up.
const calls = [], destroyed = [];
class Graphics { constructor() { this.ops = []; } beginPath() { this.ops.push('begin'); return this; } arc(...a) { this.ops.push(['arc', ...a]); return this; } moveTo() { return this; } lineTo() { return this; } stroke(style) { this.ops.push(['stroke', style]); return this; } destroy() { this.destroyed = true; } }
class Rectangle { constructor(x, y, w, h) { Object.assign(this, { x, y, w, h }); } }
const renderer = { generateTexture(options) { calls.push(options); return { destroy: () => destroyed.push(options) }; } };
const textures = createRingTextures({ Graphics, Rectangle }, renderer);
assert.equal(calls.length, 4, 'each design has separate outer and inner textures');
for (const call of calls) {
    assert.deepEqual([call.frame.w, call.frame.h], [512, 512]); assert.equal(call.resolution, 1);
    assert.ok(call.target.destroyed, 'generator graphics are released');
    const strokes = call.target.ops.filter(op => op[0] === 'stroke');
    assert.ok(strokes.length > 0 && strokes.every(([, s]) => s.color === 0xffffff && s.cap === 'butt'), 'white strokes with consistent butt caps');
}
const segmentedOuterArcs = calls[0].target.ops.filter(op => op[0] === 'arc');
assert.deepEqual(segmentedOuterArcs[0].slice(1, 4), [256, 256, .786 * 256], 'arcs are centred at (256, 256)');
destroyRingTextures(textures);
assert.equal(destroyed.length, 4, 'dispose destroys every ring texture');
console.log('Ring texture generation and disposal passed.');
