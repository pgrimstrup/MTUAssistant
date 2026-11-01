export { findHex } from "./MapGeometry.js";

// Passive observers never capture a pointer or stop propagation to pixi-viewport.
export function observeClicks(canvas, onClick) {
    const pointers = new Set();
    let candidate;
    const down = e => {
        pointers.add(e.pointerId);
        candidate = pointers.size === 1 && e.isPrimary && e.button === 0
            ? { id: e.pointerId, x: e.clientX, y: e.clientY } : null;
    };
    const move = e => {
        if (candidate?.id === e.pointerId && Math.hypot(e.clientX - candidate.x, e.clientY - candidate.y) >= 4)
            candidate = null;
    };
    const up = e => {
        move(e);
        const click = candidate?.id === e.pointerId;
        pointers.delete(e.pointerId);
        candidate = null;
        if (click) onClick(e);
    };
    const cancel = e => { pointers.delete(e.pointerId); candidate = null; };
    const wheel = () => { candidate = null; };
    const handlers = { pointerdown: down, pointermove: move, pointerup: up,
        pointercancel: cancel, pointerleave: cancel, wheel };
    for (const [name, handler] of Object.entries(handlers)) canvas.addEventListener(name, handler, { passive: true });
    return () => {
        for (const [name, handler] of Object.entries(handlers)) canvas.removeEventListener(name, handler);
        pointers.clear();
        candidate = null;
    };
}
