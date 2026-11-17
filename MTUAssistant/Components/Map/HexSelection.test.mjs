import assert from 'node:assert/strict';
import {
    hexSelectionSpec, hexCorner, hexCorners, hexPerimeterPoint, hexDashPolyline, hexSelectionPhase, hexSelectionGeometry, drawHexSelection,
    HEX_RADIUS, MIN_SCREEN_RADIUS, HEX_SELECTION_COLOUR, HEX_SELECTION_FILL_ALPHA,
    hexDestinationSpec, hexDestinationGeometry, drawHexDestination, HEX_DESTINATION_COLOUR
} from '../../wwwroot/js/HexSelection.js';
import { RING_SPEEDS } from '../../wwwroot/js/SelectionRings.js';
import { hexCenter, findHex } from '../../wwwroot/js/MapGeometry.js';

const close = (actual, expected, message) => assert.ok(Math.abs(actual - expected) < 1e-9, `${message}: ${actual} ≠ ${expected}`);
const closePoint = (a, b, message) => { close(a.x, b.x, `${message} x`); close(a.y, b.y, `${message} y`); };
const sixth = 1 / 6;

// Corners match the map's flat-topped hexPath: left corner first, clockwise on screen (y down).
const centre = { x: 10, y: 20 };
const rx = 1 / Math.sqrt(3);
const expectedCorners = [[-rx, 0], [-rx / 2, -.5], [rx / 2, -.5], [rx, 0], [rx / 2, .5], [-rx / 2, .5]];
hexCorners(centre, HEX_RADIUS).forEach((corner, k) =>
    closePoint(corner, { x: centre.x + expectedCorners[k][0], y: centre.y + expectedCorners[k][1] }, `corner ${k}`));
closePoint(hexCorner(centre, HEX_RADIUS, 6), hexCorner(centre, HEX_RADIUS, 0), 'corners wrap');
closePoint(hexCorner(centre, HEX_RADIUS, -1), hexCorner(centre, HEX_RADIUS, 5), 'negative corners wrap');
// The hex fits the grid exactly: one parsec flat to flat, and its corners sit on the hex boundary.
const grid = hexCenter(3, 7);
for (const corner of hexCorners(grid, HEX_RADIUS * .999)) {
    const hex = findHex(corner.x, corner.y);
    assert.deepEqual([hex.column, hex.row], [4, 8], 'points just inside the corners belong to the selected hex');
}
console.log('Hex selection corners match the map grid.');

// Perimeter points lie on the edges, and fractions wrap in both directions.
closePoint(hexPerimeterPoint(centre, 1, 0), hexCorner(centre, 1, 0), 't = 0 is the left corner');
closePoint(hexPerimeterPoint(centre, 1, sixth / 2), { x: centre.x - .75, y: centre.y - Math.sqrt(3) / 4 }, 'mid-edge point');
closePoint(hexPerimeterPoint(centre, 1, 1.25), hexPerimeterPoint(centre, 1, .25), 'wraps above 1');
closePoint(hexPerimeterPoint(centre, 1, -.25), hexPerimeterPoint(centre, 1, .75), 'wraps below 0');
for (let i = 0; i < 60; i++) {
    const p = hexPerimeterPoint(centre, 1, i / 60);
    const angle = Math.atan2(p.y - centre.y, p.x - centre.x);
    // Apothem / cos(offset from nearest edge normal) gives the hex boundary distance.
    // Corners sit at multiples of 60°, so edge normals sit at 30° past each corner.
    const offset = ((angle % (Math.PI / 3)) + Math.PI / 3) % (Math.PI / 3) - Math.PI / 6;
    close(Math.hypot(p.x - centre.x, p.y - centre.y), Math.sqrt(3) / 2 / Math.cos(offset), `perimeter point ${i} on the hex`);
}
console.log('Hex perimeter points and wrapping passed.');

// Dashes bend through every corner they cross; length along the perimeter is preserved.
const pathLength = points => points.slice(1).reduce((sum, p, i) => sum + Math.hypot(p.x - points[i].x, p.y - points[i].y), 0);
let dash = hexDashPolyline(centre, 1, .1, .15);
assert.equal(dash.length, 2, 'a dash within one edge is a straight segment');
dash = hexDashPolyline(centre, 1, -.05, .05);
assert.equal(dash.length, 3, 'a dash across a corner bends at it');
closePoint(dash[1], hexCorner(centre, 1, 0), 'bend is at the corner');
close(pathLength(dash), 6 * .1, 'dash length follows the perimeter');
dash = hexDashPolyline(centre, 1, .9, 1.25);
assert.equal(dash.length, 4, 'a long wrapping dash bends at both corners it crosses');
close(pathLength(dash), 6 * .35, 'wrapping dash length');
console.log('Hex dash polylines passed.');

// Spec: six corner-centred outer dashes (73/90 of a sixth); inner long/short pattern every sixth.
const spec = hexSelectionSpec();
assert.equal(spec.outer.dashes.length, 6);
spec.outer.dashes.forEach((d, i) => {
    close((d.start + d.end) / 2, i * sixth, 'outer dashes are centred on corners');
    close(d.end - d.start, sixth * 73 / 90, 'outer dash length');
    const next = spec.outer.dashes[(i + 1) % 6];
    close(((next.start - d.end) % 1 + 1) % 1, sixth * 17 / 90, 'outer gaps');
});
assert.equal(spec.inner.dashes.length, 12);
for (let i = 0; i < 6; i++) {
    const long = spec.inner.dashes[i * 2], short = spec.inner.dashes[i * 2 + 1], next = spec.inner.dashes[(i * 2 + 2) % 12];
    close(long.start, i * sixth, 'inner pattern repeats every sixth');
    close(long.end - long.start, sixth * 19 / 45, 'long dash'); close(short.start - long.end, sixth * 11 / 45, 'gap after long');
    close(short.end - short.start, sixth * 4 / 45, 'short dash'); close(((next.start - short.end) % 1 + 1) % 1, sixth * 11 / 45, 'gap after short');
}
assert.ok(spec.inner.scale < spec.outer.scale, 'inner hex sits inside the outer hex');
assert.equal(spec.outer.scale, 1, 'outer hex follows the hex edge');
assert.ok(spec.inner.scale * HEX_RADIUS * Math.sqrt(3) / 2 + spec.inner.width / 2 / 95 < spec.outer.scale * HEX_RADIUS * Math.sqrt(3) / 2 - spec.outer.width / 2 / 95,
    'inner and outer strokes do not touch at Full zoom');
console.log('Hex selection spec passed.');

// Motion shares the Battlemap speeds: outer clockwise, inner counter-clockwise; static when reduced.
assert.equal(spec.outer.speed, RING_SPEEDS.outer); assert.equal(spec.inner.speed, RING_SPEEDS.inner);
close(hexSelectionPhase(1000, RING_SPEEDS.outer), 20 / 360, 'outer marches 1/18 of the perimeter per second');
close(hexSelectionPhase(1000, RING_SPEEDS.inner), -35 / 360, 'inner marches the other way');
close(hexSelectionPhase(500, RING_SPEEDS.outer), 10 / 360, 'phase follows elapsed time');
close(hexSelectionPhase(18000, RING_SPEEDS.outer), 0, 'phase wraps after a full lap');
assert.equal(hexSelectionPhase(1000, RING_SPEEDS.outer, true), 0); assert.equal(hexSelectionPhase(1000, RING_SPEEDS.inner, true), 0);
const still = hexSelectionGeometry(centre, 50, 0), moved = hexSelectionGeometry(centre, 50, 1000), reduced = hexSelectionGeometry(centre, 50, 1000, true);
closePoint(moved.outer.dashes[0][0], hexPerimeterPoint(centre, moved.outer.radius, spec.outer.dashes[0].start + 20 / 360), 'outer dashes advance clockwise');
closePoint(moved.inner.dashes[0][0], hexPerimeterPoint(centre, moved.inner.radius, -35 / 360), 'inner dashes advance counter-clockwise');
for (const part of ['outer', 'inner']) assert.deepEqual(reduced[part].dashes, still[part].dashes, 'reduced motion keeps the marker still');
console.log('Hex selection motion and reduced motion passed.');

// Sizing: the outer hex is the selected hex at normal zoom and never smaller than 12 CSS px on screen.
assert.equal(MIN_SCREEN_RADIUS, 12);
for (const zoom of [1, 5, 10, 20.7, 25, 55, 95, 192]) {
    const g = hexSelectionGeometry(centre, zoom);
    close(g.radius, Math.max(HEX_RADIUS, 12 / zoom), `radius at zoom ${zoom}`);
    assert.ok(g.radius * zoom >= MIN_SCREEN_RADIUS - 1e-9, 'marker stays visible at low zoom');
    close(g.outer.width * zoom, 3, 'outer stroke is 3 CSS px'); close(g.inner.width * zoom, 2, 'inner stroke is 2 CSS px');
    close(g.inner.radius, g.radius * spec.inner.scale, 'inner hex scale');
    assert.equal(g.fill.length, 6);
}
closePoint(hexSelectionGeometry(centre, 95).fill[0], hexCorner(centre, HEX_RADIUS, 0), 'fill covers the selected hex at close zoom');
assert.equal(hexSelectionGeometry(null, 50), null, 'no selection, no marker');
assert.equal(hexSelectionGeometry(centre, 0), null, 'invalid zoom draws nothing');
console.log('Hex selection sizing passed.');

// Drawing: one fill plus one stroked path per hex, in the selection colour with butt caps.
class Graphics {
    constructor() { this.ops = []; }
    clear() { this.ops.push(['clear']); return this; }
    poly(points, closed) { this.ops.push(['poly', points, closed]); return this; }
    fill(style) { this.ops.push(['fill', style]); return this; }
    beginPath() { this.ops.push(['begin']); return this; }
    moveTo(x, y) { this.ops.push(['move', x, y]); return this; }
    lineTo(x, y) { this.ops.push(['line', x, y]); return this; }
    stroke(style) { this.ops.push(['stroke', style]); return this; }
}
const graphics = new Graphics();
const geometry = hexSelectionGeometry(centre, 40, 2500);
drawHexSelection(graphics, geometry);
assert.equal(graphics.ops[0][0], 'clear', 'previous frame is cleared first');
assert.deepEqual(graphics.ops.find(op => op[0] === 'fill')[1], { color: HEX_SELECTION_COLOUR, alpha: HEX_SELECTION_FILL_ALPHA });
assert.equal(HEX_SELECTION_COLOUR, 0x83e3d1, 'keeps the Galaxy Map selection teal');
const strokes = graphics.ops.filter(op => op[0] === 'stroke');
assert.deepEqual(strokes.map(([, s]) => [s.color, s.width, s.cap]), [[0x83e3d1, 3 / 40, 'butt'], [0x83e3d1, 2 / 40, 'butt']]);
assert.equal(graphics.ops.filter(op => op[0] === 'move').length, 18, 'six outer and twelve inner dashes');
const cleared = new Graphics();
drawHexSelection(cleared, null);
assert.deepEqual(cleared.ops, [['clear']], 'clearing the selection removes the marker');
console.log('Hex selection drawing passed.');

// Destination marker: the hex HUD bezel in the Battlemap target orange.
assert.equal(HEX_DESTINATION_COLOUR, 0xffa500, 'matches the Battlemap target colour');
const bezel = hexDestinationSpec();
assert.equal(bezel.outer.ticks.length, 24);
assert.equal(bezel.outer.ticks.filter(t => t.major).length, 6, 'one major tick per corner');
bezel.outer.ticks.forEach((tick, i) => {
    close(tick.at, i / 24, 'ticks are evenly spaced around the perimeter');
    assert.equal(tick.major, i % 4 === 0);
    assert.equal(tick.width, tick.major ? 3 : 2);
    assert.ok(tick.length > 0 && tick.length < 1);
    if (tick.major) assert.ok(Math.abs(tick.at * 6 - Math.round(tick.at * 6)) < 1e-9, 'major ticks sit on corners');
});
assert.equal(bezel.inner.dashes.length, 6);
bezel.inner.dashes.forEach((d, i) => {
    close((d.start + d.end) / 2, (i + .5) * sixth, 'inner dashes are centred on edge midpoints');
    close(d.end - d.start, sixth * 74 / 90, 'inner dash length');
});
assert.equal(bezel.outer.speed, RING_SPEEDS.outer); assert.equal(bezel.inner.speed, RING_SPEEDS.inner);
// The inner dashes never reach the inner ends of the ticks.
const innermostTick = Math.min(...bezel.outer.ticks.map(t => (1 - t.length) * (t.major ? 1 : Math.sqrt(3) / 2)));
assert.ok(bezel.inner.scale < innermostTick, 'inner hex stays clear of the ticks');
console.log('Hex destination spec passed.');

const bezelStill = hexDestinationGeometry(centre, 50, 0);
bezelStill.outer.ticks.forEach((tick, i) => {
    closePoint(tick.to, hexPerimeterPoint(centre, bezelStill.outer.radius, i / 24), `tick ${i} starts on the outer hex`);
    const spec = bezel.outer.ticks[i];
    close(Math.hypot(tick.to.x - tick.from.x, tick.to.y - tick.from.y),
        spec.length * Math.hypot(tick.to.x - centre.x, tick.to.y - centre.y), 'ticks point at the centre');
    close(tick.width * 50, spec.width, 'tick widths are CSS px');
});
const bezelMoved = hexDestinationGeometry(centre, 50, 1000), bezelReduced = hexDestinationGeometry(centre, 50, 1000, true);
closePoint(bezelMoved.outer.ticks[0].to, hexPerimeterPoint(centre, bezelMoved.outer.radius, 20 / 360), 'ticks march clockwise');
closePoint(bezelMoved.inner.dashes[0][0], hexPerimeterPoint(centre, bezelMoved.inner.radius, bezel.inner.dashes[0].start - 35 / 360), 'inner dashes march counter-clockwise');
assert.deepEqual(bezelReduced.outer.ticks, bezelStill.outer.ticks, 'reduced motion keeps the ticks still');
assert.deepEqual(bezelReduced.inner.dashes, bezelStill.inner.dashes, 'reduced motion keeps the inner dashes still');
for (const zoom of [1, 10, 55, 192]) close(hexDestinationGeometry(centre, zoom).radius, Math.max(HEX_RADIUS, 12 / zoom), `destination radius at zoom ${zoom}`);
assert.equal(hexDestinationGeometry(null, 50), null);

const bezelGraphics = new Graphics();
drawHexDestination(bezelGraphics, hexDestinationGeometry(centre, 40, 700));
assert.equal(bezelGraphics.ops[0][0], 'clear');
assert.equal(bezelGraphics.ops.some(op => op[0] === 'fill'), false, 'the destination has no fill, like the Battlemap bezel');
assert.deepEqual(bezelGraphics.ops.filter(op => op[0] === 'stroke').map(([, s]) => [s.color, s.width * 40, s.cap]),
    [[0xffa500, 3, 'butt'], [0xffa500, 2, 'butt'], [0xffa500, 2.5, 'butt']], 'major ticks, minor ticks, then inner dashes');
assert.equal(bezelGraphics.ops.filter(op => op[0] === 'move').length, 24 + 6);
const bezelCleared = new Graphics();
drawHexDestination(bezelCleared, null);
assert.deepEqual(bezelCleared.ops, [['clear']], 'clearing the destination removes the marker');
console.log('Hex destination geometry, motion and drawing passed.');
