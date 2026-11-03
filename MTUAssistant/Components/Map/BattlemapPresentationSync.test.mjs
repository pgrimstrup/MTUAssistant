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
