import assert from 'node:assert/strict';
import { HEX_X, hexCenter, findHex, detailLevel, LabelLayout, parseHex, worldPriority, mapAppearance, baseSymbols, isSubsectorCapital, worldName, worldLabelRules, borderSegments, routePoints, insetBorderSegment, globalHex, hexDistance, jumpLabel } from '../../wwwroot/js/MapGeometry.js';
import { readFile } from 'node:fs/promises';
// Collocated browser modules use public /js URLs; map those to physical wwwroot files in Node.
let rendererSource = await readFile(new URL('./MapView.razor.js', import.meta.url), 'utf8');
for (const moduleName of ['WorldSelection.js', 'MapGeometry.js', 'PresentationSync.js', 'CastWindow.js', 'HexSelection.js', 'MapDestination.js'])
    rendererSource = rendererSource.replaceAll(`../../js/${moduleName}`,
        new URL(`../../wwwroot/js/${moduleName}`, import.meta.url).href);
const { DynamicMap } = await import(`data:text/javascript;base64,${Buffer.from(rendererSource).toString('base64')}`);

assert.equal(detailLevel(1), 0);
for (const [zoom, expected] of [[9.99, 0], [10, 1], [24.99, 1], [25, 2], [54.99, 2], [55, 3], [94.99, 3], [95, 4], [192, 4]])
    assert.equal(detailLevel(zoom), expected);
assert.deepEqual(parseHex('3240'), { column: 32, row: 40 });
for (const value of ['0000', '3301', '0141', '01??', '101', '0001']) assert.equal(parseHex(value), null);
assert.deepEqual(hexCenter(0, 0), { x: 1 + .5 * HEX_X, y: 1.5 });
assert.deepEqual(hexCenter(1, 0), { x: 1 + 1.5 * HEX_X, y: 2 });
// Negative edges and cross-sector neighbours use the same coordinate transform as selection.
for (let column = -33; column <= 65; column++) for (let row = -41; row <= 81; row++) {
    const point = hexCenter(column, row), hex = findHex(point.x, point.y);
    assert.equal(hex.sectorColumn * 32 + hex.column - 1, column);
    assert.equal(hex.sectorRow * 40 + hex.row - 1, row);
}
const labels = new LabelLayout();
assert.equal(labels.place({x: 60, y: 60, w: 20, h: 20}), true);
assert.equal(labels.place({x: 79, y: 79, w: 30, h: 30}), false);
assert.equal(labels.place({x: 80, y: 80, w: 30, h: 30}), true);
labels.reserve({x: -10, y: -10, w: 8, h: 8});
assert.equal(labels.canPlace({x: -5, y: -5, w: 1, h: 1}), false);
assert.ok(worldPriority({remarks:'Cp', uwp:'A000100-0'}) > worldPriority({remarks:'Hi', uwp:'A000A00-0'}));
for (const property of ['worldDiameter', 'zoneDiameter', 'gasDiameter', 'baseSize']) {
    assert.ok(mapAppearance(112)[property] > mapAppearance(56)[property]);
    assert.ok(mapAppearance(192)[property] > mapAppearance(112)[property]);
}
assert.deepEqual(baseSymbols('NS'), [{kind:'base-naval', slot:'top'}, {kind:'base-scout', slot:'bottom'}]);
assert.equal(baseSymbols('NK S S').length, 2);
assert.deepEqual(baseSymbols('- ?'), []);
const relaxed = new LabelLayout();
relaxed.reserve({x:0, y:0, w:100, h:10});
assert.equal(relaxed.canPlace({x:90, y:0, w:100, h:10}), false);
assert.equal(relaxed.canPlace({x:90, y:0, w:100, h:10}, .24), true);
assert.equal(relaxed.canPlace({x:20, y:0, w:100, h:10}, .24), false);

const map = new DynamicMap({clientWidth:2400,clientHeight:2400}, {}, {});
const text = [], markers = [];
map.label = (value,x,y,size,colour) => text.push({value,x,y,size,colour});
map.marker = (kind,x,y,size,tint,zoom,alpha=1) => markers.push({kind,x,y,size,alpha});
const world = {name:'Example',hex:'0101',x:5,y:5,uwp:'C000700-0',remarks:'',bases:'NS',pbg:'703',zone:'R',priority:7};
const cases = [
    {remarks:'Cp', expected:[false,true,true,true,true]},
    {remarks:'Cx', expected:[false,false,true,true,true]},
    {remarks:'Cs', expected:[false,false,true,true,true]},
    {uwp:'C000900-0', expected:[false,false,true,true,true]},
    {uwp:'A000700-0', expected:[false,false,false,true,true]},
    {uwp:'B000700-0', expected:[false,false,false,true,true]},
    {uwp:'C000800-0', expected:[false,false,false,true,true]},
    {expected:[false,false,false,true,true]},
];
for (const zoom of [1,9.99,10,24.99,25,54.99,55,94.99,95,192]) {
    const level = detailLevel(zoom);
    for (const item of cases) {
        const w = {...world,...item};
        const rules = worldLabelRules(w,zoom);
        assert.equal(rules.name,item.expected[level]);
        text.length = markers.length = 0;
        map.drawWorld(w,level,zoom);
        map.drawWorldLabels(w,{x:0,y:0},level,zoom);
        assert.equal(text.some(t => t.value === worldName(w)),rules.name);
        assert.equal(text.some(t => t.value === w.uwp),zoom >= 55);
        assert.equal(text.some(t => t.value === w.hex),zoom >= 95);
        assert.equal(text.some(t => t.value === w.uwp[0]),zoom >= 25 && zoom < 55);
        assert.equal(markers.some(m => m.kind === 'gas'),zoom >= 25);
        assert.equal(markers.some(m => m.kind === 'base-naval'),zoom >= 25);
        if (zoom >= 55) {
            const gas = markers.find(m => m.kind === 'gas');
            const naval = markers.find(m => m.kind === 'base-naval');
            assert.equal(gas.y,naval.y);
            assert.ok(Math.abs(gas.x-w.x + naval.x-w.x) < 1e-10);
            assert.equal(gas.size,mapAppearance(zoom).gasDiameter * .8);
            assert.equal(markers.find(m => m.kind === 'zone').alpha,1);
            const name = text.find(t => t.value === worldName(w));
            assert.ok(Math.abs(name.y + (name.size+4)/zoom - (w.y+.5)) < 1e-10);
        }
    }
    // Region boundaries always draw; hex grid begins at 10.
    let hexes=0, boundaries=0;
    map.grid={clear(){},stroke(){}};
    map.boundaries={clear(){},rect(){},stroke(){},moveTo(){boundaries++;return this;},lineTo(){return this;}};
    map.hexPath=()=>hexes++;
    map.overview={columns:1,rows:1};
    map.drawGrid({x:1,y:1,w:5,h:5},[{col:0,row:0,rect:{x:1,y:1,w:32*HEX_X,h:40}}],level,zoom);
    assert.equal(boundaries,6);
    assert.equal(hexes>0,zoom>=10);
}
// Visibility depends only on the zoom band, regardless of zoom direction or history.
for (const zoom of [60,30,8,96,40,60]) {
    text.length=0;
    map.drawWorldLabels({...world,remarks:'Cp'},{x:.1,y:.2},detailLevel(zoom),zoom);
    assert.deepEqual(text.map(t=>t.value),zoom>=95 ? ['Example','0101',world.uwp]
        : zoom>=55 ? ['Example',world.uwp] : zoom>=25 ? ['Example','C'] : zoom>=10 ? ['Example'] : []);
}
assert.equal(worldName({...world,uwp:'C000800-0'}),'Example');
assert.equal(worldName({...world,uwp:'C000900-0'}),'EXAMPLE');
console.log('Fixed map detail bands, world classifications, symbols, boundaries and history-independent labels passed.');

// Inspect raster instructions and convert texture units back to CSS pixels.
const savedDocument = globalThis.document, savedPixi = globalThis.PIXI;
try {
    globalThis.document = {createElement() {
        const ctx = {scale(){},beginPath(){},arc(x,y,r){this.radius=r;},stroke(){}};
        return {ctx,getContext(){return ctx;}};
    }};
    globalThis.PIXI = {Texture:{from:canvas=>canvas}};
    map.textures = new Map();
    for (const zoom of [25,30,40,54.99,55,75,94.99,95,112,192]) {
        const diameter = mapAppearance(zoom).zoneDiameter;
        const canvas = map.texture('zone',diameter);
        assert.ok(canvas.ctx.lineWidth * diameter / 32 >= 2);
        assert.ok(canvas.ctx.radius + canvas.ctx.lineWidth / 2 < 16, 'stroke stays inside texture');
    }
    assert.equal(map.texture('zone').ctx.lineWidth,1.2);
} finally {
    globalThis.document = savedDocument;
    globalThis.PIXI = savedPixi;
}

// Search navigation uses sector identity and hex geometry without waiting for detail fetches.
const focused = new DynamicMap({}, {}, {});
let centre, refreshed=0, saved=0;
focused.sectors=[{id:17,col:0,row:0},{id:22,col:3,row:2}];
focused.worldW=200; focused.worldH=200;
focused.viewport={worldScreenWidth:40,worldScreenHeight:30,scale:{x:32},
    plugins:{remove(name){assert.equal(name,'animate');},get(){return {reset(){}};}},
    clamp(bounds){assert.ok(bounds.left < 0 && bounds.top < 0);},
    animate(options){
        assert.equal(options.time,650);
        assert.equal(options.ease,'easeInOutSine');
        assert.equal(options.scale,undefined,'pan does not animate zoom');
        centre=options.position;
        options.callbackOnComplete();
    },
    moveCenter(x,y){centre={x,y};}};
focused.update=()=>refreshed++;
focused.saveView=()=>saved++;
for (const [id,col,row,hex] of [[17,0,0,'0101'],[22,127,119,'3240']]) {
    assert.equal(focused.focusWorld(id,hex),true);
    assert.deepEqual(centre,hexCenter(col,row));
    assert.deepEqual(focused.selected,{...centre,key:id+'/'+hex});
    assert.equal(focused.viewport.scale.x,32,'search preserves zoom');
}
assert.equal(refreshed,2); assert.equal(saved,2);
assert.equal(focused.focusWorld(999,'0101'),false);
assert.equal(focused.focusWorld(17,'0000'),false);
assert.equal(refreshed,2);
assert.equal(focused.focusWorld(17,'0101'),true,'same world can be selected again');
focused.disposed=true;
assert.equal(focused.focusWorld(17,'0101'),false);

// Restoring a selection must not move or zoom the saved viewport.
focused.disposed=false;
const previousCentre = centre;
assert.equal(focused.restoreSelection(22, '3240'), true);
assert.deepEqual(focused.selected, { ...hexCenter(127,119), key:'22/3240' });
assert.equal(centre, previousCentre);
assert.equal(focused.viewport.scale.x, 32);
assert.equal(focused.restoreSelection(999, '0101'), false);
assert.equal(focused.restoreSelection(17, '0000'), false);
assert.equal(focused.selected.key, '22/3240');

// The selected world survives a new map instance and uses a separate browser storage key.
const storedMapState = new Map([['mtu.map.view', '{"x":10,"y":20,"zoom":32}']]);
const originalStorage = Object.getOwnPropertyDescriptor(globalThis, 'localStorage');
Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: {
    getItem: key => storedMapState.get(key) ?? null,
    setItem: (key, value) => storedMapState.set(key, value),
    removeItem: key => storedMapState.delete(key)
}});
try {
    focused.saveWorldSelection(22, '3240');
    const reopened = new DynamicMap({}, {}, {});
    assert.deepEqual(reopened.loadWorldSelection(), { sectorId: 22, hex: '3240' });

    const cast = new DynamicMap({}, {}, {}, { readOnly: true });
    assert.equal(cast.loadWorldSelection(), null);
    cast.saveWorldSelection(null, null);
    assert.deepEqual(reopened.loadWorldSelection(), { sectorId: 22, hex: '3240' });

    reopened.saveWorldSelection(null, null);
    assert.equal(reopened.loadWorldSelection(), null);
    assert.equal(storedMapState.get('mtu.map.view'), '{"x":10,"y":20,"zoom":32}');

    storedMapState.set('mtu.map.selection', '{');
    assert.equal(reopened.loadWorldSelection(), null);
    storedMapState.set('mtu.map.selection', '{"sectorId":22,"hex":"9940"}');
    assert.equal(reopened.loadWorldSelection(), null);
} finally {
    if (originalStorage) Object.defineProperty(globalThis, 'localStorage', originalStorage);
    else delete globalThis.localStorage;
}

// Region navigation clears selection and uses the same animated pan without changing zoom.
for (const [id,col,row] of [[17,0,0],[22,3,2]]) {
    focused.selected={key:'old world'};
    assert.equal(focused.focusRegion(id),true);
    assert.ok(Math.abs(centre.x-(1+(col+.5)*32*HEX_X))<1e-10);
    assert.equal(centre.y,1+(row+.5)*40);
    assert.equal(focused.selected,null);
    for (let index=0;index<16;index++) {
        focused.selected={key:'old world'};
        assert.equal(focused.focusRegion(id,String.fromCharCode(65+index)),true);
        assert.ok(Math.abs(centre.x-(1+col*32*HEX_X+(index%4+.5)*32*HEX_X/4))<1e-10);
        assert.equal(centre.y,1+row*40+(Math.floor(index/4)+.5)*10);
        assert.equal(focused.selected,null);
        assert.equal(focused.viewport.scale.x,32);
    }
}
focused.selected={key:'keep'};
assert.equal(focused.focusRegion(999),false);
assert.equal(focused.focusRegion(17,'Q'),false);
assert.equal(focused.focusRegion(17,''),false);
assert.deepEqual(focused.selected,{key:'keep'});

// A catalogue reload drops stale sector requests and preserves the same galactic centre.
const reloadMap = new DynamicMap({}, {}, {});
let aborted=0, moved, resized=0, redrawn=0;
reloadMap.overview={minSectorX:-4,minSectorY:-1};
reloadMap.sectorCache.set(3,{controller:{abort(){aborted++;}}});
reloadMap.viewport={center:{x:100,y:50},moveCenter(x,y){moved={x,y};}};
reloadMap.loadOverview=async()=>{reloadMap.overview={minSectorX:-5,minSectorY:-2};};
reloadMap.onResize=()=>resized++;
reloadMap.update=()=>redrawn++;
reloadMap.selected={key:'old'};
let destinationRestores=0;
reloadMap.restoreDestination=()=>destinationRestores++;
await reloadMap.reloadData();
assert.equal(aborted,1);
assert.equal(reloadMap.sectorCache.size,0);
assert.equal(reloadMap.selected,null);
assert.ok(Math.abs(moved.x-(100+32*HEX_X))<1e-10);
assert.equal(moved.y,90);
assert.equal(resized,1);
assert.equal(redrawn,1);
assert.equal(destinationRestores,1,'a catalogue reload re-resolves the saved jump destination');

const ring = borderSegments('0101',0,0);
assert.equal(ring.length,6);
for (const [a,b] of ring) assert.ok(Math.hypot(a.x-b.x,a.y-b.y)>0);
assert.equal(borderSegments('0000',0,0).length,0);
assert.deepEqual(routePoints({start:'3209',end:'0208',endOffsetX:1},0,0), [hexCenter(31,8),hexCenter(33,7)]);
assert.deepEqual(routePoints({start:'3201',end:'0103',startOffsetX:-1},1,0), [hexCenter(31,0),hexCenter(32,2)]);
assert.equal(routePoints({start:'oops',end:'0101'},0,0),null);

// Opposing borders on the same edge inset toward their respective hex interiors.
for (const zoom of [10,55,95,192]) {
    const a={x:1,y:1}, b={x:2,y:1};
    const [one] = insetBorderSegment(a,b,zoom);
    const [two] = insetBorderSegment(b,a,zoom);
    assert.ok(one.y > 1 && two.y < 1);
    assert.ok(Math.abs((one.y-1)*zoom-2)<1e-9);
    assert.ok(Math.abs((1-two.y)*zoom-2)<1e-9);
}
const centreOfHex=hexCenter(0,0);
for(const [a,b] of borderSegments('0101',0,0)) {
    const [ia,ib]=insetBorderSegment(a,b,95);
    const midpoint={x:(a.x+b.x)/2,y:(a.y+b.y)/2};
    const inner={x:(ia.x+ib.x)/2,y:(ia.y+ib.y)/2};
    assert.ok(Math.hypot(inner.x-centreOfHex.x,inner.y-centreOfHex.y)<Math.hypot(midpoint.x-centreOfHex.x,midpoint.y-centreOfHex.y));
}

// Pointer location follows hex geography and sends only changed sector/subsector pairs.
const locationCalls=[];
const locationMap=new DynamicMap({clientWidth:100,clientHeight:100}, {}, {invokeMethodAsync(...args){locationCalls.push(args);return Promise.resolve();}});
locationMap.app={canvas:{getBoundingClientRect(){return {left:0,top:0,width:100,height:100};}}};
let mapPoint=hexCenter(0,0);
locationMap.viewport={toWorld(){return mapPoint;}};
locationMap.sectors=[{col:0,row:0,name:'Spinward Marches',subsectors:[{letter:'A',name:'Cronor'},{letter:'B',name:'Jewell'}]}];
locationMap.pointerPosition={x:50,y:50};
locationMap.updatePointerLocation();
locationMap.updatePointerLocation();
assert.deepEqual(locationCalls,[['SetPointerLocation','Spinward Marches','Cronor']]);
mapPoint=hexCenter(8,0);
locationMap.updatePointerLocation();
assert.deepEqual(locationCalls.at(-1),['SetPointerLocation','Spinward Marches','Jewell']);
mapPoint=hexCenter(40,0);
locationMap.updatePointerLocation();
assert.deepEqual(locationCalls.at(-1),['SetPointerLocation',null,null]);
locationMap.pointerPosition=null;
locationMap.updatePointerLocation();
assert.equal(locationCalls.length,3);

// Presentation camera coordinates stay stable across catalogue origins and window sizes.
const presentationHost = { clientWidth: 1600, clientHeight: 900 };
const presentationCalls = [];
const presentationMap = new DynamicMap(presentationHost, {}, { invokeMethodAsync(...args) { presentationCalls.push(args); return Promise.resolve(); } }, { readOnly: true });
presentationMap.overview = { minSectorX: -4, minSectorY: -2 };
presentationMap.worldW = 1000; presentationMap.worldH = 1000;
let camera = {}, draws = 0, published = 0;
presentationMap.viewport = {
    setZoom(zoom) { camera.zoom = zoom; },
    moveCenter(x, y) { camera.x = x; camera.y = y; },
    resize() { camera = {}; },
    clampZoom() { throw Error('Presentation must preserve Admin zoom'); }
};
presentationMap.update = () => draws++;
presentationMap.presentation = { publish() { published++; } };
const snapshot = { version: 1, x: 12, y: 34, zoom: 2, selected: { x: 10, y: 35, key: '42/0101' } };
presentationMap.applyPresentation(snapshot);
assert.deepEqual(camera, { x: 12 + 4 * 32 * HEX_X, y: 114, zoom: 2 });
assert.deepEqual(presentationMap.selected, { x: 10 + 4 * 32 * HEX_X, y: 115, key: '42/0101' });
presentationHost.clientWidth = 800; presentationHost.clientHeight = 600;
presentationMap.onResize();
assert.deepEqual(camera, { x: 12 + 4 * 32 * HEX_X, y: 114, zoom: 2 }, 'resize retains the same centre and zoom');
presentationMap.saveView();
assert.equal(published, 0, 'Presentation never publishes or overwrites the saved Admin viewport');
await presentationMap.selectWorld({});
presentationMap.pointerPosition = { x: 10, y: 10 };
presentationMap.updatePointerLocation();
assert.equal(draws, 2);
assert.equal(presentationCalls.length, 1, 'camera resizing and user input do not reload world details');
assert.equal(presentationCalls[0][0], 'SelectHex');
presentationMap.applyPresentation({ ...snapshot, selected: null });
assert.equal(presentationMap.selected, null, 'clearing a selection removes its highlight');
assert.deepEqual(presentationCalls[1], ['ClearPresentationSelection'], 'deselection clears world details');
console.log('Presentation camera origins, resize, deselection and read-only rendering checks passed.');

// Pointer transmission uses map coordinates, independent of canvas size or device pixel ratio.
const pointerPackets = [];
const pointerSource = new DynamicMap({ clientWidth: 800, clientHeight: 400 }, {}, {});
pointerSource.overview = { minSectorX: -2, minSectorY: 3 };
pointerSource.app = { canvas: { getBoundingClientRect: () => ({ left: 100, top: 50, width: 400, height: 200 }) } };
pointerSource.viewport = { toWorld: (x, y) => ({ x: x / 4 + 10, y: y / 4 + 20 }) };
pointerSource.presentation = { publishPointer: point => pointerPackets.push(point) };
pointerSource.pointerPosition = { x: 200, y: 100 };
pointerSource.publishPresentationPointer();
assert.deepEqual(pointerPackets[0], { x: 60 - 2 * 32 * HEX_X, y: 165 });
pointerSource.publishPresentationPointer();
assert.equal(pointerPackets.length, 1, 'unchanged coordinates do not send duplicate updates');
pointerSource.pointerPosition = null;
pointerSource.publishPresentationPointer();
assert.equal(pointerPackets[1], null);
pointerSource.pointerPosition = { x: 90, y: 100 };
pointerSource.publishPresentationPointer();
assert.equal(pointerPackets.length, 2, 'outside-canvas pointer remains hidden');
const marker = { hidden: true, style: {} };
presentationMap.presentationPointer = marker;
presentationMap.viewport = { left: 0, top: 0, scale: { x: 2, y: 2 } };
presentationMap.presentedPointer = { x: 100 - 4 * 32 * HEX_X, y: 150 - 2 * 40 };
presentationMap.drawPresentationPointer();
assert.equal(marker.hidden, false);
assert.equal(marker.style.left, '200px');
assert.equal(marker.style.top, '300px');
presentationMap.viewport.scale = { x: 3, y: 3 };
presentationMap.drawPresentationPointer();
assert.equal(marker.style.left, '300px', 'pointer follows zoom in the same map location');
assert.equal(marker.style.top, '450px');
presentationMap.presentedPointer = { x: 10000, y: 10000 };
presentationMap.drawPresentationPointer();
assert.equal(marker.hidden, true, 'offscreen pointer is hidden');
presentationMap.presentedPointer = null;
presentationMap.drawPresentationPointer();
assert.equal(marker.hidden, true, 'leaving the Admin map clears the marker');
console.log('Presentation pointer projection, zoom, deduplication and visibility checks passed.');

// Jump distance: whole hexes, checked against a breadth-first walk across sector seams.
const adjacent = (column, row) => {
    const centre = hexCenter(column, row), result = [];
    for (let dc = -1; dc <= 1; dc++) for (let dr = -1; dr <= 1; dr++) {
        if (!dc && !dr) continue;
        const other = hexCenter(column + dc, row + dr);
        if (Math.abs(Math.hypot(other.x - centre.x, other.y - centre.y) - 1) < 1e-9) result.push({ column: column + dc, row: row + dr });
    }
    return result;
};
for (const origin of [{ column: 30, row: 38 }, { column: 31, row: 39 }, { column: 32, row: 40 }]) {
    assert.equal(adjacent(origin.column, origin.row).length, 6, 'every hex has six neighbours one parsec away');
    const steps = new Map([[`${origin.column},${origin.row}`, 0]]), queue = [origin];
    while (queue.length) {
        const hex = queue.shift();
        for (const next of adjacent(hex.column, hex.row)) {
            const key = `${next.column},${next.row}`;
            if (next.column < 20 || next.column > 44 || next.row < 28 || next.row > 52 || steps.has(key)) continue;
            steps.set(key, steps.get(`${hex.column},${hex.row}`) + 1);
            queue.push(next);
        }
    }
    for (const [key, distance] of steps) {
        const [column, row] = key.split(',').map(Number);
        assert.equal(hexDistance(origin, { column, row }), distance, `jump distance ${origin.column},${origin.row} to ${key}`);
        assert.equal(hexDistance({ column, row }, origin), distance, 'jump distance is symmetric');
    }
}
assert.equal(hexDistance({ column: 0, row: 0 }, { column: 0, row: 0 }), 0);
assert.equal(hexDistance({ column: 0, row: 0 }, { column: 0, row: 6 }), 6, 'straight down a column');
assert.equal(hexDistance({ column: 0, row: 0 }, { column: 6, row: 3 }), 6, 'diagonal along alternating columns');
for (const [column, row] of [[0, 0], [31, 39], [32, 40], [127, 119]]) {
    const centre = hexCenter(column, row);
    assert.deepEqual(globalHex(centre.x, centre.y), { column, row }, 'global hex round-trips through hex centres');
}
assert.deepEqual([0, 1, 2, 6].map(jumpLabel), ['0 parsecs', '1 parsec', '2 parsecs', '6 parsecs']);
console.log('Jump distance, global hexes and parsec labels passed.');

// Right-drag measures between snapped hex centres; releasing sets a persisted jump destination.
const jumpStorage = new Map();
const savedStorage = Object.getOwnPropertyDescriptor(globalThis, 'localStorage');
Object.defineProperty(globalThis, 'localStorage', { configurable: true, value: {
    getItem: key => jumpStorage.get(key) ?? null,
    setItem: (key, value) => jumpStorage.set(key, value),
    removeItem: key => jumpStorage.delete(key)
}});
const recorder = () => {
    const g = { ops: [] };
    for (const name of ['clear', 'moveTo', 'lineTo', 'stroke', 'circle', 'fill', 'rect', 'beginPath', 'poly'])
        g[name] = (...args) => { g.ops.push([name, ...args]); return g; };
    return g;
};
const jumpMap = (options = {}) => {
    const map = new DynamicMap({ clientWidth: 100, clientHeight: 100 }, {}, {}, options);
    const captured = new Set();
    map.overview = { columns: 2, rows: 1, minSectorX: -1, minSectorY: 0 };
    map.sectors = [{ id: 17, col: 0, row: 0 }, { id: 22, col: 1, row: 0 }];
    map.app = { canvas: { getBoundingClientRect: () => ({ left: 0, top: 0, width: 100, height: 100 }),
        setPointerCapture: id => captured.add(id), hasPointerCapture: id => captured.has(id), releasePointerCapture: id => captured.delete(id) } };
    map.captured = captured;
    map.pointerAt = hexCenter(0, 0);
    map.viewport = { center: { x: 0, y: 0 }, scale: { x: 10 }, toWorld: () => map.pointerAt, toScreen: point => ({ x: point.x * 10, y: point.y * 10 }) };
    map.measureGraphics = recorder(); map.measureLabelBackground = recorder();
    map.measureLabel = { visible: false, position: { set(x, y) { map.labelAt = { x, y }; } } };
    map.measureLabelText = { text: '', width: 60, height: 16, position: { set() {} } };
    map.selection = recorder(); map.destinationMarker = recorder();
    map.snapshots = [];
    map.presentation = { publish: state => map.snapshots.push(state) };
    return map;
};
const pointer = (map, type, column, row, button = 2) => {
    map.pointerAt = hexCenter(column, row);
    let prevented = false;
    const event = { type, button, pointerId: 7, clientX: 0, clientY: 0, preventDefault: () => { prevented = true; } };
    if (type === 'pointerdown') map.beginMeasure(event);
    else if (type === 'pointermove') { if (map.measure?.id === event.pointerId) map.updateMeasure(event); }
    else map.endMeasure(event);
    return prevented;
};
try {
    const gm = jumpMap();
    assert.equal(pointer(gm, 'pointerdown', 3, 4, 0), false, 'left button never measures');
    assert.equal(gm.measure, undefined);
    assert.equal(pointer(gm, 'pointerdown', 3, 4), true, 'right button starts a measurement and suppresses the menu');
    assert.deepEqual(gm.measure, { id: 7, origin: { column: 3, row: 4 }, target: { column: 3, row: 4 } });
    assert.ok(gm.captured.has(7), 'the measuring pointer is captured');
    assert.equal(gm.measureLabelText.text, '0 parsecs');
    assert.equal(gm.measureLabel.visible, true);
    const published = gm.snapshots.length;
    pointer(gm, 'pointermove', 3, 4);
    assert.equal(gm.snapshots.length, published, 'moving within a hex sends no update');
    pointer(gm, 'pointermove', 9, 4);
    assert.equal(gm.measureLabelText.text, '6 parsecs', 'the label shows whole parsecs');
    const strokes = gm.measureGraphics.ops.filter(op => op[0] === 'stroke');
    assert.deepEqual(strokes.at(-1)[1], { color: 0xffffff, width: 2 }, 'Battlemap measurement style: 2 px white line');
    assert.deepEqual(gm.measureGraphics.ops.filter(op => op[0] === 'circle').at(-1).slice(3), [4], 'origin dot');
    const from = hexCenter(3, 4), to = hexCenter(9, 4);
    assert.ok(Math.abs(gm.labelAt.x - ((from.x + to.x) * 5 - 35)) < 1e-9 && Math.abs(gm.labelAt.y - ((from.y + to.y) * 5 - 11)) < 1e-9,
        'the label is centred halfway along the line');
    let snapshot = gm.snapshots.at(-1);
    assert.deepEqual(snapshot.measure, { from: { x: from.x - 32 * HEX_X, y: from.y }, to: { x: to.x - 32 * HEX_X, y: to.y } },
        'the live measurement is cast as absolute hex centres');
    pointer(gm, 'pointerup', 33, 4);
    assert.equal(gm.measure, null); assert.equal(gm.measureLabel.visible, false);
    assert.ok(!gm.captured.has(7), 'release frees the pointer');
    assert.deepEqual(gm.destination, { ...hexCenter(33, 4), key: '22/0205' }, 'release snaps the destination to the hex under the pointer');
    assert.equal(jumpStorage.get('mtu.map.destination'), '{"sectorId":22,"hex":"0205"}');
    snapshot = gm.snapshots.at(-1);
    assert.equal(snapshot.measure, null, 'the cast measurement clears on release');
    assert.deepEqual(snapshot.destination, { x: hexCenter(33, 4).x - 32 * HEX_X, y: hexCenter(33, 4).y, key: '22/0205' });
    assert.deepEqual(Object.keys(snapshot).sort(), ['destination', 'measure', 'selected', 'version', 'x', 'y', 'zoom'].sort(),
        'the snapshot carries only camera, selection, destination and measurement');
    assert.ok(gm.destinationMarker.ops.some(op => op[0] === 'stroke' && op[1].color === 0xffa500), 'the destination marker is drawn in orange');

    // A cancelled drag keeps the previous destination; a right-click on the destination clears it.
    pointer(gm, 'pointerdown', 1, 1); pointer(gm, 'pointercancel', 2, 1);
    assert.equal(gm.destination.key, '22/0205');
    pointer(gm, 'pointerdown', 4, 4); pointer(gm, 'pointerup', 4, 4);
    assert.equal(gm.destination.key, '17/0505', 'a right-click elsewhere moves the destination');
    pointer(gm, 'pointerdown', 4, 4); pointer(gm, 'pointerup', 4, 4);
    assert.equal(gm.destination, null, 'a right-click on the destination clears it');
    assert.equal(jumpStorage.has('mtu.map.destination'), false);
    pointer(gm, 'pointerdown', 4, 4); pointer(gm, 'pointerup', 0, 0, 0);
    assert.equal(gm.destination, null, 'releasing another button does not choose a destination');
    assert.equal(gm.measure, null);

    // Dragging off the atlas stops at its edge; a hex without a charted sector is not a destination.
    pointer(gm, 'pointerdown', 4, 4); pointer(gm, 'pointermove', 90, 70);
    assert.deepEqual(gm.measure.target, { column: 63, row: 39 }, 'measurement clamps to the atlas');
    gm.sectors = gm.sectors.filter(sector => sector.id !== 22);
    pointer(gm, 'pointerup', 90, 70);
    assert.equal(gm.destination, null);
    gm.measure = { id: 3, origin: { column: 0, row: 0 }, target: { column: 1, row: 1 } };
    gm.cancelMeasure();
    assert.equal(gm.measure, null, 'leaving the window cancels a measurement');

    // The saved destination is restored by sector ID and hex on a new map instance.
    jumpStorage.set('mtu.map.destination', '{"sectorId":22,"hex":"0205"}');
    const reopenedJump = jumpMap();
    reopenedJump.restoreDestination();
    assert.deepEqual(reopenedJump.destination, { ...hexCenter(33, 4), key: '22/0205' });

    // Selecting the destination hex clears the destination; restoring a saved selection does not.
    reopenedJump.update = () => {};
    reopenedJump.callback = { invokeMethodAsync: () => Promise.resolve() };
    assert.equal(reopenedJump.restoreSelection(22, '0205'), true);
    assert.equal(reopenedJump.destination?.key, '22/0205', 'restoring the selection on load keeps the destination');
    reopenedJump.pointerAt = hexCenter(32, 4);
    await reopenedJump.selectWorld({ clientX: 0, clientY: 0 });
    assert.equal(reopenedJump.destination?.key, '22/0205', 'selecting another hex keeps the destination');
    reopenedJump.pointerAt = hexCenter(33, 4);
    await reopenedJump.selectWorld({ clientX: 0, clientY: 0 });
    assert.equal(reopenedJump.destination, null, 'clicking the destination hex clears it');
    assert.equal(jumpStorage.has('mtu.map.destination'), false, 'the cleared destination is removed from storage');
    assert.equal(reopenedJump.snapshots.at(-1).destination, null, 'the presentation stops showing the destination');
    jumpStorage.set('mtu.map.destination', '{"sectorId":22,"hex":"0205"}');
    reopenedJump.restoreDestination();
    reopenedJump.panToPoint = () => true;
    assert.equal(reopenedJump.focusWorld(17, '0101'), true);
    assert.equal(reopenedJump.destination?.key, '22/0205', 'searching for another world keeps the destination');
    assert.equal(reopenedJump.focusWorld(22, '0205'), true);
    assert.equal(reopenedJump.destination, null, 'selecting the destination from search clears it');
    assert.equal(jumpStorage.has('mtu.map.destination'), false);

    for (const bad of ['{', '{"sectorId":99,"hex":"0205"}', '{"sectorId":22,"hex":"9999"}', 'null']) {
        jumpStorage.set('mtu.map.destination', bad);
        reopenedJump.restoreDestination();
        assert.equal(reopenedJump.destination, null, `invalid saved destination ${bad}`);
    }

    // The presentation mirrors the destination and measurement but never measures or reads storage.
    const cast = jumpMap({ readOnly: true });
    cast.viewport = { ...cast.viewport, setZoom() {}, moveCenter() {} };
    cast.update = () => cast.drawSelection(10);
    cast.updatePresentationSelection = () => {};
    jumpStorage.set('mtu.map.destination', '{"sectorId":17,"hex":"0101"}');
    cast.restoreDestination();
    assert.equal(cast.destination, undefined, 'the presentation never reads the GM destination from storage');
    cast.applyPresentation({ version: 1, x: 0, y: 0, zoom: 10, selected: null, destination: snapshot.destination,
        measure: { from: { x: from.x - 32 * HEX_X, y: from.y }, to: { x: to.x - 32 * HEX_X, y: to.y } } });
    assert.deepEqual(cast.destination, { ...hexCenter(33, 4), key: '22/0205' });
    assert.deepEqual(cast.measure, { origin: { column: 3, row: 4 }, target: { column: 9, row: 4 } });
    assert.equal(cast.measureLabelText.text, '6 parsecs');
    cast.cancelMeasure();
    assert.ok(cast.measure, 'pointer leave in the presentation keeps the mirrored measurement');
    assert.equal(pointer(cast, 'pointerdown', 1, 1), false, 'the presentation cannot measure');
    cast.applyPresentation({ version: 1, x: 0, y: 0, zoom: 10, selected: null });
    assert.equal(cast.destination, null); assert.equal(cast.measure, null);
    assert.equal(cast.measureLabel.visible, false, 'older snapshots without these fields clear them');
} finally {
    if (savedStorage) Object.defineProperty(globalThis, 'localStorage', savedStorage);
    else delete globalThis.localStorage;
}
console.log('Jump measurement, destination persistence, restore and presentation checks passed.');
