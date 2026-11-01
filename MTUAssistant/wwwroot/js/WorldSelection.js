// Poster tiles place odd columns half a parsec above even columns.
// Search adjacent centres rather than rounding x and y independently near hex corners.
export function findHex(x, y, sectorWidth, sectorHeight) {
    const stepX = sectorWidth / 32;
    const stepY = sectorHeight / 40;
    // Traveller Map poster images include a 0.25-column left margin and
    // a 0.10-parsec top margin. TileMap draws that padding at each tile's origin.
    x -= stepX * 0.25;
    y -= stepY * 0.10;
    const approximateColumn = Math.floor(x / stepX);
    let best;
    let distance = Infinity;
    for (let column = approximateColumn - 1; column <= approximateColumn + 1; column++) {
        const offset = column % 2 === 0 ? 0.5 : 1;
        const row = Math.round(y / stepY - offset);
        const cx = (column + 0.5) * stepX;
        const cy = (row + offset) * stepY;
        const d = Math.hypot(x - cx, y - cy);
        if (d < distance) {
            distance = d;
            best = { sectorColumn: Math.floor(column / 32), sectorRow: Math.floor(row / 40),
                column: ((column % 32) + 32) % 32 + 1, row: ((row % 40) + 40) % 40 + 1 };
        }
    }
    return best;
}

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
