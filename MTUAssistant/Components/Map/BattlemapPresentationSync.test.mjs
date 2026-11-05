import assert from 'node:assert/strict';
import {playerTokens,validBattlemapSnapshot,connectBattlemapPresentation} from '../../wwwroot/js/BattlemapPresentationSync.js';
const visible={id:1,characterId:10,name:'Mercenary 1',isNpc:true,hasTokenImage:false,isVisible:true,column:2,row:3,container:{secret:true}};
const hidden={...visible,id:2,name:'Hidden captain',isVisible:false};
const offMap={...visible,id:3,column:null,row:null};
const tokens=playerTokens([visible,hidden,offMap]);
assert.equal(tokens.length,1); assert.equal(tokens[0].container,undefined);
assert.ok(!JSON.stringify(tokens).includes('Hidden captain'));
const state={scene:{id:1,campaignId:1,revision:1,gridSize:100,gridColour:'#ffffff',gridOpacity:50,gridlineWidth:2},
    view:{x:-100,y:20,scale:2,width:800,height:600,imageWidth:2000,imageHeight:1200},tokens};
assert.ok(validBattlemapSnapshot(state)); assert.ok(validBattlemapSnapshot(null));
assert.equal(validBattlemapSnapshot({...state,tokens:[hidden]}),false);
assert.equal(validBattlemapSnapshot({...state,view:{...state.view,scale:0}}),false);
const ports=[],values=new Map();
const storage={getItem:key=>values.get(key) ?? null,setItem:(key,value)=>values.set(key,value)};
const createChannel=name=>{
    const port={name,onmessage:null,closed:false,postMessage(data){
        for(const other of ports) if(other!==port && other.name===name && !other.closed) other.onmessage?.({data});
    },close(){this.closed=true;}};
    ports.push(port); return port;
};
const session='battlemap-test-session-1234';
const admin=connectBattlemapPresentation({session,readOnly:false,storage,createChannel});
admin.publish(state);
const received=[];
const player=connectBattlemapPresentation({session,readOnly:true,storage,createChannel,onState:s=>received.push(s)});
assert.deepEqual(received.at(-1),state,'late presentation receives the cached/current map');
const other=[];
const isolated=connectBattlemapPresentation({session:'another-admin-session-1234',readOnly:true,storage,createChannel,onState:s=>other.push(s)});
admin.publish({...state,tokens:[]});
assert.equal(received.at(-1).tokens.length,0,'hiding a token removes it from player state');
assert.ok(!other.some(Boolean),'sessions remain isolated');
const count=received.length; player.publish(state); assert.equal(received.length,count,'player view cannot publish changes');
admin.publish(null); assert.equal(received.at(-1),null,'closing the scene clears the player view');
player.close(); admin.close(); isolated.close();
console.log('Battlemap public filtering, read-only sync, late join, visibility, close and session isolation checks passed.');

const unconsciousTokens=playerTokens([{...visible,isUnconscious:true},{...hidden,isUnconscious:true}]);
assert.equal(unconsciousTokens.length,1);assert.equal(unconsciousTokens[0].isUnconscious,true,"Cast shares unconscious status only for visible tokens");
const statusTokens=playerTokens([{...visible,isCurrent:true,isDead:true,currentHp:-2},{...hidden,isCurrent:true,isDead:true}]);
assert.equal(statusTokens.length,1);
assert.equal(statusTokens[0].isCurrent,true); assert.equal(statusTokens[0].isDead,true);
assert.equal(statusTokens[0].currentHp,undefined,'casting shares only visual status, not HP totals');
assert.equal(playerTokens([{...hidden,isCurrent:true}]).length,0,'hidden current turns are never disclosed');
console.log('Casting shares turn/death indicators only for visible tokens.');

const pointerAdmin=connectBattlemapPresentation({session,readOnly:false,storage,createChannel});
let pointer;
const pointerPlayer=connectBattlemapPresentation({session,readOnly:true,storage,createChannel,onPointer:p=>pointer=p});
pointerAdmin.publishPointer({x:100,y:200});assert.deepEqual(pointer,{x:100,y:200});
pointerPlayer.publishPointer({x:1,y:2});assert.deepEqual(pointer,{x:100,y:200},'players cannot publish the pointer');
pointerAdmin.publishPointer({x:Infinity,y:0});assert.deepEqual(pointer,{x:100,y:200},'invalid pointer coordinates are ignored');
let latePointer;
const pointerLate=connectBattlemapPresentation({session,readOnly:true,storage,createChannel,onPointer:p=>latePointer=p});
assert.deepEqual(latePointer,{x:100,y:200},'late casts receive the live pointer');
pointerAdmin.publish(state);
assert.equal(JSON.parse(storage.getItem('mtu.battlemap.presentation.'+session)).pointer,undefined,'pointer positions are never stored in map snapshots');
pointerAdmin.publishPointer(null);assert.equal(pointer,null,'leaving the canvas hides the pointer');
pointerAdmin.publishPointer({x:10,y:20});pointerAdmin.close();assert.equal(pointer,null,'disposing the Admin renderer clears the pointer');
pointerPlayer.close();pointerLate.close();
console.log('Battlemap live pointer, read-only protection, late join, storage and cleanup passed.');

const {playerMovementRanges}=await import('../../wwwroot/js/BattlemapPresentationSync.js');
const ranges=playerMovementRanges([{id:1,column:2,row:3,diameter:4.5,colour:'#87cefa',isVisible:true},{id:2,column:4,row:5,diameter:37.5,colour:'#3388ff',isVisible:false}]);
assert.equal(ranges.length,1);assert.equal(ranges[0].id,1);
assert.ok(validBattlemapSnapshot({...state,movementRanges:ranges}));
assert.equal(validBattlemapSnapshot({...state,movementRanges:[{...ranges[0],diameter:Infinity}]}),false);
console.log('Cast movement range filtering and validation passed.');
for(const diameter of [4.5,7.5,10.5,13.5,19.5,25.5,37.5]) assert.ok(validBattlemapSnapshot({...state,movementRanges:[{...ranges[0],diameter}]}),'cast accepts each Move cycle diameter');
