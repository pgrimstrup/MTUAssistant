import { hexDistance, jumpLabel } from "./MapGeometry.js";

// Global hexes are never negative and an atlas is far under 100000 rows tall.
const hexKey = ({ column, row }) => column * 100000 + row;
const plainHex = ({ column, row }) => ({ column, row });

// At most this many routes are listed and drawn when several share the fewest jumps.
export const ROUTE_OPTIONS = 4;

// Plans jump routes between global hexes for a ship with a given jump range, using the charted
// systems as stopping points. "Quickest" means fewest jumps; routes with the same number of jumps
// are all options, listed by fewest parsecs in total.
// RULES-ASSUMPTION: every jump is at most the J-Drive rating in parsecs, and the ship stops only
// at charted systems between the two ends (it never jumps into empty hexes to break up a trip).
// Fuel, refuelling, zones and jump time are not considered.
export class JumpRouter {
    // Systems are kept as given, so system() returns the caller's world (name, UWP and so on).
    constructor(systems) {
        this.systems = new Map();
        for (const system of systems ?? [])
            if (Number.isInteger(system?.column) && Number.isInteger(system?.row)) this.systems.set(hexKey(system), system);
        this.cache = null;
    }

    system(hex) { return this.systems.get(hexKey(hex)) ?? null; }

    // Charted systems within jump parsecs of a hex, not counting the hex itself.
    within(hex, jump) {
        const found = [];
        for (let column = hex.column - jump; column <= hex.column + jump; column++)
            for (let row = hex.row - jump - 1; row <= hex.row + jump + 1; row++) {
                if (column === hex.column && row === hex.row) continue;
                const system = this.systems.get(column * 100000 + row);
                if (system && hexDistance(hex, system) <= jump) found.push(system);
            }
        return found;
    }

    // Fewest jumps (then fewest parsecs) from the origin to every system it can reach, worked
    // out one jump at a time. Cached for the last origin and range, since a drag keeps its origin.
    reachable(origin, jump) {
        const start = hexKey(origin);
        if (this.cache?.start === start && this.cache.jump === jump) return this.cache.tree;
        const tree = new Map([[start, { hex: plainHex(origin), jumps: 0, distance: 0 }]]);
        let frontier = [start];
        while (frontier.length) {
            const next = new Map();
            for (const key of frontier) {
                const from = tree.get(key);
                for (const system of this.within(from.hex, jump)) {
                    const systemKey = hexKey(system);
                    if (tree.has(systemKey)) continue;
                    const distance = from.distance + hexDistance(from.hex, system);
                    const best = next.get(systemKey);
                    if (!best || distance < best.distance) next.set(systemKey, { hex: plainHex(system), jumps: from.jumps + 1, distance });
                }
            }
            for (const [key, entry] of next) tree.set(key, entry);
            frontier = [...next.keys()];
        }
        this.cache = { start, jump, tree, ways: new Map() };
        return tree;
    }

    // Systems one jump closer to the origin on some fewest-jump route to this tree entry.
    previous(tree, entry, jump) {
        if (entry.jumps === 1) return [this.cache.start];
        return this.within(entry.hex, jump).map(hexKey).filter(key => tree.get(key)?.jumps === entry.jumps - 1);
    }

    // How many fewest-jump routes lead from the origin to a tree entry.
    ways(tree, key, jump) {
        const entry = tree.get(key);
        if (entry.jumps === 0) return 1;
        const memo = this.cache.ways;
        if (!memo.has(key)) memo.set(key, this.previous(tree, entry, jump).reduce((sum, from) => sum + this.ways(tree, from, jump), 0));
        return memo.get(key);
    }

    // Every fewest-jump route from origin to target, fewest parsecs first, up to limit routes.
    // Each route lists its stops, both ends included. count is how many such routes exist in
    // all; it is 0 when the target cannot be reached. A target within one jump is a direct route.
    routes(origin, target, jump, limit = ROUTE_OPTIONS) {
        if (!Number.isInteger(jump) || jump < 1) return { routes: [], count: 0 };
        if (hexDistance(origin, target) <= jump) return { routes: [[plainHex(origin), plainHex(target)]], count: 1 };
        const tree = this.reachable(origin, jump);
        const last = this.within(target, jump).map(hexKey).filter(key => tree.has(key));
        if (!last.length) return { routes: [], count: 0 };
        const fewest = Math.min(...last.map(key => tree.get(key).jumps));
        const ends = last.filter(key => tree.get(key).jumps === fewest);
        // Capped so a vast number of routes stays a finite number in the cast snapshot.
        const count = Math.min(Number.MAX_SAFE_INTEGER, ends.reduce((sum, key) => sum + this.ways(tree, key, jump), 0));
        // Best-first walk back from the target. A tree entry's distance is the shortest way to it
        // from the origin, so each partial route's priority is exactly its best total and
        // complete routes come out shortest first. Ties go to the route nearest the origin, so a
        // dense field of equal routes still finishes one route at a time.
        let order = 0;
        const queue = ends.map(key => ({ key, stops: [plainHex(target)], tail: hexDistance(tree.get(key).hex, target), order: order++ }));
        const priority = item => tree.get(item.key).distance + item.tail;
        const before = (a, b) => {
            const pa = priority(a), pb = priority(b);
            if (pa !== pb) return pa < pb;
            const ja = tree.get(a.key).jumps, jb = tree.get(b.key).jumps;
            return ja !== jb ? ja < jb : a.order < b.order;
        };
        const routes = [];
        for (let steps = 0; queue.length && routes.length < limit && steps < 20000; steps++) {
            let best = 0;
            for (let i = 1; i < queue.length; i++) if (before(queue[i], queue[best])) best = i;
            const item = queue.splice(best, 1)[0];
            const entry = tree.get(item.key);
            const stops = [entry.hex, ...item.stops];
            if (entry.jumps === 0) { routes.push(stops); continue; }
            for (const key of this.previous(tree, entry, jump))
                queue.push({ key, stops, tail: item.tail + hexDistance(tree.get(key).hex, entry.hex), order: order++ });
        }
        return { routes, count };
    }

    // The quickest route (fewest jumps, then fewest parsecs), or null when there is none.
    route(origin, target, jump) { return this.routes(origin, target, jump, 1).routes[0] ?? null; }
}

// What a measurement or destination shows for the equipped ship: null when no ship (or no
// J-Drive) is known or the target is within one jump, otherwise the jump range, up to
// ROUTE_OPTIONS fewest-jump routes (none when the target cannot be reached) and how many such
// routes there are in all.
export function jumpPlan(router, origin, target, jump) {
    if (!router || !Number.isInteger(jump) || jump < 1 || hexDistance(origin, target) <= jump) return null;
    return { jump, ...router.routes(origin, target, jump) };
}

export const routeParsecs = route => route.slice(1).reduce((sum, stop, i) => sum + hexDistance(route[i], stop), 0);
const plural = (count, word) => `${count} ${word}${count === 1 ? "" : "s"}`;

export function jumpPlanLabel(parsecs, plan) {
    const distance = jumpLabel(parsecs);
    if (!plan) return distance;
    if (!plan.routes.length) return `${distance} · no Jump-${plan.jump} route`;
    return `${distance} · ${plural(plan.routes[0].length - 1, "jump")}`;
}

// Sector hex ("0205") of a global hex.
export const sectorHex = ({ column, row }) => String(column % 32 + 1).padStart(2, "0") + String(row % 40 + 1).padStart(2, "0");

// The stop-over lists shown at the top left: one per route option, each stop as hex, UWP and
// name (an empty hex has no UWP and is called "Empty hex"), with the jumps and parsecs on top.
export function routeLists(plan, lookup = () => null) {
    if (!plan) return null;
    if (!plan.routes.length) return { title: `No Jump-${plan.jump} route`, lists: [], more: 0 };
    const jumps = plan.routes[0].length - 1;
    const lists = plan.routes.map((route, index) => ({
        title: `${plan.routes.length > 1 ? `Option ${index + 1} · ` : ""}${plural(jumps, "jump")} · ${jumpLabel(routeParsecs(route))}`,
        stops: route.map(hex => {
            const world = lookup(hex);
            return { hex: sectorHex(hex), uwp: world?.uwp || "", name: world ? world.name || "Unnamed" : "Empty hex" };
        })
    }));
    const more = Math.max(0, (plan.count ?? lists.length) - lists.length);
    return { title: `Jump-${plan.jump} route · ${plural(jumps, "jump")}`, lists, more };
}

// Long trips through busy space can have billions of equal routes, so big counts are rounded off.
export function moreRoutesLabel(more) {
    if (!more) return "";
    const amount = more >= 1000 ? "over 1,000" : more.toLocaleString("en");
    return `+${amount} more ${more === 1 ? "route" : "routes"} with the same number of jumps`;
}

// Splits a screen-space polyline into dash segments. The pattern carries on across corners so a
// route with short legs still reads as one dashed line. offset moves the dashes forward along
// the line, from the first point towards the last, so a growing offset animates the direction
// of travel.
export function dashedSegments(points, dash = 8, gap = 6, offset = 0) {
    const segments = [];
    const period = dash + gap, phase = ((-offset % period) + period) % period;
    let drawing = phase < dash, left = drawing ? dash - phase : period - phase;
    for (let i = 1; i < points.length; i++) {
        const a = points[i - 1], b = points[i];
        const length = Math.hypot(b.x - a.x, b.y - a.y);
        let at = 0;
        while (length - at > 1e-9) {
            const step = Math.min(left, length - at);
            if (drawing) {
                const t0 = at / length, t1 = (at + step) / length;
                segments.push([{ x: a.x + (b.x - a.x) * t0, y: a.y + (b.y - a.y) * t0 },
                    { x: a.x + (b.x - a.x) * t1, y: a.y + (b.y - a.y) * t1 }]);
            }
            at += step;
            left -= step;
            if (left <= 1e-9) { drawing = !drawing; left = drawing ? dash : gap; }
        }
    }
    return segments;
}
