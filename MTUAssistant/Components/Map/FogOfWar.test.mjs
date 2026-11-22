import assert from 'node:assert/strict';
import {fogKey,fogCells,fogVisible,validFog,paintFog,fogRectangles,fogPublicState,fogStrokeMode,stepFogBrush,fogEqual,fogGradientSquares,fogGradientLead,fogGradientSamples,fogGradientField} from '../../wwwroot/js/FogOfWar.js';
import {Battlemap} from '../../wwwroot/js/Battlemap.js';
import {validBattlemapSnapshot} from '../../wwwroot/js/BattlemapPresentationSync.js';
const revealed=new Set();
assert.equal(fogVisible(revealed,0,0),false);
assert.deepEqual(fogRectangles(revealed,250,180,100),[{x:0,y:0,width:250,height:180}]);
paintFog(revealed,{column:0,row:0},{column:2,row:1},1,true,3,2);
assert.ok(revealed.has('1,1'),'fast drag interpolates through cells');
paintFog(revealed,{column:0,row:0},{column:2,row:1},3,true,3,2);
assert.equal(revealed.size,6,'brush clips to image bounds');
assert.deepEqual(fogRectangles(revealed,250,180,100),[],'fully revealed partial edge cells leave no fog');
paintFog(revealed,{column:1,row:0},{column:1,row:0},1,false,3,2);
assert.deepEqual(fogRectangles(revealed,250,180,100),[{x:100,y:0,width:100,height:100}]);
assert.ok(validFog(fogCells(revealed)));assert.equal(validFog([{column:-1,row:0}]),false);
// Each pixel is covered exactly once when concealed, never when revealed, including partial cells.
for(let seed=0;seed<32;seed++) {
    const keys=new Set(Array.from({length:6},(_,i)=>i).filter(i=>seed & (1<<i)).map(i=>fogKey(i%3,Math.floor(i/3))));
    const rectangles=fogRectangles(keys,25,18,10);
    for(let y=0;y<18;y++)for(let x=0;x<25;x++) {
        const count=rectangles.filter(r=>x>=r.x && x<r.x+r.width && y>=r.y && y<r.y+r.height).length;
        assert.equal(count,keys.has(fogKey(Math.floor(x/10),Math.floor(y/10))) ? 0 : 1);
    }
}
const actor={id:1,characterId:1,column:0,row:0,isVisible:true,name:'Hero',isNpc:false};
const enemy={id:2,characterId:2,column:2,row:0,isVisible:true,name:'Secret guard',isNpc:true};
const map=new Battlemap({style:{},hasPointerCapture:()=>false},{id:1,campaignId:1,revision:1,gridSize:100,gridColour:'#000000',gridlineWidth:2,gridOpacity:50},'fog');
map.revealedFog=new Set(['0,0']);map.fogHistory=[];map.characterTokens=new Map([[1,actor],[2,enemy]]);
map.playerTracker=[actor,enemy];map.actionSummary={id:1,rows:[{label:'Target',value:'Secret guard'}]};map.targetIds=[2];map.description={id:2,name:'Secret guard'};
map.movementTrails=new Map([[1,{id:1,fromColumn:1,fromRow:0,toColumn:0,toRow:0}]]);
map.movementRanges=[{id:1,column:1,row:0}];
const publicState=fogPublicState(map);
assert.deepEqual(publicState.tokens,[actor]);assert.deepEqual(publicState.tracker,[actor]);
assert.equal(publicState.description,null);assert.equal(publicState.summary,null);
assert.deepEqual(publicState.trails,[]);assert.deepEqual(publicState.ranges,[]);
assert.equal(JSON.stringify(publicState).includes('Secret guard'),false,'covered names never reach players');
map.description=null;map.playerTracker=[];map.movementRanges=[];map.movementTrails.clear();
map.view={x:0,y:0,scale:1};map.width=250;map.height=180;map.imageWidth=250;map.imageHeight=180;
let published;map.presentation={publish:state=>published=state};map.publishPresentation();
assert.equal(published.tokens.length,1);assert.equal(published.fog.length,1);assert.ok(validBattlemapSnapshot(published));
assert.equal(validBattlemapSnapshot({...published,fog:[{column:0.5,row:0}]}),false);
assert.equal(validBattlemapSnapshot({...published,fog:[]}),false,'covered token snapshots rejected');
map.fogStroke={id:1,before:new Set(['0,0'])};map.revealedFog.add('1,0');
map.publishPresentation();assert.equal(published.fog.length,1,'unsaved paint stays local');
map.cancelFogStroke();assert.deepEqual([...map.revealedFog],['0,0']);
globalThis.DotNet={createJSStreamReference:value=>value};
let savedPayload;
map.callback={invokeMethodAsync:async(name,...args)=>{assert.equal(name,'SaveFog');savedPayload=JSON.parse(new TextDecoder().decode(args.at(-1)));return true;}};
map.revealedFog.add('1,0');await map.saveFogEdit(new Set(['0,0']));
assert.equal(savedPayload.length,2);assert.equal(map.fogHistory.length,1);
await map.undoFog();assert.deepEqual([...map.revealedFog],['0,0']);assert.equal(map.fogHistory.length,0);
map.callback.invokeMethodAsync=async()=>false;map.revealedFog.add('1,0');await map.saveFogEdit(new Set(['0,0']));
assert.deepEqual([...map.revealedFog],['0,0'],'failed saves roll back');
assert.equal(fogStrokeMode({shiftKey:true}),'clear');assert.equal(fogStrokeMode({ctrlKey:true}),'set');
assert.equal(fogStrokeMode({}),null,'plain drag moves tokens or pans');assert.equal(fogStrokeMode({shiftKey:true,ctrlKey:true}),null,'both modifiers are ignored');
map.host={...map.host,getBoundingClientRect:()=>({left:0,top:0}),clientLeft:0,clientTop:0,releasePointerCapture(){}};
map.revealedFog=new Set(['0,0','1,0']);map.fogBrush=1;
map.fogStroke={id:1,before:new Set(map.revealedFog),last:null,reveal:false};map.paintFogAt({clientX:150,clientY:50});
assert.deepEqual([...map.revealedFog],['0,0'],'Ctrl strokes add fog');
map.fogStroke={id:1,before:new Set(map.revealedFog),last:null,reveal:true};map.paintFogAt({clientX:50,clientY:150});
assert.ok(map.revealedFog.has('0,1'),'Shift strokes clear fog');map.cancelFogStroke();
map.readOnly=true;map.setFogBrush(9);assert.equal(map.fogBrush,1,'read-only maps ignore brush changes');
map.readOnly=false;map.setFogBrush(9);assert.equal(map.fogBrush,9);map.setFogBrush(4);assert.equal(map.fogBrush,3,'invalid brush falls back to 3');
assert.equal(stepFogBrush(1,1),3);assert.equal(stepFogBrush(5,1),9);assert.equal(stepFogBrush(9,1),9,'largest brush stays at 9');
assert.equal(stepFogBrush(3,-1),1);assert.equal(stepFogBrush(1,-1),1,'smallest brush stays at 1');assert.equal(stepFogBrush(4,1),3);
const brushUpdates=[];map.callback={invokeMethodAsync:async(name,size)=>{assert.equal(name,'FogBrushChanged');brushUpdates.push(size);}};
map.fogBrush=3;map.fogWheelDelta=0;map.wheelFogBrush({deltaY:-100,deltaMode:0});assert.equal(map.fogBrush,5,'wheel up enlarges the brush');
map.wheelFogBrush({deltaY:-100,deltaMode:0});map.wheelFogBrush({deltaY:-100,deltaMode:0});assert.equal(map.fogBrush,9);
map.wheelFogBrush({deltaY:100,deltaMode:0});assert.equal(map.fogBrush,5,'wheel down shrinks the brush');
map.wheelFogBrush({deltaY:20,deltaMode:0});assert.equal(map.fogBrush,5,'small trackpad deltas accumulate');map.wheelFogBrush({deltaY:40,deltaMode:0});assert.equal(map.fogBrush,3);
map.wheelFogBrush({deltaY:0,deltaX:-100,deltaMode:0});assert.equal(map.fogBrush,5,'Shift+wheel horizontal deltas are used');
map.wheelFogBrush({deltaY:3,deltaMode:1});assert.equal(map.fogBrush,3,'line-mode deltas are scaled');
assert.deepEqual(brushUpdates,[5,9,5,3,5,3],'clamped steps are not reported');
map.readOnly=true;map.wheelFogBrush({deltaY:-100,deltaMode:0});assert.equal(map.fogBrush,3);map.readOnly=false;
map.fogStroke=null;map.fogSaving=false;map.revealedFog=new Set(['0,0']);map.fogHistory=[new Set()];map.description=null;
map.setFogEnabled(null);assert.equal(map.revealedFog,null);assert.equal(map.fogHistory.length,0,'turning fog off clears undo');
assert.equal(published.fog,null,'players see no fog when it is off');assert.equal(published.tokens.length,2,'all visible tokens show without fog');assert.ok(validBattlemapSnapshot(published));
map.setFogEnabled([{column:0,row:0}]);assert.deepEqual([...map.revealedFog],['0,0']);assert.equal(published.fog.length,1);assert.equal(published.tokens.length,1,'fog hides covered tokens again');
let finishSave;map.callback={invokeMethodAsync:()=>new Promise(resolve=>finishSave=resolve)};
map.revealedFog.add('1,0');const pending=map.saveFogEdit(new Set(['0,0']));map.setFogEnabled(null);finishSave(false);await pending;
assert.equal(map.revealedFog,null,'a save finishing after fog is turned off does not bring fog back');
map.readOnly=true;map.setFogEnabled([]);assert.equal(map.revealedFog,null,'read-only maps ignore the fog switch');map.readOnly=false;
console.log('Fog painting, interpolation, clipping, rectangle coverage, privacy, sync, cancellation, save rollback, undo, Shift/Ctrl stroke modes, brush, wheel brush sizing, fog on/off and read-only guards passed.');
// Fog gradient: darkness comes from the revealed squares alone.
assert.equal(fogGradientSquares,3);assert.equal(fogGradientLead,1);
assert.ok(fogEqual(null,null));assert.equal(fogEqual(null,new Set()),false);assert.ok(fogEqual(new Set(['0,0','1,0']),new Set(['1,0','0,0'])));
assert.equal(fogEqual(new Set(['0,0']),new Set(['1,0'])),false);assert.equal(fogEqual(new Set(['0,0']),new Set(['0,0','1,0'])),false);
assert.equal(fogGradientSamples(40,30),4);assert.equal(fogGradientSamples(300,200),3);assert.equal(fogGradientSamples(10,4096),1,'huge grids fall back to one texel per square');assert.equal(fogGradientSamples(0,0),4);
assert.equal(fogGradientField(null,3,3,4),null,'no gradient when fog is off');
assert.ok(fogGradientField(new Set(),4,3,2).alpha.every(a=>a === 255),'an unrevealed map is fully black');
const everySquare=new Set();for(let row=0;row<3;row++)for(let column=0;column<4;column++)everySquare.add(fogKey(column,row));
assert.ok(fogGradientField(everySquare,4,3,2).alpha.every(a=>a === 0),'a fully revealed map has no fog');
assert.ok(fogGradientField(new Set(['-1,1','4,0','0,3','0.5,0','x,y']),4,3,2).alpha.every(a=>a === 255),'squares off the grid reveal nothing');
// Darkness for a texel `edge` texels past the hard edge of the fog (negative on the revealed side).
const shade=(edge,samples=4,depth=3,lead=1)=>Math.round(255*Math.min(1,Math.max(0,(edge/samples+lead)/depth)));
// A revealed 3 x 3 block in the middle of a 15 x 15 map at 4 texels per square: texels 24-35 on each axis.
const block=new Set();for(let row=6;row<=8;row++)for(let column=6;column<=8;column++)block.add(fogKey(column,row));
const centre=fogGradientField(block,15,15,4), at=(x,y)=>centre.alpha[y*centre.width+x];
assert.equal(centre.width,60);assert.equal(centre.height,60);
for(let y=28;y<32;y++)for(let x=28;x<32;x++)assert.equal(at(x,y),0,'revealed squares a full square from the fog stay fully clear');
// Outwards from the hard edge into the fog, then inwards from it across the revealed block.
for(let k=1;k<=24;k++) {
    const expected=shade(k-.5);
    assert.equal(at(35+k,29),expected);assert.equal(at(24-k,30),expected);assert.equal(at(29,35+k),expected);assert.equal(at(30,24-k),expected);
}
for(let k=1;k<=6;k++) {
    const expected=shade(.5-k);
    assert.equal(at(36-k,29),expected);assert.equal(at(23+k,30),expected);assert.equal(at(29,36-k),expected);assert.equal(at(30,23+k),expected);
}
assert.ok(at(32,29)>0 && at(31,29) === 0,'the fade starts one square inside the revealed area');
assert.ok(at(35,29)<85 && at(36,29)>85,'the fade is a third dark where it crosses the hard edge');
assert.ok(at(43,29)<255,'the second hidden square is not yet solid');assert.equal(at(44,29),255,'fog is solid from two squares in');
assert.ok(at(42,29)<255 && at(42,42) === 255,'corners are rounded: seven texels along is dim, seven along and seven down is solid');
for(let y=0;y<60;y++)for(let x=0;x<60;x++){assert.equal(at(x,y),at(59-x,y));assert.equal(at(x,y),at(y,x));}
const lone=fogGradientField(new Set(['7,7']),15,15,4);
assert.equal(lone.alpha[29*60+29],shade(-1.5),'a lone revealed square is within a square of fog all over, so it is dimmed throughout');assert.equal(lone.alpha[29*60+28],shade(-.5));
const sooner=fogGradientField(block,15,15,4,3,1,.5);
assert.equal(sooner.alpha[29*60+39],Math.round(255*Math.sqrt((3.5/4+1)/3)),'a curve below 1 darkens sooner');assert.equal(sooner.alpha[29*60+29],0);assert.equal(sooner.alpha[29*60+44],255);
const unled=fogGradientField(new Set(['7,7']),15,15,4,5,0);
for(let y=28;y<32;y++)for(let x=28;x<32;x++)assert.equal(unled.alpha[y*60+x],0,'with no lead-in the revealed squares stay fully clear');
for(let k=1;k<=28;k++)assert.equal(unled.alpha[29*60+31+k],shade(k-.5,4,5,0),'with no lead-in the fade starts at the hard edge');
assert.deepEqual(fogGradientField(block,15,15,4,3,9),fogGradientField(block,15,15,4,3,3),'the lead-in is never longer than the fade');
// Every texel matches the straight-line distance across the nearest hard edge, on sparse and crowded maps.
const nearestShade=(keys,columns,rows,samples,depth,lead)=>{
    const width=columns*samples, height=rows*samples, clear=new Uint8Array(width*height);
    for(let y=0;y<height;y++)for(let x=0;x<width;x++)if(keys.has(fogKey(Math.floor(x/samples),Math.floor(y/samples))))clear[y*width+x]=1;
    return Array.from(clear,(own,i)=>{
        const x=i%width, y=Math.floor(i/width);
        let nearest=Infinity;
        for(let sy=0;sy<height;sy++)for(let sx=0;sx<width;sx++)if(clear[sy*width+sx] !== own)nearest=Math.min(nearest,(sx-x)*(sx-x)+(sy-y)*(sy-y));
        return shade(own ? .5-Math.sqrt(nearest) : Math.sqrt(nearest)-.5,samples,depth,lead);
    });
};
let random=12345;const next=()=>(random=(random*1103515245+12345)%2147483648)/2147483648;
for(const [samples,depth,lead,share] of [[1,3,1,.04],[2,3,1,.3],[3,3,1,.6],[4,3,1,.85],[4,5,0,.04],[4,2,.5,.5],[3,1.5,1.5,.7],[2,4,2,.9]]) {
    const keys=new Set();
    for(let row=0;row<14;row++)for(let column=0;column<18;column++)if(next()<share)keys.add(fogKey(column,row));
    assert.deepEqual([...fogGradientField(keys,18,14,samples,depth,lead).alpha],nearestShade(keys,18,14,samples,depth,lead),`${samples} texels per square, ${depth} squares wide, ${lead} inside`);
}
// The band where the map shows dimly is still hidden: a token one square into it never reaches players.
const lurker={...enemy,column:1};
map.revealedFog=new Set(['0,0']);map.characterTokens=new Map([[1,actor],[2,lurker]]);
assert.ok(fogGradientField(map.revealedFog,3,2,4).alpha[1*12+5]<128,'the lurker stands where the map still shows through the fade');
assert.deepEqual(fogPublicState(map).tokens,[actor]);
map.publishPresentation();assert.equal(published.tokens.length,1);assert.deepEqual(published.fog,[{column:0,row:0}],'players are sent the revealed squares and nothing about the gradient');
assert.ok(validBattlemapSnapshot(published));
assert.equal(validBattlemapSnapshot({...published,tokens:[...published.tokens,{...published.tokens[0],id:2,characterId:2,column:1}]}),false,'tokens in the dim band are rejected');
// Rendering: players get the gradient sprite, the GM keeps hard squares.
const savedPixi=globalThis.PIXI, savedDocument=globalThis.document;
let uploads=0, canvases=0;
globalThis.document={createElement:()=>{canvases++;return {width:0,height:0,getContext:()=>({createImageData:(w,h)=>({data:new Uint8ClampedArray(w*h*4)}),putImageData(){}})};}};
globalThis.PIXI={CanvasSource:class{constructor(options){Object.assign(this,options);}update(){uploads++;}},
    Texture:class{constructor(options){Object.assign(this,options);}destroy(){this.destroyed=true;}},
    Rectangle:class{constructor(x,y,width,height){Object.assign(this,{x,y,width,height});}},
    Sprite:class{constructor(texture){this.texture=texture;this.visible=true;}destroy(){this.destroyed=true;}}};
const fogView=(readOnly,fog)=>{
    const view=new Battlemap({style:{},hasPointerCapture:()=>false},{gridSize:100},'gradient');
    return Object.assign(view,{readOnly,imageWidth:250,imageHeight:180,revealedFog:fog,fog:{children:[],addChild(child){this.children.push(child);}},
        fogGraphics:{rects:[],fills:[],clear(){this.rects=[];this.fills=[];},rect(...rect){this.rects.push(rect);},fill(fill){this.fills.push(fill);}}});
};
const gm=fogView(false,new Set(['0,0']));gm.drawFog();
assert.equal(gm.fog.children.length,0,'the GM view has no gradient');assert.deepEqual(gm.fogGraphics.fills,[{color:0,alpha:.55}]);assert.equal(gm.fogGraphics.rects.length,2,'the GM keeps hard squares');
const player=fogView(true,new Set(['0,0']));player.drawFog();
const sprite=player.fog.children[0], pixels=()=>player.fogGradient.image.data;
assert.equal(player.fog.children.length,1);assert.deepEqual(player.fogGraphics.rects,[],'players get the gradient instead of hard squares');assert.deepEqual(player.fogGraphics.fills,[]);
assert.equal(sprite.width,250);assert.equal(sprite.height,180);assert.equal(sprite.texture.source.scaleMode,'linear');
assert.deepEqual({...sprite.texture.frame},{x:0,y:0,width:10,height:7.2},'the gradient is cropped to the image');
assert.equal(player.fogGradient.canvas.width,12);assert.equal(player.fogGradient.canvas.height,8);
assert.ok(pixels().every((value,i)=>i%4 === 3 || value === 0),'fog is black');
assert.equal(pixels()[3],shade(-3.5),'the revealed square is nearly clear away from the fog');assert.equal(pixels()[3*4+3],shade(-.5));
assert.equal(pixels()[4*4+3],shade(.5),'hidden squares darken with distance');assert.equal(pixels()[11*4+3],shade(7.5));
assert.equal(uploads,1);player.setFog([{column:0,row:0}]);assert.equal(uploads,1,'unchanged fog is not redrawn');
player.setFog([{column:0,row:0},{column:1,row:0}]);assert.equal(uploads,2);assert.equal(canvases,1,'the texture is reused when the squares change');assert.equal(pixels()[4*4+3],shade(-3.5),'a newly revealed square clears');
player.setFog(null);assert.equal(sprite.visible,false,'no gradient when fog is off');assert.deepEqual(player.fogGraphics.rects,[]);
player.setFog([{column:1,row:0},{column:0,row:0}]);assert.equal(sprite.visible,true);assert.equal(uploads,2);
player.setFog([]);assert.equal(uploads,3);assert.ok(pixels().every((value,i)=>i%4 !== 3 || value === 255),'re-fogged squares go solid at once');
player.setGrid({gridSize:50});assert.ok(sprite.destroyed && sprite.texture.destroyed,'a new grid size replaces the texture');
assert.equal(player.fog.children.length,2);assert.equal(player.fogGradient.canvas.width,20);assert.equal(player.fogGradient.canvas.height,16);
assert.deepEqual({...player.fogGradient.texture.frame},{x:0,y:0,width:20,height:14.4});
// If the gradient cannot be drawn, players get solid fog rather than none.
globalThis.PIXI={...globalThis.PIXI,Sprite:class{constructor(){throw Error('No sprite.');}}};
const warn=console.warn, warnings=[];console.warn=message=>warnings.push(message);
const broken=fogView(true,new Set(['0,0']));broken.drawFog();broken.drawFog();
console.warn=warn;
assert.equal(warnings.length,1,'a failed gradient is not retried');assert.equal(broken.fog.children.length,0);
assert.deepEqual(broken.fogGraphics.fills,[{color:0,alpha:1}]);assert.equal(broken.fogGraphics.rects.length,2,'players fall back to solid squares');
globalThis.PIXI=savedPixi;globalThis.document=savedDocument;
console.log('Fog gradient distance, width, lead-in inside the revealed area, rounded corners, curve, off-grid squares, band privacy, player-only rendering, caching, grid change and solid fallback passed.');
