import assert from 'node:assert/strict';
import { JumpRouter, jumpPlan, jumpPlanLabel, dashedSegments, routeLists, moreRoutesLabel, routeParsecs, sectorHex, ROUTE_OPTIONS } from '../../wwwroot/js/JumpRoute.js';
import { hexDistance } from '../../wwwroot/js/MapGeometry.js';

const hex = (column, row) => ({ column, row });
const legs = route => route.slice(1).map((stop, i) => hexDistance(route[i], stop));

// Within range the route is the direct jump, whether or not either end is a charted system.
let router = new JumpRouter([]);
assert.deepEqual(router.route(hex(0, 0), hex(2, 0), 2), [hex(0, 0), hex(2, 0)]);
assert.equal(router.route(hex(0, 0), hex(3, 0), 2), null, 'empty space cannot be crossed');
assert.equal(router.route(hex(0, 0), hex(1, 0), 0), null, 'a ship with no J-Drive has no route');

// A chain of systems two parsecs apart along a row carries a Jump-2 ship eight parsecs.
router = new JumpRouter([hex(2, 0), hex(4, 0), hex(6, 0), hex(8, 0)]);
let route = router.route(hex(0, 0), hex(8, 0), 2);
assert.deepEqual(route, [hex(0, 0), hex(2, 0), hex(4, 0), hex(6, 0), hex(8, 0)]);
assert.ok(legs(route).every(leg => leg <= 2), 'no leg is longer than the jump range');
assert.deepEqual(router.route(hex(0, 0), hex(11, 0), 2), null, 'three parsecs past the last system is out of reach');
assert.deepEqual(router.route(hex(0, 0), hex(10, 0), 2), [hex(0, 0), hex(2, 0), hex(4, 0), hex(6, 0), hex(8, 0), hex(10, 0)],
    'an uncharted target hex can be the last stop');
assert.deepEqual(router.route(hex(0, 0), hex(8, 0), 4), [hex(0, 0), hex(4, 0), hex(8, 0)], 'a longer jump skips stops');

// Fewest jumps wins over fewest parsecs; equal jumps go the shorter way.
router = new JumpRouter([hex(3, 0), hex(6, 0), hex(1, 2), hex(2, 4), hex(3, 6), hex(5, 4), hex(6, 2)]);
route = router.route(hex(0, 0), hex(9, 0), 3);
assert.deepEqual(route, [hex(0, 0), hex(3, 0), hex(6, 0), hex(9, 0)], 'three jumps straight along the row');
router = new JumpRouter([hex(2, 0), hex(2, 3), hex(4, 0), hex(4, 3)]);
route = router.route(hex(0, 0), hex(6, 0), 2);
assert.equal(route.length - 1, 3);
assert.deepEqual(route, [hex(0, 0), hex(2, 0), hex(4, 0), hex(6, 0)], 'ties on jumps go the fewest parsecs');
assert.equal(legs(route).reduce((a, b) => a + b), 6);

// A Jump-1 ship steps hex by hex along a winding main, ignoring systems off the way.
router = new JumpRouter([hex(1, 0), hex(2, 1), hex(3, 1), hex(4, 1), hex(1, 5), hex(3, 8)]);
route = router.route(hex(0, 0), hex(5, 0), 1);
assert.deepEqual(route, [hex(0, 0), hex(1, 0), hex(2, 1), hex(3, 1), hex(4, 1), hex(5, 0)]);
assert.ok(legs(route).every(leg => leg === 1));
assert.equal(new JumpRouter([hex(1, 0), hex(3, 1), hex(4, 1)]).route(hex(0, 0), hex(5, 0), 1), null, 'a gap in the main blocks a Jump-1 ship');

// Routes cross sector seams like any other hexes (column 31 to 32, row 39 to 40).
router = new JumpRouter([hex(30, 39), hex(32, 40)]);
route = router.route(hex(28, 38), hex(34, 41), 2);
assert.deepEqual(route, [hex(28, 38), hex(30, 39), hex(32, 40), hex(34, 41)]);

// The reachability tree is cached for a drag's origin and rebuilt for a new origin or range.
router = new JumpRouter([hex(2, 0), hex(4, 0)]);
const tree = router.reachable(hex(0, 0), 2);
assert.equal(router.reachable(hex(0, 0), 2), tree);
assert.notEqual(router.reachable(hex(0, 0), 3), tree);
assert.notEqual(router.reachable(hex(1, 0), 2), router.reachable(hex(0, 0), 2));
assert.equal(new JumpRouter([null, { column: 1.5, row: 0 }, hex(2, 0)]).systems.size, 1, 'malformed systems are skipped');

// Every route with the fewest jumps is an option, fewest parsecs first, checked against a
// brute-force search of every sequence of stops.
const brute = (systems, origin, target, jump) => {
    const found = [];
    const walk = (stops, depth) => {
        const at = stops.at(-1);
        if (hexDistance(at, target) <= jump) found.push([...stops, target]);
        if (depth === 0) return;
        for (const system of systems)
            if (hexDistance(at, system) <= jump && !stops.some(stop => stop.column === system.column && stop.row === system.row))
                walk([...stops, system], depth - 1);
    };
    for (let depth = 0; depth <= systems.length && !found.length; depth++) walk([origin], depth);
    return found;
};
const grid = [];
for (let column = 1; column <= 7; column++) for (let row = -2; row <= 2; row++) if ((column * 7 + row * 3) % 4 !== 0) grid.push(hex(column, row));
router = new JumpRouter(grid);
let options = router.routes(hex(0, 0), hex(8, 0), 2, 1000);
const expected = brute(grid, hex(0, 0), hex(8, 0), 2);
assert.ok(expected.length > 4, `the grid has ${expected.length} fewest-jump routes`);
assert.equal(options.count, expected.length, 'count is every fewest-jump route');
assert.equal(options.routes.length, expected.length, 'with a high limit every route is listed');
assert.ok(options.routes.every(route => route.length === expected[0].length), 'every option has the fewest jumps');
assert.ok(options.routes.every(route => legs(route).every(leg => leg <= 2)));
const parsecs = options.routes.map(routeParsecs);
assert.deepEqual(parsecs, [...parsecs].sort((a, b) => a - b), 'options come fewest parsecs first');
assert.deepEqual([...new Set(options.routes.map(route => JSON.stringify(route)))].sort(), expected.map(route => JSON.stringify(route.map(stop => ({ column: stop.column, row: stop.row })))).sort(),
    'the options are exactly the brute-force routes');
assert.equal(Math.min(...expected.map(routeParsecs)), parsecs[0]);
const limited = router.routes(hex(0, 0), hex(8, 0), 2);
assert.equal(limited.routes.length, ROUTE_OPTIONS, `only ${ROUTE_OPTIONS} options by default`);
assert.equal(limited.count, expected.length);
assert.deepEqual(limited.routes, options.routes.slice(0, ROUTE_OPTIONS), 'the shortest options are kept');
assert.deepEqual(router.route(hex(0, 0), hex(8, 0), 2), options.routes[0], 'route() is the quickest option');
assert.deepEqual(router.routes(hex(0, 0), hex(1, 0), 2), { routes: [[hex(0, 0), hex(1, 0)]], count: 1 });
assert.deepEqual(router.routes(hex(0, 0), hex(30, 0), 2), { routes: [], count: 0 });
assert.deepEqual(router.routes(hex(0, 0), hex(30, 0), 0), { routes: [], count: 0 });

// The plan is only needed past one jump, and only with a J-Drive.
router = new JumpRouter([hex(2, 0)]);
assert.equal(jumpPlan(router, hex(0, 0), hex(2, 0), 2), null, 'one jump needs no plan');
assert.equal(jumpPlan(router, hex(0, 0), hex(4, 0), null), null, 'no equipped ship needs no plan');
assert.equal(jumpPlan(null, hex(0, 0), hex(4, 0), 2), null);
const plan = jumpPlan(router, hex(0, 0), hex(4, 0), 2);
assert.deepEqual(plan, { jump: 2, routes: [[hex(0, 0), hex(2, 0), hex(4, 0)]], count: 1 });
assert.deepEqual(jumpPlan(router, hex(0, 0), hex(5, 0), 2), { jump: 2, routes: [], count: 0 });
assert.equal(jumpPlanLabel(6, null), '6 parsecs');
assert.equal(jumpPlanLabel(4, plan), '4 parsecs · 2 jumps');
assert.equal(jumpPlanLabel(5, { jump: 2, routes: [], count: 0 }), '5 parsecs · no Jump-2 route');

// Stop-over lists: hex, UWP and name for every stop, the jumps on top, side-by-side options.
assert.equal(sectorHex(hex(0, 0)), '0101');
assert.equal(sectorHex(hex(33, 44)), '0205', 'sector hexes restart in each sector');
const worlds = new Map([['2,0', { name: 'Regina', uwp: 'A788899-C' }], ['4,0', { name: '', uwp: 'X000000-0' }]]);
const lookup = stop => worlds.get(`${stop.column},${stop.row}`) ?? null;
assert.equal(routeLists(null, lookup), null, 'nothing to list without a plan');
assert.deepEqual(routeLists(plan, lookup), { title: 'Jump-2 route · 2 jumps', more: 0, lists: [{ title: '2 jumps · 4 parsecs', stops: [
    { hex: '0101', uwp: '', name: 'Empty hex' }, { hex: '0301', uwp: 'A788899-C', name: 'Regina' }, { hex: '0501', uwp: 'X000000-0', name: 'Unnamed' }] }] });
const two = { jump: 2, routes: [[hex(0, 0), hex(2, 0), hex(4, 0)], [hex(0, 0), hex(2, 1), hex(4, 0)]], count: 7 };
const twoLists = routeLists(two, lookup);
assert.deepEqual(twoLists.lists.map(list => list.title), ['Option 1 · 2 jumps · 4 parsecs', 'Option 2 · 2 jumps · 4 parsecs']);
assert.equal(twoLists.more, 5, 'routes not listed are counted');
assert.equal(moreRoutesLabel(0), '');
assert.equal(moreRoutesLabel(1), '+1 more route with the same number of jumps');
assert.equal(moreRoutesLabel(999), '+999 more routes with the same number of jumps');
assert.equal(moreRoutesLabel(8.1e15), '+over 1,000 more routes with the same number of jumps', 'huge counts are rounded off');
assert.deepEqual(routeLists({ jump: 3, routes: [], count: 0 }, lookup), { title: 'No Jump-3 route', lists: [], more: 0 });
assert.equal(routeLists({ jump: 2, routes: [[hex(0, 0), hex(1, 0)]], count: 1 }).title, 'Jump-2 route · 1 jump');

// Larger atlases stay quick: a 160 x 120 field with a system in every hex, Jump-6.
const field = [];
for (let column = 0; column < 160; column++) for (let row = 0; row < 120; row++) field.push(hex(column, row));
router = new JumpRouter(field);
const started = performance.now();
route = router.route(hex(0, 0), hex(158, 118), 6);
const elapsed = performance.now() - started;
assert.ok(route && legs(route).every(leg => leg <= 6));
assert.equal(route.length - 1, Math.ceil(hexDistance(hex(0, 0), hex(158, 118)) / 6), 'dense space needs the fewest possible jumps');
assert.ok(elapsed < 2000, `routing ${field.length} systems took ${elapsed.toFixed(0)} ms`);

// Dashes keep their rhythm across corners and never overrun a leg.
let dashes = dashedSegments([{ x: 0, y: 0 }, { x: 20, y: 0 }], 8, 6);
assert.deepEqual(dashes, [[{ x: 0, y: 0 }, { x: 8, y: 0 }], [{ x: 14, y: 0 }, { x: 20, y: 0 }]]);
dashes = dashedSegments([{ x: 0, y: 0 }, { x: 10, y: 0 }, { x: 10, y: 10 }], 8, 6);
assert.deepEqual(dashes, [[{ x: 0, y: 0 }, { x: 8, y: 0 }], [{ x: 10, y: 4 }, { x: 10, y: 10 }]], 'the gap carries round the corner');
// The offset moves the dashes forward, towards the last point, and wraps every dash and gap.
dashes = dashedSegments([{ x: 0, y: 0 }, { x: 20, y: 0 }], 8, 6, 3);
assert.deepEqual(dashes, [[{ x: 3, y: 0 }, { x: 11, y: 0 }], [{ x: 17, y: 0 }, { x: 20, y: 0 }]], 'dashes march towards the target');
assert.deepEqual(dashedSegments([{ x: 0, y: 0 }, { x: 20, y: 0 }], 8, 6, 17), dashes, 'the pattern repeats every 14 px');
assert.deepEqual(dashedSegments([{ x: 0, y: 0 }, { x: 20, y: 0 }], 8, 6, 10)[0], [{ x: 0, y: 0 }, { x: 4, y: 0 }], 'a dash can start part-way through');
assert.deepEqual(dashedSegments([{ x: 0, y: 0 }, { x: 20, y: 0 }], 8, 6, -11), dashes, 'negative offsets wrap too');
assert.deepEqual(dashedSegments([{ x: 3, y: 3 }]), []);
assert.deepEqual(dashedSegments([{ x: 3, y: 3 }, { x: 3, y: 3 }]), [], 'zero-length legs draw nothing');
console.log('Jump route planning, tie-breaks, detours, sector seams, caching, route options, plans, labels, stop-over lists, speed and dashes passed.');
