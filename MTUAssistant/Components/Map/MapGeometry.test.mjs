import assert from 'node:assert/strict';
import { HEX_X, hexCenter, findHex, detailLevel, LabelLayout, parseHex, worldPriority, mapAppearance, baseSymbols, isSubsectorCapital, worldName, worldLabelRules, borderSegments, routePoints, insetBorderSegment } from '../../wwwroot/js/MapGeometry.js';
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

// Region navigation clears selection and uses the same animated pan without changing zoom.
focused.disposed=false;
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
await reloadMap.reloadData();
assert.equal(aborted,1);
assert.equal(reloadMap.sectorCache.size,0);
assert.equal(reloadMap.selected,null);
assert.ok(Math.abs(moved.x-(100+32*HEX_X))<1e-10);
assert.equal(moved.y,90);
assert.equal(resized,1);
assert.equal(redrawn,1);

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
