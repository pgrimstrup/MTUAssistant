// Animated selection and jump-destination markers for the Galaxy Map: the hex-shaped
// counterparts of the Battlemap selection rings. Marks march around concentric hex outlines
// in opposite directions.
// Geometry is plain data in map units (1 parsec between rows) so it can be checked in Node;
// the draw functions are a thin PixiJS layer over it.
import { ringRotation, RING_SPEEDS } from './SelectionRings.js';

// Circumradius of a flat-topped hex whose flat-to-flat height is one parsec.
export const HEX_RADIUS = 1 / Math.sqrt(3);
// The marker never shrinks below this outer circumradius in CSS pixels, so it stays visible
// at Overview and Sector zoom where a hex is only a few pixels wide.
export const MIN_SCREEN_RADIUS = 12;
export const HEX_SELECTION_COLOUR = 0x83e3d1;
export const HEX_SELECTION_FILL_ALPHA = .10;

// Dashes are [start, end] fractions of the perimeter, measured clockwise from the left corner.
// Outer: six dashes, each 73/90 of a sixth (the Segmented arcs proportion), centred on corners.
// Inner: per sixth, a long dash (19/45), gap (11/45), short dash (4/45), gap (11/45).
export function hexSelectionSpec() {
    const sixth = 1 / 6;
    const outerLength = sixth * 73 / 90;
    const outer = Array.from({ length: 6 }, (_, i) => ({ start: i * sixth - outerLength / 2, end: i * sixth + outerLength / 2 }));
    const inner = [];
    for (let i = 0; i < 6; i++) {
        const base = i * sixth;
        inner.push({ start: base, end: base + sixth * 19 / 45 });
        inner.push({ start: base + sixth * 30 / 45, end: base + sixth * 34 / 45 });
    }
    return {
        outer: { scale: 1, width: 3, speed: RING_SPEEDS.outer, dashes: outer },
        inner: { scale: .78, width: 2, speed: RING_SPEEDS.inner, dashes: inner },
    };
}

// Corner k (0–5) of a hex, clockwise on screen (y down) from the left corner, matching hexPath.
export function hexCorner(center, radius, k) {
    const i = ((k % 6) + 6) % 6;
    const angle = Math.PI + i * Math.PI / 3;
    return { x: center.x + Math.cos(angle) * radius, y: center.y + Math.sin(angle) * radius };
}

export function hexCorners(center, radius) {
    return Array.from({ length: 6 }, (_, k) => hexCorner(center, radius, k));
}

// Point at perimeter fraction t (any real number; wraps every full turn).
export function hexPerimeterPoint(center, radius, t) {
    const position = (((t % 1) + 1) % 1) * 6;
    const k = Math.floor(position), f = position - k;
    const a = hexCorner(center, radius, k), b = hexCorner(center, radius, k + 1);
    return { x: a.x + (b.x - a.x) * f, y: a.y + (b.y - a.y) * f };
}

// Polyline for a dash from start to end (end > start), bending through any corners it crosses.
export function hexDashPolyline(center, radius, start, end) {
    const points = [hexPerimeterPoint(center, radius, start)];
    for (let corner = Math.floor(start * 6) + 1; corner < end * 6; corner++)
        points.push(hexCorner(center, radius, corner));
    points.push(hexPerimeterPoint(center, radius, end));
    return points;
}

// Perimeter phase (fraction of a turn) after elapsedMs; shares the Battlemap ring speeds.
export function hexSelectionPhase(elapsedMs, degreesPerSecond, reducedMotion = false) {
    return ringRotation(elapsedMs, degreesPerSecond, reducedMotion) / (2 * Math.PI);
}

// Everything needed to draw the marker at a map-unit centre and zoom (CSS px per parsec).
export function hexSelectionGeometry(center, zoom, elapsedMs = 0, reducedMotion = false) {
    if (!center || !(zoom > 0)) return null;
    const radius = Math.max(HEX_RADIUS, MIN_SCREEN_RADIUS / zoom);
    const spec = hexSelectionSpec();
    const ring = part => {
        const phase = hexSelectionPhase(elapsedMs, part.speed, reducedMotion);
        const r = radius * part.scale;
        return { radius: r, width: part.width / zoom, phase,
            dashes: part.dashes.map(dash => hexDashPolyline(center, r, dash.start + phase, dash.end + phase)) };
    };
    return { radius, fill: hexCorners(center, radius), outer: ring(spec.outer), inner: ring(spec.inner) };
}

export function drawHexSelection(graphics, geometry, colour = HEX_SELECTION_COLOUR) {
    graphics.clear();
    if (!geometry) return;
    graphics.poly(geometry.fill.flatMap(p => [p.x, p.y]), true).fill({ color: colour, alpha: HEX_SELECTION_FILL_ALPHA });
    for (const ring of [geometry.outer, geometry.inner]) {
        graphics.beginPath();
        for (const dash of ring.dashes) {
            graphics.moveTo(dash[0].x, dash[0].y);
            for (const point of dash.slice(1)) graphics.lineTo(point.x, point.y);
        }
        graphics.stroke({ color: colour, width: ring.width, cap: 'butt', join: 'miter' });
    }
}

// Jump destination marker: the hex counterpart of the Battlemap's orange HUD bezel. Radial
// ticks march clockwise around the outer hex, with a major tick on every corner when still;
// edge-centred inner dashes (74/90 of a sixth) march the other way.
export const HEX_DESTINATION_COLOUR = 0xffa500;

export function hexDestinationSpec() {
    const sixth = 1 / 6;
    const ticks = Array.from({ length: 24 }, (_, i) => {
        const major = i % 4 === 0;
        return { at: i / 24, length: major ? .26 : .14, width: major ? 3 : 2, major };
    });
    const dashLength = sixth * 74 / 90;
    const dashes = Array.from({ length: 6 }, (_, i) => ({ start: (i + .5) * sixth - dashLength / 2, end: (i + .5) * sixth + dashLength / 2 }));
    return {
        outer: { scale: 1, speed: RING_SPEEDS.outer, ticks },
        inner: { scale: .62, width: 2.5, speed: RING_SPEEDS.inner, dashes },
    };
}

export function hexDestinationGeometry(center, zoom, elapsedMs = 0, reducedMotion = false) {
    if (!center || !(zoom > 0)) return null;
    const radius = Math.max(HEX_RADIUS, MIN_SCREEN_RADIUS / zoom);
    const spec = hexDestinationSpec();
    const outerPhase = hexSelectionPhase(elapsedMs, spec.outer.speed, reducedMotion);
    const innerPhase = hexSelectionPhase(elapsedMs, spec.inner.speed, reducedMotion);
    const outerRadius = radius * spec.outer.scale, innerRadius = radius * spec.inner.scale;
    const ticks = spec.outer.ticks.map(tick => {
        const outer = hexPerimeterPoint(center, outerRadius, tick.at + outerPhase);
        const scale = 1 - tick.length;
        const inner = { x: center.x + (outer.x - center.x) * scale, y: center.y + (outer.y - center.y) * scale };
        return { from: inner, to: outer, width: tick.width / zoom, major: tick.major };
    });
    return {
        radius,
        outer: { radius: outerRadius, phase: outerPhase, ticks },
        inner: { radius: innerRadius, width: spec.inner.width / zoom, phase: innerPhase,
            dashes: spec.inner.dashes.map(dash => hexDashPolyline(center, innerRadius, dash.start + innerPhase, dash.end + innerPhase)) },
    };
}

export function drawHexDestination(graphics, geometry, colour = HEX_DESTINATION_COLOUR) {
    graphics.clear();
    if (!geometry) return;
    for (const major of [true, false]) {
        const ticks = geometry.outer.ticks.filter(tick => tick.major === major);
        if (!ticks.length) continue;
        graphics.beginPath();
        for (const tick of ticks) graphics.moveTo(tick.from.x, tick.from.y).lineTo(tick.to.x, tick.to.y);
        graphics.stroke({ color: colour, width: ticks[0].width, cap: 'butt' });
    }
    graphics.beginPath();
    for (const dash of geometry.inner.dashes) {
        graphics.moveTo(dash[0].x, dash[0].y);
        for (const point of dash.slice(1)) graphics.lineTo(point.x, point.y);
    }
    graphics.stroke({ color: colour, width: geometry.inner.width, cap: 'butt', join: 'miter' });
}
