import assert from 'node:assert/strict';
import {polygonColour,polygonIdleColour,polygonStyle,maxPolygons,maxPolygonPoints,polygonClickSlop,polygonHitPixels,samePolygonPoint,validPolygonPoint,validPolygons,polygonPosition,polygonSnap,addPolygonPoint,polygonAt} from '../../wwwroot/js/Polygons.js';
import {Battlemap} from '../../wwwroot/js/Battlemap.js';
import {validBattlemapSnapshot} from '../../wwwroot/js/BattlemapPresentationSync.js';
const point=(x,y)=>({x,y});
// Points are in half squares: corners have two even values, centres two odd ones.
assert.ok(samePolygonPoint(point(2,4),point(2,4)));assert.equal(samePolygonPoint(point(2,4),point(4,2)),false);
assert.ok(samePolygonPoint(null,undefined));assert.equal(samePolygonPoint(null,point(0,0)),false);
assert.ok(validPolygonPoint(point(0,0)),'a square corner');assert.ok(validPolygonPoint(point(3,5)),'a square centre');
for(const bad of [point(1,0),point(0,1),point(-2,0),point(.5,.5),point('2',0),null,{}])assert.equal(validPolygonPoint(bad),false);
const triangle={points:[point(0,0),point(4,0),point(2,2)]};
assert.ok(validPolygons([]));assert.ok(validPolygons([triangle]));
for(const bad of [null,{},[null],[{}],[{points:[point(0,0),point(2,0)]}],[{points:[point(0,0),point(1,0),point(2,2)]}],Array(maxPolygons+1).fill(triangle),[{points:Array(maxPolygonPoints+1).fill(point(0,0))}]])
    assert.equal(validPolygons(bad),false);
assert.ok(validPolygons([{...triangle,active:false},{...triangle,active:true}]),'a polygon may be active or inactive');assert.equal(validPolygons([{...triangle,active:'no'}]),false);
// How polygons are drawn: red while editing, faint blue otherwise; shaded when active, outline only when inactive.
assert.deepEqual(polygonStyle(true,true),{colour:0xff0000,line:1,fill:.1});assert.deepEqual(polygonStyle(true,false),{colour:0xff0000,line:.45,fill:0});
assert.deepEqual(polygonStyle(false,true),{colour:polygonIdleColour,line:.3,fill:.15});assert.deepEqual(polygonStyle(false,false),{colour:polygonIdleColour,line:.3,fill:0});
assert.equal(polygonIdleColour,0x3b82f6);
assert.deepEqual(polygonPosition(point(3,5),100),{x:150,y:250});assert.deepEqual(polygonPosition(point(2,1),25),{x:25,y:12.5});
// Snapping on a 250 x 180 map at 100 pixels per square: 2.5 squares across and 1.8 down.
const snap=(x,y,size=100,width=250,height=180)=>polygonSnap({x,y},size,width,height);
assert.deepEqual(snap(10,10),point(0,0),'near a corner');assert.deepEqual(snap(90,10),point(2,0));assert.deepEqual(snap(60,95),point(2,2));
assert.deepEqual(snap(50,50),point(1,1),'the middle of a square snaps to its centre');assert.deepEqual(snap(50,20),point(1,1));assert.deepEqual(snap(20,20),point(0,0));
assert.deepEqual(snap(150,140),point(3,3),'the bottom corners of a partial row are off the image');
assert.deepEqual(snap(240,40),point(5,1),'a centre on the image edge is allowed');assert.deepEqual(snap(250,180),point(5,3));
for(const [x,y] of [[-1,10],[251,10],[10,181],[10,-1]])assert.equal(snap(x,y),null,'positions off the image do not snap');
assert.equal(polygonSnap(null,100,250,180),null);assert.equal(snap(10,10,0),null);
// Every position snaps to the nearest corner or centre that is on the image, including partial edge squares.
for(const [size,width,height] of [[100,250,180],[100,230,290],[100,245,195],[60,230,170],[7,50,33],[64,64,64],[300,100,100]]) {
    const maxX=Math.floor(2*width/size), maxY=Math.floor(2*height/size), all=[];
    for(let y=0;y<=maxY;y++)for(let x=0;x<=maxX;x++)if((x-y)%2 === 0)all.push(point(x,y));
    for(let py=0;py<=height;py+=height/23)for(let px=0;px<=width;px+=width/29) {
        const found=polygonSnap({x:px,y:py},size,width,height), distance=p=>Math.hypot(p.x*size/2-px,p.y*size/2-py);
        assert.ok(found && validPolygonPoint(found) && found.x<=maxX && found.y<=maxY,'snap points stay on the image');
        assert.ok(distance(found)<=Math.min(...all.map(distance))+1e-9,`nearest snap point at ${px},${py} with ${size} pixel squares`);
    }
}
// Clicking while drawing.
assert.deepEqual(addPolygonPoint([],point(0,0)),{points:[point(0,0)],closed:false},'the first click sets the origin');
assert.deepEqual(addPolygonPoint([point(0,0)],point(2,0)),{points:[point(0,0),point(2,0)],closed:false});
assert.deepEqual(addPolygonPoint([point(0,0)],point(0,0)),{points:[point(0,0)],closed:false},'the origin alone cannot close');
assert.deepEqual(addPolygonPoint([point(0,0),point(2,0)],point(0,0)),{points:[point(0,0),point(2,0)],closed:false},'two points cannot close');
assert.deepEqual(addPolygonPoint([point(0,0),point(2,0)],point(2,0)).points.length,2,'the point just placed is not added twice');
assert.deepEqual(addPolygonPoint(triangle.points,point(0,0)),{points:triangle.points,closed:true},'clicking the first point closes the polygon');
assert.equal(addPolygonPoint(triangle.points,point(4,0)).points.length,4,'an earlier point other than the first can be revisited');
const full=Array.from({length:maxPolygonPoints},(_,i)=>point(i%2*2,i%4>1 ? 2 : 0));
assert.equal(addPolygonPoint(full,point(4,4)).points.length,maxPolygonPoints,'a polygon stops growing at the point limit');assert.ok(addPolygonPoint(full,full[0]).closed);
// Picking a polygon: a 2 x 2 square at the origin, an L shape, and a triangle drawn over the square.
const square={points:[point(0,0),point(4,0),point(4,4),point(0,4)]};
const corner={points:[point(8,0),point(12,0),point(12,2),point(10,2),point(10,4),point(8,4)]};
assert.equal(polygonAt([square],{x:100,y:100},100),0,'inside');assert.equal(polygonAt([square],{x:230,y:100},100),-1,'outside');
assert.equal(polygonAt([square],{x:204,y:100},100,6),0,'just outside the outline counts');assert.equal(polygonAt([square],{x:207,y:100},100,6),-1);
assert.equal(polygonAt([square],{x:204,y:100},100,3),-1,'the margin shrinks as the map is zoomed in');
assert.equal(polygonAt([square,corner],{x:450,y:50},100),1);assert.equal(polygonAt([square,corner],{x:550,y:150},100),-1,'the notch of an L is outside it');
assert.equal(polygonAt([square,triangle],{x:100,y:50},100),1,'the most recent polygon wins where two overlap');assert.equal(polygonAt([square,triangle],{x:100,y:150},100),0);
assert.equal(polygonAt([],{x:0,y:0},100,6),-1);
console.log('Polygon points, active flag, drawing styles, snapping to corners and centres, partial edge squares, closing rules, point limit and picking passed.');

// Battlemap: Polygon mode.
const host={style:{},title:'',hasPointerCapture:()=>false,setPointerCapture(){},releasePointerCapture(){},getBoundingClientRect:()=>({left:0,top:0}),clientLeft:0,clientTop:0};
const scene={id:1,campaignId:1,revision:1,gridSize:100,gridColour:'#000000',gridlineWidth:2,gridOpacity:50};
const recorder=()=>({ops:[],clears:0,clear(){this.ops=[];this.clears++;return this;},moveTo(x,y){this.ops.push(['moveTo',x,y]);return this;},lineTo(x,y){this.ops.push(['lineTo',x,y]);return this;},
    closePath(){this.ops.push(['closePath']);return this;},circle(x,y,r){this.ops.push(['circle',x,y,r]);return this;},stroke(style){this.ops.push(['stroke',style]);return this;},fill(style){this.ops.push(['fill',style]);return this;}});
const map=new Battlemap(host,scene,'polygons');
Object.assign(map,{view:{x:0,y:0,scale:1},width:250,height:180,imageWidth:250,imageHeight:180,polygonGraphics:recorder(),revealedFog:null,fogHistory:[],movementRanges:[],playerTracker:[]});
globalThis.DotNet={createJSStreamReference:value=>value};
const saves=[];let saveResult=true;
const save=async(name,...args)=>{assert.equal(name,'SavePolygons');assert.deepEqual(args.slice(0,4),[1,1,1,100]);saves.push(JSON.parse(new TextDecoder().decode(args.at(-1))));return saveResult;};
map.callback={invokeMethodAsync:save};
const at=(x,y,button=0)=>({button,pointerId:1,clientX:x,clientY:y,preventDefault(){}});
const click=(...positions)=>{for(const [x,y] of positions)map.polygonClick(at(x,y));};
const settle=()=>map.flushTokenMoves();
assert.deepEqual(map.polygons,[]);
click([10,10]);assert.equal(map.polygonDraft,null,'clicks do nothing outside Polygon mode');
map.polygonDown(at(10,10,2));await settle();assert.equal(saves.length,0);
map.setPolygonMode(true);assert.equal(map.polygonMode,true);assert.equal(host.style.cursor,'crosshair');
// A left press is a click until it moves far enough to be a pan.
map.polygonDown(at(10,10));assert.deepEqual(map.dragging.polygonClick,{x:10,y:10});
assert.equal(map.polygonDragStarted(at(12,11)),false,'a small wobble is still a click');
assert.equal(map.polygonDragStarted(at(10+polygonClickSlop,10)),true,'a drag pans the map');assert.equal(map.dragging.polygonClick,null);
map.dragging=null;map.polygonDown(at(10,10,1));assert.equal(map.dragging,null,'other buttons do nothing');
// Drawing a triangle.
click([10,10]);assert.deepEqual(map.polygonDraft,[point(0,0)],'the first click sets the origin');
click([12,8]);assert.equal(map.polygonDraft.length,1,'clicking the origin again changes nothing');
click([190,10],[195,5]);assert.deepEqual(map.polygonDraft,[point(0,0),point(4,0)],'a repeated click adds one point');
click([5,5]);assert.equal(map.polygonDraft.length,2,'two points cannot close');
click([300,10]);assert.equal(map.polygonDraft.length,2,'clicks off the image are ignored');
click([150,140]);assert.deepEqual(map.polygonDraft.at(-1),point(3,3),'points snap to square centres too');
assert.equal(saves.length,0,'nothing is saved until the polygon is closed');
click([5,5]);await settle();
const first={points:[point(0,0),point(4,0),point(3,3)],active:true};
assert.equal(map.polygonDraft,null);assert.deepEqual(map.polygons,[first],'clicking the first point completes the polygon');
assert.deepEqual(saves,[[first]],'the scene\'s polygons are saved when one is completed');assert.equal(map.polygonHistory.length,1);assert.equal(map.polygonSaving,false);
// Right-click and Esc while drawing.
click([200,100],[100,100]);
map.polygonDown(at(50,30,2));assert.deepEqual(map.polygonDraft,[point(4,2)],'right-click takes back the last point');assert.equal(map.dragging,null);
map.polygonDown(at(50,30,2));assert.equal(map.polygonDraft,null,'taking back the only point cancels the polygon');
click([200,100]);map.cancelPolygonDraft();assert.equal(map.polygonDraft,null,'Esc cancels the polygon being drawn');map.cancelPolygonDraft();
await settle();assert.equal(saves.length,1);assert.deepEqual(map.polygons,[first],'cancelling never touches finished polygons');
// A failed or broken save puts the map back.
const second={points:[point(4,2),point(2,2),point(4,0)],active:true}, drawSecond=()=>click([200,100],[100,100],[200,0],[200,100]);
saveResult=false;drawSecond();assert.equal(map.polygons.length,2,'the new polygon shows while it is saved');assert.equal(map.polygonSaving,true);
click([10,10]);assert.equal(map.polygonDraft,null,'no drawing while a save is in flight');await map.undoPolygon();
await settle();assert.equal(saves.length,2);assert.deepEqual(map.polygons,[first],'a rejected save removes the new polygon again');assert.equal(map.polygonHistory.length,1);
const warn=console.warn;let warnings=0;console.warn=()=>warnings++;
map.callback.invokeMethodAsync=async()=>{throw Error('offline');};drawSecond();await settle();console.warn=warn;
assert.equal(warnings,1);assert.deepEqual(map.polygons,[first],'a save that throws rolls back too');assert.equal(map.polygonSaving,false);
map.callback.invokeMethodAsync=save;saveResult=true;drawSecond();await settle();
assert.deepEqual(map.polygons,[first,second]);assert.deepEqual(saves.at(-1),[first,second]);assert.equal(map.polygonHistory.length,2);
// Right-click deletes the finished polygon under the pointer.
const count=saves.length;
map.polygonDown(at(240,170,2));await settle();assert.equal(saves.length,count,'right-clicking empty map does nothing');
map.polygonDown(at(190,90,2));await settle();assert.deepEqual(map.polygons,[first],'right-click inside a polygon deletes it');assert.deepEqual(saves.at(-1),[first]);
// The first polygon's right side crosses y = 70 at x = 176.7, so x = 181 is just outside it.
map.polygonDown(at(181,70,2));await settle();assert.deepEqual(map.polygons,[],'right-click on an outline deletes it');assert.deepEqual(saves.at(-1),[]);
assert.ok(181-(200-50*70/150)<polygonHitPixels);assert.equal(map.polygonHistory.length,4);
// Undo reverses the last add or delete, one step at a time.
await map.undoPolygon();assert.deepEqual(map.polygons,[first],'undo restores a deleted polygon');assert.deepEqual(saves.at(-1),[first]);
await map.undoPolygon();assert.deepEqual(map.polygons,[first,second]);
await map.undoPolygon();assert.deepEqual(map.polygons,[first],'undo removes an added polygon');
await map.undoPolygon();assert.deepEqual(map.polygons,[]);assert.equal(map.polygonHistory.length,0);
const undone=saves.length;await map.undoPolygon();assert.equal(saves.length,undone,'nothing left to undo');
saveResult=false;map.polygonHistory=[[first]];await map.undoPolygon();assert.deepEqual(map.polygons,[],'a failed undo changes nothing');assert.equal(map.polygonHistory.length,1);
saveResult=true;map.polygonHistory=[];
click([10,10],[190,10]);const drafting=saves.length;await map.undoPolygon();
assert.deepEqual(map.polygonDraft,[point(0,0)],'undo takes back a point while drawing');assert.equal(saves.length,drafting);
map.setPolygonMode(false);assert.equal(map.polygonDraft,null,'leaving Polygon mode discards an unfinished polygon');assert.equal(host.style.cursor,'grab');
// The snap marker and preview follow the pointer and redraw only when the snap point changes.
map.setPolygonMode(true);const clears=map.polygonGraphics.clears;
map.polygonHover({x:60,y:95});assert.deepEqual(map.polygonCursor,point(2,2));map.polygonHover({x:70,y:99});assert.equal(map.polygonGraphics.clears,clears+1);
const marker=(x,y,radius,width)=>[['circle',x,y,radius],['stroke',{color:0x000000,width:width+2,alpha:.8}],['circle',x,y,radius],['stroke',{color:0xffffff,width}]];
assert.deepEqual(map.polygonGraphics.ops,marker(100,100,4,1.5),'a white marker with a dark edge shows where the next click will land');
map.polygonHover(null);assert.equal(map.polygonCursor,null);assert.equal(map.polygonGraphics.clears,clears+2);map.polygonHover(null);assert.equal(map.polygonGraphics.clears,clears+2);
// Drawing: lines of constant width, placed by the current pan and zoom. In Polygon mode they are red with a very
// light red fill; outside it they are faint blue with a light blue fill. Inactive polygons have no fill.
assert.equal(polygonColour,0xff0000);
const line={color:0xff0000,width:2}, outline=[['moveTo',10,20],['lineTo',410,20],['lineTo',210,220],['closePath']];
map.view={x:10,y:20,scale:2};map.polygons=[triangle];map.drawPolygons();
assert.deepEqual(map.polygonGraphics.ops,[...outline,['fill',{color:0xff0000,alpha:.1}],['stroke',{...line,alpha:1}]]);
map.polygons=[{...triangle,active:false}];map.drawPolygons();
assert.deepEqual(map.polygonGraphics.ops,[...outline,['stroke',{...line,alpha:.45}]],'an inactive polygon is a fainter outline with no fill');
map.polygonMode=false;map.polygons=[triangle,{...triangle,active:false}];map.drawPolygons();
assert.deepEqual(map.polygonGraphics.ops,[...outline,['fill',{color:0x3b82f6,alpha:.15}],['stroke',{color:0x3b82f6,width:2,alpha:.3}],...outline,['stroke',{color:0x3b82f6,width:2,alpha:.3}]],'outside Polygon mode: faint blue');
map.polygonMode=true;
map.polygons=[];map.polygonDraft=[point(0,0),point(2,0),point(2,2)];map.polygonCursor=point(4,2);map.drawPolygons();
assert.deepEqual(map.polygonGraphics.ops,[['moveTo',10,20],['lineTo',210,20],['lineTo',210,220],['stroke',line],
    ['moveTo',210,220],['lineTo',410,220],['stroke',{...line,alpha:.6}],
    ['circle',10,20,3],['fill',{color:0xff0000}],['circle',210,20,3],['fill',{color:0xff0000}],['circle',210,220,3],['fill',{color:0xff0000}],
    ['circle',10,20,6],['stroke',line],...marker(410,220,4,1.5)],'the polygon being drawn shows its points and a preview of the next side');
map.polygonCursor=point(0,0);map.drawPolygons();
assert.deepEqual(map.polygonGraphics.ops.slice(-4),marker(10,20,8,2),'the first point gets a larger ring when a click would close the polygon');
assert.equal(map.polygonGraphics.ops.some(op=>op[0] === 'circle' && (op[3] === 4 || op[3] === 6)),false,'that ring replaces the first-point ring and the pointer marker');
assert.deepEqual(map.polygonGraphics.ops.find(op=>op[0] === 'lineTo' && op[1] === 10 && op[2] === 20),['lineTo',10,20],'the preview shows the closing side');
map.polygonDraft=[point(0,0)];map.polygonCursor=point(0,0);map.drawPolygons();
assert.equal(map.polygonGraphics.ops.some(op=>op[0] === 'moveTo'),false,'a single point has no line yet');
map.polygonDraft=null;map.polygonCursor=null;map.view={x:0,y:0,scale:1};
// Polygon mode keeps the pointer to itself: no fog painting cursor and no token selection.
map.revealedFog=new Set(['0,0']);map.hoverToken=true;map.updateCursor({shiftKey:true});assert.equal(host.style.cursor,'crosshair');
let selected=false;map.callback.invokeMethodAsync=async()=>{selected=true;};
await map.selectTokenAt(at(50,50));assert.equal(selected,false,'double-click does not select a token in Polygon mode');
map.callback.invokeMethodAsync=save;map.revealedFog=null;map.hoverToken=false;
// Outside Polygon mode a click opens or closes the polygon under the pointer, like a door.
map.setPolygonMode(false);map.polygons=[first,second];map.polygonHistory=[];
const toggles=saves.length;
assert.equal(map.polygonUnder({x:190,y:90}),1);assert.equal(map.polygonUnder({x:240,y:170}),-1);
map.togglePolygonAt(at(190,90));await settle();
assert.deepEqual(map.polygons,[first,{...second,active:false}],'a click makes the polygon under the pointer inactive');assert.deepEqual(saves.at(-1),map.polygons,'and saves it');assert.equal(map.polygonHistory.length,1);
map.togglePolygonAt(at(190,90));await settle();assert.deepEqual(map.polygons,[first,second],'a second click makes it active again');
map.togglePolygonAt(at(240,170));await settle();assert.equal(saves.length,toggles+2,'a click on open map changes nothing');
map.setPolygonMode(true);map.togglePolygonAt(at(190,90));await settle();assert.equal(saves.length,toggles+2,'clicks do not toggle in Polygon mode');assert.equal(map.polygonUnder({x:190,y:90}),-1);map.setPolygonMode(false);
saveResult=false;map.togglePolygonAt(at(190,90));await settle();assert.deepEqual(map.polygons,[first,second],'a failed save leaves the polygon as it was');saveResult=true;
map.dragging={id:1,x:10,y:10,press:{x:10,y:10}};map.pressMoved(at(12,11));assert.ok(map.dragging.press,'a small wobble is still a click');
map.pressMoved(at(10,10+polygonClickSlop));assert.equal(map.dragging.press,null,'a drag is a pan, not a click');map.dragging=null;
map.hoverPolygon=true;map.updateCursor({});assert.equal(host.style.cursor,'pointer','the cursor shows that a polygon can be clicked');map.hoverPolygon=false;map.updateCursor({});assert.equal(host.style.cursor,'grab');
// Polygons are GM-only: nothing about them, finished or in progress, reaches the player snapshot.
let published;map.presentation={publish:state=>published=state};
map.characterTokens=new Map([[1,{id:1,characterId:1,column:0,row:0,isVisible:true,name:'Hero',isNpc:false}]]);
map.polygons=[triangle,square];map.polygonDraft=[point(0,0),point(2,0)];map.polygonCursor=point(2,2);
map.publishPresentation();
assert.ok(published && validBattlemapSnapshot(published));assert.equal(published.tokens.length,1);
assert.equal(Object.keys(published).some(key=>/polygon/i.test(key)),false,'no snapshot field carries polygons');
assert.equal(/polygon|"points"/i.test(JSON.stringify(published)),false,'polygon data never reaches players');
const player=new Battlemap({...host,style:{}},scene,'polygons-player');
Object.assign(player,{readOnly:true,view:{x:0,y:0,scale:1},imageWidth:250,imageHeight:180,polygonGraphics:recorder(),polygons:[triangle]});
player.setPolygonMode(true);assert.notEqual(player.polygonMode,true,'the player view has no Polygon mode');
player.drawPolygons();assert.deepEqual(player.polygonGraphics.ops,[],'the player view never draws polygons');
player.polygonMode=true;player.polygonClick(at(10,10));assert.equal(player.polygonDraft,null);player.polygonDown(at(100,50,2));await player.undoPolygon();assert.deepEqual(player.polygons,[triangle]);
player.polygonMode=false;player.callback={invokeMethodAsync:()=>assert.fail('the player view never saves')};player.togglePolygonAt(at(100,50));assert.equal(player.polygonUnder({x:100,y:50}),-1,'and cannot open or close polygons');
// A new grid size discards polygons, as the server does, even when a save is still in flight.
map.characterTokens=new Map();map.polygonDraft=[point(0,0)];map.polygonHistory=[[]];
map.setGrid({gridSize:100});assert.equal(map.polygons.length,2,'grid styling keeps polygons');
map.setGrid({gridSize:50});assert.deepEqual(map.polygons,[]);assert.equal(map.polygonDraft,null);assert.deepEqual(map.polygonHistory,[]);
let finish;map.callback.invokeMethodAsync=()=>new Promise(resolve=>finish=resolve);
map.polygons=[triangle];const pending=map.savePolygons([]);
map.setGrid({gridSize:100});finish(false);await pending;
assert.deepEqual(map.polygons,[],'a save that fails after the grid changed does not bring polygons back');assert.deepEqual(map.polygonHistory,[]);assert.equal(map.polygonSaving,false);
console.log('Polygon mode clicks and pans, drawing, closing, right-click removal, Esc, undo, save rollback, red and blue drawing styles, click to open or close, mode exclusivity, GM-only privacy and grid reset passed.');
