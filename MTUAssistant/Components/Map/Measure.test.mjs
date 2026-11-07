import assert from 'node:assert/strict';
import {measureGeometry,measureLabel,measureCell} from '../../wwwroot/js/MovementIndicators.js';
import {Battlemap} from '../../wwwroot/js/Battlemap.js';
import {playerMeasure,validBattlemapSnapshot} from '../../wwwroot/js/BattlemapPresentationSync.js';

const view={x:10,y:20,scale:2};
let geometry=measureGeometry({column:1,row:2},{column:4,row:2},100,view);
assert.deepEqual(geometry.start,{x:310,y:520},'origin is the centre of the square in screen pixels');
assert.deepEqual(geometry.end,{x:910,y:520},'the end snaps to the centre of the target square');
assert.deepEqual(geometry.midpoint,{x:610,y:520},'label sits halfway along the line');
assert.equal(geometry.distance,4.5,'three squares is 4.5 m');
geometry=measureGeometry({column:0,row:0},{column:3,row:4},100,{x:0,y:0,scale:1});
assert.equal(geometry.distance,7.5,'diagonals use straight-line distance between centres');
assert.equal(measureLabel(4.5),'4.5 m');assert.equal(measureLabel(0),'0.0 m');assert.equal(measureLabel(2.1213),'2.1 m');
assert.deepEqual(measureCell({x:199,y:250},100,500,400),{column:1,row:2});
assert.deepEqual(measureCell({x:900,y:-50},100,500,400),{column:4,row:0},'pointers off the map clamp to the edge square');
assert.deepEqual(measureCell({x:480,y:399},100,450,380),{column:4,row:3},'partial edge squares count');

let captured=null,released=null;
const host={style:{},getBoundingClientRect:()=>({left:0,top:0}),clientLeft:0,clientTop:0,
    setPointerCapture:id=>captured=id,hasPointerCapture:id=>captured===id,releasePointerCapture:id=>{released=id;captured=null;}};
const scene={id:1,campaignId:1,revision:1,gridSize:100,gridColour:'#000000',gridlineWidth:2,gridOpacity:50};
const map=new Battlemap(host,scene,'measure');
map.view={x:0,y:0,scale:1};map.width=500;map.height=400;map.imageWidth=500;map.imageHeight=400;
map.characterTokens=new Map();map.movementTrails=new Map();map.movementRanges=[];map.revealedFog=null;
let published=[];map.presentation={publish:state=>published.push(state)};
let prevented=0;const event=(x,y,id=7)=>({pointerId:id,clientX:x,clientY:y,preventDefault:()=>prevented++});
assert.equal(map.beginMeasure(event(600,50)),false,'measuring must start on the map image');assert.equal(map.measure ?? null,null);
assert.equal(map.beginMeasure(event(130,260)),true);
assert.deepEqual(map.measure.origin,{column:1,row:2},'origin snaps to the clicked square');
assert.equal(captured,7);assert.equal(prevented,1);
assert.deepEqual(published.at(-1).measure,{fromColumn:1,fromRow:2,toColumn:1,toRow:2},'players see the measurement start');
map.updateMeasure(event(450,250));assert.equal(map.measureGeometry().distance,4.5);
assert.deepEqual(published.at(-1).measure,{fromColumn:1,fromRow:2,toColumn:4,toRow:2});assert.ok(validBattlemapSnapshot(published.at(-1)));
const count=published.length;map.updateMeasure(event(420,210));assert.equal(published.length,count,'moving within the same square publishes nothing');
map.updateMeasure(event(900,250));assert.equal(map.measureGeometry().end.x,450,'the line stops at the edge square');
map.cancelMeasure();assert.equal(map.measure,null,'releasing the button removes the measurement');assert.equal(released,7);
assert.equal(map.measureGeometry(),null);assert.equal(published.at(-1).measure,null,'players see the measurement removed');
map.placement=5;assert.equal(map.beginMeasure(event(130,260)),false,'no measuring while placing a token');map.placement=null;

// Fog privacy: both ends must be on revealed squares.
const revealed=new Set(['1,2','2,2']);
assert.deepEqual(playerMeasure({origin:{column:1,row:2},target:{column:2,row:2}},revealed),{fromColumn:1,fromRow:2,toColumn:2,toRow:2});
assert.equal(playerMeasure({origin:{column:1,row:2},target:{column:3,row:2}},revealed),null,'measurements into fog stay GM-only');
assert.equal(playerMeasure({origin:{column:3,row:2},target:{column:1,row:2}},revealed),null,'measurements from fog stay GM-only');
assert.equal(playerMeasure(null,revealed),null);
map.revealedFog=revealed;map.beginMeasure(event(130,260));map.updateMeasure(event(350,250));
assert.equal(published.at(-1).measure,null,'the GM still measures into fog, but players are not told');
assert.ok(map.measureGeometry(),'the GM still sees the line');map.cancelMeasure();
const snapshot={...published.at(-1),fog:[{column:1,row:2},{column:2,row:2}]};
assert.ok(validBattlemapSnapshot({...snapshot,measure:{fromColumn:1,fromRow:2,toColumn:2,toRow:2}}));
assert.equal(validBattlemapSnapshot({...snapshot,measure:{fromColumn:1,fromRow:2,toColumn:3,toRow:2}}),false,'snapshots measuring into fog are rejected');
assert.equal(validBattlemapSnapshot({...snapshot,measure:{fromColumn:1,fromRow:2,toColumn:9,toRow:2}}),false,'off-map measurements are rejected');
assert.equal(validBattlemapSnapshot({...snapshot,measure:{fromColumn:1.5,fromRow:2,toColumn:2,toRow:2}}),false);

// The read-only presentation draws what it is sent and never starts its own measurement.
const presented=new Battlemap({...host},scene,'presented');presented.readOnly=true;presented.view={x:0,y:0,scale:1};
assert.equal(presented.beginMeasure(event(130,260)),false,'read-only presentation maps never measure');
presented.setMeasure({fromColumn:0,fromRow:0,toColumn:3,toRow:4});assert.equal(presented.measureGeometry().distance,7.5);
presented.setMeasure(null);assert.equal(presented.measureGeometry(),null);
console.log('Measure snapping, distance, label, pointer capture, release, player sync, fog privacy and guard checks passed.');
