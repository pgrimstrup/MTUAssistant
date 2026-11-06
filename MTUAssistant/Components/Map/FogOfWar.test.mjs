import assert from 'node:assert/strict';
import {fogKey,fogCells,fogVisible,validFog,paintFog,fogRectangles,fogPublicState} from '../../wwwroot/js/FogOfWar.js';
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
map.readOnly=true;map.fogMode='Navigate';map.setFogTool('Reveal',9);assert.equal(map.fogMode,'Navigate');
console.log('Fog painting, interpolation, clipping, rectangle coverage, privacy, sync, cancellation, save rollback, undo and read-only guards passed.');
