import assert from 'node:assert/strict';
import {weaponRangeBand} from '../../wwwroot/js/MovementIndicators.js';
import {Battlemap} from '../../wwwroot/js/Battlemap.js';
import {playerTargetSelection,validBattlemapSnapshot} from '../../wwwroot/js/BattlemapPresentationSync.js';
const oldPixi=globalThis.PIXI;
const point=()=>({set(x,y){this.x=x;this.y=y;}});
class Container {constructor(){this.position=point();this.children=[];}addChild(...children){this.children.push(...children);}destroy(){this.destroyed=true;}}
class Graphics {constructor(){this.strokes=[];}clear(){this.strokes=[];return this;}moveTo(x,y){this.start={x,y};return this;}lineTo(x,y){this.end={x,y};return this;}circle(x,y,radius){this.circleValue={x,y,radius};return this;}stroke(style){this.strokes.push(style);return this;}rect(){return this;}fill(style){this.fillStyle=style;return this;}}
class Text {constructor({text,style}){this.text=text;this.style=style;this.position=point();}get width(){return this.text.length*8;}get height(){return 16;}}
globalThis.PIXI={Container,Graphics,Text};
const map=new Battlemap({style:{}},{gridSize:100},'target-indicator');map.view={x:10,y:20,scale:2};map.combatMode=true;map.selectedId=1;
map.movementStroke=new Graphics();map.movementOverlay=new Container();map.targetRingGraphics=new Graphics();
const actor={id:1,characterId:1,name:'Hero',column:1,row:2,isNpc:false,isVisible:true};
const target={id:2,characterId:2,name:'Enemy',column:4,row:6,isNpc:true,isVisible:true};
map.characterTokens.set(1,actor);map.characterTokens.set(2,target);map.setTargetSelection({actorId:1,targetId:2});
assert.deepEqual(map.movementStroke.strokes[0],{color:0xff00ff,alpha:.25,width:6},'target line is magenta');
assert.deepEqual(map.movementStroke.start,{x:310,y:520});assert.deepEqual(map.movementStroke.end,{x:910,y:1320});
assert.equal(map.movementLabels.get('target').text.text,'7.5 m');
map.setTargetSelection({actorId:1,targetId:2,rangeProfile:{range:100,scope:false,aimBonus:0}});
assert.equal(map.movementLabels.get('target').text.text,'7.5 m · Short Range');
map.setTargetSelection({actorId:1,targetId:2});
assert.equal(map.movementLabels.get('target').text.text,'7.5 m','deselecting ranged attack removes range details while retaining distance');
for (const [distance,band] of [[4.9,'Short Range'],[5,'Medium Range'],[20,'Long Range'],[40,'Extreme Range'],[80,'Out of Range']]) assert.equal(weaponRangeBand(distance,{range:20,scope:true,aimBonus:1}),band);
assert.equal(weaponRangeBand(101,{range:1000,scope:false,aimBonus:6}),'Extreme Range');
assert.equal(weaponRangeBand(101,{range:1000,scope:true,aimBonus:0}),'Extreme Range');
assert.equal(weaponRangeBand(101,{range:1000,scope:true,aimBonus:1}),'Short Range');
assert.equal(weaponRangeBand(100,{range:1000,scope:false,aimBonus:0}),'Short Range');
assert.equal(weaponRangeBand(1,{range:0,scope:false,aimBonus:0}),null);
assert.equal(map.targetRingGraphics.strokes[0].color,0xff00ff);assert.equal(map.targetRingGraphics.circleValue.x,910);
const firstRadius=map.targetRingGraphics.circleValue.radius;map.pulseTime=350;map.drawTargetRing();assert.notEqual(map.targetRingGraphics.circleValue.radius,firstRadius,'target circle pulses over time');
actor.isNpc=true;map.drawMovementIndicators();map.drawTargetRing();assert.equal(map.movementStroke.strokes[0].color,0xff00ff);assert.equal(map.targetRingGraphics.strokes[0].color,0xff00ff);
map.movementTrails.set(1,{id:1,fromColumn:0,fromRow:0,toColumn:1,toRow:2,isNpc:true});map.drawMovementIndicators();
assert.equal(map.movementStroke.strokes[0].color,0xffffff,'movement is white even for enemies');assert.equal(map.movementStroke.strokes[1].color,0xff00ff,'target and movement lines retain distinct colours');
target.column=5;map.drawMovementIndicators();map.drawTargetRing();assert.equal(map.targetRingGraphics.circleValue.x,1110,'target ring follows movement');
map.setTargetSelection(null);assert.equal(map.movementLabels.has('target'),false);assert.equal(map.targetRingGraphics.strokes.length,0,'clearing the target clears the pulse');
map.setTargetSelection({actorId:1,targetId:2});map.combatMode=false;map.drawMovementIndicators();map.drawTargetRing();assert.equal(map.targetRingGraphics.strokes.length,0,'target pulse is combat-only');

assert.deepEqual(playerTargetSelection(1,2,[actor,target]),{actorId:1,targetId:2});
assert.equal(playerTargetSelection(1,2,[actor,{...target,isVisible:false}]),null,'hidden target IDs stay private');
assert.equal(playerTargetSelection(1,2,[{...actor,isVisible:false},target]),null,'hidden acting combatants stay private');
assert.equal(playerTargetSelection(1,null,[actor,target]),null);
const state={scene:{id:1,campaignId:1,gridSize:100,gridColour:'#000000',gridlineWidth:2,gridOpacity:50},tokens:[actor,target],view:{x:0,y:0,scale:1,width:1000,height:600,imageWidth:2000,imageHeight:2000},targetSelection:{actorId:1,targetId:2}};
assert.ok(validBattlemapSnapshot(state));assert.equal(validBattlemapSnapshot({...state,targetSelection:{actorId:1,targetId:99}}),false);
console.log('Faction target lines, pulsing circles, live positions, white movement, target clearing and cast privacy passed.');
const scoped={range:1000,scope:true,aimBonus:1};
const castSelection=playerTargetSelection(1,2,[actor,target],scoped);
assert.deepEqual(castSelection.rangeProfile,scoped);
assert.ok(validBattlemapSnapshot({...state,targetSelection:castSelection}));
assert.equal(validBattlemapSnapshot({...state,targetSelection:{...castSelection,rangeProfile:{...scoped,range:-1}}}),false);
map.combatMode=true;map.setTargetSelection(castSelection);
assert.match(map.movementLabels.get('target').text.text,/Short Range/);
console.log('Range boundaries, Scope/Aim override and cast range labels passed.');
const other={...target,id:3,name:'Second',column:7};
map.characterTokens.set(3,other);
map.setTargetSelection({actorId:1,targetId:2,targetIds:[2,3]});
assert.equal(map.targetRingGraphics.strokes.length,2,'all Full Auto targets get pulsing highlights');
assert.ok(map.movementLabels.has('target:3'),'each target gets its own distance line');
assert.deepEqual(playerTargetSelection(1,[2,3],[actor,target,other]),{actorId:1,targetId:2,targetIds:[2,3]});
assert.deepEqual(playerTargetSelection(1,[2,3],[actor,target,{...other,isVisible:false}]),{actorId:1,targetId:2},'hidden targets stay private within volleys');
assert.ok(validBattlemapSnapshot({...state,tokens:[actor,target,other],targetSelection:{actorId:1,targetId:2,targetIds:[2,3]}}));
assert.equal(validBattlemapSnapshot({...state,targetSelection:{actorId:1,targetId:2,targetIds:[2,3]}}),false,'hidden extra target rejected');
map.setTargetSelection({actorId:1,targetId:2});
assert.equal(map.targetRingGraphics.strokes.length,1);assert.equal(map.movementLabels.has('target:3'),false,'switching to Single clears extra highlights');
console.log('Multiple target highlights, distance lines, clearing and Cast privacy passed.');

map.host.getBoundingClientRect=()=>({left:0,top:0,right:2000,bottom:2000});
map.host.clientLeft=0;map.host.clientTop=0;
map.host.hasPointerCapture=()=>false;
map.targetDrag={id:8,context:{targetIds:[2]}};
map.previewTarget({clientX:1110,clientY:1320});
assert.equal(map.targetDrag.target,target,'eligible token snaps');
assert.equal(map.targetTrails().at(-1).toColumn,target.column);
assert.equal(map.targetRingGraphics.strokes.length,2,'preview has a reticle');
map.previewTarget({clientX:1500,clientY:1000});
assert.equal(map.targetDrag.target,null,'empty space does not select a target');
assert.equal(map.targetRingGraphics.strokes.length,1,'free pointer has no reticle');
assert.equal(map.targetTrails().at(-1).preview,true,'free pointer retains targeting line');
map.targetDrag.context.targetIds=[];
map.previewTarget({clientX:1110,clientY:1320});
assert.equal(map.targetDrag.target,null,'ineligible token cannot snap');
map.cancelTargetDrag();
assert.equal(map.targetDrag,null);assert.equal(map.targetTrails().length,1,'cancel preserves committed targets');
actor.turnRing=new Graphics();map.drawTurnRing(actor);
assert.equal(actor.turnRing.strokes[0].color,0xffffff,'current combatant ring is white');
console.log('Target preview snapping, eligibility, cancellation and white turn ring passed.');
for (const gridSize of [10,100]) for (const scale of [.125,.5,1,4]) {
    map.scene.gridSize=gridSize;map.view.scale=scale;
    const tokenDiameter=Math.max(gridSize*scale,36);
    for (const pulseTime of [0,350,1050]) {
        map.pulseTime=pulseTime;map.drawTargetRing();
        const ring=map.targetRingGraphics.circleValue;
        assert.ok(ring.radius-1.5>tokenDiameter/2,'entire target stroke stays outside the displayed token at every zoom and pulse phase');
        assert.equal(ring.x,map.view.x+(target.column+.5)*gridSize*scale,'ring remains centred on the target cell');
    }
}
console.log('Target rings respect minimum token size across grid sizes, zoom levels and pulse phases.');
globalThis.PIXI=oldPixi;
