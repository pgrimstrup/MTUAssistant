import assert from 'node:assert/strict';
import { HEX_X, hexCenter, findHex, detailLevel, LabelLayout, parseHex, worldPriority, mapAppearance, baseSymbols, isSubsectorCapital, worldName, worldLabelRules } from '../../wwwroot/js/MapGeometry.js';
import { readFile } from 'node:fs/promises';
// Collocated browser modules use public /js URLs; map those to physical wwwroot files in Node.
let rendererSource = await readFile(new URL('./MapView.razor.js', import.meta.url), 'utf8');
for (const moduleName of ['WorldSelection.js', 'MapGeometry.js'])
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
    {remarks:'Cp', expected:[true,true,true,true,true]},
    {remarks:'Cx', expected:[true,false,true,true,true]},
    {remarks:'Cs', expected:[true,false,true,true,true]},
    {uwp:'C000900-0', expected:[false,false,true,true,true]},
    {uwp:'A000700-0', expected:[false,false,false,true,true]},
    {uwp:'B000700-0', expected:[false,false,false,true,true]},
    {uwp:'C000800-0', expected:[false,false,true,true,true]},
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
            assert.equal(markers.find(m => m.kind === 'zone').alpha,.35);
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
        : zoom>=55 ? ['Example',world.uwp] : zoom>=25 ? ['Example','C'] : ['Example']);
}
assert.equal(worldName({...world,uwp:'C000800-0'}),'EXAMPLE');
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
