import assert from 'node:assert/strict';
import {squareMetres,sightRangeSquares,insidePolygon,sightClear,visibleSquares} from '../../wwwroot/js/LineOfSight.js';
import {fogKey} from '../../wwwroot/js/FogOfWar.js';
import {Battlemap} from '../../wwwroot/js/Battlemap.js';
import {validBattlemapSnapshot} from '../../wwwroot/js/BattlemapPresentationSync.js';
// Positions are in half squares: square corners are even, and the centre of square (column,row) is (2*column+1, 2*row+1).
const point=(x,y)=>({x,y}), centre=(column,row)=>point(2*column+1,2*row+1);
// A solid rectangle of whole squares, from its first column and row to its last.
const block=(column,row,lastColumn=column,lastRow=row)=>({points:[point(2*column,2*row),point(2*lastColumn+2,2*row),point(2*lastColumn+2,2*lastRow+2),point(2*column,2*lastRow+2)]});
const keys=cells=>new Set(cells.map(cell=>fogKey(cell.column,cell.row)));
// The Traveller sees as far as the higher of the map's and their own distance, in 1.5 m squares.
assert.equal(squareMetres,1.5);assert.equal(sightRangeSquares(24,0),16);assert.equal(sightRangeSquares(0,24),16);assert.equal(sightRangeSquares(12,30),20);assert.equal(sightRangeSquares(30,12),20);
assert.equal(sightRangeSquares(0,0),0,'both at 0: the GM reveals the map by hand');assert.equal(sightRangeSquares(undefined,null),0);assert.equal(sightRangeSquares(-6,0),0);assert.equal(sightRangeSquares('24','x'),16);
// Inside means strictly inside: the outline itself is not.
const pillar=block(1,1), wedge={points:[point(1,1),point(5,5),point(1,5)]};
const bend={points:[point(8,0),point(12,0),point(12,2),point(10,2),point(10,4),point(8,4)]};
assert.ok(insidePolygon(point(3,3),pillar.points));assert.equal(insidePolygon(point(1,1),pillar.points),false);
assert.equal(insidePolygon(point(3,2),pillar.points),false,'a position on a side is not inside');assert.equal(insidePolygon(point(2,2),pillar.points),false,'nor is a corner');
assert.ok(insidePolygon(point(2,4),wedge.points));assert.equal(insidePolygon(point(3,3),wedge.points),false,'nor a position on a diagonal side');assert.equal(insidePolygon(point(4,2),wedge.points),false);
assert.ok(insidePolygon(point(9,3),bend.points));assert.equal(insidePolygon(point(11,3),bend.points),false,'the notch of an L is outside it');
// Sight lines.
assert.ok(sightClear(centre(0,0),centre(5,3),[]),'nothing in the way');
assert.equal(sightClear(centre(0,0),centre(2,2),[pillar]),false,'a polygon in the way blocks sight');
assert.equal(sightClear(centre(0,1),centre(2,1),[pillar]),false);assert.ok(sightClear(centre(0,0),centre(2,0),[pillar]),'sight passes beside a polygon');
assert.ok(sightClear(centre(0,1),centre(1,0),[pillar]),'touching a corner does not block');
assert.ok(sightClear(point(1,1),point(5,5),[wedge]),'running along a side does not block');
assert.equal(sightClear(point(1,1),point(3,5),[wedge]),false,'leaving a corner into the polygon is blocked');
assert.equal(sightClear(point(3,4),point(2,4),[wedge]),false,'a line that stays inside is blocked');
assert.ok(sightClear(centre(0,0),centre(0,0),[pillar]));assert.equal(sightClear(centre(1,1),centre(1,1),[pillar]),false);
assert.ok(sightClear(centre(3,1),centre(9,1),[bend]) === false && sightClear(centre(5,2),centre(7,2),[bend]),'sight crosses the notch of an L but not its arm');
// No gap, no sight: polygons that touch at a corner or share a side leave nothing to see through.
assert.equal(sightClear(centre(0,0),centre(1,1),[block(1,0),block(0,1)]),false,'two polygons touching at a corner block the line through it');
assert.ok(sightClear(centre(0,0),centre(1,1),[block(1,0)]),'one of them alone does not');
assert.equal(sightClear(centre(1,2),centre(2,5),[block(1,3),block(2,4)]),false,'at any angle through the shared corner');
const upper={points:[point(1,1),point(5,1),point(5,5)]};
assert.equal(sightClear(point(1,1),point(5,5),[wedge,upper]),false,'two polygons sharing a side block the line along it');
assert.equal(sightClear(point(0,2),point(4,2),[{points:[point(0,0),point(4,0),point(2,2)]},{points:[point(2,2),point(4,4),point(0,4)]}]),false,'the waist of an hourglass is closed');
assert.ok(sightClear(point(0,2),point(4,2),[{points:[point(0,0),point(4,0),point(2,2)]}]));
// Open ground: everything within range, measured between square centres. 24 m is 16 squares.
const open=visibleSquares({column:20,row:20},[],41,41,16), openKeys=keys(open);
assert.equal(open.length,797,'every square within 16 squares');
for(let row=0;row<41;row++)for(let column=0;column<41;column++)
    assert.equal(openKeys.has(fogKey(column,row)),(column-20)**2+(row-20)**2<=256);
assert.ok(openKeys.has('36,20') && !openKeys.has('37,20'),'16 squares away is seen, 17 is not');assert.ok(openKeys.has('20,20'),'the viewer\'s own square is seen');
assert.equal(visibleSquares({column:0,row:0},[],5,4,16).length,20,'sight stops at the map edge');
assert.deepEqual(visibleSquares({column:2,row:2},[],9,9,0),[{column:2,row:2}]);assert.equal(visibleSquares({column:2,row:2},[],9,9,1).length,5);assert.equal(visibleSquares({column:2,row:2},[],9,9,1.5).length,9);
for(const viewer of [null,{column:9,row:0},{column:0,row:-1},{column:.5,row:0},{column:0}])assert.deepEqual(visibleSquares(viewer,[],9,9,16),[],'a viewer off the map sees nothing');
assert.deepEqual(visibleSquares({column:0,row:0},null,2,1,16),[{column:0,row:0},{column:1,row:0}]);
// A wall across the map hides everything behind it, and the wall itself.
const wall=block(5,0,5,19), behindWall=keys(visibleSquares({column:2,row:10},[wall],20,20,16));
for(let row=0;row<20;row++)for(let column=0;column<20;column++)
    assert.equal(behindWall.has(fogKey(column,row)),column<5 && (column-2)**2+(row-10)**2<=256,`wall: ${column},${row}`);
// A doorway in the wall lets sight through in a cone.
const doorway=[block(5,0,5,8),block(5,10,5,19)], through=keys(visibleSquares({column:2,row:9},doorway,20,20,16));
assert.ok(through.has('5,9') && through.has('6,9') && through.has('18,9'),'straight through the doorway');assert.equal(through.has('19,9'),false,'but no further than 16 squares');
assert.ok(through.has('12,8'),'the view widens beyond the doorway');assert.equal(through.has('8,7'),false,'the wall hides what is off to the side');
assert.equal(through.has('5,8'),false,'squares inside a polygon are never seen');assert.equal(through.has('6,0'),false);
assert.equal(keys(visibleSquares({column:4,row:9},doorway,20,20,16)).has('6,8'),false,'even from right beside the doorway');
// A pillar casts a shadow behind it but not beside it.
const shadow=keys(visibleSquares({column:10,row:5},[block(10,10)],30,30,16));
assert.equal(shadow.has('10,10'),false);assert.equal(shadow.has('10,11'),false);assert.equal(shadow.has('10,15'),false,'the shadow runs on behind the pillar');
assert.ok(shadow.has('9,10') && shadow.has('11,10') && shadow.has('10,9'));
assert.deepEqual(visibleSquares({column:10,row:10},[block(10,10)],30,30,16),[],'a viewer standing inside a polygon sees nothing');
assert.equal(keys(visibleSquares({column:0,row:0},[block(1,0),block(0,1)],9,9,16)).size,1,'a corner closed off by two polygons shows only the viewer\'s square');
assert.ok(keys(visibleSquares({column:0,row:0},[block(40,40)],60,60,16)).size>100,'polygons out of reach are ignored');
// Against a brute-force check on random maps of solid blocks: sample each line densely, and treat a line through a
// corner where two blocks meet diagonally as closed.
let random=20261122;const next=()=>(random=(random*1103515245+12345)%2147483648)/2147483648;
for(let trial=0;trial<5;trial++) {
    const columns=26, rows=20, filled=new Set(), blocks=[];
    for(let row=0;row<rows;row++)for(let column=0;column<columns;column++)if(next()<.13){filled.add(fogKey(column,row));blocks.push(block(column,row));}
    let viewer;do viewer={column:Math.floor(next()*columns),row:Math.floor(next()*rows)};while(filled.has(fogKey(viewer.column,viewer.row)));
    const range=[16,16,9,5.5,12][trial], seen=keys(visibleSquares(viewer,blocks,columns,rows,range));
    const isFilled=(x,y)=>filled.has(fogKey(Math.floor(x/2),Math.floor(y/2)));
    for(let row=0;row<rows;row++)for(let column=0;column<columns;column++) {
        const from=centre(viewer.column,viewer.row), to=centre(column,row), dx=to.x-from.x, dy=to.y-from.y;
        let expected=(column-viewer.column)**2+(row-viewer.row)**2<=range*range && !filled.has(fogKey(column,row));
        // Corners on the line: closed when the two squares the line passes between are both solid.
        for(let y=2;expected && y<2*rows;y+=2)for(let x=2;x<2*columns;x+=2) {
            if((x-from.x)*dy-(y-from.y)*dx !== 0 || (x-from.x)*(x-to.x)>0 || (y-from.y)*(y-to.y)>0) continue;
            if(dx*dy>0 ? isFilled(x+1,y-1) && isFilled(x-1,y+1) : isFilled(x-1,y-1) && isFilled(x+1,y+1)) expected=false;
        }
        for(let i=0;expected && i<2400;i++) {
            const t=(i+.5)/2400, x=from.x+dx*t, y=from.y+dy*t;
            if(x%2 !== 0 && y%2 !== 0 && isFilled(x,y)) expected=false;
        }
        assert.equal(seen.has(fogKey(column,row)),expected,`map ${trial}: square ${column},${row} from ${viewer.column},${viewer.row}`);
    }
}
console.log('Line of sight: the two distances, inside test, blocking, corners and sides, closed gaps, range, map edge, walls, doorways, shadows and a brute-force comparison passed.');

// Battlemap: fog clears from what the selected Traveller can see.
const host={style:{},title:'',hasPointerCapture:()=>false,setPointerCapture(){},releasePointerCapture(){},getBoundingClientRect:()=>({left:0,top:0}),clientLeft:0,clientTop:0};
const scene={id:1,campaignId:1,revision:1,gridSize:100,gridColour:'#000000',gridlineWidth:2,gridOpacity:50};
// A 12 x 8 map with a wall down column 6, leaving a doorway at row 3.
const walls=[block(6,0,6,2),block(6,4,6,7)];
const hero={id:1,characterId:1,column:2,row:3,isVisible:true,name:'Hero',isNpc:false}, guard={id:2,characterId:2,column:9,row:3,isVisible:true,name:'Guard',isNpc:true};
const lurker={id:3,characterId:3,column:9,row:0,isVisible:true,name:'Lurker',isNpc:true}, hound={id:4,characterId:4,column:1,row:1,isVisible:true,name:'Hound',isNpc:false,isAnimal:true};
const newMap=()=>{
    const map=new Battlemap({...host,style:{}},scene,'sight');
    Object.assign(map,{view:{x:0,y:0,scale:1},width:1200,height:800,imageWidth:1200,imageHeight:800,revealedFog:new Set(),fogHistory:[],movementRanges:[],playerTracker:[],polygons:walls,
        characterTokens:new Map([hero,guard,lurker,hound].map(token=>[token.id,{...token}]))});
    // These tokens have no sprites to restyle.
    map.tokenAppearance=()=>{};
    return map;
};
globalThis.DotNet={createJSStreamReference:value=>value};
const saves=[];let saveResult=true;
const save=async(name,...args)=>{assert.equal(name,'SaveFog');saves.push(JSON.parse(new TextDecoder().decode(args.at(-1))));return saveResult;};
const map=newMap();map.callback={invokeMethodAsync:save};
const settle=()=>map.flushTokenMoves();
const expectedFrom=(column,row,polygons=walls)=>keys(visibleSquares({column,row},polygons,12,8,16));
map.selectedId=1;map.revealSight();await settle();
assert.equal(map.revealedFog.size,0,'nothing is revealed while both distances are 0');
map.setAmbientSight(24);await settle();
assert.deepEqual(map.revealedFog,expectedFrom(2,3),'an Ambient Visual Distance clears what the selected Traveller sees');
assert.ok(map.revealedFog.has('2,3') && map.revealedFog.has('9,3') && !map.revealedFog.has('9,0') && !map.revealedFog.has('6,0'),'through the doorway but not through the wall');
assert.equal(saves.length,1);assert.deepEqual(keys(saves[0]),map.revealedFog,'the revealed squares are saved like a fog stroke');assert.equal(map.fogHistory.length,1);
// Players get the revealed squares and the tokens standing on them, and nothing else new.
let published;map.presentation={publish:state=>published=state};map.publishPresentation();
assert.ok(validBattlemapSnapshot(published));assert.deepEqual(keys(published.fog),map.revealedFog);
assert.deepEqual(published.tokens.map(token=>token.id).sort(),[1,2,4],'a token on a seen square shows; one behind the wall stays hidden');
assert.deepEqual(Object.keys(published).sort(),['actionSummary','combatMode','description','fog','measure','movementRanges','movementTrails','round','scene','targetSelection','tokens','tracker','view'],'no snapshot field was added');
assert.equal(/sight|polygon|"points"/i.test(JSON.stringify(published)),false,'nothing about sight or polygons reaches players');
// One reveal per situation: repeating the trigger does nothing, and Undo fog is not overridden.
map.revealSight();map.setEncounterState(false,1);await settle();assert.equal(saves.length,1);
await map.undoFog();assert.equal(map.revealedFog.size,0,'Undo fog takes the reveal back');map.revealSight();map.setEncounterState(false,1);await settle();
assert.equal(map.revealedFog.size,0,'and it stays undone until something changes');assert.equal(saves.length,2);
// Only Travellers reveal.
map.setEncounterState(false,2);await settle();assert.equal(map.revealedFog.size,0,'selecting an enemy reveals nothing');
map.setEncounterState(false,4);await settle();assert.equal(map.revealedFog.size,0,'nor does an animal');
map.setEncounterState(false,99);map.setEncounterState(false,null);await settle();assert.equal(saves.length,2);
map.setEncounterState(false,1);await settle();assert.deepEqual(map.revealedFog,expectedFrom(2,3),'selecting the Traveller again reveals');assert.equal(saves.length,3);
// Moving: nothing while the token is dragged or its move is being saved, then the new view is added to the old.
const token=map.characterTokens.get(1);
token.column=8;token.row=1;map.dragging={id:1,token};map.revealSight();assert.equal(saves.length,3,'not while dragging');
map.dragging=null;token.saving=true;map.setEncounterState(false,1);assert.equal(saves.length,3,'not while the move is being saved');
token.saving=false;map.revealSight();await settle();
const both=new Set([...expectedFrom(2,3),...expectedFrom(8,1)]);
assert.deepEqual(map.revealedFog,both,'seen squares stay seen after moving on');assert.ok(map.revealedFog.has('9,0') && map.revealedFog.size>expectedFrom(2,3).size);assert.equal(saves.length,4);
// A failed save puts the fog back and is not retried until something changes.
const few=new Set(['2,3']);map.revealedFog=new Set(few);
saveResult=false;token.column=10;token.row=6;map.revealSight();assert.ok(map.revealedFog.size>1);await settle();
assert.deepEqual(map.revealedFog,few,'a failed save rolls the reveal back');map.revealSight();await settle();assert.equal(saves.length,5);
saveResult=true;
// A reveal asked for while fog is being painted or saved waits its turn.
token.column=8;token.row=6;map.fogStroke={id:5,before:new Set(map.revealedFog),last:null,reveal:true};map.revealSight();assert.equal(saves.length,5);
map.cancelFogStroke();await settle();assert.equal(saves.length,6,'it runs when the stroke ends');assert.ok([...expectedFrom(8,6)].every(key=>map.revealedFog.has(key)));
assert.equal(map.revealedFog.has('0,0'),false);
let finish;map.callback.invokeMethodAsync=(name,...args)=>new Promise(resolve=>finish=()=>resolve(save(name,...args)));
const painted=new Set(map.revealedFog);map.revealedFog.add('0,7');const repaint=map.saveFogEdit(painted);
token.column=3;token.row=3;map.revealSight();assert.equal(map.sightPending,true);assert.equal(map.revealedFog.has('0,0'),false,'not while a fog save is in flight');
map.callback.invokeMethodAsync=save;finish();await repaint;await settle();
assert.ok(map.revealedFog.has('0,0') && map.revealedFog.has('0,7'),'the waiting reveal runs after the save');assert.equal(map.sightPending,false);assert.equal(saves.length,8);
// Polygons block sight, so saving a change to them reveals what has come into view.
map.callback.invokeMethodAsync=async(name,...args)=>name === 'SavePolygons' ? true : save(name,...args);
token.column=2;token.row=6;map.revealSight();await settle();assert.equal(map.revealedFog.has('6,5'),false,'squares inside the wall are hidden');
const fogSaves=saves.length;
await map.savePolygons([walls[0]]);await settle();
assert.ok(map.revealedFog.has('6,5'),'deleting the wall section reveals where it stood');assert.equal(saves.length,fogSaves+1);
// Turning fog off and on, and switching line of sight off.
map.setFogEnabled(null);assert.equal(map.revealedFog,null);map.revealSight();await settle();assert.equal(saves.length,fogSaves+1,'nothing to reveal with fog off');
map.setFogEnabled([]);await settle();assert.deepEqual(map.revealedFog,expectedFrom(2,6,[walls[0]]),'fog switched on is cleared at once from what the Traveller sees');
map.setAmbientSight(0);token.column=10;token.row=1;map.setEncounterState(false,1);await settle();assert.deepEqual(map.revealedFog,expectedFrom(2,6,[walls[0]]),'back at 0, moving reveals nothing');
// A Traveller standing inside a polygon sees nothing, and the player view never reveals by itself.
map.setAmbientSight(24);await settle();const inside=map.revealedFog.size;token.column=6;token.row=1;map.revealSight();await settle();assert.equal(map.revealedFog.size,inside);
// The higher of the map's distance and the Traveller's own is used, and either one alone is enough.
const seenWithin=(metres,column,row,polygons)=>keys(visibleSquares({column,row},polygons,12,8,metres/1.5));
const sight=newMap();sight.callback={invokeMethodAsync:save};sight.polygons=[];sight.selectedId=1;const eyes=sight.characterTokens.get(1);
const reset=()=>{sight.revealedFog=new Set();sight.sightKey=null;};
eyes.visualDistance=6;sight.revealSight();await sight.flushTokenMoves();assert.deepEqual(sight.revealedFog,seenWithin(6,2,3,[]),'a Traveller\'s own Visual Distance works with no ambient distance');assert.ok(sight.revealedFog.has('6,3') && !sight.revealedFog.has('7,3'),'6 m is four squares');
reset();sight.setAmbientSight(3);await sight.flushTokenMoves();assert.deepEqual(sight.revealedFog,seenWithin(6,2,3,[]),'the Traveller\'s distance wins when it is higher');
sight.setAmbientSight(9);await sight.flushTokenMoves();assert.deepEqual(sight.revealedFog,seenWithin(9,2,3,[]),'raising the ambient distance above it reveals further');
eyes.visualDistance=15;sight.revealSight();await sight.flushTokenMoves();assert.deepEqual(sight.revealedFog,seenWithin(15,2,3,[]),'and so does raising the Traveller\'s own');
reset();eyes.visualDistance=0;sight.setAmbientSight(0);await sight.flushTokenMoves();assert.equal(sight.revealedFog.size,0,'both at 0 leaves the fog to the GM');
sight.characterTokens.get(2).visualDistance=24;sight.setEncounterState(false,2);await sight.flushTokenMoves();assert.equal(sight.revealedFog.size,0,'an enemy with a distance still reveals nothing');
// Only active polygons block sight: an inactive one is an open door.
sight.setEncounterState(false,1);sight.polygons=[walls[0],block(6,3),walls[1]];sight.setAmbientSight(24);await sight.flushTokenMoves();
assert.equal(sight.revealedFog.has('9,3'),false,'a closed door blocks the view');assert.equal(sight.revealedFog.has('6,3'),false);
const opened=[walls[0],{...block(6,3),active:false},walls[1]];
sight.callback.invokeMethodAsync=async(name,...args)=>name === 'SavePolygons' ? true : save(name,...args);
await sight.savePolygons(opened);await sight.flushTokenMoves();
assert.ok(sight.revealedFog.has('9,3') && sight.revealedFog.has('6,3'),'opening it reveals the doorway and what is beyond');assert.deepEqual(sight.revealedFog,expectedFrom(2,3));
const player=newMap();player.readOnly=true;player.callback={invokeMethodAsync:()=>assert.fail('the player view never saves')};
player.selectedId=1;player.setAmbientSight(24);player.ambientSight=24;player.characterTokens.get(1).visualDistance=24;player.revealSight();assert.equal(player.revealedFog.size,0);
console.log('Line-of-sight fog: Travellers only, ambient and character distances, inactive polygons, saving and undo, one reveal per situation, moves, failed saves, waiting for fog edits, polygon changes, fog on/off and player privacy passed.');
